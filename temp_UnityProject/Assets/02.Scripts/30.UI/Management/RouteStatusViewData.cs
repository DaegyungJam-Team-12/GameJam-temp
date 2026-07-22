#nullable enable

using System;

namespace Icebreaker.UI.Management
{
    /// <summary>Precalculated route information for the management view.</summary>
    public sealed class RouteStatusViewData
    {
        public RouteStatusViewData(
            string currentDestinationId,
            string currentDestinationName,
            int progress,
            int target,
            string cargoText,
            string completedDestinationsText,
            string upcomingDestinationsText,
            bool gameCompleted)
        {
            CurrentDestinationId = Required(currentDestinationId, nameof(currentDestinationId));
            CurrentDestinationName = Required(currentDestinationName, nameof(currentDestinationName));
            Progress = NonNegative(progress, nameof(progress));
            Target = NonNegative(target, nameof(target));
            if (Progress > Target)
            {
                throw new ArgumentException("Progress cannot exceed its target.", nameof(progress));
            }

            CargoText = Required(cargoText, nameof(cargoText));
            CompletedDestinationsText = completedDestinationsText ?? string.Empty;
            UpcomingDestinationsText = upcomingDestinationsText ?? string.Empty;
            GameCompleted = gameCompleted;
        }

        public string CurrentDestinationId { get; }

        public string CurrentDestinationName { get; }

        public int Progress { get; }

        public int Target { get; }

        public string CargoText { get; }

        public string CompletedDestinationsText { get; }

        public string UpcomingDestinationsText { get; }

        public bool GameCompleted { get; }

        private static string Required(string? value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be null, empty, or whitespace.", parameterName);
            }

            return value;
        }

        private static int NonNegative(int value, string parameterName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Value cannot be negative.");
            }

            return value;
        }
    }
}
