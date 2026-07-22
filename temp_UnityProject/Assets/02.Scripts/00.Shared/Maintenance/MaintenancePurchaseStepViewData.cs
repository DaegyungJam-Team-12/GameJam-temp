#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared;

namespace Icebreaker.Shared.Maintenance
{
    public enum MaintenanceStepPurchaseState
    {
        Purchased,
        Available,
        Locked
    }

    public enum MaintenanceStepVisibility
    {
        Visible,
        Preview,
        Hidden
    }

    public static class MaintenanceStepId
    {
        public static string Create(string maintenanceId, int targetLevel)
        {
            return $"{ContractGuards.Required(maintenanceId, nameof(maintenanceId))}-L" +
                   ContractGuards.Positive(targetLevel, nameof(targetLevel));
        }
    }

    /// <summary>
    /// Fully calculated data for one visual purchase step. UI consumers do not recalculate rules.
    /// </summary>
    public sealed class MaintenancePurchaseStepViewData
    {
        public MaintenancePurchaseStepViewData(
            string stepId,
            string maintenanceId,
            string displayName,
            MaintenanceBranch branch,
            int targetLevel,
            int maxLevel,
            MaintenanceStepPurchaseState purchaseState,
            MaintenanceStepVisibility visibility,
            long cost,
            string effectText,
            bool canAfford,
            bool canPurchase,
            IReadOnlyList<string> missingRequirementIds)
        {
            StepId = ContractGuards.Required(stepId, nameof(stepId));
            MaintenanceId = ContractGuards.Required(maintenanceId, nameof(maintenanceId));
            DisplayName = ContractGuards.Required(displayName, nameof(displayName));
            Branch = branch;
            TargetLevel = ContractGuards.Positive(targetLevel, nameof(targetLevel));
            MaxLevel = ContractGuards.Positive(maxLevel, nameof(maxLevel));
            if (TargetLevel > MaxLevel)
            {
                throw new ArgumentException("Target level cannot exceed max level.", nameof(targetLevel));
            }

            PurchaseState = purchaseState;
            Visibility = visibility;
            Cost = ContractGuards.NonNegative(cost, nameof(cost));
            EffectText = ContractGuards.Required(effectText, nameof(effectText));
            CanAfford = canAfford;
            CanPurchase = canPurchase;
            MissingRequirementIds = ContractGuards.Copy(
                missingRequirementIds,
                nameof(missingRequirementIds));

            ValidateState();
        }

        public string StepId { get; }

        public string MaintenanceId { get; }

        public string DisplayName { get; }

        public MaintenanceBranch Branch { get; }

        public int TargetLevel { get; }

        public int MaxLevel { get; }

        public MaintenanceStepPurchaseState PurchaseState { get; }

        public MaintenanceStepVisibility Visibility { get; }

        public long Cost { get; }

        public string EffectText { get; }

        public bool CanAfford { get; }

        public bool CanPurchase { get; }

        public IReadOnlyList<string> MissingRequirementIds { get; }

        private void ValidateState()
        {
            if (!string.Equals(
                    StepId,
                    MaintenanceStepId.Create(MaintenanceId, TargetLevel),
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Step ID must match maintenance ID and target level.",
                    nameof(StepId));
            }

            var missingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var missingId in MissingRequirementIds)
            {
                ContractGuards.Required(missingId, nameof(MissingRequirementIds));
                if (!missingIds.Add(missingId))
                {
                    throw new ArgumentException(
                        "Missing requirement IDs must be unique.",
                        nameof(MissingRequirementIds));
                }
            }

            if (PurchaseState != MaintenanceStepPurchaseState.Locked &&
                MissingRequirementIds.Count > 0)
            {
                throw new ArgumentException(
                    "Only locked steps can have missing requirements.",
                    nameof(MissingRequirementIds));
            }

            var expectedCanPurchase =
                PurchaseState == MaintenanceStepPurchaseState.Available &&
                Visibility == MaintenanceStepVisibility.Visible &&
                CanAfford;
            if (CanPurchase != expectedCanPurchase)
            {
                throw new ArgumentException(
                    "Purchase availability must match state, visibility, and funds.",
                    nameof(CanPurchase));
            }
        }
    }
}
