#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Icebreaker.Shared.Maintenance;
using Icebreaker.UI.Maintenance;
using Icebreaker.UI.Sandbox;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.UI.Editor
{
    public static class MaintenanceTreePrefabBuilder
    {
        private const string ThemePath = "Assets/04.Images/30.UI/Theme/UiTheme.asset";
        private const string DataFolder = "Assets/09.Data/UI";
        private const string LayoutPath = DataFolder + "/MaintenanceTreeLayout.asset";
        private const string PrefabFolder = "Assets/03.Prefabs/30.UI/Maintenance";
        private const string TreePrefabPath = PrefabFolder + "/UI_MaintenanceTree.prefab";
        private const string NodePrefabPath = PrefabFolder + "/UI_MaintenanceNode.prefab";
        private const string TooltipPrefabPath = PrefabFolder + "/UI_MaintenanceTooltip.prefab";
        private const string EdgePrefabPath = PrefabFolder + "/UI_MaintenanceEdge.prefab";
        private const string ConsoleButtonArtPath =
            "Assets/04.Images/30.UI/Maintenance/Buttons/MaintenanceConsoleButton.png";
        private const string BuildStamp = "maintenance-production-font-roles-v4-console-chrome";

        private static readonly Vector2 ContentSize = new Vector2(1600f, 900f);

        [MenuItem("ICEBREAKER/UI/Rebuild Maintenance Tree Presentation")]
        public static void Build()
        {
            Build(false);
        }

        internal static void Build(bool force)
        {
            EnsureAssetFolder(DataFolder);
            EnsureAssetFolder(PrefabFolder);
            if (!force && !ProductionUiGuard.NeedsRebuild(BuildStamp, TreePrefabPath))
            {
                Validate();
                return;
            }

            MaintenanceTreeArtBuilder.Build();

            var theme = AssetDatabase.LoadAssetAtPath<UiThemeAsset>(ThemePath);
            if (theme == null)
            {
                throw new InvalidOperationException($"UI theme was not found at {ThemePath}.");
            }

            var primaryFont = theme.PrimaryFont ??
                throw new InvalidOperationException("UI primary font is not assigned.");
            var maintenanceFont = theme.MaintenanceFont ??
                throw new InvalidOperationException("UI maintenance font is not assigned.");

            var layout = BuildLayout(LoadLogicalIcons());
            BuildNodePrefab(primaryFont);
            BuildEdgePrefab();
            BuildTooltipPrefab(theme, maintenanceFont);
            BuildTreePrefab(layout, theme, primaryFont);
            ProductionUiGuard.MarkRebuilt(BuildStamp, TreePrefabPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Debug.Log("[UI-TREE-01] Static maintenance tree presentation rebuilt and validated.");
        }

        [MenuItem("ICEBREAKER/UI/Validate Maintenance Tree Presentation")]
        public static void Validate()
        {
            var errors = new List<string>();
            var layout = AssetDatabase.LoadAssetAtPath<MaintenanceTreeLayoutAsset>(LayoutPath);
            var tree = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPath);
            var node = AssetDatabase.LoadAssetAtPath<GameObject>(NodePrefabPath);
            var tooltip = AssetDatabase.LoadAssetAtPath<GameObject>(TooltipPrefabPath);
            var edge = AssetDatabase.LoadAssetAtPath<GameObject>(EdgePrefabPath);

            if (layout == null)
            {
                errors.Add($"Missing layout: {LayoutPath}");
            }
            else
            {
                errors.AddRange(layout.Validate(ExpectedStepIds()));
                if (layout.Nodes.Count != 26)
                {
                    errors.Add($"Layout must contain 26 nodes, found {layout.Nodes.Count}.");
                }

                ValidateMinimumSpacing(layout, errors);
                foreach (var nodeLayout in layout.Nodes)
                {
                    if (nodeLayout.Icon == null)
                    {
                        errors.Add($"Layout node {nodeLayout.StepId} has no icon reference.");
                    }
                    else if (AssetDatabase.GetAssetPath(nodeLayout.Icon) !=
                             $"{MaintenanceTreeArtBuilder.IconFolder}/{nodeLayout.StepId[..3]}.png")
                    {
                        errors.Add($"Layout node {nodeLayout.StepId} uses the wrong logical icon.");
                    }
                }

                var distinctIcons = layout.Nodes.Select(nodeLayout => nodeLayout.Icon).Distinct().Count();
                if (distinctIcons != 14)
                {
                    errors.Add($"Layout must reuse exactly 14 logical icons, found {distinctIcons}.");
                }
            }

            ValidatePrefabExists(node, NodePrefabPath, errors);
            ValidatePrefabExists(tooltip, TooltipPrefabPath, errors);
            ValidatePrefabExists(edge, EdgePrefabPath, errors);
            if (tree == null)
            {
                errors.Add($"Missing prefab: {TreePrefabPath}");
            }
            else
            {
                ValidateTreeHierarchy(tree, errors);
                ValidatePresenterReferences(tree, errors);
                ProductionUiGuard.CollectErrors(tree, errors);
            }

            if (node != null)
            {
                var view = node.GetComponent<MaintenanceNodeView>();
                if (view == null)
                {
                    errors.Add("Node prefab is missing MaintenanceNodeView.");
                }
                else if (typeof(MaintenanceNodeView).GetMethod(
                             "Update",
                             BindingFlags.Instance | BindingFlags.Public |
                             BindingFlags.NonPublic | BindingFlags.DeclaredOnly) != null)
                {
                    errors.Add("MaintenanceNodeView must not define Update().");
                }
            }

            if (tooltip != null)
            {
                if (tooltip.transform.Find("PurchaseButton") != null)
                {
                    errors.Add("Tooltip must not contain a PurchaseButton.");
                }

                if (tooltip.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget))
                {
                    errors.Add("Tooltip graphics must not intercept raycasts.");
                }
            }

            ValidateFakeStates(errors);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "[UI-TREE-01] Validation failed:\n- " + string.Join("\n- ", errors));
            }

            Debug.Log("[UI-TREE-01] Layout, 26 nodes, fake states, prefabs, and hierarchy passed validation.");
        }

        private static MaintenanceTreeLayoutAsset BuildLayout(
            IReadOnlyDictionary<string, Sprite> logicalIcons)
        {
            var positions = CreatePositions();
            var branchStarts = new HashSet<string>(StringComparer.Ordinal)
            {
                "C02-L1", "C03-L1", "D01-L1", "D02-L1", "S01-L1", "H01-L1"
            };
            var branchLabels = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["C01-L1"] = "공통 장비",
                ["D01-L1"] = "직접 파쇄",
                ["S01-L1"] = "보조 파쇄",
                ["H01-L1"] = "연쇄 파괴"
            };

            var nodes = new List<MaintenanceTreeNodeLayout>(26);
            foreach (var stepId in ExpectedStepIds())
            {
                var size = stepId == MaintenanceTreeLayoutAsset.RootStepId
                    ? 60f
                    : branchStarts.Contains(stepId) ? 52f : 44f;
                nodes.Add(new MaintenanceTreeNodeLayout(
                    stepId,
                    positions[stepId],
                    new Vector2(size, size),
                    logicalIcons[stepId[..3]],
                    branchLabels.TryGetValue(stepId, out var label) ? label : ""));
            }

            var edges = new List<MaintenanceTreeEdgeLayout>();
            foreach (var pair in EdgePairs())
            {
                var from = positions[pair.From];
                var to = positions[pair.To];
                if (Mathf.Approximately(from.y, to.y))
                {
                    edges.Add(new MaintenanceTreeEdgeLayout(pair.From, pair.To, Array.Empty<Vector2>()));
                }
                else
                {
                    var middleX = (from.x + to.x) * 0.5f;
                    edges.Add(new MaintenanceTreeEdgeLayout(
                        pair.From,
                        pair.To,
                        new[] { new Vector2(middleX, from.y), new Vector2(middleX, to.y) }));
                }
            }

            var asset = AssetDatabase.LoadAssetAtPath<MaintenanceTreeLayoutAsset>(LayoutPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<MaintenanceTreeLayoutAsset>();
                AssetDatabase.CreateAsset(asset, LayoutPath);
            }

            asset.Configure(ContentSize, nodes, edges);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void BuildNodePrefab(TMP_FontAsset font)
        {
            var rootFrameSprite = LoadChrome("RootFrame");
            var normalFrameSprite = LoadChrome("NodeFrame");
            var purchasedFrameSprite = LoadChrome("StatePurchased");
            var availableFrameSprite = LoadChrome("StateAvailable");
            var lockedFrameSprite = LoadChrome("StateLocked");
            var previewFrameSprite = LoadChrome("StatePreview");
            var root = new GameObject(
                "UI_MaintenanceNode",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(MaintenanceNodeView));
            try
            {
                var rootRect = root.GetComponent<RectTransform>();
                ConfigureTopLeftAnchor(rootRect, new Vector2(140f, 120f));
                var selectionFrame = CreateCenteredImage("SelectionFrame", root.transform, Vector2.zero, new Vector2(70f, 70f), Color.white);
                selectionFrame.sprite = LoadChrome("SelectionOutline");
                selectionFrame.gameObject.SetActive(false);
                var frame = CreateCenteredImage("Frame", root.transform, Vector2.zero, new Vector2(60f, 60f), Color.white);
                frame.sprite = normalFrameSprite;
                var stateFrame = CreateCenteredImage("StateFrame", root.transform, Vector2.zero, new Vector2(64f, 64f), Color.white);
                stateFrame.sprite = availableFrameSprite;
                var icon = CreateCenteredImage("Icon", root.transform, Vector2.zero, new Vector2(38f, 38f), Color.white);
                icon.preserveAspect = true;
                var idText = CreateCenteredText("IdText", root.transform, new Vector2(0f, 20f), new Vector2(40f, 14f), "C01", font, 9f, TextAlignmentOptions.Center);
                var levelText = CreateCenteredText("LevelText", root.transform, new Vector2(22f, 22f), new Vector2(34f, 18f), "1/1", font, 10f, TextAlignmentOptions.Center);
                var checkIndicator = CreateCenteredImage("CheckIndicator", root.transform, new Vector2(22f, -21f), new Vector2(18f, 18f), Color.white);
                checkIndicator.sprite = LoadChrome("Check");
                var lockIndicator = CreateCenteredImage("LockIndicator", root.transform, new Vector2(22f, -21f), new Vector2(18f, 18f), Color.white);
                lockIndicator.sprite = LoadChrome("Lock");
                checkIndicator.gameObject.SetActive(false);
                lockIndicator.gameObject.SetActive(false);
                var nameText = CreateCenteredText("NameText", root.transform, new Vector2(0f, -40f), new Vector2(136f, 22f), "강화 장비", font, 14f, TextAlignmentOptions.Center);
                var statusText = CreateCenteredText("StatusText", root.transform, new Vector2(0f, -58f), new Vector2(136f, 18f), "구매 가능", font, 11f, TextAlignmentOptions.Center);
                var branchText = CreateCenteredText("BranchLabelText", root.transform, new Vector2(0f, 48f), new Vector2(150f, 22f), "공통 장비", font, 13f, TextAlignmentOptions.Center);

                var view = root.GetComponent<MaintenanceNodeView>();
                var serialized = new SerializedObject(view);
                SetReference(serialized, "canvasGroup", root.GetComponent<CanvasGroup>());
                SetReference(serialized, "selectionFrame", selectionFrame);
                SetReference(serialized, "frame", frame);
                SetReference(serialized, "stateFrame", stateFrame);
                SetReference(serialized, "icon", icon);
                SetReference(serialized, "checkIndicator", checkIndicator);
                SetReference(serialized, "lockIndicator", lockIndicator);
                SetReference(serialized, "rootFrameSprite", rootFrameSprite);
                SetReference(serialized, "normalFrameSprite", normalFrameSprite);
                SetReference(serialized, "purchasedFrameSprite", purchasedFrameSprite);
                SetReference(serialized, "availableFrameSprite", availableFrameSprite);
                SetReference(serialized, "lockedFrameSprite", lockedFrameSprite);
                SetReference(serialized, "previewFrameSprite", previewFrameSprite);
                SetReference(serialized, "idText", idText);
                SetReference(serialized, "nameText", nameText);
                SetReference(serialized, "levelText", levelText);
                SetReference(serialized, "statusText", statusText);
                SetReference(serialized, "branchLabelText", branchText);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, NodePrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildEdgePrefab()
        {
            var root = new GameObject(
                "UI_MaintenanceEdge",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(MaintenanceTreeEdgeView));
            try
            {
                ConfigureTopLeftAnchor(root.GetComponent<RectTransform>(), new Vector2(100f, 6f));
                var image = root.GetComponent<Image>();
                image.raycastTarget = false;
                image.sprite = LoadChrome("EdgeDefault");
                var serialized = new SerializedObject(root.GetComponent<MaintenanceTreeEdgeView>());
                SetReference(serialized, "lineImage", image);
                SetReference(serialized, "defaultSprite", LoadChrome("EdgeDefault"));
                SetReference(serialized, "litSprite", LoadChrome("EdgeLit"));
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, EdgePrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildTooltipPrefab(UiThemeAsset theme, TMP_FontAsset font)
        {
            var root = new GameObject(
                "UI_MaintenanceTooltip",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(MaintenanceTooltipView));
            try
            {
                ConfigureTopLeftAnchor(root.GetComponent<RectTransform>(), new Vector2(300f, 184f));
                var panel = root.GetComponent<Image>();
                panel.sprite = LoadChrome("TooltipPanel");
                panel.type = Image.Type.Sliced;
                panel.color = Color.white;
                panel.raycastTarget = false;
                var title = CreateTopLeftText("Title", root.transform, 14f, 12f, 272f, 28f, "주 파쇄기 출력 · 2/3", font, 18f, TextAlignmentOptions.Left);
                var effect = CreateTopLeftText("Effect", root.transform, 14f, 48f, 272f, 48f, "직접 피해 ×5\n현재값 → 구매 후 값", font, 14f, TextAlignmentOptions.TopLeft);
                var cost = CreateTopLeftText("Cost", root.transform, 14f, 106f, 272f, 26f, "가격 900", font, 15f, TextAlignmentOptions.Left);
                var lockText = CreateTopLeftText("Lock", root.transform, 14f, 136f, 272f, 34f, "잠금 조건 없음", font, 13f, TextAlignmentOptions.TopLeft);
                var serialized = new SerializedObject(root.GetComponent<MaintenanceTooltipView>());
                SetReference(serialized, "titleText", title);
                SetReference(serialized, "effectText", effect);
                SetReference(serialized, "costText", cost);
                SetReference(serialized, "lockText", lockText);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, TooltipPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildTreePrefab(
            MaintenanceTreeLayoutAsset layout,
            UiThemeAsset theme,
            TMP_FontAsset font)
        {
            var nodePrefab = AssetDatabase.LoadAssetAtPath<MaintenanceNodeView>(NodePrefabPath);
            var edgePrefab = AssetDatabase.LoadAssetAtPath<MaintenanceTreeEdgeView>(EdgePrefabPath);
            var tooltipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TooltipPrefabPath);
            var consoleButtonArt = AssetDatabase.LoadAssetAtPath<Sprite>(ConsoleButtonArtPath);
            if (nodePrefab == null || edgePrefab == null || tooltipPrefab == null || consoleButtonArt == null)
            {
                throw new InvalidOperationException("Maintenance tree art or child prefabs are missing.");
            }

            var root = CreateCanvasRoot("UI_MaintenanceTree", new Vector2(960f, 540f));
            try
            {
                var presenter = root.AddComponent<MaintenanceTreePresenter>();
                CreateStretchImage("Background", root.transform, theme.Background, false);

                var topBar = CreateTopLeftImage("TopBar", root.transform, 0f, 0f, 960f, 60f, Color.white, false);
                ApplyConsoleArt(topBar, consoleButtonArt);
                CreateTopLeftText("MaintenanceTab", topBar.transform, 16f, 10f, 120f, 40f, "정비", font, 20f, TextAlignmentOptions.Center);
                CreateTopLeftText("RouteTab", topBar.transform, 144f, 10f, 140f, 40f, "운항 현황", font, 18f, TextAlignmentOptions.Center);
                CreateTopLeftText("Title", topBar.transform, 310f, 10f, 250f, 40f, "선박 정비·강화", font, 19f, TextAlignmentOptions.Center);
                var previewStateText = CreateTopLeftText("PreviewStateText", topBar.transform, 570f, 10f, 180f, 40f, "가짜 상태 · 새 저장", font, 12f, TextAlignmentOptions.Center);
                CreateButton("SettingsButton", topBar.transform, 758f, 10f, 82f, 40f, "설정", font, theme.Background);
                var closeButton = CreateButton("CollapseButton", topBar.transform, 848f, 10f, 96f, 40f, "접기", font, theme.Background);

                var viewport = CreateTopLeftImage("TreeViewport", root.transform, 16f, 60f, 928f, 408f, new Color(0.025f, 0.08f, 0.13f, 1f), true);
                viewport.gameObject.AddComponent<RectMask2D>();
                var content = CreateTopLeftRect("TreeContent", viewport.transform, 0f, -56f, ContentSize.x, ContentSize.y);
                content.localScale = Vector3.one * 0.8f;
                var edgeLayer = CreateStretchRect("EdgeLayer", content);
                var nodeLayer = CreateStretchRect("NodeLayer", content);
                CreateStretchRect("SelectionLayer", content);
                var viewportController = viewport.gameObject.AddComponent<MaintenanceTreeViewport>();
                var viewportSerialized = new SerializedObject(viewportController);
                SetReference(viewportSerialized, "content", content);
                SetReference(viewportSerialized, "canvas", root.GetComponent<Canvas>());
                viewportSerialized.FindProperty("initialContentPosition").vector2Value = content.anchoredPosition;
                viewportSerialized.FindProperty("initialZoom").floatValue = 0.8f;
                viewportSerialized.ApplyModifiedPropertiesWithoutUndo();

                var tooltipOverlay = CreateStretchRect("TooltipOverlay", root.transform);
                var tooltip = (GameObject)PrefabUtility.InstantiatePrefab(tooltipPrefab, tooltipOverlay);
                tooltip.name = "Tooltip";
                var tooltipRect = tooltip.GetComponent<RectTransform>();
                tooltipRect.anchorMin = tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
                tooltipRect.pivot = new Vector2(0f, 1f);
                tooltipRect.anchoredPosition = Vector2.zero;
                tooltip.SetActive(false);

                var bottomBar = CreateTopLeftImage("BottomBar", root.transform, 0f, 468f, 960f, 72f, Color.white, false);
                ApplyConsoleArt(bottomBar, consoleButtonArt);
                CreateTopLeftSprite("WasdIcon", bottomBar.transform, 16f, 20f, 28f, 28f, LoadChrome("ControlWasd"));
                CreateTopLeftSprite("DragIcon", bottomBar.transform, 48f, 20f, 28f, 28f, LoadChrome("ControlDrag"));
                CreateTopLeftSprite("WheelIcon", bottomBar.transform, 80f, 20f, 28f, 28f, LoadChrome("ControlWheel"));
                CreateTopLeftText("ControlGuide", bottomBar.transform, 116f, 12f, 260f, 48f, "이동 · 드래그 · 확대/축소 · R 복귀", font, 13f, TextAlignmentOptions.Left);
                var fundsText = CreateTopLeftText("FundsText", bottomBar.transform, 388f, 12f, 220f, 48f, "정비 자금 100,000", font, 17f, TextAlignmentOptions.Center);
                var stageStartButton = CreateButton("StageStartButton", bottomBar.transform, 620f, 12f, 324f, 48f, "쇄빙 시작", font, theme.ActionAccent);

                var serialized = new SerializedObject(presenter);
                SetReference(serialized, "layout", layout);
                SetReference(serialized, "sourceBehaviour", null);
                SetReference(serialized, "theme", theme);
                SetReference(serialized, "content", content);
                SetReference(serialized, "edgeLayer", edgeLayer);
                SetReference(serialized, "nodeLayer", nodeLayer);
                SetReference(serialized, "viewport", viewportController);
                SetReference(serialized, "nodePrefab", nodePrefab);
                SetReference(serialized, "edgePrefab", edgePrefab);
                SetReference(serialized, "tooltipOverlay", tooltipOverlay);
                SetReference(serialized, "treeViewport", viewport.rectTransform);
                SetReference(serialized, "tooltipView", tooltip.GetComponent<MaintenanceTooltipView>());
                SetReference(serialized, "fundsText", fundsText);
                SetReference(serialized, "previewStateText", previewStateText);
                SetReference(serialized, "closeButton", closeButton);
                SetReference(serialized, "stageStartButton", stageStartButton);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, TreePrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Dictionary<string, Sprite> LoadLogicalIcons()
        {
            var icons = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var logicalId in new[]
                     {
                         "C01", "C02", "C03", "C04", "D01", "D02", "D03", "D04",
                         "S01", "S02", "S03", "H01", "H02", "H03"
                     })
            {
                var path = $"{MaintenanceTreeArtBuilder.IconFolder}/{logicalId}.png";
                var icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (icon == null)
                {
                    throw new InvalidOperationException($"Logical maintenance icon is missing: {path}");
                }

                icons.Add(logicalId, icon);
            }

            return icons;
        }

        private static Sprite LoadChrome(string name)
        {
            var sprite = MaintenanceTreeArtBuilder.LoadChrome(name);
            if (sprite == null)
            {
                throw new InvalidOperationException($"Maintenance tree chrome sprite is missing: {name}");
            }

            return sprite;
        }

        private static Dictionary<string, Vector2> CreatePositions()
        {
            return new Dictionary<string, Vector2>(StringComparer.Ordinal)
            {
                ["C01-L1"] = new Vector2(180f, 350f),
                ["C02-L1"] = new Vector2(380f, 150f), ["C02-L2"] = new Vector2(580f, 110f), ["C02-L3"] = new Vector2(780f, 110f),
                ["C03-L1"] = new Vector2(380f, 270f), ["C04-L1"] = new Vector2(580f, 250f),
                ["D01-L1"] = new Vector2(380f, 390f), ["D01-L2"] = new Vector2(580f, 390f), ["D01-L3"] = new Vector2(780f, 390f),
                ["S01-L1"] = new Vector2(580f, 510f), ["S02-L1"] = new Vector2(780f, 510f), ["S02-L2"] = new Vector2(980f, 510f), ["S03-L1"] = new Vector2(1180f, 510f),
                ["H01-L1"] = new Vector2(780f, 620f), ["H01-L2"] = new Vector2(980f, 620f), ["H01-L3"] = new Vector2(1180f, 620f),
                ["H02-L1"] = new Vector2(1180f, 780f), ["H02-L2"] = new Vector2(1380f, 780f), ["H03-L1"] = new Vector2(1380f, 620f),
                ["D02-L1"] = new Vector2(380f, 510f), ["D02-L2"] = new Vector2(580f, 730f), ["D02-L3"] = new Vector2(780f, 730f),
                ["D04-L1"] = new Vector2(580f, 840f), ["D04-L2"] = new Vector2(780f, 840f), ["D04-L3"] = new Vector2(980f, 840f),
                ["D03-L1"] = new Vector2(780f, 270f)
            };
        }

        private static string[] ExpectedStepIds()
        {
            return new[]
            {
                "C01-L1", "C02-L1", "C02-L2", "C02-L3", "C03-L1", "C04-L1",
                "D01-L1", "D01-L2", "D01-L3", "D02-L1", "D02-L2", "D02-L3",
                "D03-L1", "D04-L1", "D04-L2", "D04-L3", "S01-L1", "S02-L1",
                "S02-L2", "S03-L1", "H01-L1", "H01-L2", "H01-L3", "H02-L1",
                "H02-L2", "H03-L1"
            };
        }

        private static (string From, string To)[] EdgePairs()
        {
            return new[]
            {
                ("C01-L1", "C02-L1"), ("C02-L1", "C02-L2"), ("C02-L2", "C02-L3"),
                ("C01-L1", "C03-L1"), ("C03-L1", "C04-L1"),
                ("C01-L1", "D01-L1"), ("D01-L1", "D01-L2"), ("D01-L2", "D01-L3"),
                ("C01-L1", "D02-L1"), ("D02-L1", "D02-L2"), ("D02-L2", "D02-L3"),
                ("D01-L2", "D03-L1"), ("D02-L1", "D04-L1"), ("D04-L1", "D04-L2"), ("D04-L2", "D04-L3"),
                ("D01-L1", "S01-L1"), ("S01-L1", "S02-L1"), ("S02-L1", "S02-L2"),
                ("S01-L1", "S03-L1"), ("S02-L1", "S03-L1"),
                ("D01-L2", "H01-L1"), ("H01-L1", "H01-L2"), ("H01-L2", "H01-L3"),
                ("H01-L1", "H02-L1"), ("H02-L1", "H02-L2"), ("H01-L3", "H03-L1")
            };
        }

        private static void ValidateMinimumSpacing(MaintenanceTreeLayoutAsset layout, ICollection<string> errors)
        {
            for (var left = 0; left < layout.Nodes.Count; left++)
            {
                for (var right = left + 1; right < layout.Nodes.Count; right++)
                {
                    var distance = Vector2.Distance(layout.Nodes[left].Position, layout.Nodes[right].Position);
                    if (distance < 92f)
                    {
                        errors.Add($"Nodes {layout.Nodes[left].StepId} and {layout.Nodes[right].StepId} are only {distance:F1}px apart.");
                    }
                }
            }
        }

        private static void ValidateTreeHierarchy(GameObject tree, ICollection<string> errors)
        {
            var required = new[]
            {
                "Background", "TopBar", "TreeViewport", "TreeViewport/TreeContent",
                "TreeViewport/TreeContent/EdgeLayer", "TreeViewport/TreeContent/NodeLayer",
                "TreeViewport/TreeContent/SelectionLayer", "TooltipOverlay", "BottomBar"
            };
            foreach (var path in required)
            {
                if (tree.transform.Find(path) == null)
                {
                    errors.Add($"Tree prefab is missing {path}.");
                }
            }

            var scaler = tree.GetComponent<CanvasScaler>();
            if (scaler == null || scaler.referenceResolution != new Vector2(960f, 540f))
            {
                errors.Add("Tree prefab must use a 960x540 CanvasScaler reference resolution.");
            }
        }

        private static void ValidatePresenterReferences(GameObject tree, ICollection<string> errors)
        {
            var presenter = tree.GetComponent<MaintenanceTreePresenter>();
            if (presenter == null)
            {
                errors.Add("Tree prefab is missing MaintenanceTreePresenter.");
                return;
            }

            var serialized = new SerializedObject(presenter);
            foreach (var propertyName in new[]
                     {
                         "layout", "theme", "content", "edgeLayer", "nodeLayer",
                         "viewport", "nodePrefab", "edgePrefab", "tooltipOverlay", "treeViewport", "tooltipView",
                         "fundsText", "previewStateText", "closeButton", "stageStartButton"
                     })
            {
                var property = serialized.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                {
                    errors.Add($"Presenter reference {propertyName} is missing.");
                }
            }

            var sourceProperty = serialized.FindProperty("sourceBehaviour");
            if (sourceProperty == null || sourceProperty.objectReferenceValue != null)
            {
                errors.Add("Production Maintenance Tree must leave sourceBehaviour empty for runtime binding.");
            }

            if (tree.GetComponentInChildren<MaintenanceTreeViewport>(true) == null)
            {
                errors.Add("Tree prefab is missing its single MaintenanceTreeViewport input owner.");
            }
        }

        private static void ValidateFakeStates(ICollection<string> errors)
        {
            var gameObject = new GameObject("MaintenanceTreeFakeDataValidation");
            try
            {
                var source = gameObject.AddComponent<MaintenanceTreeFakeDataSource>();
                foreach (MaintenanceTreePreviewState state in Enum.GetValues(typeof(MaintenanceTreePreviewState)))
                {
                    source.SetPreviewState(state);
                    if (source.CurrentSteps.Count != 26)
                    {
                        errors.Add($"Fake state {state} produced {source.CurrentSteps.Count} steps.");
                    }
                }

                source.SetPreviewState(MaintenanceTreePreviewState.FullyPurchased);
                if (source.CurrentSteps.Any(step =>
                        step.PurchaseState != MaintenanceStepPurchaseState.Purchased ||
                        step.Visibility != MaintenanceStepVisibility.Visible))
                {
                    errors.Add("FullyPurchased fake state must show all 26 purchased steps.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static GameObject CreateCanvasRoot(string name, Vector2 referenceResolution)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<RectTransform>().sizeDelta = referenceResolution;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            return root;
        }

        private static RectTransform CreateTopLeftRect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static RectTransform CreateStretchRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image CreateTopLeftImage(
            string name, Transform parent, float x, float y, float width, float height, Color color, bool raycast)
        {
            var rect = CreateTopLeftRect(name, parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static Image CreateTopLeftSprite(
            string name, Transform parent, float x, float y, float width, float height, Sprite sprite)
        {
            var image = CreateTopLeftImage(name, parent, x, y, width, height, Color.white, false);
            image.sprite = sprite;
            image.preserveAspect = true;
            return image;
        }

        private static Image CreateStretchImage(string name, Transform parent, Color color, bool raycast)
        {
            var rect = CreateStretchRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static TMP_Text CreateTopLeftText(
            string name, Transform parent, float x, float y, float width, float height,
            string value, TMP_FontAsset font, float size, TextAlignmentOptions alignment)
        {
            var rect = CreateTopLeftRect(name, parent, x, y, width, height);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            ConfigureText(text, value, font, size, alignment);
            return text;
        }

        private static TMP_Text CreateCenteredText(
            string name, Transform parent, Vector2 position, Vector2 size,
            string value, TMP_FontAsset font, float fontSize, TextAlignmentOptions alignment)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            ConfigureText(text, value, font, fontSize, alignment);
            return text;
        }

        private static Image CreateCenteredImage(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Button CreateButton(
            string name, Transform parent, float x, float y, float width, float height,
            string label, TMP_FontAsset font, Color color)
        {
            var art = AssetDatabase.LoadAssetAtPath<Sprite>(ConsoleButtonArtPath);
            if (art == null)
            {
                throw new InvalidOperationException($"Maintenance console art is missing: {ConsoleButtonArtPath}");
            }

            var action = string.Equals(name, "StageStartButton", StringComparison.Ordinal);
            var image = CreateTopLeftImage(name, parent, x, y, width, height,
                action ? new Color(1f, 0.72f, 0.42f, 1f) : Color.white, true);
            ApplyConsoleArt(image, art);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(0.75f, 0.95f, 1f, 1f),
                pressedColor = new Color(0.45f, 0.7f, 0.82f, 1f),
                selectedColor = new Color(0.75f, 0.95f, 1f, 1f),
                disabledColor = new Color(0.32f, 0.42f, 0.5f, 0.65f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            var text = CreateCenteredText("Label", image.transform, Vector2.zero,
                new Vector2(width - 16f, height - 4f), label, font, 15f, TextAlignmentOptions.Center);
            text.color = action ? new Color(1f, 0.97f, 0.87f, 1f) : Color.white;
            return button;
        }

        private static void ApplyConsoleArt(Image image, Sprite art)
        {
            image.sprite = art;
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
        }

        private static void ConfigureText(
            TMP_Text text, string value, TMP_FontAsset font, float size, TextAlignmentOptions alignment)
        {
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
        }

        private static void ConfigureTopLeftAnchor(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
        }

        private static void SetReference(
            SerializedObject serialized,
            string name,
            UnityEngine.Object? value)
        {
            var property = serialized.FindProperty(name) ??
                           throw new InvalidOperationException($"Serialized field {name} was not found.");
            property.objectReferenceValue = value;
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            var segments = assetPath.Split('/');
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

        private static void ValidatePrefabExists(GameObject? prefab, string path, ICollection<string> errors)
        {
            if (prefab == null)
            {
                errors.Add($"Missing prefab: {path}");
            }
        }
    }
}
