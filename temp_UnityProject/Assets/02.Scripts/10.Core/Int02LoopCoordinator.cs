#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Maintenance;
using Icebreaker.Shared.Progression;
using Icebreaker.Shared.State;

namespace Icebreaker.Core
{
    public sealed class Int02LoopCoordinator : IProgressionEventSource, IDisposable
    {
        private readonly GameLoopController loop;
        private readonly ProgressionLedger ledger;
        private readonly MaintenanceCore maintenanceCore;
        private readonly SaveService saveService;
        private readonly SaveData saveData;
        private readonly Func<DateTimeOffset> utcNow;

        private CombatConfig? currentStageCombatConfig;
        private int currentStageMaintenanceEfficiencyLevel;
        private SettlementSummary? pendingSettlement;
        private long currentStageId;
        private bool disposed;

        public Int02LoopCoordinator(
            GameLoopController loop,
            ProgressionLedger ledger,
            MaintenanceCore maintenanceCore,
            SaveService saveService,
            Func<DateTimeOffset>? utcNow = null)
        {
            this.loop = loop ?? throw new ArgumentNullException(nameof(loop));
            this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            this.maintenanceCore = maintenanceCore ??
                throw new ArgumentNullException(nameof(maintenanceCore));
            this.saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
            saveData = saveService.Data;
            this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
            loop.PhaseChanged += HandlePhaseChanged;
        }

        public event Action<CombatConfig> StageConfigurationPrepared = delegate { };

        public event Action<StageStarted> StageStarted = delegate { };

        public event Action<RewardGrantedEvent> RewardGranted = delegate { };

        public event Action<StageEnded> StageEnded = delegate { };

        public event Action<SettlementReady> SettlementReady = delegate { };

        public event Action<ArrivalPresentationRequested> ArrivalPresentationRequested = delegate { };

        public event Action<GameState> StateChanged = delegate { };

        public event Action MaintenanceChanged = delegate { };

        public GameLoopController Loop => loop;

        public ProgressionLedger Ledger => ledger;

        public long CurrentStageId => currentStageId;

        public GameState CurrentState => CreateState();

        public CombatConfig CurrentCombatConfig => currentStageCombatConfig ??
            CombatConfigFactory.Build(maintenanceCore.MaintenanceLevels);

        public float MasterVolume => saveData.masterVolume;

        public bool ScreenShakeEnabled => saveData.screenShakeEnabled;

        public void EnsureInitialized() => PublishState();

        public void Tick(double unscaledDeltaSeconds)
        {
            ThrowIfDisposed();
            loop.Tick(unscaledDeltaSeconds);
            if (!loop.IsPaused)
            {
                saveService.Tick(unscaledDeltaSeconds);
            }

            PublishState();
        }

        public void OpenSettings()
        {
            ThrowIfDisposed();
            if (loop.IsPaused)
            {
                return;
            }

            loop.SetSettingsPaused(true);
            PublishState();
        }

        public void CloseSettings()
        {
            ThrowIfDisposed();
            if (!loop.IsPaused)
            {
                return;
            }

            loop.SetSettingsPaused(false);
            if (loop.Phase == GamePhase.Traveling)
            {
                saveData.nextAvailableAtUtc = FormatUtc(
                    utcNow() + TimeSpan.FromSeconds(loop.VoyageRemainingSeconds));
            }

            CopyProgressionToSave();
            saveService.MarkDirty();
            saveService.Flush();
            PublishState();
        }

        public void SetMasterVolume(float value)
        {
            ThrowIfDisposed();
            saveData.masterVolume = Math.Max(0f, Math.Min(1f, value));
            saveService.MarkDirty();
        }

        public void SetScreenShakeEnabled(bool enabled)
        {
            ThrowIfDisposed();
            saveData.screenShakeEnabled = enabled;
            saveService.MarkDirty();
        }

        public void RequestStageStart()
        {
            ThrowIfDisposed();
            if (loop.Phase != GamePhase.Ready)
            {
                loop.RequestStageStart();
                return;
            }

            currentStageCombatConfig = CombatConfigFactory.Build(maintenanceCore.MaintenanceLevels);
            currentStageMaintenanceEfficiencyLevel = maintenanceCore.MaintenanceEfficiencyLevel;
            StageConfigurationPrepared(currentStageCombatConfig);
            loop.RequestStageStart();
        }

        public IReadOnlyList<MaintenanceNodeViewData> GetMaintenanceNodeViewData()
        {
            ThrowIfDisposed();
            return maintenanceCore.GetNodeViewData();
        }

        public IReadOnlyList<MaintenancePurchaseStepViewData> GetMaintenancePurchaseStepViewData()
        {
            ThrowIfDisposed();
            return maintenanceCore.GetPurchaseStepViewData();
        }

        public MaintenancePurchaseResult TryPurchaseMaintenance(string nodeId)
        {
            ThrowIfDisposed();
            if (loop.Phase != GamePhase.Traveling && loop.Phase != GamePhase.Ready)
            {
                return MaintenancePurchaseResult.InvalidPhase;
            }

            var result = maintenanceCore.TryPurchaseDetailed(nodeId);
            if (result == MaintenancePurchaseResult.Success)
            {
                MaintenanceChanged();
                PublishState();
            }

            return result;
        }

        public MaintenancePurchaseResult TryPurchaseMaintenance(string nodeId, int targetLevel)
        {
            ThrowIfDisposed();
            if (loop.Phase != GamePhase.Traveling && loop.Phase != GamePhase.Ready)
            {
                return MaintenancePurchaseResult.InvalidPhase;
            }

            var result = maintenanceCore.TryPurchaseDetailed(nodeId, targetLevel);
            if (result == MaintenancePurchaseResult.Success)
            {
                MaintenanceChanged();
                PublishState();
            }

            return result;
        }

        public bool TryApproveDestruction(IceDestroyedEvent destruction)
        {
            ThrowIfDisposed();
            if (loop.Phase != GamePhase.Playing)
            {
                return false;
            }

            var normalized = NormalizeStageId(destruction);
            var pendingArrivalBeforeApproval = ledger.PendingArrivalDestinationId;
            if (!ledger.TryApproveDestruction(normalized, out var reward))
            {
                return false;
            }

            RewardGranted(reward);
            if (!saveData.firstDestroyShown)
            {
                saveData.firstDestroyShown = true;
            }

            CopyProgressionToSave();
            saveService.MarkDirty();
            if (pendingArrivalBeforeApproval == null && ledger.PendingArrivalDestinationId != null)
            {
                saveService.Flush();
            }

            PublishState();
            return true;
        }

        public void ContinueSettlement()
        {
            ThrowIfDisposed();
            if (loop.Phase != GamePhase.Settlement || !pendingSettlement.HasValue)
            {
                return;
            }

            var summary = pendingSettlement.Value;
            pendingSettlement = null;

            if (summary.ReachedDestination)
            {
                var arrivedDestination = ledger.CurrentDestination;
                if (!ledger.ApplyArrival())
                {
                    throw new InvalidOperationException("Reached destination could not be applied.");
                }

                ArrivalPresentationRequested(new ArrivalPresentationRequested(
                    arrivedDestination.Id,
                    arrivedDestination.DisplayName));
                CopyProgressionToSave();
                loop.CompleteSettlement(true);
                loop.CompleteArrival(ledger.GameCompleted);
                return;
            }

            loop.CompleteSettlement(false);
        }

        public void Flush()
        {
            ThrowIfDisposed();
            CopyProgressionToSave();
            saveService.MarkDirty();
            saveService.Flush();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            loop.PhaseChanged -= HandlePhaseChanged;
            disposed = true;
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Playing:
                    currentStageId++;
                    ledger.BeginStage(currentStageMaintenanceEfficiencyLevel);
                    saveData.runInProgress = true;
                    saveData.nextAvailableAtUtc = "";
                    CopyProgressionToSave();
                    saveService.MarkDirty();
                    saveService.Flush();
                    StageStarted(new StageStarted(
                        currentStageId,
                        FormatUtc(utcNow()),
                        (float)loop.DurationSeconds));
                    break;

                case GamePhase.StageEnding:
                    StageEnded(new StageEnded(currentStageId, FormatUtc(utcNow())));
                    pendingSettlement = ledger.EndStage();
                    saveData.runInProgress = true;
                    CopyProgressionToSave();
                    saveService.MarkDirty();
                    saveService.Flush();
                    break;

                case GamePhase.Settlement:
                    if (!pendingSettlement.HasValue)
                    {
                        throw new InvalidOperationException("Settlement entered without a completed stage summary.");
                    }

                    SettlementReady(new SettlementReady(currentStageId, pendingSettlement.Value));
                    break;

                case GamePhase.Traveling:
                    currentStageCombatConfig = null;
                    saveData.runInProgress = false;
                    saveData.nextAvailableAtUtc = FormatUtc(
                        utcNow() + TimeSpan.FromSeconds(loop.VoyageRemainingSeconds));
                    CopyProgressionToSave();
                    saveService.MarkDirty();
                    saveService.Flush();
                    break;

                case GamePhase.Ready:
                    saveData.runInProgress = false;
                    saveData.nextAvailableAtUtc = "";
                    CopyProgressionToSave();
                    saveService.MarkDirty();
                    saveService.Flush();
                    break;

                case GamePhase.Completed:
                    saveData.runInProgress = false;
                    saveData.nextAvailableAtUtc = "";
                    CopyProgressionToSave();
                    saveService.MarkDirty();
                    saveService.Flush();
                    break;
            }

            PublishState();
        }

        private GameState CreateState()
        {
            var remainingSeconds = loop.Phase switch
            {
                GamePhase.Traveling => loop.VoyageRemainingSeconds,
                GamePhase.Countdown => loop.CountdownRemainingSeconds,
                GamePhase.Playing => loop.RemainingSeconds,
                _ => 0d
            };

            return new GameState(
                loop.Phase,
                remainingSeconds,
                loop.IsPaused,
                ledger.Funds,
                ledger.CurrentDestination.Id,
                ledger.DestinationProgress,
                ledger.DestinationTarget,
                maintenanceCore.MaintenanceLevels,
                saveData.firstDestroyShown,
                loop.Phase == GamePhase.Ready && !ledger.GameCompleted);
        }

        private IceDestroyedEvent NormalizeStageId(IceDestroyedEvent destruction)
        {
            if (destruction.StageId == currentStageId)
            {
                return destruction;
            }

            return new IceDestroyedEvent(
                currentStageId,
                destruction.IceInstanceId,
                destruction.ChainId,
                destruction.ChainDepth,
                destruction.Tier,
                destruction.SpecialType,
                destruction.DestroyCategory,
                destruction.EffectType,
                destruction.ReferencePosition,
                destruction.StageElapsedSeconds);
        }

        private void CopyProgressionToSave()
        {
            saveData.funds = ledger.Funds;
            saveData.currentDestinationIndex = ledger.CurrentDestinationIndex;
            saveData.destinationProgress = ledger.DestinationProgress;
            saveData.completedDestinationIds = new List<string>(ledger.CompletedDestinationIds);
            saveData.pendingArrivalDestinationId = ledger.PendingArrivalDestinationId ?? "";
            saveData.gameCompleted = ledger.GameCompleted;
        }

        private void PublishState() => StateChanged(CreateState());

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(Int02LoopCoordinator));
            }
        }

        private static string FormatUtc(DateTimeOffset value) =>
            value.ToString("O", CultureInfo.InvariantCulture);
    }
}
