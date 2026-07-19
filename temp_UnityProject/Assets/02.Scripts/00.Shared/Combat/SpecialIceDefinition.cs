#nullable enable

using System;
using Icebreaker.Shared;

namespace Icebreaker.Shared.Combat
{
    public sealed class SpecialIceDefinition
    {
        public SpecialIceDefinition(
            SpecialIceType type,
            float spawnChance,
            IceTier minimumTier,
            float hpMultiplier,
            float fundsMultiplier)
        {
            if (type == SpecialIceType.None)
            {
                throw new ArgumentException("None is not a special ice definition.", nameof(type));
            }

            Type = type;
            SpawnChance = ContractGuards.Probability(spawnChance, nameof(spawnChance));
            MinimumTier = minimumTier;
            HpMultiplier = ContractGuards.Positive(hpMultiplier, nameof(hpMultiplier));
            FundsMultiplier = ContractGuards.Positive(fundsMultiplier, nameof(fundsMultiplier));
        }

        public SpecialIceType Type { get; }

        public float SpawnChance { get; }

        public IceTier MinimumTier { get; }

        public float HpMultiplier { get; }

        public float FundsMultiplier { get; }
    }
}
