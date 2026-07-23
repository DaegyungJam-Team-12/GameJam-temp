#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Maintenance;
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

        [Test]
        public void CurrentCombatConfig_UsesSavedD04RadiusForTheNextStage()
        {
            var saveData = SaveData.CreateNew("demo");
            saveData.maintenanceLevels.Add(new SaveMaintenanceLevel(MaintenanceCatalog.D04, 3));

            using var coordinator = CreateCoordinator(
                CreateSmallDestinations(),
                out _,
                saveData);

            Assert.That(
                coordinator.CurrentCombatConfig.DirectAttack.CursorRadiusReferencePixels,
                Is.EqualTo(104f));
        }

        [Test]
        public void TryPurchaseMaintenance_UpdatesLedgerSaveAndGameStateFundsTogether()
        {
            var saveData = SaveData.CreateNew("demo");
            saveData.funds = 100;
            using var coordinator = CreateCoordinator(
                CreateSmallDestinations(),
                out var ledger,
                saveData);
            var changeCount = 0;
            coordinator.MaintenanceChanged += () => changeCount++;

            var result = coordinator.TryPurchaseMaintenance(MaintenanceCatalog.C01);

            Assert.That(result, Is.EqualTo(MaintenancePurchaseResult.Success));
            Assert.That(changeCount, Is.EqualTo(1));
            Assert.That(ledger.Funds, Is.Zero);
            Assert.That(saveData.funds, Is.Zero);
            Assert.That(coordinator.CurrentState.Funds, Is.Zero);
            Assert.That(
                coordinator.CurrentState.MaintenanceLevels,
                Has.Some.Matches<MaintenanceLevel>(level =>
                    level.Id == MaintenanceCatalog.C01 && level.Level == 1));
        }

        [Test]
        public void TryPurchaseMaintenance_RejectsPlayingWithoutMutation()
        {
            var saveData = SaveData.CreateNew("demo");
            saveData.funds = 10;
            using var coordinator = CreateCoordinator(
                CreateDestinations(10, 1, 1),
                out var ledger,
                saveData);
            coordinator.RequestStageStart();
            coordinator.Tick(PhaseSeconds);

            var result = coordinator.TryPurchaseMaintenance(MaintenanceCatalog.C01);

            Assert.That(result, Is.EqualTo(MaintenancePurchaseResult.InvalidPhase));
            Assert.That(ledger.Funds, Is.EqualTo(10));
            Assert.That(saveData.funds, Is.EqualTo(10));
            Assert.That(saveData.maintenanceLevels, Is.Empty);
        }

        [Test]
        public void TryPurchaseMaintenance_ExactTargetRejectsStaleAndSkippedSteps()
        {
            var saveData = SaveData.CreateNew("demo");
            saveData.funds = 570;
            saveData.maintenanceLevels.Add(new SaveMaintenanceLevel(MaintenanceCatalog.C01, 1));
            using var coordinator = CreateCoordinator(
                CreateSmallDestinations(),
                out var ledger,
                saveData);

            Assert.That(
                coordinator.TryPurchaseMaintenance(MaintenanceCatalog.D01, 2),
                Is.EqualTo(MaintenancePurchaseResult.Locked));
            Assert.That(
                coordinator.TryPurchaseMaintenance(MaintenanceCatalog.D01, 1),
                Is.EqualTo(MaintenancePurchaseResult.Success));
            Assert.That(
                coordinator.TryPurchaseMaintenance(MaintenanceCatalog.D01, 1),
                Is.EqualTo(MaintenancePurchaseResult.Locked));
            Assert.That(
                coordinator.TryPurchaseMaintenance(MaintenanceCatalog.D01, 3),
                Is.EqualTo(MaintenancePurchaseResult.Locked));
            Assert.That(ledger.Funds, Is.Zero);
            Assert.That(
                saveData.maintenanceLevels,
                Has.Some.Matches<SaveMaintenanceLevel>(level =>
                    level.id == MaintenanceCatalog.D01 && level.level == 1));
        }

        [Test]
        public void RequestStageStart_PublishesLatestD04ConfigBeforeCountdown()
        {
            var saveData = SaveData.CreateNew("demo");
            saveData.funds = 24_900;
            using var coordinator = CreateCoordinator(
                CreateDestinations(10, 1, 1),
                out _,
                saveData);
            GamePhase? phaseAtPreparation = null;
            CombatConfig? prepared = null;
            coordinator.StageConfigurationPrepared += config =>
            {
                phaseAtPreparation = coordinator.CurrentState.Phase;
                prepared = config;
            };

            Assert.That(coordinator.TryPurchaseMaintenance(MaintenanceCatalog.C01), Is.EqualTo(MaintenancePurchaseResult.Success));
            Assert.That(coordinator.TryPurchaseMaintenance(MaintenanceCatalog.D02), Is.EqualTo(MaintenancePurchaseResult.Success));
            Assert.That(coordinator.TryPurchaseMaintenance(MaintenanceCatalog.D04), Is.EqualTo(MaintenancePurchaseResult.Success));
            Assert.That(coordinator.TryPurchaseMaintenance(MaintenanceCatalog.D04), Is.EqualTo(MaintenancePurchaseResult.Success));
            Assert.That(coordinator.TryPurchaseMaintenance(MaintenanceCatalog.D04), Is.EqualTo(MaintenancePurchaseResult.Success));

            coordinator.RequestStageStart();

            Assert.That(phaseAtPreparation, Is.EqualTo(GamePhase.Ready));
            Assert.That(prepared, Is.Not.Null);
            Assert.That(prepared!.DirectAttack.CursorRadiusReferencePixels, Is.EqualTo(104f));
            Assert.That(coordinator.CurrentState.Phase, Is.EqualTo(GamePhase.Countdown));
        }

        [Test]
        public void CurrentStageConfig_RemainsFrozenAfterPlayingBegins()
        {
            var saveData = SaveData.CreateNew("demo");
            saveData.funds = 300;
            saveData.maintenanceLevels.Add(new SaveMaintenanceLevel(MaintenanceCatalog.D04, 1));
            using var coordinator = CreateCoordinator(
                CreateDestinations(10, 1, 1),
                out _,
                saveData);

            coordinator.RequestStageStart();
            coordinator.Tick(PhaseSeconds);
            var frozenConfig = coordinator.CurrentCombatConfig;

            Assert.That(
                coordinator.TryPurchaseMaintenance(MaintenanceCatalog.D04),
                Is.EqualTo(MaintenancePurchaseResult.InvalidPhase));
            Assert.That(coordinator.CurrentCombatConfig, Is.SameAs(frozenConfig));
            Assert.That(
                coordinator.CurrentCombatConfig.DirectAttack.CursorRadiusReferencePixels,
                Is.EqualTo(72f));
        }

        [Test]
        public void C02Purchase_AppliesToNextStageReward()
        {
            var saveData = SaveData.CreateNew("demo");
            saveData.funds = 560;
            using var coordinator = CreateCoordinator(
                CreateDestinations(10, 1, 1),
                out _,
                saveData);
            var rewards = new List<RewardGrantedEvent>();
            coordinator.RewardGranted += rewards.Add;

            coordinator.RequestStageStart();
            coordinator.Tick(PhaseSeconds);
            Assert.That(coordinator.TryApproveDestruction(CreateDestruction(1)), Is.True);
            coordinator.Tick(PhaseSeconds);
            coordinator.Tick(PhaseSeconds);
            coordinator.ContinueSettlement();
            coordinator.Tick(PhaseSeconds);

            Assert.That(coordinator.TryPurchaseMaintenance(MaintenanceCatalog.C01), Is.EqualTo(MaintenancePurchaseResult.Success));
            Assert.That(coordinator.TryPurchaseMaintenance(MaintenanceCatalog.C02), Is.EqualTo(MaintenancePurchaseResult.Success));

            coordinator.RequestStageStart();
            coordinator.Tick(PhaseSeconds);
            Assert.That(coordinator.TryApproveDestruction(CreateDestruction(2)), Is.True);

            Assert.That(rewards, Has.Count.EqualTo(2));
            Assert.That(rewards[0].FundsGranted, Is.EqualTo(10));
            Assert.That(rewards[1].FundsGranted, Is.EqualTo(11));
        }

        [Test]
        public void SettingsPause_FreezesTravelAndCompensatesSavedAvailabilityOnClose()
        {
            var now = new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
            var saveData = SaveData.CreateNew("demo");
            var loop = new GameLoopController(60d, 3d, 10d);
            var ledger = new ProgressionLedger(
                CreateSmallDestinations(),
                RewardTable.CreateDefault());
            var saveService = new SaveService(new SaveStore(tempDirectory), saveData);
            var maintenanceCore = new MaintenanceCore(
                MaintenanceCatalog.CreateDemo(),
                ledger,
                saveService);
            using var coordinator = new Int02LoopCoordinator(
                loop,
                ledger,
                maintenanceCore,
                saveService,
                () => now);
            coordinator.Tick(2d);

            coordinator.OpenSettings();
            coordinator.OpenSettings();
            coordinator.SetMasterVolume(0.4f);
            coordinator.Tick(100d);
            Assert.That(saveService.HasPendingWrite, Is.True);
            Assert.That(saveService.FlushCount, Is.Zero);
            now = now.AddSeconds(30d);
            coordinator.CloseSettings();
            coordinator.CloseSettings();

            Assert.That(loop.IsPaused, Is.False);
            Assert.That(loop.VoyageRemainingSeconds, Is.EqualTo(8d));
            Assert.That(
                DateTimeOffset.Parse(saveData.nextAvailableAtUtc),
                Is.EqualTo(now.AddSeconds(8d)));
            Assert.That(saveService.FlushCount, Is.EqualTo(1));
        }

        [Test]
        public void SettingsValues_PersistTogetherWhenModalCloses()
        {
            var saveData = SaveData.CreateNew("demo");
            using var coordinator = CreateCoordinator(
                CreateSmallDestinations(),
                out _,
                saveData);

            coordinator.OpenSettings();
            coordinator.SetMasterVolume(0.35f);
            coordinator.SetScreenShakeEnabled(false);
            coordinator.CloseSettings();

            var restored = new SaveStore(tempDirectory).TryLoad("demo");
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored!.masterVolume, Is.EqualTo(0.35f));
            Assert.That(restored.screenShakeEnabled, Is.False);
            Assert.That(coordinator.MasterVolume, Is.EqualTo(0.35f));
            Assert.That(coordinator.ScreenShakeEnabled, Is.False);
        }

        [Test]
        public void RouteStatusFactory_UsesLedgerForCompletedAndUpcomingDestinations()
        {
            var destinations = CreateDestinations(2, 3, 4);
            var ledger = new ProgressionLedger(
                destinations,
                RewardTable.CreateDefault(),
                initialDestinationIndex: 1,
                initialDestinationProgress: 2,
                initialCompletedDestinationIds: new[] { "island-village" });

            var route = CreateRouteStatus(ledger, destinations);

            Assert.That(GetRouteValue(route, "CurrentDestinationName"), Is.EqualTo("등대항"));
            Assert.That(GetRouteValue(route, "Progress"), Is.EqualTo(2));
            Assert.That(GetRouteValue(route, "Target"), Is.EqualTo(3));
            Assert.That(GetRouteValue(route, "CargoText"), Is.EqualTo("발전기 연료·의약품"));
            Assert.That(GetRouteValue(route, "CompletedDestinationsText"), Is.EqualTo("섬마을"));
            Assert.That(GetRouteValue(route, "UpcomingDestinationsText"), Is.EqualTo("북쪽 기지"));
            Assert.That(GetRouteValue(route, "GameCompleted"), Is.False);
        }

        [Test]
        public void RouteStatusFactory_ShowsCompletedVoyageWithoutUpcomingDestination()
        {
            var destinations = CreateDestinations(1, 1, 1);
            var ledger = new ProgressionLedger(
                destinations,
                RewardTable.CreateDefault(),
                initialDestinationIndex: 2,
                initialDestinationProgress: 1,
                initialCompletedDestinationIds: new[]
                {
                    "island-village", "lighthouse-port", "northern-base"
                },
                initialGameCompleted: true);

            var route = CreateRouteStatus(ledger, destinations);

            Assert.That(GetRouteValue(route, "CurrentDestinationName"), Is.EqualTo("북쪽 기지"));
            Assert.That(GetRouteValue(route, "CompletedDestinationsText"),
                Is.EqualTo("섬마을 → 등대항 → 북쪽 기지"));
            Assert.That(GetRouteValue(route, "UpcomingDestinationsText"), Is.EqualTo("없음"));
            Assert.That(GetRouteValue(route, "GameCompleted"), Is.True);
        }

        private Int02LoopCoordinator CreateCoordinator(
            DestinationDefinition[] destinations,
            out ProgressionLedger ledger,
            SaveData? saveData = null)
        {
            var loop = new GameLoopController(
                PhaseSeconds,
                PhaseSeconds,
                PhaseSeconds,
                PhaseSeconds);
            loop.Tick(PhaseSeconds);
            var effectiveSaveData = saveData ?? SaveData.CreateNew("demo");
            ledger = new ProgressionLedger(
                destinations,
                RewardTable.CreateDefault(),
                initialFunds: effectiveSaveData.funds);
            var saveService = new SaveService(
                new SaveStore(tempDirectory),
                effectiveSaveData);
            var maintenanceCore = new MaintenanceCore(
                MaintenanceCatalog.CreateDemo(),
                ledger,
                saveService);
            return new Int02LoopCoordinator(
                loop,
                ledger,
                maintenanceCore,
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

        private static object CreateRouteStatus(
            ProgressionLedger ledger,
            DestinationDefinition[] destinations)
        {
            Type? factoryType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                factoryType = assembly.GetType(
                    "Icebreaker.UI.Management.RouteStatusViewDataFactory",
                    throwOnError: false);
                if (factoryType != null)
                {
                    break;
                }
            }

            Assert.That(factoryType, Is.Not.Null, "Route status factory assembly was not loaded.");
            return factoryType!.GetMethod("Create")!.Invoke(
                null,
                new object[] { ledger, destinations })!;
        }

        private static object? GetRouteValue(object route, string propertyName) =>
            route.GetType().GetProperty(propertyName)!.GetValue(route);
    }
}
