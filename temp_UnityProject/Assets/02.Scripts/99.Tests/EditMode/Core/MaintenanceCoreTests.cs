#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Icebreaker.Shared.Maintenance;
using NUnit.Framework;

namespace Icebreaker.Core.Tests
{
    public sealed class MaintenanceCoreTests
    {
        private string tempDir = null!;

        [SetUp]
        public void SetUp()
        {
            tempDir = Path.Combine(
                Path.GetTempPath(),
                "icebreaker-maintenance-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Test]
        public void Catalog_StandardAndDemoMatchSpecification()
        {
            var expected = CreateCatalogExpectations();
            var standard = MaintenanceCatalog.CreateStandard();
            var demo = MaintenanceCatalog.CreateDemo();

            Assert.That(standard, Has.Count.EqualTo(13));
            Assert.That(demo, Has.Count.EqualTo(13));
            for (var index = 0; index < expected.Length; index++)
            {
                var item = expected[index];
                var standardDefinition = standard[index];
                var demoDefinition = demo[index];

                Assert.That(standardDefinition.Id, Is.EqualTo(item.Id));
                Assert.That(standardDefinition.DisplayName, Is.EqualTo(item.DisplayName));
                Assert.That(standardDefinition.Branch, Is.EqualTo(item.Branch));
                Assert.That(standardDefinition.MaxLevel, Is.EqualTo(item.Costs.Length));
                Assert.That(standardDefinition.CostsByLevel, Is.EqualTo(item.Costs));
                Assert.That(standardDefinition.EffectTextsByLevel, Is.EqualTo(item.Effects));
                AssertRequirements(standardDefinition, item.Requirements);

                Assert.That(demoDefinition.Id, Is.EqualTo(item.Id));
                Assert.That(demoDefinition.MaxLevel, Is.EqualTo(item.Costs.Length));
                for (var levelIndex = 0; levelIndex < item.Costs.Length; levelIndex++)
                {
                    Assert.That(
                        demoDefinition.CostsByLevel[levelIndex],
                        Is.EqualTo((item.Costs[levelIndex] + 9) / 10));
                }

                Assert.That(demoDefinition.EffectTextsByLevel, Is.EqualTo(item.Effects));
                AssertRequirements(demoDefinition, item.Requirements);
            }
        }

        [Test]
        public void ViewData_CalculatesOwnedAvailableLockedAndAffordability()
        {
            var data = SaveData.CreateNew("standard");
            data.funds = 100;
            var core = CreateCore(data, MaintenanceCatalog.CreateStandard());

            var initial = core.GetNodeViewData();
            var c01 = Find(initial, MaintenanceCatalog.C01);
            var c02 = Find(initial, MaintenanceCatalog.C02);
            var s03 = Find(initial, MaintenanceCatalog.S03);

            Assert.That(initial, Has.Count.EqualTo(13));
            Assert.That(c01.State, Is.EqualTo(MaintenanceNodeState.Available));
            Assert.That(c01.CurrentEffectText, Is.EqualTo("미보유"));
            Assert.That(c01.NextCost, Is.EqualTo(100));
            Assert.That(c01.CanAffordNextLevel, Is.True);
            Assert.That(c01.CanPurchaseNextLevel, Is.True);
            Assert.That(c02.State, Is.EqualTo(MaintenanceNodeState.Locked));
            Assert.That(c02.MissingRequirementIds, Is.EqualTo(new[] { MaintenanceCatalog.C01 }));
            Assert.That(c02.CanPurchaseNextLevel, Is.False);
            Assert.That(
                s03.MissingRequirementIds,
                Is.EqualTo(new[] { MaintenanceCatalog.S01, MaintenanceCatalog.S02 }));

            Assert.That(core.TryPurchase(MaintenanceCatalog.C01), Is.True);
            var purchased = core.GetNodeViewData();
            c01 = Find(purchased, MaintenanceCatalog.C01);
            c02 = Find(purchased, MaintenanceCatalog.C02);

            Assert.That(c01.State, Is.EqualTo(MaintenanceNodeState.Owned));
            Assert.That(c01.CurrentLevel, Is.EqualTo(1));
            Assert.That(c01.IsMaxLevel, Is.True);
            Assert.That(c01.NextCost, Is.Null);
            Assert.That(c01.NextEffectText, Is.Null);
            Assert.That(c01.CanAffordNextLevel, Is.False);
            Assert.That(c01.CanPurchaseNextLevel, Is.False);
            Assert.That(c02.State, Is.EqualTo(MaintenanceNodeState.Available));
            Assert.That(c02.MissingRequirementIds, Is.Empty);
            Assert.That(c02.CanAffordNextLevel, Is.False);
            Assert.That(c02.CanPurchaseNextLevel, Is.False);
        }

        [Test]
        public void TryPurchase_EnforcesPrerequisitesMaxLevelAndExactCosts()
        {
            var data = SaveData.CreateNew("standard");
            data.funds = 5_000;
            var core = CreateCore(data, MaintenanceCatalog.CreateStandard());

            Assert.That(core.TryPurchase(MaintenanceCatalog.D01), Is.False);
            Assert.That(core.Funds, Is.EqualTo(5_000));
            Assert.That(core.TryPurchase(MaintenanceCatalog.C01), Is.True);
            Assert.That(core.TryPurchase(MaintenanceCatalog.D01), Is.True);
            Assert.That(core.TryPurchase(MaintenanceCatalog.D01), Is.True);
            Assert.That(core.TryPurchase(MaintenanceCatalog.D01), Is.True);
            Assert.That(core.TryPurchase(MaintenanceCatalog.D01), Is.False);

            Assert.That(core.Funds, Is.EqualTo(1_000));
            Assert.That(Find(core.GetNodeViewData(), MaintenanceCatalog.D01).CurrentLevel, Is.EqualTo(3));
        }

        [Test]
        public void TryPurchase_RejectsUnaffordableWithoutNegativeFunds()
        {
            var data = SaveData.CreateNew("standard");
            data.funds = 99;
            var core = CreateCore(data, MaintenanceCatalog.CreateStandard());

            Assert.That(core.TryPurchase(MaintenanceCatalog.C01), Is.False);

            Assert.That(core.Funds, Is.EqualTo(99));
            Assert.That(data.funds, Is.EqualTo(99));
            Assert.That(data.maintenanceLevels, Is.Empty);
        }

        [Test]
        public void TryPurchase_EnforcesLevelAndMultipleRequirements()
        {
            var data = SaveData.CreateNew("standard");
            data.funds = 100_000;
            var core = CreateCore(data, MaintenanceCatalog.CreateStandard());

            Assert.That(core.TryPurchase(MaintenanceCatalog.C01), Is.True);
            Assert.That(core.TryPurchase(MaintenanceCatalog.D01), Is.True);
            Assert.That(core.TryPurchase(MaintenanceCatalog.D03), Is.False);
            Assert.That(core.TryPurchase(MaintenanceCatalog.D01), Is.True);
            Assert.That(core.TryPurchase(MaintenanceCatalog.D03), Is.True);

            Assert.That(core.TryPurchase(MaintenanceCatalog.S03), Is.False);
            Assert.That(core.TryPurchase(MaintenanceCatalog.S01), Is.True);
            Assert.That(core.TryPurchase(MaintenanceCatalog.S03), Is.False);
            Assert.That(core.TryPurchase(MaintenanceCatalog.S02), Is.True);
            Assert.That(core.TryPurchase(MaintenanceCatalog.S03), Is.True);
            Assert.That(core.Funds, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void TryPurchase_UsesDemoCosts()
        {
            var data = SaveData.CreateNew("demo");
            data.funds = 50;
            var core = CreateCore(data, MaintenanceCatalog.CreateDemo());

            Assert.That(core.TryPurchase(MaintenanceCatalog.C01), Is.True);
            Assert.That(core.TryPurchase(MaintenanceCatalog.C02), Is.True);

            Assert.That(core.Funds, Is.Zero);
        }

        [Test]
        public void Purchase_FlushesAndRoundTripsLevelsAndFunds()
        {
            var store = new SaveStore(tempDir);
            var data = SaveData.CreateNew("standard");
            data.funds = 1_000;
            var service = new SaveService(store, data);
            var core = new MaintenanceCore(MaintenanceCatalog.CreateStandard(), service);

            Assert.That(core.TryPurchase(MaintenanceCatalog.C01), Is.True);
            Assert.That(core.TryPurchase(MaintenanceCatalog.C02), Is.True);

            Assert.That(service.HasPendingWrite, Is.False);
            Assert.That(File.Exists(store.PathFor("standard")), Is.True);

            var loaded = store.TryLoad("standard");
            Assert.That(loaded, Is.Not.Null);
            var reloaded = new MaintenanceCore(
                MaintenanceCatalog.CreateStandard(),
                new SaveService(store, loaded!));

            Assert.That(reloaded.Funds, Is.EqualTo(500));
            Assert.That(reloaded.MaintenanceEfficiencyLevel, Is.EqualTo(1));
            Assert.That(Find(reloaded.GetNodeViewData(), MaintenanceCatalog.C01).CurrentLevel, Is.EqualTo(1));
            Assert.That(Find(reloaded.GetNodeViewData(), MaintenanceCatalog.C02).CurrentLevel, Is.EqualTo(1));
        }

        private MaintenanceCore CreateCore(
            SaveData data,
            IReadOnlyList<MaintenanceDefinition> definitions)
        {
            return new MaintenanceCore(definitions, new SaveService(new SaveStore(tempDir), data));
        }

        private static MaintenanceNodeViewData Find(
            IReadOnlyList<MaintenanceNodeViewData> nodes,
            string id)
        {
            foreach (var node in nodes)
            {
                if (node.Id == id)
                {
                    return node;
                }
            }

            throw new AssertionException($"Maintenance node {id} was not found.");
        }

        private static void AssertRequirements(
            MaintenanceDefinition definition,
            IReadOnlyList<MaintenanceRequirement> expected)
        {
            Assert.That(definition.Requirements, Has.Count.EqualTo(expected.Count));
            for (var index = 0; index < expected.Count; index++)
            {
                Assert.That(definition.Requirements[index].NodeId, Is.EqualTo(expected[index].NodeId));
                Assert.That(
                    definition.Requirements[index].RequiredLevel,
                    Is.EqualTo(expected[index].RequiredLevel));
            }
        }

        private static CatalogExpectation[] CreateCatalogExpectations()
        {
            return new[]
            {
                Expect(MaintenanceCatalog.C01, "강화 장비", MaintenanceBranch.Common,
                    new long[] { 100 }, new[] { "파쇄 계통 개방" }),
                Expect(MaintenanceCatalog.C02, "정비 효율", MaintenanceBranch.Common,
                    new long[] { 400, 2_400, 14_000 },
                    new[] { "파괴 자금 +10%", "파괴 자금 +20%", "파괴 자금 +30%" },
                    Require(MaintenanceCatalog.C01)),
                Expect(MaintenanceCatalog.C03, "청빙 대응", MaintenanceBranch.Common,
                    new long[] { 1_200 }, new[] { "청빙 출현" }, Require(MaintenanceCatalog.C01)),
                Expect(MaintenanceCatalog.C04, "심빙 대응", MaintenanceBranch.Common,
                    new long[] { 18_000 }, new[] { "심빙 출현" }, Require(MaintenanceCatalog.C03)),
                Expect(MaintenanceCatalog.D01, "주 파쇄기 출력", MaintenanceBranch.Direct,
                    new long[] { 300, 900, 2_700 },
                    new[] { "직접 피해 ×1.6", "직접 피해 ×2.56", "직접 피해 ×4.096" },
                    Require(MaintenanceCatalog.C01)),
                Expect(MaintenanceCatalog.D02, "고속 구동", MaintenanceBranch.Direct,
                    new long[] { 600, 1_800, 5_400 },
                    new[] { "누르기 속도 +2/초", "누르기 속도 +4/초", "누르기 속도 +6/초" },
                    Require(MaintenanceCatalog.C01)),
                Expect(MaintenanceCatalog.D03, "과잉 파쇄", MaintenanceBranch.Direct,
                    new long[] { 8_000 }, new[] { "초과 피해 50% 전달" },
                    Require(MaintenanceCatalog.D01, 2)),
                Expect(MaintenanceCatalog.S01, "보조 파쇄기", MaintenanceBranch.Support,
                    new long[] { 500 }, new[] { "12회 입력마다 보조탄" },
                    Require(MaintenanceCatalog.C01)),
                Expect(MaintenanceCatalog.S02, "다중 타격", MaintenanceBranch.Support,
                    new long[] { 3_000, 9_000 }, new[] { "보조 대상 +1", "보조 대상 +2" },
                    Require(MaintenanceCatalog.S01)),
                Expect(MaintenanceCatalog.S03, "표적 분석", MaintenanceBranch.Support,
                    new long[] { 15_000 }, new[] { "특수빙 우선 · 피해 ×2" },
                    Require(MaintenanceCatalog.S01), Require(MaintenanceCatalog.S02)),
                Expect(MaintenanceCatalog.H01, "파편 비산", MaintenanceBranch.Chain,
                    new long[] { 700, 2_100, 6_300 },
                    new[] { "파괴 반경 피해 ×0.25", "파괴 반경 피해 ×0.50", "파괴 반경 피해 ×0.75" },
                    Require(MaintenanceCatalog.C01)),
                Expect(MaintenanceCatalog.H02, "특수빙 증폭", MaintenanceBranch.Chain,
                    new long[] { 4_000, 12_000 }, new[] { "특수빙 효과 +30%", "특수빙 효과 +60%" },
                    Require(MaintenanceCatalog.H01)),
                Expect(MaintenanceCatalog.H03, "빙판 붕괴", MaintenanceBranch.Chain,
                    new long[] { 20_000 }, new[] { "5연쇄 시 빙판 붕괴" },
                    Require(MaintenanceCatalog.H01, 3))
            };
        }

        private static CatalogExpectation Expect(
            string id,
            string displayName,
            MaintenanceBranch branch,
            long[] costs,
            string[] effects,
            params MaintenanceRequirement[] requirements)
        {
            return new CatalogExpectation(id, displayName, branch, costs, effects, requirements);
        }

        private static MaintenanceRequirement Require(string id, int level = 1)
        {
            return new MaintenanceRequirement(id, level);
        }

        private sealed class CatalogExpectation
        {
            public CatalogExpectation(
                string id,
                string displayName,
                MaintenanceBranch branch,
                long[] costs,
                string[] effects,
                MaintenanceRequirement[] requirements)
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

            public MaintenanceRequirement[] Requirements { get; }
        }
    }
}
