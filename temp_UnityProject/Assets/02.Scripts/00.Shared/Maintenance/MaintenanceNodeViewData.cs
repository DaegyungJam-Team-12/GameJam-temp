#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared;

namespace Icebreaker.Shared.Maintenance
{
    /// <summary>
    /// Fully calculated UI data. UI consumers display these values without recalculating purchase rules.
    /// </summary>
    public sealed class MaintenanceNodeViewData
    {
        public MaintenanceNodeViewData(
            string id,
            string displayName,
            MaintenanceBranch branch,
            int currentLevel,
            int maxLevel,
            MaintenanceNodeState state,
            string currentEffectText,
            string? nextEffectText,
            long? nextCost,
            bool isMaxLevel,
            bool canAffordNextLevel,
            bool canPurchaseNextLevel,
            IReadOnlyList<string> missingRequirementIds)
        {
            Id = ContractGuards.Required(id, nameof(id));
            DisplayName = ContractGuards.Required(displayName, nameof(displayName));
            Branch = branch;
            CurrentLevel = ContractGuards.NonNegative(currentLevel, nameof(currentLevel));
            MaxLevel = ContractGuards.Positive(maxLevel, nameof(maxLevel));
            if (CurrentLevel > MaxLevel)
            {
                throw new ArgumentException("Current level cannot exceed max level.", nameof(currentLevel));
            }

            State = state;
            CurrentEffectText = ContractGuards.Required(currentEffectText, nameof(currentEffectText));
            NextEffectText = nextEffectText;
            NextCost = nextCost;
            IsMaxLevel = isMaxLevel;
            CanAffordNextLevel = canAffordNextLevel;
            CanPurchaseNextLevel = canPurchaseNextLevel;
            MissingRequirementIds = ContractGuards.Copy(
                missingRequirementIds,
                nameof(missingRequirementIds));

            ValidateState();
        }

        public string Id { get; }

        public string DisplayName { get; }

        public MaintenanceBranch Branch { get; }

        public int CurrentLevel { get; }

        public int MaxLevel { get; }

        /// <summary>Locked means prerequisites are missing; affordability is represented separately.</summary>
        public MaintenanceNodeState State { get; }

        public string CurrentEffectText { get; }

        public string? NextEffectText { get; }

        public long? NextCost { get; }

        public bool IsMaxLevel { get; }

        public bool CanAffordNextLevel { get; }

        public bool CanPurchaseNextLevel { get; }

        public IReadOnlyList<string> MissingRequirementIds { get; }

        private void ValidateState()
        {
            if ((State == MaintenanceNodeState.Owned) != (CurrentLevel > 0))
            {
                throw new ArgumentException("Owned nodes must have at least one purchased level.", nameof(State));
            }

            if (IsMaxLevel != (CurrentLevel == MaxLevel))
            {
                throw new ArgumentException("IsMaxLevel must match current and max levels.", nameof(IsMaxLevel));
            }

            if (IsMaxLevel)
            {
                if (NextEffectText != null || NextCost != null || CanAffordNextLevel || CanPurchaseNextLevel)
                {
                    throw new ArgumentException("Max-level nodes cannot expose a next purchase.", nameof(IsMaxLevel));
                }
            }
            else
            {
                ContractGuards.Required(NextEffectText, nameof(NextEffectText));
                if (NextCost == null)
                {
                    throw new ArgumentException("Non-max nodes must expose their next cost.", nameof(NextCost));
                }

                ContractGuards.NonNegative(NextCost.Value, nameof(NextCost));
            }

            var missingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var missingId in MissingRequirementIds)
            {
                ContractGuards.Required(missingId, nameof(MissingRequirementIds));
                if (!missingIds.Add(missingId))
                {
                    throw new ArgumentException("Missing requirement IDs must be unique.", nameof(MissingRequirementIds));
                }
            }

            if ((State == MaintenanceNodeState.Locked) != (MissingRequirementIds.Count > 0))
            {
                throw new ArgumentException(
                    "Only locked nodes may contain missing requirement IDs.",
                    nameof(MissingRequirementIds));
            }

            var expectedCanPurchase =
                !IsMaxLevel &&
                State != MaintenanceNodeState.Locked &&
                CanAffordNextLevel;
            if (CanPurchaseNextLevel != expectedCanPurchase)
            {
                throw new ArgumentException(
                    "Purchase availability must match max level, prerequisites, and affordability.",
                    nameof(CanPurchaseNextLevel));
            }
        }
    }
}
