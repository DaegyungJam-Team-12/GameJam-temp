using System;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Progression;
using NUnit.Framework;
using UnityEngine;

namespace Icebreaker.Shared.Tests
{
    public sealed class EventSourceTests
    {
        [Test]
        public void Subscribers_ReceiveEachEventOnce_AndCanUnsubscribe()
        {
            var combatSource = new FakeCombatEventSource();
            var progressionSource = new FakeProgressionEventSource();
            var subscriber = new TrackingSubscriber(combatSource, progressionSource);

            combatSource.RaiseAll();
            progressionSource.RaiseAll();

            Assert.That(subscriber.DamageCount, Is.EqualTo(1));
            Assert.That(subscriber.ChargeCount, Is.EqualTo(1));
            Assert.That(subscriber.DestroyedCount, Is.EqualTo(1));
            Assert.That(subscriber.StartedCount, Is.EqualTo(1));
            Assert.That(subscriber.RewardCount, Is.EqualTo(1));
            Assert.That(subscriber.EndedCount, Is.EqualTo(1));
            Assert.That(subscriber.SettlementCount, Is.EqualTo(1));

            subscriber.Dispose();
            combatSource.RaiseAll();
            progressionSource.RaiseAll();

            Assert.That(subscriber.DamageCount, Is.EqualTo(1));
            Assert.That(subscriber.ChargeCount, Is.EqualTo(1));
            Assert.That(subscriber.DestroyedCount, Is.EqualTo(1));
            Assert.That(subscriber.StartedCount, Is.EqualTo(1));
            Assert.That(subscriber.RewardCount, Is.EqualTo(1));
            Assert.That(subscriber.EndedCount, Is.EqualTo(1));
            Assert.That(subscriber.SettlementCount, Is.EqualTo(1));
        }

        private sealed class FakeCombatEventSource : ICombatEventSource
        {
            public event Action<DamageAppliedEvent> DamageApplied = delegate { };

            public event Action<SupportChargeChangedEvent> SupportChargeChanged = delegate { };

            public event Action<IceDestroyedEvent> IceDestroyed = delegate { };

            public void RaiseAll()
            {
                DamageApplied(new DamageAppliedEvent(
                    1,
                    2,
                    3,
                    0,
                    EffectType.Click,
                    1f,
                    0f,
                    false,
                    Vector2.zero,
                    0d));
                SupportChargeChanged(new SupportChargeChangedEvent(1, 1, 12));
                IceDestroyed(new IceDestroyedEvent(
                    1,
                    2,
                    3,
                    0,
                    IceTier.T1,
                    SpecialIceType.None,
                    DestroyCategory.Direct,
                    EffectType.Click,
                    Vector2.zero,
                    0d));
            }
        }

        private sealed class FakeProgressionEventSource : IProgressionEventSource
        {
            public event Action<StageStarted> StageStarted = delegate { };

            public event Action<RewardGrantedEvent> RewardGranted = delegate { };

            public event Action<StageEnded> StageEnded = delegate { };

            public event Action<SettlementReady> SettlementReady = delegate { };

            public void RaiseAll()
            {
                StageStarted(new Icebreaker.Shared.Events.StageStarted(
                    1,
                    "2026-07-19T00:00:00.0000000+00:00",
                    30f));
                RewardGranted(new RewardGrantedEvent(1, 2, 3, 1, 1, Vector2.zero));
                StageEnded(new Icebreaker.Shared.Events.StageEnded(
                    1,
                    "2026-07-19T00:00:30.0000000+00:00"));
                SettlementReady(new Icebreaker.Shared.Events.SettlementReady(
                    1,
                    new SettlementSummary(1, 1, 1, false, null)));
            }
        }

        private sealed class TrackingSubscriber : IDisposable
        {
            private readonly ICombatEventSource _combatSource;
            private readonly IProgressionEventSource _progressionSource;

            public TrackingSubscriber(
                ICombatEventSource combatSource,
                IProgressionEventSource progressionSource)
            {
                _combatSource = combatSource;
                _progressionSource = progressionSource;

                _combatSource.DamageApplied += OnDamageApplied;
                _combatSource.SupportChargeChanged += OnSupportChargeChanged;
                _combatSource.IceDestroyed += OnIceDestroyed;
                _progressionSource.StageStarted += OnStageStarted;
                _progressionSource.RewardGranted += OnRewardGranted;
                _progressionSource.StageEnded += OnStageEnded;
                _progressionSource.SettlementReady += OnSettlementReady;
            }

            public int DamageCount { get; private set; }

            public int ChargeCount { get; private set; }

            public int DestroyedCount { get; private set; }

            public int StartedCount { get; private set; }

            public int RewardCount { get; private set; }

            public int EndedCount { get; private set; }

            public int SettlementCount { get; private set; }

            public void Dispose()
            {
                _combatSource.DamageApplied -= OnDamageApplied;
                _combatSource.SupportChargeChanged -= OnSupportChargeChanged;
                _combatSource.IceDestroyed -= OnIceDestroyed;
                _progressionSource.StageStarted -= OnStageStarted;
                _progressionSource.RewardGranted -= OnRewardGranted;
                _progressionSource.StageEnded -= OnStageEnded;
                _progressionSource.SettlementReady -= OnSettlementReady;
            }

            private void OnDamageApplied(DamageAppliedEvent payload) => DamageCount++;

            private void OnSupportChargeChanged(SupportChargeChangedEvent payload) => ChargeCount++;

            private void OnIceDestroyed(IceDestroyedEvent payload) => DestroyedCount++;

            private void OnStageStarted(StageStarted payload) => StartedCount++;

            private void OnRewardGranted(RewardGrantedEvent payload) => RewardCount++;

            private void OnStageEnded(StageEnded payload) => EndedCount++;

            private void OnSettlementReady(SettlementReady payload) => SettlementCount++;
        }
    }
}
