#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared;

namespace Icebreaker.Shared.State
{
    /// <summary>
    /// Read-only presentation snapshot. Combat time boundaries must use IStageClock.
    /// </summary>
    public sealed class GameState
    {
        public GameState(
            GamePhase phase,
            double remainingSeconds,
            bool isPaused,
            long funds,
            string currentDestinationId,
            int destinationProgress,
            int destinationTarget,
            IReadOnlyList<MaintenanceLevel> maintenanceLevels,
            bool firstDestroyShown,
            bool canStartStage)
        {
            var validatedProgress = ContractGuards.NonNegative(destinationProgress, nameof(destinationProgress));
            var validatedTarget = ContractGuards.NonNegative(destinationTarget, nameof(destinationTarget));
            if (validatedProgress > validatedTarget)
            {
                throw new ArgumentException("Destination progress cannot exceed its target.", nameof(destinationProgress));
            }

            Phase = phase;
            RemainingSeconds = ContractGuards.NonNegative(remainingSeconds, nameof(remainingSeconds));
            IsPaused = isPaused;
            Funds = ContractGuards.NonNegative(funds, nameof(funds));
            CurrentDestinationId = ContractGuards.Required(currentDestinationId, nameof(currentDestinationId));
            DestinationProgress = validatedProgress;
            DestinationTarget = validatedTarget;
            MaintenanceLevels = ContractGuards.Copy(maintenanceLevels, nameof(maintenanceLevels));
            FirstDestroyShown = firstDestroyShown;
            CanStartStage = canStartStage;
        }

        public GamePhase Phase { get; }

        public double RemainingSeconds { get; }

        public bool IsPaused { get; }

        public long Funds { get; }

        public string CurrentDestinationId { get; }

        public int DestinationProgress { get; }

        public int DestinationTarget { get; }

        public IReadOnlyList<MaintenanceLevel> MaintenanceLevels { get; }

        public bool FirstDestroyShown { get; }

        public bool CanStartStage { get; }
    }
}
