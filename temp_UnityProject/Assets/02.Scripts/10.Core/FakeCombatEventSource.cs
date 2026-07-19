#nullable enable

using System;
using Icebreaker.Shared.Events;

namespace Icebreaker.Core
{
    public sealed class FakeCombatEventSource : ICombatEventSource
    {
        // Only IceDestroyed is raised by this CORE-00 fake; GP-01 replaces this seam.
#pragma warning disable 0067
        public event Action<DamageAppliedEvent> DamageApplied = delegate { };

        public event Action<SupportChargeChangedEvent> SupportChargeChanged = delegate { };
#pragma warning restore 0067

        public event Action<IceDestroyedEvent> IceDestroyed = delegate { };

        public void PublishIceDestroyed(IceDestroyedEvent e)
        {
            IceDestroyed(e);
        }
    }
}
