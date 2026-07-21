#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using UnityEngine;

namespace Icebreaker.Gameplay
{
    /// <summary>
    /// Manages up to <see cref="IceFieldConfig.MaxActiveIceCount"/> ice instances.
    /// Handles click hit-detection, damage, destruction events, and automatic respawn.
    /// </summary>
    public sealed class IceField : ICombatEventSource
    {
        private readonly long stageId;
        private readonly IceFieldConfig config;
        private readonly IceIdGenerator idGenerator;
        private readonly IceSpawnPositioner positioner;
        private readonly CriticalStrike? criticalStrike;
        private readonly List<IceInstance> activeIce;
        
        private long nextChainId = 1L;
        private long currentChainId;

        private struct QueuedDamage
        {
            public IceInstance Target;
            public float Damage;
            public EffectType EffectType;
            public DestroyCategory Category;
            public bool WasCritical;
            public int ChainDepth;
        }

        private readonly Queue<QueuedDamage> effectQueue = new Queue<QueuedDamage>();

        public IceField(long stageId, IceFieldConfig config, IceIdGenerator idGenerator, IceSpawnPositioner positioner,
            CriticalStrike? criticalStrike = null)
        {
            this.stageId = stageId;
            this.config = config;
            this.idGenerator = idGenerator;
            this.positioner = positioner;
            this.criticalStrike = criticalStrike;
            activeIce = new List<IceInstance>(config.MaxActiveIceCount);
        }

        public event Action<DamageAppliedEvent> DamageApplied = delegate { };
        public event Action<SupportChargeChangedEvent> SupportChargeChanged = delegate { };
        public event Action<IceDestroyedEvent> IceDestroyed = delegate { };

        /// <summary>Fires after a destroyed ice has been respawned at a new position.</summary>
        public event Action<int>? IceRespawned;

        public IReadOnlyList<IceInstance> ActiveIce => activeIce;

        /// <summary>
        /// Populate the field with the configured number of ice instances.
        /// Call once at the start of a stage.
        /// </summary>
        public void Initialize(double stageElapsedSeconds)
        {
            activeIce.Clear();

            for (var i = 0; i < config.MaxActiveIceCount; i++)
            {
                var ice = SpawnNewIce(stageElapsedSeconds);
                activeIce.Add(ice);
            }
        }

        /// <summary>
        /// Apply a single direct attack at the given reference position. Returns true if an ice was hit.
        /// Critical hit is rolled automatically for direct attacks.
        /// </summary>
        /// <param name="referencePosition">Click position in 960x540 space.</param>
        /// <param name="clickDamage">Base damage before critical multiplier.</param>
        /// <param name="effectType">Click or Hold to distinguish input type.</param>
        /// <param name="stageElapsedSeconds">Time since stage started.</param>
        public bool ApplyClickAt(Vector2 referencePosition, float clickDamage, EffectType effectType, double stageElapsedSeconds)
        {
            var target = FindClosestAliveAt(referencePosition);
            if (target == null)
            {
                return false;
            }

            // Start a new chain for this direct input
            currentChainId = nextChainId++;

            // Roll critical for direct attacks only.
            var wasCritical = false;
            var finalDamage = clickDamage;
            if (criticalStrike != null)
            {
                finalDamage = criticalStrike.Apply(clickDamage, out wasCritical);
            }

            EnqueueDamage(target, finalDamage, effectType, DestroyCategory.Direct, wasCritical, chainDepth: 0);
            ProcessQueue(stageElapsedSeconds);

            return true;
        }

        private void EnqueueDamage(IceInstance target, float damage, EffectType effectType, DestroyCategory category, bool wasCritical, int chainDepth)
        {
            effectQueue.Enqueue(new QueuedDamage
            {
                Target = target,
                Damage = damage,
                EffectType = effectType,
                Category = category,
                WasCritical = wasCritical,
                ChainDepth = chainDepth
            });
        }

        private void ProcessQueue(double stageElapsedSeconds)
        {
            while (effectQueue.Count > 0)
            {
                var queued = effectQueue.Dequeue();
                
                // "같은 얼음에 여러 피해가 예약돼도 순서대로 처리하고, 처음 HP가 0 이하가 된 순간 한 번만 파괴한다. 이후 예약 피해는 취소한다."
                if (queued.Target.IsDestroyed)
                {
                    continue;
                }

                if (queued.Target.TryApplyDamage(
                        queued.Damage,
                        queued.EffectType,
                        queued.Category,
                        queued.WasCritical,
                        currentChainId,
                        queued.ChainDepth,
                        stageElapsedSeconds,
                        out var damageEvent,
                        out var destroyEvent))
                {
                    DamageApplied(damageEvent);

                    if (queued.Target.IsDestroyed && damageEvent.RemainingHp <= 0f)
                    {
                        IceDestroyed(destroyEvent);
                        
                        // TODO: Enqueue chain effects (D03, Special Ice, H01) here using BFS order.
                        // if (queued.ChainDepth < 3) { ... }

                        RespawnAt(queued.Target, stageElapsedSeconds);
                    }
                }
            }
        }

        /// <summary>Find the closest alive ice within hit radius of the given position.</summary>
        private IceInstance? FindClosestAliveAt(Vector2 referencePosition)
        {
            IceInstance? closest = null;
            var closestDist = float.MaxValue;

            for (var i = 0; i < activeIce.Count; i++)
            {
                var ice = activeIce[i];
                if (ice.IsDestroyed)
                {
                    continue;
                }

                var dist = Vector2.Distance(ice.ReferencePosition, referencePosition);
                if (dist <= config.HitRadiusReferencePixels && dist < closestDist)
                {
                    closestDist = dist;
                    closest = ice;
                }
            }

            return closest;
        }

        private void RespawnAt(IceInstance destroyed, double stageElapsedSeconds)
        {
            var tier = PickRandomTier();
            var def = GetDefinition(tier);
            var positions = CollectAlivePositions(destroyed);

            positioner.TryGetPosition(positions, out var newPosition);
            
            DetermineSpecialIce(tier, out var specialType, out var hpMultiplier);

            destroyed.Reset(
                idGenerator.NextId(),
                tier,
                specialType,
                def.MaxHp * hpMultiplier,
                newPosition,
                stageElapsedSeconds);

            var index = activeIce.IndexOf(destroyed);
            if (index >= 0)
            {
                IceRespawned?.Invoke(index);
            }
        }

        private IceInstance SpawnNewIce(double stageElapsedSeconds)
        {
            var tier = PickRandomTier();
            var def = GetDefinition(tier);
            var positions = CollectAlivePositions(null);

            positioner.TryGetPosition(positions, out var position);
            
            DetermineSpecialIce(tier, out var specialType, out var hpMultiplier);

            return new IceInstance(
                stageId,
                idGenerator.NextId(),
                tier,
                specialType,
                def.MaxHp * hpMultiplier,
                position,
                stageElapsedSeconds);
        }

        private List<Vector2> CollectAlivePositions(IceInstance? exclude)
        {
            var positions = new List<Vector2>(activeIce.Count);
            for (var i = 0; i < activeIce.Count; i++)
            {
                var ice = activeIce[i];
                if (!ice.IsDestroyed && ice != exclude)
                {
                    positions.Add(ice.ReferencePosition);
                }
            }

            return positions;
        }

        private void DetermineSpecialIce(IceTier tier, out SpecialIceType specialType, out float hpMultiplier)
        {
            specialType = SpecialIceType.None;
            hpMultiplier = 1f;

            if (config.SpecialDefinitions == null || config.SpecialDefinitions.Count == 0)
            {
                return;
            }

            var currentSpecialCount = 0;
            for (var i = 0; i < activeIce.Count; i++)
            {
                if (!activeIce[i].IsDestroyed && activeIce[i].SpecialType != SpecialIceType.None)
                {
                    currentSpecialCount++;
                }
            }

            if (currentSpecialCount >= config.MaxSpecialIceCount)
            {
                return;
            }

            var roll = UnityEngine.Random.value;
            for (var i = 0; i < config.SpecialDefinitions.Count; i++)
            {
                var def = config.SpecialDefinitions[i];
                if (tier >= def.MinimumTier)
                {
                    if (roll < def.SpawnChance)
                    {
                        specialType = def.Type;
                        hpMultiplier = def.HpMultiplier;
                        return;
                    }
                    roll -= def.SpawnChance;
                }
            }
        }

        private IceTier PickRandomTier()
        {
            long totalWeight = 0;
            for (var i = 0; i < config.SpawnWeights.Count; i++)
            {
                totalWeight += config.SpawnWeights[i].Weight;
            }

            var roll = (long)(UnityEngine.Random.value * totalWeight);
            long cumulative = 0;

            for (var i = 0; i < config.SpawnWeights.Count; i++)
            {
                cumulative += config.SpawnWeights[i].Weight;
                if (roll < cumulative)
                {
                    return config.SpawnWeights[i].Tier;
                }
            }

            return config.SpawnWeights[config.SpawnWeights.Count - 1].Tier;
        }

        private IceDefinition GetDefinition(IceTier tier)
        {
            for (var i = 0; i < config.IceDefinitions.Count; i++)
            {
                if (config.IceDefinitions[i].Tier == tier)
                {
                    return config.IceDefinitions[i];
                }
            }

            throw new InvalidOperationException($"No IceDefinition found for tier {tier}.");
        }
    }
}
