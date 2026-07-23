#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Maintenance;
using Icebreaker.UI.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.UI.Management
{
    /// <summary>
    /// Compatibility facade that coordinates the focused UI-05 views and forwards
    /// their interaction events to the integration layer.
    /// </summary>
    public sealed class ManagementViewsPresenter : MonoBehaviour
    {
        public const float ArrivalDurationSeconds = ArrivalOverlayView.DurationSeconds;

        [Header("Mode")]
        [SerializeField] private bool finalGameMode;

        [Header("Header")]
        [SerializeField] private Button? stageStartButton;
        [SerializeField] private Button? settingsButton;
        [SerializeField] private Button? collapseButton;
        [SerializeField] private TMP_Text? fundsText;
        [SerializeField] private TMP_Text? stageStartText;

        [Header("Focused Views")]
        [SerializeField] private RouteStatusView? routeStatusView;
        [SerializeField] private SettingsModalView? settingsModalView;
        [SerializeField] private ArrivalOverlayView? arrivalOverlayView;

        private bool initialized;
        private bool currentCanStart;

        public event Action StageStartRequested = delegate { };
        public event Action CollapseRequested = delegate { };
        public event Action<bool> SettingsVisibilityChanged = delegate { };
        public event Action<float> MasterVolumeChanged = delegate { };
        public event Action<bool> ScreenShakeChanged = delegate { };
        public event Action QuitRequested = delegate { };
        public event Action<string> ArrivalPresentationCompleted = delegate { };

        public bool IsRouteVisible => routeStatusView != null && routeStatusView.IsVisible;

        public bool IsSettingsVisible => settingsModalView != null && settingsModalView.IsVisible;

        public bool IsArrivalPlaying => arrivalOverlayView != null && arrivalOverlayView.IsPlaying;

        public bool IsFinalGameMode => finalGameMode;

        public string LastCompletedArrivalId =>
            arrivalOverlayView?.LastCompletedArrivalId ?? string.Empty;

        private void Awake() => EnsureInitialized();

        private void OnEnable() => EnsureInitialized();

        private void OnDestroy()
        {
            if (!initialized)
            {
                return;
            }

            stageStartButton?.onClick.RemoveListener(HandleStageStart);
            settingsButton?.onClick.RemoveListener(OpenSettings);
            collapseButton?.onClick.RemoveListener(HandleCollapse);

            if (settingsModalView != null)
            {
                settingsModalView.VisibilityChanged -= HandleSettingsVisibilityChanged;
                settingsModalView.MasterVolumeChanged -= HandleMasterVolumeChanged;
                settingsModalView.ScreenShakeChanged -= HandleScreenShakeChanged;
                settingsModalView.QuitRequested -= HandleQuitRequested;
            }

            if (arrivalOverlayView != null)
            {
                arrivalOverlayView.PresentationCompleted -= HandleArrivalPresentationCompleted;
            }
        }

        public void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            stageStartButton?.onClick.AddListener(HandleStageStart);
            settingsButton?.onClick.AddListener(OpenSettings);
            collapseButton?.onClick.AddListener(HandleCollapse);

            if (settingsModalView != null)
            {
                settingsModalView.EnsureInitialized();
                settingsModalView.VisibilityChanged += HandleSettingsVisibilityChanged;
                settingsModalView.MasterVolumeChanged += HandleMasterVolumeChanged;
                settingsModalView.ScreenShakeChanged += HandleScreenShakeChanged;
                settingsModalView.QuitRequested += HandleQuitRequested;
            }

            if (arrivalOverlayView != null)
            {
                arrivalOverlayView.PresentationCompleted += HandleArrivalPresentationCompleted;
            }

            initialized = true;
            if (finalGameMode)
            {
                SetRouteVisible(false);
            }
            else
            {
                routeStatusView?.Show();
            }
        }

        public void EnableFinalGameMode()
        {
            finalGameMode = true;
            EnsureInitialized();
            SetRouteVisible(false);
        }

        /// <summary>
        /// Keeps the established integration signature. Maintenance data is rendered
        /// by the dedicated maintenance tree and is intentionally ignored here.
        /// </summary>
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

            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            if (funds < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(funds));
            }

            EnsureInitialized();
            currentCanStart = canStartStage && !route.GameCompleted;
            RenderHeader(route, funds, Math.Max(0d, nextStageRemainingSeconds));
            routeStatusView?.Render(route);
        }

        public void SetRouteVisible(bool visible)
        {
            if (!finalGameMode)
            {
                SetActive(gameObject, visible);
                return;
            }

            var header = collapseButton != null ? collapseButton.transform.parent?.gameObject : null;
            var body = routeStatusView != null ? routeStatusView.transform.parent?.gameObject : null;
            var background = body != null ? body.transform.parent?.GetComponent<Image>() : null;
            SetActive(header, visible);
            SetActive(body, visible);
            routeStatusView?.SetVisible(visible);
            if (background != null)
            {
                background.enabled = visible;
            }
        }

        public void OpenSettings()
        {
            EnsureInitialized();
            settingsModalView?.Open();
        }

        public void CloseSettings() => settingsModalView?.Close();

        public void SetSettingsValues(float masterVolume, bool screenShakeEnabled)
        {
            EnsureInitialized();
            settingsModalView?.SetValues(masterVolume, screenShakeEnabled);
        }

        public bool PresentArrival(ArrivalPresentationRequested request)
        {
            EnsureInitialized();
            return arrivalOverlayView?.Present(request) ?? false;
        }

        public void AdvanceArrivalForValidation(float unscaledDeltaTime) =>
            arrivalOverlayView?.AdvanceForValidation(unscaledDeltaTime);

        private void RenderHeader(RouteStatusViewData route, long funds, double remainingSeconds)
        {
            if (fundsText != null)
            {
                fundsText.text = $"보유 자금 {HudTextFormatter.FormatFunds(funds)}";
            }

            if (stageStartText != null)
            {
                stageStartText.text = route.GameCompleted
                    ? "운항 완료"
                    : currentCanStart
                        ? "쇄빙 시작"
                        : $"다음 쇄빙 {HudTextFormatter.FormatCountdown(remainingSeconds)}";
            }

            if (stageStartButton != null)
            {
                stageStartButton.interactable = currentCanStart;
            }
        }

        private void HandleStageStart()
        {
            if (currentCanStart)
            {
                StageStartRequested();
            }
        }

        private void HandleCollapse() => CollapseRequested();

        private void HandleSettingsVisibilityChanged(bool visible) =>
            SettingsVisibilityChanged(visible);

        private void HandleMasterVolumeChanged(float value) => MasterVolumeChanged(value);

        private void HandleScreenShakeChanged(bool value) => ScreenShakeChanged(value);

        private void HandleQuitRequested() => QuitRequested();

        private void HandleArrivalPresentationCompleted(string destinationId) =>
            ArrivalPresentationCompleted(destinationId);

        private static void SetActive(GameObject? target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
