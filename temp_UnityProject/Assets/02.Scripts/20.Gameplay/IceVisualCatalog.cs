#nullable enable

using Icebreaker.Shared.Combat;
using UnityEngine;

namespace Icebreaker.Gameplay
{
    /// <summary>
    /// Artist-authored ice sprites used by <see cref="IceFieldView"/>.
    /// Static sprites render live ice while the matching six-cell sheets render destruction.
    /// </summary>
    [CreateAssetMenu(fileName = "IceVisualCatalog", menuName = "Icebreaker/Gameplay/Ice Visual Catalog")]
    public sealed class IceVisualCatalog : ScriptableObject
    {
        [Header("T1")]
        [SerializeField] private Sprite? t1Variant01;
        [SerializeField] private Sprite? t1Variant02;
        [SerializeField] private Texture2D? t1Variant01Sheet;
        [SerializeField] private Texture2D? t1Variant02Sheet;

        [Header("T2")]
        [SerializeField] private Sprite? t2Variant01;
        [SerializeField] private Sprite? t2Variant02;
        [SerializeField] private Texture2D? t2Variant01Sheet;
        [SerializeField] private Texture2D? t2Variant02Sheet;

        [Header("T3")]
        [SerializeField] private Sprite? t3Variant01;
        [SerializeField] private Sprite? t3Variant02;
        [SerializeField] private Texture2D? t3Variant01Sheet;
        [SerializeField] private Texture2D? t3Variant02Sheet;

        [Header("Special Ice Overlays")]
        [Tooltip("Overlay drawn above the tier-specific base ice for crystal ice.")]
        [SerializeField] private Sprite? crystal;
        [SerializeField] private Texture2D? crystalSheet;

        [Tooltip("Overlay drawn above the tier-specific base ice for cracked ice.")]
        [SerializeField] private Sprite? crack;
        [SerializeField] private Texture2D? crackSheet;

        public bool IsComplete =>
            t1Variant01 != null && t1Variant02 != null &&
            t1Variant01Sheet != null && t1Variant02Sheet != null &&
            t2Variant01 != null && t2Variant02 != null &&
            t2Variant01Sheet != null && t2Variant02Sheet != null &&
            t3Variant01 != null && t3Variant02 != null &&
            t3Variant01Sheet != null && t3Variant02Sheet != null &&
            crystal != null && crystalSheet != null &&
            crack != null && crackSheet != null;

        public Sprite? ResolveStaticSprite(IceTier tier, SpecialIceType specialType, long iceInstanceId)
        {
            // Compatibility path for IceFieldView. Runtime ownership must migrate to
            // ResolveSpecialOverlaySprite before this fallback can be removed.
            if (specialType == SpecialIceType.Crystal && crystal != null)
            {
                return crystal;
            }

            if (specialType == SpecialIceType.Crack && crack != null)
            {
                return crack;
            }

            var useSecondVariant = (iceInstanceId & 1L) != 0L;
            return tier switch
            {
                IceTier.T2 => useSecondVariant ? t2Variant02 : t2Variant01,
                IceTier.T3 => useSecondVariant ? t3Variant02 : t3Variant01,
                _ => useSecondVariant ? t1Variant02 : t1Variant01,
            };
        }

        public Texture2D? ResolveDestructionSheet(
            IceTier tier,
            SpecialIceType specialType,
            long iceInstanceId)
        {
            // Compatibility path for IceFieldView; see ResolveStaticSprite.
            if (specialType == SpecialIceType.Crystal && crystalSheet != null)
            {
                return crystalSheet;
            }

            if (specialType == SpecialIceType.Crack && crackSheet != null)
            {
                return crackSheet;
            }

            var useSecondVariant = (iceInstanceId & 1L) != 0L;
            return tier switch
            {
                IceTier.T2 => useSecondVariant ? t2Variant02Sheet : t2Variant01Sheet,
                IceTier.T3 => useSecondVariant ? t3Variant02Sheet : t3Variant01Sheet,
                _ => useSecondVariant ? t1Variant02Sheet : t1Variant01Sheet,
            };
        }

        public Sprite? ResolveSpecialOverlaySprite(SpecialIceType specialType)
        {
            return specialType switch
            {
                SpecialIceType.Crystal => crystal,
                SpecialIceType.Crack => crack,
                _ => null,
            };
        }

        public Texture2D? ResolveSpecialOverlaySheet(SpecialIceType specialType)
        {
            return specialType switch
            {
                SpecialIceType.Crystal => crystalSheet,
                SpecialIceType.Crack => crackSheet,
                _ => null,
            };
        }
    }
}
