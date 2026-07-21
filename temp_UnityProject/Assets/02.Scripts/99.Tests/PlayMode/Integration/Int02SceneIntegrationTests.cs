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

        [SetUp]
        public void SetUp()
        {
            demoSavePath = Path.Combine(Application.persistentDataPath, "save_demo.json");
            originalDemoSave = File.Exists(demoSavePath)
                ? File.ReadAllBytes(demoSavePath)
                : null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (SceneManager.GetActiveScene().name == "int02_complete_loop")
            {
                yield return SceneManager.LoadSceneAsync("minjun", LoadSceneMode.Single);
                yield return null;
            }

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
