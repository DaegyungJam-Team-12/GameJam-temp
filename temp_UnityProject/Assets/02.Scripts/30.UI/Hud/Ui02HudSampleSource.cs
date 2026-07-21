#nullable enable

using System;
using Icebreaker.Shared.State;
using UnityEngine;

namespace Icebreaker.UI.Hud
{
    /// <summary>
    /// Self-contained preview data for opening the HUD prefabs in Prefab Mode.
    /// Integration code can replace it through the presenter's Bind method.
    /// </summary>
    public sealed class Ui02HudSampleSource : MonoBehaviour, IGameStateSource
    {
        [SerializeField] private GamePhase initialPhase = GamePhase.Traveling;
        [SerializeField, Min(0f)] private double initialRemainingSeconds = 24d;
        [SerializeField, Min(0f)] private long initialFunds = 12_400;
        [SerializeField] private string destinationId = "island-village";
        [SerializeField, Min(0)] private int destinationProgress = 37;
        [SerializeField, Min(0)] private int destinationTarget = 120;
        [SerializeField] private bool canStartStage;

        private GameState? currentState;

        public event Action<GameState> StateChanged = delegate { };

        public GameState CurrentState
        {
            get
            {
                EnsureInitialized();
                return currentState ?? throw new InvalidOperationException("The HUD preview state could not be initialized.");
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (currentState == null)
            {
                ResetPreview(raiseEvent: false);
            }
        }

        [ContextMenu("UI-02/Reset Preview")]
        public void ResetPreview()
        {
            ResetPreview(raiseEvent: true);
        }

        [ContextMenu("UI-02/Advance One Second")]
        public void AdvanceOneSecond()
        {
            var state = CurrentState;
            var remaining = Math.Max(0d, state.RemainingSeconds - 1d);

            if (state.Phase == GamePhase.Countdown)
            {
                var nextPhase = remaining <= 0d ? GamePhase.Playing : GamePhase.Countdown;
                var nextRemaining = nextPhase == GamePhase.Playing ? 60d : remaining;
                ReplaceState(nextPhase, nextRemaining, state.Funds, ready: false);
                return;
            }

            ReplaceState(state.Phase, remaining, state.Funds, remaining <= 0d || state.CanStartStage);
        }

        [ContextMenu("UI-02/Toggle Start Ready")]
        public void ToggleStartReady()
        {
            var state = CurrentState;
            ReplaceState(state.Phase, state.RemainingSeconds, state.Funds, !state.CanStartStage);
        }

        [ContextMenu("UI-02/Grant 80 Sample Funds")]
        public void GrantSampleFunds()
        {
            var state = CurrentState;
            ReplaceState(state.Phase, state.RemainingSeconds, checked(state.Funds + 80L), state.CanStartStage);
        }

        [ContextMenu("UI-03/Show Countdown 3")]
        public void ShowCountdownThree() => ShowCountdown(3d);

        [ContextMenu("UI-03/Show Countdown 2")]
        public void ShowCountdownTwo() => ShowCountdown(2d);

        [ContextMenu("UI-03/Show Countdown 1")]
        public void ShowCountdownOne() => ShowCountdown(1d);

        [ContextMenu("UI-03/Show Combat 60 Seconds")]
        public void ShowCombat()
        {
            var state = CurrentState;
            ReplaceState(GamePhase.Playing, 60d, state.Funds, ready: false);
        }

        private void ResetPreview(bool raiseEvent)
        {
            currentState = new GameState(
                phase: initialPhase,
                remainingSeconds: Math.Max(0d, initialRemainingSeconds),
                isPaused: false,
                funds: Math.Max(0L, initialFunds),
                currentDestinationId: string.IsNullOrWhiteSpace(destinationId) ? "island-village" : destinationId,
                destinationProgress: Math.Min(destinationProgress, destinationTarget),
                destinationTarget: destinationTarget,
                maintenanceLevels: Array.Empty<MaintenanceLevel>(),
                firstDestroyShown: true,
                canStartStage: canStartStage);

            if (raiseEvent)
            {
                StateChanged(currentState);
            }
        }

        private void ShowCountdown(double remainingSeconds)
        {
            var state = CurrentState;
            ReplaceState(GamePhase.Countdown, remainingSeconds, state.Funds, ready: false);
        }

        private void ReplaceState(GamePhase phase, double remainingSeconds, long funds, bool ready)
        {
            var previous = CurrentState;
            currentState = new GameState(
                phase: phase,
                remainingSeconds: remainingSeconds,
                isPaused: previous.IsPaused,
                funds: funds,
                currentDestinationId: previous.CurrentDestinationId,
                destinationProgress: previous.DestinationProgress,
                destinationTarget: previous.DestinationTarget,
                maintenanceLevels: previous.MaintenanceLevels,
                firstDestroyShown: previous.FirstDestroyShown,
                canStartStage: ready);

            StateChanged(currentState);
        }
    }
}
