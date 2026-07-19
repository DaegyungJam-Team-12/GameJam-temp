#nullable enable

using Icebreaker.Shared;

namespace Icebreaker.Shared.Combat
{
    public sealed class DirectAttackConfig
    {
        public DirectAttackConfig(
            float currentClickDamage,
            float holdAttacksPerSecond,
            float criticalChance,
            float criticalDamageMultiplier)
        {
            CurrentClickDamage = ContractGuards.Positive(currentClickDamage, nameof(currentClickDamage));
            HoldAttacksPerSecond = ContractGuards.Positive(holdAttacksPerSecond, nameof(holdAttacksPerSecond));
            CriticalChance = ContractGuards.Probability(criticalChance, nameof(criticalChance));
            CriticalDamageMultiplier = ContractGuards.Positive(
                criticalDamageMultiplier,
                nameof(criticalDamageMultiplier));
        }

        public float CurrentClickDamage { get; }

        public float HoldAttacksPerSecond { get; }

        public float CriticalChance { get; }

        public float CriticalDamageMultiplier { get; }
    }
}
