#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Progression;

namespace Icebreaker.Core
{
    public sealed class ProgressionLedger
    {
        private readonly DestinationDefinition[] destinations;
        private readonly RewardTable rewardTable;
        private readonly int maintenanceEfficiencyLevel;
        private readonly HashSet<string> completed = new();
        private HashSet<(long StageId, long IceInstanceId)> approvedDestructions = new();
        private int currentDestinationIndex;
        private long stageFunds;
        private int stageDestroyed;
        private int stageProgressGain;
        private bool stageReachedDestination;

        public ProgressionLedger(
            IReadOnlyList<DestinationDefinition> destinations,
            RewardTable rewardTable,
            long initialFunds = 0,
            int maintenanceEfficiencyLevel = 0)
        {
            if (destinations == null)
            {
                throw new ArgumentNullException(nameof(destinations));
            }

            if (destinations.Count == 0)
            {
                throw new ArgumentException("At least one destination is required.", nameof(destinations));
            }

            if (rewardTable == null)
            {
                throw new ArgumentNullException(nameof(rewardTable));
            }

            if (initialFunds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialFunds), initialFunds, "Value cannot be negative.");
            }

            if (maintenanceEfficiencyLevel < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maintenanceEfficiencyLevel),
                    maintenanceEfficiencyLevel,
                    "Value cannot be negative.");
            }

            this.destinations = new DestinationDefinition[destinations.Count];
            for (var i = 0; i < destinations.Count; i++)
            {
                this.destinations[i] = destinations[i];
            }

            Array.Sort(
                this.destinations,
                (left, right) => left.DisplayOrder.CompareTo(right.DisplayOrder));

            this.rewardTable = rewardTable;
            this.maintenanceEfficiencyLevel = maintenanceEfficiencyLevel;
            Funds = initialFunds;
        }

        public long Funds { get; private set; }

        public DestinationDefinition CurrentDestination => destinations[currentDestinationIndex];

        public int DestinationProgress { get; private set; }

        public int DestinationTarget => CurrentDestination.TargetProgress;

        public string? PendingArrivalDestinationId { get; private set; }

        public bool GameCompleted { get; private set; }

        public IReadOnlyCollection<string> CompletedDestinationIds => completed;

        public void BeginStage()
        {
            stageFunds = 0;
            stageDestroyed = 0;
            stageProgressGain = 0;
            stageReachedDestination = false;
            approvedDestructions = new HashSet<(long StageId, long IceInstanceId)>();
        }

        public bool TryApproveDestruction(IceDestroyedEvent e, out RewardGrantedEvent reward)
        {
            reward = default;
            if (!approvedDestructions.Add((e.StageId, e.IceInstanceId)))
            {
                return false;
            }

            var funds = rewardTable.ComputeFunds(e.Tier, e.SpecialType, maintenanceEfficiencyLevel);
            Funds += funds;
            stageFunds += funds;
            stageDestroyed++;

            var progressGranted = 0;
            if (!GameCompleted
                && PendingArrivalDestinationId == null
                && DestinationProgress < DestinationTarget)
            {
                DestinationProgress++;
                progressGranted = 1;
                stageProgressGain++;

                if (DestinationProgress == DestinationTarget)
                {
                    PendingArrivalDestinationId = CurrentDestination.Id;
                    stageReachedDestination = true;
                }
            }

            reward = new RewardGrantedEvent(
                e.StageId,
                e.IceInstanceId,
                e.ChainId,
                funds,
                progressGranted,
                e.ReferencePosition);
            return true;
        }

        public SettlementSummary EndStage()
        {
            return new SettlementSummary(
                stageFunds,
                stageDestroyed,
                stageProgressGain,
                stageReachedDestination,
                stageReachedDestination ? PendingArrivalDestinationId : null);
        }

        public bool ApplyArrival()
        {
            if (PendingArrivalDestinationId == null)
            {
                return false;
            }

            completed.Add(PendingArrivalDestinationId);
            if (currentDestinationIndex == destinations.Length - 1)
            {
                GameCompleted = true;
            }
            else
            {
                currentDestinationIndex++;
                DestinationProgress = 0;
            }

            PendingArrivalDestinationId = null;
            return true;
        }
    }
}
