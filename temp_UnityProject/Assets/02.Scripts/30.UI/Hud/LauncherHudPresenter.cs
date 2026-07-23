#nullable enable

using System;
using Icebreaker.Shared.State;
using Icebreaker.UI.Sandbox;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.UI.Hud
{
    public sealed class LauncherHudPresenter : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MonoBehaviour? stateSourceBehaviour;
        [SerializeField] private UiThemeAsset? theme;
        [SerializeField] private string destinationDisplayName = "섬마을";

        [Header("State Text")]
        [SerializeField] private TMP_Text? fundsText;
        [SerializeField] private TMP_Text? destinationNameText;
        [SerializeField] private TMP_Text? destinationProgressText;
        [SerializeField] private Image? destinationProgressFill;
        [SerializeField] private TMP_Text? startButtonText;

        [Header("Actions")]
        [SerializeField] private Button? maintenanceButton;
        [SerializeField] private Button? routeButton;
        [SerializeField] private Button? startButton;
        [SerializeField] private Button? settingsButton;
        [SerializeField] private Button? quitButton;

        [Header("Theme Targets")]
        [SerializeField] private TMP_Text[] themedTexts = Array.Empty<TMP_Text>();
        [SerializeField] private Graphic[] panelGraphics = Array.Empty<Graphic>();
        [SerializeField] private Graphic[] accentGraphics = Array.Empty<Graphic>();

        private IGameStateSource? stateSource;
        private bool subscribed;
        private bool listenersAdded;
        private bool startRequestPending;

        public event Action MaintenanceRequested = delegate { };
        public event Action RouteRequested = delegate { };
        public event Action StageStartRequested = delegate { };
        public event Action SettingsRequested = delegate { };
        public event Action QuitRequested = delegate { };

        private void Awake()
        {
            ResolveSerializedSource();
            AddButtonListeners();
            HudThemeUtility.Apply(theme, themedTexts, panelGraphics, accentGraphics, this);
        }

        private void OnEnable()
        {
            ResolveSerializedSource();
            AddButtonListeners();
            if (stateSource != null)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            RemoveButtonListeners();
        }

        public void Bind(IGameStateSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Unsubscribe();
            stateSource = source;
            startRequestPending = false;

            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void ResolveSerializedSource()
        {
            if (stateSource != null || stateSourceBehaviour == null)
            {
                return;
            }

            stateSource = stateSourceBehaviour as IGameStateSource;
            if (stateSource == null)
            {
                Debug.LogError("[UI-02] Launcher state source must implement IGameStateSource.", this);
            }
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (stateSource == null)
            {
                Debug.LogError("[UI-02] Launcher state source is missing.", this);
                return;
            }

            if (fundsText == null || destinationNameText == null ||
                destinationProgressFill == null || startButtonText == null || startButton == null)
            {
                Debug.LogError("[UI-02] Launcher HUD has one or more missing references.", this);
                return;
            }

            stateSource.EnsureInitialized();
            stateSource.StateChanged += HandleStateChanged;
            subscribed = true;
            Render(stateSource.CurrentState);
        }

        private void Unsubscribe()
        {
            if (!subscribed || stateSource == null)
            {
                return;
            }

            stateSource.StateChanged -= HandleStateChanged;
            subscribed = false;
        }

        private void AddButtonListeners()
        {
            if (listenersAdded || maintenanceButton == null || routeButton == null ||
                startButton == null || settingsButton == null)
            {
                return;
            }

            maintenanceButton.onClick.AddListener(HandleMaintenanceClicked);
            routeButton.onClick.AddListener(HandleRouteClicked);
            startButton.onClick.AddListener(HandleStartClicked);
            settingsButton.onClick.AddListener(HandleSettingsClicked);
            quitButton?.onClick.AddListener(HandleQuitClicked);
            listenersAdded = true;
        }

        private void RemoveButtonListeners()
        {
            if (!listenersAdded)
            {
                return;
            }

            maintenanceButton?.onClick.RemoveListener(HandleMaintenanceClicked);
            routeButton?.onClick.RemoveListener(HandleRouteClicked);
            startButton?.onClick.RemoveListener(HandleStartClicked);
            settingsButton?.onClick.RemoveListener(HandleSettingsClicked);
            quitButton?.onClick.RemoveListener(HandleQuitClicked);
            listenersAdded = false;
        }

        private void HandleStateChanged(GameState state)
        {
            if (!state.CanStartStage)
            {
                startRequestPending = false;
            }

            Render(state);
        }

        private void Render(GameState state)
        {
            if (fundsText != null)
            {
                fundsText.text = $"정비 자금\n{HudTextFormatter.FormatFunds(state.Funds)}";
            }

            if (destinationNameText != null)
            {
                destinationNameText.text = destinationDisplayName;
            }

            if (destinationProgressText != null)
            {
                destinationProgressText.text = HudTextFormatter.FormatProgress(
                    state.DestinationProgress,
                    state.DestinationTarget);
            }

            if (destinationProgressFill != null)
            {
                destinationProgressFill.fillAmount = state.DestinationTarget > 0
                    ? Mathf.Clamp01((float)state.DestinationProgress / state.DestinationTarget)
                    : 0f;
            }

            if (startButtonText != null)
            {
                startButtonText.text = state.CanStartStage
                    ? "쇄빙 시작"
                    : $"다음 쇄빙 {HudTextFormatter.FormatCountdown(state.RemainingSeconds)}";
            }

            if (startButton != null)
            {
                startButton.interactable = state.CanStartStage && !startRequestPending;
            }
        }

        private void HandleMaintenanceClicked() => MaintenanceRequested();

        private void HandleRouteClicked() => RouteRequested();

        private void HandleSettingsClicked() => SettingsRequested();

        private void HandleQuitClicked() => QuitRequested();

        private void HandleStartClicked()
        {
            if (startRequestPending || stateSource == null || !stateSource.CurrentState.CanStartStage)
            {
                return;
            }

            startRequestPending = true;
            if (startButton != null)
            {
                startButton.interactable = false;
            }

            StageStartRequested();
        }
    }
}
