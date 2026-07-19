#nullable enable

using Icebreaker.Shared;

namespace Icebreaker.Shared.Combat
{
    public sealed class IceDefinition
    {
        public IceDefinition(IceTier tier, string displayName, float maxHp, long baseFunds)
        {
            Tier = tier;
            DisplayName = ContractGuards.Required(displayName, nameof(displayName));
            MaxHp = ContractGuards.Positive(maxHp, nameof(maxHp));
            BaseFunds = ContractGuards.NonNegative(baseFunds, nameof(baseFunds));
        }

        public IceTier Tier { get; }

        public string DisplayName { get; }

        public float MaxHp { get; }

        public long BaseFunds { get; }
    }
}
