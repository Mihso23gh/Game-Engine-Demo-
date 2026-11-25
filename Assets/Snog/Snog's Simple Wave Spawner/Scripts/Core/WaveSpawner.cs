
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

using Snog.SimpleWaveSystem.Data;

namespace Snog.SimpleWaveSystem.Core
{
    public class WaveSpawner : MonoBehaviour
    {
        [Serializable]
        public class IntUnityEvent : UnityEvent<int> { }

        [Serializable]
        public class GameObjectUnityEvent : UnityEvent<GameObject> { }

        public enum SpawnMode
        {
            RoundRobin,
            Random,
            WeightedRandom,
            FarthestFromTarget,
            ClosestToTarget,
            OutsideCamera,
            NavMeshValid
        }

        public enum SpawnShape
        {
            PredefinedTransforms,
            RandomInBox,
            RingAroundTarget
        }

        [Header("Wave")]
        [Min(1)]
        public int currentWave = 1;

        [SerializeField] private WaveDefinition waveDefinition;

        [Tooltip("Used only if WaveDefinition.allowedEnemies is empty.")]
        [SerializeField] private List<EnemyDefinition> fallbackEnemies = new();

        [Header("Spawn Selection")]
        [SerializeField] private SpawnMode spawnMode = SpawnMode.RoundRobin;
        [SerializeField] private SpawnShape spawnShape = SpawnShape.PredefinedTransforms;

        [SerializeField] private Transform target;
        [SerializeField] private Camera referenceCamera;

        [Header("Spawn Shape: Predefined Transforms")]
        [SerializeField] private List<Transform> spawnPoints = new();

        [Header("Spawn Shape: Random In Box")]
        [SerializeField] private Bounds spawnBox = new Bounds(Vector3.zero, new Vector3(20f, 0f, 20f));

        [Header("Spawn Shape: Ring Around Target")]
        [SerializeField] private float ringInnerRadius = 8f;
        [SerializeField] private float ringOuterRadius = 12f;

        [Header("NavMesh (Optional)")]
        [SerializeField] private bool useNavMeshSampling = false;
        [SerializeField] private float navMeshSampleRadius = 2f;

        [Header("Pooling (Optional)")]
        [SerializeField] private bool usePooling = false;
        [SerializeField] private SimplePool pool;
        [SerializeField] private bool poolAutoExpand = true;

        [Tooltip("If true, inactive objects are treated as 'dead' (recommended when pooling).")]
        [SerializeField] private bool treatInactiveAsDead = true;

        [Header("Events")]
        public IntUnityEvent onWaveStarted;
        public IntUnityEvent onWaveEnded;
        public GameObjectUnityEvent onEnemySpawned;
        public UnityEvent onAllEnemiesDefeated;
        public event Action<int> OnWaveStarted;
        public event Action<int> OnWaveEnded;
        public event Action<GameObject> OnEnemySpawned;
        public event Action OnAllEnemiesDefeated;

        [Header("Runtime (Read Only)")]
        [SerializeField] private List<GameObject> spawnedEnemies = new();

        private readonly List<GameObject> enemiesToSpawn = new();

        private int spawnIndex;
        private float waveTimer;
        private float waveDuration;
        private float spawnTimer;

        private bool allDefeatedRaisedThisWave;

        private void Start()
        {
            if (waveDefinition == null)
            {
                Debug.LogWarning($"{nameof(WaveSpawner)}: No WaveDefinition assigned. Disabling spawner.");
                enabled = false;
                return;
            }

            if (spawnShape == SpawnShape.PredefinedTransforms && (spawnPoints == null || spawnPoints.Count == 0))
            {
                Debug.LogWarning($"{nameof(WaveSpawner)}: SpawnShape is PredefinedTransforms but no spawnPoints assigned. Disabling spawner.");
                enabled = false;
                return;
            }

            if (usePooling)
            {
                if (pool == null)
                {
                    GameObject go = new GameObject("SimplePool");
                    pool = go.AddComponent<SimplePool>();
                }

                pool.SetAutoExpand(poolAutoExpand);
            }

            if (referenceCamera == null)
            {
                referenceCamera = Camera.main;
            }

            GenerateWave();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            CleanupSpawnedEnemies();

            waveTimer -= dt;
            spawnTimer -= dt;

            int maxAlive = waveDefinition.GetMaxAliveEnemies(currentWave);

            if (spawnTimer <= 0f)
            {
                bool canSpawn = spawnedEnemies.Count < maxAlive;

                if (canSpawn && enemiesToSpawn.Count > 0)
                {
                    SpawnEnemy();
                    ResetSpawnTimer();
                }
                else if (enemiesToSpawn.Count <= 0 && waveDefinition.endEarlyWhenSpawnQueueEmpty)
                {
                    waveTimer = 0f;
                }
            }

            bool spawnQueueEmpty = enemiesToSpawn.Count <= 0;
            bool noneAlive = spawnedEnemies.Count <= 0;

            if (spawnQueueEmpty && noneAlive && !allDefeatedRaisedThisWave)
            {
                allDefeatedRaisedThisWave = true;
                RaiseAllEnemiesDefeated();
            }

            if (waveTimer <= 0f && spawnQueueEmpty && noneAlive)
            {
                EndWave();
                currentWave++;
                GenerateWave();
            }
        }

        private void CleanupSpawnedEnemies()
        {
            if (treatInactiveAsDead)
            {
                spawnedEnemies.RemoveAll(e => e == null || !e.activeInHierarchy);
            }
            else
            {
                spawnedEnemies.RemoveAll(e => e == null);
            }
        }

        private void GenerateWave()
        {
            allDefeatedRaisedThisWave = false;

            waveDuration = waveDefinition.GetDuration(currentWave);
            waveTimer = waveDuration;

            int budget = waveDefinition.GetBudget(currentWave);
            int cap = waveDefinition.GetMaxEnemies(currentWave);

            enemiesToSpawn.Clear();

            bool isBossWave = waveDefinition.GetSpecialWaveType(currentWave) == WaveDefinition.SpecialWaveType.Boss;

            if (isBossWave)
            {
                TryConsumeBossBudget(ref budget);
            }

            GenerateEnemiesFromBudget(budget, cap);

            if (isBossWave)
            {
                TryAddBossToQueue();
            }

            spawnTimer = 0f;

            RaiseWaveStarted(currentWave);
        }

        private void EndWave()
        {
            RaiseWaveEnded(currentWave);
        }


        private void GenerateEnemiesFromBudget(int budget, int cap)
        {
            List<EnemyDefinition> normalPool = GetEnemyPoolForWave(currentWave);
            List<EnemyDefinition> elitePool = GetElitePoolForWave(currentWave);

            float eliteChance = waveDefinition.GetEliteChance(currentWave);
            float costBias = waveDefinition.GetCostBias(currentWave);

            int safetyIterations = cap * 12;
            int iterations = 0;

            while (budget > 0 && enemiesToSpawn.Count < cap && iterations < safetyIterations)
            {
                iterations++;

                bool useElite = elitePool.Count > 0 && UnityEngine.Random.value < eliteChance;

                EnemyDefinition chosen = null;

                if (useElite)
                {
                    chosen = ChooseWeightedEnemy(elitePool, budget, costBias);
                }

                if (chosen == null)
                {
                    chosen = ChooseWeightedEnemy(normalPool, budget, costBias);
                }

                if (chosen == null)
                {
                    break;
                }

                enemiesToSpawn.Add(chosen.prefab);
                budget -= chosen.cost;
            }
        }

        private void TryConsumeBossBudget(ref int budget)
        {
            if (!waveDefinition.enableBossWaves)
            {
                return;
            }

            if (waveDefinition.bossEnemy == null || waveDefinition.bossEnemy.prefab == null)
            {
                return;
            }

            if (!waveDefinition.bossConsumesBudget)
            {
                return;
            }

            int cost = Mathf.Max(1, waveDefinition.bossEnemy.cost);

            if (budget - cost < 0)
            {
                // Not enough budget. Keep budget unchanged and skip budget consumption.
                // Boss spawning will still be attempted (below) unless you want boss to depend on budget.
                return;
            }

            budget -= cost;
        }

        private void TryAddBossToQueue()
        {
            if (!waveDefinition.enableBossWaves)
            {
                return;
            }

            if (waveDefinition.bossEnemy == null || waveDefinition.bossEnemy.prefab == null)
            {
                Debug.LogWarning($"{nameof(WaveSpawner)}: Boss wave triggered but bossEnemy is not assigned.");
                return;
            }

            GameObject bossPrefab = waveDefinition.bossEnemy.prefab;

            if (waveDefinition.bossSpawnsAtStart)
            {
                enemiesToSpawn.Insert(0, bossPrefab);
            }
            else
            {
                enemiesToSpawn.Add(bossPrefab);
            }
        }

        private void ResetSpawnTimer()
        {
            float spawnRate = waveDefinition.GetSpawnRate(currentWave);

            float normalizedProgress = 1f;

            if (waveDuration > 0.0001f)
            {
                normalizedProgress = Mathf.Clamp01(1f - (waveTimer / waveDuration));
            }

            float pacingMultiplier = waveDefinition.GetSpawnPacingMultiplier(normalizedProgress);

            float effectiveRate = spawnRate * pacingMultiplier;
            float interval = 1f / Mathf.Max(0.01f, effectiveRate);

            spawnTimer = Mathf.Max(0.01f, interval);
        }

        private void SpawnEnemy()
        {
            if (enemiesToSpawn.Count <= 0)
            {
                return;
            }

            GameObject prefab = enemiesToSpawn[0];
            enemiesToSpawn.RemoveAt(0);

            if (prefab == null)
            {
                return;
            }

            Vector3 position = GetSpawnPosition();
            Quaternion rotation = Quaternion.identity;

            GameObject enemy = null;

            if (usePooling && pool != null)
            {
                enemy = pool.Get(prefab, position, rotation);
            }
            else
            {
                enemy = Instantiate(prefab, position, rotation);
            }

            if (enemy != null)
            {
                spawnedEnemies.Add(enemy);
                RaiseEnemySpawned(enemy);
            }
        }

        private List<EnemyDefinition> GetEnemyPoolForWave(int wave)
        {
            List<EnemyDefinition> source = waveDefinition.allowedEnemies;

            if (source == null || source.Count == 0)
            {
                source = fallbackEnemies;
            }

            List<EnemyDefinition> result = new();

            if (source == null)
            {
                return result;
            }

            for (int i = 0; i < source.Count; i++)
            {
                EnemyDefinition def = source[i];

                if (def != null && def.IsAvailableForWave(wave))
                {
                    result.Add(def);
                }
            }

            return result;
        }

        private List<EnemyDefinition> GetElitePoolForWave(int wave)
        {
            List<EnemyDefinition> source = waveDefinition.eliteEnemies;
            List<EnemyDefinition> result = new();

            if (source == null)
            {
                return result;
            }

            for (int i = 0; i < source.Count; i++)
            {
                EnemyDefinition def = source[i];

                if (def != null && def.IsAvailableForWave(wave))
                {
                    result.Add(def);
                }
            }

            return result;
        }

        private EnemyDefinition ChooseWeightedEnemy(List<EnemyDefinition> poolDefs, int remainingBudget, float costBias)
        {
            float totalWeight = 0f;

            for (int i = 0; i < poolDefs.Count; i++)
            {
                EnemyDefinition def = poolDefs[i];

                if (def.cost <= remainingBudget)
                {
                    totalWeight += GetBiasedWeight(def, costBias);
                }
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;

            for (int i = 0; i < poolDefs.Count; i++)
            {
                EnemyDefinition def = poolDefs[i];

                if (def.cost > remainingBudget)
                {
                    continue;
                }

                cumulative += GetBiasedWeight(def, costBias);

                if (roll <= cumulative)
                {
                    return def;
                }
            }

            return null;
        }

        private float GetBiasedWeight(EnemyDefinition def, float costBias)
        {
            float w = Mathf.Max(0f, def.weight);

            if (Mathf.Approximately(costBias, 0f))
            {
                return w;
            }

            float cost = Mathf.Max(1f, def.cost);

            // Positive bias => prefer expensive enemies.
            // Negative bias => prefer cheap enemies.
            if (costBias > 0f)
            {
                w *= Mathf.Pow(cost, costBias);
            }
            else
            {
                w *= Mathf.Pow(1f / cost, Mathf.Abs(costBias));
            }

            return Mathf.Max(0f, w);
        }

        private void RaiseWaveStarted(int wave)
        {
            OnWaveStarted?.Invoke(wave);
            onWaveStarted?.Invoke(wave);
        }

        private void RaiseWaveEnded(int wave)
        {
            OnWaveEnded?.Invoke(wave);
            onWaveEnded?.Invoke(wave);
        }

        private void RaiseEnemySpawned(GameObject enemy)
        {
            OnEnemySpawned?.Invoke(enemy);
            onEnemySpawned?.Invoke(enemy);
        }

        private void RaiseAllEnemiesDefeated()
        {
            OnAllEnemiesDefeated?.Invoke();
            onAllEnemiesDefeated?.Invoke();
        }

        // -------------------------------------------------------
        // Spawn position logic: keep whatever you already had.
        // The methods below are placeholders to avoid duplication.
        // -------------------------------------------------------

        private Vector3 GetSpawnPosition()
        {
            Vector3 pos = transform.position;

            if (spawnShape == SpawnShape.PredefinedTransforms)
            {
                pos = GetSpawnPositionFromPoints();
            }
            else if (spawnShape == SpawnShape.RandomInBox)
            {
                pos = GetRandomPointInBox();
            }
            else if (spawnShape == SpawnShape.RingAroundTarget)
            {
                pos = GetRandomPointInRing();
            }

            if (spawnMode == SpawnMode.NavMeshValid || useNavMeshSampling)
            {
                pos = SampleNavMesh(pos);
            }

            if (spawnMode == SpawnMode.OutsideCamera)
            {
                pos = EnsureOutsideCamera(pos);
            }

            return pos;
        }

        private Vector3 GetSpawnPositionFromPoints()
        {
            if (spawnPoints == null || spawnPoints.Count == 0)
            {
                return transform.position;
            }

            if (spawnMode == SpawnMode.RoundRobin)
            {
                Transform t = spawnPoints[spawnIndex];
                spawnIndex++;

                if (spawnIndex >= spawnPoints.Count)
                {
                    spawnIndex = 0;
                }

                return t.position;
            }

            int index = UnityEngine.Random.Range(0, spawnPoints.Count);
            return spawnPoints[index].position;
        }

        private Vector3 GetRandomPointInBox()
        {
            Vector3 center = spawnBox.center;
            Vector3 ext = spawnBox.extents;

            float x = UnityEngine.Random.Range(center.x - ext.x, center.x + ext.x);
            float y = center.y;
            float z = UnityEngine.Random.Range(center.z - ext.z, center.z + ext.z);

            return new Vector3(x, y, z);
        }

        private Vector3 GetRandomPointInRing()
        {
            Vector3 center = transform.position;

            if (target != null)
            {
                center = target.position;
            }

            float inner = Mathf.Min(ringInnerRadius, ringOuterRadius);
            float outer = Mathf.Max(ringInnerRadius, ringOuterRadius);

            float radius = UnityEngine.Random.Range(inner, outer);
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            return new Vector3(center.x + x, center.y, center.z + z);
        }

        private Vector3 SampleNavMesh(Vector3 desired)
        {
            if (NavMesh.SamplePosition(desired, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return desired;
        }

        private Vector3 EnsureOutsideCamera(Vector3 desired)
        {
            if (referenceCamera == null)
            {
                return desired;
            }

            Vector3 vp = referenceCamera.WorldToViewportPoint(desired);
            bool inFront = vp.z > 0f;
            bool onScreen = vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;

            if (!(inFront && onScreen))
            {
                return desired;
            }

            for (int i = 0; i < 8; i++)
            {
                Vector3 candidate = GetRandomPointInRing();
                Vector3 cVp = referenceCamera.WorldToViewportPoint(candidate);

                bool cInFront = cVp.z > 0f;
                bool cOnScreen = cVp.x >= 0f && cVp.x <= 1f && cVp.y >= 0f && cVp.y <= 1f;

                if (!(cInFront && cOnScreen))
                {
                    return candidate;
                }
            }

            return desired;
            
        }

        public WaveDefinition WaveDefinition => waveDefinition;
        public IReadOnlyList<EnemyDefinition> FallbackEnemies => fallbackEnemies;
        public SpawnShape CurrentSpawnShape => spawnShape;
        public SpawnMode CurrentSpawnMode => spawnMode;
        public IReadOnlyList<Transform> SpawnPoints => spawnPoints;
        public Bounds SpawnBox => spawnBox;
        public float RingInnerRadius => ringInnerRadius;
        public float RingOuterRadius => ringOuterRadius;
        public Transform Target => target;
        public Camera ReferenceCamera => referenceCamera;
        public bool UsePooling => usePooling;
        public SimplePool Pool => pool;
    }
}