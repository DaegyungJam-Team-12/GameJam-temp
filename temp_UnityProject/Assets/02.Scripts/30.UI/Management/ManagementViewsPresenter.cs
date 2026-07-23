#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Maintenance;
using Icebreaker.UI.Feedback;
using Icebreaker.UI.Hud;
using Icebreaker.UI.Sandbox;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.UI.Management
{
    public sealed class ManagementViewsPresenter : MonoBehaviour
    {
        public const float ArrivalDurationSeconds = 1.5f;

        [Header("Theme")]
        [SerializeField] private UiThemeAsset? theme;
        [SerializeField] private bool finalGameMode;

        [Header("Header")]
        [SerializeField] private Button? maintenanceTabButton;
        [SerializeField] private Button? routeTabButton;
        [SerializeField] private Button? stageStartButton;
        [SerializeField] private Button? settingsButton;
        [SerializeField] private Button? collapseButton;
        [SerializeField] private TMP_Text? fundsText;
        [SerializeField] private TMP_Text? stageStartText;

        [Header("Screens")]
        [SerializeField] private GameObject? maintenanceRoot;
        [SerializeField] private GameObject? routeRoot;
        [SerializeField] private TMP_Text? maintenanceTabText;
        [SerializeField] private TMP_Text? routeTabText;

        [Header("Maintenance Tree")]
        [SerializeField] private MaintenanceNodeView[] nodeViews = Array.Empty<MaintenanceNodeView>();
        [SerializeField] private TMP_Text? selectedNameText;
        [SerializeField] private TMP_Text? selectedStateText;
        [SerializeField] private TMP_Text? selectedLevelText;
        [SerializeField] private TMP_Text? currentEffectText;
        [SerializeField] private TMP_Text? nextEffectText;
        [SerializeField] private TMP_Text? nextCostText;
        [SerializeField] private TMP_Text? requirementText;
        [SerializeField] private Button? purchaseButton;
        [SerializeField] private TMP_Text? purchaseButtonText;

        [Header("Route Status")]
        [SerializeField] private TMP_Text? destinationNameText;
        [SerializeField] private TMP_Text? destinationProgressText;
        [SerializeField] private Image? destinationProgressFill;
        [SerializeField] private TMP_Text? cargoText;
        [SerializeField] private TMP_Text? completedDestinationsText;
        [SerializeField] private TMP_Text? upcomingDestinationsText;
        [SerializeField] private GameObject? completedBadge;

        [Header("Settings Modal")]
        [SerializeField] private GameObject? settingsRoot;
        [SerializeField] private Slider? masterVolumeSlider;
        [SerializeField] private Toggle? screenShakeToggle;
        [SerializeField] private Button? settingsCloseButton;
        [SerializeField] private Button? quitButton;

        [Header("Arrival")]
        [SerializeField] private GameObject? arrivalRoot;
        [SerializeField] private CanvasGroup? arrivalCanvasGroup;
        [SerializeField] private TMP_Text? arrivalDestinationText;
        [SerializeField] private TMP_Text? arrivalStatusText;

        private readonly Dictionary<string, MaintenanceNodeViewData> nodesById =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> acceptedArrivalIds = new(StringComparer.Ordinal);
        private readonly Queue<ArrivalPresentationRequested> queuedArrivals = new();

        private bool initialized;
        private string selectedNodeId = string.Empty;
        private bool arrivalPlaying;
        private float arrivalElapsed;
        private ArrivalPresentationRequested currentArrival;
        private RouteStatusViewData? routeData;
        private long currentFunds;
        private double currentStageRemaining;
        private bool currentCanStart;

        public event Action<string> PurchaseRequested = delegate { };
        public event Action StageStartRequested = delegate { };
        public event Action CollapseRequested = delegate { };
        public event Action<bool> SettingsVisibilityChanged = delegate { };
        public event Action<float> MasterVolumeChanged = delegate { };
        public event Action<bool> ScreenShakeChanged = delegate { };
        public event Action QuitRequested = delegate { };
        public event Action<string> ArrivalPresentationCompleted = delegate { };

        public bool IsMaintenanceVisible => maintenanceRoot != null && maintenanceRoot.activeSelf;

        public bool IsRouteVisible => routeRoot != null && routeRoot.activeSelf;

        public bool IsSettingsVisible => settingsRoot != null && settingsRoot.activeSelf;

        public bool IsArrivalPlaying => arrivalPlaying;

        public bool IsFinalGameMode => finalGameMode;

        public int RenderedNodeCount => nodesById.Count;

        public string SelectedNodeId => selectedNodeId;

        public string LastCompletedArrivalId { get; private set; } = string.Empty;

        private void Awake() => EnsureInitialized();

        private void OnEnable() => EnsureInitialized();

        private void OnDestroy()
        {
            if (!initialized)
            {
                return;
            }

            maintenanceTabButton?.onClick.RemoveListener(ShowMaintenance);
            routeTabButton?.onClick.RemoveListener(ShowRoute);
            stageStartButton?.onClick.RemoveListener(HandleStageStart);
            settingsButton?.onClick.RemoveListener(OpenSettings);
            collapseButton?.onClick.RemoveListener(HandleCollapse);
            purchaseButton?.onClick.RemoveListener(HandlePurchase);
            settingsCloseButton?.onClick.RemoveListener(CloseSettings);
            quitButton?.onClick.RemoveListener(HandleQuit);
            masterVolumeSlider?.onValueChanged.RemoveListener(HandleMasterVolumeChanged);
            screenShakeToggle?.onValueChanged.RemoveListener(HandleScreenShakeChanged);
            foreach (var nodeView in nodeViews)
            {
                if (nodeView != null)
                {
                    nodeView.Clicked -= SelectNode;
                }
            }
        }

        private void Update()
        {
            if (arrivalPlaying)
            {
                AdvanceArrival(Time.unscaledDeltaTime);
            }
        }

        public void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            maintenanceTabButton?.onClick.AddListener(ShowMaintenance);
            routeTabButton?.onClick.AddListener(ShowRoute);
            stageStartButton?.onClick.AddListener(HandleStageStart);
            settingsButton?.onClick.AddListener(OpenSettings);
            collapseButton?.onClick.AddListener(HandleCollapse);
            purchaseButton?.onClick.AddListener(HandlePurchase);
            settingsCloseButton?.onClick.AddListener(CloseSettings);
            quitButton?.onClick.AddListener(HandleQuit);
            masterVolumeSlider?.onValueChanged.AddListener(HandleMasterVolumeChanged);
            screenShakeToggle?.onValueChanged.AddListener(HandleScreenShakeChanged);
            masterVolumeSlider?.SetValueWithoutNotify(UiAudioSettings.LoadAndApplyMasterVolume());
            foreach (var nodeView in nodeViews)
            {
                if (nodeView == null)
                {
                    continue;
                }

                nodeView.EnsureInitialized();
                nodeView.Clicked += SelectNode;
            }

            initialized = true;
            SetActive(settingsRoot, false);
            SetActive(arrivalRoot, false);
            if (finalGameMode)
            {
                ApplyFinalGameMode();
                SetRouteVisible(false);
            }
            else
            {
                ShowMaintenance();
            }
        }

        public void EnableFinalGameMode()
        {
            finalGameMode = true;
            EnsureInitialized();
            ApplyFinalGameMode();
            SetRouteVisible(false);
        }

        public void Render(
            IReadOnlyList<MaintenanceNodeViewData> maintenanceNodes,
            RouteStatusViewData route,
            long funds,
            double nextStageRemainingSeconds,
            bool canStartStage)
        {
            if (maintenanceNodes == null)
            {
                throw new ArgumentNullException(nameof(maintenanceNodes));
            }

            routeData = route ?? throw new ArgumentNullException(nameof(route));
            if (funds < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(funds));
            }

            EnsureInitialized();
            currentFunds = funds;
            currentStageRemaining = Math.Max(0d, nextStageRemainingSeconds);
            currentCanStart = canStartStage && !route.GameCompleted;
            nodesById.Clear();
            foreach (var node in maintenanceNodes)
            {
                if (!nodesById.TryAdd(node.Id, node))
                {
                    throw new ArgumentException($"Duplicate maintenance node ID: {node.Id}", nameof(maintenanceNodes));
                }
            }

            if (string.IsNullOrEmpty(selectedNodeId) || !nodesById.ContainsKey(selectedNodeId))
            {
                selectedNodeId = maintenanceNodes.Count > 0 ? maintenanceNodes[0].Id : string.Empty;
            }

            RenderHeader();
            RenderMaintenanceTree();
            RenderSelectedNode();
            RenderRoute();
        }

        public void SetSettingsValues(float masterVolume, bool screenShakeEnabled)
        {
            masterVolumeSlider?.SetValueWithoutNotify(Mathf.Clamp01(masterVolume));
            screenShakeToggle?.SetIsOnWithoutNotify(screenShakeEnabled);
        }

        public void ShowMaintenance()
        {
            if (finalGameMode)
            {
                ShowRoute();
                return;
            }

            SetActive(maintenanceRoot, true);
            SetActive(routeRoot, false);
            SetTabVisuals(maintenanceSelected: true);
        }

        public void ShowRoute()
        {
            if (finalGameMode)
            {
                SetRouteVisible(true);
                return;
            }

            SetActive(maintenanceRoot, false);
            SetActive(routeRoot, true);
            SetTabVisuals(maintenanceSelected: false);
        }

        public void SetRouteVisible(bool visible)
        {
            if (!finalGameMode)
            {
                SetActive(gameObject, visible);
                return;
            }

            var header = collapseButton != null ? collapseButton.transform.parent?.gameObject : null;
            var body = routeRoot != null ? routeRoot.transform.parent?.gameObject : null;
            var background = body != null ? body.transform.parent?.GetComponent<Image>() : null;
            SetActive(header, visible);
            SetActive(body, visible);
            SetActive(maintenanceRoot, false);
            SetActive(routeRoot, visible);
            if (background != null)
            {
                background.enabled = visible;
            }

            if (visible)
            {
                SetTabVisuals(maintenanceSelected: false);
            }
        }

        public void SelectNode(string nodeId)
        {
            if (!nodesById.ContainsKey(nodeId))
            {
                return;
            }

            selectedNodeId = nodeId;
            RenderMaintenanceTree();
            RenderSelectedNode();
        }

        public bool TryRequestSelectedPurchase()
        {
            if (string.IsNullOrEmpty(selectedNodeId) ||
                !nodesById.TryGetValue(selectedNodeId, out var node) ||
                !node.CanPurchaseNextLevel)
            {
                return false;
            }

            PurchaseRequested(selectedNodeId);
            return true;
        }

        public void OpenSettings()
        {
            EnsureInitialized();
            if (IsSettingsVisible)
            {
                return;
            }

            SetActive(settingsRoot, true);
            SettingsVisibilityChanged(true);
        }

        public void CloseSettings()
        {
            if (!IsSettingsVisible)
            {
                return;
            }

            SetActive(settingsRoot, false);
            SettingsVisibilityChanged(false);
        }

        public bool PresentArrival(ArrivalPresentationRequested request)
        {
            EnsureInitialized();
            if (!acceptedArrivalIds.Add(request.DestinationId))
            {
                return false;
            }

            queuedArrivals.Enqueue(request);
            StartNextArrivalIfNeeded();
            return true;
        }

        public void AdvanceArrivalForValidation(float unscaledDeltaTime) => AdvanceArrival(unscaledDeltaTime);

        public int CountRenderedNodes(MaintenanceNodeState state)
        {
            var count = 0;
            foreach (var nodeView in nodeViews)
            {
                if (nodeView != null && nodesById.ContainsKey(nodeView.NodeId) && nodeView.RenderedState == state)
                {
                    count++;
                }
            }

            return count;
        }

        private void RenderHeader()
        {
            if (fundsText != null)
            {
                fundsText.text = $"정비 자금 {HudTextFormatter.FormatFunds(currentFunds)}";
            }

            if (stageStartText != null)
            {
                stageStartText.text = routeData?.GameCompleted == true
                    ? "운항 완료"
                    : currentCanStart
                        ? "쇄빙 시작"
                        : $"다음 쇄빙 {HudTextFormatter.FormatCountdown(currentStageRemaining)}";
            }

            if (stageStartButton != null)
            {
                stageStartButton.interactable = currentCanStart;
            }
        }

        private void RenderMaintenanceTree()
        {
            var locked = new Color32(0x35, 0x45, 0x55, 0xFF);
            var available = theme?.ActionAccent ?? new Color32(0xF3, 0x9A, 0x3D, 0xFF);
            var owned = theme?.Success ?? new Color32(0x66, 0xD3, 0xBA, 0xFF);
            var text = theme?.PrimaryText ?? Color.white;
            foreach (var nodeView in nodeViews)
            {
                if (nodeView == null || !nodesById.TryGetValue(nodeView.NodeId, out var data))
                {
                    continue;
                }

                nodeView.Render(
                    data,
                    string.Equals(selectedNodeId, data.Id, StringComparison.Ordinal),
                    locked,
                    available,
                    owned,
                    text);
            }
        }

        private void RenderSelectedNode()
        {
            if (!nodesById.TryGetValue(selectedNodeId, out var node))
            {
                return;
            }

            SetText(selectedNameText, node.DisplayName);
            SetText(selectedStateText, node.State switch
            {
                MaintenanceNodeState.Owned => "상태  보유",
                MaintenanceNodeState.Available => "상태  구매 가능",
                _ => "상태  잠김"
            });
            SetText(selectedLevelText, $"현재 단계  {node.CurrentLevel}/{node.MaxLevel}");
            SetText(currentEffectText, $"현재 효과\n{node.CurrentEffectText}");
            SetText(nextEffectText, node.IsMaxLevel ? "다음 효과\n최대 단계" : $"다음 효과\n{node.NextEffectText}");
            SetText(nextCostText, node.NextCost.HasValue
                ? $"다음 비용  {node.NextCost.Value.ToString("N0", CultureInfo.InvariantCulture)}"
                : "다음 비용  -");
            SetText(requirementText, node.MissingRequirementIds.Count > 0
                ? $"필요 선행  {string.Join(", ", node.MissingRequirementIds)}"
                : "필요 선행  충족");

            if (purchaseButton != null)
            {
                purchaseButton.interactable = node.CanPurchaseNextLevel;
            }

            SetText(purchaseButtonText, node.IsMaxLevel
                ? "최대 단계"
                : node.State == MaintenanceNodeState.Locked
                    ? "잠김"
                    : node.CanPurchaseNextLevel
                        ? "구매"
                        : "자금 부족");
        }

        private void RenderRoute()
        {
            if (routeData == null)
            {
                return;
            }

            SetText(destinationNameText, routeData.GameCompleted
                ? $"{routeData.CurrentDestinationName} · 운항 완료"
                : routeData.CurrentDestinationName);
            SetText(destinationProgressText,
                $"목적지 진행  {routeData.Progress.ToString("N0", CultureInfo.InvariantCulture)} / {routeData.Target.ToString("N0", CultureInfo.InvariantCulture)}");
            if (destinationProgressFill != null)
            {
                destinationProgressFill.fillAmount = routeData.Target <= 0
                    ? 0f
                    : Mathf.Clamp01((float)routeData.Progress / routeData.Target);
            }

            SetText(cargoText, $"운송 화물\n{routeData.CargoText}");
            SetText(completedDestinationsText, $"완료한 목적지\n{routeData.CompletedDestinationsText}");
            SetText(upcomingDestinationsText, $"이후 목적지\n{routeData.UpcomingDestinationsText}");
            SetActive(completedBadge, routeData.GameCompleted);
        }

        private void SetTabVisuals(bool maintenanceSelected)
        {
            var selected = theme?.ActionAccent ?? new Color32(0xF3, 0x9A, 0x3D, 0xFF);
            var normal = theme?.PrimaryText ?? Color.white;
            if (maintenanceTabText != null)
            {
                maintenanceTabText.color = maintenanceSelected ? selected : normal;
                maintenanceTabText.fontStyle = maintenanceSelected ? FontStyles.Bold : FontStyles.Normal;
            }

            if (routeTabText != null)
            {
                routeTabText.color = maintenanceSelected ? normal : selected;
                routeTabText.fontStyle = maintenanceSelected ? FontStyles.Normal : FontStyles.Bold;
            }
        }

        private void StartNextArrivalIfNeeded()
        {
            if (arrivalPlaying || queuedArrivals.Count == 0)
            {
                return;
            }

            currentArrival = queuedArrivals.Dequeue();
            arrivalElapsed = 0f;
            arrivalPlaying = true;
            SetText(arrivalDestinationText, currentArrival.DestinationDisplayName);
            SetText(arrivalStatusText, "보급 항로 연결 완료");
            SetActive(arrivalRoot, true);
            if (arrivalCanvasGroup != null)
            {
                arrivalCanvasGroup.alpha = 0f;
                arrivalCanvasGroup.interactable = true;
                arrivalCanvasGroup.blocksRaycasts = true;
            }
        }

        private void AdvanceArrival(float unscaledDeltaTime)
        {
            if (!arrivalPlaying)
            {
                return;
            }

            arrivalElapsed += Mathf.Max(0f, unscaledDeltaTime);
            if (arrivalCanvasGroup != null)
            {
                var fadeIn = Mathf.Clamp01(arrivalElapsed / 0.2f);
                var fadeOut = Mathf.Clamp01((ArrivalDurationSeconds - arrivalElapsed) / 0.3f);
                arrivalCanvasGroup.alpha = Mathf.Min(fadeIn, fadeOut);
            }

            if (arrivalElapsed < ArrivalDurationSeconds)
            {
                return;
            }

            arrivalPlaying = false;
            SetActive(arrivalRoot, false);
            LastCompletedArrivalId = currentArrival.DestinationId;
            ArrivalPresentationCompleted(currentArrival.DestinationId);
            StartNextArrivalIfNeeded();
        }

        private void HandleStageStart()
        {
            if (currentCanStart)
            {
                StageStartRequested();
            }
        }

        private void HandleCollapse() => CollapseRequested();

        private void HandlePurchase() => TryRequestSelectedPurchase();

        private void HandleQuit() => QuitRequested();

        private void HandleMasterVolumeChanged(float value)
        {
            UiAudioSettings.SetMasterVolume(value);
            MasterVolumeChanged(Mathf.Clamp01(value));
        }

        private void HandleScreenShakeChanged(bool value) => ScreenShakeChanged(value);

        private void ApplyFinalGameMode()
        {
            SetActive(maintenanceTabButton?.gameObject, false);
            SetActive(maintenanceRoot, false);
        }

        private static void SetText(TMP_Text? target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static void SetActive(GameObject? target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
