#nullable enable

using System;
using System.Collections.Generic;
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
            BuildIcebreaking(theme, font);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Debug.Log("[UI-02] Launcher and icebreaking HUD prefabs were rebuilt and validated.");
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
                ValidateRect(icebreaking, "HudRoot/FundsArea", 16f, 12f, 220f, 40f, errors);
                ValidateRect(icebreaking, "HudRoot/TimerArea", 400f, 12f, 160f, 40f, errors);
                ValidateRect(icebreaking, "HudRoot/SettingsHitArea", 904f, 12f, 40f, 40f, errors);
                ValidateButtonSeparation(icebreaking, new[] { "HudRoot/SettingsHitArea" }, errors);
                ValidatePresenterReferences<IcebreakingHudPresenter>(icebreaking, new[]
                {
                    "stateSourceBehaviour",
                    "theme",
                    "fundsText",
                    "timerText",
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
                var destinationProgress = CreateTopLeftText("DestinationProgressText", destinationArea.transform, 112f, 3f, 80f, 24f, "37/120", font, 14f, TextAlignmentOptions.Right);
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
                var start = CreateButton("StageStartHitArea", hudRoot.transform, 528f, 8f, 208f, 56f, "다음 쇄빙 00:24", font, 17f, theme.ActionAccent, texts, accents);
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

                var fundsArea = CreateTopLeftPanel("FundsArea", hudRoot.transform, 16f, 12f, 220f, 40f, theme.Panel);
                panels.Add(fundsArea);
                var fundsText = CreateInsetText("FundsText", fundsArea.transform, "정비 자금 12.4K", font, 18f, TextAlignmentOptions.Center);
                texts.Add(fundsText);

                var timerArea = CreateTopLeftPanel("TimerArea", hudRoot.transform, 400f, 12f, 160f, 40f, theme.Panel);
                panels.Add(timerArea);
                var timerText = CreateInsetText("TimerText", timerArea.transform, "00:42", font, 24f, TextAlignmentOptions.Center);
                texts.Add(timerText);

                var settings = CreateButton("SettingsHitArea", hudRoot.transform, 904f, 12f, 40f, 40f, "설정", font, 12f, theme.Panel, texts, panels, visualInset: 3f);
                ConfigurePresenter(presenter, source, theme, fundsText, timerText, settings, texts, panels);

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
            List<Graphic> themedGraphics,
            float visualInset = 4f)
        {
            var hitArea = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var hitRect = hitArea.GetComponent<RectTransform>();
            hitRect.SetParent(parent, false);
            SetTopLeft(hitRect, x, y, width, height);

            var hitImage = hitArea.GetComponent<Image>();
            hitImage.color = Color.clear;
            hitImage.raycastTarget = true;

            var visual = CreateStretchPanel("Visual", hitArea.transform, visualColor, raycastTarget: false);
            var visualRect = visual.rectTransform;
            visualRect.offsetMin = new Vector2(visualInset, visualInset);
            visualRect.offsetMax = new Vector2(-visualInset, -visualInset);
            themedGraphics.Add(visual);

            var text = CreateInsetText("Label", visual.transform, label, font, fontSize, TextAlignmentOptions.Center);
            texts.Add(text);

            var button = hitArea.GetComponent<Button>();
            button.targetGraphic = visual;
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
            Button settings,
            List<TMP_Text> texts,
            List<Graphic> panels)
        {
            var serialized = new SerializedObject(presenter);
            SetObject(serialized, "stateSourceBehaviour", source);
            SetObject(serialized, "theme", theme);
            SetObject(serialized, "fundsText", fundsText);
            SetObject(serialized, "timerText", timerText);
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
                HudTextFormatter.FormatProgress(37, 120) != "37/120")
            {
                errors.Add("HUD state text formatting does not match the UI-02 contract.");
            }
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
                if (hitImage == null || !hitImage.raycastTarget || hitImage.color.a > 0.001f)
                {
                    errors.Add($"{prefab.name}/{path} must use a transparent raycast hit area.");
                }

                ValidateVisualInset(transform, errors);
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

        private static void ValidateVisualInset(Transform? hitArea, List<string> errors)
        {
            if (hitArea == null)
            {
                return;
            }

            var visual = hitArea.Find("Visual") as RectTransform;
            var hitRect = hitArea as RectTransform;
            if (visual == null || hitRect == null ||
                visual.rect.width >= hitRect.rect.width || visual.rect.height >= hitRect.rect.height ||
                visual.GetComponent<Graphic>()?.raycastTarget != false)
            {
                errors.Add($"{hitArea.name} must have a smaller, non-raycast Visual child.");
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
