#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.State;
using NUnit.Framework;

namespace Icebreaker.Core.Tests
{
    public sealed class CombatConfigFactoryTests
    {
        [Test]
        public void Build_BaselineMatchesSharedContractReference()
        {
            var config = CombatConfigFactory.Build(Array.Empty<MaintenanceLevel>());

            Assert.That(config.DirectAttack.CurrentClickDamage, Is.EqualTo(1f));
            Assert.That(config.DirectAttack.HoldAttacksPerSecond, Is.EqualTo(5f));
            Assert.That(config.DirectAttack.CursorRadiusReferencePixels, Is.EqualTo(56f));
            Assert.That(config.DirectAttack.CriticalChance, Is.EqualTo(0.05f));
            Assert.That(config.DirectAttack.CriticalDamageMultiplier, Is.EqualTo(3f));
            Assert.That(config.SupportAttack.Enabled, Is.False);
            Assert.That(config.SupportAttack.RequiredDirectHitCount, Is.EqualTo(12));
            Assert.That(config.SupportAttack.PrimaryDamageMultiplier, Is.EqualTo(1f));
            Assert.That(config.SupportAttack.AdditionalTargetCount, Is.Zero);
            Assert.That(config.SupportAttack.AdditionalDamageMultiplier, Is.EqualTo(0.7f));
            Assert.That(config.SupportAttack.PrioritizeSpecialIce, Is.False);
            Assert.That(config.SupportAttack.SpecialIceDamageMultiplier, Is.EqualTo(2f));
            Assert.That(config.ChainEffect.OverkillEnabled, Is.False);
            Assert.That(config.ChainEffect.OverkillTransferMultiplier, Is.EqualTo(0.5f));
            Assert.That(config.ChainEffect.HullFragmentDamageMultiplier, Is.Zero);
            Assert.That(config.ChainEffect.HullFragmentRadiusReferencePixels, Is.EqualTo(90f));
            Assert.That(config.ChainEffect.CrystalShardCount, Is.EqualTo(5));
            Assert.That(config.ChainEffect.CrackDamageMultiplier, Is.EqualTo(3f));
            Assert.That(config.ChainEffect.CrackRadiusReferencePixels, Is.EqualTo(120f));
            Assert.That(config.ChainEffect.IceCollapseEnabled, Is.False);
            Assert.That(config.ChainEffect.IceCollapseRequiredDestroyCount, Is.EqualTo(5));
            Assert.That(config.ChainEffect.IceCollapseDamageMultiplier, Is.EqualTo(1.5f));
            Assert.That(config.ChainEffect.IceCollapseRadiusReferencePixels, Is.EqualTo(140f));
            Assert.That(config.ChainEffect.MaxChainDepth, Is.EqualTo(3));
            AssertIceFieldConstants(config.IceField);
            Assert.That(config.IceField.IceDefinitions, Has.Count.EqualTo(1));
            AssertIceDefinition(config.IceField.IceDefinitions[0], IceTier.T1, "백빙", 10f, 10);
            AssertSpawnWeights(config.IceField.SpawnWeights, 100);
            AssertSpecialDefinitions(config.IceField.SpecialDefinitions);
        }

        [Test]
        public void Build_IceUnlocksSelectDefinitionsAndSpawnWeights()
        {
            var t2Config = CombatConfigFactory.Build(new[]
            {
                new MaintenanceLevel(MaintenanceCatalog.C03, 1)
            });
            var t3Config = CombatConfigFactory.Build(new[]
            {
                new MaintenanceLevel(MaintenanceCatalog.C03, 1),
                new MaintenanceLevel(MaintenanceCatalog.C04, 1)
            });

            Assert.That(t2Config.IceField.IceDefinitions, Has.Count.EqualTo(2));
            AssertIceDefinition(t2Config.IceField.IceDefinitions[1], IceTier.T2, "청빙", 60f, 80);
            AssertSpawnWeights(t2Config.IceField.SpawnWeights, 60, 40);
            Assert.That(t3Config.IceField.IceDefinitions, Has.Count.EqualTo(3));
            AssertIceDefinition(t3Config.IceField.IceDefinitions[2], IceTier.T3, "심빙", 360f, 700);
            AssertSpawnWeights(t3Config.IceField.SpawnWeights, 20, 45, 35);
        }

        [Test]
        public void Build_FullyUpgradedMatchesSharedContractReference()
        {
            var config = CombatConfigFactory.Build(CreateFullyUpgradedLevels());

            Assert.That(config.DirectAttack.CurrentClickDamage, Is.EqualTo(7f));
            Assert.That(config.DirectAttack.HoldAttacksPerSecond, Is.EqualTo(8.75f));
            Assert.That(config.DirectAttack.CursorRadiusReferencePixels, Is.EqualTo(104f));
            Assert.That(config.DirectAttack.CriticalChance, Is.EqualTo(0.05f));
            Assert.That(config.DirectAttack.CriticalDamageMultiplier, Is.EqualTo(3f));
            Assert.That(config.SupportAttack.Enabled, Is.True);
            Assert.That(config.SupportAttack.RequiredDirectHitCount, Is.EqualTo(12));
            Assert.That(config.SupportAttack.PrimaryDamageMultiplier, Is.EqualTo(1f));
            Assert.That(config.SupportAttack.AdditionalTargetCount, Is.EqualTo(2));
            Assert.That(config.SupportAttack.AdditionalDamageMultiplier, Is.EqualTo(0.7f));
            Assert.That(config.SupportAttack.PrioritizeSpecialIce, Is.True);
            Assert.That(config.SupportAttack.SpecialIceDamageMultiplier, Is.EqualTo(2f));
            Assert.That(config.ChainEffect.OverkillEnabled, Is.True);
            Assert.That(config.ChainEffect.OverkillTransferMultiplier, Is.EqualTo(0.5f));
            Assert.That(config.ChainEffect.HullFragmentDamageMultiplier, Is.EqualTo(0.75f));
            Assert.That(config.ChainEffect.HullFragmentRadiusReferencePixels, Is.EqualTo(90f));
            Assert.That(config.ChainEffect.CrystalShardCount, Is.EqualTo(8));
            Assert.That(config.ChainEffect.CrackDamageMultiplier, Is.EqualTo(4.8f));
            Assert.That(config.ChainEffect.CrackRadiusReferencePixels, Is.EqualTo(192f));
            Assert.That(config.ChainEffect.IceCollapseEnabled, Is.True);
            Assert.That(config.ChainEffect.IceCollapseRequiredDestroyCount, Is.EqualTo(5));
            Assert.That(config.ChainEffect.IceCollapseDamageMultiplier, Is.EqualTo(1.5f));
            Assert.That(config.ChainEffect.IceCollapseRadiusReferencePixels, Is.EqualTo(140f));
            Assert.That(config.ChainEffect.MaxChainDepth, Is.EqualTo(3));
            AssertIceFieldConstants(config.IceField);
            Assert.That(config.IceField.IceDefinitions, Has.Count.EqualTo(3));
            AssertSpawnWeights(config.IceField.SpawnWeights, 20, 45, 35);
            AssertSpecialDefinitions(config.IceField.SpecialDefinitions);
        }

        [Test]
        public void Build_H02LevelOneRoundsCrystalShardCountUp()
        {
            var config = CombatConfigFactory.Build(new[]
            {
                new MaintenanceLevel(MaintenanceCatalog.H02, 1)
            });

            Assert.That(config.ChainEffect.CrystalShardCount, Is.EqualTo(7));
            Assert.That(config.ChainEffect.CrackDamageMultiplier, Is.EqualTo(3.9f).Within(0.0001f));
            Assert.That(config.ChainEffect.CrackRadiusReferencePixels, Is.EqualTo(156f).Within(0.01f));
        }

        [TestCase(0, 1f, 5f)]
        [TestCase(1, 3f, 6.25f)]
        [TestCase(2, 5f, 7.5f)]
        [TestCase(3, 7f, 8.75f)]
        public void Build_DirectProgressionUsesReferenceStyleSteps(
            int level,
            float expectedDamage,
            float expectedTicksPerSecond)
        {
            var levels = level == 0
                ? Array.Empty<MaintenanceLevel>()
                : new[]
                {
                    new MaintenanceLevel(MaintenanceCatalog.D01, level),
                    new MaintenanceLevel(MaintenanceCatalog.D02, level)
                };

            var config = CombatConfigFactory.Build(levels);

            Assert.That(config.DirectAttack.CurrentDirectDamage, Is.EqualTo(expectedDamage));
            Assert.That(config.DirectAttack.AttackTicksPerSecond, Is.EqualTo(expectedTicksPerSecond));
        }

        [TestCase(0, 56f)]
        [TestCase(1, 72f)]
        [TestCase(2, 88f)]
        [TestCase(3, 104f)]
        public void Build_D04ExpandsCursorRadiusBySixteenPixelsPerLevel(
            int d04Level,
            float expectedRadius)
        {
            var levels = d04Level == 0
                ? Array.Empty<MaintenanceLevel>()
                : new[] { new MaintenanceLevel(MaintenanceCatalog.D04, d04Level) };

            var config = CombatConfigFactory.Build(levels);

            Assert.That(config.DirectAttack.CursorRadiusReferencePixels, Is.EqualTo(expectedRadius));
        }

        [Test]
        public void MaintenanceEfficiencyLevel_FeedsRewardTableFormula()
        {
            var levels = new[]
            {
                new MaintenanceLevel(MaintenanceCatalog.C02, 3)
            };

            var efficiencyLevel = CombatConfigFactory.GetMaintenanceEfficiencyLevel(levels);
            var funds = RewardTable.CreateDefault().ComputeFunds(
                IceTier.T2,
                SpecialIceType.Crystal,
                efficiencyLevel);

            Assert.That(efficiencyLevel, Is.EqualTo(3));
            Assert.That(funds, Is.EqualTo(416));
        }

        private static MaintenanceLevel[] CreateFullyUpgradedLevels()
        {
            return new[]
            {
                new MaintenanceLevel(MaintenanceCatalog.C01, 1),
                new MaintenanceLevel(MaintenanceCatalog.C02, 3),
                new MaintenanceLevel(MaintenanceCatalog.C03, 1),
                new MaintenanceLevel(MaintenanceCatalog.C04, 1),
                new MaintenanceLevel(MaintenanceCatalog.D01, 3),
                new MaintenanceLevel(MaintenanceCatalog.D02, 3),
                new MaintenanceLevel(MaintenanceCatalog.D03, 1),
                new MaintenanceLevel(MaintenanceCatalog.D04, 3),
                new MaintenanceLevel(MaintenanceCatalog.S01, 1),
                new MaintenanceLevel(MaintenanceCatalog.S02, 2),
                new MaintenanceLevel(MaintenanceCatalog.S03, 1),
                new MaintenanceLevel(MaintenanceCatalog.H01, 3),
                new MaintenanceLevel(MaintenanceCatalog.H02, 2),
                new MaintenanceLevel(MaintenanceCatalog.H03, 1)
            };
        }

        private static void AssertIceFieldConstants(IceFieldConfig iceField)
        {
            Assert.That(iceField.MaxActiveIceCount, Is.EqualTo(20));
            Assert.That(iceField.MaxSpecialIceCount, Is.EqualTo(2));
            Assert.That(iceField.HitRadiusReferencePixels, Is.EqualTo(56f));
            Assert.That(iceField.MinimumSpawnDistanceReferencePixels, Is.EqualTo(120f));
            Assert.That(iceField.RespawnProtectionSeconds, Is.EqualTo(0.25f));
        }

        private static void AssertIceDefinition(
            IceDefinition definition,
            IceTier tier,
            string displayName,
            float maxHp,
            long baseFunds)
        {
            Assert.That(definition.Tier, Is.EqualTo(tier));
            Assert.That(definition.DisplayName, Is.EqualTo(displayName));
            Assert.That(definition.MaxHp, Is.EqualTo(maxHp));
            Assert.That(definition.BaseFunds, Is.EqualTo(baseFunds));
        }

        private static void AssertSpawnWeights(
            IReadOnlyList<IceSpawnWeight> weights,
            params int[] expectedWeights)
        {
            Assert.That(weights, Has.Count.EqualTo(expectedWeights.Length));
            for (var index = 0; index < expectedWeights.Length; index++)
            {
                Assert.That(weights[index].Tier, Is.EqualTo((IceTier)index));
                Assert.That(weights[index].Weight, Is.EqualTo(expectedWeights[index]));
            }
        }

        private static void AssertSpecialDefinitions(
            IReadOnlyList<SpecialIceDefinition> definitions)
        {
            Assert.That(definitions, Has.Count.EqualTo(2));
            Assert.That(definitions[0].Type, Is.EqualTo(SpecialIceType.Crystal));
            Assert.That(definitions[0].SpawnChance, Is.EqualTo(0.025f));
            Assert.That(definitions[0].MinimumTier, Is.EqualTo(IceTier.T2));
            Assert.That(definitions[0].HpMultiplier, Is.EqualTo(1f));
            Assert.That(definitions[0].FundsMultiplier, Is.EqualTo(4f));
            Assert.That(definitions[1].Type, Is.EqualTo(SpecialIceType.Crack));
            Assert.That(definitions[1].SpawnChance, Is.EqualTo(0.02f));
            Assert.That(definitions[1].MinimumTier, Is.EqualTo(IceTier.T1));
            Assert.That(definitions[1].HpMultiplier, Is.EqualTo(0.6f));
            Assert.That(definitions[1].FundsMultiplier, Is.EqualTo(1f));
        }
    }
}
