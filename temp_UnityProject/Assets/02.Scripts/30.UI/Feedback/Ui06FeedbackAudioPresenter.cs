#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.State;
using Icebreaker.UI.Hud;
using Icebreaker.UI.Sandbox;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.UI.Feedback
{
    public enum SupportFeedbackState
    {
        Idle,
        Charging,
        Ready,
        Firing
    }

    public sealed class Ui06FeedbackAudioPresenter : MonoBehaviour
    {
        public const int ChargeSegmentCount = 12;
        public const int MaximumDestroyVoices = 8;
        public const float DestroyVoiceWindowSeconds = 0.03f;
        public const float FeedbackLifetimeSeconds = 0.45f;
        public const float SupportFireSeconds = 0.22f;

        [Header("Data")]
        [SerializeField] private MonoBehaviour? combatSourceBehaviour;
        [SerializeField] private MonoBehaviour? progressionSourceBehaviour;
        [SerializeField] private UiThemeAsset? theme;

        [Header("Support Device")]
        [SerializeField] private Image[] chargeSegments = Array.Empty<Image>();
        [SerializeField] private Image? supportCore;
        [SerializeField] private TMP_Text? supportStateText;
        [SerializeField] private GameObject? muzzleGlow;
        [SerializeField] private GameObject? supportTrail;

        [Header("Event Feedback")]
        [SerializeField] private RectTransform? feedbackLayer;
        [SerializeField] private GameObject? feedbackCueTemplate;

        [Header("Audio")]
        [SerializeField] private AudioCueCatalog? audioCueCatalog;
        [SerializeField] private AudioSource? gameplayAudioSource;
        [SerializeField] private AudioSource? uiAudioSource;
        [SerializeField] private AudioSource? musicAudioSource;
        [SerializeField] private AudioSource? ambientAudioSource;
        [SerializeField] private bool allowProceduralFallback = true;
        [SerializeField] private Button[] uiButtons = Array.Empty<Button>();

        [Header("Imported Audio Clips")]
        [SerializeField] private AudioClip? lightBreakClip;
        [SerializeField] private AudioClip? heavyBreakClip;
        [SerializeField] private AudioClip? crackClip;
        [SerializeField] private AudioClip? crystalDestroyClip;
        [SerializeField] private AudioClip? criticalHitClip;
        [SerializeField] private AudioClip? buttonClickClip;
        [SerializeField] private AudioClip? purchaseSuccessClip;
        [SerializeField] private AudioClip? countdownClip;
        [SerializeField] private AudioClip? settlementCompleteClip;
        [SerializeField] private AudioClip? arrivalHornClip;
        [SerializeField] private AudioClip? ambientLoopClip;

        private readonly Dictionary<(long StageId, long ChainId), int> chainDestroyCounts = new();
        private readonly List<ActiveFeedback> activeFeedback = new();

        private ICombatEventSource? combatSource;
        private IProgressionEventSource? progressionSource;
        private IGameStateSource? stateSource;
        private IManagementScreenSource? managementScreenSource;
        private Ui06AudioController? audioController;
        private bool initialized;
        private bool subscribed;
        private bool buttonListenersAdded;
        private float supportStateElapsed;
        private float supportFireRemaining;

        public int CurrentCharge { get; private set; }

        public int MaximumCharge { get; private set; } = ChargeSegmentCount;

        public int LitChargeSegmentCount { get; private set; }

        public SupportFeedbackState SupportState { get; private set; } = SupportFeedbackState.Idle;

        public string LastVisualCue { get; private set; } = string.Empty;

        public string LastAudioCue => audioController?.LastAudioCue ?? string.Empty;

        public Color LastVisualColor { get; private set; }

        public Vector2 LastEffectPosition { get; private set; }

        public int SettlementSoundCount => audioController?.SettlementSoundCount ?? 0;

        public int ArrivalSoundCount => audioController?.ArrivalSoundCount ?? 0;

        public int CountdownSoundCount => audioController?.CountdownSoundCount ?? 0;

        public int ChainRushSoundCount => audioController?.ChainRushSoundCount ?? 0;

        public int PeakDestroyVoices => audioController?.PeakDestroyVoices ?? 0;

        public bool IsEventSubscriptionActive => subscribed;

        public float CurrentMasterVolume => audioController?.CurrentMasterVolume ?? AudioListener.volume;

        public bool ArePhaseLoopsPlaying => audioController?.ArePhaseLoopsPlaying ?? false;

        private void Awake() => EnsureInitialized();

        private void OnEnable()
        {
            EnsureInitialized();
            audioController?.Resume();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            audioController?.Suspend();
            ClearFeedback();
        }

        private void OnDestroy()
        {
            RemoveButtonListeners();
            audioController?.Dispose();
            audioController = null;
        }

        private void Update()
        {
            var delta = Time.unscaledDeltaTime;
            Advance(delta);
            audioController?.Tick(delta);
        }

        public void Bind(ICombatEventSource combat, IProgressionEventSource progression)
        {
            EnsureInitialized();
            Unsubscribe();
            combatSource = combat ?? throw new ArgumentNullException(nameof(combat));
            progressionSource = progression ?? throw new ArgumentNullException(nameof(progression));
            stateSource = progression as IGameStateSource ?? combat as IGameStateSource;
            managementScreenSource =
                progression as IManagementScreenSource ?? combat as IManagementScreenSource;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        public void SetMasterVolume(float value)
        {
            EnsureInitialized();
            audioController?.SetMasterVolume(value);
        }

        public void SetUiButtons(params Button[] buttons)
        {
            RemoveButtonListeners();
            uiButtons = buttons ?? throw new ArgumentNullException(nameof(buttons));
            AddButtonListeners();
        }

        public void PlayPurchaseSuccess() =>
            audioController?.PlayCue(Ui06AudioCue.Purchase, ui: true);

        public void PlayCountdown() => audioController?.PlayCountdown();

        public void AdvanceForValidation(float unscaledDeltaTime)
        {
            Advance(unscaledDeltaTime);
            audioController?.Tick(unscaledDeltaTime);
        }

        public void ApplyAudioPhaseForValidation(GamePhase phase) =>
            audioController?.ApplyPhase(phase);

        public void ApplyManagementScreenForValidation(ManagementScreen screen) =>
            audioController?.ApplyManagementScreen(screen);

        public void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            ResolveSerializedSources();
            UiAudioSettings.LoadAndApplyMasterVolume();
            audioController = new Ui06AudioController(
                gameplayAudioSource,
                uiAudioSource,
                musicAudioSource,
                ambientAudioSource,
                new Ui06AudioClipLibrary(
                    audioCueCatalog,
                    lightBreakClip,
                    heavyBreakClip,
                    crackClip,
                    crystalDestroyClip,
                    criticalHitClip,
                    buttonClickClip,
                    purchaseSuccessClip,
                    countdownClip,
                    settlementCompleteClip,
                    arrivalHornClip,
                    ambientLoopClip),
                allowProceduralFallback,
                MaximumDestroyVoices,
                DestroyVoiceWindowSeconds);
            AddButtonListeners();
            RenderSupportState(SupportFeedbackState.Idle);
            if (feedbackCueTemplate != null)
            {
                feedbackCueTemplate.SetActive(false);
            }

            initialized = true;
        }

        private void ResolveSerializedSources()
        {
            combatSource ??= combatSourceBehaviour as ICombatEventSource;
            progressionSource ??= progressionSourceBehaviour as IProgressionEventSource;
            stateSource ??= progressionSourceBehaviour as IGameStateSource;
            managementScreenSource ??= progressionSourceBehaviour as IManagementScreenSource;
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (combatSource == null || progressionSource == null)
            {
                return;
            }

            if (chargeSegments.Length != ChargeSegmentCount || supportCore == null || supportStateText == null ||
                muzzleGlow == null || supportTrail == null || feedbackLayer == null || feedbackCueTemplate == null ||
                gameplayAudioSource == null || uiAudioSource == null || ambientAudioSource == null ||
                audioController == null)
            {
                Debug.LogError("[UI-06] Feedback, support-device, or audio references are missing.", this);
                return;
            }

            combatSource.DamageApplied += HandleDamageApplied;
            combatSource.SupportChargeChanged += HandleSupportChargeChanged;
            combatSource.IceDestroyed += HandleIceDestroyed;
            progressionSource.StageStarted += HandleStageStarted;
            progressionSource.StageEnded += HandleStageEnded;
            progressionSource.SettlementReady += HandleSettlementReady;
            progressionSource.ArrivalPresentationRequested += HandleArrivalPresentationRequested;
            if (stateSource != null)
            {
                stateSource.StateChanged += HandleGameStateChanged;
                audioController.ApplyPhase(stateSource.CurrentState.Phase);
            }

            if (managementScreenSource != null)
            {
                managementScreenSource.ManagementScreenChanged += HandleManagementScreenChanged;
                audioController.ApplyManagementScreen(
                    managementScreenSource.CurrentManagementScreen);
            }

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || combatSource == null || progressionSource == null)
            {
                return;
            }

            combatSource.DamageApplied -= HandleDamageApplied;
            combatSource.SupportChargeChanged -= HandleSupportChargeChanged;
            combatSource.IceDestroyed -= HandleIceDestroyed;
            progressionSource.StageStarted -= HandleStageStarted;
            progressionSource.StageEnded -= HandleStageEnded;
            progressionSource.SettlementReady -= HandleSettlementReady;
            progressionSource.ArrivalPresentationRequested -= HandleArrivalPresentationRequested;
            if (stateSource != null)
            {
                stateSource.StateChanged -= HandleGameStateChanged;
            }

            if (managementScreenSource != null)
            {
                managementScreenSource.ManagementScreenChanged -= HandleManagementScreenChanged;
            }

            subscribed = false;
        }

        private void AddButtonListeners()
        {
            if (buttonListenersAdded)
            {
                return;
            }

            foreach (var button in uiButtons)
            {
                button?.onClick.AddListener(HandleUiButtonClicked);
            }

            buttonListenersAdded = true;
        }

        private void RemoveButtonListeners()
        {
            if (!buttonListenersAdded)
            {
                return;
            }

            foreach (var button in uiButtons)
            {
                button?.onClick.RemoveListener(HandleUiButtonClicked);
            }

            buttonListenersAdded = false;
        }

        private void HandleSupportChargeChanged(SupportChargeChangedEvent payload)
        {
            MaximumCharge = Mathf.Max(1, payload.MaxCharge);
            CurrentCharge = Mathf.Clamp(payload.CurrentCharge, 0, MaximumCharge);
            var nextState = CurrentCharge <= 0
                ? SupportFeedbackState.Idle
                : CurrentCharge >= MaximumCharge
                    ? SupportFeedbackState.Ready
                    : SupportFeedbackState.Charging;
            var becameReady = nextState == SupportFeedbackState.Ready && SupportState != SupportFeedbackState.Ready;
            RenderSupportState(nextState);
            if (becameReady)
            {
                SpawnFeedback("충전 완료", new Vector2(148f, 340f), CueColor(Ui06AudioCue.ChargeReady));
                audioController?.PlayCue(Ui06AudioCue.ChargeReady, ui: false);
            }
        }

        private void HandleDamageApplied(DamageAppliedEvent payload)
        {
            var damageText = Mathf.CeilToInt(payload.Damage).ToString();
            if (payload.EffectType == EffectType.SupportShot)
            {
                supportFireRemaining = SupportFireSeconds;
                RenderSupportState(SupportFeedbackState.Firing);
                SpawnFeedback(damageText, payload.ReferencePosition, CueColor(Ui06AudioCue.SupportFire));
                audioController?.PlayCue(Ui06AudioCue.SupportFire, ui: false);
                return;
            }

            if (payload.WasCritical)
            {
                SpawnFeedback(damageText, payload.ReferencePosition, CueColor(Ui06AudioCue.Critical));
                audioController?.PlayCue(Ui06AudioCue.Critical, ui: false);
            }
            else if (payload.EffectType is EffectType.Click or EffectType.Hold)
            {
                SpawnFeedback(damageText, payload.ReferencePosition, Color.white);
                audioController?.PlayCue(Ui06AudioCue.Hit, ui: false);
            }
            else if (payload.ChainDepth > 0 && payload.RemainingHp > 0f)
            {
                SpawnFeedback(damageText, payload.ReferencePosition, CueColor(Ui06AudioCue.Chain));
            }
        }

        private void HandleIceDestroyed(IceDestroyedEvent payload)
        {
            var useImportedClip = payload.DestroyCategory == DestroyCategory.Direct;
            var key = (payload.StageId, payload.ChainId);
            chainDestroyCounts.TryGetValue(key, out var chainCount);
            chainCount++;
            chainDestroyCounts[key] = chainCount;

            if (payload.SpecialType == SpecialIceType.Crystal)
            {
                SpawnFeedback("결정빙 파쇄", payload.ReferencePosition, CueColor(Ui06AudioCue.Crystal));
                audioController?.PlayDestroyCue(Ui06AudioCue.Crystal, payload.Tier, useImportedClip);
            }
            else if (payload.SpecialType == SpecialIceType.Crack)
            {
                SpawnFeedback("균열빙 폭발", payload.ReferencePosition, CueColor(Ui06AudioCue.Crack));
                audioController?.PlayDestroyCue(Ui06AudioCue.Crack, payload.Tier, useImportedClip);
            }
            else
            {
                audioController?.PlayDestroyCue(Ui06AudioCue.Destroy, payload.Tier, useImportedClip);
            }

            if (payload.ChainDepth > 0 || payload.DestroyCategory == DestroyCategory.Chain)
            {
                var visibleCount = Mathf.Max(2, chainCount);
                SpawnFeedback($"연쇄 x{visibleCount}", payload.ReferencePosition + Vector2.up * 52f,
                    CueColor(Ui06AudioCue.Chain));
                if (visibleCount >= 3)
                {
                    audioController?.PlayCue(Ui06AudioCue.Chain, ui: false);
                }
            }
        }

        private void HandleStageStarted(StageStarted payload)
        {
            chainDestroyCounts.Clear();
            CurrentCharge = 0;
            MaximumCharge = ChargeSegmentCount;
            RenderSupportState(SupportFeedbackState.Idle);
            audioController?.ResetStage();
        }

        private void HandleStageEnded(StageEnded payload)
        {
            CurrentCharge = 0;
            RenderSupportState(SupportFeedbackState.Idle);
        }

        private void HandleSettlementReady(SettlementReady payload) =>
            audioController?.PlaySettlement(payload.StageId);

        private void HandleArrivalPresentationRequested(ArrivalPresentationRequested payload) =>
            audioController?.PlayArrival(payload.DestinationId);

        private void HandleGameStateChanged(GameState state) =>
            audioController?.ApplyPhase(state.Phase);

        private void HandleManagementScreenChanged(ManagementScreen screen) =>
            audioController?.ApplyManagementScreen(screen);

        private void HandleUiButtonClicked() =>
            audioController?.PlayCue(Ui06AudioCue.Button, ui: true);

        private void RenderSupportState(SupportFeedbackState state)
        {
            SupportState = state;
            supportStateElapsed = 0f;
            LitChargeSegmentCount = Mathf.Clamp(
                Mathf.CeilToInt((float)CurrentCharge / Mathf.Max(1, MaximumCharge) * ChargeSegmentCount),
                0,
                ChargeSegmentCount);

            var inactive = new Color32(0x21, 0x38, 0x4B, 0xB8);
            var charging = theme?.Success ?? new Color32(0x66, 0xD3, 0xBA, 0xFF);
            var ready = theme?.Reward ?? new Color32(0xFF, 0xE0, 0xA0, 0xFF);
            for (var index = 0; index < chargeSegments.Length; index++)
            {
                if (chargeSegments[index] != null)
                {
                    chargeSegments[index].color = index < LitChargeSegmentCount ? charging : inactive;
                }
            }

            if (supportCore != null)
            {
                supportCore.color = state switch
                {
                    SupportFeedbackState.Ready => ready,
                    SupportFeedbackState.Firing => theme?.ActionAccent ?? new Color32(0xF3, 0x9A, 0x3D, 0xFF),
                    SupportFeedbackState.Charging => charging,
                    _ => inactive
                };
                supportCore.rectTransform.localScale = Vector3.one;
            }

            if (supportStateText != null)
            {
                supportStateText.text = state switch
                {
                    SupportFeedbackState.Ready => "보조 파쇄 · 발사 준비",
                    SupportFeedbackState.Firing => "보조 파쇄 · 발사",
                    SupportFeedbackState.Charging => $"보조 파쇄 · 충전 {CurrentCharge}/{MaximumCharge}",
                    _ => "보조 파쇄 · 대기"
                };
            }

            SetActive(muzzleGlow, state == SupportFeedbackState.Firing);
            SetActive(supportTrail, state == SupportFeedbackState.Firing);
        }

        private void SpawnFeedback(string label, Vector2 referencePosition, Color color)
        {
            LastVisualCue = label;
            LastVisualColor = color;
            LastEffectPosition = ClampEffectPosition(referencePosition);
            if (feedbackCueTemplate == null || feedbackLayer == null)
            {
                return;
            }

            var instance = Instantiate(feedbackCueTemplate, feedbackLayer, false);
            instance.name = "FeedbackCue_" + label;
            instance.SetActive(true);
            var rect = instance.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = LastEffectPosition;
            }

            var image = instance.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(color.r, color.g, color.b, 0.82f);
            }

            var text = instance.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
                text.color = Color.white;
            }

            var canvasGroup = instance.GetComponent<CanvasGroup>() ?? instance.AddComponent<CanvasGroup>();
            activeFeedback.Add(new ActiveFeedback(instance, canvasGroup));
        }

        private void Advance(float unscaledDeltaTime)
        {
            var delta = Mathf.Max(0f, unscaledDeltaTime);
            supportStateElapsed += delta;

            if (supportFireRemaining > 0f)
            {
                supportFireRemaining -= delta;
                if (supportFireRemaining <= 0f)
                {
                    RenderSupportState(CurrentCharge >= MaximumCharge
                        ? SupportFeedbackState.Ready
                        : CurrentCharge > 0
                            ? SupportFeedbackState.Charging
                            : SupportFeedbackState.Idle);
                }
            }
            else if (SupportState == SupportFeedbackState.Ready && supportCore != null)
            {
                var pulse = 1f + Mathf.Sin(supportStateElapsed * 12f) * 0.055f;
                supportCore.rectTransform.localScale = Vector3.one * pulse;
            }

            for (var index = activeFeedback.Count - 1; index >= 0; index--)
            {
                var feedback = activeFeedback[index];
                feedback.Age += delta;
                if (feedback.Age >= FeedbackLifetimeSeconds)
                {
                    if (feedback.Root != null)
                    {
                        DestroyImmediateSafe(feedback.Root);
                    }

                    activeFeedback.RemoveAt(index);
                    continue;
                }

                if (feedback.Root != null)
                {
                    var progress = feedback.Age / FeedbackLifetimeSeconds;
                    feedback.CanvasGroup.alpha = 1f - Mathf.Clamp01((progress - 0.45f) / 0.55f);
                    feedback.Root.transform.localScale = Vector3.one * Mathf.Lerp(0.82f, 1.08f, progress);
                }
            }
        }

        private Color CueColor(Ui06AudioCue cue)
        {
            return cue switch
            {
                Ui06AudioCue.Critical => theme?.ActionAccent ?? new Color32(0xF3, 0x9A, 0x3D, 0xFF),
                Ui06AudioCue.Crystal => theme?.Reward ?? new Color32(0xFF, 0xE0, 0xA0, 0xFF),
                Ui06AudioCue.Crack => new Color32(0xE8, 0x6A, 0x62, 0xFF),
                Ui06AudioCue.Chain => new Color32(0x5A, 0xB7, 0xE8, 0xFF),
                Ui06AudioCue.SupportFire => theme?.Success ?? new Color32(0x66, 0xD3, 0xBA, 0xFF),
                Ui06AudioCue.ChargeReady => theme?.Reward ?? new Color32(0xFF, 0xE0, 0xA0, 0xFF),
                _ => theme?.PrimaryText ?? Color.white
            };
        }

        private static Vector2 ClampEffectPosition(Vector2 position) => new(
            Mathf.Clamp(position.x, 72f, 888f),
            Mathf.Clamp(position.y, 88f, 430f));

        private void ClearFeedback()
        {
            foreach (var feedback in activeFeedback)
            {
                if (feedback.Root != null)
                {
                    DestroyImmediateSafe(feedback.Root);
                }
            }

            activeFeedback.Clear();
        }

        private static void SetActive(GameObject? target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static void DestroyImmediateSafe(UnityEngine.Object target)
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

        private sealed class ActiveFeedback
        {
            public ActiveFeedback(GameObject root, CanvasGroup canvasGroup)
            {
                Root = root;
                CanvasGroup = canvasGroup;
            }

            public GameObject Root { get; }

            public CanvasGroup CanvasGroup { get; }

            public float Age { get; set; }
        }

    }
}
