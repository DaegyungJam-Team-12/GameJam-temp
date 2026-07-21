#nullable enable

using System;
using System.Collections.Generic;
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
        private const string SourceFontPath =
            "Assets/04.Images/30.UI/Fonts/NotoSansKR-Variable.ttf";
        private const string FontAssetPath =
            "Assets/04.Images/30.UI/Fonts/NotoSansKR-Variable SDF.asset";
        private const string ThemePath = "Assets/04.Images/30.UI/Theme/UiTheme.asset";

        private const string UiCharacters =
            "정비 자금 치명타 연쇄 획득 파괴한 얼음 목적지 진행 반영 완료 " +
            "섬마을 등대항 북쪽 기지 자동 항해 도달 초 뒤 계속하기 작업 정산 개";

        static UiKoreanFontInstaller()
        {
            EditorApplication.delayCall += InstallIfNeeded;
        }

        [MenuItem("ICEBREAKER/UI/Install Korean TMP Font")]
        public static void Install()
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                throw new InvalidOperationException($"Korean source font was not found at {SourceFontPath}.");
            }

            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
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
                fontAsset.name = "NotoSansKR-Variable SDF";
                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

                if (fontAsset.atlasTexture != null && !AssetDatabase.Contains(fontAsset.atlasTexture))
                {
                    fontAsset.atlasTexture.name = "NotoSansKR-Variable Atlas";
                    AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
                }

                if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
                {
                    fontAsset.material.name = "NotoSansKR-Variable Atlas Material";
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }
            }

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = true;
            if (!fontAsset.TryAddCharacters(UiCharacters, out var missingCharacters))
            {
                Debug.LogWarning($"[UI Font] Noto Sans KR is missing: {missingCharacters}", fontAsset);
            }

            AssignThemeFont(fontAsset);
            AddGlobalFallback(fontAsset);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Ui04PrefabBuilder.Build();
            Debug.Log("[UI Font] Noto Sans KR was installed and UI-04 was rebuilt.", fontAsset);
        }

        private static void InstallIfNeeded()
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                return;
            }

            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            var theme = AssetDatabase.LoadAssetAtPath<UiThemeAsset>(ThemePath);
            if (fontAsset != null && theme != null && theme.CommonFont == fontAsset &&
                TMP_Settings.fallbackFontAssets?.Contains(fontAsset) == true)
            {
                return;
            }

            Install();
        }

        private static void AssignThemeFont(TMP_FontAsset fontAsset)
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiThemeAsset>(ThemePath);
            if (theme == null)
            {
                throw new InvalidOperationException($"UI theme was not found at {ThemePath}.");
            }

            var serializedTheme = new SerializedObject(theme);
            serializedTheme.FindProperty("commonFont").objectReferenceValue = fontAsset;
            serializedTheme.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
        }

        private static void AddGlobalFallback(TMP_FontAsset fontAsset)
        {
            var settings = TMP_Settings.LoadDefaultSettings();
            if (settings == null)
            {
                throw new InvalidOperationException("TMP default settings are missing.");
            }

            TMP_Settings.fallbackFontAssets ??= new List<TMP_FontAsset>();
            if (!TMP_Settings.fallbackFontAssets.Contains(fontAsset))
            {
                TMP_Settings.fallbackFontAssets.Add(fontAsset);
            }

            EditorUtility.SetDirty(settings);
        }
    }
}
