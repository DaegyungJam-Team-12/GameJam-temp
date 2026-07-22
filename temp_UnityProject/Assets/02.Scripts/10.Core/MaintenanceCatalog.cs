#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Maintenance;

namespace Icebreaker.Core
{
    public static class MaintenanceCatalog
    {
        public const string C01 = "C01";
        public const string C02 = "C02";
        public const string C03 = "C03";
        public const string C04 = "C04";
        public const string D01 = "D01";
        public const string D02 = "D02";
        public const string D03 = "D03";
        public const string D04 = "D04";
        public const string S01 = "S01";
        public const string S02 = "S02";
        public const string S03 = "S03";
        public const string H01 = "H01";
        public const string H02 = "H02";
        public const string H03 = "H03";

        public static IReadOnlyList<MaintenanceDefinition> CreateStandard()
        {
            return Array.AsReadOnly(new[]
            {
                Define(C01, "강화 장비", MaintenanceBranch.Common, new long[] { 100 },
                    new[] { "파쇄 계통 개방" }),
                Define(C02, "정비 효율", MaintenanceBranch.Common, new long[] { 400, 2_400, 14_000 },
                    new[] { "파괴 자금 +10%", "파괴 자금 +20%", "파괴 자금 +30%" },
                    Require(C01)),
                Define(C03, "청빙 대응", MaintenanceBranch.Common, new long[] { 1_200 },
                    new[] { "청빙 출현" }, Require(C01)),
                Define(C04, "심빙 대응", MaintenanceBranch.Common, new long[] { 18_000 },
                    new[] { "심빙 출현" }, Require(C03)),
                Define(D01, "주 파쇄기 출력", MaintenanceBranch.Direct, new long[] { 300, 900, 2_700 },
                    new[] { "직접 피해 ×1.6", "직접 피해 ×2.56", "직접 피해 ×4.096" },
                    Require(C01)),
                Define(D02, "고속 구동", MaintenanceBranch.Direct, new long[] { 600, 1_800, 5_400 },
                    new[] { "자동 타격 +2회/초", "자동 타격 +4회/초", "자동 타격 +6회/초" },
                    Require(C01)),
                Define(D03, "과잉 파쇄", MaintenanceBranch.Direct, new long[] { 8_000 },
                    new[] { "초과 피해 50% 전달" }, Require(D01, 2)),
                Define(D04, "범위 확장", MaintenanceBranch.Direct, new long[] { 1_000, 3_000, 9_000 },
                    new[] { "커서 반경 72px", "커서 반경 88px", "커서 반경 104px" },
                    Require(C01)),
                Define(S01, "보조 파쇄기", MaintenanceBranch.Support, new long[] { 500 },
                    new[] { "유효 자동 틱 12회마다 보조탄" }, Require(C01)),
                Define(S02, "다중 타격", MaintenanceBranch.Support, new long[] { 3_000, 9_000 },
                    new[] { "보조 대상 +1", "보조 대상 +2" }, Require(S01)),
                Define(S03, "표적 분석", MaintenanceBranch.Support, new long[] { 15_000 },
                    new[] { "특수빙 우선 · 피해 ×2" }, Require(S01), Require(S02)),
                Define(H01, "파편 비산", MaintenanceBranch.Chain, new long[] { 700, 2_100, 6_300 },
                    new[] { "파괴 반경 피해 ×0.25", "파괴 반경 피해 ×0.50", "파괴 반경 피해 ×0.75" },
                    Require(C01)),
                Define(H02, "특수빙 증폭", MaintenanceBranch.Chain, new long[] { 4_000, 12_000 },
                    new[] { "특수빙 효과 +30%", "특수빙 효과 +60%" }, Require(H01)),
                Define(H03, "빙판 붕괴", MaintenanceBranch.Chain, new long[] { 20_000 },
                    new[] { "5연쇄 시 빙판 붕괴" }, Require(H01, 3))
            });
        }

        public static IReadOnlyList<MaintenanceDefinition> CreateDemo()
        {
            var standard = CreateStandard();
            var demo = new MaintenanceDefinition[standard.Count];
            for (var index = 0; index < standard.Count; index++)
            {
                var definition = standard[index];
                var costs = new long[definition.CostsByLevel.Count];
                for (var levelIndex = 0; levelIndex < costs.Length; levelIndex++)
                {
                    costs[levelIndex] = (definition.CostsByLevel[levelIndex] + 9) / 10;
                }

                demo[index] = new MaintenanceDefinition(
                    definition.Id,
                    definition.DisplayName,
                    definition.Branch,
                    definition.MaxLevel,
                    costs,
                    definition.EffectTextsByLevel,
                    definition.Requirements);
            }

            return Array.AsReadOnly(demo);
        }

        private static MaintenanceDefinition Define(
            string id,
            string displayName,
            MaintenanceBranch branch,
            IReadOnlyList<long> costs,
            IReadOnlyList<string> effects,
            params MaintenanceRequirement[] requirements)
        {
            return new MaintenanceDefinition(
                id,
                displayName,
                branch,
                costs.Count,
                costs,
                effects,
                requirements);
        }

        private static MaintenanceRequirement Require(string nodeId, int level = 1)
        {
            return new MaintenanceRequirement(nodeId, level);
        }
    }
}
