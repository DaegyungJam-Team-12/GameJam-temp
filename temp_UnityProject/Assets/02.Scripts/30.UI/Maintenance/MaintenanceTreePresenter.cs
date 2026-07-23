#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Icebreaker.Shared.Maintenance;
using Icebreaker.UI.Sandbox;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.UI.Maintenance
{
    public sealed class MaintenanceTreePresenter : MonoBehaviour
    {
        private const float BottomRowTooltipVerticalOffset = 40f;

        [Header("Data")]
        [SerializeField] private MaintenanceTreeLayoutAsset? layout;
        [SerializeField] private MonoBehaviour? sourceBehaviour;
        [SerializeField] private UiThemeAsset? theme;

        [Header("Tree")]
        [SerializeField] private RectTransform? content;
        [SerializeField] private RectTransform? edgeLayer;
        [SerializeField] private RectTransform? nodeLayer;
        [SerializeField] private MaintenanceTreeViewport? viewport;
        [SerializeField] private MaintenanceNodeView? nodePrefab;
        [SerializeField] private MaintenanceTreeEdgeView? edgePrefab;

        [Header("Selection")]
        [SerializeField] private RectTransform? tooltipOverlay;
        [SerializeField] private RectTransform? treeViewport;
        [SerializeField] private MaintenanceTooltipView? tooltipView;

        [Header("Chrome")]
        [SerializeField] private TMP_Text? fundsText;
        [SerializeField] private TMP_Text? previewStateText;
        [SerializeField] private Button? closeButton;
        [SerializeField] private Button? stageStartButton;

        private IMaintenanceStepViewDataSource? source;
        private bool subscribed;
        private bool listenersAdded;
        private bool stageStartAvailable = true;
        private bool stageStartRequestPending;
        private readonly Dictionary<string, MaintenancePurchaseStepViewData> dataById =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, MaintenanceNodeView> nodesById =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> pendingPurchaseStepIds = new(StringComparer.Ordinal);
        private string? hoveredStepId;

        public event Action<string> PurchaseRequested = delegate { };
        public event Action CloseRequested = delegate { };
        public event Action StageStartRequested = delegate { };

        public int VisibleNodeCount { get; private set; }

        private void Awake() => AddButtonListeners();

        private void OnEnable()
        {
            source ??= sourceBehaviour as IMaintenanceStepViewDataSource;
            stageStartRequestPending = false;
            if (stageStartButton != null)
            {
                stageStartButton.interactable = stageStartAvailable;
            }

            AddButtonListeners();
            if (source != null)
            {
                Subscribe();
            }
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            Unsubscribe();
            RemoveButtonListeners();
        }

        public void Bind(IMaintenanceStepViewDataSource dataSource)
        {
            if (dataSource == null)
            {
                throw new ArgumentNullException(nameof(dataSource));
            }

            Unsubscribe();
            source = dataSource;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        public void RejectStageStartRequest()
        {
            stageStartRequestPending = false;
            if (stageStartButton != null)
            {
                stageStartButton.interactable = stageStartAvailable;
            }
        }

        public void SetStageStartAvailable(bool available)
        {
            stageStartAvailable = available;
            if (!available)
            {
                stageStartRequestPending = false;
            }

            if (stageStartButton != null)
            {
                stageStartButton.interactable = available && !stageStartRequestPending;
            }
        }

        private void Subscribe()
        {
            if (subscribed || source == null || !ValidateReferences())
            {
                return;
            }

            source.EnsureInitialized();
            source.StepsChanged += Render;
            var inputViewport = viewport!;
            inputViewport.StepDoubleClicked += HandleStepDoubleClicked;
            inputViewport.StepHovered += HandleStepHovered;
            inputViewport.StepHoverExited += HandleStepHoverExited;
            subscribed = true;
            Render(source.CurrentSteps);
        }

        private void Unsubscribe()
        {
            ClearHover();

            if (subscribed && source != null)
            {
                source.StepsChanged -= Render;
            }

            if (subscribed && viewport != null)
            {
                viewport.StepDoubleClicked -= HandleStepDoubleClicked;
                viewport.StepHovered -= HandleStepHovered;
                viewport.StepHoverExited -= HandleStepHoverExited;
            }

            viewport?.ClearHover();
            viewport?.CancelPointer();

            subscribed = false;
        }

        public void Render(IReadOnlyList<MaintenancePurchaseStepViewData> steps)
        {
            if (!ValidateReferences())
            {
                return;
            }

            var expectedIds = new string[steps.Count];
            dataById.Clear();
            pendingPurchaseStepIds.Clear();
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
            RestoreHover();

            if (source != null)
            {
                if (fundsText != null)
                {
                    fundsText.text = "정비 자금 " +
                                     source.CurrentFunds.ToString("N0", CultureInfo.InvariantCulture);
                }

                if (previewStateText != null)
                {
                    previewStateText.text = source.CurrentPreviewStateLabel;
                }
            }
        }

        private void AddButtonListeners()
        {
            if (listenersAdded || closeButton == null || stageStartButton == null)
            {
                return;
            }

            closeButton.onClick.AddListener(HandleCloseClicked);
            stageStartButton.onClick.AddListener(HandleStageStartClicked);
            listenersAdded = true;
        }

        private void RemoveButtonListeners()
        {
            if (!listenersAdded)
            {
                return;
            }

            closeButton?.onClick.RemoveListener(HandleCloseClicked);
            stageStartButton?.onClick.RemoveListener(HandleStageStartClicked);
            listenersAdded = false;
        }

        private void HandleCloseClicked() => CloseRequested();

        private void HandleStageStartClicked()
        {
            if (!stageStartAvailable || stageStartRequestPending)
            {
                return;
            }

            stageStartRequestPending = true;
            if (stageStartButton != null)
            {
                stageStartButton.interactable = false;
            }

            StageStartRequested();
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

                var lit = target.PurchaseState is MaintenanceStepPurchaseState.Purchased or
                    MaintenanceStepPurchaseState.Available;
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
                    segment.Render(points[index], points[index + 1], 6f, lit);
                }
            }
        }

        private void RenderNodes(
            IReadOnlyDictionary<string, MaintenanceTreeNodeLayout> layoutById,
            IReadOnlyDictionary<string, MaintenancePurchaseStepViewData> dataById)
        {
            VisibleNodeCount = 0;
            nodesById.Clear();
            foreach (var pair in dataById)
            {
                var node = Instantiate(nodePrefab!, nodeLayer!, false);
                node.name = "Node_" + pair.Key;
                var rect = (RectTransform)node.transform;
                rect.anchoredPosition = ToAnchored(layoutById[pair.Key].Position);
                node.Render(pair.Value, layoutById[pair.Key], theme);
                node.ConfigureInput(viewport!);
                nodesById.Add(pair.Key, node);
                if (pair.Value.Visibility != MaintenanceStepVisibility.Hidden)
                {
                    VisibleNodeCount++;
                }
            }
        }

        private void HandleStepHovered(string stepId)
        {
            if (!dataById.TryGetValue(stepId, out var data) ||
                data.Visibility == MaintenanceStepVisibility.Hidden ||
                !nodesById.TryGetValue(stepId, out var node))
            {
                return;
            }

            if (hoveredStepId != null && nodesById.TryGetValue(hoveredStepId, out var previous))
            {
                previous.SetSelected(false);
            }

            hoveredStepId = stepId;
            node.SetSelected(true);
            ShowTooltip(data, node);
        }

        private void HandleStepDoubleClicked(string stepId) => TryPurchase(stepId);

        private void HandleStepHoverExited(string stepId)
        {
            if (!string.Equals(hoveredStepId, stepId, StringComparison.Ordinal))
            {
                return;
            }

            ClearHover();
        }

        private void ClearHover()
        {
            if (hoveredStepId != null && nodesById.TryGetValue(hoveredStepId, out var node))
            {
                node.SetSelected(false);
            }

            hoveredStepId = null;
            tooltipView?.Hide();
        }

        private void TryPurchase(string stepId)
        {
            if (dataById.TryGetValue(stepId, out var data) &&
                data.CanPurchase &&
                pendingPurchaseStepIds.Add(stepId))
            {
                PurchaseRequested(stepId);
            }
        }

        private void RestoreHover()
        {
            if (hoveredStepId != null &&
                dataById.TryGetValue(hoveredStepId, out var data) &&
                data.Visibility != MaintenanceStepVisibility.Hidden &&
                nodesById.TryGetValue(hoveredStepId, out var node))
            {
                node.SetSelected(true);
                ShowTooltip(data, node);
                return;
            }

            ClearHover();
        }

        private void ShowTooltip(MaintenancePurchaseStepViewData data, MaintenanceNodeView node)
        {
            if (tooltipOverlay == null || tooltipView == null)
            {
                return;
            }

            var localAnchor = (Vector2)tooltipOverlay.InverseTransformPoint(node.transform.position);
            if (IsBottomRowNode(data.StepId))
            {
                localAnchor.y += BottomRowTooltipVerticalOffset;
            }

            tooltipView.Show(
                data,
                localAnchor,
                GetViewportBoundsInTooltipOverlay());
        }

        private bool IsBottomRowNode(string stepId)
        {
            var maximumY = float.MinValue;
            foreach (var nodeLayout in layout!.Nodes)
            {
                maximumY = Mathf.Max(maximumY, nodeLayout.Position.y);
            }

            foreach (var nodeLayout in layout.Nodes)
            {
                if (nodeLayout.StepId == stepId)
                {
                    return Mathf.Approximately(nodeLayout.Position.y, maximumY);
                }
            }

            return false;
        }

        private Rect GetViewportBoundsInTooltipOverlay()
        {
            var corners = new Vector3[4];
            treeViewport!.GetWorldCorners(corners);
            var minimum = (Vector2)tooltipOverlay!.InverseTransformPoint(corners[0]);
            var maximum = minimum;
            for (var index = 1; index < corners.Length; index++)
            {
                var corner = (Vector2)tooltipOverlay.InverseTransformPoint(corners[index]);
                minimum = Vector2.Min(minimum, corner);
                maximum = Vector2.Max(maximum, corner);
            }

            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        private bool ValidateReferences()
        {
            if (layout != null && source != null &&
                content != null && edgeLayer != null && nodeLayer != null &&
                viewport != null && nodePrefab != null && edgePrefab != null &&
                tooltipOverlay != null && treeViewport != null && tooltipView != null &&
                closeButton != null && stageStartButton != null)
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
