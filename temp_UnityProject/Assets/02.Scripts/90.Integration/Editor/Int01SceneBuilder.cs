#nullable enable

using System;
using Icebreaker.UI.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Icebreaker.Integration.Editor
{
    public static class Int01SceneBuilder
    {
        private const string ScenePath = "Assets/01.Scenes/minjun.unity";
        private const string HudPrefabPath = "Assets/03.Prefabs/30.UI/Hud/UI_LauncherHud.prefab";

        [MenuItem("Icebreaker/INT-01/통합 씬 조립 (minjun.unity)")]
        public static void BuildMinjunScene()
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                RemoveExistingIntegrationRoots(scene);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException($"HUD prefab was not found at {HudPrefabPath}.");
                }

                var prefabContainsCanvas = prefab.GetComponentInChildren<Canvas>(true) != null;
                var hudInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

                if (prefabContainsCanvas)
                {
                    hudInstance.name = "INT01_Canvas";
                    hudInstance.transform.SetParent(null, false);
                }
                else
                {
                    var canvasObject = new GameObject(
                        "INT01_Canvas",
                        typeof(Canvas),
                        typeof(CanvasScaler),
                        typeof(GraphicRaycaster));
                    canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                    hudInstance.transform.SetParent(canvasObject.transform, false);
                }

                foreach (var sampleSource in hudInstance.GetComponentsInChildren<Ui02HudSampleSource>(true))
                {
                    UnityEngine.Object.DestroyImmediate(sampleSource);
                }

                var iceFieldObject = new GameObject("INT01_IceField");
                iceFieldObject.AddComponent<Icebreaker.Gameplay.IceFieldView>();

                var bootstrapObject = new GameObject("INT01_Bootstrap");
                bootstrapObject.AddComponent<IntegrationBootstrap>();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[INT-01] minjun.unity assembled");

                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[INT-01] failed to assemble minjun.unity: {exception}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        private static void RemoveExistingIntegrationRoots(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "INT01_Canvas" ||
                    root.name == "INT01_IceField" ||
                    root.name == "INT01_Bootstrap")
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }
    }
}
