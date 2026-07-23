#nullable enable

using System;
using Icebreaker.Gameplay;
using Icebreaker.UI.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.Integration.Editor
{
    public static class DavinP0IceArtImporter
    {
        private const string ArtFolder = "Assets/04.Images/20.Gameplay/Ice";
        private const string OceanPath = "Assets/04.Images/10.Environment/bg_ocean.png";
        private const string ShipPath = "Assets/04.Images/20.Gameplay/Ship.png";
        private const string LauncherArtFolder = "Assets/04.Images/30.UI/Launcher";
        private const string IcebreakingHudArtFolder = "Assets/04.Images/30.UI/Icebreaking";
        private const string LauncherPrefabPath =
            "Assets/03.Prefabs/30.UI/Hud/UI_LauncherHud.prefab";
        private const string IcebreakingHudPrefabPath =
            "Assets/03.Prefabs/30.UI/Hud/UI_IcebreakingHud.prefab";
        private const string DataFolder = "Assets/09.Data/Gameplay";
        private const string CatalogPath = DataFolder + "/IceVisualCatalog.asset";
        private const int CellSize = 256;
        private const float FullScreenPixelsPerUnit = 100f;

        private static readonly string[] StaticNames =
        {
            "T1_01", "T1_02",
            "T2_01", "T2_02",
            "T3_01", "T3_02",
            "Crystal",
            "Crack",
        };

        private static readonly (string Name, int FrameCount)[] Sheets =
        {
            ("T1_01_spritesheet", 6), ("T1_02_spritesheet", 6),
            ("T2_01_spritesheet", 6), ("T2_02_spritesheet", 6),
            ("T3_01_spritesheet", 6), ("T3_02_spritesheet", 6),
            ("Crystal_spritesheet", 5),
            ("Crack_spritesheet", 6),
        };

        private static readonly string[] ScenePaths =
        {
            "Assets/01.Scenes/minjun.unity",
            "Assets/01.Scenes/int02_complete_loop.unity",
            "Assets/01.Scenes/siyeon.unity",
        };

        // Source-space rectangles use Unity's bottom-left texture origin.
        private static readonly (string Name, Rect Rect)[] LauncherWidgetSprites =
        {
            ("lc_ui_money", new Rect(35f, 21f, 349f, 110f)),
            ("lc_ui_destination", new Rect(426f, 21f, 421f, 110f)),
            ("lc_ui_ship_maintenance", new Rect(1141f, 32f, 224f, 45f)),
            ("lc_ui_operational_status", new Rect(901f, 32f, 224f, 45f)),
            ("lc_ui_stage", new Rect(888f, 22f, 487f, 109f)),
            ("lc_ui_setting", new Rect(1411f, 22f, 154f, 109f)),
        };

        private static readonly (string Name, Rect Rect)[] IcebreakingWidgetSprites =
        {
            ("ui_money", new Rect(16f, 972f, 353f, 96f)),
            ("ui_time", new Rect(1351f, 972f, 262f, 96f)),
            ("ui_setting", new Rect(1624f, 972f, 279f, 96f)),
        };

        [MenuItem("Tools/Icebreaker/Art/Apply Davin P0 Art")]
        public static void Apply()
        {
            foreach (var name in StaticNames)
            {
                ConfigureStaticSprite($"{ArtFolder}/{name}.png", CellSize);
            }

            foreach (var sheet in Sheets)
            {
                ConfigureSpriteSheet(
                    $"{ArtFolder}/{sheet.Name}.png",
                    sheet.FrameCount);
            }

            ConfigureStaticSprite(OceanPath, FullScreenPixelsPerUnit);
            ConfigureStaticSprite(ShipPath, FullScreenPixelsPerUnit);
            ConfigureStaticSprite(
                $"{LauncherArtFolder}/bg_launcher.png",
                FullScreenPixelsPerUnit);
            foreach (var widget in LauncherWidgetSprites)
            {
                ConfigureCroppedUiSprite(
                    $"{LauncherArtFolder}/{widget.Name}.png",
                    widget.Name,
                    widget.Rect);
            }
            foreach (var widget in IcebreakingWidgetSprites)
            {
                ConfigureCroppedUiSprite(
                    $"{IcebreakingHudArtFolder}/{widget.Name}.png",
                    widget.Name,
                    widget.Rect);
            }

            EnsureFolder("Assets/09.Data", "Gameplay");
            var catalog = AssetDatabase.LoadAssetAtPath<IceVisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<IceVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            PopulateCatalog(catalog);
            AssetDatabase.SaveAssets();
            ApplyLauncherPrefabOnly();
            ApplyIcebreakingHudPrefabOnly();
            AssignCatalogToScenes();
            AssetDatabase.Refresh();

            if (!catalog.IsComplete)
            {
                throw new InvalidOperationException("IceVisualCatalog remained incomplete after import.");
            }

            Debug.Log(
                "[ART-P0] Davin ocean, ship, launcher/combat HUD, T1/T2/T3, crystal-ice, and crack-ice art imported and assigned.");
        }

        [MenuItem("Tools/Icebreaker/Art/Rebuild Encapsulated HUD Prefabs")]
        public static void RebuildEncapsulatedHudPrefabs()
        {
            Icebreaker.UI.Editor.Ui02PrefabBuilder.Build();
            Apply();
            Icebreaker.UI.Editor.Ui02PrefabBuilder.Build();
            Debug.Log(
                "[ART-P0] Launcher and icebreaking HUDs rebuilt with art-backed parent panels.");
        }

        public static void ApplyLauncherPrefabOnly()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(LauncherPrefabPath);
            try
            {
                var hudRoot = prefabRoot.transform.Find("HudRoot");
                if (hudRoot == null)
                {
                    throw new InvalidOperationException(
                        $"HudRoot was not found in '{LauncherPrefabPath}'.");
                }

                RemoveLegacyArtLayers(hudRoot);
                ConfigurePanelArt(
                    hudRoot.GetComponent<Image>(),
                    LoadUiSprite(LauncherArtFolder, "bg_launcher"));
                ConfigurePanelArt(
                    RequireChild(hudRoot, "FundsArea").GetComponent<Image>(),
                    LoadUiSprite(LauncherArtFolder, "lc_ui_money"));
                ConfigurePanelArt(
                    RequireChild(hudRoot, "DestinationArea").GetComponent<Image>(),
                    LoadUiSprite(LauncherArtFolder, "lc_ui_destination"));
                ConfigureArtBackedButton(
                    RequireChild(hudRoot, "MaintenanceHitArea"),
                    LoadUiSprite(LauncherArtFolder, "lc_ui_ship_maintenance"));
                ConfigureArtBackedButton(
                    RequireChild(hudRoot, "RouteHitArea"),
                    LoadUiSprite(LauncherArtFolder, "lc_ui_operational_status"));
                ConfigureArtBackedButton(
                    RequireChild(hudRoot, "StageStartHitArea"),
                    LoadUiSprite(LauncherArtFolder, "lc_ui_stage"));
                ConfigureArtBackedButton(
                    RequireChild(hudRoot, "SettingsHitArea"),
                    LoadUiSprite(LauncherArtFolder, "lc_ui_setting"));

                var presenter = prefabRoot.GetComponent<LauncherHudPresenter>();
                if (presenter == null)
                {
                    throw new InvalidOperationException(
                        $"LauncherHudPresenter was not found in '{LauncherPrefabPath}'.");
                }

                var serialized = new SerializedObject(presenter);
                ClearArray(serialized, "panelGraphics");
                ClearArray(serialized, "accentGraphics");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(presenter);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, LauncherPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        public static void ApplyIcebreakingHudPrefabOnly()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(IcebreakingHudPrefabPath);
            try
            {
                var hudRoot = prefabRoot.transform.Find("HudRoot");
                if (hudRoot == null)
                {
                    throw new InvalidOperationException(
                        $"HudRoot was not found in '{IcebreakingHudPrefabPath}'.");
                }

                RemoveLegacyArtLayers(hudRoot);
                ConfigurePanelArt(
                    RequireChild(hudRoot, "FundsArea").GetComponent<Image>(),
                    LoadUiSprite(IcebreakingHudArtFolder, "ui_money"));
                ConfigurePanelArt(
                    RequireChild(hudRoot, "TimerArea").GetComponent<Image>(),
                    LoadUiSprite(IcebreakingHudArtFolder, "ui_time"));
                ConfigureArtBackedButton(
                    RequireChild(hudRoot, "SettingsHitArea"),
                    LoadUiSprite(IcebreakingHudArtFolder, "ui_setting"));

                var presenter = prefabRoot.GetComponent<IcebreakingHudPresenter>();
                if (presenter == null)
                {
                    throw new InvalidOperationException(
                        $"IcebreakingHudPresenter was not found in '{IcebreakingHudPrefabPath}'.");
                }

                var serialized = new SerializedObject(presenter);
                ClearArray(serialized, "panelGraphics");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(presenter);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, IcebreakingHudPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ConfigureStaticSprite(string path, float pixelsPerUnit)
        {
            var importer = RequireImporter(path);
            ConfigureCommon(importer, pixelsPerUnit);
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SaveAndReimport();
        }

        private static void ConfigureCroppedUiSprite(
            string path,
            string spriteName,
            Rect sourceRect)
        {
            var importer = RequireImporter(path);
            ConfigureCommon(importer, FullScreenPixelsPerUnit);
            importer.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable CS0618
            importer.spritesheet = new[]
            {
                new SpriteMetaData
                {
                    name = spriteName,
                    rect = sourceRect,
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero,
                },
            };
#pragma warning restore CS0618
            importer.SaveAndReimport();
        }

        private static void ConfigureSpriteSheet(string path, int frameCount)
        {
            var importer = RequireImporter(path);
            ConfigureCommon(importer, CellSize);
            importer.spriteImportMode = SpriteImportMode.Multiple;

            var sprites = new SpriteMetaData[frameCount];
            for (var i = 0; i < sprites.Length; i++)
            {
                sprites[i] = new SpriteMetaData
                {
                    name = $"{System.IO.Path.GetFileNameWithoutExtension(path)}_{i}",
                    rect = new Rect(i * CellSize, 0f, CellSize, CellSize),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero,
                };
            }

#pragma warning disable CS0618
            importer.spritesheet = sprites;
#pragma warning restore CS0618
            importer.SaveAndReimport();
        }

        private static TextureImporter RequireImporter(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                throw new InvalidOperationException($"Texture importer is missing for '{path}'.");
            }

            return importer;
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

        private static void PopulateCatalog(IceVisualCatalog catalog)
        {
            var serialized = new SerializedObject(catalog);

            Set(serialized, "t1Variant01", Load<Sprite>("T1_01"));
            Set(serialized, "t1Variant02", Load<Sprite>("T1_02"));
            Set(serialized, "t1Variant01Sheet", Load<Texture2D>("T1_01_spritesheet"));
            Set(serialized, "t1Variant02Sheet", Load<Texture2D>("T1_02_spritesheet"));

            Set(serialized, "t2Variant01", Load<Sprite>("T2_01"));
            Set(serialized, "t2Variant02", Load<Sprite>("T2_02"));
            Set(serialized, "t2Variant01Sheet", Load<Texture2D>("T2_01_spritesheet"));
            Set(serialized, "t2Variant02Sheet", Load<Texture2D>("T2_02_spritesheet"));

            Set(serialized, "t3Variant01", Load<Sprite>("T3_01"));
            Set(serialized, "t3Variant02", Load<Sprite>("T3_02"));
            Set(serialized, "t3Variant01Sheet", Load<Texture2D>("T3_01_spritesheet"));
            Set(serialized, "t3Variant02Sheet", Load<Texture2D>("T3_02_spritesheet"));

            Set(serialized, "crystal", Load<Sprite>("Crystal"));
            Set(serialized, "crystalSheet", Load<Texture2D>("Crystal_spritesheet"));
            Set(serialized, "crack", Load<Sprite>("Crack"));
            Set(serialized, "crackSheet", Load<Texture2D>("Crack_spritesheet"));

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static T Load<T>(string name) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>($"{ArtFolder}/{name}.png");
            if (asset == null)
            {
                throw new InvalidOperationException($"Required ice asset '{name}' could not be loaded.");
            }

            return asset;
        }

        private static void Set(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object asset)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"IceVisualCatalog property '{propertyName}' was not found.");
            }

            property.objectReferenceValue = asset;
        }

        private static void AssignCatalogToScenes()
        {
            foreach (var scenePath in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                // Opening a scene can unload an otherwise unused asset, so load the catalog only
                // after the scene transition that precedes its assignment.
                var catalog = AssetDatabase.LoadAssetAtPath<IceVisualCatalog>(CatalogPath);
                if (catalog == null)
                {
                    throw new InvalidOperationException(
                        $"IceVisualCatalog could not be reloaded before editing '{scenePath}'.");
                }

                var views = UnityEngine.Object.FindObjectsByType<IceFieldView>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                if (views.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"No IceFieldView was found in '{scenePath}'.");
                }

                foreach (var view in views)
                {
                    var viewSerialized = new SerializedObject(view);
                    var catalogProperty = viewSerialized.FindProperty("iceVisualCatalog");
                    if (catalogProperty == null)
                    {
                        throw new InvalidOperationException(
                            $"IceFieldView catalog property was not found in '{scenePath}'.");
                    }

                    catalogProperty.objectReferenceValue = catalog;
                    viewSerialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(view);

                    var sceneCamera = viewSerialized.FindProperty("sceneCamera")?.objectReferenceValue
                        as Camera;
                    if (sceneCamera == null)
                    {
                        throw new InvalidOperationException(
                            $"IceFieldView camera reference was not found in '{scenePath}'.");
                    }

                    var backdrop = view.GetComponent<GameplayBackdropView>();
                    if (backdrop == null)
                    {
                        backdrop = view.gameObject.AddComponent<GameplayBackdropView>();
                    }

                    var ocean = AssetDatabase.LoadAssetAtPath<Sprite>(OceanPath);
                    var ship = AssetDatabase.LoadAssetAtPath<Sprite>(ShipPath);
                    if (ocean == null || ship == null)
                    {
                        throw new InvalidOperationException(
                            $"Ocean or ship art could not be loaded for '{scenePath}'.");
                    }

                    var backdropSerialized = new SerializedObject(backdrop);
                    Set(backdropSerialized, "sceneCamera", sceneCamera);
                    Set(backdropSerialized, "oceanBackground", ocean);
                    Set(backdropSerialized, "ship", ship);
                    backdropSerialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(backdrop);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void RemoveLegacyArtLayers(Transform hudRoot)
        {
            var existingArt = hudRoot.Find("ArtLayers");
            if (existingArt != null)
            {
                UnityEngine.Object.DestroyImmediate(existingArt.gameObject);
            }
        }

        private static void ConfigurePanelArt(Image? panel, Sprite sprite)
        {
            if (panel == null)
            {
                throw new InvalidOperationException(
                    $"A parent UI panel is missing for sprite '{sprite.name}'.");
            }

            panel.sprite = sprite;
            panel.type = Image.Type.Simple;
            panel.preserveAspect = false;
            panel.color = Color.white;
            panel.raycastTarget = false;
            EditorUtility.SetDirty(panel);
        }

        private static Sprite LoadUiSprite(string artFolder, string layerName)
        {
            var path = $"{artFolder}/{layerName}.png";
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite && sprite.name == layerName)
                {
                    return sprite;
                }
            }

            throw new InvalidOperationException(
                $"Required UI sprite '{layerName}' could not be loaded from '{path}'.");
        }

        private static Transform RequireChild(Transform parent, string childName)
        {
            return parent.Find(childName) ??
                   throw new InvalidOperationException(
                       $"Required UI object '{parent.name}/{childName}' is missing.");
        }

        private static void ConfigureArtBackedButton(Transform hitArea, Sprite sprite)
        {
            var button = hitArea.GetComponent<Button>();
            var hitImage = hitArea.GetComponent<Image>();
            if (button == null || hitImage == null)
            {
                throw new InvalidOperationException(
                    $"HUD button '{hitArea.name}' is missing its Button or parent Image.");
            }

            hitImage.sprite = sprite;
            hitImage.type = Image.Type.Simple;
            hitImage.preserveAspect = false;
            hitImage.color = Color.white;
            hitImage.raycastTarget = true;
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = hitImage;
            EditorUtility.SetDirty(hitImage);
            EditorUtility.SetDirty(button);
        }

        private static void ClearArray(SerializedObject serialized, string propertyName)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                throw new InvalidOperationException(
                    $"LauncherHudPresenter array '{propertyName}' was not found.");
            }

            property.arraySize = 0;
        }
    }
}
