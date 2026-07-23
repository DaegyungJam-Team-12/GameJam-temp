#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.UI.Feedback;
using Icebreaker.UI.Sandbox;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.UI.Editor
{
    public static class Ui06PrefabBuilder
    {
        private const string ThemePath = "Assets/04.Images/30.UI/Theme/UiTheme.asset";
        private const string PrefabFolder = "Assets/03.Prefabs/30.UI/Feedback";
        private const string PrefabPath = PrefabFolder + "/UI_FeedbackAudio.prefab";
        private const string PreviewFolder = PrefabFolder + "/Preview";
        private const string PreviewPrefabPath = PreviewFolder + "/UI_FeedbackAudio_Preview.prefab";
        private const string AudioCueCatalogPath = "Assets/06.Sounds/AudioCueCatalog.asset";
        private const string SfxFolder = "Assets/06.Sounds/SFX";
        private const string StageMusicLoopPath = "Assets/06.Sounds/BGM/nojisuma-on_the_way_home-468318.mp3";
        private const string StageAmbienceLoopPath = "Assets/06.Sounds/BGM/wind_sea_loop.mp3";
        private const string LightBreakPath = SfxFolder + "/light_break.mp3";
        private const string HeavyBreakPath = SfxFolder + "/heavy_break.mp3";
        private const string CrackPath = SfxFolder + "/ice_cracking.mp3";
        private const string CrystalDestroyPath = SfxFolder + "/crystal_destroy.mp3";
        private const string CriticalHitPath = SfxFolder + "/critical_hit.mp3";
        private const string ButtonClickPath = SfxFolder + "/click_sound.mp3";
        private const string PurchaseSuccessPath = SfxFolder + "/purchase_success.mp3";
        private const string CountdownPath = SfxFolder + "/countdown.mp3";
        private const string SettlementCompletePath = SfxFolder + "/complete.mp3";
        private const string ArrivalHornPath = SfxFolder + "/ship_horn.mp3";

        [MenuItem("ICEBREAKER/UI/Rebuild UI-06 Feedback Audio")]
        public static void Build()
        {
            EnsureAssetFolder(PrefabFolder);
            EnsureAssetFolder(PreviewFolder);
            var theme = AssetDatabase.LoadAssetAtPath<UiThemeAsset>(ThemePath) ??
                throw new InvalidOperationException($"UI theme was not found at {ThemePath}.");
            var primaryFont = theme.PrimaryFont ??
                throw new InvalidOperationException("UI primary font is not assigned.");
            var feedbackFont = theme.CombatFeedbackFont ??
                throw new InvalidOperationException("UI combat feedback font is not assigned.");
            var catalog = BuildAudioCueCatalog();

            var root = CreateCanvasRoot();
            try
            {
                var source = root.AddComponent<Ui06FeedbackSampleSource>();
                var presenter = root.AddComponent<Ui06FeedbackAudioPresenter>();
                var gameplayAudio = root.AddComponent<AudioSource>();
                var uiAudio = root.AddComponent<AudioSource>();
                ConfigureAudioSource(gameplayAudio, 0.86f);
                ConfigureAudioSource(uiAudio, 0.68f);
                var musicRoot = new GameObject("MusicAudio", typeof(AudioSource));
                musicRoot.transform.SetParent(root.transform, false);
                var musicAudio = musicRoot.GetComponent<AudioSource>();
                ConfigureLoopAudioSource(musicAudio, 0.316f);
                var ambientRoot = new GameObject("AmbientAudio", typeof(AudioSource));
                ambientRoot.transform.SetParent(root.transform, false);
                var ambientAudio = ambientRoot.GetComponent<AudioSource>();
                ConfigureLoopAudioSource(ambientAudio, 0.22f);

                var feedbackLayer = CreateStretchRect("FeedbackLayer", root.transform);
                var cueTemplate = CreateCueTemplate(feedbackLayer, feedbackFont, theme);
                var support = CreateSupportDevice(root.transform, primaryFont, theme);
                var sampleControls = CreateSampleControls(
                    root.transform,
                    primaryFont,
                    theme,
                    out var sampleButton);

                ConfigurePresenter(
                    presenter,
                    source,
                    catalog,
                    theme,
                    support,
                    feedbackLayer,
                    cueTemplate,
                    gameplayAudio,
                    uiAudio,
                    musicAudio,
                    ambientAudio,
                    sampleButton,
                    allowProceduralFallback: true);

                root.name = "UI_FeedbackAudio_Preview";
                PrefabUtility.SaveAsPrefabAsset(root, PreviewPrefabPath);

                ConfigureProductionPresenter(presenter);
                UnityEngine.Object.DestroyImmediate(sampleControls);
                UnityEngine.Object.DestroyImmediate(source);
                root.name = "UI_FeedbackAudio";
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Debug.Log("[UI-06] Production feedback/audio prefab, preview prefab, and cue catalog were rebuilt and validated.");
        }

        [MenuItem("ICEBREAKER/UI/Validate UI-06 Feedback Audio")]
        public static void Validate()
        {
            var errors = new List<string>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                errors.Add($"Missing prefab: {PrefabPath}");
            }
            else
            {
                ValidateStructure(prefab, errors);
            }

            var previewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPrefabPath);
            if (previewPrefab == null)
            {
                errors.Add($"Missing preview prefab: {PreviewPrefabPath}");
            }
            else
            {
                ValidatePreviewStructure(previewPrefab, errors);
                ValidateBehavior(previewPrefab, errors);
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException("[UI-06] Validation failed:\n- " + string.Join("\n- ", errors));
            }

            Debug.Log("[UI-06] Validation passed: production isolation, phase audio, manual charge, muted first run, voice cap, and deduplicated progression audio.");
        }

        private static void ValidateStructure(GameObject prefab, List<string> errors)
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) > 0)
            {
                errors.Add("UI_FeedbackAudio contains a missing script.");
            }

            var scaler = prefab.GetComponent<CanvasScaler>();
            if (scaler == null || scaler.referenceResolution != new Vector2(960f, 540f) ||
                scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                errors.Add("UI-06 canvas must use the 960x540 reference resolution.");
            }

            var presenter = prefab.GetComponent<Ui06FeedbackAudioPresenter>();
            var source = prefab.GetComponent<Ui06FeedbackSampleSource>();
            if (presenter == null)
            {
                errors.Add("UI-06 presenter is missing.");
                return;
            }

            if (source != null || prefab.GetComponentInChildren<Ui06FeedbackSampleSource>(true) != null)
            {
                errors.Add("Production UI_FeedbackAudio must not contain a sample source.");
            }

            var serialized = new SerializedObject(presenter);
            var requiredReferences = new[]
            {
                "audioCueCatalog", "theme", "supportCore", "supportStateText",
                "muzzleGlow", "supportTrail", "feedbackLayer", "feedbackCueTemplate", "gameplayAudioSource",
                "uiAudioSource", "musicAudioSource", "ambientAudioSource", "lightBreakClip", "heavyBreakClip", "crackClip",
                "crystalDestroyClip", "criticalHitClip", "buttonClickClip", "purchaseSuccessClip",
                "countdownClip", "settlementCompleteClip", "arrivalHornClip", "ambientLoopClip"
            };
            foreach (var propertyName in requiredReferences)
            {
                var property = serialized.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                {
                    errors.Add($"Ui06FeedbackAudioPresenter.{propertyName} is not assigned.");
                }
            }

            var segments = serialized.FindProperty("chargeSegments");
            if (segments == null || segments.arraySize != Ui06FeedbackAudioPresenter.ChargeSegmentCount)
            {
                errors.Add("The support device must contain exactly 12 charge segments.");
            }

            if (prefab.transform.Find("FeedbackLayer/FeedbackCueTemplate")?.gameObject.activeSelf != false)
            {
                errors.Add("FeedbackCueTemplate must be hidden and cloned for live events.");
            }

            if (prefab.transform.Find("SampleControls") != null)
            {
                errors.Add("Production UI_FeedbackAudio must not contain preview controls.");
            }

            var audioSources = prefab.GetComponentsInChildren<AudioSource>(true);
            if (audioSources.Length != 4)
            {
                errors.Add("UI-06 requires gameplay, UI, music, and ambience AudioSources.");
            }

            var oneShotSources = prefab.GetComponents<AudioSource>();
            if (oneShotSources.Length != 2)
            {
                errors.Add("UI-06 requires separate gameplay and UI AudioSources.");
            }
            else
            {
                foreach (var audioSource in oneShotSources)
                {
                    if (audioSource.playOnAwake || audioSource.loop || !Mathf.Approximately(audioSource.spatialBlend, 0f))
                    {
                        errors.Add("UI-06 audio must be 2D, non-looping, and disabled on awake.");
                    }
                }
            }

            var musicAudio = prefab.transform.Find("MusicAudio")?.GetComponent<AudioSource>();
            var ambientAudio = prefab.transform.Find("AmbientAudio")?.GetComponent<AudioSource>();
            if (!IsSilentLoopSource(musicAudio) || !IsSilentLoopSource(ambientAudio))
            {
                errors.Add("UI-06 music and ambience sources must be unassigned 2D loops disabled on awake.");
            }

            var fallbackProperty = serialized.FindProperty("allowProceduralFallback");
            if (fallbackProperty == null || fallbackProperty.boolValue)
            {
                errors.Add("Production UI_FeedbackAudio must disable procedural fallback tones.");
            }

            if (!Mathf.Approximately(UiAudioSettings.DefaultMasterVolume, 0f))
            {
                errors.Add("The first-run master volume must default to muted.");
            }
        }

        private static void ValidatePreviewStructure(GameObject prefab, List<string> errors)
        {
            if (prefab.GetComponent<Ui06FeedbackSampleSource>() == null)
            {
                errors.Add("The UI-06 preview prefab must contain its sample source.");
            }

            if (prefab.transform.Find("SampleControls") == null)
            {
                errors.Add("The UI-06 preview prefab must contain sample controls.");
            }
        }

        private static bool IsSilentLoopSource(AudioSource? source) =>
            source != null &&
            !source.playOnAwake &&
            source.loop &&
            Mathf.Approximately(source.spatialBlend, 0f) &&
            source.clip == null;

        private static void ValidateBehavior(GameObject prefab, List<string> errors)
        {
            ValidateAudioSettings(errors);
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var presenter = instance.GetComponent<Ui06FeedbackAudioPresenter>();
                var source = instance.GetComponent<Ui06FeedbackSampleSource>();
                if (presenter == null || source == null)
                {
                    return;
                }

                presenter.Bind(source, source);
                source.ResetSample();
                presenter.AdvanceForValidation(3f);
                if (presenter.CurrentCharge != 0 || presenter.LitChargeSegmentCount != 0 ||
                    presenter.SupportState != SupportFeedbackState.Idle)
                {
                    errors.Add("Support charge changed without a valid input event.");
                }

                for (var index = 0; index < 5; index++)
                {
                    source.AddValidCharge();
                }

                if (presenter.CurrentCharge != 5 || presenter.LitChargeSegmentCount != 5 ||
                    presenter.SupportState != SupportFeedbackState.Charging)
                {
                    errors.Add("Valid-input charge events did not light the 12-segment ring correctly.");
                }

                source.CompleteCharge();
                if (presenter.SupportState != SupportFeedbackState.Ready ||
                    presenter.LastVisualCue != "충전 완료" || presenter.LastAudioCue != "ChargeReady")
                {
                    errors.Add("Charge completion feedback is missing or ambiguous.");
                }

                source.FireSupportShot();
                if (presenter.SupportState != SupportFeedbackState.Firing ||
                    presenter.LastVisualCue != "보조탄 발사" || presenter.LastAudioCue != "SupportFire")
                {
                    errors.Add("Support firing feedback was not shown.");
                }

                presenter.AdvanceForValidation(Ui06FeedbackAudioPresenter.SupportFireSeconds + 0.01f);
                if (presenter.SupportState != SupportFeedbackState.Idle)
                {
                    errors.Add("Support firing state did not return to idle after the firing animation.");
                }

                source.ShowCritical();
                var criticalColor = presenter.LastVisualColor;
                if (presenter.LastVisualCue != "치명타!")
                {
                    errors.Add("Critical feedback was not shown.");
                }

                source.ShowFiveChain();
                if (!presenter.LastVisualCue.StartsWith("연쇄 x", StringComparison.Ordinal) ||
                    presenter.LastVisualColor == criticalColor)
                {
                    errors.Add("Chain feedback is not distinct from critical feedback.");
                }

                source.ShowCrystalIce();
                if (presenter.LastVisualCue != "결정빙 파쇄" || presenter.LastAudioCue != "Crystal")
                {
                    errors.Add("Crystal-ice feedback is missing.");
                }

                source.ShowCrackIce();
                if (presenter.LastVisualCue != "균열빙 폭발" || presenter.LastAudioCue != "Crack")
                {
                    errors.Add("Crack-ice feedback is missing.");
                }

                source.ShowSettlementTwice();
                if (presenter.SettlementSoundCount != 1 || presenter.LastAudioCue != "Settlement")
                {
                    errors.Add("Settlement confirmation audio did not play exactly once for a stage.");
                }

                source.ShowTwentyDestroyBurst();
                if (presenter.PeakDestroyVoices > Ui06FeedbackAudioPresenter.MaximumDestroyVoices)
                {
                    errors.Add("The simultaneous destruction voice cap exceeded eight voices.");
                }

                var sampleButton = instance.transform.Find("SampleControls/SampleButton")?.GetComponent<Button>();
                sampleButton?.onClick.Invoke();
                if (presenter.LastAudioCue != "Button")
                {
                    errors.Add("Button audio feedback is not connected.");
                }

                presenter.PlayPurchaseSuccess();
                if (presenter.LastAudioCue != "Purchase")
                {
                    errors.Add("Purchase success audio feedback is not connected.");
                }

                presenter.PlayCountdown();
                if (presenter.LastAudioCue != "Countdown" || presenter.CountdownSoundCount != 1)
                {
                    errors.Add("Countdown audio feedback is not connected.");
                }

                var settlementCountBeforeNextStage = presenter.SettlementSoundCount;
                source.StartStage();
                if (presenter.LastAudioCue != "Countdown" || presenter.CountdownSoundCount != 1)
                {
                    errors.Add("Stage start must not replay the countdown clip.");
                }

                source.EndStage();
                source.ShowSettlementTwice();
                if (presenter.LastAudioCue != "Settlement" ||
                    presenter.SettlementSoundCount != settlementCountBeforeNextStage + 1)
                {
                    errors.Add("Stage end and settlement must play one completion cue.");
                }

                source.ShowArrival();
                if (presenter.LastAudioCue != "Arrival")
                {
                    errors.Add("Arrival horn audio feedback is not connected.");
                }
            }
            catch (Exception exception)
            {
                errors.Add($"UI-06 behavior validation threw {exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void ValidateAudioSettings(List<string> errors)
        {
            var hadSavedVolume = PlayerPrefs.HasKey(UiAudioSettings.MasterVolumePlayerPrefsKey);
            var savedVolume = hadSavedVolume
                ? PlayerPrefs.GetFloat(UiAudioSettings.MasterVolumePlayerPrefsKey)
                : 0f;
            var listenerVolume = AudioListener.volume;
            try
            {
                PlayerPrefs.DeleteKey(UiAudioSettings.MasterVolumePlayerPrefsKey);
                if (!Mathf.Approximately(UiAudioSettings.LoadAndApplyMasterVolume(), 0f) ||
                    !Mathf.Approximately(AudioListener.volume, 0f))
                {
                    errors.Add("A first-run profile did not start muted.");
                }

                UiAudioSettings.SetMasterVolume(0.37f);
                if (!UiAudioSettings.HasSavedMasterVolume ||
                    !Mathf.Approximately(UiAudioSettings.LoadAndApplyMasterVolume(), 0.37f))
                {
                    errors.Add("The changed master volume was not persisted and restored.");
                }
            }
            finally
            {
                if (hadSavedVolume)
                {
                    PlayerPrefs.SetFloat(UiAudioSettings.MasterVolumePlayerPrefsKey, savedVolume);
                }
                else
                {
                    PlayerPrefs.DeleteKey(UiAudioSettings.MasterVolumePlayerPrefsKey);
                }

                PlayerPrefs.Save();
                AudioListener.volume = listenerVolume;
            }
        }

        private static GameObject CreateCanvasRoot()
        {
            var root = new GameObject(
                "UI_FeedbackAudio",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(960f, 540f);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 90;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
            return root;
        }

        private static GameObject CreateCueTemplate(Transform parent, TMP_FontAsset font, UiThemeAsset theme)
        {
            var cue = new GameObject(
                "FeedbackCueTemplate",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup));
            var rect = cue.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(190f, 48f);
            cue.GetComponent<Image>().color = new Color(theme.Success.r, theme.Success.g, theme.Success.b, 0.82f);
            cue.GetComponent<Image>().raycastTarget = false;
            var label = CreateText("Label", cue.transform, "연쇄 x5", font, 20f, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            label.fontStyle = FontStyles.Bold;
            cue.SetActive(false);
            return cue;
        }

        private static SupportTargets CreateSupportDevice(Transform parent, TMP_FontAsset font, UiThemeAsset theme)
        {
            var panel = CreateTopLeftImage(
                "SupportDevice",
                parent,
                28f,
                354f,
                224f,
                158f,
                new Color(theme.Panel.r, theme.Panel.g, theme.Panel.b, 0.92f),
                false);
            var title = CreateTopLeftText(
                "Title",
                panel.transform,
                12f,
                8f,
                200f,
                24f,
                "선수 보조 장비",
                font,
                15f,
                TextAlignmentOptions.Center);
            title.color = theme.PrimaryText;

            var core = CreateTopLeftImage(
                "Core",
                panel.transform,
                77f,
                42f,
                70f,
                70f,
                new Color32(0x21, 0x38, 0x4B, 0xFF),
                false);
            var segments = new List<Image>();
            var center = new Vector2(112f, -77f);
            for (var index = 0; index < Ui06FeedbackAudioPresenter.ChargeSegmentCount; index++)
            {
                var angle = index * Mathf.PI * 2f / Ui06FeedbackAudioPresenter.ChargeSegmentCount;
                var segment = CreateImage(
                    $"ChargeSegment_{index + 1:00}",
                    panel.transform,
                    new Color32(0x21, 0x38, 0x4B, 0xB8),
                    false);
                var segmentRect = segment.rectTransform;
                segmentRect.anchorMin = new Vector2(0f, 1f);
                segmentRect.anchorMax = new Vector2(0f, 1f);
                segmentRect.pivot = new Vector2(0.5f, 0.5f);
                segmentRect.anchoredPosition = center + new Vector2(Mathf.Sin(angle) * 48f, Mathf.Cos(angle) * 48f);
                segmentRect.sizeDelta = new Vector2(18f, 6f);
                segmentRect.localRotation = Quaternion.Euler(0f, 0f, -index * 30f);
                segments.Add(segment);
            }

            var state = CreateTopLeftText(
                "StateText",
                panel.transform,
                8f,
                124f,
                208f,
                26f,
                "보조 파쇄 · 대기",
                font,
                14f,
                TextAlignmentOptions.Center);
            state.color = theme.Success;

            var trail = CreateTopLeftImage(
                "SupportTrail",
                panel.transform,
                140f,
                72f,
                70f,
                8f,
                new Color(theme.Success.r, theme.Success.g, theme.Success.b, 0.78f),
                false);
            var muzzle = CreateTopLeftImage(
                "MuzzleGlow",
                panel.transform,
                139f,
                63f,
                24f,
                24f,
                theme.ActionAccent,
                false);
            trail.gameObject.SetActive(false);
            muzzle.gameObject.SetActive(false);
            return new SupportTargets(core, state, muzzle.gameObject, trail.gameObject, segments);
        }

        private static GameObject CreateSampleControls(
            Transform parent,
            TMP_FontAsset font,
            UiThemeAsset theme,
            out Button sampleButton)
        {
            var root = CreateTopLeftImage(
                "SampleControls",
                parent,
                744f,
                474f,
                188f,
                48f,
                theme.Panel,
                false).gameObject;
            var label = CreateText("SampleButton", root.transform, "피드백 샘플", font, 15f, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            label.color = theme.PrimaryText;
            root.name = "SampleControls";
            label.gameObject.name = "SampleButtonLabel";

            var buttonRoot = new GameObject("SampleButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var buttonRect = buttonRoot.GetComponent<RectTransform>();
            buttonRect.SetParent(root.transform, false);
            Stretch(buttonRect);
            var image = buttonRoot.GetComponent<Image>();
            image.color = theme.ActionAccent;
            var button = buttonRoot.GetComponent<Button>();
            button.targetGraphic = image;
            label.transform.SetParent(buttonRoot.transform, false);
            Stretch(label.rectTransform);
            sampleButton = button;
            return root;
        }

        private static void ConfigurePresenter(
            Ui06FeedbackAudioPresenter presenter,
            Ui06FeedbackSampleSource source,
            AudioCueCatalog catalog,
            UiThemeAsset theme,
            SupportTargets support,
            RectTransform feedbackLayer,
            GameObject cueTemplate,
            AudioSource gameplayAudio,
            AudioSource uiAudio,
            AudioSource musicAudio,
            AudioSource ambientAudio,
            Button sampleButton,
            bool allowProceduralFallback)
        {
            var serialized = new SerializedObject(presenter);
            SetObject(serialized, "combatSourceBehaviour", source);
            SetObject(serialized, "progressionSourceBehaviour", source);
            SetObject(serialized, "audioCueCatalog", catalog);
            SetObject(serialized, "theme", theme);
            SetObjectArray(serialized, "chargeSegments", support.Segments);
            SetObject(serialized, "supportCore", support.Core);
            SetObject(serialized, "supportStateText", support.StateText);
            SetObject(serialized, "muzzleGlow", support.MuzzleGlow);
            SetObject(serialized, "supportTrail", support.Trail);
            SetObject(serialized, "feedbackLayer", feedbackLayer);
            SetObject(serialized, "feedbackCueTemplate", cueTemplate);
            SetObject(serialized, "gameplayAudioSource", gameplayAudio);
            SetObject(serialized, "uiAudioSource", uiAudio);
            SetObject(serialized, "musicAudioSource", musicAudio);
            SetObject(serialized, "ambientAudioSource", ambientAudio);
            SetObject(serialized, "lightBreakClip", LoadRequiredAudioClip(LightBreakPath));
            SetObject(serialized, "heavyBreakClip", LoadRequiredAudioClip(HeavyBreakPath));
            SetObject(serialized, "crackClip", LoadRequiredAudioClip(CrackPath));
            SetObject(serialized, "crystalDestroyClip", LoadRequiredAudioClip(CrystalDestroyPath));
            SetObject(serialized, "criticalHitClip", LoadRequiredAudioClip(CriticalHitPath));
            SetObject(serialized, "buttonClickClip", LoadRequiredAudioClip(ButtonClickPath));
            SetObject(serialized, "purchaseSuccessClip", LoadRequiredAudioClip(PurchaseSuccessPath));
            SetObject(serialized, "countdownClip", LoadRequiredAudioClip(CountdownPath));
            SetObject(serialized, "settlementCompleteClip", LoadRequiredAudioClip(SettlementCompletePath));
            SetObject(serialized, "arrivalHornClip", LoadRequiredAudioClip(ArrivalHornPath));
            SetObject(serialized, "ambientLoopClip", LoadRequiredAudioClip(StageAmbienceLoopPath));
            SetObjectArray(serialized, "uiButtons", new[] { sampleButton });
            SetBool(serialized, "allowProceduralFallback", allowProceduralFallback);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureProductionPresenter(Ui06FeedbackAudioPresenter presenter)
        {
            var serialized = new SerializedObject(presenter);
            SetObject(serialized, "combatSourceBehaviour", null);
            SetObject(serialized, "progressionSourceBehaviour", null);
            SetObjectArray<Button>(serialized, "uiButtons", Array.Empty<Button>());
            SetBool(serialized, "allowProceduralFallback", false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAudioSource(AudioSource audioSource, float volume)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = volume;
        }

        private static void ConfigureLoopAudioSource(AudioSource audioSource, float volume)
        {
            audioSource.clip = null;
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;
            audioSource.volume = volume;
        }

        private static AudioCueCatalog BuildAudioCueCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCueCatalog>(AudioCueCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AudioCueCatalog>();
                AssetDatabase.CreateAsset(catalog, AudioCueCatalogPath);
            }

            var serialized = new SerializedObject(catalog);
            SetObject(serialized, "stageMusicLoop", LoadRequiredAudioClip(StageMusicLoopPath));
            SetObject(serialized, "stageAmbienceLoop", null);
            SetObject(serialized, "hit", LoadRequiredAudioClip(LightBreakPath));
            SetObject(serialized, "tier1Destroy", LoadRequiredAudioClip(LightBreakPath));
            SetObject(serialized, "tier2Destroy", LoadRequiredAudioClip(HeavyBreakPath));
            SetObject(serialized, "tier3Destroy", LoadRequiredAudioClip(HeavyBreakPath));
            SetObject(serialized, "critical", LoadRequiredAudioClip(CriticalHitPath));
            SetObject(serialized, "crystal", LoadRequiredAudioClip(CrystalDestroyPath));
            SetObject(serialized, "crack", LoadRequiredAudioClip(CrackPath));
            SetObject(serialized, "chain", LoadRequiredAudioClip(CrackPath));
            SetObject(serialized, "chainRush", LoadRequiredAudioClip(CrystalDestroyPath));
            SetObject(serialized, "chargeReady", LoadRequiredAudioClip(CountdownPath));
            SetObject(serialized, "supportFire", LoadRequiredAudioClip(HeavyBreakPath));
            SetObject(serialized, "button", LoadRequiredAudioClip(ButtonClickPath));
            SetObject(serialized, "countdown", LoadRequiredAudioClip(CountdownPath));
            SetObject(serialized, "stageStart", LoadRequiredAudioClip(CountdownPath));
            SetObject(serialized, "stageEnd", LoadRequiredAudioClip(SettlementCompletePath));
            SetObject(serialized, "settlement", LoadRequiredAudioClip(SettlementCompletePath));
            SetObject(serialized, "purchase", LoadRequiredAudioClip(PurchaseSuccessPath));
            SetObject(serialized, "arrival", LoadRequiredAudioClip(ArrivalHornPath));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static AudioClip LoadRequiredAudioClip(string path) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>(path) ??
            throw new InvalidOperationException($"Audio clip was not found at {path}.");

        private static Image CreateTopLeftImage(
            string name,
            Transform parent,
            float x,
            float y,
            float width,
            float height,
            Color color,
            bool raycast)
        {
            var image = CreateImage(name, parent, color, raycast);
            SetTopLeft(image.rectTransform, x, y, width, height);
            return image;
        }

        private static Image CreateImage(string name, Transform parent, Color color, bool raycast)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.GetComponent<RectTransform>().SetParent(parent, false);
            var image = root.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static TextMeshProUGUI CreateTopLeftText(
            string name,
            Transform parent,
            float x,
            float y,
            float width,
            float height,
            string value,
            TMP_FontAsset font,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            var text = CreateText(name, parent, value, font, fontSize, alignment);
            SetTopLeft(text.rectTransform, x, y, width, height);
            return text;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string value,
            TMP_FontAsset font,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            root.GetComponent<RectTransform>().SetParent(parent, false);
            var text = root.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateStretchRect(string name, Transform parent)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect);
            return rect;
        }

        private static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void SetObject(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object? value)
        {
            var property = serialized.FindProperty(propertyName) ??
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found.");
            property.objectReferenceValue = value;
        }

        private static void SetBool(SerializedObject serialized, string propertyName, bool value)
        {
            var property = serialized.FindProperty(propertyName) ??
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found.");
            property.boolValue = value;
        }

        private static void SetObjectArray<T>(SerializedObject serialized, string propertyName, IReadOnlyList<T> values)
            where T : UnityEngine.Object
        {
            var property = serialized.FindProperty(propertyName) ??
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found.");
            property.arraySize = values.Count;
            for (var index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static void EnsureAssetFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private readonly struct SupportTargets
        {
            public SupportTargets(
                Image core,
                TMP_Text stateText,
                GameObject muzzleGlow,
                GameObject trail,
                IReadOnlyList<Image> segments)
            {
                Core = core;
                StateText = stateText;
                MuzzleGlow = muzzleGlow;
                Trail = trail;
                Segments = segments;
            }

            public Image Core { get; }

            public TMP_Text StateText { get; }

            public GameObject MuzzleGlow { get; }

            public GameObject Trail { get; }

            public IReadOnlyList<Image> Segments { get; }
        }
    }
}
