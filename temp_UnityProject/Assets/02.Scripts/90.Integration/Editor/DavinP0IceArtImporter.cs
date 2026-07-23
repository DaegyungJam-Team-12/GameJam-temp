#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Gameplay;
using Icebreaker.UI.Hud;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.Integration.Editor
{
    /// <summary>
    /// Compatibility entry point for the P0 art pipeline. The class name is kept so
    /// its script GUID and existing editor references remain stable.
    ///
    /// Import/Validate changes texture importer metadata only. Bind changes catalog
    /// and prefab references only. Rebuild is an explicit, independent operation.
    /// This class never opens or saves a scene.
    /// </summary>
    public static class DavinP0IceArtImporter
    {
        private const string IceFolder = "Assets/04.Images/20.Gameplay/Ice";
        private const string LauncherFolder = "Assets/04.Images/30.UI/Launcher";
        private const string IcebreakingFolder = "Assets/04.Images/30.UI/Icebreaking";
        private const string LauncherPrefabPath =
            "Assets/03.Prefabs/30.UI/Hud/UI_LauncherHud.prefab";
        private const string IcebreakingPrefabPath =
            "Assets/03.Prefabs/30.UI/Hud/UI_IcebreakingHud.prefab";
        private const string CatalogPath = "Assets/09.Data/Gameplay/IceVisualCatalog.asset";
        private const int IceFrameSize = 256;
        private const float UiPixelsPerUnit = 100f;

        private enum ArtRole
        {
            BaseIce,
            BaseIceAnimation,
            SpecialIceOverlay,
            SpecialIceOverlayAnimation,
            Environment,
            LauncherBackground,
            LauncherPanel,
            LauncherButton,
            IcebreakingPanel,
            IcebreakingButton,
        }

        private readonly struct ArtBinding
        {
            public ArtBinding(
                ArtRole role,
                string path,
                string spriteName,
                int frameCount = 1,
                Rect? sourceRect = null,
                Vector4? border = null,
                float pixelsPerUnit = UiPixelsPerUnit)
            {
                Role = role;
                Path = path;
                SpriteName = spriteName;
                FrameCount = frameCount;
                SourceRect = sourceRect;
                Border = border ?? Vector4.zero;
                PixelsPerUnit = pixelsPerUnit;
            }

            public ArtRole Role { get; }
            public string Path { get; }
            public string SpriteName { get; }
            public int FrameCount { get; }
            public Rect? SourceRect { get; }
            public Vector4 Border { get; }
            public float PixelsPerUnit { get; }
            public bool IsSheet => FrameCount > 1;
        }

        private static readonly ArtBinding[] Bindings =
        {
            IceStatic("T1_01"), IceStatic("T1_02"),
            IceStatic("T2_01"), IceStatic("T2_02"),
            IceStatic("T3_01"), IceStatic("T3_02"),
            IceSheet("T1_01", 6), IceSheet("T1_02", 6),
            IceSheet("T2_01", 6), IceSheet("T2_02", 6),
            IceSheet("T3_01", 6), IceSheet("T3_02", 6),
            OverlayStatic("Crystal"), OverlayStatic("Crack"),
            OverlaySheet("Crystal", 5), OverlaySheet("Crack", 6),
            new(
                ArtRole.Environment,
                "Assets/04.Images/10.Environment/bg_ocean.png",
                "bg_ocean"),
            new(
                ArtRole.Environment,
                "Assets/04.Images/20.Gameplay/Ship.png",
                "Ship"),
            new(
                ArtRole.LauncherBackground,
                $"{LauncherFolder}/bg_launcher.png",
                "bg_launcher"),
            Cropped(
                ArtRole.LauncherPanel,
                LauncherFolder,
                "lc_ui_money",
                new Rect(35f, 21f, 349f, 110f)),
            Cropped(
                ArtRole.LauncherPanel,
                LauncherFolder,
                "lc_ui_destination",
                new Rect(426f, 21f, 421f, 110f)),
            Cropped(
                ArtRole.LauncherButton,
                LauncherFolder,
                "lc_ui_ship_maintenance",
                new Rect(1141f, 32f, 224f, 45f)),
            Cropped(
                ArtRole.LauncherButton,
                LauncherFolder,
                "lc_ui_operational_status",
                new Rect(901f, 32f, 224f, 45f)),
            Cropped(
                ArtRole.LauncherButton,
                LauncherFolder,
                "lc_ui_stage",
                new Rect(888f, 22f, 487f, 109f)),
            Cropped(
                ArtRole.LauncherButton,
                LauncherFolder,
                "lc_ui_setting",
                new Rect(1411f, 22f, 154f, 109f)),
            Cropped(
                ArtRole.IcebreakingPanel,
                IcebreakingFolder,
                "ui_money",
                new Rect(16f, 972f, 353f, 96f)),
            Cropped(
                ArtRole.IcebreakingPanel,
                IcebreakingFolder,
                "ui_time",
                new Rect(1351f, 972f, 262f, 96f)),
            Cropped(
                ArtRole.IcebreakingButton,
                IcebreakingFolder,
                "ui_setting",
                new Rect(1624f, 972f, 279f, 96f)),
        };

        [MenuItem("Tools/Icebreaker/Art/1. Import and Validate")]
        public static void ImportAndValidate()
        {
            foreach (var binding in Bindings)
            {
                ConfigureImporter(binding);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateImportedAssets();
            Debug.Log("[ART-P0] Import/Validate completed without opening or saving scenes.");
        }

        [MenuItem("Tools/Icebreaker/Art/2. Bind Catalog and Prefabs")]
        public static void Bind()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<IceVisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"Run the project data setup before binding; missing '{CatalogPath}'.");
            }

            PopulateCatalog(catalog);
            ApplyLauncherPrefabOnly();
            ApplyIcebreakingHudPrefabOnly();
            AssetDatabase.SaveAssets();
            ValidateBoundAssets(catalog);
            Debug.Log(
                "[ART-P0] Catalog/prefab binding completed. Scene binding is intentionally " +
                "owned by the Runtime stream and was not changed.");
        }

        [MenuItem("Tools/Icebreaker/Art/3. Explicit Rebuild UI-02 Prefabs")]
        public static void Rebuild()
        {
            Icebreaker.UI.Editor.Ui02PrefabBuilder.Build();
            Debug.Log(
                "[ART-P0] UI-02 prefabs rebuilt once. Run Bind explicitly afterward when art " +
                "references need to be applied.");
        }

        [MenuItem("Tools/Icebreaker/Art/Validate Current Binding")]
        public static void ValidateCurrentBinding()
        {
            ValidateImportedAssets();
            var catalog = AssetDatabase.LoadAssetAtPath<IceVisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException($"Missing catalog '{CatalogPath}'.");
            }

            ValidateBoundAssets(catalog);
            Debug.Log("[ART-P0] Import metadata and asset bindings are valid.");
        }

        public static void ApplyLauncherPrefabOnly()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(LauncherPrefabPath);
            try
            {
                var hudRoot = RequireChild(prefabRoot.transform, "HudRoot");
                RemoveLegacyArtLayers(hudRoot);
                ConfigureImage(
                    hudRoot.GetComponent<Image>(),
                    LoadUiSprite(LauncherFolder, "bg_launcher"),
                    false);
                ConfigureImage(
                    RequireChild(hudRoot, "FundsArea").GetComponent<Image>(),
                    LoadUiSprite(LauncherFolder, "lc_ui_money"),
                    false);
                ConfigureImage(
                    RequireChild(hudRoot, "DestinationArea").GetComponent<Image>(),
                    LoadUiSprite(LauncherFolder, "lc_ui_destination"),
                    false);
                ConfigureButton(
                    RequireChild(hudRoot, "MaintenanceHitArea"),
                    LoadUiSprite(LauncherFolder, "lc_ui_ship_maintenance"));
                ConfigureButton(
                    RequireChild(hudRoot, "RouteHitArea"),
                    LoadUiSprite(LauncherFolder, "lc_ui_operational_status"));
                ConfigureButton(
                    RequireChild(hudRoot, "StageStartHitArea"),
                    LoadUiSprite(LauncherFolder, "lc_ui_stage"));
                ConfigureButton(
                    RequireChild(hudRoot, "SettingsHitArea"),
                    LoadUiSprite(LauncherFolder, "lc_ui_setting"));

                var presenter = prefabRoot.GetComponent<LauncherHudPresenter>() ??
                                throw new InvalidOperationException(
                                    $"LauncherHudPresenter is missing in '{LauncherPrefabPath}'.");
                var serialized = new SerializedObject(presenter);
                ClearArray(serialized, "panelGraphics");
                ClearArray(serialized, "accentGraphics");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, LauncherPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        public static void ApplyIcebreakingHudPrefabOnly()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(IcebreakingPrefabPath);
            try
            {
                var hudRoot = RequireChild(prefabRoot.transform, "HudRoot");
                RemoveLegacyArtLayers(hudRoot);
                ConfigureImage(
                    RequireChild(hudRoot, "FundsArea").GetComponent<Image>(),
                    LoadUiSprite(IcebreakingFolder, "ui_money"),
                    false);
                ConfigureImage(
                    RequireChild(hudRoot, "TimerArea").GetComponent<Image>(),
                    LoadUiSprite(IcebreakingFolder, "ui_time"),
                    false);
                ConfigureButton(
                    RequireChild(hudRoot, "SettingsHitArea"),
                    LoadUiSprite(IcebreakingFolder, "ui_setting"));

                var presenter = prefabRoot.GetComponent<IcebreakingHudPresenter>() ??
                                throw new InvalidOperationException(
                                    $"IcebreakingHudPresenter is missing in '{IcebreakingPrefabPath}'.");
                var serialized = new SerializedObject(presenter);
                ClearArray(serialized, "panelGraphics");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, IcebreakingPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static ArtBinding IceStatic(string name)
        {
            return new ArtBinding(
                ArtRole.BaseIce,
                $"{IceFolder}/{name}.png",
                name,
                pixelsPerUnit: IceFrameSize);
        }

        private static ArtBinding IceSheet(string name, int frameCount)
        {
            return new ArtBinding(
                ArtRole.BaseIceAnimation,
                $"{IceFolder}/{name}_spritesheet.png",
                $"{name}_spritesheet",
                frameCount,
                pixelsPerUnit: IceFrameSize);
        }

        private static ArtBinding OverlayStatic(string name)
        {
            return new ArtBinding(
                ArtRole.SpecialIceOverlay,
                $"{IceFolder}/{name}.png",
                name,
                pixelsPerUnit: IceFrameSize);
        }

        private static ArtBinding OverlaySheet(string name, int frameCount)
        {
            return new ArtBinding(
                ArtRole.SpecialIceOverlayAnimation,
                $"{IceFolder}/{name}_spritesheet.png",
                $"{name}_spritesheet",
                frameCount,
                pixelsPerUnit: IceFrameSize);
        }

        private static ArtBinding Cropped(
            ArtRole role,
            string folder,
            string name,
            Rect sourceRect)
        {
            return new ArtBinding(
                role,
                $"{folder}/{name}.png",
                name,
                sourceRect: sourceRect);
        }

        private static void ConfigureImporter(ArtBinding binding)
        {
            var importer = RequireImporter(binding.Path);
            ConfigureCommon(importer, binding.PixelsPerUnit);

            if (binding.IsSheet)
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
                var sprites = new SpriteMetaData[binding.FrameCount];
                for (var index = 0; index < binding.FrameCount; index++)
                {
                    sprites[index] = CreateMeta(
                        $"{binding.SpriteName}_{index}",
                        new Rect(index * IceFrameSize, 0f, IceFrameSize, IceFrameSize),
                        binding.Border);
                }

#pragma warning disable CS0618
                importer.spritesheet = sprites;
#pragma warning restore CS0618
            }
            else if (binding.SourceRect.HasValue)
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable CS0618
                importer.spritesheet = new[]
                {
                    CreateMeta(binding.SpriteName, binding.SourceRect.Value, binding.Border),
                };
#pragma warning restore CS0618
            }
            else
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePivot = new Vector2(0.5f, 0.5f);
                importer.spriteBorder = binding.Border;
            }

            importer.SaveAndReimport();
        }

        private static SpriteMetaData CreateMeta(string name, Rect rect, Vector4 border)
        {
            return new SpriteMetaData
            {
                name = name,
                rect = rect,
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = border,
            };
        }

        private static TextureImporter RequireImporter(string path)
        {
            return AssetImporter.GetAtPath(path) as TextureImporter ??
                   throw new InvalidOperationException($"Texture importer is missing for '{path}'.");
        }

        private static void ConfigureCommon(TextureImporter importer, float pixelsPerUnit)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.maxTextureSize = 2048;
        }

        private static void ValidateImportedAssets()
        {
            var errors = new List<string>();
            foreach (var binding in Bindings)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(binding.Path);
                if (texture == null)
                {
                    errors.Add($"Missing texture for role {binding.Role}: {binding.Path}");
                    continue;
                }

                if (binding.SourceRect is { } rect &&
                    (rect.xMin < 0f || rect.yMin < 0f ||
                     rect.xMax > texture.width || rect.yMax > texture.height))
                {
                    errors.Add(
                        $"{binding.Role} crop '{binding.SpriteName}' exceeds {texture.width}x{texture.height}.");
                }

                var sprites = AssetDatabase.LoadAllAssetsAtPath(binding.Path);
                var spriteCount = 0;
                foreach (var asset in sprites)
                {
                    if (asset is not Sprite sprite)
                    {
                        continue;
                    }

                    spriteCount++;
                    var normalizedPivot = new Vector2(
                        sprite.pivot.x / sprite.rect.width,
                        sprite.pivot.y / sprite.rect.height);
                    if (Vector2.Distance(normalizedPivot, new Vector2(0.5f, 0.5f)) > 0.001f)
                    {
                        errors.Add($"{binding.SpriteName} must use a centered pivot.");
                    }

                    if (sprite.border != binding.Border)
                    {
                        errors.Add($"{binding.SpriteName} has an unexpected 9-slice border.");
                    }
                }

                var expectedCount = binding.IsSheet || binding.SourceRect.HasValue
                    ? binding.FrameCount
                    : 1;
                if (spriteCount != expectedCount)
                {
                    errors.Add(
                        $"{binding.SpriteName} expected {expectedCount} sprite frame(s), found {spriteCount}.");
                }
            }

            ThrowIfErrors("Import/Validate", errors);
        }

        private static void PopulateCatalog(IceVisualCatalog catalog)
        {
            var serialized = new SerializedObject(catalog);
            Set(serialized, "t1Variant01", LoadIce<Sprite>("T1_01"));
            Set(serialized, "t1Variant02", LoadIce<Sprite>("T1_02"));
            Set(serialized, "t1Variant01Sheet", LoadIce<Texture2D>("T1_01_spritesheet"));
            Set(serialized, "t1Variant02Sheet", LoadIce<Texture2D>("T1_02_spritesheet"));
            Set(serialized, "t2Variant01", LoadIce<Sprite>("T2_01"));
            Set(serialized, "t2Variant02", LoadIce<Sprite>("T2_02"));
            Set(serialized, "t2Variant01Sheet", LoadIce<Texture2D>("T2_01_spritesheet"));
            Set(serialized, "t2Variant02Sheet", LoadIce<Texture2D>("T2_02_spritesheet"));
            Set(serialized, "t3Variant01", LoadIce<Sprite>("T3_01"));
            Set(serialized, "t3Variant02", LoadIce<Sprite>("T3_02"));
            Set(serialized, "t3Variant01Sheet", LoadIce<Texture2D>("T3_01_spritesheet"));
            Set(serialized, "t3Variant02Sheet", LoadIce<Texture2D>("T3_02_spritesheet"));
            Set(serialized, "crystal", LoadIce<Sprite>("Crystal"));
            Set(serialized, "crystalSheet", LoadIce<Texture2D>("Crystal_spritesheet"));
            Set(serialized, "crack", LoadIce<Sprite>("Crack"));
            Set(serialized, "crackSheet", LoadIce<Texture2D>("Crack_spritesheet"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static T LoadIce<T>(string name) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>($"{IceFolder}/{name}.png") ??
                   throw new InvalidOperationException($"Missing ice role asset '{name}'.");
        }

        private static void ValidateBoundAssets(IceVisualCatalog catalog)
        {
            var errors = new List<string>();
            if (!catalog.IsComplete)
            {
                errors.Add("IceVisualCatalog is incomplete.");
            }

            ValidateImageUsage(LauncherPrefabPath, errors);
            ValidateImageUsage(IcebreakingPrefabPath, errors);
            Icebreaker.UI.Editor.ProductionUiGuard.CollectErrors(
                AssetDatabase.LoadAssetAtPath<GameObject>(LauncherPrefabPath),
                errors);
            Icebreaker.UI.Editor.ProductionUiGuard.CollectErrors(
                AssetDatabase.LoadAssetAtPath<GameObject>(IcebreakingPrefabPath),
                errors);
            ThrowIfErrors("Bind", errors);
        }

        private static void ValidateImageUsage(string prefabPath, ICollection<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                errors.Add($"Missing prefab: {prefabPath}");
                return;
            }

            foreach (var image in prefab.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null)
                {
                    continue;
                }

                var hasBorder = image.sprite.border.sqrMagnitude > 0f;
                if (image.type == Image.Type.Sliced && !hasBorder)
                {
                    errors.Add(
                        $"{prefab.name}/{image.name} uses Sliced without a sprite border.");
                }
                else if (hasBorder && image.type == Image.Type.Simple)
                {
                    errors.Add(
                        $"{prefab.name}/{image.name} has a 9-slice border but uses Simple.");
                }
            }
        }

        private static void Set(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object asset)
        {
            var property = serialized.FindProperty(propertyName) ??
                           throw new InvalidOperationException(
                               $"IceVisualCatalog property '{propertyName}' is missing.");
            property.objectReferenceValue = asset;
        }

        private static void RemoveLegacyArtLayers(Transform hudRoot)
        {
            var existingArt = hudRoot.Find("ArtLayers");
            if (existingArt != null)
            {
                UnityEngine.Object.DestroyImmediate(existingArt.gameObject);
            }
        }

        private static void ConfigureImage(Image? image, Sprite sprite, bool raycastTarget)
        {
            if (image == null)
            {
                throw new InvalidOperationException(
                    $"A UI Image is missing for role sprite '{sprite.name}'.");
            }

            image.sprite = sprite;
            image.type = sprite.border.sqrMagnitude > 0f
                ? Image.Type.Sliced
                : Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
            image.raycastTarget = raycastTarget;
            EditorUtility.SetDirty(image);
        }

        private static void ConfigureButton(Transform hitArea, Sprite sprite)
        {
            var button = hitArea.GetComponent<Button>() ??
                         throw new InvalidOperationException(
                             $"HUD button '{hitArea.name}' is missing Button.");
            var image = hitArea.GetComponent<Image>() ??
                        throw new InvalidOperationException(
                            $"HUD button '{hitArea.name}' is missing Image.");
            ConfigureImage(image, sprite, true);
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = image;
            EditorUtility.SetDirty(button);
        }

        private static Sprite LoadUiSprite(string folder, string spriteName)
        {
            var path = $"{folder}/{spriteName}.png";
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite && sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            throw new InvalidOperationException(
                $"Missing role sprite '{spriteName}' in '{path}'.");
        }

        private static Transform RequireChild(Transform parent, string childName)
        {
            return parent.Find(childName) ??
                   throw new InvalidOperationException(
                       $"Required UI object '{parent.name}/{childName}' is missing.");
        }

        private static void ClearArray(SerializedObject serialized, string propertyName)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                throw new InvalidOperationException(
                    $"Presenter array '{propertyName}' was not found.");
            }

            property.arraySize = 0;
        }

        private static void ThrowIfErrors(string stage, ICollection<string> errors)
        {
            if (errors.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                $"[ART-P0] {stage} validation failed:\n- {string.Join("\n- ", errors)}");
        }
    }
}
