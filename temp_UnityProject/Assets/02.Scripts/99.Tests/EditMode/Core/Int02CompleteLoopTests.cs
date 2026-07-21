#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Progression;
using Icebreaker.Shared.State;
using NUnit.Framework;
using UnityEngine;

namespace Icebreaker.Core.Tests
{
    public sealed class Int02CompleteLoopTests
    {
        private string tempDirectory = null!;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(
                Path.GetTempPath(),
                "icebreaker-int02-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        [Test]
        public void TwoCycles_AccumulateOnce_SettleGrantedTotals_AndResolveSavedBoot()
        {
            var now = new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
            var store = new SaveStore(tempDirectory);
            var saveData = SaveData.CreateNew("demo");
            var saveService = new SaveService(store, saveData);
            var loop = new GameLoopController(60d, 3d, 10d);
            loop.Tick(10d);
            var ledger = new ProgressionLedger(CreateDemoDestinations(), RewardTable.CreateDefault());
            using var coordinator = new Int02LoopCoordinator(loop, ledger, saveService, () => now);

            var progressionEvents = new List<string>();
            var rewards = new List<RewardGrantedEvent>();
            var settlements = new List<SettlementReady>();
            var travelingCount = 0;
            coordinator.StageStarted += payload => progressionEvents.Add($"start:{payload.StageId}");
            coordinator.RewardGranted += payload => rewards.Add(payload);
            coordinator.StageEnded += payload => progressionEvents.Add($"end:{payload.StageId}");
            coordinator.SettlementReady += payload =>
            {
                progressionEvents.Add($"settlement:{payload.StageId}");
                settlements.Add(payload);
            };
            coordinator.StateChanged += state =>
            {
                if (state.Phase == GamePhase.Traveling)
                {
                    travelingCount++;
                }
            };

            coordinator.RequestStageStart();
            coordinator.Tick(3d);
            var first = CreateDestruction(1, 1, IceTier.T1);
            Assert.That(coordinator.TryApproveDestruction(first), Is.True);
            Assert.That(coordinator.TryApproveDestruction(first), Is.False);
            Assert.That(coordinator.TryApproveDestruction(CreateDestruction(1, 2, IceTier.T2)), Is.True);
            coordinator.Tick(60d);
            Assert.That(coordinator.CurrentState.Phase, Is.EqualTo(GamePhase.StageEnding));
            Assert.That(saveData.runInProgress, Is.True);
            Assert.That(settlements, Is.Empty);
            coordinator.Tick(1.2d);
            Assert.That(coordinator.CurrentState.Phase, Is.EqualTo(GamePhase.Settlement));
            Assert.That(saveData.runInProgress, Is.True);
            coordinator.ContinueSettlement();
            Assert.That(coordinator.CurrentState.Phase, Is.EqualTo(GamePhase.Traveling));
            Assert.That(saveData.runInProgress, Is.False);
            coordinator.Tick(10d);

            coordinator.RequestStageStart();
            coordinator.Tick(3d);
            Assert.That(coordinator.TryApproveDestruction(CreateDestruction(2, 1, IceTier.T1)), Is.True);
            coordinator.Tick(60d);
            Assert.That(coordinator.CurrentState.Phase, Is.EqualTo(GamePhase.StageEnding));
            coordinator.Tick(1.2d);
            Assert.That(coordinator.CurrentState.Phase, Is.EqualTo(GamePhase.Settlement));
            coordinator.ContinueSettlement();
            Assert.That(coordinator.CurrentState.Phase, Is.EqualTo(GamePhase.Traveling));
            coordinator.Tick(10d);

            Assert.That(
                progressionEvents,
                Is.EqualTo(new[]
                {
                    "start:1", "end:1", "settlement:1",
                    "start:2", "end:2", "settlement:2"
                }));
            Assert.That(rewards, Has.Count.EqualTo(3));
            Assert.That(ledger.Funds, Is.EqualTo(100));
            Assert.That(ledger.DestinationProgress, Is.EqualTo(3));
            Assert.That(settlements, Has.Count.EqualTo(2));
            Assert.That(settlements[0].Summary.EarnedFunds, Is.EqualTo(90));
            Assert.That(settlements[0].Summary.DestroyedCount, Is.EqualTo(2));
            Assert.That(settlements[0].Summary.DestinationProgressGain, Is.EqualTo(2));
            Assert.That(settlements[1].Summary.EarnedFunds, Is.EqualTo(10));
            Assert.That(settlements[1].Summary.DestroyedCount, Is.EqualTo(1));
            Assert.That(settlements[1].Summary.DestinationProgressGain, Is.EqualTo(1));
            Assert.That(
                settlements[0].Summary.EarnedFunds + settlements[1].Summary.EarnedFunds,
                Is.EqualTo(SumGrantedFunds(rewards)));
            Assert.That(travelingCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(saveData.firstDestroyShown, Is.True);

            coordinator.Flush();
            var readySave = store.TryLoad("demo");
            var readyBoot = SaveBootResolver.Resolve(readySave, now, 10d);
            Assert.That(readyBoot.Phase, Is.EqualTo(GamePhase.Ready));
            Assert.That(readyBoot.VoyageRemainingSeconds, Is.Zero);

            var restoredLedger = CreateRestoredLedger(readySave!);
            Assert.That(restoredLedger.Funds, Is.EqualTo(100));
            Assert.That(restoredLedger.DestinationProgress, Is.EqualTo(3));

            coordinator.RequestStageStart();
            coordinator.Tick(3d);
            coordinator.Flush();
            var interruptedSave = store.TryLoad("demo");
            var interruptedBoot = SaveBootResolver.Resolve(interruptedSave, now, 10d);
            Assert.That(interruptedSave!.runInProgress, Is.True);
            Assert.That(interruptedBoot.Phase, Is.EqualTo(GamePhase.Traveling));
            Assert.That(interruptedBoot.VoyageRemainingSeconds, Is.EqualTo(10d));
        }

        [Test]
        public void InterruptedDuringSettlement_KeepsApprovedResults_AndRestartsFullVoyage()
        {
            var now = new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
            var store = new SaveStore(tempDirectory);
            var saveData = SaveData.CreateNew("demo");
            var saveService = new SaveService(store, saveData);
            var loop = new GameLoopController(60d, 3d, 10d);
            loop.Tick(10d);
            var ledger = new ProgressionLedger(CreateDemoDestinations(), RewardTable.CreateDefault());
            using var coordinator = new Int02LoopCoordinator(loop, ledger, saveService, () => now);

            coordinator.RequestStageStart();
            coordinator.Tick(3d);
            Assert.That(coordinator.TryApproveDestruction(CreateDestruction(1, 1, IceTier.T1)), Is.True);
            coordinator.Tick(60d);
            coordinator.Tick(1.2d);
            Assert.That(coordinator.CurrentState.Phase, Is.EqualTo(GamePhase.Settlement));

            coordinator.Flush();
            var interruptedSave = store.TryLoad("demo");
            var interruptedBoot = SaveBootResolver.Resolve(interruptedSave, now, 10d);

            Assert.That(interruptedSave, Is.Not.Null);
            Assert.That(interruptedSave!.funds, Is.EqualTo(10));
            Assert.That(interruptedSave.destinationProgress, Is.EqualTo(1));
            Assert.That(interruptedSave.runInProgress, Is.True);
            Assert.That(interruptedBoot.Phase, Is.EqualTo(GamePhase.Traveling));
            Assert.That(interruptedBoot.VoyageRemainingSeconds, Is.EqualTo(10d));
        }

        private static DestinationDefinition[] CreateDemoDestinations()
        {
            return new[]
            {
                new DestinationDefinition("island-village", "섬마을", 40, "식료품·우편", 0),
                new DestinationDefinition("lighthouse-port", "등대항", 120, "발전기 연료·의약품", 1),
                new DestinationDefinition("northern-base", "북쪽 기지", 300, "기계 부품·우편", 2)
            };
        }

        private static ProgressionLedger CreateRestoredLedger(SaveData data)
        {
            return new ProgressionLedger(
                CreateDemoDestinations(),
                RewardTable.CreateDefault(),
                initialFunds: data.funds,
                initialDestinationIndex: data.currentDestinationIndex,
                initialDestinationProgress: data.destinationProgress,
                initialCompletedDestinationIds: data.completedDestinationIds,
                initialPendingArrivalDestinationId: data.pendingArrivalDestinationId,
                initialGameCompleted: data.gameCompleted);
        }

        private static IceDestroyedEvent CreateDestruction(
            long stageId,
            long iceInstanceId,
            IceTier tier)
        {
            return new IceDestroyedEvent(
                stageId,
                iceInstanceId,
                iceInstanceId,
                0,
                tier,
                SpecialIceType.None,
                DestroyCategory.Direct,
                EffectType.Click,
                new Vector2(480f, 270f),
                1d);
        }

        private static long SumGrantedFunds(IReadOnlyList<RewardGrantedEvent> rewards)
        {
            long total = 0;
            for (var i = 0; i < rewards.Count; i++)
            {
                total += rewards[i].FundsGranted;
            }

            return total;
        }
    }
}
