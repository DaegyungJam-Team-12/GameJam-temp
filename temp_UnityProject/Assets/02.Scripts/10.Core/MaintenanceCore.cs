#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Maintenance;
using Icebreaker.Shared.State;

namespace Icebreaker.Core
{
    public sealed class MaintenanceCore
    {
        private const string NotOwnedEffectText = "미보유";

        private readonly IReadOnlyList<MaintenanceDefinition> definitions;
        private readonly Dictionary<string, MaintenanceDefinition> definitionsById;
        private readonly Dictionary<string, int> levelsById;
        private readonly ProgressionLedger ledger;
        private readonly SaveService saveService;

        public MaintenanceCore(
            IReadOnlyList<MaintenanceDefinition> definitions,
            ProgressionLedger ledger,
            SaveService saveService)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            if (saveService == null)
            {
                throw new ArgumentNullException(nameof(saveService));
            }

            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            if (saveService.Data.funds < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(saveService),
                    saveService.Data.funds,
                    "Saved funds cannot be negative.");
            }

            this.definitions = definitions;
            this.ledger = ledger;
            this.saveService = saveService;
            definitionsById = new Dictionary<string, MaintenanceDefinition>(StringComparer.Ordinal);
            levelsById = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException("Definitions cannot contain null.", nameof(definitions));
                }

                if (!definitionsById.TryAdd(definition.Id, definition))
                {
                    throw new ArgumentException("Definition IDs must be unique.", nameof(definitions));
                }

                levelsById.Add(definition.Id, 0);
            }

            var loadedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var savedLevel in saveService.Data.maintenanceLevels)
            {
                if (string.IsNullOrEmpty(savedLevel.id) ||
                    !definitionsById.TryGetValue(savedLevel.id, out var definition))
                {
                    continue;
                }

                if (!loadedIds.Add(savedLevel.id))
                {
                    throw new ArgumentException(
                        "Saved maintenance level IDs must be unique.",
                        nameof(saveService));
                }

                if (savedLevel.level < 0 || savedLevel.level > definition.MaxLevel)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(saveService),
                        savedLevel.level,
                        "Saved maintenance level must be within the node range.");
                }

                levelsById[savedLevel.id] = savedLevel.level;
            }

            if (ledger.Funds != saveService.Data.funds)
            {
                throw new ArgumentException(
                    "Ledger and saved funds must match when maintenance is initialized.",
                    nameof(ledger));
            }
        }

        public long Funds => ledger.Funds;

        public int MaintenanceEfficiencyLevel => levelsById[MaintenanceCatalog.C02];

        public IReadOnlyList<MaintenanceLevel> MaintenanceLevels
        {
            get
            {
                var levels = new MaintenanceLevel[definitions.Count];
                for (var index = 0; index < definitions.Count; index++)
                {
                    var id = definitions[index].Id;
                    levels[index] = new MaintenanceLevel(id, levelsById[id]);
                }

                return Array.AsReadOnly(levels);
            }
        }

        public bool TryPurchase(string nodeId)
        {
            return TryPurchaseDetailed(nodeId) == MaintenancePurchaseResult.Success;
        }

        public MaintenancePurchaseResult TryPurchaseDetailed(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) ||
                !definitionsById.TryGetValue(nodeId, out var definition))
            {
                return MaintenancePurchaseResult.InvalidNode;
            }

            var currentLevel = levelsById[nodeId];
            if (currentLevel == definition.MaxLevel)
            {
                return MaintenancePurchaseResult.MaxLevel;
            }

            if (currentLevel == 0 && !RequirementsMet(definition))
            {
                return MaintenancePurchaseResult.Locked;
            }

            var cost = definition.CostsByLevel[currentLevel];
            if (!ledger.TrySpendFunds(cost))
            {
                return MaintenancePurchaseResult.InsufficientFunds;
            }

            levelsById[nodeId] = currentLevel + 1;
            SaveAndFlush();
            return MaintenancePurchaseResult.Success;
        }

        public IReadOnlyList<MaintenanceNodeViewData> GetNodeViewData()
        {
            var viewData = new MaintenanceNodeViewData[definitions.Count];
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                var currentLevel = levelsById[definition.Id];
                var isMaxLevel = currentLevel == definition.MaxLevel;
                var missingRequirementIds = currentLevel > 0
                    ? Array.Empty<string>()
                    : GetMissingRequirementIds(definition);
                var state = currentLevel > 0
                    ? MaintenanceNodeState.Owned
                    : missingRequirementIds.Length > 0
                        ? MaintenanceNodeState.Locked
                        : MaintenanceNodeState.Available;
                var nextCost = isMaxLevel
                    ? (long?)null
                    : definition.CostsByLevel[currentLevel];
                var canAfford = nextCost.HasValue && Funds >= nextCost.Value;

                viewData[index] = new MaintenanceNodeViewData(
                    definition.Id,
                    definition.DisplayName,
                    definition.Branch,
                    currentLevel,
                    definition.MaxLevel,
                    state,
                    currentLevel == 0
                        ? NotOwnedEffectText
                        : definition.EffectTextsByLevel[currentLevel - 1],
                    isMaxLevel ? null : definition.EffectTextsByLevel[currentLevel],
                    nextCost,
                    isMaxLevel,
                    canAfford,
                    !isMaxLevel && state != MaintenanceNodeState.Locked && canAfford,
                    missingRequirementIds);
            }

            return Array.AsReadOnly(viewData);
        }

        private bool RequirementsMet(MaintenanceDefinition definition)
        {
            foreach (var requirement in definition.Requirements)
            {
                if (!levelsById.TryGetValue(requirement.NodeId, out var level) ||
                    level < requirement.RequiredLevel)
                {
                    return false;
                }
            }

            return true;
        }

        private string[] GetMissingRequirementIds(MaintenanceDefinition definition)
        {
            var missingIds = new List<string>();
            foreach (var requirement in definition.Requirements)
            {
                if (!levelsById.TryGetValue(requirement.NodeId, out var level) ||
                    level < requirement.RequiredLevel)
                {
                    missingIds.Add(requirement.NodeId);
                }
            }

            return missingIds.ToArray();
        }

        private void SaveAndFlush()
        {
            var data = saveService.Data;
            data.funds = ledger.Funds;
            data.maintenanceLevels = new List<SaveMaintenanceLevel>();
            foreach (var definition in definitions)
            {
                var level = levelsById[definition.Id];
                if (level > 0)
                {
                    data.maintenanceLevels.Add(new SaveMaintenanceLevel(definition.Id, level));
                }
            }

            saveService.MarkDirty();
            saveService.Flush();
        }
    }
}
