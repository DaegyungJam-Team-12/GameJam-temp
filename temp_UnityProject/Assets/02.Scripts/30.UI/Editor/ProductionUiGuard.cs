#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Icebreaker.UI.Editor
{
    /// <summary>
    /// Central shipping policy for normal UI prefabs. Preview data sources remain
    /// available to Sandbox prefabs and editor-only validation instances.
    /// </summary>
    public static class ProductionUiGuard
    {
        private static readonly string[] ProductionPrefabPaths =
        {
            "Assets/03.Prefabs/30.UI/Hud/UI_LauncherHud.prefab",
            "Assets/03.Prefabs/30.UI/Hud/UI_IcebreakingHud.prefab",
            "Assets/03.Prefabs/30.UI/Hud/UI_RewardSettlement.prefab",
            "Assets/03.Prefabs/30.UI/Maintenance/UI_MaintenanceTree.prefab",
            "Assets/03.Prefabs/30.UI/Management/UI_ManagementViews.prefab",
        };

        public static bool IsPreviewOnly(Type type)
        {
            var name = type.Name;
            return name.Contains("Sample", StringComparison.Ordinal) ||
                   name.Contains("Fake", StringComparison.Ordinal) ||
                   name.Contains("Sandbox", StringComparison.Ordinal);
        }

        public static void CollectErrors(
            GameObject productionPrefab,
            ICollection<string> errors)
        {
            foreach (var behaviour in productionPrefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (IsPreviewOnly(behaviour.GetType()))
                {
                    errors.Add(
                        $"{productionPrefab.name} contains preview-only component " +
                        $"{behaviour.GetType().FullName} on '{behaviour.name}'.");
                }
            }
        }

        [MenuItem("ICEBREAKER/UI/Validate All Production Prefabs")]
        public static void ValidateAll()
        {
            var errors = new List<string>();
            foreach (var path in ProductionPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    errors.Add($"Missing production prefab: {path}");
                    continue;
                }

                CollectErrors(prefab, errors);
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "[UI-PRODUCTION] Preview component validation failed:\n- " +
                    string.Join("\n- ", errors));
            }

            Debug.Log("[UI-PRODUCTION] Five production UI prefabs contain no Sample/Fake/Sandbox components.");
        }

        [MenuItem("ICEBREAKER/UI/Rebuild Owned Production Prefabs")]
        public static void RebuildOwnedProductionPrefabs()
        {
            Ui02PrefabBuilder.Build();
            Ui04PrefabBuilder.Build();
            MaintenanceTreePrefabBuilder.Build();
            Ui05PrefabBuilder.Build();
            ValidateAll();
        }
    }
}
