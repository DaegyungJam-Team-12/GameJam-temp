#nullable enable

using Icebreaker.Shared.Progression;
using UnityEngine;

namespace Icebreaker.Shared.Events
{
    public readonly struct StageStarted
    {
        public StageStarted(long stageId, string startedAtUtc, float durationSeconds)
        {
            StageId = stageId;
            StartedAtUtc = ContractGuards.Required(startedAtUtc, nameof(startedAtUtc));
            DurationSeconds = ContractGuards.NonNegative(durationSeconds, nameof(durationSeconds));
        }

        public long StageId { get; }

        /// <summary>UTC timestamp formatted with DateTimeOffset.UtcNow.ToString("O").</summary>
        public string StartedAtUtc { get; }

        public float DurationSeconds { get; }
    }

    public readonly struct RewardGrantedEvent
    {
        public RewardGrantedEvent(
            long stageId,
            long iceInstanceId,
            long chainId,
            long fundsGranted,
            int destinationProgressGranted,
            Vector2 referencePosition)
        {
            StageId = stageId;
            IceInstanceId = iceInstanceId;
            ChainId = chainId;
            FundsGranted = fundsGranted;
            DestinationProgressGranted = destinationProgressGranted;
            ReferencePosition = referencePosition;
        }

        public long StageId { get; }

        public long IceInstanceId { get; }

        public long ChainId { get; }

        public long FundsGranted { get; }

        public int DestinationProgressGranted { get; }

        /// <summary>Position in the 960 x 540 bottom-left-origin reference space.</summary>
        public Vector2 ReferencePosition { get; }
    }

    public readonly struct StageEnded
    {
        public StageEnded(long stageId, string endedAtUtc)
        {
            StageId = stageId;
            EndedAtUtc = ContractGuards.Required(endedAtUtc, nameof(endedAtUtc));
        }

        public long StageId { get; }

        /// <summary>UTC timestamp formatted with DateTimeOffset.UtcNow.ToString("O").</summary>
        public string EndedAtUtc { get; }
    }

    public readonly struct SettlementReady
    {
        public SettlementReady(long stageId, SettlementSummary summary)
        {
            StageId = stageId;
            Summary = summary;
        }

        public long StageId { get; }

        public SettlementSummary Summary { get; }
    }
}
