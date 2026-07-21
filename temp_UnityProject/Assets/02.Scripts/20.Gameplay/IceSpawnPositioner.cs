#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Icebreaker.Gameplay
{
    /// <summary>
    /// Picks random spawn positions in a reference-pixel rectangle while keeping a minimum
    /// distance from existing ice. Falls back to a relaxed distance after repeated failures.
    /// </summary>
    public sealed class IceSpawnPositioner
    {
        private const int MaxAttemptsBeforeRelax = 30;
        private const int MaxRelaxedAttempts = 4096;
        private const float RelaxedMinDistanceFactor = 104f / 120f;

        private readonly Rect spawnBounds;
        private readonly float minDistance;
        private readonly float relaxedMinDistance;
        private readonly IReadOnlyList<Rect> excludedAreas;

        public IceSpawnPositioner(
            Rect spawnBounds,
            float minDistance,
            IReadOnlyList<Rect>? excludedAreas = null,
            float excludedAreaPadding = 0f)
        {
            this.spawnBounds = spawnBounds;
            this.minDistance = minDistance;
            relaxedMinDistance = minDistance * RelaxedMinDistanceFactor;
            this.excludedAreas = ExpandExcludedAreas(excludedAreas, excludedAreaPadding);
        }

        /// <summary>
        /// Try to find a position that is at least <paramref name="minDistance"/> from every
        /// position in <paramref name="existing"/>. Returns true on success.
        /// </summary>
        public bool TryGetPosition(IReadOnlyList<Vector2> existing, out Vector2 position)
        {
            var bestCandidate = default(Vector2);
            var bestDistance = float.MinValue;

            // First pass: full min distance.
            for (var attempt = 0; attempt < MaxAttemptsBeforeRelax; attempt++)
            {
                var candidate = RandomPointInBounds();
                var distance = MinimumDistance(candidate, existing);
                if (distance > bestDistance)
                {
                    bestCandidate = candidate;
                    bestDistance = distance;
                }
            }

            if (bestDistance >= minDistance)
            {
                position = bestCandidate;
                return true;
            }

            // Second pass: relaxed distance (104px instead of 120px).
            bestDistance = float.MinValue;
            for (var attempt = 0; attempt < MaxRelaxedAttempts; attempt++)
            {
                var candidate = RandomPointInBounds();
                var distance = MinimumDistance(candidate, existing);
                if (distance > bestDistance)
                {
                    bestCandidate = candidate;
                    bestDistance = distance;
                }
            }

            position = bestCandidate;
            return bestDistance >= relaxedMinDistance;
        }

        private static IReadOnlyList<Rect> ExpandExcludedAreas(
            IReadOnlyList<Rect>? areas,
            float padding)
        {
            if (areas == null || areas.Count == 0)
            {
                return Array.Empty<Rect>();
            }

            var expanded = new Rect[areas.Count];
            for (var i = 0; i < areas.Count; i++)
            {
                var area = areas[i];
                expanded[i] = new Rect(
                    area.xMin - padding,
                    area.yMin - padding,
                    area.width + padding * 2f,
                    area.height + padding * 2f);
            }

            return expanded;
        }

        private Vector2 RandomPointInBounds()
        {
            const int maxProtectedAreaAttempts = 1000;
            for (var attempt = 0; attempt < maxProtectedAreaAttempts; attempt++)
            {
                var candidate = new Vector2(
                    UnityEngine.Random.Range(spawnBounds.xMin, spawnBounds.xMax),
                    UnityEngine.Random.Range(spawnBounds.yMin, spawnBounds.yMax));
                if (!IsExcluded(candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Spawn bounds do not contain an allowed ice position.");
        }

        private bool IsExcluded(Vector2 candidate)
        {
            for (var i = 0; i < excludedAreas.Count; i++)
            {
                if (excludedAreas[i].Contains(candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private static float MinimumDistance(Vector2 candidate, IReadOnlyList<Vector2> existing)
        {
            var minimum = float.MaxValue;
            for (var i = 0; i < existing.Count; i++)
            {
                var distance = Vector2.Distance(candidate, existing[i]);
                if (distance < minimum)
                {
                    minimum = distance;
                }
            }

            return minimum;
        }
    }
}
