#nullable enable

using Icebreaker.Shared;

namespace Icebreaker.Shared.Combat
{
    public sealed class SupportAttackConfig
    {
        public SupportAttackConfig(
            bool enabled,
            int requiredDirectHitCount,
            float primaryDamageMultiplier,
            int additionalTargetCount,
            float additionalDamageMultiplier,
            bool prioritizeSpecialIce,
            float specialIceDamageMultiplier)
        {
            Enabled = enabled;
            RequiredDirectHitCount = ContractGuards.Positive(
                requiredDirectHitCount,
                nameof(requiredDirectHitCount));
            PrimaryDamageMultiplier = ContractGuards.NonNegative(
                primaryDamageMultiplier,
                nameof(primaryDamageMultiplier));
            AdditionalTargetCount = ContractGuards.NonNegative(
                additionalTargetCount,
                nameof(additionalTargetCount));
            AdditionalDamageMultiplier = ContractGuards.NonNegative(
                additionalDamageMultiplier,
                nameof(additionalDamageMultiplier));
            PrioritizeSpecialIce = prioritizeSpecialIce;
            SpecialIceDamageMultiplier = ContractGuards.NonNegative(
                specialIceDamageMultiplier,
                nameof(specialIceDamageMultiplier));
        }

        public bool Enabled { get; }

        public int RequiredDirectHitCount { get; }

        public float PrimaryDamageMultiplier { get; }

        public int AdditionalTargetCount { get; }

        public float AdditionalDamageMultiplier { get; }

        public bool PrioritizeSpecialIce { get; }

        public float SpecialIceDamageMultiplier { get; }
    }
}
