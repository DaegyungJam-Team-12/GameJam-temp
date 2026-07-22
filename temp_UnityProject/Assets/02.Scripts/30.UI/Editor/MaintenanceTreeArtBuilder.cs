#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Icebreaker.UI.Editor
{
    public static class MaintenanceTreeArtBuilder
    {
        public const string ArtFolder = "Assets/04.Images/30.UI/Maintenance";
        public const string IconFolder = ArtFolder + "/Icons";
        public const string ChromeFolder = ArtFolder + "/Chrome";

        private static readonly string[] LogicalIds =
        {
            "C01", "C02", "C03", "C04", "D01", "D02", "D03", "D04",
            "S01", "S02", "S03", "H01", "H02", "H03"
        };

        [MenuItem("ICEBREAKER/UI/Rebuild Maintenance Tree Art")]
        public static void Build()
        {
            EnsureAssetFolder(ChromeFolder);

            Write("RootFrame", 128, texture =>
            {
                FillCircle(texture, 64, 64, 57, new Color32(13, 34, 53, 235));
                DrawCircleRing(texture, 64, 64, 57, 5, new Color32(221, 247, 255, 255));
                DrawCircleRing(texture, 64, 64, 48, 2, new Color32(88, 201, 242, 210));
            });
            Write("NodeFrame", 128, texture =>
            {
                FillRoundedRect(texture, 10, 10, 108, 108, 18, new Color32(13, 34, 53, 235));
                DrawRoundedRect(texture, 10, 10, 108, 108, 18, 4, new Color32(168, 221, 234, 255));
            });
            Write("StatePurchased", 128, texture =>
                DrawCircleRing(texture, 64, 64, 58, 7, new Color32(102, 211, 186, 255)));
            Write("StateAvailable", 128, texture =>
            {
                DrawCircleRing(texture, 64, 64, 58, 6, new Color32(243, 154, 61, 255));
                DrawCircleRing(texture, 64, 64, 49, 2, new Color32(255, 224, 160, 230));
            });
            Write("StateLocked", 128, texture =>
            {
                DrawCorner(texture, 12, 12, 22, new Color32(117, 148, 166, 230));
                DrawCorner(texture, 116, 12, -22, new Color32(117, 148, 166, 230));
                DrawCorner(texture, 12, 116, 22, new Color32(117, 148, 166, 230), true);
                DrawCorner(texture, 116, 116, -22, new Color32(117, 148, 166, 230), true);
            });
            Write("StatePreview", 128, texture =>
            {
                for (var angle = 0; angle < 360; angle += 30)
                {
                    DrawArc(texture, 64, 64, 57, angle, angle + 17, 5, new Color32(88, 201, 242, 190));
                }
            });
            Write("SelectionOutline", 128, texture =>
            {
                DrawCircleRing(texture, 64, 64, 62, 3, new Color32(255, 224, 160, 255));
                DrawCircleRing(texture, 64, 64, 55, 2, new Color32(243, 154, 61, 255));
            });
            Write("Check", 64, texture =>
            {
                DrawLine(texture, 12, 33, 26, 47, 7, new Color32(102, 211, 186, 255));
                DrawLine(texture, 26, 47, 53, 17, 7, new Color32(221, 247, 255, 255));
            });
            Write("Lock", 64, texture =>
            {
                DrawArc(texture, 32, 27, 15, 180, 360, 6, new Color32(168, 221, 234, 255));
                FillRoundedRect(texture, 13, 27, 38, 28, 7, new Color32(39, 136, 188, 255));
                FillCircle(texture, 32, 40, 4, new Color32(221, 247, 255, 255));
            });
            Write("EdgeDefault", 32, texture =>
            {
                FillRoundedRect(texture, 0, 11, 32, 10, 5, new Color32(88, 201, 242, 100));
                FillRoundedRect(texture, 0, 14, 32, 4, 2, new Color32(168, 221, 234, 170));
            });
            Write("EdgeLit", 32, texture =>
            {
                FillRoundedRect(texture, 0, 9, 32, 14, 7, new Color32(243, 154, 61, 90));
                FillRoundedRect(texture, 0, 13, 32, 6, 3, new Color32(255, 224, 160, 255));
            });
            Write("TooltipPanel", 64, texture =>
            {
                FillRoundedRect(texture, 1, 1, 62, 62, 8, new Color32(13, 34, 53, 250));
                DrawRoundedRect(texture, 1, 1, 62, 62, 8, 2, new Color32(88, 201, 242, 210));
            });
            Write("BottomBar", 64, texture =>
            {
                FillRoundedRect(texture, 0, 0, 64, 64, 6, new Color32(13, 34, 53, 248));
                FillRoundedRect(texture, 0, 0, 64, 3, 1, new Color32(88, 201, 242, 220));
            });
            Write("ControlWasd", 64, DrawWasd);
            Write("ControlDrag", 64, DrawDrag);
            Write("ControlWheel", 64, DrawWheel);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var id in LogicalIds)
            {
                ConfigureSprite($"{IconFolder}/{id}.png", Vector4.zero);
            }

            foreach (var name in ChromeNames())
            {
                var border = name is "TooltipPanel" or "BottomBar"
                    ? new Vector4(12f, 12f, 12f, 12f)
                    : Vector4.zero;
                ConfigureSprite($"{ChromeFolder}/{name}.png", border);
            }

            AssetDatabase.SaveAssets();
        }

        public static Sprite LoadChrome(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{ChromeFolder}/{name}.png");

        private static IEnumerable<string> ChromeNames()
        {
            yield return "RootFrame";
            yield return "NodeFrame";
            yield return "StatePurchased";
            yield return "StateAvailable";
            yield return "StateLocked";
            yield return "StatePreview";
            yield return "SelectionOutline";
            yield return "Check";
            yield return "Lock";
            yield return "EdgeDefault";
            yield return "EdgeLit";
            yield return "TooltipPanel";
            yield return "BottomBar";
            yield return "ControlWasd";
            yield return "ControlDrag";
            yield return "ControlWheel";
        }

        private static void Write(string name, int size, Action<Texture2D> draw)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(new Color32[size * size]);
                draw(texture);
                texture.Apply(false, false);
                File.WriteAllBytes($"{ChromeFolder}/{name}.png", texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void ConfigureSprite(string path, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Maintenance tree sprite is missing: {path}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static void DrawWasd(Texture2D texture)
        {
            DrawKey(texture, 22, 34);
            DrawKey(texture, 6, 17);
            DrawKey(texture, 22, 17);
            DrawKey(texture, 38, 17);
            DrawLine(texture, 30, 39, 30, 48, 3, new Color32(221, 247, 255, 255));
            DrawLine(texture, 30, 48, 26, 44, 3, new Color32(221, 247, 255, 255));
            DrawLine(texture, 30, 48, 34, 44, 3, new Color32(221, 247, 255, 255));
        }

        private static void DrawKey(Texture2D texture, int x, int y)
        {
            FillRoundedRect(texture, x, y, 14, 14, 3, new Color32(13, 34, 53, 255));
            DrawRoundedRect(texture, x, y, 14, 14, 3, 2, new Color32(168, 221, 234, 255));
        }

        private static void DrawDrag(Texture2D texture)
        {
            DrawCircleRing(texture, 26, 33, 14, 3, new Color32(168, 221, 234, 255));
            FillRoundedRect(texture, 24, 19, 4, 14, 2, new Color32(243, 154, 61, 255));
            DrawLine(texture, 39, 33, 56, 33, 3, new Color32(221, 247, 255, 255));
            DrawLine(texture, 56, 33, 50, 27, 3, new Color32(221, 247, 255, 255));
            DrawLine(texture, 56, 33, 50, 39, 3, new Color32(221, 247, 255, 255));
        }

        private static void DrawWheel(Texture2D texture)
        {
            FillRoundedRect(texture, 18, 5, 28, 54, 14, new Color32(13, 34, 53, 255));
            DrawRoundedRect(texture, 18, 5, 28, 54, 14, 3, new Color32(168, 221, 234, 255));
            FillRoundedRect(texture, 29, 12, 6, 16, 3, new Color32(243, 154, 61, 255));
        }

        private static void DrawCorner(Texture2D texture, int x, int y, int length, Color32 color, bool upward = false)
        {
            DrawLine(texture, x, y, x + length, y, 4, color);
            DrawLine(texture, x, y, x, y + (upward ? -Math.Abs(length) : Math.Abs(length)), 4, color);
        }

        private static void DrawArc(Texture2D texture, int cx, int cy, int radius, float start, float end, int thickness, Color32 color)
        {
            for (var angle = start; angle <= end; angle += 1f)
            {
                var radians = angle * Mathf.Deg2Rad;
                FillCircle(texture,
                    Mathf.RoundToInt(cx + Mathf.Cos(radians) * radius),
                    Mathf.RoundToInt(cy + Mathf.Sin(radians) * radius),
                    thickness / 2,
                    color);
            }
        }

        private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, int thickness, Color32 color)
        {
            var steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (var index = 0; index <= steps; index++)
            {
                var t = steps == 0 ? 0f : (float)index / steps;
                FillCircle(texture, Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), thickness / 2, color);
            }
        }

        private static void DrawCircleRing(Texture2D texture, int cx, int cy, int radius, int thickness, Color32 color)
        {
            var inner = (radius - thickness) * (radius - thickness);
            var outer = radius * radius;
            for (var y = cy - radius; y <= cy + radius; y++)
            {
                for (var x = cx - radius; x <= cx + radius; x++)
                {
                    var distance = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    if (distance >= inner && distance <= outer)
                    {
                        SetPixel(texture, x, y, color);
                    }
                }
            }
        }

        private static void FillCircle(Texture2D texture, int cx, int cy, int radius, Color32 color)
        {
            var squared = radius * radius;
            for (var y = cy - radius; y <= cy + radius; y++)
            {
                for (var x = cx - radius; x <= cx + radius; x++)
                {
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= squared)
                    {
                        SetPixel(texture, x, y, color);
                    }
                }
            }
        }

        private static void FillRoundedRect(Texture2D texture, int x, int y, int width, int height, int radius, Color32 color)
        {
            for (var py = y; py < y + height; py++)
            {
                for (var px = x; px < x + width; px++)
                {
                    if (InsideRoundedRect(px, py, x, y, width, height, radius))
                    {
                        SetPixel(texture, px, py, color);
                    }
                }
            }
        }

        private static void DrawRoundedRect(Texture2D texture, int x, int y, int width, int height, int radius, int thickness, Color32 color)
        {
            for (var py = y; py < y + height; py++)
            {
                for (var px = x; px < x + width; px++)
                {
                    if (InsideRoundedRect(px, py, x, y, width, height, radius) &&
                        !InsideRoundedRect(px, py, x + thickness, y + thickness, width - thickness * 2, height - thickness * 2, Mathf.Max(0, radius - thickness)))
                    {
                        SetPixel(texture, px, py, color);
                    }
                }
            }
        }

        private static bool InsideRoundedRect(int px, int py, int x, int y, int width, int height, int radius)
        {
            var nearestX = Mathf.Clamp(px, x + radius, x + width - radius - 1);
            var nearestY = Mathf.Clamp(py, y + radius, y + height - radius - 1);
            var dx = px - nearestX;
            var dy = py - nearestY;
            return dx * dx + dy * dy <= radius * radius;
        }

        private static void SetPixel(Texture2D texture, int x, int y, Color32 color)
        {
            if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
            {
                texture.SetPixel(x, y, color);
            }
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            var segments = assetPath.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }
    }
}
