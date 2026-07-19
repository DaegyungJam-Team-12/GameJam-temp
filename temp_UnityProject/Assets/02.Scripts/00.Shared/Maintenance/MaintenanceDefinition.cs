#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared;

namespace Icebreaker.Shared.Maintenance
{
    public sealed class MaintenanceDefinition
    {
        public MaintenanceDefinition(
            string id,
            string displayName,
            MaintenanceBranch branch,
            int maxLevel,
            IReadOnlyList<long> costsByLevel,
            IReadOnlyList<string> effectTextsByLevel,
            IReadOnlyList<MaintenanceRequirement> requirements)
        {
            Id = ContractGuards.Required(id, nameof(id));
            DisplayName = ContractGuards.Required(displayName, nameof(displayName));
            Branch = branch;
            MaxLevel = ContractGuards.Positive(maxLevel, nameof(maxLevel));
            CostsByLevel = ContractGuards.Copy(costsByLevel, nameof(costsByLevel));
            EffectTextsByLevel = ContractGuards.Copy(effectTextsByLevel, nameof(effectTextsByLevel));
            Requirements = ContractGuards.Copy(requirements, nameof(requirements));

            ValidateLevelData();
            ValidateRequirements();
        }

        public string Id { get; }

        public string DisplayName { get; }

        public MaintenanceBranch Branch { get; }

        public int MaxLevel { get; }

        public IReadOnlyList<long> CostsByLevel { get; }

        public IReadOnlyList<string> EffectTextsByLevel { get; }

        public IReadOnlyList<MaintenanceRequirement> Requirements { get; }

        private void ValidateLevelData()
        {
            if (CostsByLevel.Count != MaxLevel)
            {
                throw new ArgumentException(
                    "Costs must contain exactly one entry per level.",
                    nameof(CostsByLevel));
            }

            if (EffectTextsByLevel.Count != MaxLevel)
            {
                throw new ArgumentException(
                    "Effect texts must contain exactly one entry per level.",
                    nameof(EffectTextsByLevel));
            }

            for (var index = 0; index < MaxLevel; index++)
            {
                ContractGuards.NonNegative(CostsByLevel[index], nameof(CostsByLevel));
                ContractGuards.Required(EffectTextsByLevel[index], nameof(EffectTextsByLevel));
            }
        }

        private void ValidateRequirements()
        {
            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var requirement in Requirements)
            {
                if (string.Equals(requirement.NodeId, Id, StringComparison.Ordinal))
                {
                    throw new ArgumentException("A node cannot require itself.", nameof(Requirements));
                }

                if (!nodeIds.Add(requirement.NodeId))
                {
                    throw new ArgumentException("Requirement node IDs must be unique.", nameof(Requirements));
                }
            }
        }
    }
}
