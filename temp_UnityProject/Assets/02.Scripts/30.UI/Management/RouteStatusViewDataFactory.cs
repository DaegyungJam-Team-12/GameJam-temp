#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Icebreaker.Core;
using Icebreaker.Shared.Progression;

namespace Icebreaker.UI.Management
{
    public static class RouteStatusViewDataFactory
    {
        public static RouteStatusViewData Create(
            ProgressionLedger ledger,
            IReadOnlyList<DestinationDefinition> destinations)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            if (destinations == null)
            {
                throw new ArgumentNullException(nameof(destinations));
            }

            var ordered = new List<DestinationDefinition>(destinations.Count);
            for (var index = 0; index < destinations.Count; index++)
            {
                ordered.Add(destinations[index]);
            }

            ordered.Sort((left, right) => left.DisplayOrder.CompareTo(right.DisplayOrder));
            var completedNames = new List<string>();
            var upcomingNames = new List<string>();
            foreach (var destination in ordered)
            {
                if (ledger.CompletedDestinationIds.Contains(destination.Id))
                {
                    completedNames.Add(destination.DisplayName);
                }
                else if (destination.DisplayOrder > ledger.CurrentDestination.DisplayOrder)
                {
                    upcomingNames.Add(destination.DisplayName);
                }
            }

            var current = ledger.CurrentDestination;
            return new RouteStatusViewData(
                current.Id,
                current.DisplayName,
                ledger.DestinationProgress,
                ledger.DestinationTarget,
                current.CargoName,
                completedNames.Count > 0 ? string.Join(" → ", completedNames) : "없음",
                upcomingNames.Count > 0 ? string.Join(" → ", upcomingNames) : "없음",
                ledger.GameCompleted);
        }
    }
}
