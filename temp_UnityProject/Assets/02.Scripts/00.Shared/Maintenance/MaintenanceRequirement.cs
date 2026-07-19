#nullable enable

using Icebreaker.Shared;

namespace Icebreaker.Shared.Maintenance
{
    public readonly struct MaintenanceRequirement
    {
        public MaintenanceRequirement(string nodeId, int requiredLevel)
        {
            NodeId = ContractGuards.Required(nodeId, nameof(nodeId));
            RequiredLevel = ContractGuards.Positive(requiredLevel, nameof(requiredLevel));
        }

        public string NodeId { get; }

        public int RequiredLevel { get; }
    }
}
