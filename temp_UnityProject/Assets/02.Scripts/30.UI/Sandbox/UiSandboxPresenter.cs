#nullable enable

using System;
using System.Globalization;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.State;
using Icebreaker.UI.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.UI.Sandbox
{
    public sealed class UiSandboxPresenter : MonoBehaviour
    {
        private const float ReferenceWidth = 960f;
        private const float ReferenceHeight = 540f;

        [Header("Data")]
        [SerializeField] private UiSandboxDataSource? dataSource;
        [SerializeField] private UiThemeAsset? theme;

        [Header("Text")]
        [SerializeField] private TMP_Text? fundsText;
        [SerializeField] private TMP_Text? timerText;
        [SerializeField] private TMP_Text? rewardText;
        [SerializeField] private TMP_Text[] themedTexts = Array.Empty<TMP_Text>();

        [Header("Graphics")]
        [SerializeField] private Graphic? backgroundGraphic;
        [SerializeField] private Graphic[] panelGraphics = Array.Empty<Graphic>();
        [SerializeField] private Graphic[] accentGraphics = Array.Empty<Graphic>();

        private bool subscribed;
        private bool missingFontWarningShown;
        private TMP_FontAsset? runtimeFallbackFont;

        private void Awake()
        {
            ApplyTheme();
        }

        private void OnEnable()
        {
            if (subscribed)
            {
                return;
            }

            if (dataSource == null)
            {
                Debug.LogError("[UI-01] UI sandbox data source is missing.", this);
                return;
            }

            if (fundsText == null || timerText == null || rewardText == null)
            {
                Debug.LogError("[UI-01] One or more UI sandbox text references are missing.", this);
                return;
            }

            dataSource.EnsureInitialized();
            dataSource.StateChanged += HandleStateChanged;
            dataSource.RewardGranted += HandleRewardGranted;
            subscribed = true;

            RenderState(dataSource.CurrentState);
            rewardText.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (!subscribed || dataSource == null)
            {
                return;
            }

            dataSource.StateChanged -= HandleStateChanged;
            dataSource.RewardGranted -= HandleRewardGranted;
            subscribed = false;
        }

        private void OnDestroy()
        {
            if (runtimeFallbackFont != null)
            {
                Destroy(runtimeFallbackFont);
            }
        }

        private void HandleStateChanged(GameState state)
        {
            RenderState(state);

            if (rewardText != null)
            {
                rewardText.gameObject.SetActive(false);
            }
        }

        private void HandleRewardGranted(RewardGrantedEvent reward)
        {
            if (rewardText == null)
            {
                return;
            }

            rewardText.text = $"+{reward.FundsGranted.ToString("N0", CultureInfo.InvariantCulture)}";
            rewardText.color = theme != null ? theme.Reward : Color.white;

            if (rewardText.rectTransform.parent is RectTransform rewardLayer)
            {
                var layerSize = rewardLayer.rect.size;
                var scaleX = layerSize.x > 0f ? layerSize.x / ReferenceWidth : 1f;
                var scaleY = layerSize.y > 0f ? layerSize.y / ReferenceHeight : 1f;
                rewardText.rectTransform.anchoredPosition = new Vector2(
                    (reward.ReferencePosition.x - ReferenceWidth * 0.5f) * scaleX,
                    (reward.ReferencePosition.y - ReferenceHeight * 0.5f) * scaleY);
            }

            rewardText.gameObject.SetActive(true);
        }

        private void RenderState(GameState state)
        {
            if (fundsText != null)
            {
                fundsText.text = $"FUNDS {HudTextFormatter.FormatFunds(state.Funds)}";
            }

            if (timerText != null)
            {
                timerText.text = HudTextFormatter.FormatCountdown(state.RemainingSeconds);
            }
        }

        private void ApplyTheme()
        {
            if (theme == null)
            {
                Debug.LogWarning("[UI-01] UI theme is not assigned; prefab colors are used.", this);
                return;
            }

            if (backgroundGraphic != null)
            {
                backgroundGraphic.color = theme.Background;
            }

            foreach (var panelGraphic in panelGraphics)
            {
                if (panelGraphic != null)
                {
                    panelGraphic.color = theme.Panel;
                }
            }

            foreach (var accentGraphic in accentGraphics)
            {
                if (accentGraphic != null)
                {
                    accentGraphic.color = theme.ActionAccent;
                }
            }

            var displayFont = GetDisplayFont();

            foreach (var text in themedTexts)
            {
                if (text == null)
                {
                    continue;
                }

                text.color = theme.PrimaryText;
                if (displayFont != null)
                {
                    text.font = displayFont;
                }
            }

            if (rewardText != null)
            {
                rewardText.color = theme.Reward;
                if (displayFont != null)
                {
                    rewardText.font = displayFont;
                }
            }

            if (theme.PrimaryFont == null && !missingFontWarningShown)
            {
                missingFontWarningShown = true;
                Debug.LogWarning(
                    "[UI-01] The shared Korean TMP font is not assigned yet; the TMP default font is used.",
                    this);
            }
        }

        private TMP_FontAsset? GetDisplayFont()
        {
            var tmpSettings = TMP_Settings.LoadDefaultSettings();
            var defaultFont = tmpSettings != null ? TMP_Settings.defaultFontAsset : null;
            return theme?.PrimaryFont
                ?? defaultFont
                ?? GetOrCreateRuntimeFallbackFont();
        }

        private TMP_FontAsset? GetOrCreateRuntimeFallbackFont()
        {
            if (runtimeFallbackFont != null)
            {
                return runtimeFallbackFont;
            }

            try
            {
                var systemFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Segoe UI", "Arial", "Liberation Sans", "Noto Sans" },
                    16);
                if (systemFont != null)
                {
                    runtimeFallbackFont = TMP_FontAsset.CreateFontAsset(systemFont);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[UI-01] Could not create a system-font TMP fallback: {exception.Message}",
                    this);
            }

            if (runtimeFallbackFont == null)
            {
                var builtInFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (builtInFont != null)
                {
                    runtimeFallbackFont = TMP_FontAsset.CreateFontAsset(builtInFont);
                }
            }

            if (runtimeFallbackFont == null)
            {
                Debug.LogError(
                    "[UI-01] No usable runtime fallback font is available. Import TMP Essentials or assign the shared font.",
                    this);
                return null;
            }

            runtimeFallbackFont.name = "UI-01 Runtime TMP Fallback";
            return runtimeFallbackFont;
        }

    }
}
