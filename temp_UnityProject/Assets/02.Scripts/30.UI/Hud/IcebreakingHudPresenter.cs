#nullable enable

using System;
using Icebreaker.Shared.State;
using Icebreaker.UI.Sandbox;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.UI.Hud
{
    public sealed class IcebreakingHudPresenter : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MonoBehaviour? stateSourceBehaviour;
        [SerializeField] private UiThemeAsset? theme;

        [Header("State Text")]
        [SerializeField] private TMP_Text? fundsText;
        [SerializeField] private TMP_Text? timerText;

        [Header("Actions")]
        [SerializeField] private Button? settingsButton;

        [Header("Theme Targets")]
        [SerializeField] private TMP_Text[] themedTexts = Array.Empty<TMP_Text>();
        [SerializeField] private Graphic[] panelGraphics = Array.Empty<Graphic>();

        private IGameStateSource? stateSource;
        private bool subscribed;
        private bool listenerAdded;

        public event Action SettingsRequested = delegate { };

        private void Awake()
        {
            ResolveSerializedSource();
            AddButtonListener();
            HudThemeUtility.Apply(theme, themedTexts, panelGraphics, Array.Empty<Graphic>(), this);
        }

        private void OnEnable()
        {
            ResolveSerializedSource();
            AddButtonListener();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (listenerAdded)
            {
                settingsButton?.onClick.RemoveListener(HandleSettingsClicked);
            }
        }

        public void Bind(IGameStateSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Unsubscribe();
            stateSource = source;
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
                Debug.LogError("[UI-02] Icebreaking state source must implement IGameStateSource.", this);
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
                Debug.LogError("[UI-02] Icebreaking state source is missing.", this);
                return;
            }

            if (fundsText == null || timerText == null || settingsButton == null)
            {
                Debug.LogError("[UI-02] Icebreaking HUD has one or more missing references.", this);
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

        private void AddButtonListener()
        {
            if (listenerAdded || settingsButton == null)
            {
                return;
            }

            settingsButton.onClick.AddListener(HandleSettingsClicked);
            listenerAdded = true;
        }

        private void HandleStateChanged(GameState state) => Render(state);

        private void Render(GameState state)
        {
            if (fundsText != null)
            {
                fundsText.text = $"정비 자금 {HudTextFormatter.FormatFunds(state.Funds)}";
            }

            if (timerText != null)
            {
                timerText.text = HudTextFormatter.FormatCountdown(state.RemainingSeconds);
            }
        }

        private void HandleSettingsClicked() => SettingsRequested();
    }
}
