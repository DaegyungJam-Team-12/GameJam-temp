#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Icebreaker.UI.Sandbox;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Icebreaker.UI.Editor
{
    [InitializeOnLoad]
    public static class UiKoreanFontInstaller
    {
        private const string FontFolder = "Assets/04.Images/30.UI/Fonts";
        private const string ThemePath = "Assets/04.Images/30.UI/Theme/UiTheme.asset";
        private const string NotoLicensePath = FontFolder + "/OFL-NotoSansKR.txt";

        private const string PrimarySourcePath = FontFolder + "/DNFBitBitTTF.ttf";
        private const string PrimaryAssetPath = FontFolder + "/DNFBitBitTTF SDF.asset";
        private const string MaintenanceSourcePath = FontFolder + "/NotoSansKR-Variable.ttf";
        private const string MaintenanceAssetPath = FontFolder + "/NotoSansKR-Variable SDF.asset";
        private const string FeedbackSourcePath = FontFolder + "/DOSIyagiBoldface.ttf";
        private const string FeedbackAssetPath = FontFolder + "/DOSIyagiBoldface SDF.asset";

        private static readonly string[] CharacterSourceFolders =
        {
            "Assets/02.Scripts/00.Shared",
            "Assets/02.Scripts/10.Core",
            "Assets/02.Scripts/20.Gameplay",
            "Assets/02.Scripts/30.UI",
            "Assets/02.Scripts/90.Integration",
            "Assets/03.Prefabs/30.UI",
            "Assets/09.Data",
        };

        private const string CommonUiSymbols =
            " !\"#$%&'()*+,-./0123456789:;<=>?@" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "·×→←…₩℃";

        static UiKoreanFontInstaller()
        {
            EditorApplication.delayCall += InstallIfNeeded;
        }

        [MenuItem("ICEBREAKER/UI/Install Production TMP Fonts")]
        public static void Install()
        {
            if (AssetDatabase.LoadAssetAtPath<TextAsset>(NotoLicensePath) == null)
            {
                throw new InvalidOperationException(
                    $"The Noto Sans KR license must remain at {NotoLicensePath}.");
            }

            var maintenanceFont = LoadOrCreateFontAsset(
                MaintenanceSourcePath,
                MaintenanceAssetPath,
                "NotoSansKR-Variable SDF");
            var primaryFont = LoadOrCreateFontAsset(
                PrimarySourcePath,
                PrimaryAssetPath,
                "DNFBitBitTTF SDF");
            var feedbackFont = LoadOrCreateFontAsset(
                FeedbackSourcePath,
                FeedbackAssetPath,
                "DOSIyagiBoldface SDF");

            SetFallbacks(maintenanceFont);
            SetFallbacks(primaryFont, maintenanceFont);
            SetFallbacks(feedbackFont, maintenanceFont);

            var requiredCharacters = CollectRequiredCharacters();
            ValidateSourceCoverage(maintenanceFont, requiredCharacters);
            ValidateSourceCoverage(primaryFont, requiredCharacters);
            ValidateSourceCoverage(feedbackFont, requiredCharacters);

            AssignThemeFonts(primaryFont, maintenanceFont, feedbackFont);
            ConfigureTmpSettings(primaryFont, maintenanceFont);

            EditorUtility.SetDirty(primaryFont);
            EditorUtility.SetDirty(maintenanceFont);
            EditorUtility.SetDirty(feedbackFont);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ProductionUiGuard.RebuildOwnedProductionPrefabs();
            Debug.Log(
                "[UI Font] Production TMP roles installed: " +
                "DNFBitBitTTF=primary, Noto Sans KR=maintenance/fallback, " +
                "DOSIyagiBoldface=combat feedback.",
                primaryFont);
        }

        private static void InstallIfNeeded()
        {
            if (AssetDatabase.LoadAssetAtPath<Font>(PrimarySourcePath) == null ||
                AssetDatabase.LoadAssetAtPath<Font>(MaintenanceSourcePath) == null ||
                AssetDatabase.LoadAssetAtPath<Font>(FeedbackSourcePath) == null)
            {
                return;
            }

            var primaryFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PrimaryAssetPath);
            var maintenanceFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MaintenanceAssetPath);
            var feedbackFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FeedbackAssetPath);
            var theme = AssetDatabase.LoadAssetAtPath<UiThemeAsset>(ThemePath);
            var settings = TMP_Settings.LoadDefaultSettings();

            if (primaryFont != null &&
                maintenanceFont != null &&
                feedbackFont != null &&
                theme != null &&
                theme.PrimaryFont == primaryFont &&
                theme.MaintenanceFont == maintenanceFont &&
                theme.CombatFeedbackFont == feedbackFont &&
                TMP_Settings.defaultFontAsset == primaryFont &&
                TMP_Settings.clearDynamicDataOnBuild &&
                HasOnlyFallback(primaryFont, maintenanceFont) &&
                HasOnlyFallback(feedbackFont, maintenanceFont) &&
                HasOnlyFallback(maintenanceFont) &&
                settings != null &&
                TMP_Settings.fallbackFontAssets?.Count == 1 &&
                TMP_Settings.fallbackFontAssets[0] == maintenanceFont)
            {
                MaintenanceTreePrefabBuilder.Build(false);
                Ui05PrefabBuilder.Build(false);
                return;
            }

            Install();
        }

        private static TMP_FontAsset LoadOrCreateFontAsset(
            string sourcePath,
            string assetPath,
            string assetName)
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (sourceFont == null)
            {
                throw new InvalidOperationException($"Source font was not found at {sourcePath}.");
            }

            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (fontAsset == null)
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(
                    sourceFont,
                    90,
                    9,
                    GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic,
                    true);
                fontAsset.name = assetName;
                AssetDatabase.CreateAsset(fontAsset, assetPath);

                if (fontAsset.atlasTexture != null &&
                    !AssetDatabase.Contains(fontAsset.atlasTexture))
                {
                    fontAsset.atlasTexture.name = assetName.Replace(" SDF", " Atlas");
                    AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
                }

                if (fontAsset.material != null &&
                    !AssetDatabase.Contains(fontAsset.material))
                {
                    fontAsset.material.name = assetName.Replace(" SDF", " Atlas Material");
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }
            }

            if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            }
            fontAsset.isMultiAtlasTexturesEnabled = true;

            if (AssetDatabase.GetAssetPath(fontAsset.sourceFontFile) != sourcePath)
            {
                throw new InvalidOperationException(
                    $"{assetPath} does not reference its expected source font {sourcePath}.");
            }

            if (fontAsset.atlasTexture == null || fontAsset.material == null)
            {
                throw new InvalidOperationException(
                    $"{assetPath} must contain both an atlas texture and material.");
            }

            return fontAsset;
        }

        private static void SetFallbacks(
            TMP_FontAsset fontAsset,
            params TMP_FontAsset[] fallbacks)
        {
            fontAsset.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            fontAsset.fallbackFontAssetTable.Clear();
            fontAsset.fallbackFontAssetTable.AddRange(fallbacks);
        }

        private static bool HasOnlyFallback(
            TMP_FontAsset fontAsset,
            params TMP_FontAsset[] expected)
        {
            var actual = fontAsset.fallbackFontAssetTable;
            if (actual == null)
            {
                return expected.Length == 0;
            }

            return actual.Count == expected.Length &&
                   actual.SequenceEqual(expected);
        }

        private static void AssignThemeFonts(
            TMP_FontAsset primaryFont,
            TMP_FontAsset maintenanceFont,
            TMP_FontAsset feedbackFont)
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiThemeAsset>(ThemePath);
            if (theme == null)
            {
                throw new InvalidOperationException($"UI theme was not found at {ThemePath}.");
            }

            var serializedTheme = new SerializedObject(theme);
            serializedTheme.FindProperty("primaryFont").objectReferenceValue = primaryFont;
            serializedTheme.FindProperty("maintenanceFont").objectReferenceValue = maintenanceFont;
            serializedTheme.FindProperty("combatFeedbackFont").objectReferenceValue = feedbackFont;
            serializedTheme.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
        }

        private static void ConfigureTmpSettings(
            TMP_FontAsset primaryFont,
            TMP_FontAsset maintenanceFont)
        {
            var settings = TMP_Settings.LoadDefaultSettings();
            if (settings == null)
            {
                throw new InvalidOperationException("TMP default settings are missing.");
            }

            TMP_Settings.defaultFontAsset = primaryFont;
            TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset> { maintenanceFont };

            var serializedSettings = new SerializedObject(settings);
            serializedSettings.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static string CollectRequiredCharacters()
        {
            var characters = new SortedSet<char>(CommonUiSymbols);
            foreach (var assetFolder in CharacterSourceFolders)
            {
                var absoluteFolder = Path.Combine(
                    Directory.GetParent(Application.dataPath)!.FullName,
                    assetFolder.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(absoluteFolder))
                {
                    continue;
                }

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
                        if (IsRequiredUiCharacter(character))
                        {
                            characters.Add(character);
                        }
                    }
                }
            }

            return new string(characters.ToArray());
        }

        private static bool IsRequiredUiCharacter(char character)
        {
            return character is >= '\uAC00' and <= '\uD7A3' ||
                   CommonUiSymbols.IndexOf(character) >= 0;
        }

        private static void ValidateSourceCoverage(
            TMP_FontAsset fontAsset,
            string requiredCharacters)
        {
            var missing = new StringBuilder();
            foreach (var character in requiredCharacters)
            {
                if (!CanRenderFromSource(fontAsset, character, new HashSet<TMP_FontAsset>()))
                {
                    missing.Append(character);
                }
            }

            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    $"{fontAsset.name} and its fallbacks cannot render: {missing}");
            }
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

            if (fontAsset.fallbackFontAssetTable == null)
            {
                return false;
            }

            foreach (var fallback in fontAsset.fallbackFontAssetTable)
            {
                if (fallback != null &&
                    CanRenderFromSource(fallback, character, visited))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
