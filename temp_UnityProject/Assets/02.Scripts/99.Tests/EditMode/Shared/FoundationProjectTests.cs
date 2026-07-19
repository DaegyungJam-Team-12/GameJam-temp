using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Icebreaker.Shared.Tests
{
    public sealed class FoundationProjectTests
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/01.Scenes/minjun.unity",
            "Assets/01.Scenes/siyeon.unity",
            "Assets/01.Scenes/jeonghwan.unity"
        };

        [Test]
        public void OwnerScenes_ExistAtFlatPaths_WithDistinctGuids()
        {
            var guids = new HashSet<string>();

            foreach (var scenePath in ScenePaths)
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), Is.Not.Null, scenePath);

                var guid = AssetDatabase.AssetPathToGUID(scenePath);
                Assert.That(guid, Is.Not.Empty, scenePath);
                Assert.That(guids.Add(guid), Is.True, $"Duplicate GUID for {scenePath}");
            }
        }

        [Test]
        public void BuildSettings_ContainOnlyEnabledMinjunScene()
        {
            var scenes = EditorBuildSettings.scenes;

            Assert.That(scenes, Has.Length.EqualTo(1));
            Assert.That(scenes[0].enabled, Is.True);
            Assert.That(scenes[0].path, Is.EqualTo(ScenePaths[0]));
        }

        [Test]
        public void SharedAssembly_HasNoDomainAssemblyReferences()
        {
            var asmdefPath = Path.Combine(
                Application.dataPath,
                "02.Scripts/00.Shared/Icebreaker.Shared.asmdef");
            var asmdef = File.ReadAllText(asmdefPath);

            StringAssert.Contains("\"name\": \"Icebreaker.Shared\"", asmdef);
            StringAssert.Contains("\"references\": []", asmdef);
            StringAssert.DoesNotContain("Icebreaker.Core", asmdef);
            StringAssert.DoesNotContain("Icebreaker.Gameplay", asmdef);
            StringAssert.DoesNotContain("Icebreaker.UI", asmdef);
        }
    }
}
