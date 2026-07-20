#nullable enable

using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using UnityEngine;

namespace Icebreaker.Gameplay
{
    public sealed class T1IceTarget
    {
        public const float MaxHp = 10f;
        public const float ClickDamage = 1f;

        private readonly long stageId;
        private readonly long iceInstanceId;
        private readonly Vector2 referencePosition;

        public T1IceTarget(long stageId, long iceInstanceId, Vector2 referencePosition)
        {
            this.stageId = stageId;
            this.iceInstanceId = iceInstanceId;
            this.referencePosition = referencePosition;
            RemainingHp = MaxHp;
        }

        public long StageId => stageId;

        public long IceInstanceId => iceInstanceId;

        public Vector2 ReferencePosition => referencePosition;

        public float RemainingHp { get; private set; }

        public bool IsDestroyed { get; private set; }

        public bool TryApplyClick(
            double stageElapsedSeconds,
            out DamageAppliedEvent damageApplied,
            out IceDestroyedEvent iceDestroyed)
        {
            damageApplied = default;
            iceDestroyed = default;

            if (IsDestroyed)
            {
                return false;
            }

            RemainingHp = Mathf.Max(0f, RemainingHp - ClickDamage);
            damageApplied = new DamageAppliedEvent(
                stageId,
                iceInstanceId,
                0L,
                0,
                EffectType.Click,
                ClickDamage,
                RemainingHp,
                false,
                referencePosition,
                stageElapsedSeconds);

            if (RemainingHp > 0f)
            {
                return true;
            }

            IsDestroyed = true;
            iceDestroyed = new IceDestroyedEvent(
                stageId,
                iceInstanceId,
                0L,
                0,
                IceTier.T1,
                SpecialIceType.None,
                DestroyCategory.Direct,
                EffectType.Click,
                referencePosition,
                stageElapsedSeconds);

            return true;
        }
    }
}
