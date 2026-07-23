#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.State;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.Gameplay.Tests
{
    public sealed class IceVisualCatalogTests
    {
        private const string ArtFolder = "Assets/04.Images/20.Gameplay/Ice";
        private const string CatalogPath = "Assets/09.Data/Gameplay/IceVisualCatalog.asset";
        private const string OceanPath = "Assets/04.Images/10.Environment/bg_ocean.png";
        private const string ShipPath = "Assets/04.Images/20.Gameplay/Ship.png";
        private const string LauncherArtFolder = "Assets/04.Images/30.UI/Launcher";
        private const string IcebreakingHudArtFolder = "Assets/04.Images/30.UI/Icebreaking";
        private const string LauncherPrefabPath =
            "Assets/03.Prefabs/30.UI/Hud/UI_LauncherHud.prefab";
        private const string IcebreakingHudPrefabPath =
            "Assets/03.Prefabs/30.UI/Hud/UI_IcebreakingHud.prefab";

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

        [Test]
        public void Catalog_MapsBothTierVariantsAndCrack()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<IceVisualCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog!.IsComplete, Is.True);
            Assert.That(catalog.ResolveStaticSprite(IceTier.T1, SpecialIceType.None, 2)!.name,
                Is.EqualTo("T1_01"));
            Assert.That(catalog.ResolveStaticSprite(IceTier.T1, SpecialIceType.None, 3)!.name,
                Is.EqualTo("T1_02"));
            Assert.That(catalog.ResolveStaticSprite(IceTier.T2, SpecialIceType.None, 2)!.name,
                Is.EqualTo("T2_01"));
            Assert.That(catalog.ResolveStaticSprite(IceTier.T2, SpecialIceType.None, 3)!.name,
                Is.EqualTo("T2_02"));
            Assert.That(catalog.ResolveStaticSprite(IceTier.T3, SpecialIceType.None, 2)!.name,
                Is.EqualTo("T3_01"));
            Assert.That(catalog.ResolveStaticSprite(IceTier.T3, SpecialIceType.None, 3)!.name,
                Is.EqualTo("T3_02"));
            Assert.That(catalog.ResolveStaticSprite(IceTier.T2, SpecialIceType.Crystal, 2)!.name,
                Is.EqualTo("Crystal"));
            Assert.That(catalog.ResolveDestructionSheet(IceTier.T2, SpecialIceType.Crystal, 2)!.name,
                Is.EqualTo("Crystal_spritesheet"));
            Assert.That(catalog.ResolveStaticSprite(IceTier.T3, SpecialIceType.Crack, 2)!.name,
                Is.EqualTo("Crack"));
            Assert.That(catalog.ResolveDestructionSheet(IceTier.T1, SpecialIceType.Crack, 2)!.name,
                Is.EqualTo("Crack_spritesheet"));
            Assert.That(catalog.ResolveSpecialOverlaySprite(SpecialIceType.Crystal)!.name,
                Is.EqualTo("Crystal"));
            Assert.That(catalog.ResolveSpecialOverlaySheet(SpecialIceType.Crystal)!.name,
                Is.EqualTo("Crystal_spritesheet"));
            Assert.That(catalog.ResolveSpecialOverlaySprite(SpecialIceType.Crack)!.name,
                Is.EqualTo("Crack"));
            Assert.That(catalog.ResolveSpecialOverlaySheet(SpecialIceType.Crack)!.name,
                Is.EqualTo("Crack_spritesheet"));
            Assert.That(catalog.ResolveSpecialOverlaySprite(SpecialIceType.None), Is.Null);
        }

        [Test]
        public void StaticSprites_UseApprovedGameplayImportSettings()
        {
            foreach (var name in StaticNames)
            {
                var importer = LoadImporter(name);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), name);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), name);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(256f), name);
                Assert.That(importer.spritePivot, Is.EqualTo(new Vector2(0.5f, 0.5f)), name);
                Assert.That(importer.mipmapEnabled, Is.False, name);
                Assert.That(importer.sRGBTexture, Is.True, name);
                Assert.That(importer.alphaIsTransparency, Is.True, name);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear), name);
                Assert.That(
                    importer.textureCompression,
                    Is.EqualTo(TextureImporterCompression.Uncompressed),
                    name);
            }
        }

        [TestCase(OceanPath)]
        [TestCase(ShipPath)]
        public void FullScreenLayers_UseApprovedImportSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer!.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), path);
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f), path);
            Assert.That(importer.spritePivot, Is.EqualTo(new Vector2(0.5f, 0.5f)), path);
            Assert.That(importer.mipmapEnabled, Is.False, path);
            Assert.That(importer.sRGBTexture, Is.True, path);
            Assert.That(importer.alphaIsTransparency, Is.True, path);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear), path);
            Assert.That(
                importer.textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed),
                path);
        }

        [Test]
        public void LauncherLayers_UseApprovedImportSettings()
        {
            AssertFullScreenUiImporter($"{LauncherArtFolder}/bg_launcher.png");
            foreach (var widget in LauncherWidgetSprites)
            {
                AssertCroppedUiImporter(
                    $"{LauncherArtFolder}/{widget.Name}.png",
                    widget.Name,
                    widget.Rect);
            }
        }

        [Test]
        public void IcebreakingHudLayers_UseApprovedImportSettings()
        {
            foreach (var widget in IcebreakingWidgetSprites)
            {
                AssertCroppedUiImporter(
                    $"{IcebreakingHudArtFolder}/{widget.Name}.png",
                    widget.Name,
                    widget.Rect);
            }
        }

        [Test]
        public void LauncherPrefab_ParentsArtToMatchingLogicalControls()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LauncherPrefabPath);

            Assert.That(prefab, Is.Not.Null);
            var hudRoot = prefab!.transform.Find("HudRoot");
            Assert.That(hudRoot, Is.Not.Null);
            Assert.That(hudRoot!.Find("ArtLayers"), Is.Null);
            Assert.That(hudRoot.Find("BackgroundArt"), Is.Null);
            AssertPanelArt(hudRoot, "bg_launcher", false);
            AssertPanelArt(hudRoot.Find("FundsArea"), "lc_ui_money", false);
            AssertPanelArt(hudRoot.Find("DestinationArea"), "lc_ui_destination", false);
            Assert.That(hudRoot.Find("StageArea"), Is.Null);
            Assert.That(hudRoot.Find("FundsArea/FundsText"), Is.Not.Null);
            Assert.That(hudRoot.Find("DestinationArea/DestinationNameText"), Is.Not.Null);
            Assert.That(hudRoot.Find("DestinationArea/DestinationProgressText"), Is.Not.Null);

            foreach (var mapping in new[]
                     {
                         ("MaintenanceHitArea", "lc_ui_ship_maintenance"),
                         ("RouteHitArea", "lc_ui_operational_status"),
                         ("StageStartHitArea", "lc_ui_stage"),
                     })
            {
                var hitArea = hudRoot.Find(mapping.Item1);
                var button = hitArea?.GetComponent<Button>();
                var hitImage = hitArea?.GetComponent<Image>();
                AssertPanelArt(hitArea, mapping.Item2, true);
                Assert.That(button, Is.Not.Null, mapping.Item1);
                Assert.That(button!.targetGraphic, Is.SameAs(hitImage), mapping.Item1);
                AssertTextChild(hitArea!, "Label");
                Assert.That(hitArea!.Find("Visual"), Is.Null, mapping.Item1);
            }

            var settings = hudRoot.Find("SettingsHitArea");
            var settingsButton = settings?.GetComponent<Button>();
            var settingsImage = settings?.GetComponent<Image>();
            AssertPanelArt(settings, "lc_ui_setting", true);
            Assert.That(settingsButton, Is.Not.Null);
            Assert.That(settingsButton!.targetGraphic, Is.SameAs(settingsImage));
            AssertTextChild(settings!, "Label");
            Assert.That(settings!.Find("Visual"), Is.Null);

            var progressTrack = hudRoot.Find("DestinationArea/ProgressTrack")?.GetComponent<Image>();
            var progressFill =
                hudRoot.Find("DestinationArea/ProgressTrack/ProgressFill")?.GetComponent<Image>();
            Assert.That(progressTrack, Is.Not.Null);
            Assert.That(progressTrack!.color.a, Is.GreaterThan(0f));
            Assert.That(progressFill, Is.Not.Null);
            Assert.That(progressFill!.color.a, Is.GreaterThan(0f));
            Assert.That(progressFill.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(progressFill.fillMethod, Is.EqualTo(Image.FillMethod.Horizontal));
            Assert.That(
                progressFill.sprite,
                Is.Not.Null,
                "A Filled Image without a sprite renders at full width regardless of fillAmount.");

            var presenter = prefab
                .GetComponents<MonoBehaviour>()
                .SingleOrDefault(component =>
                    component.GetType().FullName == "Icebreaker.UI.Hud.LauncherHudPresenter");
            Assert.That(presenter, Is.Not.Null);
            var serialized = new SerializedObject(presenter);
            Assert.That(serialized.FindProperty("panelGraphics")!.arraySize, Is.EqualTo(0));
            Assert.That(serialized.FindProperty("accentGraphics")!.arraySize, Is.EqualTo(0));
            Assert.That(serialized.FindProperty("themedTexts")!.arraySize, Is.GreaterThan(0));
        }

        [TestCase(0, 120, 0f, "0/120")]
        [TestCase(37, 120, 37f / 120f, "37/120")]
        [TestCase(120, 120, 1f, "120/120")]
        [TestCase(0, 40, 0f, "0/40")]
        [TestCase(10, 40, 0.25f, "10/40")]
        [TestCase(40, 40, 1f, "40/40")]
        public void LauncherPresenter_RendersTextAndFillFromSameProgressState(
            int progress,
            int target,
            float expectedFill,
            string expectedText)
        {
            var root = new GameObject("LauncherPresenterTest");
            root.SetActive(false);
            try
            {
                var presenterType = FindType("Icebreaker.UI.Hud.LauncherHudPresenter");
                var presenter = root.AddComponent(presenterType);

                var fillObject = new GameObject(
                    "ProgressFill",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                fillObject.transform.SetParent(root.transform, false);
                var fill = fillObject.GetComponent<Image>();

                var textType = FindType("TMPro.TextMeshProUGUI");
                var textObject = new GameObject(
                    "ProgressText",
                    typeof(RectTransform),
                    typeof(CanvasRenderer));
                textObject.transform.SetParent(root.transform, false);
                var text = textObject.AddComponent(textType);

                presenterType
                    .GetField("destinationProgressFill", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(presenter, fill);
                presenterType
                    .GetField("destinationProgressText", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(presenter, text);

                var state = new GameState(
                    GamePhase.Traveling,
                    remainingSeconds: 0d,
                    isPaused: false,
                    funds: 0L,
                    currentDestinationId: "destination-test",
                    destinationProgress: progress,
                    destinationTarget: target,
                    maintenanceLevels: Array.Empty<MaintenanceLevel>(),
                    firstDestroyShown: true,
                    canStartStage: false);

                presenterType
                    .GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(presenter, new object[] { state });

                Assert.That(fill.fillAmount, Is.EqualTo(expectedFill).Within(0.0001f));
                Assert.That(textType.GetProperty("text")!.GetValue(text), Is.EqualTo(expectedText));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(0f)]
        [TestCase(37f / 120f)]
        [TestCase(1f)]
        public void LauncherProgressFill_GeneratesExpectedMeshWidth(float fillAmount)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LauncherPrefabPath);
            var source = prefab!.transform
                .Find("HudRoot/DestinationArea/ProgressTrack/ProgressFill")
                .GetComponent<Image>();
            var fillObject = new GameObject(
                "ProgressFillMeshTest",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            try
            {
                var rect = fillObject.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(184f, 12f);
                var fill = fillObject.GetComponent<Image>();
                fill.sprite = source.sprite;
                fill.type = source.type;
                fill.fillMethod = source.fillMethod;
                fill.fillOrigin = source.fillOrigin;

                var fullWidth = GetRenderedWidth(fill, 1f);
                var renderedWidth = GetRenderedWidth(fill, fillAmount);
                Assert.That(fullWidth, Is.GreaterThan(183f));
                Assert.That(renderedWidth, Is.EqualTo(fullWidth * fillAmount).Within(0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fillObject);
            }
        }

        [Test]
        public void IcebreakingHudPrefab_ParentsArtToMatchingLogicalControls()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IcebreakingHudPrefabPath);

            Assert.That(prefab, Is.Not.Null);
            var hudRoot = prefab!.transform.Find("HudRoot");
            Assert.That(hudRoot, Is.Not.Null);
            Assert.That(hudRoot!.Find("ArtLayers"), Is.Null);
            AssertPanelArt(hudRoot.Find("FundsArea"), "ui_money", false);
            AssertPanelArt(hudRoot.Find("TimerArea"), "ui_time", false);
            Assert.That(hudRoot.Find("FundsArea/FundsText"), Is.Not.Null);
            Assert.That(hudRoot.Find("TimerArea/TimerText"), Is.Not.Null);

            var settings = hudRoot.Find("SettingsHitArea");
            var button = settings?.GetComponent<Button>();
            var hitImage = settings?.GetComponent<Image>();
            AssertPanelArt(settings, "ui_setting", true);
            Assert.That(button, Is.Not.Null);
            Assert.That(button!.targetGraphic, Is.SameAs(hitImage));
            AssertTextChild(settings!, "Label");
            Assert.That(settings!.Find("Visual"), Is.Null);

            var countdown = hudRoot.Find("CountdownText");
            Assert.That(countdown, Is.Not.Null);

            var presenter = prefab
                .GetComponents<MonoBehaviour>()
                .SingleOrDefault(component =>
                    component.GetType().FullName == "Icebreaker.UI.Hud.IcebreakingHudPresenter");
            Assert.That(presenter, Is.Not.Null);
            var serialized = new SerializedObject(presenter);
            Assert.That(serialized.FindProperty("panelGraphics")!.arraySize, Is.EqualTo(0));
            Assert.That(serialized.FindProperty("themedTexts")!.arraySize, Is.GreaterThan(0));
        }

        [Test]
        public void DestructionSheets_AreLeftToRightCenteredCells()
        {
            foreach (var sheet in Sheets)
            {
                var name = sheet.Name;
                var importer = LoadImporter(name);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), name);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple), name);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(256f), name);
                Assert.That(importer.mipmapEnabled, Is.False, name);
                Assert.That(importer.sRGBTexture, Is.True, name);
                Assert.That(importer.alphaIsTransparency, Is.True, name);

#pragma warning disable CS0618
                var sprites = importer.spritesheet.OrderBy(sprite => sprite.rect.x).ToArray();
#pragma warning restore CS0618
                Assert.That(sprites, Has.Length.EqualTo(sheet.FrameCount), name);
                for (var i = 0; i < sprites.Length; i++)
                {
                    Assert.That(sprites[i].rect, Is.EqualTo(new Rect(i * 256, 0, 256, 256)), name);
                    Assert.That(sprites[i].pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)), name);
                }
            }
        }

        private static TextureImporter LoadImporter(string name)
        {
            var path = $"{ArtFolder}/{name}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            return importer!;
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, throwOnError: false))
                .First(type => type != null)!;
        }

        private static float GetRenderedWidth(Image fill, float fillAmount)
        {
            fill.fillAmount = fillAmount;
            using var vertices = new VertexHelper();
            typeof(Image)
                .GetMethod(
                    "OnPopulateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: new[] { typeof(VertexHelper) },
                    modifiers: null)!
                .Invoke(fill, new object[] { vertices });
            if (vertices.currentVertCount == 0)
            {
                return 0f;
            }

            var stream = new List<UIVertex>();
            vertices.GetUIVertexStream(stream);
            return stream.Max(vertex => vertex.position.x) -
                   stream.Min(vertex => vertex.position.x);
        }

        private static void AssertFullScreenUiImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer!.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), path);
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f), path);
            Assert.That(importer.spritePivot, Is.EqualTo(new Vector2(0.5f, 0.5f)), path);
            Assert.That(importer.mipmapEnabled, Is.False, path);
            Assert.That(importer.sRGBTexture, Is.True, path);
            Assert.That(importer.alphaIsTransparency, Is.True, path);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear), path);
            Assert.That(
                importer.textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed),
                path);
        }

        private static void AssertCroppedUiImporter(
            string path,
            string spriteName,
            Rect expectedRect)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer!.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple), path);
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f), path);
            Assert.That(importer.mipmapEnabled, Is.False, path);
            Assert.That(importer.sRGBTexture, Is.True, path);
            Assert.That(importer.alphaIsTransparency, Is.True, path);
#pragma warning disable CS0618
            Assert.That(importer.spritesheet, Has.Length.EqualTo(1), path);
            Assert.That(importer.spritesheet[0].name, Is.EqualTo(spriteName), path);
            Assert.That(importer.spritesheet[0].rect, Is.EqualTo(expectedRect), path);
            Assert.That(
                importer.spritesheet[0].pivot,
                Is.EqualTo(new Vector2(0.5f, 0.5f)),
                path);
#pragma warning restore CS0618
        }

        private static void AssertPanelArt(
            Transform? panel,
            string spriteName,
            bool raycastTarget)
        {
            Assert.That(panel, Is.Not.Null, spriteName);
            var image = panel!.GetComponent<Image>();
            Assert.That(image, Is.Not.Null, spriteName);
            Assert.That(image!.sprite?.name, Is.EqualTo(spriteName), spriteName);
            Assert.That(image.color, Is.EqualTo(Color.white), spriteName);
            Assert.That(image.raycastTarget, Is.EqualTo(raycastTarget), spriteName);
            Assert.That(panel.Find("Art"), Is.Null, spriteName);
        }

        private static void AssertTextChild(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            Assert.That(child, Is.Not.Null, $"{parent.name}/{childName}");
            Assert.That(
                child!.GetComponents<Component>()
                    .Any(component => component.GetType().Name == "TextMeshProUGUI"),
                Is.True,
                $"{parent.name}/{childName}");
        }
    }
}
