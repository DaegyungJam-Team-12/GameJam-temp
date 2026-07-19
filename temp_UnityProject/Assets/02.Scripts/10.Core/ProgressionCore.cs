#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Progression;
using Icebreaker.Shared.State;

namespace Icebreaker.Core
{
    public sealed class ProgressionCore : IProgressionEventSource
    {
        // CORE-02 replaces these skeleton values with real reward calculation.
        private const long SkeletonFundsPerDestroy = 10;
        private const int SkeletonProgressPerDestroy = 1;

        private readonly DestinationDefinition destination;
        private readonly HashSet<(long StageId, long IceInstanceId)> processedDestructions = new();

        public ProgressionCore(DestinationDefinition destination, long initialFunds = 0)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (initialFunds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialFunds), initialFunds, "Value cannot be negative.");
            }

            this.destination = destination;
            Funds = initialFunds;
        }

        // Raised from CORE-01/CORE-02 onward.
#pragma warning disable 0067
        public event Action<StageStarted> StageStarted = delegate { };

        public event Action<StageEnded> StageEnded = delegate { };

        public event Action<SettlementReady> SettlementReady = delegate { };
#pragma warning restore 0067

        public event Action<RewardGrantedEvent> RewardGranted = delegate { };

        public long Funds { get; private set; }

        public int DestinationProgress { get; private set; }

        public bool HandleIceDestroyed(IceDestroyedEvent e)
        {
            if (!processedDestructions.Add((e.StageId, e.IceInstanceId)))
            {
                return false;
            }

            Funds += SkeletonFundsPerDestroy;
            DestinationProgress = Math.Min(
                DestinationProgress + SkeletonProgressPerDestroy,
                destination.TargetProgress);

            RewardGranted(new RewardGrantedEvent(
                e.StageId,
                e.IceInstanceId,
                e.ChainId,
                SkeletonFundsPerDestroy,
                SkeletonProgressPerDestroy,
                e.ReferencePosition));

            return true;
        }

        public GameState CreateSnapshot()
        {
            return new GameState(
                GamePhase.Ready,
                0d,
                false,
                Funds,
                destination.Id,
                DestinationProgress,
                destination.TargetProgress,
                Array.Empty<MaintenanceLevel>(),
                false,
                true);
        }
    }
}
