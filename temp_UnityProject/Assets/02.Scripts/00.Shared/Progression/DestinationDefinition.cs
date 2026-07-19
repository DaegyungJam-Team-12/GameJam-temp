#nullable enable

using Icebreaker.Shared;

namespace Icebreaker.Shared.Progression
{
    public sealed class DestinationDefinition
    {
        public DestinationDefinition(
            string id,
            string displayName,
            int targetProgress,
            string cargoName,
            int displayOrder)
        {
            Id = ContractGuards.Required(id, nameof(id));
            DisplayName = ContractGuards.Required(displayName, nameof(displayName));
            TargetProgress = ContractGuards.Positive(targetProgress, nameof(targetProgress));
            CargoName = ContractGuards.Required(cargoName, nameof(cargoName));
            DisplayOrder = ContractGuards.NonNegative(displayOrder, nameof(displayOrder));
        }

        public string Id { get; }

        public string DisplayName { get; }

        public int TargetProgress { get; }

        public string CargoName { get; }

        public int DisplayOrder { get; }
    }
}
