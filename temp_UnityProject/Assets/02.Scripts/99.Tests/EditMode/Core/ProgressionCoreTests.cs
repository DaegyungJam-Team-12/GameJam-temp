#nullable enable

using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Progression;
using Icebreaker.Shared.State;
using NUnit.Framework;
using UnityEngine;

namespace Icebreaker.Core.Tests
{
    public sealed class ProgressionCoreTests
    {
        private FakeCombatEventSource source = null!;
        private ProgressionCore core = null!;
        private int rewardCount;
        private RewardGrantedEvent lastReward;

        [SetUp]
        public void SetUp()
        {
            InitializeCore(120);
        }

        [TearDown]
        public void TearDown()
        {
            source.IceDestroyed -= HandleIceDestroyed;
            core.RewardGranted -= CaptureReward;
        }

        [Test]
        public void FirstDestruction_GrantsFundsAndProgressOnce()
        {
            source.PublishIceDestroyed(CreateIceDestroyedEvent(1, 1, 1));

            Assert.That(core.Funds, Is.EqualTo(10));
            Assert.That(core.DestinationProgress, Is.EqualTo(1));
            Assert.That(rewardCount, Is.EqualTo(1));
            Assert.That(lastReward.FundsGranted, Is.EqualTo(10));
            Assert.That(lastReward.DestinationProgressGranted, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateDestruction_DoesNotChangeState()
        {
            var destruction = CreateIceDestroyedEvent(1, 1, 1);

            source.PublishIceDestroyed(destruction);
            source.PublishIceDestroyed(destruction);

            Assert.That(core.Funds, Is.EqualTo(10));
            Assert.That(core.DestinationProgress, Is.EqualTo(1));
            Assert.That(rewardCount, Is.EqualTo(1));
        }

        [Test]
        public void SecondUniqueDestruction_Accumulates()
        {
            source.PublishIceDestroyed(CreateIceDestroyedEvent(1, 1, 1));
            source.PublishIceDestroyed(CreateIceDestroyedEvent(1, 2, 2));

            Assert.That(core.Funds, Is.EqualTo(20));
            Assert.That(core.DestinationProgress, Is.EqualTo(2));
            Assert.That(rewardCount, Is.EqualTo(2));
        }

        [Test]
        public void DestructionAtTarget_GrantsFundsOnly_WithZeroProgressDelta()
        {
            ReinitializeCore(2);

            source.PublishIceDestroyed(CreateIceDestroyedEvent(1, 1, 1));
            source.PublishIceDestroyed(CreateIceDestroyedEvent(1, 2, 2));
            source.PublishIceDestroyed(CreateIceDestroyedEvent(1, 3, 3));

            Assert.That(core.Funds, Is.EqualTo(30));
            Assert.That(core.DestinationProgress, Is.EqualTo(2));
            Assert.That(rewardCount, Is.EqualTo(3));
            Assert.That(lastReward.FundsGranted, Is.EqualTo(10));
            Assert.That(lastReward.DestinationProgressGranted, Is.Zero);
        }

        [Test]
        public void CreateSnapshot_ReflectsCurrentState()
        {
            source.PublishIceDestroyed(CreateIceDestroyedEvent(1, 1, 1));
            source.PublishIceDestroyed(CreateIceDestroyedEvent(1, 2, 2));

            var snapshot = core.CreateSnapshot();

            Assert.That(snapshot.Phase, Is.EqualTo(GamePhase.Ready));
            Assert.That(snapshot.Funds, Is.EqualTo(20));
            Assert.That(snapshot.CurrentDestinationId, Is.EqualTo("island-village"));
            Assert.That(snapshot.DestinationProgress, Is.EqualTo(2));
            Assert.That(snapshot.DestinationTarget, Is.EqualTo(120));
            Assert.That(snapshot.MaintenanceLevels.Count, Is.Zero);
            Assert.That(snapshot.FirstDestroyShown, Is.False);
            Assert.That(snapshot.CanStartStage, Is.True);
        }

        private void ReinitializeCore(int targetProgress)
        {
            source.IceDestroyed -= HandleIceDestroyed;
            core.RewardGranted -= CaptureReward;
            InitializeCore(targetProgress);
        }

        private void InitializeCore(int targetProgress)
        {
            var destination = new DestinationDefinition(
                "island-village",
                "섬마을",
                targetProgress,
                "식료품·우편",
                0);

            source = new FakeCombatEventSource();
            core = new ProgressionCore(destination);
            rewardCount = 0;
            lastReward = default;

            source.IceDestroyed += HandleIceDestroyed;
            core.RewardGranted += CaptureReward;
        }

        private void HandleIceDestroyed(IceDestroyedEvent payload)
        {
            core.HandleIceDestroyed(payload);
        }

        private void CaptureReward(RewardGrantedEvent payload)
        {
            rewardCount++;
            lastReward = payload;
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
