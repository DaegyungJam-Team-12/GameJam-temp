#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Icebreaker.Gameplay;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Icebreaker.Integration.Tests
{
    public sealed class Int02SceneIntegrationTests
    {
        private const string Int02ScenePath = "Assets/01.Scenes/int02_complete_loop.unity";

        private string demoSavePath = null!;
        private byte[]? originalDemoSave;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return DestroyActiveOrchestrators();

            demoSavePath = Path.Combine(Application.persistentDataPath, "save_demo.json");
            originalDemoSave = File.Exists(demoSavePath)
                ? File.ReadAllBytes(demoSavePath)
                : null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return DestroyActiveOrchestrators();

            if (originalDemoSave != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(demoSavePath)!);
                File.WriteAllBytes(demoSavePath, originalDemoSave);
            }
            else if (File.Exists(demoSavePath))
            {
                File.Delete(demoSavePath);
            }
        }

        private static IEnumerator DestroyActiveOrchestrators()
        {
            var orchestratorType = FindType("Icebreaker.Integration.Int02IntegrationOrchestrator");
            foreach (var orchestrator in Resources.FindObjectsOfTypeAll(orchestratorType))
            {
                if (orchestrator is Component component && component.gameObject.scene.isLoaded)
                {
                    UnityEngine.Object.Destroy(component);
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator CompleteLoopScene_BindsHudAndInjectsRealStageClock()
        {
            EditorSceneManager.LoadSceneInPlayMode(
                Int02ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            for (var i = 0; i < 5; i++)
            {
                yield return null;
            }

            var orchestratorObject = GameObject.Find("INT02_Orchestrator");
            Assert.That(orchestratorObject, Is.Not.Null);
            var orchestratorType = FindType("Icebreaker.Integration.Int02IntegrationOrchestrator");
            var orchestrator = orchestratorObject!.GetComponent(orchestratorType);
            Assert.That(orchestrator, Is.Not.Null);

            var currentState = orchestratorType.GetProperty("CurrentState")!.GetValue(orchestrator);
            var phase = currentState!.GetType().GetProperty("Phase")!.GetValue(currentState)!.ToString();
            Assert.That(new[] { "Traveling", "Ready", "Completed" }, Does.Contain(phase));

            var view = UnityEngine.Object.FindFirstObjectByType<IceFieldView>();
            Assert.That(view, Is.Not.Null);
            var activeClock = typeof(IceFieldView)
                .GetField("activeClock", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(view);
            Assert.That(activeClock, Is.Not.Null);
            Assert.That(activeClock!.GetType().FullName, Is.EqualTo("Icebreaker.Core.GameLoopController"));

            var settlementObject = GameObject.Find("INT02_RewardSettlement");
            Assert.That(settlementObject, Is.Not.Null);
            var settlementType = FindType("Icebreaker.UI.Hud.RewardSettlementPresenter");
            var settlement = settlementObject!.GetComponent(settlementType);
            Assert.That(settlement, Is.Not.Null);
            var subscriptionActive = settlementType
                .GetProperty("IsEventSubscriptionActive")!
                .GetValue(settlement);
            Assert.That(subscriptionActive, Is.True);
        }

        [UnityTest]
        public IEnumerator CompleteLoopScene_InjectsSavedD04RadiusIntoTheField()
        {
            File.WriteAllText(demoSavePath, @"{
  ""saveVersion"": 1,
  ""profileId"": ""demo"",
  ""funds"": 0,
  ""maintenanceLevels"": [{ ""id"": ""D04"", ""level"": 3 }],
  ""currentDestinationIndex"": 0,
  ""destinationProgress"": 0,
  ""completedDestinationIds"": [],
  ""pendingArrivalDestinationId"": """",
  ""firstDestroyShown"": false,
  ""nextAvailableAtUtc"": """",
  ""runInProgress"": false,
  ""gameCompleted"": false,
  ""masterVolume"": 1,
  ""screenShakeEnabled"": true
}");

            EditorSceneManager.LoadSceneInPlayMode(
                Int02ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return WaitForFrames(5);

            var view = UnityEngine.Object.FindFirstObjectByType<IceFieldView>();
            Assert.That(view, Is.Not.Null);
            var directAttackConfig = typeof(IceFieldView)
                .GetField("directAttackConfig", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(view) as DirectAttackConfig;

            Assert.That(directAttackConfig, Is.Not.Null);
            Assert.That(directAttackConfig!.CursorRadiusReferencePixels, Is.EqualTo(104f));

            ClickStartButtonThroughEventSystem();
            yield return null;

            var ringRoot = typeof(IceFieldView)
                .GetField("cursorRingRoot", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(view) as Transform;
            var ringRotator = typeof(IceFieldView)
                .GetField("cursorRingRotator", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(view) as Transform;
            Assert.That(ringRoot, Is.Not.Null);
            Assert.That(ringRotator, Is.Not.Null);

            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            var expectedScale = camera!.orthographicSize *
                (directAttackConfig.CursorRadiusReferencePixels * 2f / 540f) * 2f;
            Assert.That(ringRotator!.localScale.x, Is.EqualTo(expectedScale).Within(0.0001f));
            Assert.That(ringRotator.localScale.y, Is.EqualTo(expectedScale).Within(0.0001f));

            var updateCursorRing = typeof(IceFieldView).GetMethod(
                "UpdateCursorRing",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(updateCursorRing, Is.Not.Null);
            var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            updateCursorRing!.Invoke(view, new object[] { screenCenter, true });
            var expectedPosition = camera.ScreenToWorldPoint(new Vector3(
                screenCenter.x,
                screenCenter.y,
                -camera.transform.position.z));
            Assert.That(ringRoot!.position.x, Is.EqualTo(expectedPosition.x).Within(0.0001f));
            Assert.That(ringRoot.position.y, Is.EqualTo(expectedPosition.y).Within(0.0001f));

            var rotationBefore = ringRotator.eulerAngles.z;
            yield return null;
            updateCursorRing.Invoke(view, new object[] { screenCenter, true });
            Assert.That(Mathf.DeltaAngle(rotationBefore, ringRotator.eulerAngles.z), Is.LessThan(0f));
        }

        [UnityTest]
        public IEnumerator MaintenanceScreen_PurchasesWithRealFundsAndPersistsAcrossReload()
        {
            File.WriteAllText(demoSavePath, @"{
  ""saveVersion"": 1,
  ""profileId"": ""demo"",
  ""funds"": 100,
  ""maintenanceLevels"": [],
  ""currentDestinationIndex"": 0,
  ""destinationProgress"": 0,
  ""completedDestinationIds"": [],
  ""pendingArrivalDestinationId"": """",
  ""firstDestroyShown"": false,
  ""nextAvailableAtUtc"": """",
  ""runInProgress"": false,
  ""gameCompleted"": false,
  ""masterVolume"": 1,
  ""screenShakeEnabled"": true
}");

            EditorSceneManager.LoadSceneInPlayMode(
                Int02ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return WaitForFrames(5);

            var orchestratorType = FindType("Icebreaker.Integration.Int02IntegrationOrchestrator");
            var orchestrator = FindOrchestrator(orchestratorType);
            Assert.That(GetStateValue(orchestrator, orchestratorType, "Phase").ToString(), Is.EqualTo("Ready"));

            ClickLauncherMaintenanceButton();
            yield return null;

            Assert.That(
                orchestratorType.GetProperty("CurrentManagementScreen")!.GetValue(orchestrator)!.ToString(),
                Is.EqualTo("Maintenance"));
            Assert.That(GetStateValue(orchestrator, orchestratorType, "Phase").ToString(), Is.EqualTo("Ready"));
            var launcher = GameObject.Find("INT02_LauncherHud");
            Assert.That(launcher, Is.Null, "Launcher must be hidden while maintenance is open.");
            var tree = GameObject.Find("INT02_MaintenanceTree");
            Assert.That(tree, Is.Not.Null);
            Assert.That(
                tree!.transform.Find("BottomBar/FundsText")!.GetComponent<TMPro.TMP_Text>().text,
                Does.Contain("100"));

            var presenterType = FindType("Icebreaker.UI.Maintenance.MaintenanceTreePresenter");
            var presenter = tree.GetComponent(presenterType);
            var closeButton = (Button)presenterType
                .GetField("closeButton", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(presenter)!;
            closeButton.onClick.Invoke();
            yield return null;

            Assert.That(
                orchestratorType.GetProperty("CurrentManagementScreen")!.GetValue(orchestrator)!.ToString(),
                Is.EqualTo("None"));
            Assert.That(tree.activeSelf, Is.False);
            Assert.That(GameObject.Find("INT02_LauncherHud"), Is.Not.Null);

            ClickLauncherMaintenanceButton();
            yield return null;
            Assert.That(tree.activeSelf, Is.True);

            ClickMaintenanceStep(tree, "C01-L1");
            ClickMaintenancePurchaseButton(tree);
            yield return null;

            Assert.That(GetStateValue(orchestrator, orchestratorType, "Funds"), Is.Zero);
            AssertStepState(orchestrator, orchestratorType, "C01-L1", "Purchased");
            AssertStepState(orchestrator, orchestratorType, "C02-L1", "Available");
            Assert.That(
                tree.transform.Find("BottomBar/FundsText")!.GetComponent<TMPro.TMP_Text>().text,
                Does.Contain("0"));

            EditorSceneManager.LoadSceneInPlayMode(
                Int02ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return WaitForFrames(5);

            orchestrator = FindOrchestrator(orchestratorType);
            Assert.That(GetStateValue(orchestrator, orchestratorType, "Funds"), Is.Zero);
            AssertStepState(orchestrator, orchestratorType, "C01-L1", "Purchased");

            ClickLauncherMaintenanceButton();
            yield return null;
            tree = GameObject.Find("INT02_MaintenanceTree");
            Assert.That(tree, Is.Not.Null);
            presenter = tree!.GetComponent(presenterType);
            var startButton = (Button)presenterType
                .GetField("stageStartButton", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(presenter)!;

            startButton.onClick.Invoke();
            startButton.onClick.Invoke();
            yield return null;

            Assert.That(GetStateValue(orchestrator, orchestratorType, "Phase").ToString(), Is.EqualTo("Countdown"));
            Assert.That(
                orchestratorType.GetProperty("CurrentManagementScreen")!.GetValue(orchestrator)!.ToString(),
                Is.EqualTo("None"));
            Assert.That(tree.activeSelf, Is.False);
            Assert.That(startButton.interactable, Is.False);
            var requestResult = (bool)orchestratorType
                .GetMethod("RequestManagementScreen")!
                .Invoke(orchestrator, new[]
                {
                    Enum.Parse(FindType("Icebreaker.Shared.State.ManagementScreen"), "Maintenance")
                })!;
            Assert.That(requestResult, Is.False);
            Assert.That(
                orchestratorType.GetProperty("CurrentManagementScreen")!.GetValue(orchestrator)!.ToString(),
                Is.EqualTo("None"));
        }

        [UnityTest]
        public IEnumerator StartButton_UsesRealCountdownAndCompletesTwoSavedCycles()
        {
            if (File.Exists(demoSavePath))
            {
                File.Delete(demoSavePath);
            }

            EditorSceneManager.LoadSceneInPlayMode(
                Int02ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return WaitForFrames(5);

            var orchestratorType = FindType("Icebreaker.Integration.Int02IntegrationOrchestrator");
            var orchestrator = FindOrchestrator(orchestratorType);
            yield return WaitForPhase(orchestrator, orchestratorType, "Ready", 12f);

            var view = UnityEngine.Object.FindFirstObjectByType<IceFieldView>();
            Assert.That(view, Is.Not.Null);

            ClickStartButtonThroughEventSystem();
            Assert.That(GetStateValue(orchestrator, orchestratorType, "Phase").ToString(), Is.EqualTo("Countdown"));
            yield return new WaitForSecondsRealtime(2.8f);
            Assert.That(GetStateValue(orchestrator, orchestratorType, "Phase").ToString(), Is.EqualTo("Countdown"));
            yield return WaitForPhase(orchestrator, orchestratorType, "Playing", 1f);

            DestroyOneNormalT1((IceField)view!.Source);
            AssertState(orchestrator, orchestratorType, expectedFunds: 10, expectedProgress: 1);

            yield return new WaitForSecondsRealtime(59.5f);
            Assert.That(GetStateValue(orchestrator, orchestratorType, "Phase").ToString(), Is.EqualTo("Playing"));
            yield return WaitForPhase(orchestrator, orchestratorType, "StageEnding", 1f);
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(GetStateValue(orchestrator, orchestratorType, "Phase").ToString(), Is.EqualTo("StageEnding"));
            yield return WaitForPhase(orchestrator, orchestratorType, "Settlement", 0.5f);
            yield return WaitForPhase(orchestrator, orchestratorType, "Traveling", 5f);
            yield return WaitForPhase(orchestrator, orchestratorType, "Ready", 11f);

            ClickStartButtonThroughEventSystem();
            Assert.That(GetStateValue(orchestrator, orchestratorType, "Phase").ToString(), Is.EqualTo("Countdown"));
            yield return new WaitForSecondsRealtime(2.8f);
            Assert.That(GetStateValue(orchestrator, orchestratorType, "Phase").ToString(), Is.EqualTo("Countdown"));
            yield return WaitForPhase(orchestrator, orchestratorType, "Playing", 1f);

            DestroyOneNormalT1((IceField)view.Source);
            AssertState(orchestrator, orchestratorType, expectedFunds: 20, expectedProgress: 2);

            yield return new WaitForSecondsRealtime(59.5f);
            Assert.That(GetStateValue(orchestrator, orchestratorType, "Phase").ToString(), Is.EqualTo("Playing"));
            yield return WaitForPhase(orchestrator, orchestratorType, "StageEnding", 1f);
            yield return WaitForPhase(orchestrator, orchestratorType, "Settlement", 1.5f);
            yield return WaitForPhase(orchestrator, orchestratorType, "Traveling", 5f);

            EditorSceneManager.LoadSceneInPlayMode(
                Int02ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return WaitForFrames(5);

            orchestrator = FindOrchestrator(orchestratorType);
            Assert.That(GetStateValue(orchestrator, orchestratorType, "Phase").ToString(), Is.EqualTo("Traveling"));
            AssertState(orchestrator, orchestratorType, expectedFunds: 20, expectedProgress: 2);

            var settlementObject = GameObject.Find("INT02_RewardSettlement");
            var settlementType = FindType("Icebreaker.UI.Hud.RewardSettlementPresenter");
            var settlement = settlementObject!.GetComponent(settlementType);
            Assert.That(settlementType.GetProperty("IsSettlementVisible")!.GetValue(settlement), Is.False);
        }

        private static IEnumerator WaitForFrames(int frameCount)
        {
            for (var i = 0; i < frameCount; i++)
            {
                yield return null;
            }
        }

        private static Component FindOrchestrator(Type orchestratorType)
        {
            var orchestratorObject = GameObject.Find("INT02_Orchestrator");
            Assert.That(orchestratorObject, Is.Not.Null);
            var orchestrator = orchestratorObject!.GetComponent(orchestratorType);
            Assert.That(orchestrator, Is.Not.Null);
            return orchestrator!;
        }

        private static IEnumerator WaitForPhase(
            Component orchestrator,
            Type orchestratorType,
            string expectedPhase,
            float timeoutSeconds)
        {
            var timeoutAt = Time.realtimeSinceStartup + timeoutSeconds;
            while (GetStateValue(orchestrator, orchestratorType, "Phase").ToString() != expectedPhase &&
                   Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(
                GetStateValue(orchestrator, orchestratorType, "Phase").ToString(),
                Is.EqualTo(expectedPhase),
                $"Timed out waiting {timeoutSeconds:F1}s for {expectedPhase}.");
        }

        private static object GetStateValue(Component orchestrator, Type orchestratorType, string propertyName)
        {
            var state = orchestratorType.GetProperty("CurrentState")!.GetValue(orchestrator);
            return state!.GetType().GetProperty(propertyName)!.GetValue(state)!;
        }

        private static void AssertState(
            Component orchestrator,
            Type orchestratorType,
            long expectedFunds,
            int expectedProgress)
        {
            Assert.That(GetStateValue(orchestrator, orchestratorType, "Funds"), Is.EqualTo(expectedFunds));
            Assert.That(GetStateValue(orchestrator, orchestratorType, "DestinationProgress"), Is.EqualTo(expectedProgress));
        }

        private static void ClickStartButtonThroughEventSystem()
        {
            var launcherObject = GameObject.Find("INT02_LauncherHud");
            Assert.That(launcherObject, Is.Not.Null);
            var launcherType = FindType("Icebreaker.UI.Hud.LauncherHudPresenter");
            var launcher = launcherObject!.GetComponent(launcherType);
            var startButton = (Button)launcherType
                .GetField("startButton", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(launcher)!;
            Assert.That(startButton.interactable, Is.True);

            var eventSystem = EventSystem.current;
            Assert.That(eventSystem, Is.Not.Null);
            var canvas = startButton.GetComponentInParent<Canvas>();
            var eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var buttonRect = (RectTransform)startButton.transform;
            var screenPosition = RectTransformUtility.WorldToScreenPoint(
                eventCamera,
                buttonRect.TransformPoint(buttonRect.rect.center));
            var pointer = new PointerEventData(eventSystem!)
            {
                button = PointerEventData.InputButton.Left,
                position = screenPosition,
            };
            var raycastResults = new List<RaycastResult>();
            eventSystem!.RaycastAll(pointer, raycastResults);
            var hit = raycastResults.FirstOrDefault(result =>
                result.gameObject == startButton.gameObject ||
                result.gameObject.transform.IsChildOf(startButton.transform));
            Assert.That(hit.gameObject, Is.Not.Null, "The visible start button was not hit by the EventSystem raycast.");

            ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            var clickReceiver = ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(clickReceiver, Is.EqualTo(startButton.gameObject));
        }

        private static void ClickLauncherMaintenanceButton()
        {
            var launcherObject = GameObject.Find("INT02_LauncherHud");
            Assert.That(launcherObject, Is.Not.Null);
            var launcherType = FindType("Icebreaker.UI.Hud.LauncherHudPresenter");
            var launcher = launcherObject!.GetComponent(launcherType);
            var button = (Button)launcherType
                .GetField("maintenanceButton", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(launcher)!;
            button.onClick.Invoke();
        }

        private static void ClickMaintenanceStep(GameObject tree, string stepId)
        {
            var viewportType = FindType("Icebreaker.UI.Maintenance.MaintenanceTreeViewport");
            var viewport = tree.GetComponentInChildren(viewportType, true);
            Assert.That(viewport, Is.Not.Null);
            var eventSystem = EventSystem.current;
            Assert.That(eventSystem, Is.Not.Null);
            var down = new PointerEventData(eventSystem!)
            {
                pointerId = 1,
                position = Vector2.zero
            };
            var up = new PointerEventData(eventSystem)
            {
                pointerId = 1,
                position = new Vector2(2f, 0f)
            };
            viewportType.GetMethod("ProcessPointerDown")!
                .Invoke(viewport, new object[] { down, stepId });
            viewportType.GetMethod("ProcessPointerUp")!
                .Invoke(viewport, new object[] { up, stepId });
        }

        private static void ClickMaintenancePurchaseButton(GameObject tree)
        {
            var button = tree.transform
                .Find("TooltipOverlay/Tooltip/PurchaseButton")!
                .GetComponent<Button>();
            button.onClick.Invoke();
        }

        private static void AssertStepState(
            Component orchestrator,
            Type orchestratorType,
            string stepId,
            string expectedState)
        {
            var steps = (IEnumerable)orchestratorType
                .GetProperty("CurrentSteps")!
                .GetValue(orchestrator)!;
            var step = steps.Cast<object>().Single(candidate =>
                candidate.GetType().GetProperty("StepId")!.GetValue(candidate)!.ToString() == stepId);
            Assert.That(
                step.GetType().GetProperty("PurchaseState")!.GetValue(step)!.ToString(),
                Is.EqualTo(expectedState));
        }

        private static void DestroyOneNormalT1(IceField field)
        {
            var target = field.ActiveIce.First(ice =>
                ice.Tier == IceTier.T1 && ice.SpecialType == SpecialIceType.None);
            var targetId = target.IceInstanceId;

            for (var i = 0; i < 10 && field.ActiveIce.Any(ice => ice.IceInstanceId == targetId); i++)
            {
                Assert.That(
                    field.ApplyClickAt(target.ReferencePosition, 1f, EffectType.Click, Time.timeAsDouble),
                    Is.True);
            }

            Assert.That(field.ActiveIce.Any(ice => ice.IceInstanceId == targetId), Is.False);
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

            throw new InvalidOperationException($"Type was not loaded: {fullName}");
        }
    }
}
