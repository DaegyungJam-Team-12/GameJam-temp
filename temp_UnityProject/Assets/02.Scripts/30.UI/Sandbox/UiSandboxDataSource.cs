#nullable enable

using System;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.State;
using Icebreaker.UI.Hud;
using UnityEngine;

namespace Icebreaker.UI.Sandbox
{
    /// <summary>
    /// UI-only sample source. It mirrors progression outputs without depending on Core or Gameplay.
    /// </summary>
    public sealed class UiSandboxDataSource : MonoBehaviour, IProgressionEventSource, IGameStateSource
    {
        private const long InitialFunds = 12_400;
        private const double InitialRemainingSeconds = 42d;
        private const int InitialDestinationProgress = 37;
        private const int DestinationTarget = 120;
        private const long SampleRewardFunds = 80;

        private GameState? currentState;
        private long nextIceInstanceId = 1;
        private long nextChainId = 1;

        public event Action<GameState> StateChanged = delegate { };

#pragma warning disable 0067
        public event Action<StageStarted> StageStarted = delegate { };

        public event Action<StageEnded> StageEnded = delegate { };

        public event Action<SettlementReady> SettlementReady = delegate { };
#pragma warning restore 0067

        public event Action<RewardGrantedEvent> RewardGranted = delegate { };

        public GameState CurrentState => currentState ?? throw new InvalidOperationException(
            "The UI sandbox state is not initialized yet.");

        private void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (currentState == null)
            {
                ResetState(raiseEvent: false);
            }
        }

        public void ResetSample()
        {
            ResetState(raiseEvent: true);
        }

        public void DecreaseTime()
        {
            var state = CurrentState;
            ReplaceState(
                state,
                remainingSeconds: Math.Max(0d, state.RemainingSeconds - 1d),
                funds: state.Funds,
                destinationProgress: state.DestinationProgress);
        }

        public void GrantSampleReward()
        {
            var state = CurrentState;
            var progressGranted = state.DestinationProgress < state.DestinationTarget ? 1 : 0;
            var newProgress = Math.Min(
                state.DestinationProgress + progressGranted,
                state.DestinationTarget);

            ReplaceState(
                state,
                remainingSeconds: state.RemainingSeconds,
                funds: checked(state.Funds + SampleRewardFunds),
                destinationProgress: newProgress);

            RewardGranted(new RewardGrantedEvent(
                stageId: 1,
                iceInstanceId: nextIceInstanceId++,
                chainId: nextChainId++,
                fundsGranted: SampleRewardFunds,
                destinationProgressGranted: progressGranted,
                referencePosition: new Vector2(480f, 270f)));
        }

        private void ResetState(bool raiseEvent)
        {
            nextIceInstanceId = 1;
            nextChainId = 1;
            currentState = new GameState(
                phase: GamePhase.Playing,
                remainingSeconds: InitialRemainingSeconds,
                isPaused: false,
                funds: InitialFunds,
                currentDestinationId: "island-village",
                destinationProgress: InitialDestinationProgress,
                destinationTarget: DestinationTarget,
                maintenanceLevels: Array.Empty<MaintenanceLevel>(),
                firstDestroyShown: true,
                canStartStage: false);

            if (raiseEvent)
            {
                StateChanged(currentState);
            }
        }

        private void ReplaceState(
            GameState previous,
            double remainingSeconds,
            long funds,
            int destinationProgress)
        {
            currentState = new GameState(
                phase: previous.Phase,
                remainingSeconds: remainingSeconds,
                isPaused: previous.IsPaused,
                funds: funds,
                currentDestinationId: previous.CurrentDestinationId,
                destinationProgress: destinationProgress,
                destinationTarget: previous.DestinationTarget,
                maintenanceLevels: previous.MaintenanceLevels,
                firstDestroyShown: previous.FirstDestroyShown,
                canStartStage: previous.CanStartStage);

            StateChanged(currentState);
        }
    }
}
