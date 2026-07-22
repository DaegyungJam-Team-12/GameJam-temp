#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Icebreaker.Shared.Maintenance;
using Icebreaker.UI.Sandbox;
using TMPro;
using UnityEngine;

namespace Icebreaker.UI.Maintenance
{
    public sealed class MaintenanceTreePresenter : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MaintenanceTreeLayoutAsset? layout;
        [SerializeField] private MonoBehaviour? sourceBehaviour;
        [SerializeField] private UiThemeAsset? theme;

        [Header("Tree")]
        [SerializeField] private RectTransform? content;
        [SerializeField] private RectTransform? edgeLayer;
        [SerializeField] private RectTransform? nodeLayer;
        [SerializeField] private MaintenanceNodeView? nodePrefab;
        [SerializeField] private MaintenanceTreeEdgeView? edgePrefab;

        [Header("Chrome")]
        [SerializeField] private TMP_Text? fundsText;
        [SerializeField] private TMP_Text? previewStateText;

        private IMaintenanceStepViewDataSource? source;
        private bool subscribed;

        public int VisibleNodeCount { get; private set; }

        private void OnEnable()
        {
            source = sourceBehaviour as IMaintenanceStepViewDataSource;
            if (!ValidateReferences())
            {
                return;
            }

            source!.EnsureInitialized();
            source.StepsChanged += Render;
            subscribed = true;
            Render(source.CurrentSteps);
        }

        private void OnDisable()
        {
            if (subscribed && source != null)
            {
                source.StepsChanged -= Render;
            }

            subscribed = false;
        }

        public void Render(IReadOnlyList<MaintenancePurchaseStepViewData> steps)
        {
            if (!ValidateReferences())
            {
                return;
            }

            var expectedIds = new string[steps.Count];
            var dataById = new Dictionary<string, MaintenancePurchaseStepViewData>(StringComparer.Ordinal);
            for (var index = 0; index < steps.Count; index++)
            {
                expectedIds[index] = steps[index].StepId;
                dataById.Add(steps[index].StepId, steps[index]);
            }

            var layoutErrors = layout!.Validate(expectedIds);
            if (layoutErrors.Count > 0)
            {
                Debug.LogError("[UI-TREE-01] Layout validation failed:\n- " +
                               string.Join("\n- ", layoutErrors), this);
                return;
            }

            content!.sizeDelta = layout.ContentSize;
            ClearChildren(edgeLayer!);
            ClearChildren(nodeLayer!);

            var layoutById = new Dictionary<string, MaintenanceTreeNodeLayout>(StringComparer.Ordinal);
            foreach (var nodeLayout in layout.Nodes)
            {
                layoutById.Add(nodeLayout.StepId, nodeLayout);
            }

            RenderEdges(layoutById, dataById);
            RenderNodes(layoutById, dataById);

            if (source != null)
            {
                if (fundsText != null)
                {
                    fundsText.text = "정비 자금 " +
                                     source.CurrentFunds.ToString("N0", CultureInfo.InvariantCulture);
                }

                if (previewStateText != null)
                {
                    previewStateText.text = "가짜 상태 · " + source.CurrentPreviewStateLabel;
                }
            }
        }

        private void RenderEdges(
            IReadOnlyDictionary<string, MaintenanceTreeNodeLayout> layoutById,
            IReadOnlyDictionary<string, MaintenancePurchaseStepViewData> dataById)
        {
            foreach (var edge in layout!.Edges)
            {
                var target = dataById[edge.ToStepId];
                if (target.Visibility == MaintenanceStepVisibility.Hidden)
                {
                    continue;
                }

                var color = ResolveEdgeColor(target);
                var points = new List<Vector2>(edge.BendPoints.Count + 2)
                {
                    ToAnchored(layoutById[edge.FromStepId].Position)
                };
                foreach (var bendPoint in edge.BendPoints)
                {
                    points.Add(ToAnchored(bendPoint));
                }

                points.Add(ToAnchored(layoutById[edge.ToStepId].Position));
                for (var index = 0; index < points.Count - 1; index++)
                {
                    var segment = Instantiate(edgePrefab!, edgeLayer!, false);
                    segment.name = $"Edge_{edge.FromStepId}_{edge.ToStepId}_{index}";
                    segment.Render(points[index], points[index + 1], 6f, color);
                }
            }
        }

        private void RenderNodes(
            IReadOnlyDictionary<string, MaintenanceTreeNodeLayout> layoutById,
            IReadOnlyDictionary<string, MaintenancePurchaseStepViewData> dataById)
        {
            VisibleNodeCount = 0;
            foreach (var pair in dataById)
            {
                var node = Instantiate(nodePrefab!, nodeLayer!, false);
                node.name = "Node_" + pair.Key;
                var rect = (RectTransform)node.transform;
                rect.anchoredPosition = ToAnchored(layoutById[pair.Key].Position);
                node.Render(pair.Value, layoutById[pair.Key], theme);
                if (pair.Value.Visibility != MaintenanceStepVisibility.Hidden)
                {
                    VisibleNodeCount++;
                }
            }
        }

        private Color ResolveEdgeColor(MaintenancePurchaseStepViewData target)
        {
            var primary = theme != null ? theme.PrimaryText : Color.white;
            var success = theme != null ? theme.Success : Color.cyan;
            var action = theme != null ? theme.ActionAccent : new Color(1f, 0.6f, 0.2f, 1f);
            return target.PurchaseState switch
            {
                MaintenanceStepPurchaseState.Purchased => success,
                MaintenanceStepPurchaseState.Available => action,
                _ when target.Visibility == MaintenanceStepVisibility.Preview =>
                    new Color(primary.r, primary.g, primary.b, 0.22f),
                _ => new Color(primary.r, primary.g, primary.b, 0.4f)
            };
        }

        private bool ValidateReferences()
        {
            if (layout != null && sourceBehaviour != null && sourceBehaviour is IMaintenanceStepViewDataSource &&
                content != null && edgeLayer != null && nodeLayer != null &&
                nodePrefab != null && edgePrefab != null)
            {
                return true;
            }

            Debug.LogError("[UI-TREE-01] Maintenance tree references are incomplete.", this);
            return false;
        }

        private static Vector2 ToAnchored(Vector2 topLeftPosition) =>
            new Vector2(topLeftPosition.x, -topLeftPosition.y);

        private static void ClearChildren(RectTransform parent)
        {
            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                Destroy(parent.GetChild(index).gameObject);
            }
        }
    }
}
