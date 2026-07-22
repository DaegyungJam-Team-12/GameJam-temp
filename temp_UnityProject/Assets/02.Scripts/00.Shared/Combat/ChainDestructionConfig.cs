#nullable enable

namespace Icebreaker.Shared.Combat
{
    public sealed class ChainDestructionConfig
    {
        // D03 과잉 파쇄
        public bool EnableOverkill { get; }
        public float OverkillTransferRatio { get; }

        // H01 파편 비산
        public bool EnableHullFragment { get; }
        public float HullFragmentDamageMultiplier { get; }
        public float HullFragmentRadius { get; }

        // H02 특수빙 증폭
        public int CrystalShardCount { get; }
        public float CrackDamageMultiplier { get; }
        public float CrackRadius { get; }

        // H03 빙판 붕괴
        public bool EnableIceCollapse { get; }
        public float IceCollapseDamageMultiplier { get; }
        public float IceCollapseRadius { get; }

        public ChainDestructionConfig(
            bool enableOverkill,
            float overkillTransferRatio,
            bool enableHullFragment,
            float hullFragmentDamageMultiplier,
            float hullFragmentRadius,
            int crystalShardCount,
            float crackDamageMultiplier,
            float crackRadius,
            bool enableIceCollapse,
            float iceCollapseDamageMultiplier,
            float iceCollapseRadius)
        {
            EnableOverkill = enableOverkill;
            OverkillTransferRatio = overkillTransferRatio;
            EnableHullFragment = enableHullFragment;
            HullFragmentDamageMultiplier = hullFragmentDamageMultiplier;
            HullFragmentRadius = hullFragmentRadius;
            CrystalShardCount = crystalShardCount;
            CrackDamageMultiplier = crackDamageMultiplier;
            CrackRadius = crackRadius;
            EnableIceCollapse = enableIceCollapse;
            IceCollapseDamageMultiplier = iceCollapseDamageMultiplier;
            IceCollapseRadius = iceCollapseRadius;
        }
    }
}
