#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared;
using UnityEngine;

namespace Icebreaker.Shared.Maintenance
{
    [Serializable]
    public sealed class MaintenanceTreeNodeLayout
    {
        [SerializeField] private string stepId = "";
        [SerializeField] private Vector2 position;
        [SerializeField] private Vector2 visualSize;
        [SerializeField] private Sprite? icon;
        [SerializeField] private string branchLabel = "";

        public MaintenanceTreeNodeLayout(
            string stepId,
            Vector2 position,
            Vector2 visualSize,
            Sprite? icon,
            string branchLabel)
        {
            this.stepId = ContractGuards.Required(stepId, nameof(stepId));
            this.position = position;
            this.visualSize = visualSize;
            this.icon = icon;
            this.branchLabel = branchLabel ?? "";
        }

        public string StepId => stepId;

        public Vector2 Position => position;

        public Vector2 VisualSize => visualSize;

        public Sprite? Icon => icon;

        public string BranchLabel => branchLabel;
    }

    [Serializable]
    public sealed class MaintenanceTreeEdgeLayout
    {
        [SerializeField] private string fromStepId = "";
        [SerializeField] private string toStepId = "";
        [SerializeField] private Vector2[] bendPoints = Array.Empty<Vector2>();

        public MaintenanceTreeEdgeLayout(
            string fromStepId,
            string toStepId,
            IReadOnlyList<Vector2> bendPoints)
        {
            this.fromStepId = ContractGuards.Required(fromStepId, nameof(fromStepId));
            this.toStepId = ContractGuards.Required(toStepId, nameof(toStepId));
            if (bendPoints == null)
            {
                throw new ArgumentNullException(nameof(bendPoints));
            }

            this.bendPoints = new Vector2[bendPoints.Count];
            for (var index = 0; index < bendPoints.Count; index++)
            {
                this.bendPoints[index] = bendPoints[index];
            }
        }

        public string FromStepId => fromStepId;

        public string ToStepId => toStepId;

        public IReadOnlyList<Vector2> BendPoints => Array.AsReadOnly(bendPoints);
    }

    [CreateAssetMenu(
        fileName = "MaintenanceTreeLayout",
        menuName = "ICEBREAKER/UI/Maintenance Tree Layout")]
    public sealed class MaintenanceTreeLayoutAsset : ScriptableObject
    {
        public const string RootStepId = "C01-L1";

        [SerializeField] private Vector2 contentSize = new Vector2(1600f, 900f);
        [SerializeField] private MaintenanceTreeNodeLayout[] nodes =
            Array.Empty<MaintenanceTreeNodeLayout>();
        [SerializeField] private MaintenanceTreeEdgeLayout[] edges =
            Array.Empty<MaintenanceTreeEdgeLayout>();

        public Vector2 ContentSize => contentSize;

        public IReadOnlyList<MaintenanceTreeNodeLayout> Nodes => Array.AsReadOnly(nodes);

        public IReadOnlyList<MaintenanceTreeEdgeLayout> Edges => Array.AsReadOnly(edges);

        public void Configure(
            Vector2 contentSize,
            IReadOnlyList<MaintenanceTreeNodeLayout> nodes,
            IReadOnlyList<MaintenanceTreeEdgeLayout> edges)
        {
            if (nodes == null)
            {
                throw new ArgumentNullException(nameof(nodes));
            }

            if (edges == null)
            {
                throw new ArgumentNullException(nameof(edges));
            }

            this.contentSize = contentSize;
            this.nodes = Copy(nodes);
            this.edges = Copy(edges);
        }

        public IReadOnlyList<string> Validate(IReadOnlyCollection<string> expectedStepIds)
        {
            if (expectedStepIds == null)
            {
                throw new ArgumentNullException(nameof(expectedStepIds));
            }

            var errors = new List<string>();
            if (contentSize.x <= 0f || contentSize.y <= 0f)
            {
                errors.Add("Content size must be positive.");
            }

            var expected = new HashSet<string>(expectedStepIds, StringComparer.Ordinal);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes)
            {
                if (node == null)
                {
                    errors.Add("Layout nodes cannot contain null.");
                    continue;
                }

                if (!actual.Add(node.StepId))
                {
                    errors.Add($"Duplicate Step ID: {node.StepId}.");
                }

                if (!expected.Contains(node.StepId))
                {
                    errors.Add($"Unknown Step ID: {node.StepId}.");
                }

                if (node.VisualSize.x <= 0f || node.VisualSize.y <= 0f)
                {
                    errors.Add($"Node {node.StepId} visual size must be positive.");
                    continue;
                }

                var halfSize = node.VisualSize * 0.5f;
                if (node.Position.x - halfSize.x < 0f ||
                    node.Position.y - halfSize.y < 0f ||
                    node.Position.x + halfSize.x > contentSize.x ||
                    node.Position.y + halfSize.y > contentSize.y)
                {
                    errors.Add($"Node {node.StepId} must remain inside content bounds.");
                }
            }

            foreach (var expectedId in expected)
            {
                if (!actual.Contains(expectedId))
                {
                    errors.Add($"Missing Step layout: {expectedId}.");
                }
            }

            if (!actual.Contains(RootStepId))
            {
                errors.Add($"Root Step {RootStepId} is required.");
            }

            foreach (var edge in edges)
            {
                if (edge == null)
                {
                    errors.Add("Layout edges cannot contain null.");
                    continue;
                }

                if (string.Equals(edge.FromStepId, edge.ToStepId, StringComparison.Ordinal))
                {
                    errors.Add($"Edge {edge.FromStepId} cannot point to itself.");
                }

                if (!actual.Contains(edge.FromStepId))
                {
                    errors.Add($"Edge references missing Step {edge.FromStepId}.");
                }

                if (!actual.Contains(edge.ToStepId))
                {
                    errors.Add($"Edge references missing Step {edge.ToStepId}.");
                }
            }

            return errors.AsReadOnly();
        }

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            var result = new T[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                result[index] = source[index];
            }

            return result;
        }
    }
}
