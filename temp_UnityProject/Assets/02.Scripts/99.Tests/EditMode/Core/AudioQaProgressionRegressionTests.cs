#nullable enable

using System.Collections.Generic;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Progression;
using NUnit.Framework;
using UnityEngine;

namespace Icebreaker.Core.Tests
{
    public sealed class AudioQaProgressionRegressionTests
    {
        [TestCase(false, 120)]
        [TestCase(true, 40)]
        public void ProfileProgression_ZeroTargetMinusOneAndExactTarget_TransitionsOnce(
            bool demo,
            int expectedFirstTarget)
        {
            var destinations = demo
                ? DestinationCatalog.CreateDemo()
                : DestinationCatalog.CreateStandard();
            var fresh = new ProgressionLedger(destinations, RewardTable.CreateDefault());
            Assert.That(fresh.DestinationProgress, Is.Zero);
            Assert.That(fresh.DestinationTarget, Is.EqualTo(expectedFirstTarget));
            Assert.That(fresh.PendingArrivalDestinationId, Is.Null);

            var nearTarget = new ProgressionLedger(
                destinations,
                RewardTable.CreateDefault(),
                initialDestinationProgress: expectedFirstTarget - 1);
            Assert.That(nearTarget.PendingArrivalDestinationId, Is.Null);

            nearTarget.BeginStage(0);
            Assert.That(
                nearTarget.TryApproveDestruction(CreateDestruction(stageId: 1, iceId: 1), out var reward),
                Is.True);
            Assert.That(reward.DestinationProgressGranted, Is.EqualTo(1));
            Assert.That(nearTarget.DestinationProgress, Is.EqualTo(expectedFirstTarget));
            Assert.That(
                nearTarget.PendingArrivalDestinationId,
                Is.EqualTo(destinations[0].Id));

            Assert.That(nearTarget.ApplyArrival(), Is.True);
            Assert.That(nearTarget.CurrentDestinationIndex, Is.EqualTo(1));
            Assert.That(nearTarget.DestinationProgress, Is.Zero);
            Assert.That(nearTarget.PendingArrivalDestinationId, Is.Null);
            Assert.That(nearTarget.ApplyArrival(), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PendingArrivalReload_AppliesOnceAndPreservesNextDestination(bool demo)
        {
            var destinations = demo
                ? DestinationCatalog.CreateDemo()
                : DestinationCatalog.CreateStandard();
            var pending = new ProgressionLedger(
                destinations,
                RewardTable.CreateDefault(),
                initialDestinationProgress: destinations[0].TargetProgress,
                initialPendingArrivalDestinationId: destinations[0].Id);

            Assert.That(pending.ApplyArrival(), Is.True);
            Assert.That(pending.ApplyArrival(), Is.False);

            var reloaded = new ProgressionLedger(
                destinations,
                RewardTable.CreateDefault(),
                initialDestinationIndex: pending.CurrentDestinationIndex,
                initialDestinationProgress: pending.DestinationProgress,
                initialCompletedDestinationIds: pending.CompletedDestinationIds,
                initialPendingArrivalDestinationId: pending.PendingArrivalDestinationId,
                initialGameCompleted: pending.GameCompleted);
            Assert.That(reloaded.CurrentDestinationIndex, Is.EqualTo(1));
            Assert.That(reloaded.DestinationProgress, Is.Zero);
            Assert.That(reloaded.PendingArrivalDestinationId, Is.Null);
            Assert.That(reloaded.ApplyArrival(), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FinalDestinationCompletionReload_RemainsCompletedWithoutReapplyingArrival(bool demo)
        {
            var destinations = demo
                ? DestinationCatalog.CreateDemo()
                : DestinationCatalog.CreateStandard();
            var finalIndex = destinations.Count - 1;
            var completedIds = new List<string>
            {
                destinations[0].Id,
                destinations[1].Id
            };
            var pendingFinal = new ProgressionLedger(
                destinations,
                RewardTable.CreateDefault(),
                initialDestinationIndex: finalIndex,
                initialDestinationProgress: destinations[finalIndex].TargetProgress,
                initialCompletedDestinationIds: completedIds,
                initialPendingArrivalDestinationId: destinations[finalIndex].Id);

            Assert.That(pendingFinal.ApplyArrival(), Is.True);
            Assert.That(pendingFinal.GameCompleted, Is.True);
            Assert.That(pendingFinal.ApplyArrival(), Is.False);

            var reloaded = new ProgressionLedger(
                destinations,
                RewardTable.CreateDefault(),
                initialDestinationIndex: pendingFinal.CurrentDestinationIndex,
                initialDestinationProgress: pendingFinal.DestinationProgress,
                initialCompletedDestinationIds: pendingFinal.CompletedDestinationIds,
                initialPendingArrivalDestinationId: pendingFinal.PendingArrivalDestinationId,
                initialGameCompleted: pendingFinal.GameCompleted);
            Assert.That(reloaded.GameCompleted, Is.True);
            Assert.That(reloaded.PendingArrivalDestinationId, Is.Null);
            Assert.That(reloaded.DestinationProgress, Is.EqualTo(reloaded.DestinationTarget));
            Assert.That(reloaded.ApplyArrival(), Is.False);
        }

        private static IceDestroyedEvent CreateDestruction(long stageId, long iceId) =>
            new(
                stageId,
                iceId,
                iceId,
                0,
                IceTier.T1,
                SpecialIceType.None,
                DestroyCategory.Direct,
                EffectType.Click,
                new Vector2(480f, 270f),
                0d);
    }
}
