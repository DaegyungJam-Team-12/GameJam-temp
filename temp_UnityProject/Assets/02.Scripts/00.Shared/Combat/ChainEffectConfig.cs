#nullable enable

using Icebreaker.Shared;

namespace Icebreaker.Shared.Combat
{
    public sealed class ChainEffectConfig
    {
        public ChainEffectConfig(
            bool overkillEnabled,
            float overkillTransferMultiplier,
            float hullFragmentDamageMultiplier,
            float hullFragmentRadiusReferencePixels,
            int crystalShardCount,
            float crackDamageMultiplier,
            float crackRadiusReferencePixels,
            bool iceCollapseEnabled,
            int iceCollapseRequiredDestroyCount,
            float iceCollapseDamageMultiplier,
            float iceCollapseRadiusReferencePixels,
            int maxChainDepth)
        {
            OverkillEnabled = overkillEnabled;
            OverkillTransferMultiplier = ContractGuards.NonNegative(
                overkillTransferMultiplier,
                nameof(overkillTransferMultiplier));
            HullFragmentDamageMultiplier = ContractGuards.NonNegative(
                hullFragmentDamageMultiplier,
                nameof(hullFragmentDamageMultiplier));
            HullFragmentRadiusReferencePixels = ContractGuards.NonNegative(
                hullFragmentRadiusReferencePixels,
                nameof(hullFragmentRadiusReferencePixels));
            CrystalShardCount = ContractGuards.NonNegative(crystalShardCount, nameof(crystalShardCount));
            CrackDamageMultiplier = ContractGuards.NonNegative(
                crackDamageMultiplier,
                nameof(crackDamageMultiplier));
            CrackRadiusReferencePixels = ContractGuards.NonNegative(
                crackRadiusReferencePixels,
                nameof(crackRadiusReferencePixels));
            IceCollapseEnabled = iceCollapseEnabled;
            IceCollapseRequiredDestroyCount = ContractGuards.Positive(
                iceCollapseRequiredDestroyCount,
                nameof(iceCollapseRequiredDestroyCount));
            IceCollapseDamageMultiplier = ContractGuards.NonNegative(
                iceCollapseDamageMultiplier,
                nameof(iceCollapseDamageMultiplier));
            IceCollapseRadiusReferencePixels = ContractGuards.NonNegative(
                iceCollapseRadiusReferencePixels,
                nameof(iceCollapseRadiusReferencePixels));
            MaxChainDepth = ContractGuards.NonNegative(maxChainDepth, nameof(maxChainDepth));
        }

        public bool OverkillEnabled { get; }

        public float OverkillTransferMultiplier { get; }

        public float HullFragmentDamageMultiplier { get; }

        /// <summary>Radius in the 960 x 540 bottom-left-origin reference space.</summary>
        public float HullFragmentRadiusReferencePixels { get; }

        public int CrystalShardCount { get; }

        public float CrackDamageMultiplier { get; }

        /// <summary>Radius in the 960 x 540 bottom-left-origin reference space.</summary>
        public float CrackRadiusReferencePixels { get; }

        public bool IceCollapseEnabled { get; }

        public int IceCollapseRequiredDestroyCount { get; }

        public float IceCollapseDamageMultiplier { get; }

        /// <summary>Radius in the 960 x 540 bottom-left-origin reference space.</summary>
        public float IceCollapseRadiusReferencePixels { get; }

        public int MaxChainDepth { get; }
    }
}
