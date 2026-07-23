#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Icebreaker.Integration.Editor;
using Icebreaker.Shared.State;
using Icebreaker.UI.Hud;
using Icebreaker.UI.Sandbox;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.UI.Editor
{
    public static class Ui02PrefabBuilder
    {
        private const string ThemePath = "Assets/04.Images/30.UI/Theme/UiTheme.asset";
        private const string PrefabFolder = "Assets/03.Prefabs/30.UI/Hud";
        private const string LauncherPrefabPath = PrefabFolder + "/UI_LauncherHud.prefab";
        private const string IcebreakingPrefabPath = PrefabFolder + "/UI_IcebreakingHud.prefab";

        [MenuItem("ICEBREAKER/UI/Rebuild UI-02 HUD Prefabs")]
        public static void Build()
        {
            EnsureAssetFolder(PrefabFolder);

            var theme = AssetDatabase.LoadAssetAtPath<UiThemeAsset>(ThemePath);
            if (theme == null)
            {
                throw new InvalidOperationException($"UI-01 theme was not found at {ThemePath}.");
            }

            var tmpSettings = TMP_Settings.LoadDefaultSettings();
            var font = tmpSettings != null ? TMP_Settings.defaultFontAsset : null;
            if (font == null)
            {
                throw new InvalidOperationException("TMP default font is missing. Import TMP Essentials first.");
            }

            BuildLauncher(theme, font);
            DavinP0IceArtImporter.ApplyLauncherPrefabOnly();
            BuildIcebreaking(theme, font);
            DavinP0IceArtImporter.ApplyIcebreakingHudPrefabOnly();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Debug.Log("[UI-02] Launcher and icebreaking HUD prefabs were rebuilt and validated.");
        }

        [MenuItem("ICEBREAKER/UI/Rebuild UI-03 Combat HUD")]
        public static void BuildCombatHud()
        {
            EnsureAssetFolder(PrefabFolder);

            var theme = AssetDatabase.LoadAssetAtPath<UiThemeAsset>(ThemePath);
            if (theme == null)
            {
                throw new InvalidOperationException($"UI theme was not found at {ThemePath}.");
            }

            var tmpSettings = TMP_Settings.LoadDefaultSettings();
            var font = tmpSettings != null ? TMP_Settings.defaultFontAsset : null;
            if (font == null)
            {
                throw new InvalidOperationException("TMP default font is missing. Import TMP Essentials first.");
            }

            BuildIcebreaking(theme, font);
            DavinP0IceArtImporter.ApplyIcebreakingHudPrefabOnly();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateCombatHud();
            Debug.Log("[UI-03] Countdown and combat HUD prefab was rebuilt and validated.");
        }

        [MenuItem("ICEBREAKER/UI/Validate UI-02 HUD Prefabs")]
        public static void Validate()
        {
            var errors = new List<string>();
            var launcher = AssetDatabase.LoadAssetAtPath<GameObject>(LauncherPrefabPath);
            var icebreaking = AssetDatabase.LoadAssetAtPath<GameObject>(IcebreakingPrefabPath);

            if (launcher == null)
            {
                errors.Add($"Missing prefab: {LauncherPrefabPath}");
            }
            else
            {
                ValidateCanvas(launcher, new Vector2(800f, 72f), errors);
                ValidateRect(launcher, "HudRoot/FundsArea", 8f, 8f, 112f, 56f, errors);
                ValidateRect(launcher, "HudRoot/DestinationArea", 128f, 8f, 200f, 56f, errors);
                ValidateRect(launcher, "HudRoot/MaintenanceHitArea", 336f, 8f, 80f, 56f, errors);
                ValidateRect(launcher, "HudRoot/RouteHitArea", 424f, 8f, 96f, 56f, errors);
                ValidateRect(launcher, "HudRoot/StageStartHitArea", 528f, 8f, 208f, 56f, errors);
                ValidateRect(launcher, "HudRoot/SettingsHitArea", 744f, 12f, 48f, 48f, errors);
                ValidateButtonSeparation(launcher, new[]
                {
                    "HudRoot/MaintenanceHitArea",
                    "HudRoot/RouteHitArea",
                    "HudRoot/StageStartHitArea",
                    "HudRoot/SettingsHitArea"
                }, errors);
                ValidatePresenterReferences<LauncherHudPresenter>(launcher, new[]
                {
                    "stateSourceBehaviour",
                    "theme",
                    "fundsText",
                    "destinationNameText",
                    "destinationProgressText",
                    "destinationProgressFill",
                    "startButtonText",
                    "maintenanceButton",
                    "routeButton",
                    "startButton",
                    "settingsButton"
                }, errors);
            }

            if (icebreaking == null)
            {
                errors.Add($"Missing prefab: {IcebreakingPrefabPath}");
            }
            else
            {
                ValidateCanvas(icebreaking, new Vector2(960f, 540f), errors);
                ValidateRect(icebreaking, "HudRoot/FundsArea", 8f, 6f, 176.5f, 48f, errors);
                ValidateRect(icebreaking, "HudRoot/TimerArea", 675.5f, 6f, 131f, 48f, errors);
                ValidateRect(icebreaking, "HudRoot/SettingsHitArea", 812f, 6f, 139.5f, 48f, errors);
                ValidateRect(icebreaking, "HudRoot/CountdownText", 380f, 150f, 200f, 240f, errors);
                ValidateButtonSeparation(icebreaking, new[] { "HudRoot/SettingsHitArea" }, errors);
                ValidatePresenterReferences<IcebreakingHudPresenter>(icebreaking, new[]
                {
                    "stateSourceBehaviour",
                    "theme",
                    "fundsText",
                    "timerText",
                    "countdownText",
                    "settingsButton"
                }, errors);
            }

            ValidateFormatting(errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException("[UI-02] Validation failed:\n- " + string.Join("\n- ", errors));
            }

            Debug.Log("[UI-02] Prefab validation passed: exact bounds, separate hit areas, and no overlaps.");
        }

        [MenuItem("ICEBREAKER/UI/Validate UI-03 Combat HUD")]
        public static void ValidateCombatHud()
        {
            var errors = new List<string>();
            var icebreaking = AssetDatabase.LoadAssetAtPath<GameObject>(IcebreakingPrefabPath);

            if (icebreaking == null)
            {
                errors.Add($"Missing prefab: {IcebreakingPrefabPath}");
            }
            else
            {
                ValidateCanvas(icebreaking, new Vector2(960f, 540f), errors);
                ValidateRect(icebreaking, "HudRoot/FundsArea", 8f, 6f, 176.5f, 48f, errors);
                ValidateRect(icebreaking, "HudRoot/TimerArea", 675.5f, 6f, 131f, 48f, errors);
                ValidateRect(icebreaking, "HudRoot/SettingsHitArea", 812f, 6f, 139.5f, 48f, errors);
                ValidateRect(icebreaking, "HudRoot/CountdownText", 380f, 150f, 200f, 240f, errors);
                ValidateButtonSeparation(icebreaking, new[] { "HudRoot/SettingsHitArea" }, errors);
                ValidatePresenterReferences<IcebreakingHudPresenter>(icebreaking, new[]
                {
                    "stateSourceBehaviour",
                    "theme",
                    "fundsText",
                    "timerText",
                    "countdownText",
                    "settingsButton"
                }, errors);
                ValidateCombatHudScope(icebreaking, errors);
                ValidateCombatHudBehavior(icebreaking, errors);
            }

            ValidateFormatting(errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException("[UI-03] Validation failed:\n- " + string.Join("\n- ", errors));
            }

            Debug.Log("[UI-03] Combat HUD validation passed: countdown, funds, timer, settings, and hidden out-of-scope views.");
        }

        private static void BuildLauncher(UiThemeAsset theme, TMP_FontAsset font)
        {
            var root = CreateCanvasRoot("UI_LauncherHud", new Vector2(800f, 72f));

            try
            {
                var source = root.AddComponent<Ui02HudSampleSource>();
                ConfigureSource(source, GamePhase.Traveling, 24d, canStart: false);
                var presenter = root.AddComponent<LauncherHudPresenter>();
                var hudRoot = CreateStretchPanel("HudRoot", root.transform, theme.Background, raycastTarget: false);

                var panels = new List<Graphic>();
                var accents = new List<Graphic>();
                var texts = new List<TMP_Text>();

                var fundsArea = CreateTopLeftPanel("FundsArea", hudRoot.transform, 8f, 8f, 112f, 56f, theme.Panel);
                panels.Add(fundsArea);
                var fundsText = CreateInsetText("FundsText", fundsArea.transform, "정비 자금\n12.4K", font, 15f, TextAlignmentOptions.Center);
                texts.Add(fundsText);

                var destinationArea = CreateTopLeftPanel("DestinationArea", hudRoot.transform, 128f, 8f, 200f, 56f, theme.Panel);
                panels.Add(destinationArea);
                var destinationName = CreateTopLeftText("DestinationNameText", destinationArea.transform, 8f, 3f, 108f, 24f, "섬마을", font, 15f, TextAlignmentOptions.Left);
                destinationName.overflowMode = TextOverflowModes.Overflow;
                var destinationProgress = CreateTopLeftText("DestinationProgressText", destinationArea.transform, 112f, 3f, 80f, 24f, "37/120", font, 14f, TextAlignmentOptions.Right);
                destinationProgress.overflowMode = TextOverflowModes.Overflow;
                texts.Add(destinationName);
                texts.Add(destinationProgress);

                var progressTrack = CreateTopLeftPanel("ProgressTrack", destinationArea.transform, 8f, 34f, 184f, 12f, new Color(0.02f, 0.07f, 0.12f, 1f));
                progressTrack.raycastTarget = false;
                var progressFill = CreateStretchPanel("ProgressFill", progressTrack.transform, theme.Success, raycastTarget: false);
                progressFill.type = Image.Type.Filled;
                progressFill.fillMethod = Image.FillMethod.Horizontal;
                progressFill.fillOrigin = 0;
                progressFill.fillAmount = 37f / 120f;

                var maintenance = CreateButton("MaintenanceHitArea", hudRoot.transform, 336f, 8f, 80f, 56f, "정비", font, 17f, theme.Panel, texts, panels);
                var route = CreateButton("RouteHitArea", hudRoot.transform, 424f, 8f, 96f, 56f, "운항 현황", font, 16f, theme.Panel, texts, panels);
                var start = CreateButton("StageStartHitArea", hudRoot.transform, 528f, 8f, 208f, 56f, "쇄빙 시작", font, 17f, theme.ActionAccent, texts, accents);
                start.interactable = false;
                var settings = CreateButton("SettingsHitArea", hudRoot.transform, 744f, 12f, 48f, 48f, "설정", font, 14f, theme.Panel, texts, panels);

                ConfigurePresenter(
                    presenter,
                    source,
                    theme,
                    fundsText,
                    destinationName,
                    destinationProgress,
                    progressFill,
                    start.GetComponentInChildren<TMP_Text>(),
                    maintenance,
                    route,
                    start,
                    settings,
                    texts,
                    panels,
                    accents);

                PrefabUtility.SaveAsPrefabAsset(root, LauncherPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildIcebreaking(UiThemeAsset theme, TMP_FontAsset font)
        {
            var root = CreateCanvasRoot("UI_IcebreakingHud", new Vector2(960f, 540f));

            try
            {
                var source = root.AddComponent<Ui02HudSampleSource>();
                ConfigureSource(source, GamePhase.Playing, 42d, canStart: false);
                var presenter = root.AddComponent<IcebreakingHudPresenter>();
                var hudRoot = CreateStretchRect("HudRoot", root.transform);

                var panels = new List<Graphic>();
                var texts = new List<TMP_Text>();

                var fundsArea = CreateTopLeftPanel("FundsArea", hudRoot.transform, 8f, 6f, 176.5f, 48f, theme.Panel);
                panels.Add(fundsArea);
                var fundsText = CreateInsetText("FundsText", fundsArea.transform, "정비 자금 12.4K", font, 18f, TextAlignmentOptions.Center);
                SetTopLeft(fundsText.rectTransform, 52f, 3f, 119f, 42f);
                texts.Add(fundsText);

                var timerArea = CreateTopLeftPanel("TimerArea", hudRoot.transform, 675.5f, 6f, 131f, 48f, theme.Panel);
                panels.Add(timerArea);
                var timerText = CreateInsetText("TimerText", timerArea.transform, "00:42", font, 24f, TextAlignmentOptions.Center);
                SetTopLeft(timerText.rectTransform, 40f, 3f, 87f, 42f);
                texts.Add(timerText);

                var settings = CreateButton("SettingsHitArea", hudRoot.transform, 812f, 6f, 139.5f, 48f, "설정", font, 14f, theme.Panel, texts, panels);
                SetTopLeft(settings.GetComponentInChildren<TMP_Text>().rectTransform, 43f, 3f, 92f, 42f);

                var countdownText = CreateTopLeftText(
                    "CountdownText",
                    hudRoot.transform,
                    380f,
                    150f,
                    200f,
                    240f,
                    "3",
                    font,
                    160f,
                    TextAlignmentOptions.Center);
                countdownText.fontStyle = FontStyles.Bold;
                countdownText.gameObject.SetActive(false);
                texts.Add(countdownText);

                ConfigurePresenter(presenter, source, theme, fundsText, timerText, countdownText, settings, texts, panels);

                PrefabUtility.SaveAsPrefabAsset(root, IcebreakingPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateCanvasRoot(string name, Vector2 referenceResolution)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = referenceResolution;

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
            return root;
        }

        private static RectTransform CreateStretchRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect);
            return rect;
        }

        private static Image CreateStretchPanel(string name, Transform parent, Color color, bool raycastTarget)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static Image CreateTopLeftPanel(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetTopLeft(rect, x, y, width, height);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            float x,
            float y,
            float width,
            float height,
            string label,
            TMP_FontAsset font,
            float fontSize,
            Color visualColor,
            List<TMP_Text> texts,
            List<Graphic> themedGraphics)
        {
            var hitArea = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var hitRect = hitArea.GetComponent<RectTransform>();
            hitRect.SetParent(parent, false);
            SetTopLeft(hitRect, x, y, width, height);

            var hitImage = hitArea.GetComponent<Image>();
            hitImage.color = visualColor;
            hitImage.raycastTarget = true;

            var text = CreateInsetText(
                "Label",
                hitArea.transform,
                label,
                font,
                fontSize,
                TextAlignmentOptions.Center);
            texts.Add(text);
            themedGraphics.Add(hitImage);

            var button = hitArea.GetComponent<Button>();
            button.targetGraphic = hitImage;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.55f, 0.6f, 0.65f, 0.65f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            return button;
        }

        private static TextMeshProUGUI CreateInsetText(
            string name,
            Transform parent,
            string value,
            TMP_FontAsset font,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            var text = CreateTextObject(name, parent, value, font, fontSize, alignment);
            var rect = text.rectTransform;
            Stretch(rect);
            rect.offsetMin = new Vector2(4f, 3f);
            rect.offsetMax = new Vector2(-4f, -3f);
            return text;
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
            var text = CreateTextObject(name, parent, value, font, fontSize, alignment);
            SetTopLeft(text.rectTransform, x, y, width, height);
            return text;
        }

        private static TextMeshProUGUI CreateTextObject(
            string name,
            Transform parent,
            string value,
            TMP_FontAsset font,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var text = gameObject.GetComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(parent, false);
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static void ConfigureSource(Ui02HudSampleSource source, GamePhase phase, double remainingSeconds, bool canStart)
        {
            var serialized = new SerializedObject(source);
            serialized.FindProperty("initialPhase").enumValueIndex = (int)phase;
            serialized.FindProperty("initialRemainingSeconds").doubleValue = remainingSeconds;
            serialized.FindProperty("initialFunds").longValue = 12_400L;
            serialized.FindProperty("destinationId").stringValue = "island-village";
            serialized.FindProperty("destinationProgress").intValue = 37;
            serialized.FindProperty("destinationTarget").intValue = 120;
            serialized.FindProperty("canStartStage").boolValue = canStart;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigurePresenter(
            LauncherHudPresenter presenter,
            Ui02HudSampleSource source,
            UiThemeAsset theme,
            TMP_Text fundsText,
            TMP_Text destinationName,
            TMP_Text destinationProgress,
            Image progressFill,
            TMP_Text startText,
            Button maintenance,
            Button route,
            Button start,
            Button settings,
            List<TMP_Text> texts,
            List<Graphic> panels,
            List<Graphic> accents)
        {
            var serialized = new SerializedObject(presenter);
            SetObject(serialized, "stateSourceBehaviour", source);
            SetObject(serialized, "theme", theme);
            serialized.FindProperty("destinationDisplayName").stringValue = "섬마을";
            SetObject(serialized, "fundsText", fundsText);
            SetObject(serialized, "destinationNameText", destinationName);
            SetObject(serialized, "destinationProgressText", destinationProgress);
            SetObject(serialized, "destinationProgressFill", progressFill);
            SetObject(serialized, "startButtonText", startText);
            SetObject(serialized, "maintenanceButton", maintenance);
            SetObject(serialized, "routeButton", route);
            SetObject(serialized, "startButton", start);
            SetObject(serialized, "settingsButton", settings);
            SetArray(serialized, "themedTexts", texts);
            SetArray(serialized, "panelGraphics", panels);
            SetArray(serialized, "accentGraphics", accents);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigurePresenter(
            IcebreakingHudPresenter presenter,
            Ui02HudSampleSource source,
            UiThemeAsset theme,
            TMP_Text fundsText,
            TMP_Text timerText,
            TMP_Text countdownText,
            Button settings,
            List<TMP_Text> texts,
            List<Graphic> panels)
        {
            var serialized = new SerializedObject(presenter);
            SetObject(serialized, "stateSourceBehaviour", source);
            SetObject(serialized, "theme", theme);
            SetObject(serialized, "fundsText", fundsText);
            SetObject(serialized, "timerText", timerText);
            SetObject(serialized, "countdownText", countdownText);
            SetObject(serialized, "settingsButton", settings);
            SetArray(serialized, "themedTexts", texts);
            SetArray(serialized, "panelGraphics", panels);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            serialized.FindProperty(propertyName).objectReferenceValue = value;
        }

        private static void SetArray<T>(SerializedObject serialized, string propertyName, List<T> values)
            where T : UnityEngine.Object
        {
            var property = serialized.FindProperty(propertyName);
            property.arraySize = values.Count;
            for (var index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static void ValidateCanvas(GameObject prefab, Vector2 expectedResolution, List<string> errors)
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) > 0)
            {
                errors.Add($"{prefab.name} contains a missing script.");
            }

            var scaler = prefab.GetComponent<CanvasScaler>();
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
                scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand ||
                Vector2.Distance(scaler.referenceResolution, expectedResolution) > 0.01f)
            {
                errors.Add($"{prefab.name} has an invalid CanvasScaler configuration.");
            }
        }

        private static void ValidatePresenterReferences<T>(GameObject prefab, string[] propertyNames, List<string> errors)
            where T : MonoBehaviour
        {
            var presenter = prefab.GetComponent<T>();
            if (presenter == null)
            {
                errors.Add($"{prefab.name} is missing {typeof(T).Name}.");
                return;
            }

            var serialized = new SerializedObject(presenter);
            foreach (var propertyName in propertyNames)
            {
                var property = serialized.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                {
                    errors.Add($"{prefab.name}/{typeof(T).Name}.{propertyName} is not assigned.");
                }
            }

            var stateSource = serialized.FindProperty("stateSourceBehaviour")?.objectReferenceValue as MonoBehaviour;
            if (stateSource is not IGameStateSource)
            {
                errors.Add($"{prefab.name} state source does not implement IGameStateSource.");
            }
        }

        private static void ValidateFormatting(List<string> errors)
        {
            if (HudTextFormatter.FormatFunds(12_400L) != "12.4K" ||
                HudTextFormatter.FormatFunds(999L) != "999" ||
                HudTextFormatter.FormatCountdown(42d) != "00:42" ||
                HudTextFormatter.FormatCountdown(-1d) != "00:00" ||
                HudTextFormatter.FormatCountdownDigit(3d) != "3" ||
                HudTextFormatter.FormatCountdownDigit(2d) != "2" ||
                HudTextFormatter.FormatCountdownDigit(1d) != "1" ||
                HudTextFormatter.FormatCountdownDigit(0d) != string.Empty ||
                HudTextFormatter.FormatProgress(37, 120) != "37/120")
            {
                errors.Add("HUD state text formatting does not match the UI-02/UI-03 contract.");
            }
        }

        private static void ValidateCombatHudScope(GameObject prefab, List<string> errors)
        {
            var forbiddenPaths = new[]
            {
                "HudRoot/DestinationArea",
                "HudRoot/MaintenanceHitArea",
                "HudRoot/RouteHitArea",
                "HudRoot/FoldHitArea",
                "HudRoot/StatusPanel"
            };

            foreach (var path in forbiddenPaths)
            {
                if (prefab.transform.Find(path) != null)
                {
                    errors.Add($"{prefab.name}/{path} must be hidden from the combat HUD.");
                }
            }
        }

        private static void ValidateCombatHudBehavior(GameObject prefab, List<string> errors)
        {
            var instance = UnityEngine.Object.Instantiate(prefab);

            try
            {
                var presenter = instance.GetComponent<IcebreakingHudPresenter>();
                var source = instance.GetComponent<Ui02HudSampleSource>();
                var render = typeof(IcebreakingHudPresenter).GetMethod(
                    "Render",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var awake = typeof(IcebreakingHudPresenter).GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (presenter == null || source == null || render == null || awake == null)
                {
                    errors.Add("UI-03 behavior validation could not resolve the presenter, sample source, or render methods.");
                    return;
                }

                awake.Invoke(presenter, null);
                source.EnsureInitialized();
                RenderAndValidateCombatState(instance, presenter, source.CurrentState, "00:42", errors);

                ValidateCountdownState(instance, presenter, source, render, source.ShowCountdownThree, "3", errors);
                ValidateCountdownState(instance, presenter, source, render, source.ShowCountdownTwo, "2", errors);
                ValidateCountdownState(instance, presenter, source, render, source.ShowCountdownOne, "1", errors);

                source.ShowCombat();
                render.Invoke(presenter, new object[] { source.CurrentState });
                RenderAndValidateCombatState(instance, presenter, source.CurrentState, "01:00", errors);

                var requestCount = 0;
                presenter.SettingsRequested += () => requestCount++;
                instance.transform.Find("HudRoot/SettingsHitArea")?.GetComponent<Button>()?.onClick.Invoke();
                if (requestCount != 1)
                {
                    errors.Add($"UI-03 settings click raised {requestCount} requests instead of exactly one.");
                }
            }
            catch (Exception exception)
            {
                errors.Add($"UI-03 behavior validation threw {exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void ValidateCountdownState(
            GameObject instance,
            IcebreakingHudPresenter presenter,
            Ui02HudSampleSource source,
            MethodInfo render,
            Action showState,
            string expectedDigit,
            List<string> errors)
        {
            showState();
            render.Invoke(presenter, new object[] { source.CurrentState });

            var countdown = instance.transform.Find("HudRoot/CountdownText")?.GetComponent<TMP_Text>();
            if (countdown == null || !countdown.gameObject.activeSelf || countdown.text != expectedDigit ||
                IsActive(instance, "HudRoot/FundsArea") ||
                IsActive(instance, "HudRoot/TimerArea") ||
                IsActive(instance, "HudRoot/SettingsHitArea"))
            {
                errors.Add($"UI-03 countdown state {expectedDigit} does not show only the expected center digit.");
            }
        }

        private static void RenderAndValidateCombatState(
            GameObject instance,
            IcebreakingHudPresenter presenter,
            GameState state,
            string expectedTimer,
            List<string> errors)
        {
            var render = typeof(IcebreakingHudPresenter).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic);
            render?.Invoke(presenter, new object[] { state });

            var funds = instance.transform.Find("HudRoot/FundsArea/FundsText")?.GetComponent<TMP_Text>();
            var timer = instance.transform.Find("HudRoot/TimerArea/TimerText")?.GetComponent<TMP_Text>();
            if (!IsActive(instance, "HudRoot/FundsArea") ||
                !IsActive(instance, "HudRoot/TimerArea") ||
                !IsActive(instance, "HudRoot/SettingsHitArea") ||
                IsActive(instance, "HudRoot/CountdownText") ||
                funds?.text != "정비 자금 12.4K" ||
                timer?.text != expectedTimer)
            {
                errors.Add($"UI-03 combat state does not show only funds, {expectedTimer}, and settings.");
            }
        }

        private static bool IsActive(GameObject instance, string path)
        {
            return instance.transform.Find(path)?.gameObject.activeSelf == true;
        }

        private static void ValidateRect(GameObject prefab, string path, float x, float y, float width, float height, List<string> errors)
        {
            var transform = prefab.transform.Find(path) as RectTransform;
            if (transform == null)
            {
                errors.Add($"{prefab.name}/{path} is missing.");
                return;
            }

            var expectedPosition = new Vector2(x, -y);
            var expectedSize = new Vector2(width, height);
            if (Vector2.Distance(transform.anchoredPosition, expectedPosition) > 0.01f ||
                Vector2.Distance(transform.sizeDelta, expectedSize) > 0.01f ||
                transform.anchorMin != new Vector2(0f, 1f) ||
                transform.anchorMax != new Vector2(0f, 1f) ||
                transform.pivot != new Vector2(0f, 1f))
            {
                errors.Add($"{prefab.name}/{path} does not match X {x}, Y {y}, {width}x{height}.");
            }
        }

        private static void ValidateButtonSeparation(GameObject prefab, string[] paths, List<string> errors)
        {
            var rects = new List<Rect>();
            foreach (var path in paths)
            {
                var transform = prefab.transform.Find(path) as RectTransform;
                if (transform == null)
                {
                    continue;
                }

                if (transform.GetComponent<Button>() == null)
                {
                    errors.Add($"{prefab.name}/{path} has no Button component.");
                }

                var hitImage = transform.GetComponent<Image>();
                if (hitImage == null ||
                    !hitImage.raycastTarget ||
                    hitImage.color.a < 0.99f)
                {
                    errors.Add(
                        $"{prefab.name}/{path} must use its visible parent Image as the hit area.");
                }

                ValidateEncapsulatedButton(transform, errors);
                var x = transform.anchoredPosition.x;
                var y = -transform.anchoredPosition.y;
                rects.Add(new Rect(x, y, transform.sizeDelta.x, transform.sizeDelta.y));
            }

            for (var left = 0; left < rects.Count; left++)
            {
                for (var right = left + 1; right < rects.Count; right++)
                {
                    if (rects[left].Overlaps(rects[right]))
                    {
                        errors.Add($"{prefab.name} button hit areas {left} and {right} overlap.");
                    }
                }
            }
        }

        private static void ValidateEncapsulatedButton(Transform? hitArea, List<string> errors)
        {
            if (hitArea == null)
            {
                return;
            }

            var label = hitArea.Find("Label");
            var image = hitArea.GetComponent<Image>();
            var button = hitArea.GetComponent<Button>();
            if (label?.GetComponent<TMP_Text>() == null ||
                hitArea.Find("Visual") != null ||
                image == null ||
                button == null ||
                button.targetGraphic != image)
            {
                errors.Add(
                    $"{hitArea.name} must directly parent its Label and use its own Image as Button target.");
            }
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

        private static void EnsureAssetFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }
    }
}
