#nullable enable

using System;
using Icebreaker.Shared;

namespace Icebreaker.Shared.Progression
{
    public readonly struct SettlementSummary
    {
        public SettlementSummary(
            long earnedFunds,
            int destroyedCount,
            int destinationProgressGain,
            bool reachedDestination,
            string? destinationId)
        {
            if (reachedDestination && string.IsNullOrWhiteSpace(destinationId))
            {
                throw new ArgumentException(
                    "A reached destination must include its destination ID.",
                    nameof(destinationId));
            }

            EarnedFunds = ContractGuards.NonNegative(earnedFunds, nameof(earnedFunds));
            DestroyedCount = ContractGuards.NonNegative(destroyedCount, nameof(destroyedCount));
            DestinationProgressGain = ContractGuards.NonNegative(
                destinationProgressGain,
                nameof(destinationProgressGain));
            ReachedDestination = reachedDestination;
            DestinationId = destinationId;
        }

        public long EarnedFunds { get; }

        public int DestroyedCount { get; }

        public int DestinationProgressGain { get; }

        public bool ReachedDestination { get; }

        public string? DestinationId { get; }
    }
}
