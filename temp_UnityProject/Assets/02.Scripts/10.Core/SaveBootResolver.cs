#nullable enable

using System;
using System.Globalization;
using Icebreaker.Shared.State;

namespace Icebreaker.Core
{
    public static class SaveBootResolver
    {
        public readonly struct BootState
        {
            public BootState(GamePhase phase, double voyageRemainingSeconds)
            {
                Phase = phase;
                VoyageRemainingSeconds = voyageRemainingSeconds;
            }

            public GamePhase Phase { get; }

            public double VoyageRemainingSeconds { get; }
        }

        public static double ComputeRemainingVoyageSeconds(
            DateTimeOffset now,
            string? nextAvailableAtUtc,
            double totalVoyageSeconds)
        {
            if (totalVoyageSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalVoyageSeconds),
                    totalVoyageSeconds,
                    "Value must be positive.");
            }

            if (string.IsNullOrEmpty(nextAvailableAtUtc))
            {
                return 0d;
            }

            if (!DateTimeOffset.TryParse(
                    nextAvailableAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var target))
            {
                return 0d;
            }

            var remaining = (target - now).TotalSeconds;
            if (remaining < 0d)
            {
                return 0d;
            }

            if (remaining > totalVoyageSeconds)
            {
                return totalVoyageSeconds;
            }

            return remaining;
        }

        public static BootState Resolve(
            SaveData? data,
            DateTimeOffset now,
            double totalVoyageSeconds)
        {
            if (totalVoyageSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalVoyageSeconds),
                    totalVoyageSeconds,
                    "Value must be positive.");
            }

            if (data == null)
            {
                return new BootState(GamePhase.Ready, 0d);
            }

            if (data.gameCompleted)
            {
                return new BootState(GamePhase.Completed, 0d);
            }

            if (data.runInProgress)
            {
                return new BootState(GamePhase.Traveling, totalVoyageSeconds);
            }

            var remaining = ComputeRemainingVoyageSeconds(
                now,
                data.nextAvailableAtUtc,
                totalVoyageSeconds);

            return remaining <= 0d
                ? new BootState(GamePhase.Ready, 0d)
                : new BootState(GamePhase.Traveling, remaining);
        }
    }
}
