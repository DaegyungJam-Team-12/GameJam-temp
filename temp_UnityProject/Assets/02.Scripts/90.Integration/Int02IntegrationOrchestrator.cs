#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Core;
using Icebreaker.Gameplay;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.State;
using Icebreaker.UI.Hud;
using UnityEngine;

namespace Icebreaker.Integration
{
    [DefaultExecutionOrder(-1000)]
    public sealed class Int02IntegrationOrchestrator : MonoBehaviour,
        ICombatEventSource,
        IProgressionEventSource,
        IGameStateSource
    {
        public const string DemoProfileId = "demo";
        public const double DemoStageSeconds = 60d;
        public const double DemoCountdownSeconds = 3d;
        public const double DemoVoyageSeconds = 10d;

        private Int02LoopCoordinator? coordinator;
        private ICombatEventSource? combatSource;
        private IceFieldView? iceFieldView;
        private LauncherHudPresenter? launcherHud;
        private IcebreakingHudPresenter? icebreakingHud;
        private RewardSettlementPresenter? settlementPresenter;
        private bool shutdownFlushed;

        public event Action<DamageAppliedEvent> DamageApplied = delegate { };

        public event Action<SupportChargeChangedEvent> SupportChargeChanged = delegate { };

        public event Action<IceDestroyedEvent> IceDestroyed = delegate { };

        public event Action<StageStarted> StageStarted = delegate { };

        public event Action<RewardGrantedEvent> RewardGranted = delegate { };

        public event Action<StageEnded> StageEnded = delegate { };

        public event Action<SettlementReady> SettlementReady = delegate { };

        public event Action<ArrivalPresentationRequested> ArrivalPresentationRequested = delegate { };

        public event Action<GameState> StateChanged = delegate { };

        public GameState CurrentState => coordinator?.CurrentState ??
            throw new InvalidOperationException("INT-02 loop is not initialized.");

        private void Awake()
        {
            iceFieldView = FindFirstObjectByType<IceFieldView>();
            if (iceFieldView == null)
            {
                Debug.LogError("[INT-02] IceFieldView is missing from the integration scene.", this);
                enabled = false;
                return;
            }

            var store = new SaveStore(Application.persistentDataPath);
            var loadedData = store.TryLoad(DemoProfileId);
            var saveData = loadedData ?? SaveData.CreateNew(DemoProfileId);
            var now = DateTimeOffset.UtcNow;
            var bootState = SaveBootResolver.Resolve(loadedData, now, DemoVoyageSeconds);
            var ledger = CreateLedger(saveData);
            var recoveredArrival = ApplyPendingArrival(ledger, saveData);
            var recoveredInterruptedStage = saveData.runInProgress;

            if (ledger.GameCompleted)
            {
                bootState = new SaveBootResolver.BootState(GamePhase.Completed, 0d);
            }
            else if (recoveredArrival)
            {
                bootState = new SaveBootResolver.BootState(GamePhase.Traveling, DemoVoyageSeconds);
            }

            if (recoveredArrival || recoveredInterruptedStage)
            {
                saveData.runInProgress = false;
                saveData.nextAvailableAtUtc = ledger.GameCompleted
                    ? ""
                    : (now + TimeSpan.FromSeconds(DemoVoyageSeconds)).ToString("O");
            }

            var saveService = new SaveService(store, saveData);
            if (recoveredArrival || recoveredInterruptedStage)
            {
                saveService.MarkDirty();
                saveService.Flush();
            }

            var loop = CreateLoop(bootState);
            coordinator = new Int02LoopCoordinator(loop, ledger, saveService);
            coordinator.StageStarted += HandleStageStarted;
            coordinator.RewardGranted += HandleRewardGranted;
            coordinator.StageEnded += HandleStageEnded;
            coordinator.SettlementReady += HandleSettlementReady;
            coordinator.ArrivalPresentationRequested += HandleArrivalPresentationRequested;
            coordinator.StateChanged += HandleCoordinatorStateChanged;

            iceFieldView.InjectStageClock(loop);
            iceFieldView.InjectCombatConfig(coordinator.CurrentCombatConfig);
        }

        private void Start()
        {
            if (coordinator == null || iceFieldView == null)
            {
                return;
            }

            launcherHud = FindFirstObjectByType<LauncherHudPresenter>();
            icebreakingHud = FindFirstObjectByType<IcebreakingHudPresenter>();
            settlementPresenter = FindFirstObjectByType<RewardSettlementPresenter>();
            if (launcherHud == null || icebreakingHud == null || settlementPresenter == null)
            {
                Debug.LogError("[INT-02] Launcher, icebreaking, or settlement HUD is missing.", this);
                enabled = false;
                return;
            }

            combatSource = iceFieldView.Source;
            combatSource.DamageApplied += HandleDamageApplied;
            combatSource.SupportChargeChanged += HandleSupportChargeChanged;
            combatSource.IceDestroyed += HandleIceDestroyed;

            launcherHud.Bind(this);
            icebreakingHud.Bind(this);
            settlementPresenter.Bind(this, this, this);
            settlementPresenter.SetInputTargets(iceFieldView);
            launcherHud.StageStartRequested += HandleStageStartRequested;
            settlementPresenter.ContinueRequested += HandleContinueRequested;

            ApplyViewState(coordinator.CurrentState);
            coordinator.EnsureInitialized();
            Debug.Log("[INT-02] demo loop wired: state -> gameplay -> UI -> settlement -> save.", this);
        }

        private void Update()
        {
            coordinator?.Tick(Time.unscaledDeltaTime);
        }

        private void OnApplicationQuit() => FlushForShutdown();

        private void OnDestroy()
        {
            if (launcherHud != null)
            {
                launcherHud.StageStartRequested -= HandleStageStartRequested;
            }

            if (settlementPresenter != null)
            {
                settlementPresenter.ContinueRequested -= HandleContinueRequested;
            }

            if (combatSource != null)
            {
                combatSource.DamageApplied -= HandleDamageApplied;
                combatSource.SupportChargeChanged -= HandleSupportChargeChanged;
                combatSource.IceDestroyed -= HandleIceDestroyed;
            }

            if (coordinator != null)
            {
                coordinator.StageStarted -= HandleStageStarted;
                coordinator.RewardGranted -= HandleRewardGranted;
                coordinator.StageEnded -= HandleStageEnded;
                coordinator.SettlementReady -= HandleSettlementReady;
                coordinator.ArrivalPresentationRequested -= HandleArrivalPresentationRequested;
                coordinator.StateChanged -= HandleCoordinatorStateChanged;
                FlushForShutdown();
                coordinator.Dispose();
            }
        }

        public void EnsureInitialized() => coordinator?.EnsureInitialized();

        private void HandleStageStartRequested()
        {
            if (coordinator?.CurrentState.CanStartStage == true)
            {
                coordinator.RequestStageStart();
            }
        }

        private void HandleContinueRequested() => coordinator?.ContinueSettlement();

        private void HandleDamageApplied(DamageAppliedEvent payload)
        {
            if (coordinator == null)
            {
                return;
            }

            DamageApplied(new DamageAppliedEvent(
                coordinator.CurrentStageId,
                payload.IceInstanceId,
                payload.ChainId,
                payload.ChainDepth,
                payload.EffectType,
                payload.Damage,
                payload.RemainingHp,
                payload.WasCritical,
                payload.ReferencePosition,
                payload.StageElapsedSeconds));
        }

        private void HandleSupportChargeChanged(SupportChargeChangedEvent payload)
        {
            if (coordinator != null)
            {
                SupportChargeChanged(new SupportChargeChangedEvent(
                    coordinator.CurrentStageId,
                    payload.CurrentCharge,
                    payload.MaxCharge));
            }
        }

        private void HandleIceDestroyed(IceDestroyedEvent payload)
        {
            if (coordinator == null)
            {
                return;
            }

            var normalized = new IceDestroyedEvent(
                coordinator.CurrentStageId,
                payload.IceInstanceId,
                payload.ChainId,
                payload.ChainDepth,
                payload.Tier,
                payload.SpecialType,
                payload.DestroyCategory,
                payload.EffectType,
                payload.ReferencePosition,
                payload.StageElapsedSeconds);
            IceDestroyed(normalized);
            coordinator.TryApproveDestruction(normalized);
        }

        private void HandleStageStarted(StageStarted payload)
        {
            iceFieldView?.ResetStage();
            StageStarted(payload);
        }

        private void HandleRewardGranted(RewardGrantedEvent payload) => RewardGranted(payload);

        private void HandleStageEnded(StageEnded payload) => StageEnded(payload);

        private void HandleSettlementReady(SettlementReady payload) => SettlementReady(payload);

        private void HandleArrivalPresentationRequested(ArrivalPresentationRequested payload) =>
            ArrivalPresentationRequested(payload);

        private void HandleCoordinatorStateChanged(GameState state)
        {
            StateChanged(state);
            ApplyViewState(state);
        }

        private void ApplyViewState(GameState state)
        {
            if (launcherHud != null)
            {
                var showLauncher = state.Phase == GamePhase.Traveling ||
                                   state.Phase == GamePhase.Ready ||
                                   state.Phase == GamePhase.Completed;
                if (launcherHud.gameObject.activeSelf != showLauncher)
                {
                    launcherHud.gameObject.SetActive(showLauncher);
                }
            }

            if (iceFieldView != null)
            {
                iceFieldView.enabled = state.Phase == GamePhase.Countdown ||
                                       state.Phase == GamePhase.Playing;
            }
        }

        private void FlushForShutdown()
        {
            if (shutdownFlushed || coordinator == null)
            {
                return;
            }

            coordinator.Flush();
            shutdownFlushed = true;
        }

        private static ProgressionLedger CreateLedger(SaveData saveData)
        {
            return new ProgressionLedger(
                DestinationCatalog.CreateDemo(),
                RewardTable.CreateDefault(),
                initialFunds: saveData.funds,
                maintenanceEfficiencyLevel: CombatConfigFactory.GetMaintenanceEfficiencyLevel(
                    CreateMaintenanceLevels(saveData.maintenanceLevels)),
                initialDestinationIndex: saveData.currentDestinationIndex,
                initialDestinationProgress: saveData.destinationProgress,
                initialCompletedDestinationIds: saveData.completedDestinationIds,
                initialPendingArrivalDestinationId: saveData.pendingArrivalDestinationId,
                initialGameCompleted: saveData.gameCompleted);
        }

        private static MaintenanceLevel[] CreateMaintenanceLevels(
            IReadOnlyList<SaveMaintenanceLevel> savedLevels)
        {
            var levels = new MaintenanceLevel[savedLevels.Count];
            for (var index = 0; index < savedLevels.Count; index++)
            {
                levels[index] = new MaintenanceLevel(
                    savedLevels[index].id,
                    savedLevels[index].level);
            }

            return levels;
        }

        private static bool ApplyPendingArrival(ProgressionLedger ledger, SaveData saveData)
        {
            if (ledger.PendingArrivalDestinationId == null)
            {
                return false;
            }

            if (!ledger.ApplyArrival())
            {
                return false;
            }

            saveData.funds = ledger.Funds;
            saveData.currentDestinationIndex = ledger.CurrentDestinationIndex;
            saveData.destinationProgress = ledger.DestinationProgress;
            saveData.completedDestinationIds = new List<string>(ledger.CompletedDestinationIds);
            saveData.pendingArrivalDestinationId = "";
            saveData.gameCompleted = ledger.GameCompleted;
            return true;
        }

        private static GameLoopController CreateLoop(SaveBootResolver.BootState bootState)
        {
            var loop = new GameLoopController(
                DemoStageSeconds,
                DemoCountdownSeconds,
                DemoVoyageSeconds);

            switch (bootState.Phase)
            {
                case GamePhase.Traveling:
                    loop.Tick(Math.Max(0d, DemoVoyageSeconds - bootState.VoyageRemainingSeconds));
                    break;

                case GamePhase.Ready:
                    loop.Tick(DemoVoyageSeconds);
                    break;

                case GamePhase.Completed:
                    loop.Tick(DemoVoyageSeconds);
                    loop.RequestStageStart();
                    loop.Tick(DemoCountdownSeconds);
                    loop.Tick(DemoStageSeconds);
                    loop.EnterSettlement();
                    loop.CompleteSettlement(true);
                    loop.CompleteArrival(true);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported boot phase {bootState.Phase}.");
            }

            return loop;
        }
    }
}
