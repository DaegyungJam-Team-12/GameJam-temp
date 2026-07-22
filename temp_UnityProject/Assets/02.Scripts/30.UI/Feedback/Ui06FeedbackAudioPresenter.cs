#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
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
        [SerializeField] private AudioSource? gameplayAudioSource;
        [SerializeField] private AudioSource? uiAudioSource;
        [SerializeField] private Button[] uiButtons = Array.Empty<Button>();

        private readonly Dictionary<(long StageId, long ChainId), int> chainDestroyCounts = new();
        private readonly List<ActiveFeedback> activeFeedback = new();
        private readonly Dictionary<AudioCue, AudioClip> runtimeClips = new();

        private ICombatEventSource? combatSource;
        private IProgressionEventSource? progressionSource;
        private bool initialized;
        private bool subscribed;
        private bool buttonListenersAdded;
        private float supportStateElapsed;
        private float supportFireRemaining;
        private float destroyVoiceWindowAge = float.PositiveInfinity;
        private int destroyVoicesInWindow;
        private bool chainRushPlayedInWindow;
        private long lastSettlementAudioStageId = long.MinValue;

        public int CurrentCharge { get; private set; }

        public int MaximumCharge { get; private set; } = ChargeSegmentCount;

        public int LitChargeSegmentCount { get; private set; }

        public SupportFeedbackState SupportState { get; private set; } = SupportFeedbackState.Idle;

        public string LastVisualCue { get; private set; } = string.Empty;

        public string LastAudioCue { get; private set; } = string.Empty;

        public Color LastVisualColor { get; private set; }

        public Vector2 LastEffectPosition { get; private set; }

        public int SettlementSoundCount { get; private set; }

        public int PeakDestroyVoices { get; private set; }

        public bool IsEventSubscriptionActive => subscribed;

        public float CurrentMasterVolume { get; private set; }

        private void Awake() => EnsureInitialized();

        private void OnEnable()
        {
            EnsureInitialized();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ClearFeedback();
        }

        private void OnDestroy()
        {
            RemoveButtonListeners();
            foreach (var clip in runtimeClips.Values)
            {
                if (clip != null)
                {
                    Destroy(clip);
                }
            }

            runtimeClips.Clear();
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        public void Bind(ICombatEventSource combat, IProgressionEventSource progression)
        {
            EnsureInitialized();
            Unsubscribe();
            combatSource = combat ?? throw new ArgumentNullException(nameof(combat));
            progressionSource = progression ?? throw new ArgumentNullException(nameof(progression));
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        public void SetMasterVolume(float value)
        {
            UiAudioSettings.SetMasterVolume(value);
            CurrentMasterVolume = AudioListener.volume;
        }

        public void SetUiButtons(params Button[] buttons)
        {
            RemoveButtonListeners();
            uiButtons = buttons ?? throw new ArgumentNullException(nameof(buttons));
            AddButtonListeners();
        }

        public void AdvanceForValidation(float unscaledDeltaTime) => Advance(unscaledDeltaTime);

        public void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            ResolveSerializedSources();
            CurrentMasterVolume = UiAudioSettings.LoadAndApplyMasterVolume();
            ConfigureAudioSource(gameplayAudioSource);
            ConfigureAudioSource(uiAudioSource);
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
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (combatSource == null || progressionSource == null)
            {
                Debug.LogError("[UI-06] Combat and progression event sources are required.", this);
                return;
            }

            if (chargeSegments.Length != ChargeSegmentCount || supportCore == null || supportStateText == null ||
                muzzleGlow == null || supportTrail == null || feedbackLayer == null || feedbackCueTemplate == null ||
                gameplayAudioSource == null || uiAudioSource == null)
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
                SpawnFeedback("충전 완료", new Vector2(148f, 340f), CueColor(AudioCue.ChargeReady));
                PlayCue(AudioCue.ChargeReady, ui: false);
            }
        }

        private void HandleDamageApplied(DamageAppliedEvent payload)
        {
            if (payload.EffectType == EffectType.SupportShot)
            {
                supportFireRemaining = SupportFireSeconds;
                RenderSupportState(SupportFeedbackState.Firing);
                SpawnFeedback("보조탄 발사", payload.ReferencePosition, CueColor(AudioCue.SupportFire));
                PlayCue(AudioCue.SupportFire, ui: false);
                return;
            }

            if (payload.WasCritical)
            {
                SpawnFeedback("치명타!", payload.ReferencePosition, CueColor(AudioCue.Critical));
                PlayCue(AudioCue.Critical, ui: false);
            }
            else if (payload.EffectType is EffectType.Click or EffectType.Hold)
            {
                PlayCue(AudioCue.Hit, ui: false);
            }
            else if (payload.ChainDepth > 0 && payload.RemainingHp > 0f)
            {
                SpawnFeedback("연쇄 타격", payload.ReferencePosition, CueColor(AudioCue.Chain));
            }
        }

        private void HandleIceDestroyed(IceDestroyedEvent payload)
        {
            TrackDestroyVoiceWindow();
            var key = (payload.StageId, payload.ChainId);
            chainDestroyCounts.TryGetValue(key, out var chainCount);
            chainCount++;
            chainDestroyCounts[key] = chainCount;

            if (payload.SpecialType == SpecialIceType.Crystal)
            {
                SpawnFeedback("결정빙 파쇄", payload.ReferencePosition, CueColor(AudioCue.Crystal));
                PlayDestroyCue(AudioCue.Crystal);
            }
            else if (payload.SpecialType == SpecialIceType.Crack)
            {
                SpawnFeedback("균열빙 폭발", payload.ReferencePosition, CueColor(AudioCue.Crack));
                PlayDestroyCue(AudioCue.Crack);
            }
            else
            {
                PlayDestroyCue(AudioCue.Destroy);
            }

            if (payload.ChainDepth > 0 || payload.DestroyCategory == DestroyCategory.Chain)
            {
                var visibleCount = Mathf.Max(2, chainCount);
                SpawnFeedback($"연쇄 x{visibleCount}", payload.ReferencePosition + Vector2.up * 52f,
                    CueColor(AudioCue.Chain));
                if (visibleCount >= 3)
                {
                    PlayCue(AudioCue.Chain, ui: false);
                }
            }
        }

        private void HandleStageStarted(StageStarted payload)
        {
            chainDestroyCounts.Clear();
            CurrentCharge = 0;
            MaximumCharge = ChargeSegmentCount;
            RenderSupportState(SupportFeedbackState.Idle);
            PlayCue(AudioCue.StageStart, ui: true);
        }

        private void HandleStageEnded(StageEnded payload)
        {
            CurrentCharge = 0;
            RenderSupportState(SupportFeedbackState.Idle);
            PlayCue(AudioCue.StageEnd, ui: true);
        }

        private void HandleSettlementReady(SettlementReady payload)
        {
            if (payload.StageId == lastSettlementAudioStageId)
            {
                return;
            }

            lastSettlementAudioStageId = payload.StageId;
            SettlementSoundCount++;
            PlayCue(AudioCue.Settlement, ui: true);
        }

        private void HandleUiButtonClicked() => PlayCue(AudioCue.Button, ui: true);

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
            destroyVoiceWindowAge += delta;
            if (destroyVoiceWindowAge > DestroyVoiceWindowSeconds)
            {
                destroyVoicesInWindow = 0;
                chainRushPlayedInWindow = false;
            }

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

        private void TrackDestroyVoiceWindow()
        {
            if (destroyVoiceWindowAge > DestroyVoiceWindowSeconds)
            {
                destroyVoiceWindowAge = 0f;
                destroyVoicesInWindow = 0;
                chainRushPlayedInWindow = false;
            }
        }

        private void PlayDestroyCue(AudioCue cue)
        {
            if (destroyVoicesInWindow < MaximumDestroyVoices)
            {
                destroyVoicesInWindow++;
                PeakDestroyVoices = Mathf.Max(PeakDestroyVoices, destroyVoicesInWindow);
                PlayCue(cue, ui: false);
            }
            else if (!chainRushPlayedInWindow)
            {
                chainRushPlayedInWindow = true;
                PlayCue(AudioCue.ChainRush, ui: false);
            }
        }

        private void PlayCue(AudioCue cue, bool ui)
        {
            LastAudioCue = cue.ToString();
            CurrentMasterVolume = AudioListener.volume;
            if (!Application.isPlaying || CurrentMasterVolume <= 0f)
            {
                return;
            }

            EnsureRuntimeClips();
            var source = ui ? uiAudioSource : gameplayAudioSource;
            if (source != null && runtimeClips.TryGetValue(cue, out var clip) && clip != null)
            {
                source.PlayOneShot(clip);
            }
        }

        private void EnsureRuntimeClips()
        {
            if (runtimeClips.Count > 0)
            {
                return;
            }

            runtimeClips[AudioCue.Hit] = CreateTone("UI06_Hit", 720f, 0.045f, 0.11f);
            runtimeClips[AudioCue.Destroy] = CreateTone("UI06_Destroy", 330f, 0.09f, 0.15f, 0.25f);
            runtimeClips[AudioCue.Critical] = CreateTone("UI06_Critical", 1320f, 0.08f, 0.12f);
            runtimeClips[AudioCue.Crystal] = CreateTone("UI06_Crystal", 1040f, 0.14f, 0.13f, 0.5f);
            runtimeClips[AudioCue.Crack] = CreateTone("UI06_Crack", 235f, 0.16f, 0.17f, 0.35f);
            runtimeClips[AudioCue.Chain] = CreateTone("UI06_Chain", 620f, 0.12f, 0.13f, 0.4f);
            runtimeClips[AudioCue.ChainRush] = CreateTone("UI06_ChainRush", 460f, 0.18f, 0.12f, 0.7f);
            runtimeClips[AudioCue.ChargeReady] = CreateTone("UI06_ChargeReady", 910f, 0.11f, 0.11f, 0.5f);
            runtimeClips[AudioCue.SupportFire] = CreateTone("UI06_SupportFire", 510f, 0.13f, 0.16f, 0.25f);
            runtimeClips[AudioCue.Button] = CreateTone("UI06_Button", 760f, 0.035f, 0.075f);
            runtimeClips[AudioCue.StageStart] = CreateTone("UI06_StageStart", 440f, 0.12f, 0.1f, 0.5f);
            runtimeClips[AudioCue.StageEnd] = CreateTone("UI06_StageEnd", 285f, 0.16f, 0.12f, 0.3f);
            runtimeClips[AudioCue.Settlement] = CreateTone("UI06_Settlement", 880f, 0.15f, 0.1f, 0.5f);
        }

        private static AudioClip CreateTone(
            string name,
            float frequency,
            float duration,
            float amplitude,
            float harmonic = 0f)
        {
            const int sampleRate = 44_100;
            var sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
            var samples = new float[sampleCount];
            for (var index = 0; index < sampleCount; index++)
            {
                var time = (float)index / sampleRate;
                var progress = (float)index / sampleCount;
                var envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress)) * (1f - progress * 0.35f);
                var fundamental = Mathf.Sin(2f * Mathf.PI * frequency * time);
                var overtone = Mathf.Sin(2f * Mathf.PI * frequency * 2.01f * time) * harmonic;
                samples[index] = (fundamental + overtone) * envelope * amplitude;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(samples, 0);
            return clip;
        }

        private static void ConfigureAudioSource(AudioSource? source)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
        }

        private Color CueColor(AudioCue cue)
        {
            return cue switch
            {
                AudioCue.Critical => theme?.ActionAccent ?? new Color32(0xF3, 0x9A, 0x3D, 0xFF),
                AudioCue.Crystal => theme?.Reward ?? new Color32(0xFF, 0xE0, 0xA0, 0xFF),
                AudioCue.Crack => new Color32(0xE8, 0x6A, 0x62, 0xFF),
                AudioCue.Chain => new Color32(0x5A, 0xB7, 0xE8, 0xFF),
                AudioCue.SupportFire => theme?.Success ?? new Color32(0x66, 0xD3, 0xBA, 0xFF),
                AudioCue.ChargeReady => theme?.Reward ?? new Color32(0xFF, 0xE0, 0xA0, 0xFF),
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

        private enum AudioCue
        {
            Hit,
            Destroy,
            Critical,
            Crystal,
            Crack,
            Chain,
            ChainRush,
            ChargeReady,
            SupportFire,
            Button,
            StageStart,
            StageEnd,
            Settlement
        }
    }
}
