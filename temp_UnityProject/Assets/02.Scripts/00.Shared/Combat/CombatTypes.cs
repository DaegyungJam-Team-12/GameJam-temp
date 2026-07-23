#nullable enable

using Icebreaker.Shared;

namespace Icebreaker.Shared.Combat
{
    public enum IceTier
    {
        T1,
        T2,
        T3,
        T4,
        T5
    }

    public enum SpecialIceType
    {
        None,
        Crystal,
        Crack
    }

    public readonly struct IceSpawnWeight
    {
        public IceSpawnWeight(IceTier tier, int weight)
        {
            Tier = tier;
            Weight = ContractGuards.NonNegative(weight, nameof(weight));
        }

        public IceTier Tier { get; }

        public int Weight { get; }
    }
}
