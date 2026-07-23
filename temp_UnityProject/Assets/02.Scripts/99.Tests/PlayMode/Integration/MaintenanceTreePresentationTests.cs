#nullable enable

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Icebreaker.Shared.Maintenance;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Icebreaker.Integration.Tests
{
    public sealed class MaintenanceTreePresentationTests
    {
        private const string TreePrefabPath =
            "Assets/03.Prefabs/30.UI/Maintenance/UI_MaintenanceTree.prefab";
        private const string TooltipPrefabPath =
            "Assets/03.Prefabs/30.UI/Maintenance/UI_MaintenanceTooltip.prefab";
        private const string NodePrefabPath =
            "Assets/03.Prefabs/30.UI/Maintenance/UI_MaintenanceNode.prefab";
        private const string EdgePrefabPath =
            "Assets/03.Prefabs/30.UI/Maintenance/UI_MaintenanceEdge.prefab";
        private const string LayoutPath =
            "Assets/09.Data/UI/MaintenanceTreeLayout.asset";

        [Test]
        public void ArtAssets_ReuseFourteenLogicalIconsAndProvideNonColorStateCues()
        {
            var layout = AssetDatabase.LoadAssetAtPath<MaintenanceTreeLayoutAsset>(LayoutPath);
            Assert.That(layout, Is.Not.Null);
            Assert.That(layout!.Nodes, Has.Count.EqualTo(26));
            Assert.That(layout.Nodes.Select(node => node.Icon).Distinct().Count(), Is.EqualTo(14));
            foreach (var logicalGroup in layout.Nodes.GroupBy(node => node.StepId[..3]))
            {
                Assert.That(logicalGroup.Select(node => node.Icon).Distinct().Count(), Is.EqualTo(1));
                Assert.That(
                    AssetDatabase.GetAssetPath(logicalGroup.First().Icon),
                    Is.EqualTo($"Assets/04.Images/30.UI/Maintenance/Icons/{logicalGroup.Key}.png"));
            }

            var node = AssetDatabase.LoadAssetAtPath<GameObject>(NodePrefabPath);
            Assert.That(node, Is.Not.Null);
            foreach (var childName in new[]
                     {
                         "SelectionFrame", "Frame", "StateFrame", "Icon",
                         "CheckIndicator", "LockIndicator"
                     })
            {
                Assert.That(node!.transform.Find(childName), Is.Not.Null, childName);
            }

            var nodeView = node!.GetComponent(FindType("Icebreaker.UI.Maintenance.MaintenanceNodeView"));
            AssertSpriteReferences(
                nodeView,
                "rootFrameSprite", "normalFrameSprite", "purchasedFrameSprite",
                "availableFrameSprite", "lockedFrameSprite", "previewFrameSprite");

            var edge = AssetDatabase.LoadAssetAtPath<GameObject>(EdgePrefabPath);
            Assert.That(edge, Is.Not.Null);
            var edgeView = edge!.GetComponent(FindType("Icebreaker.UI.Maintenance.MaintenanceTreeEdgeView"));
            AssertSpriteReferences(edgeView, "defaultSprite", "litSprite");

            var tooltip = AssetDatabase.LoadAssetAtPath<GameObject>(TooltipPrefabPath);
            Assert.That(tooltip!.GetComponent<Image>().type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(tooltip.transform.Find("PurchaseButton"), Is.Null);
            Assert.That(
                tooltip.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget),
                Is.True,
                "The hover-only tooltip must not intercept node or viewport input.");
            var tree = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPath);
            var bottomBar = tree!.transform.Find("BottomBar")!;
            Assert.That(bottomBar.GetComponent<Image>().type, Is.EqualTo(Image.Type.Sliced));
            foreach (var controlName in new[] { "WasdIcon", "DragIcon", "WheelIcon" })
            {
                Assert.That(bottomBar.Find(controlName)!.GetComponent<Image>().sprite, Is.Not.Null);
            }

            AssertNoMissingComponents(node, edge, tooltip, tree);
        }

        [UnityTest]
        public IEnumerator StaticTree_RendersFiveInitialAndAllFullyPurchasedNodesWithoutRaycastsOnHidden()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var instance = UnityEngine.Object.Instantiate(prefab!);

            try
            {
                yield return null;

                var presenterType = FindType("Icebreaker.UI.Maintenance.MaintenanceTreePresenter");
                var sourceType = FindType("Icebreaker.UI.Maintenance.MaintenanceTreeFakeDataSource");
                var previewStateType = FindType("Icebreaker.UI.Maintenance.MaintenanceTreePreviewState");
                var presenter = instance.GetComponent(presenterType);
                var source = instance.GetComponent(sourceType);
                Assert.That(presenter, Is.Not.Null);
                Assert.That(source, Is.Not.Null);

                var nodeLayer = instance.transform.Find("TreeViewport/TreeContent/NodeLayer");
                Assert.That(nodeLayer, Is.Not.Null);
                Assert.That(nodeLayer!.childCount, Is.EqualTo(26));
                Assert.That(CountActiveChildren(nodeLayer), Is.EqualTo(5));
                Assert.That(GetVisibleNodeCount(presenter!, presenterType), Is.EqualTo(5));
                AssertInitialNodesInsideViewport(instance, nodeLayer);
                AssertTextFits(instance.transform.Find("TopBar")!);
                AssertTextFits(instance.transform.Find("BottomBar")!);

                foreach (Transform node in nodeLayer)
                {
                    if (node.gameObject.activeSelf)
                    {
                        continue;
                    }

                    Assert.That(
                        node.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget),
                        Is.False,
                        $"Hidden node {node.name} must not receive raycasts.");
                    Assert.That(node.GetComponent<CanvasGroup>()!.blocksRaycasts, Is.False);
                }

                foreach (var edgeImage in instance.transform
                             .Find("TreeViewport/TreeContent/EdgeLayer")!
                             .GetComponentsInChildren<Image>())
                {
                    Assert.That(edgeImage.color, Is.EqualTo(Color.white));
                    Assert.That(edgeImage.sprite.name, Is.EqualTo("EdgeDefault"));
                }

                var fullyPurchased = Enum.Parse(previewStateType, "FullyPurchased");
                sourceType.GetMethod("SetPreviewState")!.Invoke(source, new[] { fullyPurchased });
                yield return null;

                Assert.That(nodeLayer.childCount, Is.EqualTo(26));
                Assert.That(CountActiveChildren(nodeLayer), Is.EqualTo(26));
                Assert.That(GetVisibleNodeCount(presenter!, presenterType), Is.EqualTo(26));
                foreach (Transform node in nodeLayer)
                {
                    Assert.That(node.Find("StateFrame")!.GetComponent<Image>().sprite.name, Is.EqualTo("StatePurchased"));
                    Assert.That(node.Find("CheckIndicator")!.gameObject.activeSelf, Is.True);
                    Assert.That(node.Find("LockIndicator")!.gameObject.activeSelf, Is.False);
                }

                foreach (var edgeImage in instance.transform
                             .Find("TreeViewport/TreeContent/EdgeLayer")!
                             .GetComponentsInChildren<Image>())
                {
                    Assert.That(edgeImage.color, Is.EqualTo(Color.white));
                    Assert.That(edgeImage.sprite.name, Is.EqualTo("EdgeLit"));
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(instance);
            }
        }

        [UnityTest]
        public IEnumerator KoreanTooltip_TextFitsItsFixedOverlayCards()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TooltipPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var instance = UnityEngine.Object.Instantiate(prefab!);

            try
            {
                yield return null;
                foreach (var text in instance.GetComponentsInChildren<TMP_Text>(true))
                {
                    AssertTextFits(text);
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(instance);
            }
        }

        [UnityTest]
        public IEnumerator HoverTooltip_OnlyExactSameNodeDoubleClickRequestsPurchase()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var instance = UnityEngine.Object.Instantiate(prefab!);
            var eventSystemObject = new GameObject("MaintenanceTreeTestEventSystem");
            var eventSystem = eventSystemObject.AddComponent<EventSystem>();

            try
            {
                yield return null;

                var presenterType = FindType("Icebreaker.UI.Maintenance.MaintenanceTreePresenter");
                var viewportType = FindType("Icebreaker.UI.Maintenance.MaintenanceTreeViewport");
                var sourceType = FindType("Icebreaker.UI.Maintenance.MaintenanceTreeFakeDataSource");
                var previewStateType = FindType("Icebreaker.UI.Maintenance.MaintenanceTreePreviewState");
                var presenter = instance.GetComponent(presenterType)!;
                var viewport = instance.GetComponentInChildren(viewportType, true)!;
                var source = instance.GetComponent(sourceType)!;
                var requests = new System.Collections.Generic.List<string>();
                Action<string> handler = requests.Add;
                presenterType.GetEvent("PurchaseRequested")!.AddEventHandler(presenter, handler);

                var tooltip = instance.transform.Find("TooltipOverlay/Tooltip");
                Assert.That(tooltip, Is.Not.Null);
                Assert.That(tooltip!.gameObject.activeSelf, Is.False);

                InvokeHover(viewportType, viewport, "ProcessPointerEnter", "C01-L1");
                Assert.That(tooltip.gameObject.activeSelf, Is.True);
                Assert.That(
                    instance.transform.Find("TreeViewport/TreeContent/NodeLayer/Node_C01-L1/SelectionFrame")!
                        .gameObject.activeSelf,
                    Is.True);

                InvokeHover(viewportType, viewport, "ProcessPointerEnter", "C02-L1");
                InvokeHover(viewportType, viewport, "ProcessPointerExit", "C01-L1");
                Assert.That(tooltip.gameObject.activeSelf, Is.True, "An older node exit must not hide the current hover tooltip.");
                Assert.That(
                    instance.transform.Find("TreeViewport/TreeContent/NodeLayer/Node_C02-L1/SelectionFrame")!
                        .gameObject.activeSelf,
                    Is.True);

                InvokeHover(viewportType, viewport, "ProcessPointerExit", "C02-L1");
                Assert.That(tooltip.gameObject.activeSelf, Is.False, "Hover exit must hide the tooltip in the same frame.");

                ClickStep(viewportType, viewport, eventSystem, 1, "C02-L1", Vector2.zero, new Vector2(2f, 0f), 2);
                Assert.That(requests, Is.Empty, "A node that cannot be purchased must not request a purchase.");

                ClickStep(viewportType, viewport, eventSystem, 2, "C01-L1", Vector2.zero, new Vector2(4f, 0f), 1);
                Assert.That(requests, Is.Empty, "A single click must not purchase.");
                Assert.That(tooltip.gameObject.activeSelf, Is.False, "A single click must not latch the tooltip.");

                ClickStep(viewportType, viewport, eventSystem, 3, "C01-L1", Vector2.zero, new Vector2(4f, 0f), 3);
                Assert.That(requests, Is.Empty, "Only clickCount exactly equal to two can purchase.");

                InvokePointer(viewportType, viewport, "ProcessPointerDown", Pointer(eventSystem, 4, Vector2.zero), "C01-L1");
                InvokePointer(viewportType, viewport, "ProcessPointerUp", Pointer(eventSystem, 4, new Vector2(4f, 0f), 2), "C02-L1");
                Assert.That(requests, Is.Empty, "A down/up node mismatch must not purchase.");

                InvokeHover(viewportType, viewport, "ProcessPointerEnter", "C01-L1");
                InvokePointer(viewportType, viewport, "ProcessPointerDown", Pointer(eventSystem, 5, Vector2.zero), "C01-L1");
                var drag = Pointer(eventSystem, 5, new Vector2(20f, 0f));
                drag.delta = new Vector2(20f, 0f);
                viewportType.GetMethod("ProcessPointerDrag")!.Invoke(viewport, new object[] { drag });
                InvokeHover(viewportType, viewport, "ProcessPointerEnter", "C02-L1");
                InvokeHover(viewportType, viewport, "ProcessPointerExit", "C01-L1");
                Assert.That(tooltip.gameObject.activeSelf, Is.True);
                Assert.That(
                    instance.transform.Find("TreeViewport/TreeContent/NodeLayer/Node_C01-L1/SelectionFrame")!
                        .gameObject.activeSelf,
                    Is.True,
                    "Crossing nodes while dragging must not change the hover highlight.");
                InvokePointer(viewportType, viewport, "ProcessPointerUp", Pointer(eventSystem, 5, new Vector2(20f, 0f), 2), "C01-L1");
                Assert.That(requests, Is.Empty, "An 8px-or-more drag must not purchase.");
                Assert.That(tooltip.gameObject.activeSelf, Is.False, "Ending a drag must clear stale hover UI.");
                Assert.That(
                    instance.transform.Find("TreeViewport/TreeContent/NodeLayer/Node_C01-L1/SelectionFrame")!
                        .gameObject.activeSelf,
                    Is.False);

                InvokePointer(viewportType, viewport, "ProcessPointerDown", Pointer(eventSystem, 6, Vector2.zero), "C01-L1");
                ((Behaviour)viewport).enabled = false;
                ((Behaviour)viewport).enabled = true;
                InvokePointer(viewportType, viewport, "ProcessPointerUp", Pointer(eventSystem, 6, new Vector2(2f, 0f), 2), "C01-L1");
                Assert.That(requests, Is.Empty, "Disable must clear stale pointer state.");

                ClickStep(viewportType, viewport, eventSystem, 7, "C01-L1", Vector2.zero, new Vector2(7f, 0f), 2);
                Assert.That(requests, Is.EqualTo(new[] { "C01-L1" }));
                ClickStep(viewportType, viewport, eventSystem, 8, "C01-L1", Vector2.zero, new Vector2(4f, 0f), 2);
                Assert.That(requests, Has.Count.EqualTo(1), "A pending Step must ignore duplicate double-clicks.");

                var c01Purchased = Enum.Parse(previewStateType, "C01Purchased");
                sourceType.GetMethod("SetPreviewState")!.Invoke(source, new[] { c01Purchased });
                yield return null;

                ClickStep(viewportType, viewport, eventSystem, 9, "C02-L1", Vector2.zero, new Vector2(4f, 0f), 3);
                Assert.That(requests, Is.EqualTo(new[] { "C01-L1" }));
                ClickStep(viewportType, viewport, eventSystem, 10, "C02-L1", Vector2.zero, new Vector2(8f, 0f), 2);
                Assert.That(requests, Is.EqualTo(new[] { "C01-L1" }), "Exactly 8px must be treated as a drag.");
                ClickStep(viewportType, viewport, eventSystem, 11, "C02-L1", Vector2.zero, new Vector2(4f, 0f), 2);
                Assert.That(requests, Is.EqualTo(new[] { "C01-L1", "C02-L1" }));

                InvokeHover(viewportType, viewport, "ProcessPointerEnter", "C02-L1");
                viewportType.GetMethod("ApplyZoomAtPointer")!.Invoke(
                    viewport,
                    new object[] { 1f, new Vector2(400f, -200f) });
                Assert.That(tooltip!.localScale, Is.EqualTo(Vector3.one));
                Assert.That(tooltip.gameObject.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.Destroy(instance);
                UnityEngine.Object.Destroy(eventSystemObject);
            }
        }

        [UnityTest]
        public IEnumerator DisablingTheTree_ClearsHoverBeforeTheSameNodeCanBeEnteredAgain()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var instance = UnityEngine.Object.Instantiate(prefab!);

            try
            {
                yield return null;

                var viewportType = FindType("Icebreaker.UI.Maintenance.MaintenanceTreeViewport");
                var viewport = instance.GetComponentInChildren(viewportType, true)!;
                var tooltip = instance.transform.Find("TooltipOverlay/Tooltip")!;

                InvokeHover(viewportType, viewport, "ProcessPointerEnter", "C01-L1");
                Assert.That(tooltip.gameObject.activeSelf, Is.True);

                instance.SetActive(false);
                instance.SetActive(true);
                yield return null;

                Assert.That(tooltip.gameObject.activeSelf, Is.False);
                Assert.That(
                    instance.transform.Find("TreeViewport/TreeContent/NodeLayer/Node_C01-L1/SelectionFrame")!
                        .gameObject.activeSelf,
                    Is.False);

                InvokeHover(viewportType, viewport, "ProcessPointerEnter", "C01-L1");
                Assert.That(tooltip.gameObject.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.Destroy(instance);
            }
        }

        [UnityTest]
        public IEnumerator Tooltip_PreservesVerticalOffsetAtCenterAndBottomNodesAcrossZoomAndPan()
        {
            const float normalTooltipVerticalOffset = 28f;
            const float bottomRowTooltipVerticalOffset = 68f;
            var layout = AssetDatabase.LoadAssetAtPath<MaintenanceTreeLayoutAsset>(LayoutPath);
            Assert.That(layout, Is.Not.Null);
            var maximumY = layout!.Nodes.Max(node => node.Position.y);
            var bottomRowStepIds = layout.Nodes
                .Where(node => Mathf.Approximately(node.Position.y, maximumY))
                .Select(node => node.StepId)
                .ToArray();
            Assert.That(bottomRowStepIds, Is.EquivalentTo(new[] { "D04-L1", "D04-L2", "D04-L3" }));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var instance = UnityEngine.Object.Instantiate(prefab!);

            try
            {
                yield return null;

                var viewportType = FindType("Icebreaker.UI.Maintenance.MaintenanceTreeViewport");
                var sourceType = FindType("Icebreaker.UI.Maintenance.MaintenanceTreeFakeDataSource");
                var previewStateType = FindType("Icebreaker.UI.Maintenance.MaintenanceTreePreviewState");
                var viewport = instance.GetComponentInChildren(viewportType, true)!;
                var source = instance.GetComponent(sourceType)!;
                sourceType.GetMethod("SetPreviewState")!.Invoke(
                    source,
                    new[] { Enum.Parse(previewStateType, "FullyPurchased") });
                yield return null;

                var viewportRect = (RectTransform)instance.transform.Find("TreeViewport")!;
                var content = (RectTransform)instance.transform.Find("TreeViewport/TreeContent")!;
                var tooltipOverlay = (RectTransform)instance.transform.Find("TooltipOverlay")!;
                var tooltip = (RectTransform)instance.transform.Find("TooltipOverlay/Tooltip")!;
                foreach (var zoom in new[] { 0.8f, 1f, 1.25f })
                {
                    foreach (var position in new[] { Vector2.zero, new Vector2(-9999f, 9999f) })
                    {
                        content.localScale = Vector3.one * zoom;
                        content.anchoredPosition = position;
                        viewportType.GetMethod("ClampNow")!.Invoke(viewport, null);
                        Canvas.ForceUpdateCanvases();

                        var centerNode = (RectTransform)instance.transform.Find(
                            "TreeViewport/TreeContent/NodeLayer/Node_C01-L1")!;
                        var centerAnchor = (Vector2)tooltipOverlay.InverseTransformPoint(centerNode.position);
                        InvokeHover(viewportType, viewport, "ProcessPointerEnter", "C01-L1");
                        Assert.That(
                            tooltip.anchoredPosition.y,
                            Is.EqualTo(centerAnchor.y + normalTooltipVerticalOffset).Within(0.01f),
                            "C01-L1");
                        Assert.That(tooltip.localScale, Is.EqualTo(Vector3.one));
                        InvokeHover(viewportType, viewport, "ProcessPointerExit", "C01-L1");

                        foreach (var stepId in bottomRowStepIds)
                        {
                            var node = (RectTransform)instance.transform.Find(
                                "TreeViewport/TreeContent/NodeLayer/Node_" + stepId)!;
                            var anchor = (Vector2)tooltipOverlay.InverseTransformPoint(node.position);
                            InvokeHover(viewportType, viewport, "ProcessPointerEnter", stepId);
                            Assert.That(
                                tooltip.anchoredPosition.y,
                                Is.EqualTo(anchor.y + bottomRowTooltipVerticalOffset).Within(0.01f),
                                stepId);
                            Assert.That(tooltip.localScale, Is.EqualTo(Vector3.one));
                            InvokeHover(viewportType, viewport, "ProcessPointerExit", stepId);
                        }
                    }
                }

                content.localScale = Vector3.one * 0.8f;
                content.anchoredPosition = Vector2.zero;
                viewportType.GetMethod("ClampNow")!.Invoke(viewport, null);
                Canvas.ForceUpdateCanvases();

                var rightNode = (RectTransform)instance.transform.Find(
                    "TreeViewport/TreeContent/NodeLayer/Node_H02-L1")!;
                var rightAnchor = (Vector2)tooltipOverlay.InverseTransformPoint(rightNode.position);
                InvokeHover(viewportType, viewport, "ProcessPointerEnter", "H02-L1");
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewportRect, tooltip);
                Assert.That(tooltip.anchoredPosition.x, Is.LessThan(rightAnchor.x), "H02-L1");
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(viewportRect.rect.xMin + 15.5f));
                Assert.That(bounds.max.x, Is.LessThanOrEqualTo(viewportRect.rect.xMax - 15.5f));
                InvokeHover(viewportType, viewport, "ProcessPointerExit", "H02-L1");
            }
            finally
            {
                UnityEngine.Object.Destroy(instance);
            }
        }

        private static int CountActiveChildren(Transform parent)
        {
            var count = 0;
            foreach (Transform child in parent)
            {
                if (child.gameObject.activeSelf)
                {
                    count++;
                }
            }

            return count;
        }

        private static PointerEventData Pointer(
            EventSystem eventSystem,
            int pointerId,
            Vector2 position,
            int clickCount = 0)
        {
            return new PointerEventData(eventSystem)
            {
                pointerId = pointerId,
                position = position,
                clickCount = clickCount
            };
        }

        private static void ClickStep(
            Type viewportType,
            Component viewport,
            EventSystem eventSystem,
            int pointerId,
            string stepId,
            Vector2 downPosition,
            Vector2 upPosition,
            int clickCount)
        {
            InvokePointer(viewportType, viewport, "ProcessPointerDown", Pointer(eventSystem, pointerId, downPosition), stepId);
            InvokePointer(viewportType, viewport, "ProcessPointerUp", Pointer(eventSystem, pointerId, upPosition, clickCount), stepId);
        }

        private static void InvokeHover(
            Type viewportType,
            Component viewport,
            string methodName,
            string stepId)
        {
            viewportType.GetMethod(methodName)!.Invoke(viewport, new object[] { stepId });
        }

        private static void InvokePointer(
            Type viewportType,
            Component viewport,
            string methodName,
            PointerEventData pointer,
            string stepId)
        {
            viewportType.GetMethod(methodName)!.Invoke(viewport, new object[] { pointer, stepId });
        }

        private static void AssertInitialNodesInsideViewport(GameObject instance, Transform nodeLayer)
        {
            var viewport = (RectTransform)instance.transform.Find("TreeViewport")!;
            foreach (Transform node in nodeLayer)
            {
                if (!node.gameObject.activeSelf)
                {
                    continue;
                }

                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    viewport,
                    (RectTransform)node);
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(viewport.rect.xMin - 0.5f), node.name);
                Assert.That(bounds.max.x, Is.LessThanOrEqualTo(viewport.rect.xMax + 0.5f), node.name);
                Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(viewport.rect.yMin - 0.5f), node.name);
                Assert.That(bounds.max.y, Is.LessThanOrEqualTo(viewport.rect.yMax + 0.5f), node.name);
            }
        }

        private static void AssertTextFits(Transform root)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                AssertTextFits(text);
            }
        }

        private static void AssertTextFits(TMP_Text text)
        {
            text.ForceMeshUpdate();
            var preferred = text.GetPreferredValues(
                text.text,
                text.rectTransform.rect.width,
                1000f);
            Assert.That(
                preferred.y,
                Is.LessThanOrEqualTo(text.rectTransform.rect.height + 0.5f),
                $"{text.name} Korean text is vertically clipped.");
        }

        private static int GetVisibleNodeCount(Component presenter, Type presenterType)
        {
            return (int)presenterType.GetProperty("VisibleNodeCount")!.GetValue(presenter)!;
        }

        private static void AssertSpriteReferences(Component component, params string[] propertyNames)
        {
            Assert.That(component, Is.Not.Null);
            var serialized = new SerializedObject(component);
            foreach (var propertyName in propertyNames)
            {
                Assert.That(
                    serialized.FindProperty(propertyName)!.objectReferenceValue,
                    Is.Not.Null,
                    propertyName);
            }
        }

        private static void AssertNoMissingComponents(params GameObject[] prefabs)
        {
            foreach (var prefab in prefabs)
            {
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                {
                    Assert.That(
                        transform.GetComponents<Component>().Any(component => component == null),
                        Is.False,
                        $"{prefab.name}/{transform.name} has a missing component reference.");
                }
            }
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            throw new AssertionException($"Type {fullName} was not found.");
        }
    }
}
