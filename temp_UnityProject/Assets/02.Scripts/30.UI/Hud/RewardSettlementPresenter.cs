#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Progression;
using Icebreaker.Shared.State;
using Icebreaker.UI.Sandbox;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Icebreaker.UI.Hud
{
    public sealed class RewardSettlementPresenter : MonoBehaviour
    {
        public const float MinimumSettlementSeconds = 1.2f;
        public const float AutomaticContinueSeconds = 4f;
        public const float RewardBatchSeconds = 0.1f;
        public const float PopupLifetimeSeconds = 0.45f;
        public const float SamplePopupLifetimeSeconds = 2.5f;

        private const float SafeLeft = 28f;
        private const float SafeRight = 932f;
        private const float SafeBottom = 64f;
        private const float SafeTop = 389f;

        [Header("Data")]
        [SerializeField] private MonoBehaviour? combatSourceBehaviour;
        [SerializeField] private MonoBehaviour? progressionSourceBehaviour;
        [SerializeField] private MonoBehaviour? stateSourceBehaviour;
        [SerializeField] private UiThemeAsset? theme;

        [Header("Feedback")]
        [SerializeField] private RectTransform? feedbackLayer;
        [SerializeField] private TMP_Text? popupTemplate;

        [Header("Settlement")]
        [SerializeField] private GameObject? settlementRoot;
        [SerializeField] private CanvasGroup? settlementCanvasGroup;
        [SerializeField] private TMP_Text? earnedFundsText;
        [SerializeField] private TMP_Text? destroyedCountText;
        [SerializeField] private TMP_Text? destinationProgressText;
        [SerializeField] private TMP_Text? appliedStatusText;
        [SerializeField] private GameObject? destinationBadge;
        [SerializeField] private TMP_Text? destinationNameText;
        [SerializeField] private TMP_Text? autoContinueText;
        [SerializeField] private Button? continueButton;
        [SerializeField] private Button? inputBlockerButton;
        [SerializeField] private MonoBehaviour[] inputTargets = Array.Empty<MonoBehaviour>();

        [Header("Theme Targets")]
        [SerializeField] private TMP_Text[] themedTexts = Array.Empty<TMP_Text>();
        [SerializeField] private Graphic[] panelGraphics = Array.Empty<Graphic>();
        [SerializeField] private Graphic[] accentGraphics = Array.Empty<Graphic>();

        private readonly List<PendingRewardGroup> pendingRewardGroups = new();
        private readonly List<ActivePopup> activePopups = new();
        private readonly Dictionary<(long StageId, long ChainId), int> chainDestroyCounts = new();
        private readonly Dictionary<MonoBehaviour, bool> inputTargetStates = new();

        private ICombatEventSource? combatSource;
        private IProgressionEventSource? progressionSource;
        private IGameStateSource? stateSource;
        private bool subscribed;
        private bool listenersAdded;
        private bool firstDestroyHintConsumed;
        private bool settlementVisible;
        private bool continueRequested;
        private bool inputLocked;
        private float settlementElapsed;
        private long lastSettlementStageId = long.MinValue;

        public event Action ContinueRequested = delegate { };

        public event Action<bool> InputLockChanged = delegate { };

        public bool IsSettlementVisible => settlementVisible;

        public bool IsInputLocked => inputLocked;

        /// <summary>True only while the runtime event path is bound and receiving events.</summary>
        public bool IsEventSubscriptionActive => subscribed;

        public float SettlementElapsed => settlementElapsed;

        public string LastPopupText { get; private set; } = string.Empty;

        public Vector2 LastPopupPosition { get; private set; }

        private void Awake()
        {
            ResolveSerializedSources();
            AddButtonListeners();
            ApplyTheme();
            SetSettlementVisible(false);
        }

        private void OnEnable()
        {
            ResolveSerializedSources();
            AddButtonListeners();
            if (combatSource != null && progressionSource != null && stateSource != null)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            ClearFeedback();
            SetInputLocked(false);
        }

        private void OnDestroy()
        {
            if (!listenersAdded)
            {
                return;
            }

            continueButton?.onClick.RemoveListener(HandleContinueButton);
            inputBlockerButton?.onClick.RemoveListener(HandleScreenClick);
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);

            if (!settlementVisible || settlementElapsed < MinimumSettlementSeconds)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame ||
                 keyboard.numpadEnterKey.wasPressedThisFrame ||
                 keyboard.spaceKey.wasPressedThisFrame))
            {
                TryContinue();
            }
        }

        public void Bind(
            ICombatEventSource combat,
            IProgressionEventSource progression,
            IGameStateSource state)
        {
            if (combat == null)
            {
                throw new ArgumentNullException(nameof(combat));
            }

            if (progression == null)
            {
                throw new ArgumentNullException(nameof(progression));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            Unsubscribe();
            combatSource = combat;
            progressionSource = progression;
            stateSource = state;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        public void SetInputTargets(params MonoBehaviour[] targets)
        {
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            if (inputLocked)
            {
                RestoreInputTargets();
            }

            inputTargets = targets;
            if (inputLocked)
            {
                DisableInputTargets();
            }
        }

        public bool TryContinue()
        {
            if (!settlementVisible || continueRequested || settlementElapsed < MinimumSettlementSeconds)
            {
                return false;
            }

            RequestContinue();
            return true;
        }

        public void AdvanceForValidation(float unscaledDeltaTime) => Advance(unscaledDeltaTime);

        /// <summary>Editor-only sample fallback for inspecting a reward without entering Play Mode.</summary>
        public void PreviewReward(RewardGrantedEvent payload)
        {
            var showFullName = !firstDestroyHintConsumed;
            firstDestroyHintConsumed = true;
            SpawnPopup(
                FormatReward(payload.FundsGranted, showFullName),
                payload.ReferencePosition,
                theme?.Reward ?? Color.yellow,
                SamplePopupLifetimeSeconds);
        }

        /// <summary>Context-menu preview for a combined chain reward.</summary>
        public void PreviewChainReward(long funds, Vector2 center, int chainCount)
        {
            var showFullName = !firstDestroyHintConsumed;
            firstDestroyHintConsumed = true;
            SpawnPopup(
                $"연쇄 x{Mathf.Max(2, chainCount)}",
                center + Vector2.up * 60f,
                theme?.Success ?? new Color(0.4f, 0.83f, 0.73f),
                SamplePopupLifetimeSeconds);
            SpawnPopup(
                FormatReward(funds, showFullName),
                center,
                theme?.Reward ?? Color.yellow,
                SamplePopupLifetimeSeconds);
        }

        /// <summary>Sample fallback that stays visible long enough to inspect from the context menu.</summary>
        public void PreviewDamage(DamageAppliedEvent payload)
        {
            if (payload.WasCritical)
            {
                SpawnPopup(
                    "치명타!",
                    payload.ReferencePosition,
                    theme?.ActionAccent ?? new Color(1f, 0.55f, 0.2f),
                    SamplePopupLifetimeSeconds);
            }
        }

        /// <summary>Editor-only sample fallback for inspecting chain feedback.</summary>
        public void PreviewDestroyed(IceDestroyedEvent payload) => HandleIceDestroyed(payload);

        /// <summary>Editor-only sample fallback for inspecting a settlement panel.</summary>
        public void PreviewSettlement(SettlementReady payload) => HandleSettlementReady(payload);

        public static Vector2 ClampReferencePosition(Vector2 referencePosition)
        {
            return new Vector2(
                Mathf.Clamp(referencePosition.x, SafeLeft, SafeRight),
                Mathf.Clamp(referencePosition.y, SafeBottom, SafeTop));
        }

        private void ResolveSerializedSources()
        {
            combatSource ??= combatSourceBehaviour as ICombatEventSource;
            progressionSource ??= progressionSourceBehaviour as IProgressionEventSource;
            stateSource ??= stateSourceBehaviour as IGameStateSource;
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (combatSource == null || progressionSource == null || stateSource == null)
            {
                Debug.LogError("[UI-04] Combat, progression, and state sources are required.", this);
                return;
            }

            if (feedbackLayer == null || popupTemplate == null || settlementRoot == null ||
                settlementCanvasGroup == null || earnedFundsText == null || destroyedCountText == null ||
                destinationProgressText == null || appliedStatusText == null || destinationBadge == null ||
                destinationNameText == null || autoContinueText == null || continueButton == null ||
                inputBlockerButton == null)
            {
                Debug.LogError("[UI-04] Reward or settlement view references are missing.", this);
                return;
            }

            stateSource.EnsureInitialized();
            firstDestroyHintConsumed = stateSource.CurrentState.FirstDestroyShown;
            combatSource.DamageApplied += HandleDamageApplied;
            combatSource.IceDestroyed += HandleIceDestroyed;
            progressionSource.StageStarted += HandleStageStarted;
            progressionSource.RewardGranted += HandleRewardGranted;
            progressionSource.StageEnded += HandleStageEnded;
            progressionSource.SettlementReady += HandleSettlementReady;
            stateSource.StateChanged += HandleStateChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || combatSource == null || progressionSource == null || stateSource == null)
            {
                return;
            }

            combatSource.DamageApplied -= HandleDamageApplied;
            combatSource.IceDestroyed -= HandleIceDestroyed;
            progressionSource.StageStarted -= HandleStageStarted;
            progressionSource.RewardGranted -= HandleRewardGranted;
            progressionSource.StageEnded -= HandleStageEnded;
            progressionSource.SettlementReady -= HandleSettlementReady;
            stateSource.StateChanged -= HandleStateChanged;
            subscribed = false;
        }

        private void AddButtonListeners()
        {
            if (listenersAdded || continueButton == null || inputBlockerButton == null)
            {
                return;
            }

            continueButton.onClick.AddListener(HandleContinueButton);
            inputBlockerButton.onClick.AddListener(HandleScreenClick);
            listenersAdded = true;
        }

        private void ApplyTheme()
        {
            HudThemeUtility.Apply(theme, themedTexts, panelGraphics, accentGraphics, this);
            if (popupTemplate != null && theme != null)
            {
                popupTemplate.color = theme.Reward;
            }
        }

        private void HandleStateChanged(GameState state)
        {
            if (state.FirstDestroyShown)
            {
                firstDestroyHintConsumed = true;
            }
        }

        private void HandleStageStarted(StageStarted payload)
        {
            chainDestroyCounts.Clear();
            pendingRewardGroups.Clear();
        }

        private void HandleStageEnded(StageEnded payload)
        {
            FlushAllPendingRewards();
            chainDestroyCounts.Clear();
        }

        private void HandleDamageApplied(DamageAppliedEvent payload)
        {
            if (payload.WasCritical)
            {
                SpawnPopup("치명타!", payload.ReferencePosition, theme?.ActionAccent ?? new Color(1f, 0.55f, 0.2f));
            }
        }

        private void HandleIceDestroyed(IceDestroyedEvent payload)
        {
            var key = (payload.StageId, payload.ChainId);
            chainDestroyCounts.TryGetValue(key, out var count);
            count++;
            chainDestroyCounts[key] = count;

            if (payload.ChainDepth > 0 || count >= 2)
            {
                SpawnPopup($"연쇄 x{count}", payload.ReferencePosition, theme?.Success ?? new Color(0.4f, 0.83f, 0.73f));
            }
        }

        private void HandleRewardGranted(RewardGrantedEvent payload)
        {
            var showFullName = !firstDestroyHintConsumed;
            firstDestroyHintConsumed = true;

            PendingRewardGroup? group = null;
            foreach (var candidate in pendingRewardGroups)
            {
                if (candidate.StageId == payload.StageId && candidate.ChainId == payload.ChainId &&
                    candidate.Age < RewardBatchSeconds)
                {
                    group = candidate;
                    break;
                }
            }

            if (group == null)
            {
                group = new PendingRewardGroup(payload.StageId, payload.ChainId);
                pendingRewardGroups.Add(group);
            }

            group.Rewards.Add(new PendingReward(payload.FundsGranted, payload.ReferencePosition, showFullName));
        }

        private void HandleSettlementReady(SettlementReady payload)
        {
            if (payload.StageId == lastSettlementStageId)
            {
                return;
            }

            lastSettlementStageId = payload.StageId;
            FlushAllPendingRewards();
            chainDestroyCounts.Clear();
            RenderSettlement(payload.Summary);
            settlementElapsed = 0f;
            continueRequested = false;
            SetSettlementVisible(true);
            SetInputLocked(true);
            UpdateSettlementControls();
        }

        private void RenderSettlement(SettlementSummary summary)
        {
            if (earnedFundsText != null)
            {
                earnedFundsText.text = $"획득 정비 자금 +{summary.EarnedFunds.ToString("N0", CultureInfo.InvariantCulture)}";
            }

            if (destroyedCountText != null)
            {
                destroyedCountText.text = $"파괴한 얼음 {summary.DestroyedCount.ToString("N0", CultureInfo.InvariantCulture)}개";
            }

            if (destinationProgressText != null)
            {
                destinationProgressText.text = $"목적지 진행 +{summary.DestinationProgressGain.ToString("N0", CultureInfo.InvariantCulture)}";
            }

            if (appliedStatusText != null)
            {
                appliedStatusText.text = "정비 자금 반영 완료";
            }

            SetActive(destinationBadge, summary.ReachedDestination);
            if (destinationNameText != null)
            {
                destinationNameText.text = summary.ReachedDestination
                    ? ResolveDestinationName(summary.DestinationId)
                    : string.Empty;
            }
        }

        private void Advance(float unscaledDeltaTime)
        {
            var deltaTime = Mathf.Max(0f, unscaledDeltaTime);
            AdvancePendingRewards(deltaTime);
            AdvancePopups(deltaTime);

            if (!settlementVisible || continueRequested)
            {
                return;
            }

            settlementElapsed += deltaTime;
            UpdateSettlementControls();
            if (settlementElapsed >= AutomaticContinueSeconds)
            {
                RequestContinue();
            }
        }

        private void AdvancePendingRewards(float deltaTime)
        {
            for (var index = pendingRewardGroups.Count - 1; index >= 0; index--)
            {
                var group = pendingRewardGroups[index];
                group.Age += deltaTime;
                if (group.Age < RewardBatchSeconds)
                {
                    continue;
                }

                FlushRewardGroup(group);
                pendingRewardGroups.RemoveAt(index);
            }
        }

        private void FlushAllPendingRewards()
        {
            foreach (var group in pendingRewardGroups)
            {
                FlushRewardGroup(group);
            }

            pendingRewardGroups.Clear();
        }

        private void FlushRewardGroup(PendingRewardGroup group)
        {
            if (group.Rewards.Count >= 3)
            {
                long total = 0;
                var center = Vector2.zero;
                var showFullName = false;
                foreach (var reward in group.Rewards)
                {
                    total = checked(total + reward.Funds);
                    center += reward.Position;
                    showFullName |= reward.ShowFullName;
                }

                center /= group.Rewards.Count;
                SpawnPopup(FormatReward(total, showFullName), center, theme?.Reward ?? Color.yellow);
                return;
            }

            foreach (var reward in group.Rewards)
            {
                SpawnPopup(
                    FormatReward(reward.Funds, reward.ShowFullName),
                    reward.Position,
                    theme?.Reward ?? Color.yellow);
            }
        }

        private void SpawnPopup(
            string value,
            Vector2 referencePosition,
            Color color,
            float lifetime = PopupLifetimeSeconds)
        {
            if (feedbackLayer == null || popupTemplate == null)
            {
                return;
            }

            var popup = Instantiate(popupTemplate, feedbackLayer);
            popup.name = "FeedbackPopup";
            popup.text = value;
            popup.color = color;
            popup.enabled = true;
            popup.gameObject.SetActive(true);

            var rect = popup.rectTransform;
            var position = ClampPopupPosition(referencePosition, rect);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchoredPosition = position;
            rect.SetAsLastSibling();
            popup.canvasRenderer.SetAlpha(1f);
            popup.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();

            LastPopupText = value;
            LastPopupPosition = position;
            activePopups.Add(new ActivePopup(popup, color, position, Mathf.Max(0.01f, lifetime)));
        }

        private void AdvancePopups(float deltaTime)
        {
            for (var index = activePopups.Count - 1; index >= 0; index--)
            {
                var popup = activePopups[index];
                popup.Age += deltaTime;
                if (popup.Age >= popup.Lifetime || popup.Text == null)
                {
                    if (popup.Text != null)
                    {
                        DestroyPopupObject(popup.Text.gameObject);
                    }

                    activePopups.RemoveAt(index);
                    continue;
                }

                var progress = popup.Age / popup.Lifetime;
                popup.Text.rectTransform.anchoredPosition = popup.StartPosition + Vector2.up * (18f * progress);
                var color = popup.BaseColor;
                color.a *= 1f - progress;
                popup.Text.color = color;
            }
        }

        private void UpdateSettlementControls()
        {
            if (continueButton != null)
            {
                continueButton.interactable = settlementElapsed >= MinimumSettlementSeconds && !continueRequested;
            }

            if (autoContinueText != null)
            {
                var remaining = Mathf.Max(1, Mathf.CeilToInt(AutomaticContinueSeconds - settlementElapsed));
                autoContinueText.text = $"{remaining}초 뒤 자동 항해";
            }
        }

        private void HandleContinueButton() => TryContinue();

        private void HandleScreenClick() => TryContinue();

        private void RequestContinue()
        {
            if (continueRequested)
            {
                return;
            }

            continueRequested = true;
            SetSettlementVisible(false);
            SetInputLocked(false);
            ContinueRequested();
        }

        private void SetSettlementVisible(bool visible)
        {
            settlementVisible = visible;
            SetActive(settlementRoot, visible);
            if (settlementCanvasGroup != null)
            {
                settlementCanvasGroup.alpha = visible ? 1f : 0f;
                settlementCanvasGroup.interactable = visible;
                settlementCanvasGroup.blocksRaycasts = visible;
            }
        }

        private void SetInputLocked(bool locked)
        {
            if (inputLocked == locked)
            {
                return;
            }

            inputLocked = locked;
            if (locked)
            {
                DisableInputTargets();
            }
            else
            {
                RestoreInputTargets();
            }

            InputLockChanged(locked);
        }

        private void DisableInputTargets()
        {
            inputTargetStates.Clear();
            foreach (var target in inputTargets)
            {
                if (target == null)
                {
                    continue;
                }

                inputTargetStates[target] = target.enabled;
                target.enabled = false;
            }
        }

        private void RestoreInputTargets()
        {
            foreach (var pair in inputTargetStates)
            {
                if (pair.Key != null)
                {
                    pair.Key.enabled = pair.Value;
                }
            }

            inputTargetStates.Clear();
        }

        private void ClearFeedback()
        {
            pendingRewardGroups.Clear();
            chainDestroyCounts.Clear();
            foreach (var popup in activePopups)
            {
                if (popup.Text != null)
                {
                    DestroyPopupObject(popup.Text.gameObject);
                }
            }

            activePopups.Clear();
        }

        private static string FormatReward(long funds, bool showFullName)
        {
            var amount = HudTextFormatter.FormatFunds(funds);
            return showFullName ? $"정비 자금 +{amount}" : $"+{amount}";
        }

        private static string ResolveDestinationName(string? destinationId)
        {
            return destinationId switch
            {
                "island-village" => "섬마을",
                "lighthouse-port" => "등대항",
                "northern-base" => "북쪽 기지",
                null or "" => string.Empty,
                _ => destinationId
            };
        }

        private static void SetActive(GameObject? target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static void DestroyPopupObject(UnityEngine.Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static Vector2 ClampPopupPosition(Vector2 referencePosition, RectTransform popup)
        {
            var size = popup.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                size = popup.sizeDelta;
            }

            var halfWidth = Mathf.Max(0f, size.x * 0.5f);
            var halfHeight = Mathf.Max(0f, size.y * 0.5f);
            return new Vector2(
                Mathf.Clamp(referencePosition.x, SafeLeft + halfWidth, SafeRight - halfWidth),
                Mathf.Clamp(referencePosition.y, SafeBottom + halfHeight, SafeTop - halfHeight));
        }

        private sealed class PendingRewardGroup
        {
            public PendingRewardGroup(long stageId, long chainId)
            {
                StageId = stageId;
                ChainId = chainId;
            }

            public long StageId { get; }

            public long ChainId { get; }

            public float Age { get; set; }

            public List<PendingReward> Rewards { get; } = new();
        }

        private readonly struct PendingReward
        {
            public PendingReward(long funds, Vector2 position, bool showFullName)
            {
                Funds = funds;
                Position = position;
                ShowFullName = showFullName;
            }

            public long Funds { get; }

            public Vector2 Position { get; }

            public bool ShowFullName { get; }
        }

        private sealed class ActivePopup
        {
            public ActivePopup(TMP_Text text, Color baseColor, Vector2 startPosition, float lifetime)
            {
                Text = text;
                BaseColor = baseColor;
                StartPosition = startPosition;
                Lifetime = lifetime;
            }

            public TMP_Text Text { get; }

            public Color BaseColor { get; }

            public Vector2 StartPosition { get; }

            public float Lifetime { get; }

            public float Age { get; set; }
        }
    }
}
