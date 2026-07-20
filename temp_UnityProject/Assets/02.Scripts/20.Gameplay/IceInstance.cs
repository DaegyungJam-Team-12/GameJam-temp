#nullable enable

using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using UnityEngine;

namespace Icebreaker.Gameplay
{
    /// <summary>
    /// A single ice block on the field. Supports any tier and can be reset for object-pool reuse.
    /// </summary>
    public sealed class IceInstance
    {
        private readonly long stageId;
        private long iceInstanceId;
        private float maxHp;
        private Vector2 referencePosition;

        public IceInstance(
            long stageId,
            long iceInstanceId,
            IceTier tier,
            SpecialIceType specialType,
            float maxHp,
            Vector2 referencePosition,
            double spawnTime)
        {
            this.stageId = stageId;
            this.iceInstanceId = iceInstanceId;
            Tier = tier;
            SpecialType = specialType;
            this.maxHp = maxHp;
            RemainingHp = maxHp;
            this.referencePosition = referencePosition;
            SpawnTime = spawnTime;
        }

        public long StageId => stageId;
        public long IceInstanceId => iceInstanceId;
        public IceTier Tier { get; private set; }
        public SpecialIceType SpecialType { get; private set; }
        public float MaxHp => maxHp;
        public float RemainingHp { get; private set; }
        public Vector2 ReferencePosition => referencePosition;
        public bool IsDestroyed { get; private set; }
        public double SpawnTime { get; private set; }

        /// <summary>
        /// Apply damage to this ice. Returns false if already destroyed.
        /// </summary>
        public bool TryApplyDamage(
            float damage,
            EffectType effectType,
            DestroyCategory destroyCategory,
            bool wasCritical,
            long chainId,
            int chainDepth,
            double stageElapsedSeconds,
            out DamageAppliedEvent damageEvent,
            out IceDestroyedEvent destroyEvent)
        {
            damageEvent = default;
            destroyEvent = default;

            if (IsDestroyed)
            {
                return false;
            }

            RemainingHp = Mathf.Max(0f, RemainingHp - damage);

            damageEvent = new DamageAppliedEvent(
                stageId,
                iceInstanceId,
                chainId,
                chainDepth,
                effectType,
                damage,
                RemainingHp,
                wasCritical,
                referencePosition,
                stageElapsedSeconds);

            if (RemainingHp > 0f)
            {
                return true;
            }

            IsDestroyed = true;

            destroyEvent = new IceDestroyedEvent(
                stageId,
                iceInstanceId,
                chainId,
                chainDepth,
                Tier,
                SpecialType,
                destroyCategory,
                effectType,
                referencePosition,
                stageElapsedSeconds);

            return true;
        }

        /// <summary>
        /// Reset this instance for object-pool reuse with a new ID, position, and full HP.
        /// </summary>
        public void Reset(
            long newIceInstanceId,
            IceTier tier,
            SpecialIceType specialType,
            float newMaxHp,
            Vector2 newPosition,
            double spawnTime)
        {
            iceInstanceId = newIceInstanceId;
            Tier = tier;
            SpecialType = specialType;
            maxHp = newMaxHp;
            RemainingHp = newMaxHp;
            referencePosition = newPosition;
            IsDestroyed = false;
            SpawnTime = spawnTime;
        }
    }
}
