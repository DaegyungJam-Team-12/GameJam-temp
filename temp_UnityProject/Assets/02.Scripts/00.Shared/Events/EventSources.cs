#nullable enable

using System;

namespace Icebreaker.Shared.Events
{
    /// <summary>Subscribers must unsubscribe from every event in OnDisable or Dispose.</summary>
    public interface ICombatEventSource
    {
        event Action<DamageAppliedEvent> DamageApplied;

        event Action<SupportChargeChangedEvent> SupportChargeChanged;

        event Action<IceDestroyedEvent> IceDestroyed;
    }

    /// <summary>Subscribers must unsubscribe from every event in OnDisable or Dispose.</summary>
    public interface IProgressionEventSource
    {
        event Action<StageStarted> StageStarted;

        event Action<RewardGrantedEvent> RewardGranted;

        event Action<StageEnded> StageEnded;

        event Action<SettlementReady> SettlementReady;
    }
}
