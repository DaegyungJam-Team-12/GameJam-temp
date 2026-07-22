#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Progression;

namespace Icebreaker.Core
{
    public static class DestinationCatalog
    {
        public static IReadOnlyList<DestinationDefinition> CreateStandard() =>
            Create(120, 600, 2_400);

        public static IReadOnlyList<DestinationDefinition> CreateDemo() =>
            Create(40, 120, 300);

        private static IReadOnlyList<DestinationDefinition> Create(
            int islandVillageTarget,
            int lighthousePortTarget,
            int northernBaseTarget)
        {
            return Array.AsReadOnly(new[]
            {
                new DestinationDefinition(
                    "island-village",
                    "섬마을",
                    islandVillageTarget,
                    "식료품·우편",
                    0),
                new DestinationDefinition(
                    "lighthouse-port",
                    "등대항",
                    lighthousePortTarget,
                    "발전기 연료·의약품",
                    1),
                new DestinationDefinition(
                    "northern-base",
                    "북쪽 기지",
                    northernBaseTarget,
                    "기계 부품·우편",
                    2)
            });
        }
    }
}
