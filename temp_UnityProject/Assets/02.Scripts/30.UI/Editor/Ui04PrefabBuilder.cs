#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.UI.Hud;
using Icebreaker.UI.Sandbox;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.UI.Editor
{
    public static class Ui04PrefabBuilder
    {
        private const string ThemePath = "Assets/04.Images/30.UI/Theme/UiTheme.asset";
        private const string PrefabFolder = "Assets/03.Prefabs/30.UI/Hud";
        private const string PrefabPath = PrefabFolder + "/UI_RewardSettlement.prefab";

        [MenuItem("ICEBREAKER/UI/Rebuild UI-04 Reward Settlement")]
        public static void Build()
        {
            EnsureAssetFolder(PrefabFolder);

            var theme = AssetDatabase.LoadAssetAtPath<UiThemeAsset>(ThemePath);
            if (theme == null)
            {
                throw new InvalidOperationException($"UI theme was not found at {ThemePath}.");
            }

            var settings = TMP_Settings.LoadDefaultSettings();
            var font = theme.CommonFont ?? (settings != null ? TMP_Settings.defaultFontAsset : null);
            if (font == null)
            {
                throw new InvalidOperationException("TMP default font is missing. Import TMP Essentials first.");
            }

            var root = CreateCanvasRoot();
            try
            {
                var presenter = root.AddComponent<RewardSettlementPresenter>();
                var feedbackLayer = CreateStretchRect("FeedbackLayer", root.transform);
                var popupTemplate = CreateText(
                    "PopupTemplate",
                    feedbackLayer,
                    0f,
                    0f,
                    280f,
                    52f,
                    "+80",
                    font,
                    28f,
                    TextAlignmentOptions.Center,
                    topLeft: false);
                popupTemplate.fontStyle = FontStyles.Bold;
                popupTemplate.color = theme.Reward;
                popupTemplate.gameObject.SetActive(false);

                var settlementRoot = CreateStretchObject("SettlementRoot", root.transform, typeof(CanvasGroup));
                var settlementCanvasGroup = settlementRoot.GetComponent<CanvasGroup>();
                var themedTexts = new List<TMP_Text>();
                var panels = new List<Graphic>();
                var accents = new List<Graphic>();

                var dim = CreateStretchImage(
                    "Dim",
                    settlementRoot.transform,
                    new Color(0.01f, 0.035f, 0.065f, 0.6f),
                    raycastTarget: false);
                var inputBlocker = CreateTransparentButton("InputBlocker", settlementRoot.transform);

                var panel = CreateTopLeftImage(
                    "SettlementPanel",
                    settlementRoot.transform,
                    160f,
                    72f,
                    640f,
                    396f,
                    theme.Panel,
                    raycastTarget: false);
                panels.Add(panel);

                var title = CreateText(
                    "TitleText",
                    panel.transform,
                    24f,
                    24f,
                    592f,
                    44f,
                    "쇄빙 작업 정산",
                    font,
                    30f,
                    TextAlignmentOptions.Center);
                title.fontStyle = FontStyles.Bold;
                themedTexts.Add(title);

                var badge = CreateTopLeftImage(
                    "DestinationBadge",
                    panel.transform,
                    424f,
                    18f,
                    192f,
                    54f,
                    theme.Success,
                    raycastTarget: false);
                var badgeLabel = CreateText(
                    "BadgeLabel",
                    badge.transform,
                    8f,
                    4f,
                    176f,
                    20f,
                    "목적지 도달",
                    font,
                    14f,
                    TextAlignmentOptions.Center);
                themedTexts.Add(badgeLabel);
                var destinationName = CreateText(
                    "DestinationNameText",
                    badge.transform,
                    8f,
                    24f,
                    176f,
                    25f,
                    "섬마을",
                    font,
                    18f,
                    TextAlignmentOptions.Center);
                destinationName.fontStyle = FontStyles.Bold;
                themedTexts.Add(destinationName);
                badge.gameObject.SetActive(false);

                var earnedFunds = CreateText(
                    "EarnedFundsText",
                    panel.transform,
                    32f,
                    82f,
                    576f,
                    86f,
                    "획득 정비 자금 +1,240",
                    font,
                    38f,
                    TextAlignmentOptions.Center);
                earnedFunds.fontStyle = FontStyles.Bold;
                earnedFunds.color = theme.Reward;

                var destroyedCount = CreateText(
                    "DestroyedCountText",
                    panel.transform,
                    80f,
                    188f,
                    480f,
                    34f,
                    "파괴한 얼음 37개",
                    font,
                    21f,
                    TextAlignmentOptions.Center);
                themedTexts.Add(destroyedCount);

                var destinationProgress = CreateText(
                    "DestinationProgressText",
                    panel.transform,
                    80f,
                    226f,
                    480f,
                    34f,
                    "목적지 진행 +37",
                    font,
                    21f,
                    TextAlignmentOptions.Center);
                themedTexts.Add(destinationProgress);

                var appliedStatus = CreateText(
                    "AppliedStatusText",
                    panel.transform,
                    80f,
                    266f,
                    480f,
                    28f,
                    "정비 자금 반영 완료",
                    font,
                    16f,
                    TextAlignmentOptions.Center);
                appliedStatus.color = theme.Success;

                var continueButton = CreateButton(
                    "ContinueButton",
                    settlementRoot.transform,
                    350f,
                    384f,
                    260f,
                    48f,
                    "항해 계속",
                    font,
                    20f,
                    theme.ActionAccent,
                    themedTexts,
                    accents);

                var autoContinue = CreateText(
                    "AutoContinueText",
                    settlementRoot.transform,
                    350f,
                    436f,
                    260f,
                    28f,
                    "4초 뒤 자동 항해",
                    font,
                    15f,
                    TextAlignmentOptions.Center);
                themedTexts.Add(autoContinue);

                ConfigurePresenter(
                    presenter,
                    null,
                    theme,
                    feedbackLayer,
                    popupTemplate,
                    settlementRoot,
                    settlementCanvasGroup,
                    earnedFunds,
                    destroyedCount,
                    destinationProgress,
                    appliedStatus,
                    badge.gameObject,
                    destinationName,
                    autoContinue,
                    continueButton,
                    inputBlocker,
                    themedTexts,
                    panels,
                    accents);
                settlementRoot.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Debug.Log("[UI-04] Reward and settlement prefab was rebuilt and validated.");
        }

        [MenuItem("ICEBREAKER/UI/Validate UI-04 Reward Settlement")]
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
                ValidateBehavior(prefab, errors);
                ProductionUiGuard.CollectErrors(prefab, errors);
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException("[UI-04] Validation failed:\n- " + string.Join("\n- ", errors));
            }

            Debug.Log("[UI-04] Validation passed: reward feedback, safe bounds, settlement lock, and one-shot navigation.");
        }

        private static void ValidateStructure(GameObject prefab, List<string> errors)
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) > 0)
            {
                errors.Add("UI_RewardSettlement contains a missing script.");
            }

            var scaler = prefab.GetComponent<CanvasScaler>();
            if (scaler == null || scaler.referenceResolution != new Vector2(960f, 540f) ||
                scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                errors.Add("UI-04 canvas must use the 960x540 reference resolution.");
            }

            ValidateRect(prefab, "SettlementRoot/SettlementPanel", 160f, 72f, 640f, 396f, errors);
            ValidateRect(prefab, "SettlementRoot/ContinueButton", 350f, 384f, 260f, 48f, errors);

            var presenter = prefab.GetComponent<RewardSettlementPresenter>();
            if (presenter == null)
            {
                errors.Add("UI-04 presenter is missing.");
                return;
            }

            var serialized = new SerializedObject(presenter);
            var requiredReferences = new[]
            {
                "theme",
                "feedbackLayer", "popupTemplate", "settlementRoot", "settlementCanvasGroup",
                "earnedFundsText", "destroyedCountText", "destinationProgressText", "appliedStatusText",
                "destinationBadge", "destinationNameText", "autoContinueText", "continueButton",
                "inputBlockerButton"
            };
            foreach (var propertyName in requiredReferences)
            {
                var property = serialized.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                {
                    errors.Add($"RewardSettlementPresenter.{propertyName} is not assigned.");
                }
            }

            foreach (var propertyName in new[]
                     {
                         "combatSourceBehaviour",
                         "progressionSourceBehaviour",
                         "stateSourceBehaviour"
                     })
            {
                if (serialized.FindProperty(propertyName)?.objectReferenceValue != null)
                {
                    errors.Add($"RewardSettlementPresenter.{propertyName} must be runtime-bound.");
                }
            }

            if (prefab.transform.Find("SettlementRoot")?.gameObject.activeSelf != false)
            {
                errors.Add("SettlementRoot must be hidden before SettlementReady.");
            }

            if (prefab.transform.Find("FeedbackLayer/PopupTemplate")?.gameObject.activeSelf != false)
            {
                errors.Add("PopupTemplate must be hidden and cloned only for live feedback.");
            }
        }

        private static void ValidateBehavior(GameObject prefab, List<string> errors)
        {
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var presenter = instance.GetComponent<RewardSettlementPresenter>();
                if (presenter == null)
                {
                    return;
                }

                var source = instance.AddComponent<Ui04RewardSettlementSampleSource>();
                presenter.Bind(source, source, source);
                source.ResetSample();
                source.ShowEdgeReward();
                presenter.AdvanceForValidation(RewardSettlementPresenter.RewardBatchSeconds + 0.01f);
                if (presenter.LastPopupText != "정비 자금 +80" ||
                    presenter.LastPopupPosition != new Vector2(792f, 90f))
                {
                    errors.Add("First reward text or safe-area clamping is incorrect.");
                }

                source.ShowEdgeReward();
                presenter.AdvanceForValidation(RewardSettlementPresenter.RewardBatchSeconds + 0.01f);
                if (presenter.LastPopupText != "+80")
                {
                    errors.Add("The first-destroy full label was shown more than once.");
                }

                source.ShowCritical();
                if (presenter.LastPopupText != "치명타!")
                {
                    errors.Add("Critical feedback was not shown.");
                }

                source.ShowThreeRewardChain();
                presenter.AdvanceForValidation(RewardSettlementPresenter.RewardBatchSeconds + 0.01f);
                if (presenter.LastPopupText != "+240" ||
                    presenter.LastPopupPosition != new Vector2(480f, 260f))
                {
                    errors.Add("Three same-chain rewards were not combined at their center.");
                }

                var continueCount = 0;
                presenter.ContinueRequested += () => continueCount++;
                source.ShowSettlement();
                if (!presenter.IsSettlementVisible || !presenter.IsInputLocked ||
                    ReadText(instance, "SettlementRoot/SettlementPanel/EarnedFundsText") != "획득 정비 자금 +1,240" ||
                    ReadText(instance, "SettlementRoot/SettlementPanel/DestroyedCountText") != "파괴한 얼음 37개" ||
                    ReadText(instance, "SettlementRoot/SettlementPanel/DestinationProgressText") != "목적지 진행 +37")
                {
                    errors.Add("SettlementSummary values were not rendered verbatim.");
                }

                presenter.AdvanceForValidation(1.19f);
                if (presenter.TryContinue())
                {
                    errors.Add("Settlement continued before the 1.2-second lock ended.");
                }

                presenter.AdvanceForValidation(0.02f);
                if (!presenter.TryContinue() || presenter.TryContinue() || continueCount != 1 ||
                    presenter.IsSettlementVisible || presenter.IsInputLocked)
                {
                    errors.Add("Manual continuation did not fire exactly once after unlock.");
                }

                source.ShowDestinationSettlement();
                if (!IsActive(instance, "SettlementRoot/SettlementPanel/DestinationBadge") ||
                    ReadText(instance, "SettlementRoot/SettlementPanel/DestinationBadge/DestinationNameText") != "섬마을")
                {
                    errors.Add("Destination settlement badge or port name is missing.");
                }

                presenter.AdvanceForValidation(RewardSettlementPresenter.AutomaticContinueSeconds);
                if (continueCount != 2 || presenter.IsSettlementVisible || presenter.IsInputLocked)
                {
                    errors.Add("Settlement did not auto-continue exactly once at four seconds.");
                }
            }
            catch (Exception exception)
            {
                errors.Add($"UI-04 behavior validation threw {exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static GameObject CreateCanvasRoot()
        {
            var root = new GameObject(
                "UI_RewardSettlement",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(960f, 540f);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 100;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
            return root;
        }

        private static RectTransform CreateStretchRect(string name, Transform parent)
        {
            return CreateStretchObject(name, parent).GetComponent<RectTransform>();
        }

        private static GameObject CreateStretchObject(string name, Transform parent, params Type[] components)
        {
            var types = new Type[components.Length + 1];
            types[0] = typeof(RectTransform);
            Array.Copy(components, 0, types, 1, components.Length);
            var gameObject = new GameObject(name, types);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect);
            return gameObject;
        }

        private static Image CreateStretchImage(string name, Transform parent, Color color, bool raycastTarget)
        {
            var gameObject = CreateStretchObject(name, parent, typeof(CanvasRenderer), typeof(Image));
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static Image CreateTopLeftImage(
            string name,
            Transform parent,
            float x,
            float y,
            float width,
            float height,
            Color color,
            bool raycastTarget)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetTopLeft(rect, x, y, width, height);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static Button CreateTransparentButton(string name, Transform parent)
        {
            var image = CreateStretchImage(name, parent, Color.clear, raycastTarget: true);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            return button;
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
            Color color,
            List<TMP_Text> texts,
            List<Graphic> graphics)
        {
            var hitArea = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rect = hitArea.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetTopLeft(rect, x, y, width, height);
            var hitImage = hitArea.GetComponent<Image>();
            hitImage.color = Color.clear;
            hitImage.raycastTarget = true;

            var visual = CreateStretchImage("Visual", hitArea.transform, color, raycastTarget: false);
            visual.rectTransform.offsetMin = new Vector2(3f, 3f);
            visual.rectTransform.offsetMax = new Vector2(-3f, -3f);
            graphics.Add(visual);

            var text = CreateStretchText("Label", visual.transform, label, font, fontSize, TextAlignmentOptions.Center);
            texts.Add(text);

            var button = hitArea.GetComponent<Button>();
            button.targetGraphic = visual;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.48f, 0.52f, 0.58f, 0.7f);
            button.colors = colors;
            return button;
        }

        private static TextMeshProUGUI CreateStretchText(
            string name,
            Transform parent,
            string value,
            TMP_FontAsset font,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            var text = CreateTextObject(name, parent, value, font, fontSize, alignment);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(4f, 3f);
            text.rectTransform.offsetMax = new Vector2(-4f, -3f);
            return text;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            float x,
            float y,
            float width,
            float height,
            string value,
            TMP_FontAsset font,
            float fontSize,
            TextAlignmentOptions alignment,
            bool topLeft = true)
        {
            var text = CreateTextObject(name, parent, value, font, fontSize, alignment);
            if (topLeft)
            {
                SetTopLeft(text.rectTransform, x, y, width, height);
            }
            else
            {
                text.rectTransform.anchorMin = Vector2.zero;
                text.rectTransform.anchorMax = Vector2.zero;
                text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                text.rectTransform.anchoredPosition = new Vector2(x, y);
                text.rectTransform.sizeDelta = new Vector2(width, height);
            }

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
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static void ConfigurePresenter(
            RewardSettlementPresenter presenter,
            UnityEngine.Object? source,
            UiThemeAsset theme,
            RectTransform feedbackLayer,
            TMP_Text popupTemplate,
            GameObject settlementRoot,
            CanvasGroup settlementCanvasGroup,
            TMP_Text earnedFunds,
            TMP_Text destroyedCount,
            TMP_Text destinationProgress,
            TMP_Text appliedStatus,
            GameObject destinationBadge,
            TMP_Text destinationName,
            TMP_Text autoContinue,
            Button continueButton,
            Button inputBlocker,
            List<TMP_Text> themedTexts,
            List<Graphic> panels,
            List<Graphic> accents)
        {
            var serialized = new SerializedObject(presenter);
            SetOptionalObject(serialized, "combatSourceBehaviour", source);
            SetOptionalObject(serialized, "progressionSourceBehaviour", source);
            SetOptionalObject(serialized, "stateSourceBehaviour", source);
            SetObject(serialized, "theme", theme);
            SetObject(serialized, "feedbackLayer", feedbackLayer);
            SetObject(serialized, "popupTemplate", popupTemplate);
            SetObject(serialized, "settlementRoot", settlementRoot);
            SetObject(serialized, "settlementCanvasGroup", settlementCanvasGroup);
            SetObject(serialized, "earnedFundsText", earnedFunds);
            SetObject(serialized, "destroyedCountText", destroyedCount);
            SetObject(serialized, "destinationProgressText", destinationProgress);
            SetObject(serialized, "appliedStatusText", appliedStatus);
            SetObject(serialized, "destinationBadge", destinationBadge);
            SetObject(serialized, "destinationNameText", destinationName);
            SetObject(serialized, "autoContinueText", autoContinue);
            SetObject(serialized, "continueButton", continueButton);
            SetObject(serialized, "inputBlockerButton", inputBlocker);
            SetArray(serialized, "themedTexts", themedTexts);
            SetArray(serialized, "panelGraphics", panels);
            SetArray(serialized, "accentGraphics", accents);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            serialized.FindProperty(propertyName).objectReferenceValue = value;
        }

        private static void SetOptionalObject(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object? value)
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

        private static void ValidateRect(
            GameObject root,
            string path,
            float x,
            float y,
            float width,
            float height,
            List<string> errors)
        {
            var rect = root.transform.Find(path) as RectTransform;
            if (rect == null || rect.anchorMin != new Vector2(0f, 1f) || rect.anchorMax != new Vector2(0f, 1f) ||
                rect.pivot != new Vector2(0f, 1f) || rect.anchoredPosition != new Vector2(x, -y) ||
                rect.sizeDelta != new Vector2(width, height))
            {
                errors.Add($"{path} does not match X {x}, Y {y}, {width}x{height}.");
            }
        }

        private static string? ReadText(GameObject root, string path)
        {
            return root.transform.Find(path)?.GetComponent<TMP_Text>()?.text;
        }

        private static bool IsActive(GameObject root, string path)
        {
            return root.transform.Find(path)?.gameObject.activeSelf == true;
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
