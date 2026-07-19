#nullable enable

using Icebreaker.Shared.Combat;
using UnityEngine;

namespace Icebreaker.Shared.Events
{
    public readonly struct DamageAppliedEvent
    {
        public DamageAppliedEvent(
            long stageId,
            long iceInstanceId,
            long chainId,
            int chainDepth,
            EffectType effectType,
            float damage,
            float remainingHp,
            bool wasCritical,
            Vector2 referencePosition,
            double stageElapsedSeconds)
        {
            StageId = stageId;
            IceInstanceId = iceInstanceId;
            ChainId = chainId;
            ChainDepth = chainDepth;
            EffectType = effectType;
            Damage = damage;
            RemainingHp = remainingHp;
            WasCritical = wasCritical;
            ReferencePosition = referencePosition;
            StageElapsedSeconds = stageElapsedSeconds;
        }

        public long StageId { get; }

        public long IceInstanceId { get; }

        public long ChainId { get; }

        public int ChainDepth { get; }

        public EffectType EffectType { get; }

        public float Damage { get; }

        public float RemainingHp { get; }

        public bool WasCritical { get; }

        /// <summary>Position in the 960 x 540 bottom-left-origin reference space.</summary>
        public Vector2 ReferencePosition { get; }

        /// <summary>Stage time excluding settings-pause time.</summary>
        public double StageElapsedSeconds { get; }
    }

    public readonly struct SupportChargeChangedEvent
    {
        public SupportChargeChangedEvent(long stageId, int currentCharge, int maxCharge)
        {
            StageId = stageId;
            CurrentCharge = currentCharge;
            MaxCharge = maxCharge;
        }

        public long StageId { get; }

        public int CurrentCharge { get; }

        public int MaxCharge { get; }
    }

    public readonly struct IceDestroyedEvent
    {
        public IceDestroyedEvent(
            long stageId,
            long iceInstanceId,
            long chainId,
            int chainDepth,
            IceTier tier,
            SpecialIceType specialType,
            DestroyCategory destroyCategory,
            EffectType effectType,
            Vector2 referencePosition,
            double stageElapsedSeconds)
        {
            StageId = stageId;
            IceInstanceId = iceInstanceId;
            ChainId = chainId;
            ChainDepth = chainDepth;
            Tier = tier;
            SpecialType = specialType;
            DestroyCategory = destroyCategory;
            EffectType = effectType;
            ReferencePosition = referencePosition;
            StageElapsedSeconds = stageElapsedSeconds;
        }

        public long StageId { get; }

        public long IceInstanceId { get; }

        public long ChainId { get; }

        public int ChainDepth { get; }

        public IceTier Tier { get; }

        public SpecialIceType SpecialType { get; }

        public DestroyCategory DestroyCategory { get; }

        public EffectType EffectType { get; }

        /// <summary>Position in the 960 x 540 bottom-left-origin reference space.</summary>
        public Vector2 ReferencePosition { get; }

        /// <summary>Stage time excluding settings-pause time.</summary>
        public double StageElapsedSeconds { get; }
    }
}
