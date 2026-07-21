#nullable enable

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Icebreaker.Integration.Editor
{
    public static class Int02SceneBuilder
    {
        private const string ScenePath = "Assets/01.Scenes/int02_complete_loop.unity";
        private const string LauncherPrefabPath = "Assets/03.Prefabs/30.UI/Hud/UI_LauncherHud.prefab";
        private const string IcebreakingPrefabPath = "Assets/03.Prefabs/30.UI/Hud/UI_IcebreakingHud.prefab";
        private const string SettlementPrefabPath = "Assets/03.Prefabs/30.UI/Hud/UI_RewardSettlement.prefab";

        [MenuItem("Icebreaker/INT-02/통합 씬 조립")]
        public static void BuildScene()
        {
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                CreateCamera();
                CreateEventSystem();
                InstantiatePrefab(LauncherPrefabPath, "INT02_LauncherHud");
                InstantiatePrefab(IcebreakingPrefabPath, "INT02_IcebreakingHud");
                InstantiatePrefab(SettlementPrefabPath, "INT02_RewardSettlement");

                var iceField = new GameObject("INT02_IceField");
                iceField.AddComponent<Icebreaker.Gameplay.IceFieldView>();

                var orchestrator = new GameObject("INT02_Orchestrator");
                orchestrator.AddComponent<Int02IntegrationOrchestrator>();

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                {
                    throw new InvalidOperationException($"Scene could not be saved to {ScenePath}.");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[INT-02] int02_complete_loop.unity assembled.");

                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[INT-02] failed to assemble integration scene: {exception}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.08f, 0.13f, 1f);
        }

        private static void CreateEventSystem()
        {
            _ = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
        }

        private static void InstantiatePrefab(string path, string instanceName)
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
        }
    }
}
