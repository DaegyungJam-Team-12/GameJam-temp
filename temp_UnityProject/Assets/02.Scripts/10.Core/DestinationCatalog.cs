#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Progression;

namespace Icebreaker.Core
{
    public static class DestinationCatalog
    {
        public static IReadOnlyList<DestinationDefinition> CreateStandard() =>
            Create(new[] { 120, 600, 2_400 });

        public static IReadOnlyList<DestinationDefinition> CreateDemo() =>
            Create(new[] { 40, 120, 300 });

        public static IReadOnlyList<DestinationDefinition> Create(IReadOnlyList<int> targets)
        {
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            if (targets.Count != 3)
            {
                throw new ArgumentException("Exactly three destination targets are required.", nameof(targets));
            }

            for (var index = 0; index < targets.Count; index++)
            {
                if (targets[index] <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(targets),
                        "Destination targets must be positive.");
                }
            }

            return Array.AsReadOnly(new[]
            {
                new DestinationDefinition(
                    "island-village",
                    "섬마을",
                    targets[0],
                    "식료품·우편",
                    0),
                new DestinationDefinition(
                    "lighthouse-port",
                    "등대항",
                    targets[1],
                    "발전기 연료·의약품",
                    1),
                new DestinationDefinition(
                    "northern-base",
                    "북쪽 기지",
                    targets[2],
                    "기계 부품·우편",
                    2)
            });
        }
    }
}
