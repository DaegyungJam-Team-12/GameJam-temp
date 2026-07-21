#nullable enable

using System;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Progression;
using Icebreaker.Shared.State;
using UnityEngine;

namespace Icebreaker.UI.Hud
{
    public sealed class Ui04RewardSettlementSampleSource : MonoBehaviour,
        ICombatEventSource,
        IProgressionEventSource,
        IGameStateSource
    {
        private const long InitialFunds = 12_400L;
        private const int InitialProgress = 37;
        private const int DestinationTarget = 120;

        private GameState? currentState;
        private long nextIceId = 1L;
        private long nextChainId = 1L;
        private long nextStageId = 1L;

        [SerializeField] private RewardSettlementPresenter? previewPresenter;

        public event Action<GameState> StateChanged = delegate { };
        public event Action<DamageAppliedEvent> DamageApplied = delegate { };
        public event Action<SupportChargeChangedEvent> SupportChargeChanged = delegate { };
        public event Action<IceDestroyedEvent> IceDestroyed = delegate { };
        public event Action<StageStarted> StageStarted = delegate { };
        public event Action<RewardGrantedEvent> RewardGranted = delegate { };
        public event Action<StageEnded> StageEnded = delegate { };
        public event Action<SettlementReady> SettlementReady = delegate { };

        public GameState CurrentState
        {
            get
            {
                EnsureInitialized();
                return currentState ?? throw new InvalidOperationException("UI-04 sample state is missing.");
            }
        }

        private void Awake() => EnsureInitialized();

        public void EnsureInitialized()
        {
            if (currentState == null)
            {
                ResetSample(raiseEvent: false);
            }
        }

        [ContextMenu("UI-04/Reset Sample")]
        public void ResetSample() => ResetSample(raiseEvent: true);

        [ContextMenu("UI-04/Show Edge Reward")]
        public void ShowEdgeReward()
        {
            var presenter = ResolvePreviewPresenter();
            if (presenter != null)
            {
                var iceId = nextIceId++;
                var chainId = nextChainId++;
                presenter.PreviewReward(new RewardGrantedEvent(
                    stageId: nextStageId,
                    iceInstanceId: iceId,
                    chainId: chainId,
                    fundsGranted: 80L,
                    destinationProgressGranted: 1,
                    referencePosition: new Vector2(955f, 12f)));
                ApplyPreviewReward(80L, 1);
                return;
            }

            PublishDestroyAndReward(
                chainId: nextChainId++,
                chainDepth: 0,
                funds: 80L,
                position: new Vector2(955f, 12f));
        }

        [ContextMenu("UI-04/Show Critical")]
        public void ShowCritical()
        {
            var payload = new DamageAppliedEvent(
                stageId: nextStageId,
                iceInstanceId: nextIceId,
                chainId: nextChainId++,
                chainDepth: 0,
                effectType: EffectType.Click,
                damage: 30f,
                remainingHp: 8f,
                wasCritical: true,
                referencePosition: new Vector2(480f, 270f),
                stageElapsedSeconds: 12d);

            var presenter = ResolvePreviewPresenter();
            if (presenter != null)
            {
                presenter.PreviewDamage(payload);
                return;
            }

            DamageApplied(payload);
        }

        [ContextMenu("UI-04/Show Three-Reward Chain")]
        public void ShowThreeRewardChain()
        {
            var chainId = nextChainId++;
            var presenter = ResolvePreviewPresenter();
            if (presenter != null)
            {
                nextIceId += 3L;
                presenter.PreviewChainReward(240L, new Vector2(480f, 260f), 3);
                ApplyPreviewReward(240L, 3);
                return;
            }

            PublishDestroyAndReward(chainId, 0, 80L, new Vector2(400f, 250f));
            PublishDestroyAndReward(chainId, 1, 80L, new Vector2(480f, 280f));
            PublishDestroyAndReward(chainId, 2, 80L, new Vector2(560f, 250f));
        }

        [ContextMenu("UI-04/Show Settlement")]
        public void ShowSettlement()
        {
            var stageId = nextStageId++;
            ReplaceState(GamePhase.Settlement, CurrentState.Funds, CurrentState.DestinationProgress, true);
            var payload = new SettlementReady(
                stageId,
                new SettlementSummary(
                    earnedFunds: 1_240L,
                    destroyedCount: 37,
                    destinationProgressGain: 37,
                    reachedDestination: false,
                    destinationId: null));
            var presenter = ResolvePreviewPresenter();
            if (ShouldUseDirectPreview(presenter))
            {
                presenter!.PreviewSettlement(payload);
            }

            SettlementReady(payload);
        }

        [ContextMenu("UI-04/Show Destination Settlement")]
        public void ShowDestinationSettlement()
        {
            var stageId = nextStageId++;
            ReplaceState(GamePhase.Settlement, CurrentState.Funds, DestinationTarget, true);
            var payload = new SettlementReady(
                stageId,
                new SettlementSummary(
                    earnedFunds: 2_800L,
                    destroyedCount: 42,
                    destinationProgressGain: 18,
                    reachedDestination: true,
                    destinationId: "island-village"));
            var presenter = ResolvePreviewPresenter();
            if (ShouldUseDirectPreview(presenter))
            {
                presenter!.PreviewSettlement(payload);
            }

            SettlementReady(payload);
        }

        [ContextMenu("UI-04/Start New Stage")]
        public void StartNewStage()
        {
            var stageId = nextStageId++;
            ReplaceState(GamePhase.Playing, CurrentState.Funds, CurrentState.DestinationProgress, true);
            StageStarted(new StageStarted(stageId, DateTimeOffset.UtcNow.ToString("O"), 60f));
        }

        private void PublishDestroyAndReward(long chainId, int chainDepth, long funds, Vector2 position)
        {
            var iceId = nextIceId++;
            var destruction = new IceDestroyedEvent(
                stageId: nextStageId,
                iceInstanceId: iceId,
                chainId: chainId,
                chainDepth: chainDepth,
                tier: IceTier.T1,
                specialType: SpecialIceType.None,
                destroyCategory: chainDepth == 0 ? DestroyCategory.Direct : DestroyCategory.Chain,
                effectType: chainDepth == 0 ? EffectType.Click : EffectType.Overkill,
                referencePosition: position,
                stageElapsedSeconds: 12d);
            var presenter = ResolvePreviewPresenter();
            if (ShouldUseDirectPreview(presenter))
            {
                presenter!.PreviewDestroyed(destruction);
            }

            IceDestroyed(destruction);

            var reward = new RewardGrantedEvent(
                stageId: nextStageId,
                iceInstanceId: iceId,
                chainId: chainId,
                fundsGranted: funds,
                destinationProgressGranted: 1,
                referencePosition: position);
            if (ShouldUseDirectPreview(presenter))
            {
                presenter!.PreviewReward(reward);
            }

            RewardGranted(reward);

            var previous = CurrentState;
            ReplaceState(
                GamePhase.Playing,
                checked(previous.Funds + funds),
                Math.Min(previous.DestinationProgress + 1, previous.DestinationTarget),
                true);
        }

        private RewardSettlementPresenter? ResolvePreviewPresenter()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(this) ||
                previewPresenter == null ||
                UnityEditor.EditorUtility.IsPersistent(previewPresenter))
            {
                var presenters = FindObjectsByType<RewardSettlementPresenter>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                foreach (var candidate in presenters)
                {
                    if (candidate != null && !UnityEditor.EditorUtility.IsPersistent(candidate))
                    {
                        return candidate;
                    }
                }
            }
#endif

            return previewPresenter;
        }

        private bool ShouldUseDirectPreview(RewardSettlementPresenter? presenter)
        {
            if (presenter == null)
            {
                return false;
            }

#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(this))
            {
                return true;
            }
#endif

            return !presenter.IsEventSubscriptionActive;
        }

        private void ApplyPreviewReward(long funds, int destroyedCount)
        {
            var previous = CurrentState;
            ReplaceState(
                GamePhase.Playing,
                checked(previous.Funds + funds),
                Math.Min(previous.DestinationProgress + destroyedCount, previous.DestinationTarget),
                true);
        }

        private void ResetSample(bool raiseEvent)
        {
            nextIceId = 1L;
            nextChainId = 1L;
            nextStageId = 1L;
            currentState = new GameState(
                phase: GamePhase.Playing,
                remainingSeconds: 42d,
                isPaused: false,
                funds: InitialFunds,
                currentDestinationId: "island-village",
                destinationProgress: InitialProgress,
                destinationTarget: DestinationTarget,
                maintenanceLevels: Array.Empty<MaintenanceLevel>(),
                firstDestroyShown: false,
                canStartStage: false);

            if (raiseEvent)
            {
                StateChanged(currentState);
            }
        }

        private void ReplaceState(GamePhase phase, long funds, int progress, bool firstDestroyShown)
        {
            var previous = CurrentState;
            currentState = new GameState(
                phase: phase,
                remainingSeconds: phase == GamePhase.Playing ? 42d : 0d,
                isPaused: false,
                funds: funds,
                currentDestinationId: previous.CurrentDestinationId,
                destinationProgress: progress,
                destinationTarget: previous.DestinationTarget,
                maintenanceLevels: previous.MaintenanceLevels,
                firstDestroyShown: firstDestroyShown,
                canStartStage: false);
            StateChanged(currentState);
        }
    }
}
