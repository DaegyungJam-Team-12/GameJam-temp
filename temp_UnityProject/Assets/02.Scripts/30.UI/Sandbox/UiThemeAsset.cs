#nullable enable

using TMPro;
using UnityEngine;

namespace Icebreaker.UI.Sandbox
{
    [CreateAssetMenu(fileName = "UiTheme", menuName = "ICEBREAKER/UI/Theme")]
    public sealed class UiThemeAsset : ScriptableObject
    {
        [SerializeField] private TMP_FontAsset? commonFont;
        [SerializeField] private Color background = new Color32(0x0B, 0x1F, 0x33, 0xFF);
        [SerializeField] private Color panel = new Color32(0x0D, 0x22, 0x35, 0xFF);
        [SerializeField] private Color primaryText = new Color32(0xDD, 0xF7, 0xFF, 0xFF);
        [SerializeField] private Color actionAccent = new Color32(0xF3, 0x9A, 0x3D, 0xFF);
        [SerializeField] private Color reward = new Color32(0xFF, 0xE0, 0xA0, 0xFF);
        [SerializeField] private Color success = new Color32(0x66, 0xD3, 0xBA, 0xFF);

        public TMP_FontAsset? CommonFont => commonFont;

        public Color Background => background;

        public Color Panel => panel;

        public Color PrimaryText => primaryText;

        public Color ActionAccent => actionAccent;

        public Color Reward => reward;

        public Color Success => success;
    }
}
