#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Maintenance;
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

        private static readonly string[] ExpectedNodeIds =
        {
            "C01", "C02", "C03", "C04",
            "D01", "D02", "D03",
            "S01", "S02", "S03",
            "H01", "H02", "H03"
        };

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

            Debug.Log("[UI-05] Validation passed: 13 nodes, three states, read-only route, settings, completion, and one-shot arrivals.");
        }

        private static void BuildContents(GameObject root, UiThemeAsset theme, TMP_FontAsset font)
        {
            var background = CreateStretchImage("Background", root.transform, theme.Background, false);
            var presenter = root.AddComponent<ManagementViewsPresenter>();
            var source = root.AddComponent<Ui05ManagementSampleSource>();

            var header = CreateTopLeftImage("CommonHeader", background.transform, 16f, 12f, 928f, 48f, theme.Panel, false);
            var maintenanceTab = CreateButton("MaintenanceTabButton", header.transform, 0f, 0f, 96f, 48f, "정비", font, 17f, theme.Panel);
            var routeTab = CreateButton("RouteTabButton", header.transform, 96f, 0f, 112f, 48f, "운항 현황", font, 16f, theme.Panel);
            var fundsArea = CreateTopLeftImage("FundsArea", header.transform, 484f, 4f, 152f, 40f, new Color32(0x12, 0x31, 0x49, 0xFF), false);
            var fundsText = CreateInsetText("FundsText", fundsArea.transform, "정비 자금 12.4K", font, 15f, TextAlignmentOptions.Center);
            var startButton = CreateButton("StageStartButton", header.transform, 636f, 0f, 148f, 48f, "다음 쇄빙 00:24", font, 14f, theme.ActionAccent);
            var settingsButton = CreateButton("SettingsButton", header.transform, 784f, 0f, 64f, 48f, "설정", font, 14f, theme.Panel);
            var collapseButton = CreateButton("CollapseButton", header.transform, 848f, 0f, 80f, 48f, "접기", font, 14f, theme.Panel);

            var body = CreateTopLeftImage("Body", background.transform, 16f, 72f, 928f, 452f, new Color32(0x08, 0x19, 0x2A, 0xFF), false);
            var maintenanceRoot = CreateStretchRect("MaintenanceRoot", body.transform).gameObject;
            var routeRoot = CreateStretchRect("RouteRoot", body.transform).gameObject;

            var nodeViews = BuildMaintenance(maintenanceRoot.transform, theme, font);
            var maintenanceDetail = maintenanceRoot.transform.Find("DetailPanel");
            if (maintenanceDetail == null)
            {
                throw new InvalidOperationException("Maintenance detail panel was not built.");
            }

            var routeTargets = BuildRoute(routeRoot.transform, theme, font);
            var settingsTargets = BuildSettings(background.transform, theme, font);
            var arrivalTargets = BuildArrival(background.transform, theme, font);

            var presenterObject = new SerializedObject(presenter);
            SetObject(presenterObject, "theme", theme);
            SetObject(presenterObject, "maintenanceTabButton", maintenanceTab);
            SetObject(presenterObject, "routeTabButton", routeTab);
            SetObject(presenterObject, "stageStartButton", startButton);
            SetObject(presenterObject, "settingsButton", settingsButton);
            SetObject(presenterObject, "collapseButton", collapseButton);
            SetObject(presenterObject, "fundsText", fundsText);
            SetObject(presenterObject, "stageStartText", startButton.GetComponentInChildren<TMP_Text>());
            SetObject(presenterObject, "maintenanceRoot", maintenanceRoot);
            SetObject(presenterObject, "routeRoot", routeRoot);
            SetObject(presenterObject, "maintenanceTabText", maintenanceTab.GetComponentInChildren<TMP_Text>());
            SetObject(presenterObject, "routeTabText", routeTab.GetComponentInChildren<TMP_Text>());
            SetObjectArray(presenterObject, "nodeViews", nodeViews.Cast<UnityEngine.Object>().ToArray());
            SetObject(presenterObject, "selectedNameText", FindText(maintenanceDetail, "SelectedNameText"));
            SetObject(presenterObject, "selectedStateText", FindText(maintenanceDetail, "SelectedStateText"));
            SetObject(presenterObject, "selectedLevelText", FindText(maintenanceDetail, "SelectedLevelText"));
            SetObject(presenterObject, "currentEffectText", FindText(maintenanceDetail, "CurrentEffectText"));
            SetObject(presenterObject, "nextEffectText", FindText(maintenanceDetail, "NextEffectText"));
            SetObject(presenterObject, "nextCostText", FindText(maintenanceDetail, "NextCostText"));
            SetObject(presenterObject, "requirementText", FindText(maintenanceDetail, "RequirementText"));
            SetObject(presenterObject, "purchaseButton", FindButton(maintenanceDetail, "PurchaseButton"));
            SetObject(presenterObject, "purchaseButtonText", FindText(maintenanceDetail, "PurchaseButton/Label"));
            SetObject(presenterObject, "destinationNameText", routeTargets.DestinationName);
            SetObject(presenterObject, "destinationProgressText", routeTargets.ProgressText);
            SetObject(presenterObject, "destinationProgressFill", routeTargets.ProgressFill);
            SetObject(presenterObject, "cargoText", routeTargets.Cargo);
            SetObject(presenterObject, "completedDestinationsText", routeTargets.Completed);
            SetObject(presenterObject, "upcomingDestinationsText", routeTargets.Upcoming);
            SetObject(presenterObject, "completedBadge", routeTargets.CompletedBadge);
            SetObject(presenterObject, "settingsRoot", settingsTargets.Root);
            SetObject(presenterObject, "masterVolumeSlider", settingsTargets.Volume);
            SetObject(presenterObject, "screenShakeToggle", settingsTargets.Shake);
            SetObject(presenterObject, "settingsCloseButton", settingsTargets.Close);
            SetObject(presenterObject, "quitButton", settingsTargets.Quit);
            SetObject(presenterObject, "arrivalRoot", arrivalTargets.Root);
            SetObject(presenterObject, "arrivalCanvasGroup", arrivalTargets.CanvasGroup);
            SetObject(presenterObject, "arrivalDestinationText", arrivalTargets.Destination);
            SetObject(presenterObject, "arrivalStatusText", arrivalTargets.Status);
            presenterObject.ApplyModifiedPropertiesWithoutUndo();

            var sourceObject = new SerializedObject(source);
            SetObject(sourceObject, "presenter", presenter);
            sourceObject.ApplyModifiedPropertiesWithoutUndo();

            routeRoot.SetActive(false);
            settingsTargets.Root.SetActive(false);
            arrivalTargets.Root.SetActive(false);
        }

        private static List<MaintenanceNodeView> BuildMaintenance(Transform parent, UiThemeAsset theme, TMP_FontAsset font)
        {
            var title = CreateTopLeftText("TitleText", parent, 18f, 10f, 480f, 34f, "선박 정비·강화", font, 25f, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            var legend = CreateTopLeftText("StateLegend", parent, 390f, 14f, 190f, 24f, "■ 보유   ◆ 구매 가능   ▣ 잠김", font, 12f, TextAlignmentOptions.Right);
            legend.color = theme.PrimaryText;

            var treeArea = CreateTopLeftImage("TreeArea", parent, 14f, 50f, 560f, 386f, new Color32(0x0B, 0x20, 0x32, 0xFF), false);
            var lines = CreateStretchRect("Connections", treeArea.transform);
            BuildConnections(lines, theme.Success);

            var positions = new Dictionary<string, Vector2>(StringComparer.Ordinal)
            {
                ["C01"] = new(18f, 166f),
                ["C02"] = new(156f, 10f),
                ["C03"] = new(156f, 66f),
                ["C04"] = new(300f, 66f),
                ["D01"] = new(156f, 126f),
                ["D02"] = new(300f, 174f),
                ["D03"] = new(300f, 118f),
                ["S01"] = new(156f, 230f),
                ["S02"] = new(300f, 230f),
                ["S03"] = new(438f, 230f),
                ["H01"] = new(156f, 318f),
                ["H02"] = new(300f, 292f),
                ["H03"] = new(300f, 348f)
            };

            var names = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["C01"] = "강화 장비", ["C02"] = "정비 효율", ["C03"] = "청빙 대응", ["C04"] = "심빙 대응",
                ["D01"] = "주 파쇄기", ["D02"] = "고속 구동", ["D03"] = "과잉 파쇄",
                ["S01"] = "보조 파쇄기", ["S02"] = "다중 타격", ["S03"] = "표적 분석",
                ["H01"] = "파편 비산", ["H02"] = "특수빙 증폭", ["H03"] = "빙판 붕괴"
            };

            var views = new List<MaintenanceNodeView>();
            foreach (var id in ExpectedNodeIds)
            {
                views.Add(CreateMaintenanceNode(treeArea.transform, id, names[id], positions[id], font));
            }

            var detail = CreateTopLeftImage("DetailPanel", parent, 590f, 20f, 320f, 416f, theme.Panel, false);
            var detailTitle = CreateTopLeftText("DetailTitle", detail.transform, 18f, 14f, 284f, 24f, "선택한 정비", font, 14f, TextAlignmentOptions.Left);
            detailTitle.color = theme.Reward;
            var selectedName = CreateTopLeftText("SelectedNameText", detail.transform, 18f, 42f, 284f, 40f, "강화 장비", font, 25f, TextAlignmentOptions.Left);
            selectedName.fontStyle = FontStyles.Bold;
            CreateTopLeftText("SelectedStateText", detail.transform, 18f, 86f, 138f, 28f, "상태  보유", font, 14f, TextAlignmentOptions.Left);
            CreateTopLeftText("SelectedLevelText", detail.transform, 156f, 86f, 146f, 28f, "현재 단계  1/1", font, 14f, TextAlignmentOptions.Right);
            CreateTopLeftImage("Divider", detail.transform, 18f, 122f, 284f, 2f, new Color32(0x3D, 0x62, 0x75, 0xFF), false);
            CreateTopLeftText("CurrentEffectText", detail.transform, 18f, 138f, 284f, 54f, "현재 효과\n파쇄 계통 개방", font, 15f, TextAlignmentOptions.TopLeft);
            CreateTopLeftText("NextEffectText", detail.transform, 18f, 202f, 284f, 54f, "다음 효과\n최대 단계", font, 15f, TextAlignmentOptions.TopLeft);
            CreateTopLeftText("NextCostText", detail.transform, 18f, 266f, 284f, 26f, "다음 비용  -", font, 14f, TextAlignmentOptions.Left);
            CreateTopLeftText("RequirementText", detail.transform, 18f, 298f, 284f, 26f, "필요 선행  충족", font, 14f, TextAlignmentOptions.Left);
            var purchase = CreateButton("PurchaseButton", detail.transform, 18f, 344f, 284f, 54f, "최대 단계", font, 18f, theme.ActionAccent);
            purchase.interactable = false;
            return views;
        }

        private static MaintenanceNodeView CreateMaintenanceNode(
            Transform parent,
            string id,
            string displayName,
            Vector2 position,
            TMP_FontAsset font)
        {
            var root = new GameObject($"Node_{id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(MaintenanceNodeView));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetTopLeft(rect, position.x, position.y, 118f, 50f);
            var hitImage = root.GetComponent<Image>();
            hitImage.color = Color.clear;

            var border = CreateStretchImage("SelectionBorder", root.transform, Color.white, false);
            border.rectTransform.offsetMin = Vector2.zero;
            border.rectTransform.offsetMax = Vector2.zero;
            var visual = CreateStretchImage("Visual", root.transform, new Color32(0x35, 0x45, 0x55, 0xFF), false);
            visual.rectTransform.offsetMin = new Vector2(3f, 3f);
            visual.rectTransform.offsetMax = new Vector2(-3f, -3f);
            var name = CreateTopLeftText("NameText", visual.transform, 6f, 3f, 106f, 19f, displayName, font, 12f, TextAlignmentOptions.Center);
            name.fontStyle = FontStyles.Bold;
            var level = CreateTopLeftText("LevelText", visual.transform, 5f, 22f, 66f, 18f, $"{id}  Lv.0/1", font, 10f, TextAlignmentOptions.Left);
            var state = CreateTopLeftText("StateText", visual.transform, 68f, 22f, 44f, 18f, "잠김", font, 9f, TextAlignmentOptions.Right);

            var button = root.GetComponent<Button>();
            button.targetGraphic = visual;
            ConfigureButtonColors(button);
            var view = root.GetComponent<MaintenanceNodeView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("nodeId").stringValue = id;
            SetObject(serialized, "button", button);
            SetObject(serialized, "background", visual);
            SetObject(serialized, "selectionBorder", border);
            SetObject(serialized, "nameText", name);
            SetObject(serialized, "levelText", level);
            SetObject(serialized, "stateText", state);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            border.enabled = false;
            return view;
        }

        private static void BuildConnections(RectTransform parent, Color activeColor)
        {
            var color = new Color(activeColor.r, activeColor.g, activeColor.b, 0.38f);
            CreateLine(parent, 77f, 191f, 156f, 35f, color);
            CreateLine(parent, 77f, 191f, 156f, 91f, color);
            CreateLine(parent, 274f, 91f, 300f, 91f, color);
            CreateLine(parent, 77f, 191f, 156f, 151f, color);
            CreateLine(parent, 274f, 151f, 300f, 143f, color);
            CreateLine(parent, 77f, 191f, 300f, 199f, color);
            CreateLine(parent, 77f, 191f, 156f, 255f, color);
            CreateLine(parent, 274f, 255f, 300f, 255f, color);
            CreateLine(parent, 418f, 255f, 438f, 255f, color);
            CreateLine(parent, 77f, 191f, 156f, 343f, color);
            CreateLine(parent, 274f, 343f, 300f, 317f, color);
            CreateLine(parent, 274f, 343f, 300f, 373f, color);
        }

        private static void CreateLine(Transform parent, float x1, float y1, float x2, float y2, Color color)
        {
            var start = new Vector2(x1, -y1);
            var end = new Vector2(x2, -y2);
            var center = (start + end) * 0.5f;
            var delta = end - start;
            var image = CreateImage("Connection", parent, color, false);
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = new Vector2(delta.magnitude, 3f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private static RouteTargets BuildRoute(Transform parent, UiThemeAsset theme, TMP_FontAsset font)
        {
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
            return new RouteTargets(destination, progressText, fill, cargo, completed, upcoming, completedBadge);
        }

        private static SettingsTargets BuildSettings(Transform parent, UiThemeAsset theme, TMP_FontAsset font)
        {
            var root = CreateStretchRect("SettingsRoot", parent).gameObject;
            CreateStretchImage("Dim", root.transform, new Color(0.01f, 0.03f, 0.05f, 0.72f), true);
            var modal = CreateTopLeftImage("SettingsModal", root.transform, 320f, 150f, 320f, 240f, theme.Panel, true);
            var title = CreateTopLeftText("TitleText", modal.transform, 20f, 14f, 200f, 34f, "설정", font, 25f, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            var close = CreateButton("CloseButton", modal.transform, 264f, 8f, 48f, 44f, "닫기", font, 12f, theme.Panel);

            CreateTopLeftText("VolumeLabel", modal.transform, 20f, 64f, 110f, 26f, "마스터 볼륨", font, 15f, TextAlignmentOptions.Left);
            var volume = CreateSlider("MasterVolumeSlider", modal.transform, 136f, 64f, 160f, 28f, theme);
            volume.minValue = 0f;
            volume.maxValue = 1f;
            volume.value = 0f;

            CreateTopLeftText("ShakeLabel", modal.transform, 20f, 112f, 200f, 30f, "화면 진동", font, 15f, TextAlignmentOptions.Left);
            var shake = CreateToggle("ScreenShakeToggle", modal.transform, 258f, 112f, 34f, 30f, theme);
            shake.isOn = true;

            var quit = CreateButton("QuitButton", modal.transform, 20f, 168f, 276f, 52f, "게임 종료", font, 18f, new Color32(0x83, 0x3D, 0x45, 0xFF));
            return new SettingsTargets(root, volume, shake, close, quit);
        }

        private static ArrivalTargets BuildArrival(Transform parent, UiThemeAsset theme, TMP_FontAsset font)
        {
            var root = new GameObject("ArrivalRoot", typeof(RectTransform), typeof(CanvasGroup));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect);
            CreateStretchImage("Backdrop", root.transform, new Color(0.01f, 0.08f, 0.13f, 0.94f), true);
            var glow = CreateTopLeftImage("HarborGlow", root.transform, 300f, 108f, 360f, 324f, new Color(theme.Success.r, theme.Success.g, theme.Success.b, 0.16f), false);
            var marker = CreateTopLeftText("MarkerText", glow.transform, 60f, 36f, 240f, 70f, "◆", font, 54f, TextAlignmentOptions.Center);
            marker.color = theme.Success;
            var destination = CreateTopLeftText("DestinationText", glow.transform, 24f, 118f, 312f, 68f, "섬마을", font, 42f, TextAlignmentOptions.Center);
            destination.fontStyle = FontStyles.Bold;
            var status = CreateTopLeftText("StatusText", glow.transform, 24f, 198f, 312f, 52f, "보급 항로 연결 완료", font, 21f, TextAlignmentOptions.Center);
            status.color = theme.Reward;
            CreateTopLeftText("DurationHint", glow.transform, 24f, 260f, 312f, 32f, "목적지 도착", font, 14f, TextAlignmentOptions.Center);
            return new ArrivalTargets(root, root.GetComponent<CanvasGroup>(), destination, status);
        }

        private static void ValidateStructure(GameObject prefab, List<string> errors)
        {
            var scaler = prefab.GetComponent<CanvasScaler>();
            if (scaler == null || scaler.referenceResolution != new Vector2(960f, 540f))
            {
                errors.Add("Canvas reference resolution must be 960x540.");
            }

            var nodes = prefab.GetComponentsInChildren<MaintenanceNodeView>(true);
            if (nodes.Length != 13)
            {
                errors.Add($"Maintenance tree must contain 13 nodes; found {nodes.Length}.");
            }

            var actualIds = nodes.Select(node => node.NodeId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var expectedIds = ExpectedNodeIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (!actualIds.SequenceEqual(expectedIds, StringComparer.Ordinal))
            {
                errors.Add("Maintenance node IDs do not match the fixed 13-node catalog.");
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
                prefab.transform.Find("Background/SettingsRoot/SettingsModal/QuitButton") == null)
            {
                errors.Add("Settings modal must expose only the required volume, shake, and quit controls.");
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
                if (presenter.RenderedNodeCount != 13 ||
                    presenter.CountRenderedNodes(MaintenanceNodeState.Owned) == 0 ||
                    presenter.CountRenderedNodes(MaintenanceNodeState.Available) == 0 ||
                    presenter.CountRenderedNodes(MaintenanceNodeState.Locked) == 0)
                {
                    errors.Add("Sample must render all 13 nodes and all three maintenance states.");
                }

                var purchaseCount = 0;
                presenter.PurchaseRequested += _ => purchaseCount++;
                presenter.SelectNode("C02");
                presenter.TryRequestSelectedPurchase();
                if (purchaseCount != 1)
                {
                    errors.Add("An eligible purchase must emit exactly one request without mutating UI data.");
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
                if (!acceptedFirst || acceptedDuplicate || completedCount != 1 || presenter.LastCompletedArrivalId != "island-village")
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

        private static TMP_Text FindText(Transform parent, string path) =>
            parent.Find(path)?.GetComponent<TMP_Text>() ??
            throw new InvalidOperationException($"Missing TMP text at {parent.name}/{path}.");

        private static Button FindButton(Transform parent, string path) =>
            parent.Find(path)?.GetComponent<Button>() ??
            throw new InvalidOperationException($"Missing button at {parent.name}/{path}.");

        private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object? value)
        {
            var property = serialized.FindProperty(propertyName) ??
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on {serialized.targetObject.name}.");
            property.objectReferenceValue = value;
        }

        private static void SetObjectArray(SerializedObject serialized, string propertyName, IReadOnlyList<UnityEngine.Object> values)
        {
            var property = serialized.FindProperty(propertyName) ??
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on {serialized.targetObject.name}.");
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

        private readonly struct RouteTargets
        {
            public RouteTargets(TMP_Text destinationName, TMP_Text progressText, Image progressFill, TMP_Text cargo, TMP_Text completed, TMP_Text upcoming, GameObject completedBadge)
            {
                DestinationName = destinationName;
                ProgressText = progressText;
                ProgressFill = progressFill;
                Cargo = cargo;
                Completed = completed;
                Upcoming = upcoming;
                CompletedBadge = completedBadge;
            }

            public TMP_Text DestinationName { get; }
            public TMP_Text ProgressText { get; }
            public Image ProgressFill { get; }
            public TMP_Text Cargo { get; }
            public TMP_Text Completed { get; }
            public TMP_Text Upcoming { get; }
            public GameObject CompletedBadge { get; }
        }

        private readonly struct SettingsTargets
        {
            public SettingsTargets(GameObject root, Slider volume, Toggle shake, Button close, Button quit)
            {
                Root = root;
                Volume = volume;
                Shake = shake;
                Close = close;
                Quit = quit;
            }

            public GameObject Root { get; }
            public Slider Volume { get; }
            public Toggle Shake { get; }
            public Button Close { get; }
            public Button Quit { get; }
        }

        private readonly struct ArrivalTargets
        {
            public ArrivalTargets(GameObject root, CanvasGroup canvasGroup, TMP_Text destination, TMP_Text status)
            {
                Root = root;
                CanvasGroup = canvasGroup;
                Destination = destination;
                Status = status;
            }

            public GameObject Root { get; }
            public CanvasGroup CanvasGroup { get; }
            public TMP_Text Destination { get; }
            public TMP_Text Status { get; }
        }
    }
}
