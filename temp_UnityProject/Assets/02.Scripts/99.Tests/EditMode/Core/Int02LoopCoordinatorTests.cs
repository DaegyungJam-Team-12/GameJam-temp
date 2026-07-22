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
    public sealed class Int02LoopCoordinatorTests
    {
        private const double PhaseSeconds = 1d;

        private string tempDirectory = null!;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(
                Path.GetTempPath(),
                "icebreaker-core03-" + Guid.NewGuid().ToString("N"));
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
        public void ContinueSettlement_OnArrival_RequestsPresentationOnceForCompletedDestination()
        {
            using var coordinator = CreateCoordinator(CreateSmallDestinations(), out var ledger);
            var requests = new List<ArrivalPresentationRequested>();
            IProgressionEventSource progressionSource = coordinator;
            progressionSource.ArrivalPresentationRequested += requests.Add;

            ReachSettlement(coordinator, iceInstanceId: 1);
            coordinator.ContinueSettlement();
            coordinator.ContinueSettlement();

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].DestinationId, Is.EqualTo("island-village"));
            Assert.That(requests[0].DestinationDisplayName, Is.EqualTo("섬마을"));
            Assert.That(ledger.CurrentDestination.Id, Is.EqualTo("lighthouse-port"));
            Assert.That(coordinator.CurrentState.Phase, Is.EqualTo(GamePhase.Traveling));
        }

        [Test]
        public void ContinueSettlement_WithoutArrival_DoesNotRequestPresentation()
        {
            using var coordinator = CreateCoordinator(CreateDestinations(2, 1, 1), out var ledger);
            var requestCount = 0;
            coordinator.ArrivalPresentationRequested += _ => requestCount++;

            ReachSettlement(coordinator, iceInstanceId: 1);
            coordinator.ContinueSettlement();

            Assert.That(requestCount, Is.Zero);
            Assert.That(ledger.DestinationProgress, Is.EqualTo(1));
            Assert.That(coordinator.CurrentState.Phase, Is.EqualTo(GamePhase.Traveling));
        }

        [Test]
        public void ThreeDestinationPlaythrough_CompletesGameAndDisablesStageStart()
        {
            using var coordinator = CreateCoordinator(CreateSmallDestinations(), out var ledger);
            var requests = new List<ArrivalPresentationRequested>();
            coordinator.ArrivalPresentationRequested += requests.Add;

            ReachSettlement(coordinator, iceInstanceId: 1);
            coordinator.ContinueSettlement();
            coordinator.Tick(PhaseSeconds);

            ReachSettlement(coordinator, iceInstanceId: 2);
            coordinator.ContinueSettlement();
            coordinator.Tick(PhaseSeconds);

            ReachSettlement(coordinator, iceInstanceId: 3);
            coordinator.ContinueSettlement();

            Assert.That(requests, Has.Count.EqualTo(3));
            Assert.That(requests[0].DestinationId, Is.EqualTo("island-village"));
            Assert.That(requests[1].DestinationId, Is.EqualTo("lighthouse-port"));
            Assert.That(requests[2].DestinationId, Is.EqualTo("northern-base"));
            Assert.That(ledger.GameCompleted, Is.True);
            Assert.That(ledger.CompletedDestinationIds, Has.Count.EqualTo(3));
            Assert.That(coordinator.CurrentState.Phase, Is.EqualTo(GamePhase.Completed));
            Assert.That(coordinator.CurrentState.CanStartStage, Is.False);
        }

        private Int02LoopCoordinator CreateCoordinator(
            DestinationDefinition[] destinations,
            out ProgressionLedger ledger)
        {
            var loop = new GameLoopController(
                PhaseSeconds,
                PhaseSeconds,
                PhaseSeconds,
                PhaseSeconds);
            loop.Tick(PhaseSeconds);
            ledger = new ProgressionLedger(destinations, RewardTable.CreateDefault());
            var saveService = new SaveService(
                new SaveStore(tempDirectory),
                SaveData.CreateNew("demo"));
            return new Int02LoopCoordinator(
                loop,
                ledger,
                saveService,
                () => new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero));
        }

        private static void ReachSettlement(
            Int02LoopCoordinator coordinator,
            long iceInstanceId)
        {
            coordinator.RequestStageStart();
            coordinator.Tick(PhaseSeconds);
            Assert.That(coordinator.CurrentState.Phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(
                coordinator.TryApproveDestruction(CreateDestruction(iceInstanceId)),
                Is.True);
            coordinator.Tick(PhaseSeconds);
            Assert.That(coordinator.CurrentState.Phase, Is.EqualTo(GamePhase.StageEnding));
            coordinator.Tick(PhaseSeconds);
            Assert.That(coordinator.CurrentState.Phase, Is.EqualTo(GamePhase.Settlement));
        }

        private static DestinationDefinition[] CreateSmallDestinations() =>
            CreateDestinations(1, 1, 1);

        private static DestinationDefinition[] CreateDestinations(
            int islandVillageTarget,
            int lighthousePortTarget,
            int northernBaseTarget)
        {
            return new[]
            {
                new DestinationDefinition(
                    "island-village",
                    "섬마을",
                    islandVillageTarget,
                    "식료품·우편",
                    0),
                new DestinationDefinition(
                    "lighthouse-port",
                    "등대항",
                    lighthousePortTarget,
                    "발전기 연료·의약품",
                    1),
                new DestinationDefinition(
                    "northern-base",
                    "북쪽 기지",
                    northernBaseTarget,
                    "기계 부품·우편",
                    2)
            };
        }

        private static IceDestroyedEvent CreateDestruction(long iceInstanceId)
        {
            return new IceDestroyedEvent(
                stageId: 0,
                iceInstanceId: iceInstanceId,
                chainId: iceInstanceId,
                chainDepth: 0,
                tier: IceTier.T1,
                specialType: SpecialIceType.None,
                destroyCategory: DestroyCategory.Direct,
                effectType: EffectType.Click,
                referencePosition: new Vector2(480f, 270f),
                stageElapsedSeconds: 0d);
        }
    }
}
