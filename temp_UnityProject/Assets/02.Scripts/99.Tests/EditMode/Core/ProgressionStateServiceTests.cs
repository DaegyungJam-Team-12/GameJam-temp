#nullable enable

using System;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Progression;
using Icebreaker.Shared.State;
using NUnit.Framework;
using UnityEngine;

namespace Icebreaker.Core.Tests
{
    public sealed class ProgressionStateServiceTests
    {
        private FakeCombatEventSource source = null!;
        private ProgressionStateService service = null!;
        private int stateChangedCount;
        private GameState? lastState;

        [SetUp]
        public void SetUp()
        {
            var destination = new DestinationDefinition(
                "island-village",
                "섬마을",
                120,
                "식료품·우편",
                0);

            source = new FakeCombatEventSource();
            service = new ProgressionStateService(destination);
            service.AttachCombatSource(source);
            stateChangedCount = 0;
            lastState = null;
            service.StateChanged += CaptureState;
        }

        [TearDown]
        public void TearDown()
        {
            service.StateChanged -= CaptureState;
            service.Dispose();
        }

        [Test]
        public void FirstDestruction_RaisesStateChangedWithGrantedFunds()
        {
            source.PublishIceDestroyed(CreateIceDestroyedEvent(1, 1, 1));

            Assert.That(stateChangedCount, Is.EqualTo(1));
            Assert.That(lastState, Is.Not.Null);
            Assert.That(lastState!.Funds, Is.EqualTo(10));
            Assert.That(lastState.DestinationProgress, Is.EqualTo(1));
            Assert.That(service.CurrentState.Funds, Is.EqualTo(10));
        }

        [Test]
        public void DuplicateDestruction_DoesNotRaiseOrChange()
        {
            var destruction = CreateIceDestroyedEvent(1, 1, 1);

            source.PublishIceDestroyed(destruction);
            source.PublishIceDestroyed(destruction);

            Assert.That(stateChangedCount, Is.EqualTo(1));
            Assert.That(service.CurrentState.Funds, Is.EqualTo(10));
        }

        [Test]
        public void SecondUniqueDestruction_Accumulates()
        {
            source.PublishIceDestroyed(CreateIceDestroyedEvent(1, 1, 1));
            source.PublishIceDestroyed(CreateIceDestroyedEvent(1, 2, 2));

            Assert.That(stateChangedCount, Is.EqualTo(2));
            Assert.That(service.CurrentState.Funds, Is.EqualTo(20));
            Assert.That(service.CurrentState.DestinationProgress, Is.EqualTo(2));
        }

        [Test]
        public void EnsureInitialized_ProducesInitialSnapshotBeforeAnyDestruction()
        {
            Assert.That(service.CurrentState.Funds, Is.Zero);
            Assert.That(service.CurrentState.DestinationProgress, Is.Zero);
            Assert.That(service.CurrentState.DestinationTarget, Is.EqualTo(120));
            Assert.That(service.CurrentState.Phase, Is.EqualTo(GamePhase.Ready));
        }

        [Test]
        public void AttachTwice_Throws()
        {
            var secondSource = new FakeCombatEventSource();

            Assert.Throws<InvalidOperationException>(() => service.AttachCombatSource(secondSource));
        }

        [Test]
        public void Dispose_Unsubscribes()
        {
            service.Dispose();

            source.PublishIceDestroyed(CreateIceDestroyedEvent(1, 1, 1));

            Assert.That(stateChangedCount, Is.Zero);
        }

        private void CaptureState(GameState state)
        {
            stateChangedCount++;
            lastState = state;
        }

        private static IceDestroyedEvent CreateIceDestroyedEvent(
            long stageId,
            long iceInstanceId,
            long chainId)
        {
            return new IceDestroyedEvent(
                stageId,
                iceInstanceId,
                chainId,
                0,
                IceTier.T1,
                SpecialIceType.None,
                DestroyCategory.Direct,
                EffectType.Click,
                new Vector2(480f, 270f),
                0d);
        }
    }
}
