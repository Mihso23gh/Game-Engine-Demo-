using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using Snog.SimpleWaveSystem.Core;
using Snog.SimpleWaveSystem.Data;

namespace Snog.SimpleWaveSystem.Editor
{

    [CustomEditor(typeof(WaveSpawner))]
    public class WaveSpawnerEditor : UnityEditor.Editor
    {
        private int previewWaveOffset = 1;
        private int previewSeed = 12345;

        public override void OnInspectorGUI()
        {
            WaveSpawner spawner = (WaveSpawner)target;

            DrawDefaultInspector();

            EditorGUILayout.Space(10f);
            DrawValidation(spawner);

            EditorGUILayout.Space(10f);
            DrawWavePreview(spawner);
        }

        private void DrawValidation(WaveSpawner spawner)
        {
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

            if (spawner.WaveDefinition == null)
            {
                EditorGUILayout.HelpBox("No WaveDefinition assigned. The spawner will not run.", MessageType.Error);
                return;
            }

            bool hasAllowed = spawner.WaveDefinition.allowedEnemies != null && spawner.WaveDefinition.allowedEnemies.Count > 0;
            bool hasFallback = spawner.FallbackEnemies != null && spawner.FallbackEnemies.Count > 0;

            if (!hasAllowed && !hasFallback)
            {
                EditorGUILayout.HelpBox("No enemies configured. Add enemies to WaveDefinition.allowedEnemies or WaveSpawner.fallbackEnemies.", MessageType.Error);
            }

            if (spawner.CurrentSpawnShape == WaveSpawner.SpawnShape.PredefinedTransforms)
            {
                if (spawner.SpawnPoints == null || spawner.SpawnPoints.Count == 0)
                {
                    EditorGUILayout.HelpBox("SpawnShape is PredefinedTransforms but Spawn Points is empty.", MessageType.Error);
                }
            }

            if (spawner.CurrentSpawnShape == WaveSpawner.SpawnShape.RingAroundTarget)
            {
                if (spawner.Target == null)
                {
                    EditorGUILayout.HelpBox("SpawnShape is RingAroundTarget but Target is not assigned.", MessageType.Warning);
                }

                if (spawner.RingOuterRadius <= 0f || spawner.RingInnerRadius < 0f)
                {
                    EditorGUILayout.HelpBox("Ring radii look invalid. Ensure InnerRadius >= 0 and OuterRadius > 0.", MessageType.Warning);
                }
            }

            if (spawner.CurrentSpawnMode == WaveSpawner.SpawnMode.OutsideCamera)
            {
                if (spawner.ReferenceCamera == null)
                {
                    EditorGUILayout.HelpBox("SpawnMode is OutsideCamera but Reference Camera is not assigned (Camera.main fallback only works at runtime).", MessageType.Warning);
                }
            }

            if (spawner.UsePooling)
            {
                if (spawner.Pool == null)
                {
                    EditorGUILayout.HelpBox("Pooling is enabled but no SimplePool is assigned. Spawner may create one at runtime, but for a package it's better to assign one.", MessageType.Info);
                }
            }
        }

        private void DrawWavePreview(WaveSpawner spawner)
        {
            EditorGUILayout.LabelField("Wave Preview", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Preview Tools", EditorStyles.miniBoldLabel);

                previewWaveOffset = EditorGUILayout.IntField("Wave Offset", previewWaveOffset);
                previewSeed = EditorGUILayout.IntField("Preview Seed", previewSeed);

                int wave = Mathf.Max(1, spawner.currentWave + previewWaveOffset);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Generate Next Wave Preview"))
                    {
                        WavePreviewResult result = WavePreviewUtility.GeneratePreview(spawner, wave, previewSeed);
                        WavePreviewUtility.DrawResult(result);
                    }

                    if (GUILayout.Button("Preview Current Wave"))
                    {
                        WavePreviewResult result = WavePreviewUtility.GeneratePreview(spawner, spawner.currentWave, previewSeed);
                        WavePreviewUtility.DrawResult(result);
                    }
                }

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField($"Target Preview Wave: {wave}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Tip: Adjust curves in WaveDefinition and re-run preview.", EditorStyles.miniLabel);
            }
        }

        private class WavePreviewResult
        {
            public int wave;
            public float duration;
            public int budget;
            public float spawnRate;
            public int maxEnemies;
            public int maxAlive;
            public float eliteChance;
            public WaveDefinition.SpecialWaveType specialType;

            public bool bossWave;
            public bool bossSpawnsAtStart;
            public bool bossConsumesBudget;
            public int bossCost;
            public bool bossAssigned;

            public int availableNormalCount;
            public int availableEliteCount;

            public int estimatedCountByMinCost;
            public int sampleGeneratedCount;
            public string warnings;
        }

        private static class WavePreviewUtility
        {
            public static WavePreviewResult GeneratePreview(WaveSpawner spawner, int wave, int seed)
            {
                WavePreviewResult r = new WavePreviewResult();
                r.wave = wave;

                WaveDefinition def = spawner.WaveDefinition;

                if (def == null)
                {
                    r.warnings = "No WaveDefinition assigned.";
                    return r;
                }

                r.duration = def.GetDuration(wave);
                r.budget = def.GetBudget(wave);
                r.spawnRate = def.GetSpawnRate(wave);
                r.maxEnemies = def.GetMaxEnemies(wave);
                r.maxAlive = def.GetMaxAliveEnemies(wave);
                r.eliteChance = def.GetEliteChance(wave);
                r.specialType = def.GetSpecialWaveType(wave);

                r.bossWave = r.specialType == WaveDefinition.SpecialWaveType.Boss && def.enableBossWaves;
                r.bossSpawnsAtStart = def.bossSpawnsAtStart;
                r.bossConsumesBudget = def.bossConsumesBudget;
                r.bossAssigned = def.bossEnemy != null && def.bossEnemy.prefab != null;
                r.bossCost = def.bossEnemy != null ? Mathf.Max(1, def.bossEnemy.cost) : 0;

                List<EnemyDefinition> normalPool = BuildNormalPool(spawner, wave);
                List<EnemyDefinition> elitePool = BuildElitePool(def, wave);

                r.availableNormalCount = normalPool.Count;
                r.availableEliteCount = elitePool.Count;

                int minCost = FindMinCost(normalPool, elitePool);
                if (minCost <= 0)
                {
                    r.estimatedCountByMinCost = 0;
                }
                else
                {
                    int bossExtra = r.bossWave && r.bossAssigned ? 1 : 0;
                    int estimated = Mathf.Min(r.maxEnemies, r.budget / minCost);
                    r.estimatedCountByMinCost = estimated + bossExtra;
                }

                r.sampleGeneratedCount = SimulateOneWave(def, normalPool, elitePool, wave, seed);

                r.warnings = BuildWarnings(r, def, spawner, minCost);

                return r;
            }

            public static void DrawResult(WavePreviewResult r)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Preview Result", EditorStyles.boldLabel);

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField($"Wave: {r.wave}");
                    EditorGUILayout.LabelField($"Special Type: {r.specialType}");
                    EditorGUILayout.LabelField($"Duration: {r.duration:0.00}s");
                    EditorGUILayout.LabelField($"Budget: {r.budget}");
                    EditorGUILayout.LabelField($"Spawn Rate: {r.spawnRate:0.00} spawns/sec");
                    EditorGUILayout.LabelField($"Max Enemies (cap): {r.maxEnemies}");
                    EditorGUILayout.LabelField($"Max Alive (concurrency): {(r.maxAlive == int.MaxValue ? "Unlimited" : r.maxAlive.ToString())}");
                    EditorGUILayout.LabelField($"Elite Chance: {(r.eliteChance * 100f):0.0}%");

                    EditorGUILayout.Space(6f);
                    EditorGUILayout.LabelField("Pools", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField($"Normal available: {r.availableNormalCount}");
                    EditorGUILayout.LabelField($"Elite available: {r.availableEliteCount}");

                    EditorGUILayout.Space(6f);
                    EditorGUILayout.LabelField("Enemy Count Estimate", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField($"Estimated (Budget/MinCost + Boss): {r.estimatedCountByMinCost}");
                    EditorGUILayout.LabelField($"Sample Simulation (seeded): {r.sampleGeneratedCount}");

                    if (!string.IsNullOrEmpty(r.warnings))
                    {
                        EditorGUILayout.Space(6f);
                        EditorGUILayout.HelpBox(r.warnings, MessageType.Warning);
                    }
                }
            }

            private static List<EnemyDefinition> BuildNormalPool(WaveSpawner spawner, int wave)
            {
                List<EnemyDefinition> source = spawner.WaveDefinition.allowedEnemies;

                if (source == null || source.Count == 0)
                {
                    source = new List<EnemyDefinition>();

                    IReadOnlyList<EnemyDefinition> fallback = spawner.FallbackEnemies;

                    if (fallback != null)
                    {
                        for (int i = 0; i < fallback.Count; i++)
                        {
                            source.Add(fallback[i]);
                        }
                    }
                }

                List<EnemyDefinition> pool = new();

                for (int i = 0; i < source.Count; i++)
                {
                    EnemyDefinition e = source[i];

                    if (e != null && e.IsAvailableForWave(wave))
                    {
                        pool.Add(e);
                    }
                }

                return pool;
            }

            private static List<EnemyDefinition> BuildElitePool(WaveDefinition def, int wave)
            {
                List<EnemyDefinition> pool = new();

                if (def.eliteEnemies == null)
                {
                    return pool;
                }

                for (int i = 0; i < def.eliteEnemies.Count; i++)
                {
                    EnemyDefinition e = def.eliteEnemies[i];

                    if (e != null && e.IsAvailableForWave(wave))
                    {
                        pool.Add(e);
                    }
                }

                return pool;
            }

            private static int FindMinCost(List<EnemyDefinition> normal, List<EnemyDefinition> elite)
            {
                int min = int.MaxValue;

                for (int i = 0; i < normal.Count; i++)
                {
                    min = Mathf.Min(min, normal[i].cost);
                }

                for (int i = 0; i < elite.Count; i++)
                {
                    min = Mathf.Min(min, elite[i].cost);
                }

                if (min == int.MaxValue)
                {
                    return 0;
                }

                return Mathf.Max(1, min);
            }

            private static int SimulateOneWave(WaveDefinition def, List<EnemyDefinition> normalPool, List<EnemyDefinition> elitePool, int wave, int seed)
            {
                int budget = def.GetBudget(wave);
                int cap = def.GetMaxEnemies(wave);

                bool bossWave = def.GetSpecialWaveType(wave) == WaveDefinition.SpecialWaveType.Boss && def.enableBossWaves;
                bool bossAssigned = def.bossEnemy != null && def.bossEnemy.prefab != null;

                if (bossWave && def.bossConsumesBudget && bossAssigned)
                {
                    int bossCost = Mathf.Max(1, def.bossEnemy.cost);

                    if (budget - bossCost >= 0)
                    {
                        budget -= bossCost;
                    }
                }

                int count = 0;

                System.Random rng = new System.Random(seed);
                float eliteChance = def.GetEliteChance(wave);
                float costBias = def.GetCostBias(wave);

                int safety = cap * 15;

                for (int i = 0; i < safety; i++)
                {
                    if (budget <= 0 || count >= cap)
                    {
                        break;
                    }

                    bool tryElite = elitePool.Count > 0 && rng.NextDouble() < eliteChance;

                    EnemyDefinition chosen = null;

                    if (tryElite)
                    {
                        chosen = ChooseWeighted(rng, elitePool, budget, costBias);
                    }

                    if (chosen == null)
                    {
                        chosen = ChooseWeighted(rng, normalPool, budget, costBias);
                    }

                    if (chosen == null)
                    {
                        break;
                    }

                    budget -= chosen.cost;
                    count++;
                }

                if (bossWave && bossAssigned)
                {
                    count += 1;
                }

                return count;
            }

            private static EnemyDefinition ChooseWeighted(System.Random rng, List<EnemyDefinition> pool, int remainingBudget, float costBias)
            {
                double total = 0.0;

                for (int i = 0; i < pool.Count; i++)
                {
                    EnemyDefinition e = pool[i];

                    if (e.cost <= remainingBudget)
                    {
                        total += GetBiasedWeight(e, costBias);
                    }
                }

                if (total <= 0.0)
                {
                    return null;
                }

                double roll = rng.NextDouble() * total;
                double cumulative = 0.0;

                for (int i = 0; i < pool.Count; i++)
                {
                    EnemyDefinition e = pool[i];

                    if (e.cost > remainingBudget)
                    {
                        continue;
                    }

                    cumulative += GetBiasedWeight(e, costBias);

                    if (roll <= cumulative)
                    {
                        return e;
                    }
                }

                return null;
            }

            private static double GetBiasedWeight(EnemyDefinition e, float costBias)
            {
                double w = Mathf.Max(0f, e.weight);
                double cost = Mathf.Max(1, e.cost);

                if (Mathf.Approximately(costBias, 0f))
                {
                    return w;
                }

                if (costBias > 0f)
                {
                    return w * System.Math.Pow(cost, costBias);
                }

                return w * System.Math.Pow(1.0 / cost, System.Math.Abs(costBias));
            }

            private static string BuildWarnings(WavePreviewResult r, WaveDefinition def, WaveSpawner spawner, int minCost)
            {
                List<string> warn = new();

                if (r.availableNormalCount <= 0)
                {
                    warn.Add("No valid normal enemies available for this wave (check minWave/maxWave, cost, prefab).");
                }

                if (r.budget <= 0)
                {
                    warn.Add("Budget is 0. No enemies will generate unless boss is 'free' and enabled.");
                }

                if (minCost > 0 && r.budget > 0 && r.budget < minCost)
                {
                    warn.Add($"Budget ({r.budget}) is below the cheapest enemy cost ({minCost}). Likely 0 spawns.");
                }

                if (r.bossWave && !r.bossAssigned)
                {
                    warn.Add("Boss wave triggered but no bossEnemy is assigned.");
                }

                if (spawner.CurrentSpawnShape == WaveSpawner.SpawnShape.PredefinedTransforms)
                {
                    if (spawner.SpawnPoints == null || spawner.SpawnPoints.Count == 0)
                    {
                        warn.Add("SpawnShape is PredefinedTransforms but spawnPoints is empty.");
                    }
                }

                if (spawner.CurrentSpawnShape == WaveSpawner.SpawnShape.RingAroundTarget && spawner.Target == null)
                {
                    warn.Add("RingAroundTarget selected but no Target assigned.");
                }

                if (spawner.CurrentSpawnMode == WaveSpawner.SpawnMode.OutsideCamera && spawner.ReferenceCamera == null)
                {
                    warn.Add("OutsideCamera selected but no ReferenceCamera assigned (Camera.main won't exist in edit-time preview).");
                }

                if (spawner.UsePooling && spawner.Pool == null)
                {
                    warn.Add("Pooling enabled but no SimplePool assigned.");
                }

                return string.Join("\n", warn);
            }
        }
    }
}