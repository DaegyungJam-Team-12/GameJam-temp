#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Icebreaker.UI.Tests
{
    public sealed class ProductionUiFontTests
    {
        private const string FontFolder = "Assets/04.Images/30.UI/Fonts";
        private const string ThemePath = "Assets/04.Images/30.UI/Theme/UiTheme.asset";
        private const string PrimaryFontPath = FontFolder + "/DNFBitBitTTF SDF.asset";
        private const string MaintenanceFontPath = FontFolder + "/NotoSansKR-Variable SDF.asset";
        private const string FeedbackFontPath = FontFolder + "/DOSIyagiBoldface SDF.asset";

        private static readonly string[] PrimaryPrefabPaths =
        {
            "Assets/03.Prefabs/30.UI/Hud/UI_LauncherHud.prefab",
            "Assets/03.Prefabs/30.UI/Hud/UI_IcebreakingHud.prefab",
            "Assets/03.Prefabs/30.UI/Hud/UI_RewardSettlement.prefab",
            "Assets/03.Prefabs/30.UI/Maintenance/UI_MaintenanceNode.prefab",
            "Assets/03.Prefabs/30.UI/Management/UI_ManagementViews.prefab",
        };

        private static readonly string[] MaintenancePrefabPaths =
        {
            "Assets/03.Prefabs/30.UI/Maintenance/UI_MaintenanceTooltip.prefab",
        };

        private const string FeedbackPrefabPath =
            "Assets/03.Prefabs/30.UI/Feedback/UI_FeedbackAudio.prefab";

        [Test]
        public void ThemeAndTmpSettings_UseApprovedRoleFonts()
        {
            var primary = LoadFont(PrimaryFontPath);
            var maintenance = LoadFont(MaintenanceFontPath);
            var feedback = LoadFont(FeedbackFontPath);
            var theme = AssetDatabase.LoadMainAssetAtPath(ThemePath);
            Assert.That(theme, Is.Not.Null);

            var serializedTheme = new SerializedObject(theme);
            Assert.That(
                serializedTheme.FindProperty("primaryFont").objectReferenceValue,
                Is.SameAs(primary));
            Assert.That(
                serializedTheme.FindProperty("maintenanceFont").objectReferenceValue,
                Is.SameAs(maintenance));
            Assert.That(
                serializedTheme.FindProperty("combatFeedbackFont").objectReferenceValue,
                Is.SameAs(feedback));

            Assert.That(TMP_Settings.defaultFontAsset, Is.SameAs(primary));
            Assert.That(TMP_Settings.clearDynamicDataOnBuild, Is.True);
            Assert.That(TMP_Settings.fallbackFontAssets, Is.EqualTo(new[] { maintenance }));
        }

        [Test]
        public void FontAssets_AreDynamicAndReferenceMatchingSourceAtlasAndMaterial()
        {
            AssertFontAsset(
                LoadFont(PrimaryFontPath),
                FontFolder + "/DNFBitBitTTF.ttf",
                LoadFont(MaintenanceFontPath));
            AssertFontAsset(
                LoadFont(MaintenanceFontPath),
                FontFolder + "/NotoSansKR-Variable.ttf");
            AssertFontAsset(
                LoadFont(FeedbackFontPath),
                FontFolder + "/DOSIyagiBoldface.ttf",
                LoadFont(MaintenanceFontPath));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<TextAsset>(
                    FontFolder + "/OFL-NotoSansKR.txt"),
                Is.Not.Null,
                "The Noto Sans KR license file must remain in the project.");
        }

        [Test]
        public void ProductionPrefabs_UseOnlyTheirApprovedRoleFont()
        {
            AssertProductionPrefabFonts();
        }

        [Test]
        public void ProductionSourceCharacters_AreCoveredByEachRoleFallbackChain()
        {
            var requiredCharacters = CollectRequiredCharacters();
            AssertSourceCoverage(LoadFont(PrimaryFontPath), requiredCharacters);
            AssertSourceCoverage(LoadFont(MaintenanceFontPath), requiredCharacters);
            AssertSourceCoverage(LoadFont(FeedbackFontPath), requiredCharacters);
        }

        [Test]
        public void RebuildingProductionPrefabs_PreservesRoleFontReferences()
        {
            Assert.That(
                EditorApplication.ExecuteMenuItem(
                    "ICEBREAKER/UI/Rebuild Owned Production Prefabs"),
                Is.True,
                "The production prefab rebuild menu item was not found.");
            AssertProductionPrefabFonts();
        }

        private static void AssertProductionPrefabFonts()
        {
            var primary = LoadFont(PrimaryFontPath);
            var maintenance = LoadFont(MaintenanceFontPath);
            var feedback = LoadFont(FeedbackFontPath);

            foreach (var prefabPath in PrimaryPrefabPaths)
            {
                AssertPrefabUsesFont(prefabPath, primary);
            }

            foreach (var prefabPath in MaintenancePrefabPaths)
            {
                AssertPrefabUsesFont(prefabPath, maintenance);
            }

            var maintenanceTree = LoadPrefab("Assets/03.Prefabs/30.UI/Maintenance/UI_MaintenanceTree.prefab");
            foreach (var text in maintenanceTree.GetComponentsInChildren<TMP_Text>(true))
            {
                var relativePath = GetRelativePath(maintenanceTree.transform, text.transform);
                var expected = relativePath.StartsWith("Tooltip/", StringComparison.Ordinal)
                    ? maintenance
                    : primary;
                AssertTextUsesFont("UI_MaintenanceTree.prefab", relativePath, text, expected);
            }

            var feedbackPrefab = LoadPrefab(FeedbackPrefabPath);
            var feedbackTexts = feedbackPrefab.GetComponentsInChildren<TMP_Text>(true);
            Assert.That(feedbackTexts, Is.Not.Empty);
            foreach (var text in feedbackTexts)
            {
                var relativePath = GetRelativePath(feedbackPrefab.transform, text.transform);
                var expected = relativePath == "FeedbackLayer/FeedbackCueTemplate/Label"
                    ? feedback
                    : primary;
                AssertTextUsesFont(FeedbackPrefabPath, relativePath, text, expected);
            }
        }

        private static void AssertPrefabUsesFont(
            string prefabPath,
            TMP_FontAsset expectedFont)
        {
            var prefab = LoadPrefab(prefabPath);
            var texts = prefab.GetComponentsInChildren<TMP_Text>(true);
            Assert.That(texts, Is.Not.Empty, $"{prefabPath} contains no TMP text.");
            foreach (var text in texts)
            {
                AssertTextUsesFont(
                    prefabPath,
                    GetRelativePath(prefab.transform, text.transform),
                    text,
                    expectedFont);
            }
        }

        private static void AssertTextUsesFont(
            string prefabPath,
            string relativePath,
            TMP_Text text,
            TMP_FontAsset expectedFont)
        {
            var context = $"{prefabPath}:{relativePath}";
            Assert.That(text.font, Is.Not.Null, $"{context} has a null font.");
            Assert.That(text.font, Is.SameAs(expectedFont), $"{context} uses the wrong role font.");
            Assert.That(text.fontSharedMaterial, Is.Not.Null, $"{context} has a null material.");
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    text.fontSharedMaterial.mainTexture,
                    out var actualAtlasGuid,
                    out long actualAtlasId),
                Is.True);
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    expectedFont.atlasTexture,
                    out var expectedAtlasGuid,
                    out long expectedAtlasId),
                Is.True);
            Assert.That(
                (actualAtlasGuid, actualAtlasId),
                Is.EqualTo((expectedAtlasGuid, expectedAtlasId)),
                $"{context} material does not use the font's atlas.");
        }

        private static void AssertFontAsset(
            TMP_FontAsset fontAsset,
            string expectedSourcePath,
            params TMP_FontAsset[] expectedFallbacks)
        {
            Assert.That(fontAsset.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic));
            Assert.That(fontAsset.isMultiAtlasTexturesEnabled, Is.True);
            Assert.That(fontAsset.sourceFontFile, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(fontAsset.sourceFontFile),
                Is.EqualTo(expectedSourcePath));
            Assert.That(fontAsset.atlasTexture, Is.Not.Null);
            Assert.That(fontAsset.material, Is.Not.Null);
            Assert.That(fontAsset.material.mainTexture, Is.SameAs(fontAsset.atlasTexture));
            Assert.That(fontAsset.fallbackFontAssetTable, Is.EqualTo(expectedFallbacks));
        }

        private static void AssertSourceCoverage(
            TMP_FontAsset fontAsset,
            IEnumerable<char> requiredCharacters)
        {
            var missing = requiredCharacters
                .Where(character =>
                    !CanRenderFromSource(
                        fontAsset,
                        character,
                        new HashSet<TMP_FontAsset>()))
                .ToArray();
            Assert.That(
                missing,
                Is.Empty,
                $"{fontAsset.name} fallback chain cannot render: {new string(missing)}");
        }

        private static bool CanRenderFromSource(
            TMP_FontAsset fontAsset,
            char character,
            ISet<TMP_FontAsset> visited)
        {
            if (!visited.Add(fontAsset))
            {
                return false;
            }

            if (fontAsset.sourceFontFile != null &&
                fontAsset.sourceFontFile.HasCharacter(character))
            {
                return true;
            }

            return fontAsset.fallbackFontAssetTable != null &&
                   fontAsset.fallbackFontAssetTable.Any(
                       fallback =>
                           fallback != null &&
                           CanRenderFromSource(fallback, character, visited));
        }

        private static IEnumerable<char> CollectRequiredCharacters()
        {
            const string commonSymbols =
                " !\"#$%&'()*+,-./0123456789:;<=>?@" +
                "ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
                "·×→←…₩℃";
            var characters = new SortedSet<char>(commonSymbols);
            var folders = new[]
            {
                "Assets/02.Scripts/00.Shared",
                "Assets/02.Scripts/10.Core",
                "Assets/02.Scripts/20.Gameplay",
                "Assets/02.Scripts/30.UI",
                "Assets/02.Scripts/90.Integration",
                "Assets/03.Prefabs/30.UI",
                "Assets/09.Data",
            };
            var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;

            foreach (var assetFolder in folders)
            {
                var absoluteFolder = Path.Combine(
                    projectRoot,
                    assetFolder.Replace('/', Path.DirectorySeparatorChar));
                foreach (var file in Directory.EnumerateFiles(
                             absoluteFolder,
                             "*.*",
                             SearchOption.AllDirectories))
                {
                    var extension = Path.GetExtension(file);
                    if (extension != ".cs" &&
                        extension != ".json" &&
                        extension != ".asset" &&
                        extension != ".prefab")
                    {
                        continue;
                    }

                    foreach (var character in File.ReadAllText(file))
                    {
                        if (character is >= '\uAC00' and <= '\uD7A3' ||
                            commonSymbols.IndexOf(character) >= 0)
                        {
                            characters.Add(character);
                        }
                    }
                }
            }

            return characters;
        }

        private static TMP_FontAsset LoadFont(string path)
        {
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) ??
                   throw new AssertionException($"Missing TMP font asset: {path}");
        }

        private static GameObject LoadPrefab(string path)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(path) ??
                   throw new AssertionException($"Missing production prefab: {path}");
        }

        private static string GetRelativePath(Transform root, Transform child)
        {
            var names = new Stack<string>();
            for (var current = child; current != root; current = current.parent)
            {
                names.Push(current.name);
            }

            return string.Join("/", names);
        }
    }
}
