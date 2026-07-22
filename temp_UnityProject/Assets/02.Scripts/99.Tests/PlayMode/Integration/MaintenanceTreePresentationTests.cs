#nullable enable

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
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

                var fullyPurchased = Enum.Parse(previewStateType, "FullyPurchased");
                sourceType.GetMethod("SetPreviewState")!.Invoke(source, new[] { fullyPurchased });
                yield return null;

                Assert.That(nodeLayer.childCount, Is.EqualTo(26));
                Assert.That(CountActiveChildren(nodeLayer), Is.EqualTo(26));
                Assert.That(GetVisibleNodeCount(presenter!, presenterType), Is.EqualTo(26));
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
        public IEnumerator ShortClick_RequestsOnceWhileDragDisableAndDuplicateInputDoNotPurchase()
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

                InvokePointer(viewportType, viewport, "ProcessPointerDown", Pointer(eventSystem, 1, Vector2.zero), "C01-L1");
                InvokePointer(viewportType, viewport, "ProcessPointerUp", Pointer(eventSystem, 1, new Vector2(4f, 0f)), "C01-L1");
                Assert.That(requests, Is.EqualTo(new[] { "C01-L1" }));

                InvokePointer(viewportType, viewport, "ProcessPointerDown", Pointer(eventSystem, 2, Vector2.zero), "C01-L1");
                InvokePointer(viewportType, viewport, "ProcessPointerUp", Pointer(eventSystem, 2, Vector2.zero), "C01-L1");
                Assert.That(requests, Has.Count.EqualTo(1), "A pending Step must ignore duplicate clicks.");

                var c01Purchased = Enum.Parse(previewStateType, "C01Purchased");
                sourceType.GetMethod("SetPreviewState")!.Invoke(source, new[] { c01Purchased });
                yield return null;

                InvokePointer(viewportType, viewport, "ProcessPointerDown", Pointer(eventSystem, 3, Vector2.zero), "C02-L1");
                var drag = Pointer(eventSystem, 3, new Vector2(20f, 0f));
                drag.delta = new Vector2(20f, 0f);
                viewportType.GetMethod("ProcessPointerDrag")!.Invoke(viewport, new object[] { drag });
                InvokePointer(viewportType, viewport, "ProcessPointerUp", drag, "C02-L1");
                Assert.That(requests, Has.Count.EqualTo(1), "A 20px drag must not purchase.");

                InvokePointer(viewportType, viewport, "ProcessPointerDown", Pointer(eventSystem, 4, Vector2.zero), "C02-L1");
                ((Behaviour)viewport).enabled = false;
                ((Behaviour)viewport).enabled = true;
                InvokePointer(viewportType, viewport, "ProcessPointerUp", Pointer(eventSystem, 4, Vector2.zero), "C02-L1");
                Assert.That(requests, Has.Count.EqualTo(1), "Disable must clear stale pointer state.");

                InvokePointer(viewportType, viewport, "ProcessPointerDown", Pointer(eventSystem, 5, Vector2.zero), "C02-L1");
                InvokePointer(viewportType, viewport, "ProcessPointerUp", Pointer(eventSystem, 5, Vector2.zero), "C02-L1");
                Assert.That(requests, Is.EqualTo(new[] { "C01-L1", "C02-L1" }));

                viewportType.GetMethod("ApplyZoomAtPointer")!.Invoke(
                    viewport,
                    new object[] { 1f, new Vector2(400f, -200f) });
                var tooltip = instance.transform.Find("TooltipOverlay/Tooltip");
                Assert.That(tooltip, Is.Not.Null);
                Assert.That(tooltip!.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                UnityEngine.Object.Destroy(instance);
                UnityEngine.Object.Destroy(eventSystemObject);
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
            Vector2 position)
        {
            return new PointerEventData(eventSystem)
            {
                pointerId = pointerId,
                position = position
            };
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
