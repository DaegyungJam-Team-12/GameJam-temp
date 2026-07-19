#nullable enable

using Icebreaker.Shared;

namespace Icebreaker.Shared.State
{
    public readonly struct MaintenanceLevel
    {
        public MaintenanceLevel(string id, int level)
        {
            Id = ContractGuards.Required(id, nameof(id));
            Level = ContractGuards.NonNegative(level, nameof(level));
        }

        public string Id { get; }

        public int Level { get; }
    }
}
