#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Maintenance;

namespace Icebreaker.Core
{
    internal sealed class MaintenanceStepProjector
    {
        private sealed class StepDefinition
        {
            public StepDefinition(
                MaintenanceDefinition maintenance,
                int targetLevel,
                string[] parentStepIds)
            {
                Maintenance = maintenance;
                TargetLevel = targetLevel;
                StepId = MaintenanceStepId.Create(maintenance.Id, targetLevel);
                ParentStepIds = parentStepIds;
            }

            public MaintenanceDefinition Maintenance { get; }

            public int TargetLevel { get; }

            public string StepId { get; }

            public string[] ParentStepIds { get; }
        }

        private readonly StepDefinition[] steps;

        public MaintenanceStepProjector(IReadOnlyList<MaintenanceDefinition> definitions)
        {
            var definitionsById = CreateDefinitionMap(definitions);
            steps = CreateSteps(definitions, definitionsById);
            ValidateAcyclicGraph(steps);
        }

        public IReadOnlyList<MaintenancePurchaseStepViewData> Project(
            IReadOnlyDictionary<string, int> levelsById,
            long funds)
        {
            var purchaseStates = new Dictionary<string, MaintenanceStepPurchaseState>(
                steps.Length,
                StringComparer.Ordinal);
            foreach (var step in steps)
            {
                purchaseStates.Add(step.StepId, ResolvePurchaseState(step, levelsById));
            }

            var activeSteps = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in purchaseStates)
            {
                if (pair.Value == MaintenanceStepPurchaseState.Purchased ||
                    pair.Value == MaintenanceStepPurchaseState.Available)
                {
                    activeSteps.Add(pair.Key);
                }
            }

            var result = new MaintenancePurchaseStepViewData[steps.Length];
            for (var index = 0; index < steps.Length; index++)
            {
                var step = steps[index];
                var purchaseState = purchaseStates[step.StepId];
                var visibility = MaintenanceTreeVisibilityResolver.Resolve(
                    purchaseState,
                    step.ParentStepIds,
                    activeSteps);
                var cost = step.Maintenance.CostsByLevel[step.TargetLevel - 1];
                var canAfford = funds >= cost;
                result[index] = new MaintenancePurchaseStepViewData(
                    step.StepId,
                    step.Maintenance.Id,
                    step.Maintenance.DisplayName,
                    step.Maintenance.Branch,
                    step.TargetLevel,
                    step.Maintenance.MaxLevel,
                    purchaseState,
                    visibility,
                    cost,
                    step.Maintenance.EffectTextsByLevel[step.TargetLevel - 1],
                    canAfford,
                    purchaseState == MaintenanceStepPurchaseState.Available &&
                    visibility == MaintenanceStepVisibility.Visible &&
                    canAfford,
                    ResolveMissingRequirementIds(step, levelsById, purchaseState));
            }

            return Array.AsReadOnly(result);
        }

        private static Dictionary<string, MaintenanceDefinition> CreateDefinitionMap(
            IReadOnlyList<MaintenanceDefinition> definitions)
        {
            var result = new Dictionary<string, MaintenanceDefinition>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                result.Add(definition.Id, definition);
            }

            foreach (var definition in definitions)
            {
                foreach (var requirement in definition.Requirements)
                {
                    if (!result.TryGetValue(requirement.NodeId, out var requiredDefinition))
                    {
                        throw new ArgumentException(
                            $"Maintenance {definition.Id} requires undefined node {requirement.NodeId}.",
                            nameof(definitions));
                    }

                    if (requirement.RequiredLevel > requiredDefinition.MaxLevel)
                    {
                        throw new ArgumentException(
                            $"Maintenance {definition.Id} requires an out-of-range level of {requirement.NodeId}.",
                            nameof(definitions));
                    }
                }
            }

            return result;
        }

        private static StepDefinition[] CreateSteps(
            IReadOnlyList<MaintenanceDefinition> definitions,
            IReadOnlyDictionary<string, MaintenanceDefinition> definitionsById)
        {
            var count = 0;
            foreach (var definition in definitions)
            {
                count += definition.MaxLevel;
            }

            var result = new StepDefinition[count];
            var index = 0;
            foreach (var definition in definitions)
            {
                for (var targetLevel = 1; targetLevel <= definition.MaxLevel; targetLevel++)
                {
                    string[] parentIds;
                    if (targetLevel > 1)
                    {
                        parentIds = new[] { MaintenanceStepId.Create(definition.Id, targetLevel - 1) };
                    }
                    else
                    {
                        parentIds = new string[definition.Requirements.Count];
                        for (var requirementIndex = 0;
                             requirementIndex < definition.Requirements.Count;
                             requirementIndex++)
                        {
                            var requirement = definition.Requirements[requirementIndex];
                            _ = definitionsById[requirement.NodeId];
                            parentIds[requirementIndex] = MaintenanceStepId.Create(
                                requirement.NodeId,
                                requirement.RequiredLevel);
                        }
                    }

                    result[index++] = new StepDefinition(definition, targetLevel, parentIds);
                }
            }

            return result;
        }

        private static MaintenanceStepPurchaseState ResolvePurchaseState(
            StepDefinition step,
            IReadOnlyDictionary<string, int> levelsById)
        {
            var currentLevel = levelsById[step.Maintenance.Id];
            if (step.TargetLevel <= currentLevel)
            {
                return MaintenanceStepPurchaseState.Purchased;
            }

            if (step.TargetLevel != currentLevel + 1)
            {
                return MaintenanceStepPurchaseState.Locked;
            }

            if (currentLevel > 0 || RequirementsMet(step.Maintenance, levelsById))
            {
                return MaintenanceStepPurchaseState.Available;
            }

            return MaintenanceStepPurchaseState.Locked;
        }

        private static string[] ResolveMissingRequirementIds(
            StepDefinition step,
            IReadOnlyDictionary<string, int> levelsById,
            MaintenanceStepPurchaseState purchaseState)
        {
            if (purchaseState != MaintenanceStepPurchaseState.Locked)
            {
                return Array.Empty<string>();
            }

            var currentLevel = levelsById[step.Maintenance.Id];
            if (step.TargetLevel > currentLevel + 1)
            {
                return new[] { step.Maintenance.Id };
            }

            var missingIds = new List<string>();
            foreach (var requirement in step.Maintenance.Requirements)
            {
                if (levelsById[requirement.NodeId] < requirement.RequiredLevel)
                {
                    missingIds.Add(requirement.NodeId);
                }
            }

            return missingIds.ToArray();
        }

        private static bool RequirementsMet(
            MaintenanceDefinition definition,
            IReadOnlyDictionary<string, int> levelsById)
        {
            foreach (var requirement in definition.Requirements)
            {
                if (levelsById[requirement.NodeId] < requirement.RequiredLevel)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ValidateAcyclicGraph(IReadOnlyList<StepDefinition> definitions)
        {
            var definitionsById = new Dictionary<string, StepDefinition>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                definitionsById.Add(definition.StepId, definition);
            }

            var states = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                Visit(definition, definitionsById, states);
            }
        }

        private static void Visit(
            StepDefinition definition,
            IReadOnlyDictionary<string, StepDefinition> definitionsById,
            IDictionary<string, int> states)
        {
            if (states.TryGetValue(definition.StepId, out var state))
            {
                if (state == 1)
                {
                    throw new ArgumentException("Maintenance requirements cannot contain a cycle.");
                }

                return;
            }

            states[definition.StepId] = 1;
            foreach (var parentId in definition.ParentStepIds)
            {
                Visit(definitionsById[parentId], definitionsById, states);
            }

            states[definition.StepId] = 2;
        }
    }

    internal static class MaintenanceTreeVisibilityResolver
    {
        public static MaintenanceStepVisibility Resolve(
            MaintenanceStepPurchaseState purchaseState,
            IReadOnlyList<string> parentStepIds,
            ISet<string> activeStepIds)
        {
            if (purchaseState == MaintenanceStepPurchaseState.Purchased ||
                purchaseState == MaintenanceStepPurchaseState.Available)
            {
                return MaintenanceStepVisibility.Visible;
            }

            foreach (var parentId in parentStepIds)
            {
                if (activeStepIds.Contains(parentId))
                {
                    return MaintenanceStepVisibility.Preview;
                }
            }

            return MaintenanceStepVisibility.Hidden;
        }
    }
}
