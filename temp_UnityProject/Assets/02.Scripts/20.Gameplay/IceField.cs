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
        private readonly List<IceInstance> activeIce;

        public IceField(long stageId, IceFieldConfig config, IceIdGenerator idGenerator, IceSpawnPositioner positioner)
        {
            this.stageId = stageId;
            this.config = config;
            this.idGenerator = idGenerator;
            this.positioner = positioner;
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
        /// Apply a single click at the given reference position. Returns true if an ice was hit.
        /// </summary>
        public bool ApplyClickAt(Vector2 referencePosition, float clickDamage, double stageElapsedSeconds)
        {
            var target = FindClosestAliveAt(referencePosition);
            if (target == null)
            {
                return false;
            }

            if (!target.TryApplyDamage(
                    clickDamage,
                    EffectType.Click,
                    DestroyCategory.Direct,
                    wasCritical: false,
                    chainId: 0L,
                    chainDepth: 0,
                    stageElapsedSeconds,
                    out var damageEvent,
                    out var destroyEvent))
            {
                return false;
            }

            DamageApplied(damageEvent);

            if (target.IsDestroyed && damageEvent.RemainingHp <= 0f)
            {
                IceDestroyed(destroyEvent);
                RespawnAt(target, stageElapsedSeconds);
            }

            return true;
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

        /// <summary>Reset a destroyed ice instance with a new ID and position.</summary>
        private void RespawnAt(IceInstance destroyed, double stageElapsedSeconds)
        {
            var tier = PickRandomTier();
            var def = GetDefinition(tier);
            var positions = CollectAlivePositions(destroyed);

            positioner.TryGetPosition(positions, out var newPosition);

            destroyed.Reset(
                idGenerator.NextId(),
                tier,
                SpecialIceType.None,
                def.MaxHp,
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

            return new IceInstance(
                stageId,
                idGenerator.NextId(),
                tier,
                SpecialIceType.None,
                def.MaxHp,
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
