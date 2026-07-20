#nullable enable

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
        private const float RelaxedMinDistanceFactor = 104f / 120f;

        private readonly Rect spawnBounds;
        private readonly float minDistance;
        private readonly float relaxedMinDistance;

        public IceSpawnPositioner(Rect spawnBounds, float minDistance)
        {
            this.spawnBounds = spawnBounds;
            this.minDistance = minDistance;
            relaxedMinDistance = minDistance * RelaxedMinDistanceFactor;
        }

        /// <summary>
        /// Try to find a position that is at least <paramref name="minDistance"/> from every
        /// position in <paramref name="existing"/>. Returns true on success.
        /// </summary>
        public bool TryGetPosition(IReadOnlyList<Vector2> existing, out Vector2 position)
        {
            // First pass: full min distance.
            for (var attempt = 0; attempt < MaxAttemptsBeforeRelax; attempt++)
            {
                var candidate = RandomPointInBounds();
                if (IsFarEnough(candidate, existing, minDistance))
                {
                    position = candidate;
                    return true;
                }
            }

            // Second pass: relaxed distance (104px instead of 120px).
            for (var attempt = 0; attempt < MaxAttemptsBeforeRelax; attempt++)
            {
                var candidate = RandomPointInBounds();
                if (IsFarEnough(candidate, existing, relaxedMinDistance))
                {
                    position = candidate;
                    return true;
                }
            }

            // Last resort: accept any random position.
            position = RandomPointInBounds();
            return true;
        }

        private Vector2 RandomPointInBounds()
        {
            return new Vector2(
                Random.Range(spawnBounds.xMin, spawnBounds.xMax),
                Random.Range(spawnBounds.yMin, spawnBounds.yMax));
        }

        private static bool IsFarEnough(Vector2 candidate, IReadOnlyList<Vector2> existing, float distance)
        {
            for (var i = 0; i < existing.Count; i++)
            {
                if (Vector2.Distance(candidate, existing[i]) < distance)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
