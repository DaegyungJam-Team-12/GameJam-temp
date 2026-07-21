#nullable enable

using System;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Progression;
using NUnit.Framework;
using UnityEngine;

namespace Icebreaker.Core.Tests
{
    public sealed class ProgressionLedgerTests
    {
        [Test]
        public void RewardTable_BaseFundsPerTier()
        {
            var rewardTable = RewardTable.CreateDefault();

            Assert.That(rewardTable.ComputeFunds(IceTier.T1, SpecialIceType.None, 0), Is.EqualTo(10));
            Assert.That(rewardTable.ComputeFunds(IceTier.T2, SpecialIceType.None, 0), Is.EqualTo(80));
            Assert.That(rewardTable.ComputeFunds(IceTier.T3, SpecialIceType.None, 0), Is.EqualTo(700));
        }

        [Test]
        public void RewardTable_CrystalMultiplier()
        {
            var rewardTable = RewardTable.CreateDefault();

            Assert.That(rewardTable.ComputeFunds(IceTier.T2, SpecialIceType.Crystal, 0), Is.EqualTo(320));
            Assert.That(rewardTable.ComputeFunds(IceTier.T3, SpecialIceType.Crystal, 0), Is.EqualTo(2800));
        }

        [Test]
        public void RewardTable_CrackAndNone_NoMultiplier()
        {
            var rewardTable = RewardTable.CreateDefault();

            Assert.That(rewardTable.ComputeFunds(IceTier.T2, SpecialIceType.Crack, 0), Is.EqualTo(80));
            Assert.That(rewardTable.ComputeFunds(IceTier.T2, SpecialIceType.None, 0), Is.EqualTo(80));
        }

        [Test]
        public void RewardTable_EfficiencyLevel_FloorsOnce()
        {
            var rewardTable = RewardTable.CreateDefault();
            var fractionalRewardTable = new RewardTable(11, 80, 700, 4.0);

            Assert.That(rewardTable.ComputeFunds(IceTier.T1, SpecialIceType.None, 1), Is.EqualTo(11));
            Assert.That(rewardTable.ComputeFunds(IceTier.T2, SpecialIceType.None, 1), Is.EqualTo(88));
            Assert.That(fractionalRewardTable.ComputeFunds(IceTier.T1, SpecialIceType.None, 1), Is.EqualTo(12));
        }

        [Test]
        public void Approve_GrantsRealFundsAndProgress()
        {
            var ledger = CreateLedger(CreateDestinations());
            ledger.BeginStage();

            var approved = ledger.TryApproveDestruction(CreateIceDestroyedEvent(1, 1), out var reward);

            Assert.That(approved, Is.True);
            Assert.That(reward.FundsGranted, Is.EqualTo(10));
            Assert.That(reward.DestinationProgressGranted, Is.EqualTo(1));
            Assert.That(ledger.Funds, Is.EqualTo(10));
            Assert.That(ledger.DestinationProgress, Is.EqualTo(1));
        }

        [Test]
        public void Duplicate_NotApproved()
        {
            var ledger = CreateLedger(CreateDestinations());
            var destruction = CreateIceDestroyedEvent(1, 1);
            ledger.BeginStage();

            var firstApproved = ledger.TryApproveDestruction(destruction, out _);
            var secondApproved = ledger.TryApproveDestruction(destruction, out var duplicateReward);

            Assert.That(firstApproved, Is.True);
            Assert.That(secondApproved, Is.False);
            Assert.That(ledger.Funds, Is.EqualTo(10));
            Assert.That(ledger.DestinationProgress, Is.EqualTo(1));
            Assert.That(duplicateReward.FundsGranted, Is.Zero);
            Assert.That(duplicateReward.DestinationProgressGranted, Is.Zero);
        }

        [Test]
        public void ProgressCapsAtTarget_ThenFundsOnly()
        {
            var ledger = CreateLedger(CreateSmallDestinations());
            ledger.BeginStage();

            ledger.TryApproveDestruction(CreateIceDestroyedEvent(1, 1), out _);
            ledger.TryApproveDestruction(CreateIceDestroyedEvent(1, 2), out _);
            ledger.TryApproveDestruction(CreateIceDestroyedEvent(1, 3), out var rewardAfterTarget);

            Assert.That(ledger.DestinationProgress, Is.EqualTo(2));
            Assert.That(ledger.PendingArrivalDestinationId, Is.EqualTo("island-village"));
            Assert.That(rewardAfterTarget.DestinationProgressGranted, Is.Zero);
            Assert.That(rewardAfterTarget.FundsGranted, Is.EqualTo(10));
            Assert.That(ledger.Funds, Is.EqualTo(30));
        }

        [Test]
        public void EndStage_SummaryMatchesAccumulation()
        {
            var ledger = CreateLedger(CreateSmallDestinations());
            ledger.BeginStage();

            ledger.TryApproveDestruction(CreateIceDestroyedEvent(1, 1), out _);
            ledger.TryApproveDestruction(
                CreateIceDestroyedEvent(1, 2, IceTier.T2),
                out _);
            ledger.TryApproveDestruction(CreateIceDestroyedEvent(1, 3), out _);

            var summary = ledger.EndStage();

            Assert.That(summary.EarnedFunds, Is.EqualTo(100));
            Assert.That(summary.DestroyedCount, Is.EqualTo(3));
            Assert.That(summary.DestinationProgressGain, Is.EqualTo(2));
            Assert.That(summary.ReachedDestination, Is.True);
            Assert.That(summary.DestinationId, Is.EqualTo("island-village"));
        }

        [Test]
        public void ApplyArrival_AdvancesToNextDestination()
        {
            var ledger = CreateLedger(CreateSmallDestinations());
            ledger.BeginStage();
            ledger.TryApproveDestruction(CreateIceDestroyedEvent(1, 1), out _);
            ledger.TryApproveDestruction(CreateIceDestroyedEvent(1, 2), out _);

            var applied = ledger.ApplyArrival();

            Assert.That(applied, Is.True);
            Assert.That(ledger.CurrentDestination.Id, Is.EqualTo("lighthouse"));
            Assert.That(ledger.DestinationProgress, Is.Zero);
            Assert.That(ledger.PendingArrivalDestinationId, Is.Null);
            Assert.That(ledger.CompletedDestinationIds, Does.Contain("island-village"));
            Assert.That(ledger.GameCompleted, Is.False);
        }

        [Test]
        public void ApplyArrival_OnLastDestination_CompletesGame()
        {
            var ledger = CreateLedger(CreateSmallDestinations());

            ReachCurrentDestinationAndApply(ledger, 1, 2);
            ReachCurrentDestinationAndApply(ledger, 2, 1);

            ledger.BeginStage();
            ledger.TryApproveDestruction(CreateIceDestroyedEvent(3, 1), out _);
            Assert.That(ledger.ApplyArrival(), Is.True);

            Assert.That(ledger.GameCompleted, Is.True);
            Assert.That(ledger.CurrentDestination.Id, Is.EqualTo("north-base"));
            Assert.That(ledger.DestinationProgress, Is.EqualTo(1));
            Assert.That(ledger.PendingArrivalDestinationId, Is.Null);
            Assert.That(ledger.CompletedDestinationIds, Does.Contain("north-base"));

            ledger.BeginStage();
            var approved = ledger.TryApproveDestruction(
                CreateIceDestroyedEvent(4, 1),
                out var rewardAfterCompletion);

            Assert.That(approved, Is.True);
            Assert.That(rewardAfterCompletion.FundsGranted, Is.EqualTo(10));
            Assert.That(rewardAfterCompletion.DestinationProgressGranted, Is.Zero);
            Assert.That(ledger.DestinationProgress, Is.EqualTo(ledger.DestinationTarget));
        }

        [Test]
        public void Constructor_RejectsEmptyDestinations()
        {
            Assert.That(
                () => new ProgressionLedger(
                    Array.Empty<DestinationDefinition>(),
                    RewardTable.CreateDefault()),
                Throws.TypeOf<ArgumentException>()
                    .With.Property("ParamName").EqualTo("destinations"));
        }

        [Test]
        public void Constructor_RejectsNullRewardTable()
        {
            Assert.That(
                () => new ProgressionLedger(CreateDestinations(), null!),
                Throws.TypeOf<ArgumentNullException>()
                    .With.Property("ParamName").EqualTo("rewardTable"));
        }

        private static ProgressionLedger CreateLedger(DestinationDefinition[] destinations)
        {
            return new ProgressionLedger(destinations, RewardTable.CreateDefault());
        }

        private static DestinationDefinition[] CreateDestinations()
        {
            return new[]
            {
                new DestinationDefinition("island-village", "섬마을", 120, "식료품·우편", 0),
                new DestinationDefinition("lighthouse", "등대항", 600, "발전기 연료·의약품", 1),
                new DestinationDefinition("north-base", "북쪽 기지", 2400, "기계 부품·우편", 2)
            };
        }

        private static DestinationDefinition[] CreateSmallDestinations()
        {
            return new[]
            {
                new DestinationDefinition("island-village", "섬마을", 2, "식료품·우편", 0),
                new DestinationDefinition("lighthouse", "등대항", 1, "발전기 연료·의약품", 1),
                new DestinationDefinition("north-base", "북쪽 기지", 1, "기계 부품·우편", 2)
            };
        }

        private static void ReachCurrentDestinationAndApply(
            ProgressionLedger ledger,
            long stageId,
            int targetProgress)
        {
            ledger.BeginStage();
            for (var iceInstanceId = 1; iceInstanceId <= targetProgress; iceInstanceId++)
            {
                ledger.TryApproveDestruction(
                    CreateIceDestroyedEvent(stageId, iceInstanceId),
                    out _);
            }

            Assert.That(ledger.ApplyArrival(), Is.True);
        }

        private static IceDestroyedEvent CreateIceDestroyedEvent(
            long stageId,
            long iceInstanceId,
            IceTier tier = IceTier.T1,
            SpecialIceType specialType = SpecialIceType.None)
        {
            return new IceDestroyedEvent(
                stageId,
                iceInstanceId,
                iceInstanceId,
                0,
                tier,
                specialType,
                DestroyCategory.Direct,
                EffectType.Click,
                new Vector2(480f, 270f),
                0d);
        }
    }
}
