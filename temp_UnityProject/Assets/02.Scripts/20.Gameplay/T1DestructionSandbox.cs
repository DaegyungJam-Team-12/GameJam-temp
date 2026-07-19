#nullable enable

using System;
using Icebreaker.Shared.Events;
using UnityEngine;

namespace Icebreaker.Gameplay
{
    public sealed class T1DestructionSandbox : ICombatEventSource
    {
        public T1DestructionSandbox(long stageId, long iceInstanceId, Vector2 referencePosition)
        {
            Target = new T1IceTarget(stageId, iceInstanceId, referencePosition);
        }

        public event Action<DamageAppliedEvent> DamageApplied = delegate { };

        public event Action<SupportChargeChangedEvent> SupportChargeChanged = delegate { };

        public event Action<IceDestroyedEvent> IceDestroyed = delegate { };

        public T1IceTarget Target { get; }

        public bool ApplyClick(double stageElapsedSeconds)
        {
            if (!Target.TryApplyClick(stageElapsedSeconds, out var damageApplied, out var iceDestroyed))
            {
                return false;
            }

            DamageApplied(damageApplied);

            if (Target.IsDestroyed && damageApplied.RemainingHp <= 0f)
            {
                IceDestroyed(iceDestroyed);
            }

            return true;
        }
    }
}
