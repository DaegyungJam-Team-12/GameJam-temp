#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Icebreaker.Shared.Maintenance;
using NUnit.Framework;

namespace Icebreaker.Core.Tests
{
    public sealed class MaintenancePurchaseStepTests
    {
        private string tempDirectory = null!;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(
                Path.GetTempPath(),
                "icebreaker-maintenance-steps-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        [Test]
        public void Catalog_ProjectsExactly26UniqueInRangeStepsForEveryLogicalNode()
        {
            var definitions = MaintenanceCatalog.CreateStandard();
            var steps = CreateCore(SaveData.CreateNew("standard"), definitions)
                .GetPurchaseStepViewData();

            Assert.That(steps, Has.Count.EqualTo(26));
            Assert.That(steps.Select(step => step.StepId).Distinct().Count(), Is.EqualTo(26));
            foreach (var definition in definitions)
            {
                var nodeSteps = steps.Where(step => step.MaintenanceId == definition.Id).ToArray();
                Assert.That(nodeSteps, Has.Length.EqualTo(definition.MaxLevel));
                Assert.That(
                    nodeSteps.Select(step => step.TargetLevel),
                    Is.EqualTo(Enumerable.Range(1, definition.MaxLevel)));
                Assert.That(nodeSteps.All(step => step.MaxLevel == definition.MaxLevel), Is.True);
            }
        }

        [Test]
        public void InitialVisibility_ShowsC01AndPreviewsOnlyItsFourDirectChildren()
        {
            var steps = CreateCore(SaveData.CreateNew("standard"))
                .GetPurchaseStepViewData();

            AssertStep(
                steps,
                MaintenanceCatalog.C01,
                1,
                MaintenanceStepPurchaseState.Available,
                MaintenanceStepVisibility.Visible);

            foreach (var childId in new[]
                     {
                         MaintenanceCatalog.C02,
                         MaintenanceCatalog.C03,
                         MaintenanceCatalog.D01,
                         MaintenanceCatalog.D02
                     })
            {
                AssertStep(
                    steps,
                    childId,
                    1,
                    MaintenanceStepPurchaseState.Locked,
                    MaintenanceStepVisibility.Preview);
            }

            AssertStep(
                steps,
                MaintenanceCatalog.C02,
                2,
                MaintenanceStepPurchaseState.Locked,
                MaintenanceStepVisibility.Hidden);
            AssertStep(
                steps,
                MaintenanceCatalog.S01,
                1,
                MaintenanceStepPurchaseState.Locked,
                MaintenanceStepVisibility.Hidden);
        }

        [Test]
        public void C01Purchase_RevealsFourChoicesAndOnlyTheirNextStepChildren()
        {
            var data = SaveData.CreateNew("standard");
            data.funds = 100_000;
            var core = CreateCore(data);
            Assert.That(core.TryPurchase(MaintenanceCatalog.C01), Is.True);

            var steps = core.GetPurchaseStepViewData();
            foreach (var childId in new[]
                     {
                         MaintenanceCatalog.C02,
                         MaintenanceCatalog.C03,
                         MaintenanceCatalog.D01,
                         MaintenanceCatalog.D02
                     })
            {
                AssertStep(
                    steps,
                    childId,
                    1,
                    MaintenanceStepPurchaseState.Available,
                    MaintenanceStepVisibility.Visible);
            }

            AssertStep(steps, MaintenanceCatalog.C02, 2,
                MaintenanceStepPurchaseState.Locked, MaintenanceStepVisibility.Preview);
            AssertStep(steps, MaintenanceCatalog.C02, 3,
                MaintenanceStepPurchaseState.Locked, MaintenanceStepVisibility.Hidden);
            AssertStep(steps, MaintenanceCatalog.S01, 1,
                MaintenanceStepPurchaseState.Locked, MaintenanceStepVisibility.Preview);
            AssertStep(steps, MaintenanceCatalog.D03, 1,
                MaintenanceStepPurchaseState.Locked, MaintenanceStepVisibility.Hidden);
        }

        [Test]
        public void DirectBranch_RevealsS01AtD01Level1AndD03H01AtLevel2()
        {
            var data = SaveData.CreateNew("standard");
            data.funds = 100_000;
            var core = CreateCore(data);
            Assert.That(core.TryPurchase(MaintenanceCatalog.C01), Is.True);
            Assert.That(core.TryPurchase(MaintenanceCatalog.D01), Is.True);

            AssertStep(core.GetPurchaseStepViewData(), MaintenanceCatalog.S01, 1,
                MaintenanceStepPurchaseState.Available, MaintenanceStepVisibility.Visible);
            Assert.That(core.TryPurchase(MaintenanceCatalog.D01), Is.True);

            var levelTwoSteps = core.GetPurchaseStepViewData();
            AssertStep(levelTwoSteps, MaintenanceCatalog.D03, 1,
                MaintenanceStepPurchaseState.Available, MaintenanceStepVisibility.Visible);
            AssertStep(levelTwoSteps, MaintenanceCatalog.H01, 1,
                MaintenanceStepPurchaseState.Available, MaintenanceStepVisibility.Visible);
        }

        [Test]
        public void D02Level1_RevealsD04AsAvailable()
        {
            var data = SaveData.CreateNew("standard");
            data.funds = 100_000;
            var core = CreateCore(data);
            Assert.That(core.TryPurchase(MaintenanceCatalog.C01), Is.True);
            Assert.That(core.TryPurchase(MaintenanceCatalog.D02), Is.True);

            AssertStep(core.GetPurchaseStepViewData(), MaintenanceCatalog.D04, 1,
                MaintenanceStepPurchaseState.Available, MaintenanceStepVisibility.Visible);
        }

        [Test]
        public void AvailableStep_RemainsAvailableWhenFundsAreInsufficient()
        {
            var c01 = FindStep(
                CreateCore(SaveData.CreateNew("standard")).GetPurchaseStepViewData(),
                MaintenanceCatalog.C01,
                1);

            Assert.That(c01.PurchaseState, Is.EqualTo(MaintenanceStepPurchaseState.Available));
            Assert.That(c01.Visibility, Is.EqualTo(MaintenanceStepVisibility.Visible));
            Assert.That(c01.CanAfford, Is.False);
            Assert.That(c01.CanPurchase, Is.False);
        }

        [Test]
        public void HiddenFutureStep_CannotSkipTheExactNextLevel()
        {
            var data = SaveData.CreateNew("standard");
            data.funds = 100_000;
            var core = CreateCore(data);
            var hidden = FindStep(
                core.GetPurchaseStepViewData(),
                MaintenanceCatalog.C02,
                2);

            Assert.That(hidden.Visibility, Is.EqualTo(MaintenanceStepVisibility.Hidden));
            Assert.That(
                core.TryPurchaseDetailed(hidden.MaintenanceId, hidden.TargetLevel),
                Is.EqualTo(MaintenancePurchaseResult.Locked));
            Assert.That(core.MaintenanceLevels.Single(level => level.Id == MaintenanceCatalog.C02).Level, Is.Zero);
        }

        [Test]
        public void FullyPurchasedSave_ProjectsEveryStepPurchasedAndVisible()
        {
            var data = SaveData.CreateNew("standard");
            foreach (var definition in MaintenanceCatalog.CreateStandard())
            {
                data.maintenanceLevels.Add(new SaveMaintenanceLevel(definition.Id, definition.MaxLevel));
            }

            var steps = CreateCore(data).GetPurchaseStepViewData();

            Assert.That(steps, Has.Count.EqualTo(26));
            Assert.That(
                steps.All(step =>
                    step.PurchaseState == MaintenanceStepPurchaseState.Purchased &&
                    step.Visibility == MaintenanceStepVisibility.Visible),
                Is.True);
        }

        [Test]
        public void CatalogValidation_RejectsUndefinedRequirementsAndCycles()
        {
            var undefined = new[]
            {
                Definition("A", new MaintenanceRequirement("missing", 1))
            };
            var cyclic = new[]
            {
                Definition("A", new MaintenanceRequirement("B", 1)),
                Definition("B", new MaintenanceRequirement("A", 1))
            };

            Assert.Throws<ArgumentException>(() =>
                CreateCore(SaveData.CreateNew("undefined"), undefined));
            Assert.Throws<ArgumentException>(() =>
                CreateCore(SaveData.CreateNew("cyclic"), cyclic));
        }

        private MaintenanceCore CreateCore(
            SaveData data,
            IReadOnlyList<MaintenanceDefinition>? definitions = null)
        {
            var ledger = new ProgressionLedger(
                DestinationCatalog.CreateDemo(),
                RewardTable.CreateDefault(),
                initialFunds: data.funds);
            return new MaintenanceCore(
                definitions ?? MaintenanceCatalog.CreateStandard(),
                ledger,
                new SaveService(new SaveStore(tempDirectory), data));
        }

        private static MaintenanceDefinition Definition(
            string id,
            params MaintenanceRequirement[] requirements)
        {
            return new MaintenanceDefinition(
                id,
                id,
                MaintenanceBranch.Common,
                1,
                new long[] { 1 },
                new[] { id + " effect" },
                requirements);
        }

        private static void AssertStep(
            IReadOnlyList<MaintenancePurchaseStepViewData> steps,
            string maintenanceId,
            int targetLevel,
            MaintenanceStepPurchaseState purchaseState,
            MaintenanceStepVisibility visibility)
        {
            var step = FindStep(steps, maintenanceId, targetLevel);
            Assert.That(step.StepId, Is.EqualTo(MaintenanceStepId.Create(maintenanceId, targetLevel)));
            Assert.That(step.PurchaseState, Is.EqualTo(purchaseState));
            Assert.That(step.Visibility, Is.EqualTo(visibility));
        }

        private static MaintenancePurchaseStepViewData FindStep(
            IReadOnlyList<MaintenancePurchaseStepViewData> steps,
            string maintenanceId,
            int targetLevel)
        {
            return steps.Single(step =>
                step.MaintenanceId == maintenanceId && step.TargetLevel == targetLevel);
        }
    }
}
