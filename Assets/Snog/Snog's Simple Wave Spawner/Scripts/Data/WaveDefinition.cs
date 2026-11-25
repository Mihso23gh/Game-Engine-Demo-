
using System.Collections.Generic;
using UnityEngine;

namespace Snog.SimpleWaveSystem.Data
{
    [CreateAssetMenu(menuName = "Snog/Simple Wave System/Wave Definition", fileName = "WD_WaveDefinition")]
    public class WaveDefinition : ScriptableObject
    {
        public enum SpecialWaveType
        {
            None,
            Rush,
            Tank,
            Swarm,
            Boss
        }

        [Header("Scaling Curves")]
        [Tooltip("Total budget for the wave. (X = Wave Number, Y = Value)")]
        public AnimationCurve budgetCurve = AnimationCurve.Linear(1f, 10f, 20f, 80f);
        public AnimationCurve durationCurve = AnimationCurve.Linear(1f, 20f, 20f, 60f);

        [Tooltip("Spawns per second. Example: 0.5 = one spawn every 2 seconds, 2 = two spawns per second.")]
        public AnimationCurve spawnRateCurve = AnimationCurve.Linear(1f, 0.5f, 20f, 2f);

        [Tooltip("Max enemies generated for the wave.")]
        public AnimationCurve maxEnemiesCurve = AnimationCurve.Linear(1f, 25f, 20f, 60f);

        [Tooltip("Max alive at once. Set Y <= 0 to disable cap.")]
        public AnimationCurve maxAliveEnemiesCurve = AnimationCurve.Constant(1f, 20f, 0f);

        [Header("Spawn Pacing")]
        [Tooltip("(X = 0..1 wave progress, Y = spawn rate multiplier)")]
        public AnimationCurve spawnPacingCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Header("Allowed Enemies (Optional)")]
        [Tooltip("If empty, spawner uses its fallback list.")]
        public List<EnemyDefinition> allowedEnemies = new();

        [Header("Behavior")]
        public bool endEarlyWhenSpawnQueueEmpty = true;

        [Header("Global Multipliers")]
        [Min(0.1f)]
        public float budgetMultiplier = 1f;

        [Min(0.1f)]
        public float durationMultiplier = 1f;

        [Min(0.1f)]
        public float spawnRateMultiplier = 1f;

        [Header("Elite Enemies (Optional)")]
        [Tooltip("If empty, elite system is effectively disabled.")]
        public List<EnemyDefinition> eliteEnemies = new();

        [Tooltip("Chance (0..1) to pick from eliteEnemies each spawn, based on wave number.")]
        public AnimationCurve eliteChanceByWave = AnimationCurve.Linear(1f, 0f, 20f, 0.25f);

        [Header("Boss Waves")]
        public bool enableBossWaves = false;

        [Min(1)]
        public int bossEveryNWaves = 10;

        public EnemyDefinition bossEnemy;

        [Tooltip("If true, boss cost is subtracted from budget. If false, boss is 'free'.")]
        public bool bossConsumesBudget = false;

        [Tooltip("Spawn the boss at the start of the wave.")]
        public bool bossSpawnsAtStart = true;

        [Header("Special Waves")]
        public bool enableRushWaves = false;

        [Min(1)]
        public int rushEveryNWaves = 7;

        [Min(0.1f)]
        public float rushBudgetMultiplier = 0.8f;

        [Min(0.1f)]
        public float rushSpawnRateMultiplier = 1.6f;

        [Min(0.1f)]
        public float rushDurationMultiplier = 0.8f;

        [Min(0.1f)]
        public float rushMaxEnemiesMultiplier = 1f;

        public bool enableTankWaves = false;

        [Min(1)]
        public int tankEveryNWaves = 8;

        [Min(0.1f)]
        public float tankBudgetMultiplier = 1f;

        [Min(0.1f)]
        public float tankSpawnRateMultiplier = 0.6f;

        [Min(0.1f)]
        public float tankDurationMultiplier = 1.1f;

        [Min(0.1f)]
        public float tankMaxEnemiesMultiplier = 0.6f;

        [Tooltip("Bias toward expensive enemies. 0 = no bias. Higher values = prefers expensive enemies.")]
        public float tankCostBias = 1.25f;

        public bool enableSwarmWaves = false;

        [Min(1)]
        public int swarmEveryNWaves = 6;

        [Min(0.1f)]
        public float swarmBudgetMultiplier = 1.3f;

        [Min(0.1f)]
        public float swarmSpawnRateMultiplier = 1.8f;

        [Min(0.1f)]
        public float swarmDurationMultiplier = 1f;

        [Min(0.1f)]
        public float swarmMaxEnemiesMultiplier = 1.6f;

        [Tooltip("Bias toward cheap enemies. 0 = no bias. Higher values = prefers cheap enemies.")]
        public float swarmCostBias = 1.25f;

        public SpecialWaveType GetSpecialWaveType(int wave)
        {
            if (enableBossWaves && bossEveryNWaves > 0 && wave % bossEveryNWaves == 0)
            {
                return SpecialWaveType.Boss;
            }

            if (enableTankWaves && tankEveryNWaves > 0 && wave % tankEveryNWaves == 0)
            {
                return SpecialWaveType.Tank;
            }

            if (enableSwarmWaves && swarmEveryNWaves > 0 && wave % swarmEveryNWaves == 0)
            {
                return SpecialWaveType.Swarm;
            }

            if (enableRushWaves && rushEveryNWaves > 0 && wave % rushEveryNWaves == 0)
            {
                return SpecialWaveType.Rush;
            }

            return SpecialWaveType.None;
        }

        public float GetSpawnPacingMultiplier(float normalizedProgress)
        {
            normalizedProgress = Mathf.Clamp01(normalizedProgress);
            float v = spawnPacingCurve.Evaluate(normalizedProgress);
            return Mathf.Max(0.01f, v);
        }

        public float GetEliteChance(int wave)
        {
            float v = eliteChanceByWave.Evaluate(wave);
            return Mathf.Clamp01(v);
        }

        public int GetMaxAliveEnemies(int wave)
        {
            int v = Mathf.RoundToInt(maxAliveEnemiesCurve.Evaluate(wave));

            if (v <= 0)
            {
                return int.MaxValue;
            }

            return v;
        }

        public int GetBudget(int wave)
        {
            float baseValue = budgetCurve.Evaluate(wave) * budgetMultiplier;
            float special = GetSpecialBudgetMultiplier(GetSpecialWaveType(wave));

            float v = baseValue * special;
            return Mathf.Max(0, Mathf.RoundToInt(v));
        }

        public float GetDuration(int wave)
        {
            float baseValue = durationCurve.Evaluate(wave) * durationMultiplier;
            float special = GetSpecialDurationMultiplier(GetSpecialWaveType(wave));

            float v = baseValue * special;
            return Mathf.Max(0.1f, v);
        }

        public float GetSpawnRate(int wave)
        {
            float baseValue = spawnRateCurve.Evaluate(wave) * spawnRateMultiplier;
            float special = GetSpecialSpawnRateMultiplier(GetSpecialWaveType(wave));

            float v = baseValue * special;
            return Mathf.Max(0.01f, v);
        }

        public int GetMaxEnemies(int wave)
        {
            float baseValue = maxEnemiesCurve.Evaluate(wave);
            float special = GetSpecialMaxEnemiesMultiplier(GetSpecialWaveType(wave));

            float v = baseValue * special;
            return Mathf.Max(1, Mathf.RoundToInt(v));
        }

        public float GetCostBias(int wave)
        {
            SpecialWaveType t = GetSpecialWaveType(wave);

            if (t == SpecialWaveType.Tank)
            {
                return Mathf.Max(0f, tankCostBias);
            }

            if (t == SpecialWaveType.Swarm)
            {
                return -Mathf.Max(0f, swarmCostBias);
            }

            return 0f;
        }

        private float GetSpecialBudgetMultiplier(SpecialWaveType t)
        {
            if (t == SpecialWaveType.Rush)
            {
                return rushBudgetMultiplier;
            }

            if (t == SpecialWaveType.Tank)
            {
                return tankBudgetMultiplier;
            }

            if (t == SpecialWaveType.Swarm)
            {
                return swarmBudgetMultiplier;
            }

            return 1f;
        }

        private float GetSpecialDurationMultiplier(SpecialWaveType t)
        {
            if (t == SpecialWaveType.Rush)
            {
                return rushDurationMultiplier;
            }

            if (t == SpecialWaveType.Tank)
            {
                return tankDurationMultiplier;
            }

            if (t == SpecialWaveType.Swarm)
            {
                return swarmDurationMultiplier;
            }

            return 1f;
        }

        private float GetSpecialSpawnRateMultiplier(SpecialWaveType t)
        {
            if (t == SpecialWaveType.Rush)
            {
                return rushSpawnRateMultiplier;
            }

            if (t == SpecialWaveType.Tank)
            {
                return tankSpawnRateMultiplier;
            }

            if (t == SpecialWaveType.Swarm)
            {
                return swarmSpawnRateMultiplier;
            }

            return 1f;
        }

        private float GetSpecialMaxEnemiesMultiplier(SpecialWaveType t)
        {
            if (t == SpecialWaveType.Rush)
            {
                return rushMaxEnemiesMultiplier;
            }

            if (t == SpecialWaveType.Tank)
            {
                return tankMaxEnemiesMultiplier;
            }

            if (t == SpecialWaveType.Swarm)
            {
                return swarmMaxEnemiesMultiplier;
            }

            return 1f;
        }
    }
}