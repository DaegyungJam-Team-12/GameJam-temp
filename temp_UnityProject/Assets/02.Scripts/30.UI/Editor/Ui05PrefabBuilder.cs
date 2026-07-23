#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Events;
using Icebreaker.UI.Management;
using Icebreaker.UI.Sandbox;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.UI.Editor
{
    public static class Ui05PrefabBuilder
    {
        private const string ThemePath = "Assets/04.Images/30.UI/Theme/UiTheme.asset";
        private const string PrefabFolder = "Assets/03.Prefabs/30.UI/Management";
        private const string PrefabPath = PrefabFolder + "/UI_ManagementViews.prefab";

        [MenuItem("ICEBREAKER/UI/Rebuild UI-05 Management Views")]
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
                BuildContents(root, theme, font);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            Validate();
            Debug.Log("[UI-05] Management views prefab was rebuilt and validated.");
        }

        [MenuItem("ICEBREAKER/UI/Validate UI-05 Management Views")]
        public static void Validate()
        {
            var errors = new List<string>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"UI-05 prefab was not found at {PrefabPath}.");
            }

            ValidateStructure(prefab, errors);
            ValidateBehaviour(prefab, errors);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException("[UI-05] Validation failed:\n- " + string.Join("\n- ", errors));
            }

            Debug.Log("[UI-05] Validation passed: route status, settings, completion, and one-shot arrivals.");
        }

        private static void BuildContents(GameObject root, UiThemeAsset theme, TMP_FontAsset font)
        {
            var background = CreateStretchImage("Background", root.transform, theme.Background, false);
            var presenter = root.AddComponent<ManagementViewsPresenter>();
            var source = root.AddComponent<Ui05ManagementSampleSource>();

            var header = CreateTopLeftImage("CommonHeader", background.transform, 16f, 12f, 928f, 48f, theme.Panel, false);
            var headerTitle = CreateTopLeftText("TitleText", header.transform, 20f, 7f, 260f, 34f, "운항 현황", font, 23f, TextAlignmentOptions.Left);
            headerTitle.fontStyle = FontStyles.Bold;
            var fundsArea = CreateTopLeftImage("FundsArea", header.transform, 484f, 4f, 152f, 40f, new Color32(0x12, 0x31, 0x49, 0xFF), false);
            var fundsText = CreateInsetText("FundsText", fundsArea.transform, "보유 자금 12.4K", font, 15f, TextAlignmentOptions.Center);
            var startButton = CreateButton("StageStartButton", header.transform, 636f, 0f, 148f, 48f, "다음 쇄빙 00:24", font, 14f, theme.ActionAccent);
            var settingsButton = CreateButton("SettingsButton", header.transform, 784f, 0f, 64f, 48f, "설정", font, 14f, theme.Panel);
            var collapseButton = CreateButton("CollapseButton", header.transform, 848f, 0f, 80f, 48f, "접기", font, 14f, theme.Panel);

            var body = CreateTopLeftImage("Body", background.transform, 16f, 72f, 928f, 452f, new Color32(0x08, 0x19, 0x2A, 0xFF), false);
            var routeRoot = CreateStretchRect("RouteRoot", body.transform).gameObject;
            var routeTargets = BuildRoute(routeRoot.transform, theme, font);
            var settingsTargets = BuildSettings(background.transform, theme, font);
            var arrivalTargets = BuildArrival(background.transform, theme, font);

            var presenterObject = new SerializedObject(presenter);
            SetObject(presenterObject, "stageStartButton", startButton);
            SetObject(presenterObject, "settingsButton", settingsButton);
            SetObject(presenterObject, "collapseButton", collapseButton);
            SetObject(presenterObject, "fundsText", fundsText);
            SetObject(presenterObject, "stageStartText", startButton.GetComponentInChildren<TMP_Text>());
            SetObject(presenterObject, "routeStatusView", routeTargets.View);
            SetObject(presenterObject, "settingsModalView", settingsTargets.View);
            SetObject(presenterObject, "arrivalOverlayView", arrivalTargets.View);
            presenterObject.ApplyModifiedPropertiesWithoutUndo();

            var sourceObject = new SerializedObject(source);
            SetObject(sourceObject, "presenter", presenter);
            sourceObject.ApplyModifiedPropertiesWithoutUndo();

            settingsTargets.Root.SetActive(false);
            arrivalTargets.Root.SetActive(false);
        }

        private static RouteTargets BuildRoute(Transform parent, UiThemeAsset theme, TMP_FontAsset font)
        {
            var view = parent.gameObject.AddComponent<RouteStatusView>();
            var title = CreateTopLeftText("TitleText", parent, 24f, 18f, 420f, 38f, "운항 현황", font, 28f, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            var info = CreateTopLeftText("ReadOnlyNotice", parent, 590f, 24f, 310f, 28f, "정보 확인 전용 · 경로 선택 없음", font, 14f, TextAlignmentOptions.Right);
            info.color = theme.Reward;

            var currentPanel = CreateTopLeftImage("CurrentDestinationPanel", parent, 24f, 74f, 548f, 334f, theme.Panel, false);
            CreateTopLeftText("SectionLabel", currentPanel.transform, 24f, 22f, 250f, 26f, "현재 목적지", font, 15f, TextAlignmentOptions.Left).color = theme.Reward;
            var destination = CreateTopLeftText("DestinationNameText", currentPanel.transform, 24f, 56f, 500f, 56f, "섬마을", font, 36f, TextAlignmentOptions.Left);
            destination.fontStyle = FontStyles.Bold;
            var progressText = CreateTopLeftText("DestinationProgressText", currentPanel.transform, 24f, 124f, 500f, 30f, "목적지 진행  37 / 120", font, 18f, TextAlignmentOptions.Left);
            var track = CreateTopLeftImage("ProgressTrack", currentPanel.transform, 24f, 164f, 500f, 22f, new Color32(0x04, 0x12, 0x20, 0xFF), false);
            var fill = CreateStretchImage("ProgressFill", track.transform, theme.Success, false);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 37f / 120f;
            var cargo = CreateTopLeftText("CargoText", currentPanel.transform, 24f, 212f, 500f, 74f, "운송 화물\n식료품 · 우편", font, 20f, TextAlignmentOptions.TopLeft);
            var completedBadge = CreateTopLeftImage("CompletedBadge", currentPanel.transform, 340f, 22f, 184f, 38f, theme.Success, false).gameObject;
            CreateInsetText("Label", completedBadge.transform, "운항 완료", font, 17f, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            completedBadge.SetActive(false);

            var listPanel = CreateTopLeftImage("DestinationListPanel", parent, 592f, 74f, 312f, 334f, theme.Panel, false);
            var completed = CreateTopLeftText("CompletedDestinationsText", listPanel.transform, 22f, 28f, 268f, 116f, "완료한 목적지\n출항 기지", font, 18f, TextAlignmentOptions.TopLeft);
            CreateTopLeftImage("Divider", listPanel.transform, 22f, 156f, 268f, 2f, new Color32(0x3D, 0x62, 0x75, 0xFF), false);
            var upcoming = CreateTopLeftText("UpcomingDestinationsText", listPanel.transform, 22f, 178f, 268f, 116f, "이후 목적지\n등대항 → 북쪽 기지", font, 18f, TextAlignmentOptions.TopLeft);
            var serialized = new SerializedObject(view);
            SetObject(serialized, "destinationNameText", destination);
            SetObject(serialized, "destinationProgressText", progressText);
            SetObject(serialized, "destinationProgressFill", fill);
            SetObject(serialized, "cargoText", cargo);
            SetObject(serialized, "completedDestinationsText", completed);
            SetObject(serialized, "upcomingDestinationsText", upcoming);
            SetObject(serialized, "completedBadge", completedBadge);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return new RouteTargets(view);
        }

        private static SettingsTargets BuildSettings(Transform parent, UiThemeAsset theme, TMP_FontAsset font)
        {
            var root = CreateStretchRect("SettingsRoot", parent).gameObject;
            var view = root.AddComponent<SettingsModalView>();
            CreateStretchImage("Dim", root.transform, new Color(0.01f, 0.03f, 0.05f, 0.72f), true);
            var modal = CreateTopLeftImage("SettingsModal", root.transform, 210f, 30f, 540f, 490f, theme.Panel, true);
            var title = CreateTopLeftText("TitleText", modal.transform, 20f, 14f, 200f, 34f, "설정", font, 25f, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            var close = CreateButton("CloseButton", modal.transform, 476f, 8f, 48f, 44f, "닫기", font, 12f, theme.Panel);

            CreateTopLeftText("VolumeLabel", modal.transform, 20f, 64f, 110f, 26f, "마스터 볼륨", font, 15f, TextAlignmentOptions.Left);
            var volume = CreateSlider("MasterVolumeSlider", modal.transform, 136f, 64f, 160f, 28f, theme);
            volume.minValue = 0f;
            volume.maxValue = 1f;
            volume.value = 0f;

            CreateTopLeftText("ShakeLabel", modal.transform, 20f, 112f, 200f, 30f, "화면 진동", font, 15f, TextAlignmentOptions.Left);
            var shake = CreateToggle("ScreenShakeToggle", modal.transform, 258f, 112f, 34f, 30f, theme);
            shake.isOn = true;

            var quit = CreateButton("QuitButton", modal.transform, 20f, 168f, 276f, 52f, "게임 종료", font, 18f, new Color32(0x83, 0x3D, 0x45, 0xFF));
            var resetSave = CreateButton("ResetSaveButton", modal.transform, 20f, 228f, 276f, 52f, "저장 초기화", font, 18f, new Color32(0x8A, 0x5A, 0x1E, 0xFF));

            var windowControls = BuildWindowControls(modal.transform, theme, font);

            var serialized = new SerializedObject(view);
            SetObject(serialized, "masterVolumeSlider", volume);
            SetObject(serialized, "screenShakeToggle", shake);
            SetObject(serialized, "closeButton", close);
            SetObject(serialized, "quitButton", quit);
            SetObject(serialized, "resetSaveButton", resetSave);
            SetObject(serialized, "resetSaveButtonText", resetSave.GetComponentInChildren<TMP_Text>());

            SetObject(serialized, "positionBottomLeftButton", windowControls.PositionBottomLeft);
            SetObject(serialized, "positionBottomCenterButton", windowControls.PositionBottomCenter);
            SetObject(serialized, "positionBottomRightButton", windowControls.PositionBottomRight);
            SetObject(serialized, "positionResetButton", windowControls.PositionReset);
            SetObject(serialized, "positionBottomLeftText", windowControls.PositionBottomLeft.GetComponentInChildren<TMP_Text>());
            SetObject(serialized, "positionBottomCenterText", windowControls.PositionBottomCenter.GetComponentInChildren<TMP_Text>());
            SetObject(serialized, "positionBottomRightText", windowControls.PositionBottomRight.GetComponentInChildren<TMP_Text>());

            SetObject(serialized, "sizeDefaultButton", windowControls.SizeDefault);
            SetObject(serialized, "sizeLargeButton", windowControls.SizeLarge);
            SetObject(serialized, "sizeExtraLargeButton", windowControls.SizeExtraLarge);
            SetObject(serialized, "sizeResetButton", windowControls.SizeReset);
            SetObject(serialized, "sizeDefaultText", windowControls.SizeDefault.GetComponentInChildren<TMP_Text>());
            SetObject(serialized, "sizeLargeText", windowControls.SizeLarge.GetComponentInChildren<TMP_Text>());
            SetObject(serialized, "sizeExtraLargeText", windowControls.SizeExtraLarge.GetComponentInChildren<TMP_Text>());

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return new SettingsTargets(root, view);
        }

        private static WindowControlTargets BuildWindowControls(Transform modal, UiThemeAsset theme, TMP_FontAsset font)
        {
            const float buttonWidth = 118f;
            const float buttonHeight = 44f;
            const float gap = 8f;
            const float column0 = 20f;
            const float column1 = column0 + buttonWidth + gap;
            const float column2 = column1 + buttonWidth + gap;
            const float column3 = column2 + buttonWidth + gap;

            CreateTopLeftText("WindowPositionLabel", modal, 20f, 296f, 300f, 26f, "창 위치", font, 15f, TextAlignmentOptions.Left);
            var positionBottomLeft = CreateButton("PositionBottomLeftButton", modal, column0, 330f, buttonWidth, buttonHeight, "왼쪽 아래", font, 14f, theme.Panel);
            var positionBottomCenter = CreateButton("PositionBottomCenterButton", modal, column1, 330f, buttonWidth, buttonHeight, "가운데 아래", font, 14f, theme.Panel);
            var positionBottomRight = CreateButton("PositionBottomRightButton", modal, column2, 330f, buttonWidth, buttonHeight, "오른쪽 아래", font, 14f, theme.Panel);
            var positionReset = CreateButton("PositionResetButton", modal, column3, 330f, buttonWidth, buttonHeight, "위치 초기화", font, 14f, new Color32(0x3A, 0x4A, 0x58, 0xFF));

            CreateTopLeftText("WindowSizeLabel", modal, 20f, 392f, 300f, 26f, "창 크기", font, 15f, TextAlignmentOptions.Left);
            var sizeDefault = CreateButton("SizeDefaultButton", modal, column0, 426f, buttonWidth, buttonHeight, "기본", font, 14f, theme.Panel);
            var sizeLarge = CreateButton("SizeLargeButton", modal, column1, 426f, buttonWidth, buttonHeight, "크게", font, 14f, theme.Panel);
            var sizeExtraLarge = CreateButton("SizeExtraLargeButton", modal, column2, 426f, buttonWidth, buttonHeight, "매우 크게", font, 14f, theme.Panel);
            var sizeReset = CreateButton("SizeResetButton", modal, column3, 426f, buttonWidth, buttonHeight, "크기 초기화", font, 14f, new Color32(0x3A, 0x4A, 0x58, 0xFF));

            return new WindowControlTargets(
                positionBottomLeft,
                positionBottomCenter,
                positionBottomRight,
                positionReset,
                sizeDefault,
                sizeLarge,
                sizeExtraLarge,
                sizeReset);
        }

        private static ArrivalTargets BuildArrival(Transform parent, UiThemeAsset theme, TMP_FontAsset font)
        {
            var root = new GameObject("ArrivalRoot", typeof(RectTransform), typeof(CanvasGroup));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect);
            var view = root.AddComponent<ArrivalOverlayView>();
            CreateStretchImage("Backdrop", root.transform, new Color(0.01f, 0.08f, 0.13f, 0.94f), true);
            var glow = CreateTopLeftImage("HarborGlow", root.transform, 300f, 108f, 360f, 324f, new Color(theme.Success.r, theme.Success.g, theme.Success.b, 0.16f), false);
            var marker = CreateTopLeftText("MarkerText", glow.transform, 60f, 36f, 240f, 70f, "◆", font, 54f, TextAlignmentOptions.Center);
            marker.color = theme.Success;
            var destination = CreateTopLeftText("DestinationText", glow.transform, 24f, 118f, 312f, 68f, "섬마을", font, 42f, TextAlignmentOptions.Center);
            destination.fontStyle = FontStyles.Bold;
            var status = CreateTopLeftText("StatusText", glow.transform, 24f, 198f, 312f, 52f, "보급 항로 연결 완료", font, 21f, TextAlignmentOptions.Center);
            status.color = theme.Reward;
            CreateTopLeftText("DurationHint", glow.transform, 24f, 260f, 312f, 32f, "목적지 도착", font, 14f, TextAlignmentOptions.Center);
            var serialized = new SerializedObject(view);
            SetObject(serialized, "canvasGroup", root.GetComponent<CanvasGroup>());
            SetObject(serialized, "destinationText", destination);
            SetObject(serialized, "statusText", status);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return new ArrivalTargets(root, view);
        }

        private static void ValidateStructure(GameObject prefab, List<string> errors)
        {
            var scaler = prefab.GetComponent<CanvasScaler>();
            if (scaler == null || scaler.referenceResolution != new Vector2(960f, 540f))
            {
                errors.Add("Canvas reference resolution must be 960x540.");
            }

            if (prefab.transform.Find("Background/CommonHeader/MaintenanceTabButton") != null ||
                prefab.transform.Find("Background/Body/MaintenanceRoot") != null)
            {
                errors.Add("Legacy maintenance tab and tree objects must be absent.");
            }

            if (prefab.GetComponentsInChildren<RouteStatusView>(true).Length != 1 ||
                prefab.GetComponentsInChildren<SettingsModalView>(true).Length != 1 ||
                prefab.GetComponentsInChildren<ArrivalOverlayView>(true).Length != 1)
            {
                errors.Add("Route, settings, and arrival must each have exactly one focused view component.");
            }

            if (prefab.GetComponentInChildren<ScrollRect>(true) != null)
            {
                errors.Add("Management views must not use scrolling, dragging, or zooming.");
            }

            var route = prefab.transform.Find("Background/Body/RouteRoot");
            if (route == null)
            {
                errors.Add("RouteRoot is missing.");
            }
            else if (route.GetComponentsInChildren<Selectable>(true).Length != 0)
            {
                errors.Add("Route view must be read-only and contain no path-selection controls.");
            }

            if (prefab.transform.Find("Background/SettingsRoot/SettingsModal/MasterVolumeSlider") == null ||
                prefab.transform.Find("Background/SettingsRoot/SettingsModal/ScreenShakeToggle") == null ||
                prefab.transform.Find("Background/SettingsRoot/SettingsModal/QuitButton") == null ||
                prefab.transform.Find("Background/SettingsRoot/SettingsModal/ResetSaveButton") == null)
            {
                errors.Add("Settings modal must expose the required volume, shake, quit, and reset-save controls.");
            }

            var windowControlPaths = new[]
            {
                "Background/SettingsRoot/SettingsModal/PositionBottomLeftButton",
                "Background/SettingsRoot/SettingsModal/PositionBottomCenterButton",
                "Background/SettingsRoot/SettingsModal/PositionBottomRightButton",
                "Background/SettingsRoot/SettingsModal/PositionResetButton",
                "Background/SettingsRoot/SettingsModal/SizeDefaultButton",
                "Background/SettingsRoot/SettingsModal/SizeLargeButton",
                "Background/SettingsRoot/SettingsModal/SizeExtraLargeButton",
                "Background/SettingsRoot/SettingsModal/SizeResetButton"
            };

            foreach (var path in windowControlPaths)
            {
                if (prefab.transform.Find(path) == null)
                {
                    errors.Add($"Settings modal must expose window control {path}.");
                }
            }
        }

        private static void ValidateBehaviour(GameObject prefab, List<string> errors)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                errors.Add("Could not instantiate the UI-05 prefab for behaviour validation.");
                return;
            }

            try
            {
                var presenter = instance.GetComponent<ManagementViewsPresenter>();
                var source = instance.GetComponent<Ui05ManagementSampleSource>();
                if (presenter == null || source == null)
                {
                    errors.Add("Presenter or sample source is missing.");
                    return;
                }

                source.ResetSample();
                var routeView = instance.GetComponentInChildren<RouteStatusView>(true);
                var settingsView = instance.GetComponentInChildren<SettingsModalView>(true);
                var arrivalView = instance.GetComponentInChildren<ArrivalOverlayView>(true);
                if (routeView?.CurrentData?.CurrentDestinationId != "island-village" ||
                    settingsView == null ||
                    arrivalView == null)
                {
                    errors.Add("Focused views must be independently present and route data must render.");
                }

                var settingsEvents = 0;
                presenter.SettingsVisibilityChanged += _ => settingsEvents++;
                presenter.OpenSettings();
                presenter.OpenSettings();
                presenter.CloseSettings();
                presenter.CloseSettings();
                if (settingsEvents != 2)
                {
                    errors.Add("Settings open/close must each emit one pause-boundary request.");
                }

                var completedCount = 0;
                presenter.ArrivalPresentationCompleted += _ => completedCount++;
                var request = new ArrivalPresentationRequested("island-village", "섬마을");
                var acceptedFirst = presenter.PresentArrival(request);
                var acceptedDuplicate = presenter.PresentArrival(request);
                presenter.AdvanceArrivalForValidation(ManagementViewsPresenter.ArrivalDurationSeconds + 0.1f);
                if (!acceptedFirst ||
                    acceptedDuplicate ||
                    completedCount != 1 ||
                    presenter.LastCompletedArrivalId != "island-village" ||
                    arrivalView?.LastCompletedArrivalId != "island-village")
                {
                    errors.Add("Each destination arrival must play and complete exactly once.");
                }

                source.ShowCompletedState();
                var startButton = instance.transform.Find("Background/CommonHeader/StageStartButton")?.GetComponent<Button>();
                var startText = startButton?.GetComponentInChildren<TMP_Text>()?.text;
                if (startButton == null || startButton.interactable || startText != "운항 완료")
                {
                    errors.Add("Completed state must show '운항 완료' and disable stage start.");
                }
            }
            catch (Exception exception)
            {
                errors.Add($"Behaviour validation threw {exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static GameObject CreateCanvasRoot()
        {
            var root = new GameObject("UI_ManagementViews", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(960f, 540f);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
            return root;
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
            Color color)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetTopLeft(rect, x, y, width, height);
            var image = root.GetComponent<Image>();
            image.color = color;
            var button = root.GetComponent<Button>();
            button.targetGraphic = image;
            ConfigureButtonColors(button);
            CreateInsetText("Label", root.transform, label, font, fontSize, TextAlignmentOptions.Center);
            return button;
        }

        private static Slider CreateSlider(
            string name,
            Transform parent,
            float x,
            float y,
            float width,
            float height,
            UiThemeAsset theme)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Slider));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetTopLeft(rect, x, y, width, height);
            var track = CreateStretchImage("Track", root.transform, new Color32(0x04, 0x12, 0x20, 0xFF), false);
            track.rectTransform.offsetMin = new Vector2(0f, 9f);
            track.rectTransform.offsetMax = new Vector2(0f, -9f);
            var fillArea = CreateStretchRect("Fill Area", root.transform);
            fillArea.offsetMin = new Vector2(4f, 10f);
            fillArea.offsetMax = new Vector2(-4f, -10f);
            var fill = CreateStretchImage("Fill", fillArea, theme.ActionAccent, false);
            var handleArea = CreateStretchRect("Handle Slide Area", root.transform);
            handleArea.offsetMin = new Vector2(8f, 0f);
            handleArea.offsetMax = new Vector2(-8f, 0f);
            var handle = CreateImage("Handle", handleArea, theme.PrimaryText, true);
            handle.rectTransform.sizeDelta = new Vector2(16f, height);
            var slider = root.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static Toggle CreateToggle(
            string name,
            Transform parent,
            float x,
            float y,
            float width,
            float height,
            UiThemeAsset theme)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetTopLeft(rect, x, y, width, height);
            var background = CreateStretchImage("Background", root.transform, new Color32(0x04, 0x12, 0x20, 0xFF), true);
            var checkmark = CreateStretchImage("Checkmark", background.transform, theme.Success, false);
            checkmark.rectTransform.offsetMin = new Vector2(6f, 6f);
            checkmark.rectTransform.offsetMax = new Vector2(-6f, -6f);
            var toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            return toggle;
        }

        private static void ConfigureButtonColors(Selectable button)
        {
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.48f, 0.53f, 0.58f, 0.72f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static Image CreateTopLeftImage(string name, Transform parent, float x, float y, float width, float height, Color color, bool raycast)
        {
            var image = CreateImage(name, parent, color, raycast);
            SetTopLeft(image.rectTransform, x, y, width, height);
            return image;
        }

        private static Image CreateStretchImage(string name, Transform parent, Color color, bool raycast)
        {
            var image = CreateImage(name, parent, color, raycast);
            Stretch(image.rectTransform);
            return image;
        }

        private static Image CreateImage(string name, Transform parent, Color color, bool raycast)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
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

        private static TextMeshProUGUI CreateInsetText(
            string name,
            Transform parent,
            string value,
            TMP_FontAsset font,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            var text = CreateText(name, parent, value, font, fontSize, alignment);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(6f, 3f);
            text.rectTransform.offsetMax = new Vector2(-6f, -3f);
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
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var text = root.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.color = new Color32(0xDD, 0xF7, 0xFF, 0xFF);
            text.alignment = alignment;
            text.enableWordWrapping = false;
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

        private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object? value)
        {
            var property = serialized.FindProperty(propertyName) ??
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on {serialized.targetObject.name}.");
            property.objectReferenceValue = value;
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

        private readonly struct RouteTargets
        {
            public RouteTargets(RouteStatusView view)
            {
                View = view;
            }

            public RouteStatusView View { get; }
        }

        private readonly struct SettingsTargets
        {
            public SettingsTargets(GameObject root, SettingsModalView view)
            {
                Root = root;
                View = view;
            }

            public GameObject Root { get; }
            public SettingsModalView View { get; }
        }

        private readonly struct WindowControlTargets
        {
            public WindowControlTargets(
                Button positionBottomLeft,
                Button positionBottomCenter,
                Button positionBottomRight,
                Button positionReset,
                Button sizeDefault,
                Button sizeLarge,
                Button sizeExtraLarge,
                Button sizeReset)
            {
                PositionBottomLeft = positionBottomLeft;
                PositionBottomCenter = positionBottomCenter;
                PositionBottomRight = positionBottomRight;
                PositionReset = positionReset;
                SizeDefault = sizeDefault;
                SizeLarge = sizeLarge;
                SizeExtraLarge = sizeExtraLarge;
                SizeReset = sizeReset;
            }

            public Button PositionBottomLeft { get; }
            public Button PositionBottomCenter { get; }
            public Button PositionBottomRight { get; }
            public Button PositionReset { get; }
            public Button SizeDefault { get; }
            public Button SizeLarge { get; }
            public Button SizeExtraLarge { get; }
            public Button SizeReset { get; }
        }

        private readonly struct ArrivalTargets
        {
            public ArrivalTargets(GameObject root, ArrivalOverlayView view)
            {
                Root = root;
                View = view;
            }

            public GameObject Root { get; }
            public ArrivalOverlayView View { get; }
        }
    }
}
