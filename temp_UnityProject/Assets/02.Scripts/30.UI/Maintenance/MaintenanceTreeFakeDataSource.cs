#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Maintenance;
using UnityEngine;

namespace Icebreaker.UI.Maintenance
{
    public interface IMaintenanceStepViewDataSource
    {
        event Action<IReadOnlyList<MaintenancePurchaseStepViewData>> StepsChanged;

        IReadOnlyList<MaintenancePurchaseStepViewData> CurrentSteps { get; }

        long CurrentFunds { get; }

        string CurrentPreviewStateLabel { get; }

        void EnsureInitialized();
    }

    public enum MaintenanceTreePreviewState
    {
        NewSave,
        C01Purchased,
        DirectPartial,
        RequirementsMetFundsShort,
        FullyPurchased
    }

    /// <summary>Presentation-only data for Phase D prefab review. It never reads Core or SaveData.</summary>
    public sealed class MaintenanceTreeFakeDataSource : MonoBehaviour, IMaintenanceStepViewDataSource
    {
        private sealed class FakeDefinition
        {
            public FakeDefinition(
                string id,
                string displayName,
                MaintenanceBranch branch,
                long[] costs,
                string[] effects,
                params FakeRequirement[] requirements)
            {
                Id = id;
                DisplayName = displayName;
                Branch = branch;
                Costs = costs;
                Effects = effects;
                Requirements = requirements;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public MaintenanceBranch Branch { get; }
            public long[] Costs { get; }
            public string[] Effects { get; }
            public FakeRequirement[] Requirements { get; }
        }

        private readonly struct FakeRequirement
        {
            public FakeRequirement(string id, int level)
            {
                Id = id;
                Level = level;
            }

            public string Id { get; }
            public int Level { get; }
        }

        [SerializeField] private MaintenanceTreePreviewState previewState;

        private static readonly FakeDefinition[] Definitions = CreateDefinitions();
        private IReadOnlyList<MaintenancePurchaseStepViewData> currentSteps =
            Array.Empty<MaintenancePurchaseStepViewData>();
        private bool initialized;

        public event Action<IReadOnlyList<MaintenancePurchaseStepViewData>> StepsChanged = delegate { };

        public IReadOnlyList<MaintenancePurchaseStepViewData> CurrentSteps => currentSteps;

        public long CurrentFunds { get; private set; }

        public string CurrentPreviewStateLabel => "가짜 상태 · " + (previewState switch
        {
            MaintenanceTreePreviewState.NewSave => "새 저장",
            MaintenanceTreePreviewState.C01Purchased => "C01만 구매",
            MaintenanceTreePreviewState.DirectPartial => "직접 파쇄 일부 구매",
            MaintenanceTreePreviewState.RequirementsMetFundsShort => "선행 충족 · 자금 부족",
            MaintenanceTreePreviewState.FullyPurchased => "전부 구매",
            _ => previewState.ToString()
        });

        private void OnEnable() => EnsureInitialized();

        public void EnsureInitialized()
        {
            if (!initialized)
            {
                Rebuild();
            }
        }

        public void SetPreviewState(MaintenanceTreePreviewState state)
        {
            previewState = state;
            Rebuild();
            StepsChanged(currentSteps);
        }

        [ContextMenu("Next Preview State")]
        public void SelectNextPreviewState()
        {
            var count = Enum.GetValues(typeof(MaintenanceTreePreviewState)).Length;
            SetPreviewState((MaintenanceTreePreviewState)(((int)previewState + 1) % count));
        }

        private void Rebuild()
        {
            var levels = CreatePreviewLevels(previewState);
            CurrentFunds = previewState == MaintenanceTreePreviewState.RequirementsMetFundsShort
                ? 0L
                : 100_000L;

            var states = new Dictionary<string, MaintenanceStepPurchaseState>(StringComparer.Ordinal);
            foreach (var definition in Definitions)
            {
                var currentLevel = levels[definition.Id];
                for (var targetLevel = 1; targetLevel <= definition.Costs.Length; targetLevel++)
                {
                    var stepId = MaintenanceStepId.Create(definition.Id, targetLevel);
                    states.Add(stepId, ResolvePurchaseState(definition, targetLevel, currentLevel, levels));
                }
            }

            var active = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in states)
            {
                if (pair.Value != MaintenanceStepPurchaseState.Locked)
                {
                    active.Add(pair.Key);
                }
            }

            var steps = new List<MaintenancePurchaseStepViewData>(26);
            foreach (var definition in Definitions)
            {
                var currentLevel = levels[definition.Id];
                for (var targetLevel = 1; targetLevel <= definition.Costs.Length; targetLevel++)
                {
                    var stepId = MaintenanceStepId.Create(definition.Id, targetLevel);
                    var purchaseState = states[stepId];
                    var parentIds = GetParentStepIds(definition, targetLevel);
                    var visibility = ResolveVisibility(purchaseState, parentIds, active);
                    var cost = definition.Costs[targetLevel - 1];
                    var canAfford = CurrentFunds >= cost;
                    steps.Add(new MaintenancePurchaseStepViewData(
                        stepId,
                        definition.Id,
                        definition.DisplayName,
                        definition.Branch,
                        targetLevel,
                        definition.Costs.Length,
                        purchaseState,
                        visibility,
                        cost,
                        definition.Effects[targetLevel - 1],
                        canAfford,
                        purchaseState == MaintenanceStepPurchaseState.Available && canAfford,
                        GetMissingRequirementIds(
                            definition,
                            targetLevel,
                            currentLevel,
                            levels,
                            purchaseState)));
                }
            }

            currentSteps = steps.AsReadOnly();
            initialized = true;
        }

        private static MaintenanceStepPurchaseState ResolvePurchaseState(
            FakeDefinition definition,
            int targetLevel,
            int currentLevel,
            IReadOnlyDictionary<string, int> levels)
        {
            if (targetLevel <= currentLevel)
            {
                return MaintenanceStepPurchaseState.Purchased;
            }

            if (targetLevel != currentLevel + 1)
            {
                return MaintenanceStepPurchaseState.Locked;
            }

            if (currentLevel > 0 || RequirementsMet(definition, levels))
            {
                return MaintenanceStepPurchaseState.Available;
            }

            return MaintenanceStepPurchaseState.Locked;
        }

        private static MaintenanceStepVisibility ResolveVisibility(
            MaintenanceStepPurchaseState purchaseState,
            IReadOnlyList<string> parentIds,
            ISet<string> active)
        {
            if (purchaseState != MaintenanceStepPurchaseState.Locked)
            {
                return MaintenanceStepVisibility.Visible;
            }

            foreach (var parentId in parentIds)
            {
                if (active.Contains(parentId))
                {
                    return MaintenanceStepVisibility.Preview;
                }
            }

            return MaintenanceStepVisibility.Hidden;
        }

        private static bool RequirementsMet(
            FakeDefinition definition,
            IReadOnlyDictionary<string, int> levels)
        {
            foreach (var requirement in definition.Requirements)
            {
                if (levels[requirement.Id] < requirement.Level)
                {
                    return false;
                }
            }

            return true;
        }

        private static string[] GetParentStepIds(FakeDefinition definition, int targetLevel)
        {
            if (targetLevel > 1)
            {
                return new[] { MaintenanceStepId.Create(definition.Id, targetLevel - 1) };
            }

            var parents = new string[definition.Requirements.Length];
            for (var index = 0; index < definition.Requirements.Length; index++)
            {
                parents[index] = MaintenanceStepId.Create(
                    definition.Requirements[index].Id,
                    definition.Requirements[index].Level);
            }

            return parents;
        }

        private static string[] GetMissingRequirementIds(
            FakeDefinition definition,
            int targetLevel,
            int currentLevel,
            IReadOnlyDictionary<string, int> levels,
            MaintenanceStepPurchaseState purchaseState)
        {
            if (purchaseState != MaintenanceStepPurchaseState.Locked)
            {
                return Array.Empty<string>();
            }

            if (targetLevel > currentLevel + 1)
            {
                return new[] { definition.Id };
            }

            var result = new List<string>();
            foreach (var requirement in definition.Requirements)
            {
                if (levels[requirement.Id] < requirement.Level)
                {
                    result.Add(requirement.Id);
                }
            }

            return result.ToArray();
        }

        private static Dictionary<string, int> CreatePreviewLevels(MaintenanceTreePreviewState state)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var definition in Definitions)
            {
                result.Add(definition.Id, state == MaintenanceTreePreviewState.FullyPurchased
                    ? definition.Costs.Length
                    : 0);
            }

            if (state != MaintenanceTreePreviewState.NewSave &&
                state != MaintenanceTreePreviewState.FullyPurchased)
            {
                result["C01"] = 1;
            }

            if (state == MaintenanceTreePreviewState.DirectPartial ||
                state == MaintenanceTreePreviewState.RequirementsMetFundsShort)
            {
                result["D01"] = 2;
            }

            return result;
        }

        private static FakeRequirement Require(string id, int level = 1) => new FakeRequirement(id, level);

        private static FakeDefinition[] CreateDefinitions()
        {
            return new[]
            {
                new FakeDefinition("C01", "강화 장비", MaintenanceBranch.Common,
                    new long[] { 100 }, new[] { "파쇄 계통 개방" }),
                new FakeDefinition("C02", "정비 효율", MaintenanceBranch.Common,
                    new long[] { 400, 2_400, 14_000 },
                    new[] { "파괴 자금 +10%", "파괴 자금 +20%", "파괴 자금 +30%" }, Require("C01")),
                new FakeDefinition("C03", "청빙 대응", MaintenanceBranch.Common,
                    new long[] { 1_200 }, new[] { "청빙 출현" }, Require("C01")),
                new FakeDefinition("C04", "심빙 대응", MaintenanceBranch.Common,
                    new long[] { 18_000 }, new[] { "심빙 출현" }, Require("C03")),
                new FakeDefinition("D01", "주 파쇄기 출력", MaintenanceBranch.Direct,
                    new long[] { 300, 900, 2_700 },
                    new[] { "직접 피해 ×1.6", "직접 피해 ×2.56", "직접 피해 ×4.096" }, Require("C01")),
                new FakeDefinition("D02", "고속 구동", MaintenanceBranch.Direct,
                    new long[] { 600, 1_800, 5_400 },
                    new[] { "자동 타격 +2회/초", "자동 타격 +4회/초", "자동 타격 +6회/초" }, Require("C01")),
                new FakeDefinition("D03", "과잉 파쇄", MaintenanceBranch.Direct,
                    new long[] { 8_000 }, new[] { "초과 피해 50% 전달" }, Require("D01", 2)),
                new FakeDefinition("D04", "범위 확장", MaintenanceBranch.Direct,
                    new long[] { 1_000, 3_000, 9_000 },
                    new[] { "커서 반경 72px", "커서 반경 88px", "커서 반경 104px" }, Require("D02")),
                new FakeDefinition("S01", "보조 파쇄기", MaintenanceBranch.Support,
                    new long[] { 500 }, new[] { "유효 틱 12회마다 보조탄" }, Require("D01")),
                new FakeDefinition("S02", "다중 타격", MaintenanceBranch.Support,
                    new long[] { 3_000, 9_000 }, new[] { "보조 대상 +1", "보조 대상 +2" }, Require("S01")),
                new FakeDefinition("S03", "표적 분석", MaintenanceBranch.Support,
                    new long[] { 15_000 }, new[] { "특수빙 우선 · 피해 ×2" }, Require("S01"), Require("S02")),
                new FakeDefinition("H01", "파편 비산", MaintenanceBranch.Chain,
                    new long[] { 700, 2_100, 6_300 },
                    new[] { "파괴 반경 피해 ×0.25", "파괴 반경 피해 ×0.50", "파괴 반경 피해 ×0.75" }, Require("D01", 2)),
                new FakeDefinition("H02", "특수빙 증폭", MaintenanceBranch.Chain,
                    new long[] { 4_000, 12_000 }, new[] { "특수빙 효과 +30%", "특수빙 효과 +60%" }, Require("H01")),
                new FakeDefinition("H03", "빙판 붕괴", MaintenanceBranch.Chain,
                    new long[] { 20_000 }, new[] { "5연쇄 파괴 시 범위 피해" }, Require("H01", 3))
            };
        }
    }
}
