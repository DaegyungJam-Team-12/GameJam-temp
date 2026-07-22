#nullable enable

using Icebreaker.Shared;

namespace Icebreaker.Shared.Combat
{
    public sealed class DirectAttackConfig
    {
        public DirectAttackConfig(
            float currentDirectDamage,
            float attackTicksPerSecond,
            float cursorRadiusReferencePixels,
            float criticalChance,
            float criticalDamageMultiplier)
        {
            CurrentDirectDamage = ContractGuards.Positive(currentDirectDamage, nameof(currentDirectDamage));
            AttackTicksPerSecond = ContractGuards.Positive(attackTicksPerSecond, nameof(attackTicksPerSecond));
            CursorRadiusReferencePixels = ContractGuards.Positive(
                cursorRadiusReferencePixels,
                nameof(cursorRadiusReferencePixels));
            CriticalChance = ContractGuards.Probability(criticalChance, nameof(criticalChance));
            CriticalDamageMultiplier = ContractGuards.Positive(
                criticalDamageMultiplier,
                nameof(criticalDamageMultiplier));
        }

        /// <summary>Base damage for each automatic cursor-area tick.</summary>
        public float CurrentDirectDamage { get; }

        /// <summary>Automatic area-attack ticks per second.</summary>
        public float AttackTicksPerSecond { get; }

        /// <summary>Radius in the 960 x 540 bottom-left-origin reference space.</summary>
        public float CursorRadiusReferencePixels { get; }

        // Kept as source-compatible aliases for callers not yet migrated from click-and-hold terminology.
        public float CurrentClickDamage => CurrentDirectDamage;

        public float HoldAttacksPerSecond => AttackTicksPerSecond;

        public float CriticalChance { get; }

        public float CriticalDamageMultiplier { get; }
    }
}
