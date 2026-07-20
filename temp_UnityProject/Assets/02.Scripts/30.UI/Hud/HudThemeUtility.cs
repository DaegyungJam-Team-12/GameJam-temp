#nullable enable

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Icebreaker.UI.Sandbox;

namespace Icebreaker.UI.Hud
{
    internal static class HudThemeUtility
    {
        public static void Apply(
            UiThemeAsset? theme,
            TMP_Text[] texts,
            Graphic[] panels,
            Graphic[] accents,
            UnityEngine.Object context)
        {
            if (theme == null)
            {
                Debug.LogWarning("[UI-02] UI theme is not assigned; prefab defaults are used.", context);
                return;
            }

            foreach (var panel in panels)
            {
                if (panel != null)
                {
                    panel.color = theme.Panel;
                }
            }

            foreach (var accent in accents)
            {
                if (accent != null)
                {
                    accent.color = theme.ActionAccent;
                }
            }

            var font = theme.CommonFont;
            if (font == null)
            {
                var settings = TMP_Settings.LoadDefaultSettings();
                font = settings != null ? TMP_Settings.defaultFontAsset : null;
            }

            foreach (var text in texts)
            {
                if (text == null)
                {
                    continue;
                }

                text.color = theme.PrimaryText;
                if (font != null)
                {
                    text.font = font;
                }
            }
        }
    }
}
