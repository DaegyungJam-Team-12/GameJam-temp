#nullable enable

using System;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using UnityEngine;

namespace Icebreaker.Gameplay
{
    public enum IceRespawnState
    {
        Active,
        RespawnGap,
        SpawnAnimating
    }

    /// <summary>
    /// A single ice block on the field. Supports any tier and can be reset for object-pool reuse.
    /// </summary>
    public sealed class IceInstance
    {
        private struct PendingSpawn
        {
            public long IceInstanceId;
            public IceTier Tier;
            public SpecialIceType SpecialType;
            public float MaxHp;
            public Vector2 ReferencePosition;
            public float VisualDiameterReferencePixels;
        }

        private readonly long stageId;
        private long iceInstanceId;
        private float maxHp;
        private Vector2 referencePosition;
        private PendingSpawn pendingSpawn;
        private bool hasPendingSpawn;

        public IceInstance(
            long stageId,
            long iceInstanceId,
            IceTier tier,
            SpecialIceType specialType,
            float maxHp,
            Vector2 referencePosition,
            double spawnTime,
            float visualDiameterReferencePixels = 0f)
        {
            this.stageId = stageId;
            this.iceInstanceId = iceInstanceId;
            Tier = tier;
            SpecialType = specialType;
            this.maxHp = maxHp;
            RemainingHp = maxHp;
            this.referencePosition = referencePosition;
            SpawnTime = spawnTime;
            VisualDiameterReferencePixels = visualDiameterReferencePixels;
            RespawnState = IceRespawnState.Active;
            RespawnStateStartedAt = spawnTime;
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
        public float VisualDiameterReferencePixels { get; private set; }
        public IceRespawnState RespawnState { get; private set; }
        public double RespawnStateStartedAt { get; private set; }
        public bool IsActive => RespawnState == IceRespawnState.Active && !IsDestroyed;
        public bool HasPendingSpawn => hasPendingSpawn;
        public long VisualIceInstanceId => hasPendingSpawn ? pendingSpawn.IceInstanceId : iceInstanceId;
        public IceTier VisualTier => hasPendingSpawn ? pendingSpawn.Tier : Tier;
        public SpecialIceType VisualSpecialType => hasPendingSpawn ? pendingSpawn.SpecialType : SpecialType;
        public Vector2 VisualReferencePosition => hasPendingSpawn
            ? pendingSpawn.ReferencePosition
            : referencePosition;
        public float VisualDiameterForDisplayReferencePixels => hasPendingSpawn
            ? pendingSpawn.VisualDiameterReferencePixels
            : VisualDiameterReferencePixels;
        public SpecialIceType PendingSpecialType => hasPendingSpawn
            ? pendingSpawn.SpecialType
            : SpecialIceType.None;

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
            Reset(
                newIceInstanceId,
                tier,
                specialType,
                newMaxHp,
                newPosition,
                spawnTime,
                VisualDiameterReferencePixels);
        }

        public void Reset(
            long newIceInstanceId,
            IceTier tier,
            SpecialIceType specialType,
            float newMaxHp,
            Vector2 newPosition,
            double spawnTime,
            float visualDiameterReferencePixels)
        {
            iceInstanceId = newIceInstanceId;
            Tier = tier;
            SpecialType = specialType;
            maxHp = newMaxHp;
            RemainingHp = newMaxHp;
            referencePosition = newPosition;
            IsDestroyed = false;
            SpawnTime = spawnTime;
            VisualDiameterReferencePixels = visualDiameterReferencePixels;
            RespawnState = IceRespawnState.Active;
            RespawnStateStartedAt = spawnTime;
            hasPendingSpawn = false;
        }

        public void BeginRespawnGap(double stageElapsedSeconds)
        {
            RespawnState = IceRespawnState.RespawnGap;
            RespawnStateStartedAt = stageElapsedSeconds;
            hasPendingSpawn = false;
        }

        public void BeginSpawnAnimation(
            long pendingIceInstanceId,
            IceTier pendingTier,
            SpecialIceType pendingSpecialType,
            float pendingMaxHp,
            Vector2 pendingReferencePosition,
            float pendingVisualDiameterReferencePixels,
            double stageElapsedSeconds)
        {
            pendingSpawn = new PendingSpawn
            {
                IceInstanceId = pendingIceInstanceId,
                Tier = pendingTier,
                SpecialType = pendingSpecialType,
                MaxHp = pendingMaxHp,
                ReferencePosition = pendingReferencePosition,
                VisualDiameterReferencePixels = pendingVisualDiameterReferencePixels
            };
            hasPendingSpawn = true;
            RespawnState = IceRespawnState.SpawnAnimating;
            RespawnStateStartedAt = stageElapsedSeconds;
        }

        public void ActivatePendingSpawn(double spawnTime)
        {
            if (!hasPendingSpawn)
            {
                throw new InvalidOperationException("Cannot activate an ice spawn that has not been prepared.");
            }

            Reset(
                pendingSpawn.IceInstanceId,
                pendingSpawn.Tier,
                pendingSpawn.SpecialType,
                pendingSpawn.MaxHp,
                pendingSpawn.ReferencePosition,
                spawnTime,
                pendingSpawn.VisualDiameterReferencePixels);
        }

        public void CancelPendingSpawn()
        {
            hasPendingSpawn = false;
            RespawnState = IceRespawnState.RespawnGap;
        }
    }
}
