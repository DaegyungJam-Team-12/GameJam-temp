#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Icebreaker.Gameplay
{
    public readonly struct SpawnPositionBlocker
    {
        public SpawnPositionBlocker(Vector2 referencePosition, float visualRadiusReferencePixels)
        {
            ReferencePosition = referencePosition;
            VisualRadiusReferencePixels = visualRadiusReferencePixels;
        }

        public Vector2 ReferencePosition { get; }

        public float VisualRadiusReferencePixels { get; }
    }

    /// <summary>
    /// Picks random spawn positions in a reference-pixel rectangle. The visual-spacing overload
    /// samples a fixed candidate set and chooses from its highest-clearance positions.
    /// </summary>
    public sealed class IceSpawnPositioner
    {
        private const int MaxAttemptsBeforeRelax = 30;
        private const int MaxRelaxedAttempts = 4096;
        private const float LegacyRelaxedMinDistanceFactor = 104f / 120f;
        private const int DefaultCandidateCount = 48;
        private const int DefaultTopCandidateCount = 8;

        private readonly Rect spawnBounds;
        private readonly float legacyMinDistance;
        private readonly float legacyRelaxedMinDistance;
        private readonly float strictExtraVisualGap;
        private readonly float relaxedExtraVisualGap;
        private readonly bool usesVisualSpacing;
        private readonly IReadOnlyList<Rect> excludedAreas;
        private readonly Vector2[] topCandidates;
        private readonly float[] topCandidateScores;
        private readonly int candidateCount;

        public IceSpawnPositioner(
            Rect spawnBounds,
            float minDistance,
            IReadOnlyList<Rect>? excludedAreas = null,
            float excludedAreaPadding = 0f)
        {
            this.spawnBounds = spawnBounds;
            legacyMinDistance = minDistance;
            legacyRelaxedMinDistance = minDistance * LegacyRelaxedMinDistanceFactor;
            strictExtraVisualGap = 0f;
            relaxedExtraVisualGap = 0f;
            usesVisualSpacing = false;
            this.excludedAreas = ExpandExcludedAreas(excludedAreas, excludedAreaPadding);
            topCandidates = new Vector2[DefaultTopCandidateCount];
            topCandidateScores = new float[DefaultTopCandidateCount];
            candidateCount = DefaultCandidateCount;
        }

        public IceSpawnPositioner(
            Rect spawnBounds,
            float strictExtraVisualGap,
            float relaxedExtraVisualGap,
            IReadOnlyList<Rect>? excludedAreas = null,
            float excludedAreaPadding = 0f,
            int candidateCount = DefaultCandidateCount,
            int topCandidateCount = DefaultTopCandidateCount)
        {
            if (strictExtraVisualGap < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(strictExtraVisualGap));
            }

            if (relaxedExtraVisualGap < 0f || relaxedExtraVisualGap > strictExtraVisualGap)
            {
                throw new ArgumentOutOfRangeException(nameof(relaxedExtraVisualGap));
            }

            if (candidateCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(candidateCount));
            }

            if (topCandidateCount <= 0 || topCandidateCount > candidateCount)
            {
                throw new ArgumentOutOfRangeException(nameof(topCandidateCount));
            }

            this.spawnBounds = spawnBounds;
            legacyMinDistance = 0f;
            legacyRelaxedMinDistance = 0f;
            this.strictExtraVisualGap = strictExtraVisualGap;
            this.relaxedExtraVisualGap = relaxedExtraVisualGap;
            usesVisualSpacing = true;
            this.excludedAreas = ExpandExcludedAreas(excludedAreas, excludedAreaPadding);
            topCandidates = new Vector2[topCandidateCount];
            topCandidateScores = new float[topCandidateCount];
            this.candidateCount = candidateCount;
        }

        /// <summary>
        /// Legacy fixed-distance placement retained for focused combat tests and callers that do
        /// not provide per-ice visual dimensions.
        /// </summary>
        public bool TryGetPosition(IReadOnlyList<Vector2> existing, out Vector2 position)
        {
            var bestCandidate = default(Vector2);
            var bestDistance = float.MinValue;

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

            if (bestDistance >= legacyMinDistance)
            {
                position = bestCandidate;
                return true;
            }

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
            return bestDistance >= legacyRelaxedMinDistance;
        }

        /// <summary>
        /// Uses visual radii plus strict then relaxed gaps. Recent destruction locations use a
        /// separate fixed-distance exclusion so newly spawned ice does not refill in place.
        /// </summary>
        public bool TryGetPosition(
            IReadOnlyList<SpawnPositionBlocker> blockers,
            float candidateVisualRadiusReferencePixels,
            IReadOnlyList<Vector2> recentDestructionPositions,
            float recentDestructionExclusionReferencePixels,
            out Vector2 position)
        {
            if (!usesVisualSpacing)
            {
                throw new InvalidOperationException(
                    "Visual spawn placement requires the visual-spacing IceSpawnPositioner constructor.");
            }

            if (candidateVisualRadiusReferencePixels <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(candidateVisualRadiusReferencePixels));
            }

            if (recentDestructionExclusionReferencePixels < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(recentDestructionExclusionReferencePixels));
            }

            return TryGetVisualPosition(
                       blockers,
                       candidateVisualRadiusReferencePixels,
                       recentDestructionPositions,
                       recentDestructionExclusionReferencePixels,
                       strictExtraVisualGap,
                       out position)
                   || TryGetVisualPosition(
                       blockers,
                       candidateVisualRadiusReferencePixels,
                       recentDestructionPositions,
                       recentDestructionExclusionReferencePixels,
                       relaxedExtraVisualGap,
                       out position);
        }

        private bool TryGetVisualPosition(
            IReadOnlyList<SpawnPositionBlocker> blockers,
            float candidateVisualRadiusReferencePixels,
            IReadOnlyList<Vector2> recentDestructionPositions,
            float recentDestructionExclusionReferencePixels,
            float visualGap,
            out Vector2 position)
        {
            var selectedCount = 0;
            for (var attempt = 0; attempt < candidateCount; attempt++)
            {
                var candidate = RandomPointInBounds();
                if (!TryGetClearance(
                        candidate,
                        blockers,
                        candidateVisualRadiusReferencePixels,
                        recentDestructionPositions,
                        recentDestructionExclusionReferencePixels,
                        visualGap,
                        out var clearance))
                {
                    continue;
                }

                InsertTopCandidate(candidate, clearance, ref selectedCount);
            }

            if (selectedCount == 0)
            {
                position = default;
                return false;
            }

            var totalWeight = selectedCount * (selectedCount + 1) / 2;
            var roll = UnityEngine.Random.Range(0, totalWeight);
            for (var index = 0; index < selectedCount; index++)
            {
                var weight = selectedCount - index;
                if (roll < weight)
                {
                    position = topCandidates[index];
                    return true;
                }

                roll -= weight;
            }

            position = topCandidates[selectedCount - 1];
            return true;
        }

        private bool TryGetClearance(
            Vector2 candidate,
            IReadOnlyList<SpawnPositionBlocker> blockers,
            float candidateVisualRadiusReferencePixels,
            IReadOnlyList<Vector2> recentDestructionPositions,
            float recentDestructionExclusionReferencePixels,
            float visualGap,
            out float clearance)
        {
            clearance = float.MaxValue;
            for (var i = 0; i < blockers.Count; i++)
            {
                var blocker = blockers[i];
                var requiredDistance = candidateVisualRadiusReferencePixels +
                    blocker.VisualRadiusReferencePixels + visualGap;
                var distance = Vector2.Distance(candidate, blocker.ReferencePosition);
                if (distance < requiredDistance)
                {
                    return false;
                }

                clearance = Mathf.Min(clearance, distance - requiredDistance);
            }

            for (var i = 0; i < recentDestructionPositions.Count; i++)
            {
                var distance = Vector2.Distance(candidate, recentDestructionPositions[i]);
                if (distance < recentDestructionExclusionReferencePixels)
                {
                    return false;
                }

                clearance = Mathf.Min(
                    clearance,
                    distance - recentDestructionExclusionReferencePixels);
            }

            if (clearance == float.MaxValue)
            {
                clearance = 0f;
            }

            var candidateZone = GetScreenZone(candidate);
            var sameZoneBlockerCount = 0;
            for (var i = 0; i < blockers.Count; i++)
            {
                if (GetScreenZone(blockers[i].ReferencePosition) == candidateZone)
                {
                    sameZoneBlockerCount++;
                }
            }

            // Keep this deliberately small: clearance remains the primary placement criterion.
            clearance -= sameZoneBlockerCount * 2f;

            return true;
        }

        private int GetScreenZone(Vector2 position)
        {
            var normalizedX = spawnBounds.width > 0f
                ? (position.x - spawnBounds.xMin) / spawnBounds.width
                : 0f;
            var normalizedY = spawnBounds.height > 0f
                ? (position.y - spawnBounds.yMin) / spawnBounds.height
                : 0f;
            var x = Mathf.Clamp((int)(normalizedX * 3f), 0, 2);
            var y = Mathf.Clamp((int)(normalizedY * 3f), 0, 2);
            return y * 3 + x;
        }

        private void InsertTopCandidate(Vector2 candidate, float score, ref int selectedCount)
        {
            var insertIndex = selectedCount;
            if (insertIndex < topCandidates.Length)
            {
                selectedCount++;
            }
            else if (score <= topCandidateScores[topCandidates.Length - 1])
            {
                return;
            }
            else
            {
                insertIndex = topCandidates.Length - 1;
            }

            while (insertIndex > 0 && score > topCandidateScores[insertIndex - 1])
            {
                if (insertIndex < topCandidates.Length)
                {
                    topCandidates[insertIndex] = topCandidates[insertIndex - 1];
                    topCandidateScores[insertIndex] = topCandidateScores[insertIndex - 1];
                }

                insertIndex--;
            }

            topCandidates[insertIndex] = candidate;
            topCandidateScores[insertIndex] = score;
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
