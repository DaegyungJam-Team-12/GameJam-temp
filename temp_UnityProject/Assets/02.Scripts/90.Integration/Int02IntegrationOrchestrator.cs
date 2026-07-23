#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Core;
using Icebreaker.Gameplay;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Maintenance;
using Icebreaker.Shared.State;
using Icebreaker.UI.Feedback;
using Icebreaker.UI.Hud;
using Icebreaker.UI.Management;
using Icebreaker.UI.Maintenance;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Icebreaker.Integration
{
    [DefaultExecutionOrder(-1000)]
    public sealed class Int02IntegrationOrchestrator : MonoBehaviour,
        ICombatEventSource,
        IProgressionEventSource,
        IGameStateSource,
        IManagementScreenSource,
        IMaintenanceStepViewDataSource
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
        private MaintenanceTreePresenter? maintenanceTree;
        private ManagementViewsPresenter? managementViews;
        private Ui06FeedbackAudioPresenter? feedbackAudio;
        private ManagementScreen currentManagementScreen;
        private ManagementScreen managementScreenBeforeSettings;
        private bool stageStartRequestPending;
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

        public event Action<ManagementScreen> ManagementScreenChanged = delegate { };

        public event Action<IReadOnlyList<MaintenancePurchaseStepViewData>> StepsChanged = delegate { };

        public GameState CurrentState => coordinator?.CurrentState ??
            throw new InvalidOperationException("INT-02 loop is not initialized.");

        public ManagementScreen CurrentManagementScreen => currentManagementScreen;

        public IReadOnlyList<MaintenancePurchaseStepViewData> CurrentSteps =>
            coordinator?.GetMaintenancePurchaseStepViewData() ??
            Array.Empty<MaintenancePurchaseStepViewData>();

        public long CurrentFunds => coordinator?.CurrentState.Funds ?? 0L;

        public string CurrentPreviewStateLabel => "실제 저장";

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
            if (loadedData == null)
            {
                saveData.masterVolume = UiAudioSettings.LoadAndApplyMasterVolume();
            }

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
            var maintenanceCore = new MaintenanceCore(
                MaintenanceCatalog.CreateDemo(),
                ledger,
                saveService);
            coordinator = new Int02LoopCoordinator(
                loop,
                ledger,
                maintenanceCore,
                saveService);
            coordinator.StageConfigurationPrepared += HandleStageConfigurationPrepared;
            coordinator.StageStarted += HandleStageStarted;
            coordinator.RewardGranted += HandleRewardGranted;
            coordinator.StageEnded += HandleStageEnded;
            coordinator.SettlementReady += HandleSettlementReady;
            coordinator.ArrivalPresentationRequested += HandleArrivalPresentationRequested;
            coordinator.StateChanged += HandleCoordinatorStateChanged;
            coordinator.MaintenanceChanged += HandleMaintenanceChanged;

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
            maintenanceTree = FindFirstObjectByType<MaintenanceTreePresenter>(FindObjectsInactive.Include);
            managementViews = FindFirstObjectByType<ManagementViewsPresenter>(FindObjectsInactive.Include);
            feedbackAudio = FindFirstObjectByType<Ui06FeedbackAudioPresenter>(FindObjectsInactive.Include);
            if (launcherHud == null || icebreakingHud == null || settlementPresenter == null ||
                maintenanceTree == null)
            {
                Debug.LogError("[INT-TREE-01] Launcher, maintenance, icebreaking, or settlement UI is missing.", this);
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
            maintenanceTree.Bind(this);
            managementViews?.EnableFinalGameMode();
            feedbackAudio?.Bind(this, this);
            if (feedbackAudio != null)
            {
                feedbackAudio.SetMasterVolume(coordinator.MasterVolume);
                feedbackAudio.SetUiButtons(
                    FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            }

            launcherHud.StageStartRequested += HandleStageStartRequested;
            launcherHud.MaintenanceRequested += HandleMaintenanceRequested;
            launcherHud.RouteRequested += HandleRouteRequested;
            launcherHud.SettingsRequested += HandleSettingsRequested;
            icebreakingHud.SettingsRequested += HandleSettingsRequested;
            maintenanceTree.PurchaseRequested += HandleMaintenancePurchaseRequested;
            maintenanceTree.CloseRequested += HandleMaintenanceCloseRequested;
            maintenanceTree.StageStartRequested += HandleMaintenanceStageStartRequested;
            settlementPresenter.ContinueRequested += HandleContinueRequested;
            if (managementViews != null)
            {
                managementViews.StageStartRequested += HandleStageStartRequested;
                managementViews.CollapseRequested += HandleManagementCollapseRequested;
                managementViews.SettingsVisibilityChanged += HandleSettingsVisibilityChanged;
                managementViews.MasterVolumeChanged += HandleMasterVolumeChanged;
                managementViews.ScreenShakeChanged += HandleScreenShakeChanged;
                managementViews.QuitRequested += HandleQuitRequested;
                managementViews.ResetSaveRequested += HandleResetSaveRequested;
            }

            ApplyViewState(coordinator.CurrentState);
            coordinator.EnsureInitialized();
            Debug.Log("[INT-02] demo loop wired: state -> gameplay -> UI -> settlement -> save.", this);
        }

        private void Update()
        {
            coordinator?.Tick(Time.unscaledDeltaTime);
        }

        private void OnApplicationQuit()
        {
            managementViews?.CloseSettings();
            CloseManagementScreen();
            FlushForShutdown();
        }

        private void OnDestroy()
        {
            // Do not manipulate child UI here: during scene unload (e.g. save-reset reload)
            // those views may already be destroyed, and CloseSettings() would touch a dead
            // GameObject. Live-teardown UI cleanup belongs to OnApplicationQuit. OnDestroy only
            // unsubscribes events (Unity's != null skips destroyed refs) and disposes the loop.

            if (launcherHud != null)
            {
                launcherHud.StageStartRequested -= HandleStageStartRequested;
                launcherHud.MaintenanceRequested -= HandleMaintenanceRequested;
                launcherHud.RouteRequested -= HandleRouteRequested;
                launcherHud.SettingsRequested -= HandleSettingsRequested;
            }

            if (icebreakingHud != null)
            {
                icebreakingHud.SettingsRequested -= HandleSettingsRequested;
            }

            if (maintenanceTree != null)
            {
                maintenanceTree.PurchaseRequested -= HandleMaintenancePurchaseRequested;
                maintenanceTree.CloseRequested -= HandleMaintenanceCloseRequested;
                maintenanceTree.StageStartRequested -= HandleMaintenanceStageStartRequested;
            }

            if (settlementPresenter != null)
            {
                settlementPresenter.ContinueRequested -= HandleContinueRequested;
            }

            if (managementViews != null)
            {
                managementViews.StageStartRequested -= HandleStageStartRequested;
                managementViews.CollapseRequested -= HandleManagementCollapseRequested;
                managementViews.SettingsVisibilityChanged -= HandleSettingsVisibilityChanged;
                managementViews.MasterVolumeChanged -= HandleMasterVolumeChanged;
                managementViews.ScreenShakeChanged -= HandleScreenShakeChanged;
                managementViews.QuitRequested -= HandleQuitRequested;
                managementViews.ResetSaveRequested -= HandleResetSaveRequested;
            }

            if (combatSource != null)
            {
                combatSource.DamageApplied -= HandleDamageApplied;
                combatSource.SupportChargeChanged -= HandleSupportChargeChanged;
                combatSource.IceDestroyed -= HandleIceDestroyed;
            }

            if (coordinator != null)
            {
                coordinator.StageConfigurationPrepared -= HandleStageConfigurationPrepared;
                coordinator.StageStarted -= HandleStageStarted;
                coordinator.RewardGranted -= HandleRewardGranted;
                coordinator.StageEnded -= HandleStageEnded;
                coordinator.SettlementReady -= HandleSettlementReady;
                coordinator.ArrivalPresentationRequested -= HandleArrivalPresentationRequested;
                coordinator.StateChanged -= HandleCoordinatorStateChanged;
                coordinator.MaintenanceChanged -= HandleMaintenanceChanged;
                FlushForShutdown();
                coordinator.Dispose();
            }
        }

        public void EnsureInitialized() => coordinator?.EnsureInitialized();

        public bool RequestManagementScreen(ManagementScreen screen)
        {
            if (screen == ManagementScreen.None)
            {
                CloseManagementScreen();
                return true;
            }

            if (screen == ManagementScreen.Settings)
            {
                if (coordinator == null || managementViews == null ||
                    !ManagementScreenRules.CanOpen(screen, coordinator.CurrentState.Phase))
                {
                    return false;
                }

                HandleSettingsRequested();
                return currentManagementScreen == ManagementScreen.Settings;
            }

            if (coordinator == null ||
                !ManagementScreenRules.CanOpen(screen, coordinator.CurrentState.Phase))
            {
                return false;
            }

            SetManagementScreen(screen);
            if (screen == ManagementScreen.Route)
            {
                RenderRouteStatus();
            }

            ApplyViewState(coordinator.CurrentState);
            return true;
        }

        public void CloseManagementScreen()
        {
            if (currentManagementScreen == ManagementScreen.Settings)
            {
                if (managementViews?.IsSettingsVisible == true)
                {
                    managementViews.CloseSettings();
                }
                else
                {
                    coordinator?.CloseSettings();
                    RestoreManagementScreenAfterSettings();
                }

                return;
            }

            if (currentManagementScreen == ManagementScreen.None)
            {
                return;
            }

            SetManagementScreen(ManagementScreen.None);
            if (coordinator != null)
            {
                ApplyViewState(coordinator.CurrentState);
            }
        }

        private void HandleStageStartRequested()
        {
            if (coordinator?.CurrentState.CanStartStage == true)
            {
                coordinator.RequestStageStart();
            }
        }

        private void HandleMaintenanceRequested() =>
            RequestManagementScreen(ManagementScreen.Maintenance);

        private void HandleRouteRequested() =>
            RequestManagementScreen(ManagementScreen.Route);

        private void HandleSettingsRequested()
        {
            if (coordinator == null || managementViews == null || managementViews.IsSettingsVisible ||
                !ManagementScreenRules.CanOpen(
                    ManagementScreen.Settings,
                    coordinator.CurrentState.Phase))
            {
                return;
            }

            managementViews.SetSettingsValues(
                coordinator.MasterVolume,
                coordinator.ScreenShakeEnabled);
            managementViews.OpenSettings();
        }

        private void HandleManagementCollapseRequested() => CloseManagementScreen();

        private void HandleSettingsVisibilityChanged(bool visible)
        {
            if (visible)
            {
                if (coordinator == null || !ManagementScreenRules.CanOpen(
                        ManagementScreen.Settings,
                        coordinator.CurrentState.Phase))
                {
                    managementViews?.CloseSettings();
                    return;
                }

                managementScreenBeforeSettings =
                    currentManagementScreen == ManagementScreen.Route
                        ? ManagementScreen.Route
                        : ManagementScreen.None;
                SetManagementScreen(ManagementScreen.Settings);
                coordinator.OpenSettings();
                ApplyViewState(coordinator.CurrentState);
            }
            else
            {
                coordinator?.CloseSettings();
                RestoreManagementScreenAfterSettings();
            }
        }

        private void HandleMasterVolumeChanged(float value)
        {
            coordinator?.SetMasterVolume(value);
            feedbackAudio?.SetMasterVolume(value);
        }

        private void HandleScreenShakeChanged(bool enabled) =>
            coordinator?.SetScreenShakeEnabled(enabled);

        private static void HandleQuitRequested() => Application.Quit();

        private void HandleResetSaveRequested()
        {
            // The coordinator deletes the file and permanently suspends saving, so neither the
            // debounce Tick nor the teardown flush (fired while this scene unloads) can recreate
            // it. Reloading the active scene then boots a brand-new game from the missing save,
            // resetting funds, maintenance levels, destination progress, and phase in place.
            coordinator?.ResetSave();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void HandleMaintenanceCloseRequested() => CloseManagementScreen();

        private void HandleMaintenanceStageStartRequested()
        {
            if (stageStartRequestPending || coordinator == null ||
                currentManagementScreen != ManagementScreen.Maintenance ||
                !coordinator.CurrentState.CanStartStage)
            {
                maintenanceTree?.RejectStageStartRequest();
                return;
            }

            stageStartRequestPending = true;
            coordinator.RequestStageStart();
        }

        private void HandleMaintenancePurchaseRequested(string stepId)
        {
            if (coordinator == null || currentManagementScreen != ManagementScreen.Maintenance)
            {
                StepsChanged(CurrentSteps);
                return;
            }

            MaintenancePurchaseStepViewData? requestedStep = null;
            foreach (var step in CurrentSteps)
            {
                if (string.Equals(step.StepId, stepId, StringComparison.Ordinal))
                {
                    requestedStep = step;
                    break;
                }
            }

            if (requestedStep == null || !requestedStep.CanPurchase ||
                coordinator.TryPurchaseMaintenance(
                    requestedStep.MaintenanceId,
                    requestedStep.TargetLevel) != MaintenancePurchaseResult.Success)
            {
                StepsChanged(CurrentSteps);
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

        private void HandleStageConfigurationPrepared(CombatConfig config)
        {
            iceFieldView?.InjectCombatConfig(config);
        }

        private void HandleStageStarted(StageStarted payload)
        {
            iceFieldView?.ResetStage();
            StageStarted(payload);
        }

        private void HandleRewardGranted(RewardGrantedEvent payload) => RewardGranted(payload);

        private void HandleStageEnded(StageEnded payload) => StageEnded(payload);

        private void HandleSettlementReady(SettlementReady payload) => SettlementReady(payload);

        private void HandleArrivalPresentationRequested(ArrivalPresentationRequested payload)
        {
            managementViews?.PresentArrival(payload);
            ArrivalPresentationRequested(payload);
        }

        private void HandleCoordinatorStateChanged(GameState state)
        {
            if ((currentManagementScreen == ManagementScreen.Maintenance ||
                 currentManagementScreen == ManagementScreen.Route) &&
                !ManagementScreenRules.CanOpen(currentManagementScreen, state.Phase))
            {
                SetManagementScreen(ManagementScreen.None);
            }

            if (state.Phase != GamePhase.Ready)
            {
                stageStartRequestPending = false;
            }

            StateChanged(state);
            if (currentManagementScreen == ManagementScreen.Route)
            {
                RenderRouteStatus();
            }

            ApplyViewState(state);
        }

        private void HandleMaintenanceChanged() => StepsChanged(CurrentSteps);

        private void ApplyViewState(GameState state)
        {
            if (launcherHud != null)
            {
                var showLauncher = state.Phase == GamePhase.Traveling ||
                                   state.Phase == GamePhase.Ready ||
                                   state.Phase == GamePhase.Completed;
                showLauncher &= currentManagementScreen == ManagementScreen.None;
                if (launcherHud.gameObject.activeSelf != showLauncher)
                {
                    launcherHud.gameObject.SetActive(showLauncher);
                }
            }

            if (maintenanceTree != null)
            {
                maintenanceTree.SetStageStartAvailable(state.CanStartStage);
                var showMaintenance = currentManagementScreen == ManagementScreen.Maintenance &&
                                      CanOpenManagement(state.Phase);
                if (maintenanceTree.gameObject.activeSelf != showMaintenance)
                {
                    maintenanceTree.gameObject.SetActive(showMaintenance);
                }
            }

            managementViews?.SetRouteVisible(
                currentManagementScreen == ManagementScreen.Route && CanOpenManagement(state.Phase));

            if (iceFieldView != null)
            {
                iceFieldView.enabled = state.Phase == GamePhase.Countdown ||
                                       state.Phase == GamePhase.Playing;
            }
        }

        private void SetManagementScreen(ManagementScreen screen)
        {
            if (currentManagementScreen == screen)
            {
                return;
            }

            currentManagementScreen = screen;
            ManagementScreenChanged(screen);
        }

        private void RenderRouteStatus()
        {
            if (coordinator == null || managementViews == null)
            {
                return;
            }

            var state = coordinator.CurrentState;
            managementViews.Render(
                Array.Empty<MaintenanceNodeViewData>(),
                RouteStatusViewDataFactory.Create(
                    coordinator.Ledger,
                    DestinationCatalog.CreateDemo()),
                state.Funds,
                state.RemainingSeconds,
                state.CanStartStage);
        }

        private void RestoreManagementScreenAfterSettings()
        {
            var screen = managementScreenBeforeSettings;
            managementScreenBeforeSettings = ManagementScreen.None;
            if (coordinator == null ||
                (screen != ManagementScreen.None &&
                 !ManagementScreenRules.CanOpen(screen, coordinator.CurrentState.Phase)))
            {
                screen = ManagementScreen.None;
            }

            SetManagementScreen(screen);
            if (screen == ManagementScreen.Route)
            {
                RenderRouteStatus();
            }

            if (coordinator != null)
            {
                ApplyViewState(coordinator.CurrentState);
            }
        }

        private static bool CanOpenManagement(GamePhase phase) =>
            phase == GamePhase.Traveling || phase == GamePhase.Ready;

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
                initialDestinationIndex: saveData.currentDestinationIndex,
                initialDestinationProgress: saveData.destinationProgress,
                initialCompletedDestinationIds: saveData.completedDestinationIds,
                initialPendingArrivalDestinationId: saveData.pendingArrivalDestinationId,
                initialGameCompleted: saveData.gameCompleted);
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
