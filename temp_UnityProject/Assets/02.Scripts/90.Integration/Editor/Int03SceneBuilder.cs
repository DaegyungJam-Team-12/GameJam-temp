#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Gameplay;
using Icebreaker.UI.Feedback;
using Icebreaker.UI.Hud;
using Icebreaker.UI.Management;
using Icebreaker.UI.Maintenance;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Icebreaker.Integration.Editor
{
    public static class Int03SceneBuilder
    {
        public const string ScenePath = "Assets/01.Scenes/minjun.unity";

        private const string LauncherPrefabPath =
            "Assets/03.Prefabs/30.UI/Hud/UI_LauncherHud.prefab";
        private const string MaintenancePrefabPath =
            "Assets/03.Prefabs/30.UI/Maintenance/UI_MaintenanceTree.prefab";
        private const string IcebreakingPrefabPath =
            "Assets/03.Prefabs/30.UI/Hud/UI_IcebreakingHud.prefab";
        private const string SettlementPrefabPath =
            "Assets/03.Prefabs/30.UI/Hud/UI_RewardSettlement.prefab";
        private const string ManagementPrefabPath =
            "Assets/03.Prefabs/30.UI/Management/UI_ManagementViews.prefab";
        private const string FeedbackPrefabPath =
            "Assets/03.Prefabs/30.UI/Feedback/UI_FeedbackAudio.prefab";

        [MenuItem("Icebreaker/INT-03/최종 씬 조립 (minjun.unity)")]
        public static void BuildScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("INT-03 scene cannot be rebuilt while entering or in Play Mode.");
            }

            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var sceneCamera = CreateCamera();
                CreateEventSystem();
                InstantiatePrefab(LauncherPrefabPath, "INT03_LauncherHud");
                var maintenance = InstantiatePrefab(MaintenancePrefabPath, "INT03_MaintenanceTree");
                maintenance.SetActive(false);
                InstantiatePrefab(IcebreakingPrefabPath, "INT03_IcebreakingHud");
                InstantiatePrefab(SettlementPrefabPath, "INT03_RewardSettlement");

                var management = InstantiatePrefab(ManagementPrefabPath, "INT03_ManagementViews");
                management.GetComponent<Canvas>().sortingOrder = 100;
                management.GetComponent<Ui05ManagementSampleSource>().enabled = false;
                var managementPresenter = management.GetComponent<ManagementViewsPresenter>();
                var serializedManagement = new SerializedObject(managementPresenter);
                serializedManagement.FindProperty("finalGameMode").boolValue = true;
                serializedManagement.ApplyModifiedPropertiesWithoutUndo();
                var maintenanceTab = serializedManagement
                    .FindProperty("maintenanceTabButton").objectReferenceValue as Button;
                maintenanceTab?.gameObject.SetActive(false);
                var routeRoot = serializedManagement
                    .FindProperty("routeRoot").objectReferenceValue as GameObject;
                routeRoot?.SetActive(false);
                var maintenanceRoot = serializedManagement
                    .FindProperty("maintenanceRoot").objectReferenceValue as GameObject;
                maintenanceRoot?.SetActive(false);

                var feedback = InstantiatePrefab(FeedbackPrefabPath, "INT03_FeedbackAudio");
                feedback.GetComponent<Ui06FeedbackSampleSource>().enabled = false;

                var iceField = new GameObject("INT03_IceField");
                var iceFieldView = iceField.AddComponent<IceFieldView>();
                var serializedField = new SerializedObject(iceFieldView);
                serializedField.FindProperty("sceneCamera").objectReferenceValue = sceneCamera;
                serializedField.ApplyModifiedPropertiesWithoutUndo();

                var orchestrator = new GameObject("INT03_Orchestrator");
                orchestrator.AddComponent<Int02IntegrationOrchestrator>();

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                {
                    throw new InvalidOperationException($"Scene could not be saved to {ScenePath}.");
                }

                ValidateScene(scene);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[INT-03] minjun.unity assembled and validated.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[INT-03] failed to assemble final scene: {exception}");
                throw;
            }
        }

        [MenuItem("Icebreaker/INT-03/최종 씬 참조 검증 (minjun.unity)")]
        public static void ValidateSavedScene()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedForValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedForValidation)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                ValidateScene(scene);
                Debug.Log("[INT-03] minjun.unity reference validation passed.");
            }
            finally
            {
                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }

        public static void ValidateScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException($"INT-03 scene is not loaded: {ScenePath}");
            }

            var errors = new List<string>();
            RequireNamedRoot(scene, "Main Camera", errors);
            RequireNamedRoot(scene, "EventSystem", errors);
            RequireNamedRoot(scene, "INT03_LauncherHud", errors);
            RequireNamedRoot(scene, "INT03_MaintenanceTree", errors);
            RequireNamedRoot(scene, "INT03_IcebreakingHud", errors);
            RequireNamedRoot(scene, "INT03_RewardSettlement", errors);
            RequireNamedRoot(scene, "INT03_ManagementViews", errors);
            RequireNamedRoot(scene, "INT03_FeedbackAudio", errors);
            RequireNamedRoot(scene, "INT03_IceField", errors);
            RequireNamedRoot(scene, "INT03_Orchestrator", errors);

            RequireOne<Camera>(scene, errors);
            RequireOne<EventSystem>(scene, errors);
            RequireOne<InputSystemUIInputModule>(scene, errors);
            RequireOne<LauncherHudPresenter>(scene, errors);
            RequireOne<MaintenanceTreePresenter>(scene, errors);
            RequireOne<IcebreakingHudPresenter>(scene, errors);
            RequireOne<RewardSettlementPresenter>(scene, errors);
            RequireOne<ManagementViewsPresenter>(scene, errors);
            RequireOne<Ui06FeedbackAudioPresenter>(scene, errors);
            RequireOne<IceFieldView>(scene, errors);
            RequireOne<Int02IntegrationOrchestrator>(scene, errors);

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                    {
                        errors.Add($"Missing script: {GetHierarchyPath(transform)}");
                    }
                }
            }

            ValidateManagement(scene, errors);
            ValidateFeedback(scene, errors);
            ValidateIceField(scene, errors);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "[INT-03] final scene reference validation failed:\n- " +
                    string.Join("\n- ", errors));
            }
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.08f, 0.13f, 1f);
            return camera;
        }

        private static void CreateEventSystem()
        {
            _ = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
        }

        private static GameObject InstantiatePrefab(string path, string instanceName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Prefab was not found at {path}.");
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException($"Prefab could not be instantiated from {path}.");
            }

            instance.name = instanceName;
            instance.transform.SetParent(null, false);
            return instance;
        }

        private static void ValidateManagement(Scene scene, List<string> errors)
        {
            var presenter = FindComponentsInScene<ManagementViewsPresenter>(scene)[0];
            var serialized = new SerializedObject(presenter);
            ValidateReferences(serialized, new[]
            {
                "routeTabButton", "stageStartButton", "settingsButton", "collapseButton",
                "maintenanceRoot", "routeRoot", "destinationNameText",
                "destinationProgressText", "destinationProgressFill", "cargoText",
                "completedDestinationsText", "upcomingDestinationsText", "completedBadge",
                "settingsRoot", "masterVolumeSlider", "screenShakeToggle", "settingsCloseButton",
                "quitButton", "arrivalRoot", "arrivalCanvasGroup", "arrivalDestinationText",
                "arrivalStatusText"
            }, errors);

            if (!serialized.FindProperty("finalGameMode").boolValue)
            {
                errors.Add("ManagementViewsPresenter.finalGameMode must be enabled.");
            }

            var maintenanceButton = serialized.FindProperty("maintenanceTabButton").objectReferenceValue as Button;
            if (maintenanceButton == null || maintenanceButton.gameObject.activeSelf)
            {
                errors.Add("UI-05 maintenance tab must exist but remain unreachable in the final scene.");
            }

            var maintenanceRoot = serialized.FindProperty("maintenanceRoot").objectReferenceValue as GameObject;
            if (maintenanceRoot == null || maintenanceRoot.activeSelf)
            {
                errors.Add("UI-05 maintenance root must remain inactive in the final scene.");
            }

            var stageStartButton = serialized.FindProperty("stageStartButton").objectReferenceValue as Button;
            var settingsButton = serialized.FindProperty("settingsButton").objectReferenceValue as Button;
            if (stageStartButton == null || !stageStartButton.gameObject.activeSelf ||
                settingsButton == null || !settingsButton.gameObject.activeSelf)
            {
                errors.Add("UI-05 route stage-start and settings controls must remain active.");
            }

            var sample = presenter.GetComponent<Ui05ManagementSampleSource>();
            if (sample == null || sample.enabled)
            {
                errors.Add("UI-05 sample source must be disabled in the final scene.");
            }

            if (presenter.GetComponent<Canvas>().sortingOrder < 100)
            {
                errors.Add("Management settings/arrival overlays must render above the live HUD and feedback.");
            }
        }

        private static void ValidateFeedback(Scene scene, List<string> errors)
        {
            var presenter = FindComponentsInScene<Ui06FeedbackAudioPresenter>(scene)[0];
            var serialized = new SerializedObject(presenter);
            ValidateReferences(serialized, new[]
            {
                "theme", "supportCore", "supportStateText", "muzzleGlow", "supportTrail",
                "feedbackLayer", "feedbackCueTemplate", "gameplayAudioSource", "uiAudioSource",
                "ambientAudioSource", "lightBreakClip", "heavyBreakClip", "crackClip",
                "crystalDestroyClip", "criticalHitClip", "buttonClickClip", "purchaseSuccessClip",
                "stageStartClip", "settlementCompleteClip", "arrivalHornClip", "ambientLoopClip"
            }, errors);
            var segments = serialized.FindProperty("chargeSegments");
            if (segments == null || segments.arraySize != Ui06FeedbackAudioPresenter.ChargeSegmentCount)
            {
                errors.Add("UI-06 feedback must have exactly 12 charge segments.");
            }

            var sample = presenter.GetComponent<Ui06FeedbackSampleSource>();
            if (sample == null || sample.enabled)
            {
                errors.Add("UI-06 sample source must be disabled; the orchestrator supplies live events.");
            }
        }

        private static void ValidateIceField(Scene scene, List<string> errors)
        {
            var view = FindComponentsInScene<IceFieldView>(scene)[0];
            var sceneCamera = new SerializedObject(view).FindProperty("sceneCamera").objectReferenceValue;
            if (sceneCamera == null)
            {
                errors.Add("IceFieldView.sceneCamera is not assigned.");
            }
        }

        private static void ValidateReferences(
            SerializedObject serialized,
            IReadOnlyList<string> propertyNames,
            List<string> errors)
        {
            foreach (var propertyName in propertyNames)
            {
                var property = serialized.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                {
                    errors.Add($"{serialized.targetObject.GetType().Name}.{propertyName} is not assigned.");
                }
            }
        }

        private static void RequireNamedRoot(Scene scene, string name, List<string> errors)
        {
            var count = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (string.Equals(root.name, name, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            if (count != 1)
            {
                errors.Add($"Expected exactly one root named {name}; found {count}.");
            }
        }

        private static void RequireOne<T>(Scene scene, List<string> errors) where T : Component
        {
            var count = FindComponentsInScene<T>(scene).Count;
            if (count != 1)
            {
                errors.Add($"Expected exactly one {typeof(T).Name}; found {count}.");
            }
        }

        private static List<T> FindComponentsInScene<T>(Scene scene) where T : Component
        {
            var results = new List<T>();
            foreach (var root in scene.GetRootGameObjects())
            {
                results.AddRange(root.GetComponentsInChildren<T>(includeInactive: true));
            }

            return results;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }
    }
}
