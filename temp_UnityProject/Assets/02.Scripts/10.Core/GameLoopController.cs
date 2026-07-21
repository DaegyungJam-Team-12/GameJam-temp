#nullable enable

using System;
using Icebreaker.Shared.State;

namespace Icebreaker.Core
{
    public sealed class GameLoopController : IStageClock
    {
        private readonly double stageDurationSeconds;
        private readonly double countdownSeconds;
        private readonly double voyageSeconds;
        private readonly double stageEndingSeconds;

        private double voyageRemaining;
        private double stageElapsed;
        private double countdownRemaining;
        private double stageEndingRemaining;
        private bool settingsPaused;

        public GameLoopController(
            double stageDurationSeconds = 60d,
            double countdownSeconds = 3d,
            double voyageSeconds = 30d,
            double stageEndingSeconds = 1.2d)
        {
            if (!(stageDurationSeconds > 0d))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stageDurationSeconds),
                    stageDurationSeconds,
                    "Value must be greater than zero.");
            }

            if (!(countdownSeconds > 0d))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(countdownSeconds),
                    countdownSeconds,
                    "Value must be greater than zero.");
            }

            if (!(voyageSeconds > 0d))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(voyageSeconds),
                    voyageSeconds,
                    "Value must be greater than zero.");
            }

            if (!(stageEndingSeconds > 0d))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stageEndingSeconds),
                    stageEndingSeconds,
                    "Value must be greater than zero.");
            }

            this.stageDurationSeconds = stageDurationSeconds;
            this.countdownSeconds = countdownSeconds;
            this.voyageSeconds = voyageSeconds;
            this.stageEndingSeconds = stageEndingSeconds;

            Phase = GamePhase.Traveling;
            voyageRemaining = voyageSeconds;
            stageElapsed = 0d;
            countdownRemaining = countdownSeconds;
            stageEndingRemaining = stageEndingSeconds;
            settingsPaused = false;
        }

        public event Action<GamePhase> PhaseChanged = delegate { };

        public GamePhase Phase { get; private set; }

        public double DurationSeconds => stageDurationSeconds;

        public double StageElapsedSeconds => stageElapsed;

        public double RemainingSeconds => Math.Max(0d, stageDurationSeconds - stageElapsed);

        public bool IsPaused => settingsPaused;

        public double VoyageRemainingSeconds => Math.Max(0d, voyageRemaining);

        public double CountdownRemainingSeconds => Math.Max(0d, countdownRemaining);

        public void SetSettingsPaused(bool paused) => settingsPaused = paused;

        public void Tick(double deltaSeconds)
        {
            if (!(deltaSeconds >= 0d))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaSeconds),
                    deltaSeconds,
                    "Value cannot be negative.");
            }

            if (settingsPaused)
            {
                return;
            }

            switch (Phase)
            {
                case GamePhase.Traveling:
                    voyageRemaining -= deltaSeconds;
                    if (voyageRemaining <= 0d)
                    {
                        voyageRemaining = 0d;
                        SetPhase(GamePhase.Ready);
                    }

                    break;
                case GamePhase.Countdown:
                    countdownRemaining -= deltaSeconds;
                    if (countdownRemaining <= 0d)
                    {
                        countdownRemaining = 0d;
                        stageElapsed = 0d;
                        SetPhase(GamePhase.Playing);
                    }

                    break;
                case GamePhase.Playing:
                    stageElapsed += deltaSeconds;
                    if (stageElapsed >= stageDurationSeconds)
                    {
                        stageElapsed = stageDurationSeconds;
                        stageEndingRemaining = stageEndingSeconds;
                        SetPhase(GamePhase.StageEnding);
                    }

                    break;
                case GamePhase.StageEnding:
                    stageEndingRemaining -= deltaSeconds;
                    if (stageEndingRemaining <= 0d)
                    {
                        stageEndingRemaining = 0d;
                        EnterSettlement();
                    }

                    break;
            }
        }

        public void RequestStageStart()
        {
            if (Phase != GamePhase.Ready)
            {
                throw new InvalidOperationException(
                    $"Stage start can only be requested from {GamePhase.Ready}; current phase is {Phase}.");
            }

            countdownRemaining = countdownSeconds;
            SetPhase(GamePhase.Countdown);
        }

        public void EnterSettlement()
        {
            if (Phase != GamePhase.StageEnding)
            {
                throw new InvalidOperationException(
                    $"Settlement can only be entered from {GamePhase.StageEnding}; current phase is {Phase}.");
            }

            stageEndingRemaining = 0d;
            SetPhase(GamePhase.Settlement);
        }

        public void CompleteSettlement(bool destinationReached)
        {
            if (Phase != GamePhase.Settlement)
            {
                throw new InvalidOperationException(
                    $"Settlement can only be completed from {GamePhase.Settlement}; current phase is {Phase}.");
            }

            if (destinationReached)
            {
                SetPhase(GamePhase.Arrival);
                return;
            }

            voyageRemaining = voyageSeconds;
            stageElapsed = 0d;
            SetPhase(GamePhase.Traveling);
        }

        public void CompleteArrival(bool gameCompleted)
        {
            if (Phase != GamePhase.Arrival)
            {
                throw new InvalidOperationException(
                    $"Arrival can only be completed from {GamePhase.Arrival}; current phase is {Phase}.");
            }

            if (gameCompleted)
            {
                SetPhase(GamePhase.Completed);
                return;
            }

            voyageRemaining = voyageSeconds;
            stageElapsed = 0d;
            SetPhase(GamePhase.Traveling);
        }

        private void SetPhase(GamePhase next)
        {
            if (Phase == next)
            {
                return;
            }

            Phase = next;
            PhaseChanged(next);
        }
    }
}
