#nullable enable

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Icebreaker.Shared.State;

namespace Icebreaker.Integration.Tests
{
    public sealed class EscNavigationIntegrationTests : InputTestFixture
    {
        private const string Int02ScenePath = "Assets/01.Scenes/int02_complete_loop.unity";
        private string standardSavePath = null!;
        private byte[]? originalStandardSave;
        private Keyboard keyboard = null!;

        public override void Setup()
        {
            base.Setup(); // Setup InputTestFixture
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            yield return DestroyActiveOrchestrators();

            standardSavePath = Path.Combine(Application.persistentDataPath, "save_standard.json");
            originalStandardSave = File.Exists(standardSavePath)
                ? File.ReadAllBytes(standardSavePath)
                : null;
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            yield return DestroyActiveOrchestrators();

            if (originalStandardSave != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(standardSavePath)!);
                File.WriteAllBytes(standardSavePath, originalStandardSave);
            }
            else if (File.Exists(standardSavePath))
            {
                File.Delete(standardSavePath);
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
            throw new InvalidOperationException($"Type was not loaded: {fullName}");
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

        private static void WriteCleanStandardSave(string path)
        {
            File.WriteAllText(path, @"{
  ""saveVersion"": 1,
  ""profileId"": ""standard"",
  ""funds"": 0,
  ""maintenanceLevels"": [],
  ""currentDestinationIndex"": 0,
  ""destinationProgress"": 0,
  ""completedDestinationIds"": [],
  ""pendingArrivalDestinationId"": """",
  ""firstDestroyShown"": false,
  ""nextAvailableAtUtc"": """",
  ""runInProgress"": false,
  ""gameCompleted"": false,
  ""masterVolume"": 0,
  ""screenShakeEnabled"": true
}");
        }

        private IEnumerator PressEscape(Component orchestrator, Type orchestratorType)
        {
            var handleEscape = orchestratorType.GetMethod("HandleEscapePressed", BindingFlags.Instance | BindingFlags.NonPublic);
            handleEscape!.Invoke(orchestrator, null);
            yield return null;
        }

        private IEnumerator WaitForPhase(Component orchestrator, Type orchestratorType, GamePhase expectedPhase, float timeoutSeconds)
        {
            var timeoutAt = Time.realtimeSinceStartup + timeoutSeconds;
            while ((GamePhase)GetStateValue(orchestrator, orchestratorType, "Phase") != expectedPhase &&
                   Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That((GamePhase)GetStateValue(orchestrator, orchestratorType, "Phase"), Is.EqualTo(expectedPhase));
        }

        private static object GetStateValue(Component orchestrator, Type orchestratorType, string propertyName)
        {
            var state = orchestratorType.GetProperty("CurrentState")!.GetValue(orchestrator);
            return state!.GetType().GetProperty(propertyName)!.GetValue(state)!;
        }

        private Component FindOrchestrator(Type orchestratorType)
        {
            return (Component)UnityEngine.Object.FindFirstObjectByType(orchestratorType);
        }

        private void InjectSettings(Component orchestrator, Type orchestratorType)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03.Prefabs/30.UI/Management/UI_ManagementViews.prefab");
            if (prefab == null) return;
            
            var mockViewsObj = UnityEngine.Object.Instantiate(prefab);
            var mockViewsType = FindType("Icebreaker.UI.Management.ManagementViewsPresenter");
            var mockViews = mockViewsObj.GetComponent(mockViewsType);

            mockViewsType.GetMethod("EnsureInitialized")!.Invoke(mockViews, null);
            orchestratorType.GetField("managementViews", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(orchestrator, mockViews);

            var eventInfo = mockViewsType.GetEvent("SettingsVisibilityChanged");
            var handlerMethod = orchestratorType.GetMethod("HandleSettingsVisibilityChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            var handler = Delegate.CreateDelegate(eventInfo!.EventHandlerType!, orchestrator, handlerMethod!);
            eventInfo.AddEventHandler(mockViews, handler);
        }

        [UnityTest]
        public IEnumerator Esc_FromLauncher_OpensSettings_And_Esc_ReturnsToLauncher()
        {
            WriteCleanStandardSave(standardSavePath);
            EditorSceneManager.LoadSceneInPlayMode(Int02ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            for (var i = 0; i < 5; i++) yield return null;

            var orchestratorType = FindType("Icebreaker.Integration.Int02IntegrationOrchestrator");
            var orchestrator = FindOrchestrator(orchestratorType);
            Assert.That(orchestrator, Is.Not.Null);
            
            InjectSettings(orchestrator, orchestratorType);

            var phase = (GamePhase)GetStateValue(orchestrator, orchestratorType, "Phase");
            var screen = (ManagementScreen)orchestratorType.GetProperty("CurrentManagementScreen")!.GetValue(orchestrator);
            Assert.That(phase, Is.EqualTo(GamePhase.Ready));
            Assert.That(screen, Is.EqualTo(ManagementScreen.None));

            // 1. ESC 누르면 설정 창 오픈
            yield return PressEscape(orchestrator, orchestratorType);

            phase = (GamePhase)GetStateValue(orchestrator, orchestratorType, "Phase");
            screen = (ManagementScreen)orchestratorType.GetProperty("CurrentManagementScreen")!.GetValue(orchestrator);
            Assert.That(screen, Is.EqualTo(ManagementScreen.Settings));
            Assert.That(phase, Is.EqualTo(GamePhase.Ready));

            // 2. 다시 ESC 누르면 런처로 복귀
            yield return PressEscape(orchestrator, orchestratorType);

            phase = (GamePhase)GetStateValue(orchestrator, orchestratorType, "Phase");
            screen = (ManagementScreen)orchestratorType.GetProperty("CurrentManagementScreen")!.GetValue(orchestrator);
            Assert.That(screen, Is.EqualTo(ManagementScreen.None));
            Assert.That(phase, Is.EqualTo(GamePhase.Ready));
        }

        [UnityTest]
        public IEnumerator Esc_FromMaintenance_ReturnsToLauncher()
        {
            WriteCleanStandardSave(standardSavePath);
            EditorSceneManager.LoadSceneInPlayMode(Int02ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            for (var i = 0; i < 5; i++) yield return null;

            var orchestratorType = FindType("Icebreaker.Integration.Int02IntegrationOrchestrator");
            var orchestrator = FindOrchestrator(orchestratorType);

            // 정비 창 오픈
            orchestratorType.GetMethod("RequestManagementScreen")!.Invoke(orchestrator, new object[] { ManagementScreen.Maintenance });
            yield return null;
            
            var screen = (ManagementScreen)orchestratorType.GetProperty("CurrentManagementScreen")!.GetValue(orchestrator);
            Assert.That(screen, Is.EqualTo(ManagementScreen.Maintenance));

            // ESC 누르면 런처로 복귀
            yield return PressEscape(orchestrator, orchestratorType);

            var phase = (GamePhase)GetStateValue(orchestrator, orchestratorType, "Phase");
            screen = (ManagementScreen)orchestratorType.GetProperty("CurrentManagementScreen")!.GetValue(orchestrator);
            Assert.That(screen, Is.EqualTo(ManagementScreen.None));
            Assert.That(phase, Is.EqualTo(GamePhase.Ready));
        }

        [UnityTest]
        public IEnumerator Esc_FromRoute_ReturnsToLauncher()
        {
            WriteCleanStandardSave(standardSavePath);
            EditorSceneManager.LoadSceneInPlayMode(Int02ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            for (var i = 0; i < 5; i++) yield return null;

            var orchestratorType = FindType("Icebreaker.Integration.Int02IntegrationOrchestrator");
            var orchestrator = FindOrchestrator(orchestratorType);

            // 운항 현황 창 오픈
            orchestratorType.GetMethod("RequestManagementScreen")!.Invoke(orchestrator, new object[] { ManagementScreen.Route });
            yield return null;

            var screen = (ManagementScreen)orchestratorType.GetProperty("CurrentManagementScreen")!.GetValue(orchestrator);
            Assert.That(screen, Is.EqualTo(ManagementScreen.Route));

            // ESC 누르면 런처로 복귀
            yield return PressEscape(orchestrator, orchestratorType);

            var phase = (GamePhase)GetStateValue(orchestrator, orchestratorType, "Phase");
            screen = (ManagementScreen)orchestratorType.GetProperty("CurrentManagementScreen")!.GetValue(orchestrator);
            Assert.That(screen, Is.EqualTo(ManagementScreen.None));
            Assert.That(phase, Is.EqualTo(GamePhase.Ready));
        }

        [UnityTest]
        public IEnumerator Esc_WhilePlaying_PausesAndOpensSettings_And_Esc_Resumes()
        {
            WriteCleanStandardSave(standardSavePath);
            EditorSceneManager.LoadSceneInPlayMode(Int02ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            for (var i = 0; i < 5; i++) yield return null;

            var orchestratorType = FindType("Icebreaker.Integration.Int02IntegrationOrchestrator");
            var orchestrator = FindOrchestrator(orchestratorType);
            
            InjectSettings(orchestrator, orchestratorType);
            
            // 쇄빙 시작 (Playing 상태로 전환)
            var handleStageStart = orchestratorType.GetMethod("HandleStageStartRequested", BindingFlags.Instance | BindingFlags.NonPublic);
            handleStageStart!.Invoke(orchestrator, null);
            
            // 3초 카운트다운을 기다려 Playing 상태로 전환
            yield return WaitForPhase(orchestrator, orchestratorType, GamePhase.Playing, 5f);

            var phase = (GamePhase)GetStateValue(orchestrator, orchestratorType, "Phase");
            var isPaused = (bool)GetStateValue(orchestrator, orchestratorType, "IsPaused");
            Assert.That(phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(isPaused, Is.False);

            // 1. ESC 누르면 일시정지 및 설정 오픈
            yield return PressEscape(orchestrator, orchestratorType);

            phase = (GamePhase)GetStateValue(orchestrator, orchestratorType, "Phase");
            isPaused = (bool)GetStateValue(orchestrator, orchestratorType, "IsPaused");
            var screen = (ManagementScreen)orchestratorType.GetProperty("CurrentManagementScreen")!.GetValue(orchestrator);
            
            Assert.That(screen, Is.EqualTo(ManagementScreen.Settings));
            Assert.That(phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(isPaused, Is.True); // 일시정지 되어야 함

            // 2. 다시 ESC 누르면 설정 닫기 및 쇄빙 재개
            yield return PressEscape(orchestrator, orchestratorType);

            phase = (GamePhase)GetStateValue(orchestrator, orchestratorType, "Phase");
            isPaused = (bool)GetStateValue(orchestrator, orchestratorType, "IsPaused");
            screen = (ManagementScreen)orchestratorType.GetProperty("CurrentManagementScreen")!.GetValue(orchestrator);
            
            Assert.That(screen, Is.EqualTo(ManagementScreen.None));
            Assert.That(phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(isPaused, Is.False); // 다시 진행되어야 함
        }
    }
}
