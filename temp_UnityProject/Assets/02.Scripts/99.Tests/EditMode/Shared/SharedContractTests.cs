#nullable enable

using System;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Maintenance;
using Icebreaker.Shared.Progression;
using Icebreaker.Shared.State;
using NUnit.Framework;
using UnityEngine;

namespace Icebreaker.Shared.Tests
{
    public sealed class SharedContractTests
    {
        [Test]
        public void StageClock_ExposesTheAuthoritativeDamageBoundary()
        {
            IStageClock clock = new FakeStageClock(
                GamePhase.Playing,
                60d,
                59.999d,
                0.001d,
                false);

            Assert.That(clock.Phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(clock.IsPaused, Is.False);
            Assert.That(clock.StageElapsedSeconds, Is.LessThan(clock.DurationSeconds));
        }

        [Test]
        public void EventPayloads_PreserveLongDoubleAndReferencePositionValues()
        {
            const long stageId = 4_500_000_001L;
            const long iceId = 5_500_000_002L;
            const long chainId = 6_500_000_003L;
            const double elapsed = 12.3456789012345d;
            var position = new Vector2(959.5f, 539.25f);

            var started = new StageStarted(stageId, "2026-07-19T12:34:56.0000000+00:00", 60f);
            var damage = new DamageAppliedEvent(
                stageId,
                iceId,
                chainId,
                3,
                EffectType.CrackExplosion,
                4.8f,
                7.25f,
                true,
                position,
                elapsed);
            var charge = new SupportChargeChangedEvent(stageId, 11, 12);
            var destroyed = new IceDestroyedEvent(
                stageId,
                iceId,
                chainId,
                3,
                IceTier.T3,
                SpecialIceType.Crack,
                DestroyCategory.Chain,
                EffectType.CrackExplosion,
                position,
                elapsed);
            var reward = new RewardGrantedEvent(stageId, iceId, chainId, 9_000_000_004L, 7, position);
            var ended = new StageEnded(stageId, "2026-07-19T12:35:26.0000000+00:00");
            var summary = new SettlementSummary(100, 4, 2, true, "destination-2");
            var settlement = new SettlementReady(stageId, summary);

            Assert.That(started.StageId, Is.EqualTo(stageId));
            Assert.That(started.DurationSeconds, Is.EqualTo(60f));
            Assert.That(damage.IceInstanceId, Is.EqualTo(iceId));
            Assert.That(damage.ChainId, Is.EqualTo(chainId));
            Assert.That(damage.StageElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(damage.ReferencePosition, Is.EqualTo(position));
            Assert.That(charge.CurrentCharge, Is.EqualTo(11));
            Assert.That(charge.MaxCharge, Is.EqualTo(12));
            Assert.That(destroyed.StageId, Is.EqualTo(stageId));
            Assert.That(destroyed.IceInstanceId, Is.EqualTo(iceId));
            Assert.That(destroyed.ChainId, Is.EqualTo(chainId));
            Assert.That(destroyed.StageElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(destroyed.ReferencePosition, Is.EqualTo(position));
            Assert.That(reward.FundsGranted, Is.EqualTo(9_000_000_004L));
            Assert.That(reward.ReferencePosition, Is.EqualTo(position));
            Assert.That(ended.StageId, Is.EqualTo(stageId));
            Assert.That(settlement.StageId, Is.EqualTo(stageId));
            Assert.That(settlement.Summary.DestinationId, Is.EqualTo("destination-2"));
        }

        [Test]
        public void SettlementSummary_ValidatesReachedDestinationAndNullableId()
        {
            Assert.DoesNotThrow(() => new SettlementSummary(0, 0, 0, false, null));
            Assert.DoesNotThrow(() => new SettlementSummary(0, 0, 0, true, "destination-1"));
            Assert.Throws<ArgumentException>(() => new SettlementSummary(0, 0, 0, true, null));
            Assert.Throws<ArgumentException>(() => new SettlementSummary(0, 0, 0, true, " "));
        }

        [Test]
        public void RuntimeContracts_DefensivelyCopyInputArrays()
        {
            var levels = new[] { new MaintenanceLevel("common-1", 1) };
            var gameState = new GameState(
                GamePhase.Ready,
                30d,
                false,
                100,
                "destination-1",
                2,
                10,
                levels,
                false,
                true);

            var costs = new long[] { 10, 20 };
            var effects = new[] { "+1", "+2" };
            var requirements = new[] { new MaintenanceRequirement("common-0", 1) };
            var maintenance = new MaintenanceDefinition(
                "direct-1",
                "직접 공격",
                MaintenanceBranch.Direct,
                2,
                costs,
                effects,
                requirements);

            var definitions = CreateIceDefinitions();
            var weights = new[]
            {
                new IceSpawnWeight(IceTier.T1, 100),
                new IceSpawnWeight(IceTier.T2, 0),
                new IceSpawnWeight(IceTier.T3, 0)
            };
            var specials = CreateSpecialDefinitions();
            var combat = CreateBaselineCombatConfig(definitions, weights, specials);

            levels[0] = new MaintenanceLevel("changed", 9);
            costs[0] = 999;
            effects[0] = "changed";
            requirements[0] = new MaintenanceRequirement("changed", 9);
            definitions[0] = new IceDefinition(IceTier.T1, "changed", 999f, 999);
            weights[0] = new IceSpawnWeight(IceTier.T1, 1);
            specials[0] = new SpecialIceDefinition(SpecialIceType.Crystal, 0.9f, IceTier.T3, 9f, 9f);

            Assert.That(gameState.MaintenanceLevels[0].Id, Is.EqualTo("common-1"));
            Assert.That(maintenance.CostsByLevel[0], Is.EqualTo(10));
            Assert.That(maintenance.EffectTextsByLevel[0], Is.EqualTo("+1"));
            Assert.That(maintenance.Requirements[0].NodeId, Is.EqualTo("common-0"));
            Assert.That(combat.IceField.IceDefinitions[0].DisplayName, Is.EqualTo("백빙"));
            Assert.That(combat.IceField.SpawnWeights[0].Weight, Is.EqualTo(100));
            Assert.That(combat.IceField.SpecialDefinitions[0].SpawnChance, Is.EqualTo(0.025f));
        }

        [Test]
        public void MaintenanceDefinition_RejectsInvalidLevelDataAndRequirements()
        {
            Assert.Throws<ArgumentException>(() => new MaintenanceDefinition(
                "node",
                "노드",
                MaintenanceBranch.Common,
                2,
                new long[] { 10 },
                new[] { "+1", "+2" },
                Array.Empty<MaintenanceRequirement>()));

            Assert.Throws<ArgumentOutOfRangeException>(() => new MaintenanceDefinition(
                "node",
                "노드",
                MaintenanceBranch.Common,
                1,
                new long[] { -1 },
                new[] { "+1" },
                Array.Empty<MaintenanceRequirement>()));

            Assert.Throws<ArgumentOutOfRangeException>(() => new MaintenanceRequirement("previous", 0));
        }

        [Test]
        public void MaintenanceNodeViewData_PreservesCalculatedPurchaseBoundary()
        {
            var missing = new[] { "common-1", "support-1" };
            var locked = new MaintenanceNodeViewData(
                id: "support-3",
                displayName: "특수빙 우선",
                branch: MaintenanceBranch.Support,
                currentLevel: 0,
                maxLevel: 1,
                state: MaintenanceNodeState.Locked,
                currentEffectText: "미보유",
                nextEffectText: "특수빙 피해 x2",
                nextCost: 300,
                isMaxLevel: false,
                canAffordNextLevel: true,
                canPurchaseNextLevel: false,
                missingRequirementIds: missing);

            missing[0] = "changed";

            Assert.That(locked.State, Is.EqualTo(MaintenanceNodeState.Locked));
            Assert.That(locked.CanAffordNextLevel, Is.True);
            Assert.That(locked.CanPurchaseNextLevel, Is.False);
            Assert.That(locked.MissingRequirementIds[0], Is.EqualTo("common-1"));

            Assert.Throws<ArgumentException>(() => new MaintenanceNodeViewData(
                id: "direct-1",
                displayName: "직접 공격",
                branch: MaintenanceBranch.Direct,
                currentLevel: 0,
                maxLevel: 1,
                state: MaintenanceNodeState.Available,
                currentEffectText: "미보유",
                nextEffectText: "직접 피해 x1.6",
                nextCost: 300,
                isMaxLevel: false,
                canAffordNextLevel: true,
                canPurchaseNextLevel: false,
                missingRequirementIds: Array.Empty<string>()));

            Assert.DoesNotThrow(() => new MaintenanceNodeViewData(
                id: "direct-1",
                displayName: "직접 공격",
                branch: MaintenanceBranch.Direct,
                currentLevel: 0,
                maxLevel: 1,
                state: MaintenanceNodeState.Available,
                currentEffectText: "미보유",
                nextEffectText: "직접 피해 x1.6",
                nextCost: 300,
                isMaxLevel: false,
                canAffordNextLevel: true,
                canPurchaseNextLevel: true,
                missingRequirementIds: Array.Empty<string>()));
        }

        [Test]
        public void IceFieldConfig_RejectsDuplicateOrEmptyWeightsAndInvalidProbability()
        {
            var definitions = CreateIceDefinitions();
            var specials = CreateSpecialDefinitions();

            Assert.Throws<ArgumentException>(() => new IceFieldConfig(
                20,
                2,
                56f,
                120f,
                0.25f,
                definitions,
                new[]
                {
                    new IceSpawnWeight(IceTier.T1, 50),
                    new IceSpawnWeight(IceTier.T1, 50)
                },
                specials));

            Assert.Throws<ArgumentException>(() => new IceFieldConfig(
                20,
                2,
                56f,
                120f,
                0.25f,
                definitions,
                Array.Empty<IceSpawnWeight>(),
                specials));

            Assert.Throws<ArgumentOutOfRangeException>(() => new SpecialIceDefinition(
                SpecialIceType.Crystal,
                1.01f,
                IceTier.T1,
                2f,
                2f));
        }

        [Test]
        public void CombatConfigs_PreserveBaselineAndFullyUpgradedValues()
        {
            var baseline = CreateBaselineCombatConfig(
                CreateIceDefinitions(),
                CreateSpawnWeights(100, 0, 0),
                CreateSpecialDefinitions());
            var fullyUpgraded = CreateFullyUpgradedCombatConfig(
                CreateIceDefinitions(),
                CreateSpawnWeights(20, 45, 35),
                CreateSpecialDefinitions());

            Assert.That(baseline.DirectAttack.CurrentClickDamage, Is.EqualTo(1f));
            Assert.That(baseline.DirectAttack.HoldAttacksPerSecond, Is.EqualTo(5f));
            Assert.That(baseline.SupportAttack.Enabled, Is.False);
            Assert.That(baseline.SupportAttack.AdditionalTargetCount, Is.Zero);
            Assert.That(baseline.ChainEffect.OverkillEnabled, Is.False);
            Assert.That(baseline.ChainEffect.HullFragmentDamageMultiplier, Is.Zero);
            Assert.That(baseline.ChainEffect.IceCollapseEnabled, Is.False);
            Assert.That(fullyUpgraded.DirectAttack.CurrentClickDamage, Is.EqualTo(4.096f).Within(0.0001f));
            Assert.That(fullyUpgraded.DirectAttack.HoldAttacksPerSecond, Is.EqualTo(11f));
            Assert.That(fullyUpgraded.DirectAttack.CriticalChance, Is.EqualTo(0.05f));
            Assert.That(fullyUpgraded.DirectAttack.CriticalDamageMultiplier, Is.EqualTo(3f));
            Assert.That(fullyUpgraded.IceField.MaxActiveIceCount, Is.EqualTo(20));
            Assert.That(fullyUpgraded.IceField.MaxSpecialIceCount, Is.EqualTo(2));
            Assert.That(fullyUpgraded.IceField.HitRadiusReferencePixels, Is.EqualTo(56f));
            Assert.That(fullyUpgraded.IceField.MinimumSpawnDistanceReferencePixels, Is.EqualTo(120f));
            Assert.That(fullyUpgraded.IceField.RespawnProtectionSeconds, Is.EqualTo(0.25f));
            Assert.That(fullyUpgraded.SupportAttack.RequiredDirectHitCount, Is.EqualTo(12));
            Assert.That(fullyUpgraded.SupportAttack.PrimaryDamageMultiplier, Is.EqualTo(1f));
            Assert.That(fullyUpgraded.SupportAttack.AdditionalTargetCount, Is.EqualTo(2));
            Assert.That(fullyUpgraded.SupportAttack.AdditionalDamageMultiplier, Is.EqualTo(0.7f));
            Assert.That(fullyUpgraded.SupportAttack.SpecialIceDamageMultiplier, Is.EqualTo(2f));
            Assert.That(fullyUpgraded.ChainEffect.OverkillTransferMultiplier, Is.EqualTo(0.5f));
            Assert.That(fullyUpgraded.ChainEffect.HullFragmentDamageMultiplier, Is.EqualTo(0.75f));
            Assert.That(fullyUpgraded.ChainEffect.HullFragmentRadiusReferencePixels, Is.EqualTo(90f));
            Assert.That(baseline.ChainEffect.CrystalShardCount, Is.EqualTo(5));
            Assert.That(fullyUpgraded.ChainEffect.CrystalShardCount, Is.EqualTo(8));
            Assert.That(fullyUpgraded.ChainEffect.CrackDamageMultiplier, Is.EqualTo(4.8f));
            Assert.That(fullyUpgraded.ChainEffect.CrackRadiusReferencePixels, Is.EqualTo(192f));
            Assert.That(fullyUpgraded.ChainEffect.IceCollapseRequiredDestroyCount, Is.EqualTo(5));
            Assert.That(fullyUpgraded.ChainEffect.IceCollapseDamageMultiplier, Is.EqualTo(1.5f));
            Assert.That(fullyUpgraded.ChainEffect.IceCollapseRadiusReferencePixels, Is.EqualTo(140f));
            Assert.That(fullyUpgraded.ChainEffect.MaxChainDepth, Is.EqualTo(3));

            Assert.That(fullyUpgraded.IceField.IceDefinitions[0].MaxHp, Is.EqualTo(10f));
            Assert.That(fullyUpgraded.IceField.IceDefinitions[0].BaseFunds, Is.EqualTo(10));
            Assert.That(fullyUpgraded.IceField.IceDefinitions[1].MaxHp, Is.EqualTo(60f));
            Assert.That(fullyUpgraded.IceField.IceDefinitions[1].BaseFunds, Is.EqualTo(80));
            Assert.That(fullyUpgraded.IceField.IceDefinitions[2].MaxHp, Is.EqualTo(360f));
            Assert.That(fullyUpgraded.IceField.IceDefinitions[2].BaseFunds, Is.EqualTo(700));
            Assert.That(fullyUpgraded.IceField.SpecialDefinitions[0].SpawnChance, Is.EqualTo(0.025f));
            Assert.That(fullyUpgraded.IceField.SpecialDefinitions[0].MinimumTier, Is.EqualTo(IceTier.T2));
            Assert.That(fullyUpgraded.IceField.SpecialDefinitions[0].HpMultiplier, Is.EqualTo(1f));
            Assert.That(fullyUpgraded.IceField.SpecialDefinitions[0].FundsMultiplier, Is.EqualTo(4f));
            Assert.That(fullyUpgraded.IceField.SpecialDefinitions[1].SpawnChance, Is.EqualTo(0.02f));
            Assert.That(fullyUpgraded.IceField.SpecialDefinitions[1].MinimumTier, Is.EqualTo(IceTier.T1));
            Assert.That(fullyUpgraded.IceField.SpecialDefinitions[1].HpMultiplier, Is.EqualTo(0.6f));
            Assert.That(fullyUpgraded.IceField.SpecialDefinitions[1].FundsMultiplier, Is.EqualTo(1f));
        }

        [TestCase(1f, 5f)]
        [TestCase(1.6f, 7f)]
        [TestCase(2.56f, 9f)]
        [TestCase(4.096f, 11f)]
        public void DirectAttackSnapshots_PreservePlannedProgression(float clickDamage, float holdRate)
        {
            var config = new DirectAttackConfig(clickDamage, holdRate, 0.05f, 3f);

            Assert.That(config.CurrentClickDamage, Is.EqualTo(clickDamage));
            Assert.That(config.HoldAttacksPerSecond, Is.EqualTo(holdRate));
        }

        [TestCase(0.25f)]
        [TestCase(0.5f)]
        [TestCase(0.75f)]
        public void HullFragmentSnapshots_PreservePlannedProgression(float hullMultiplier)
        {
            var config = CreateChainEffectConfig(hullMultiplier, 5, 3f, 120f);

            Assert.That(config.HullFragmentDamageMultiplier, Is.EqualTo(hullMultiplier));
        }

        [TestCase(5, 3f, 120f)]
        [TestCase(7, 3.9f, 156f)]
        [TestCase(8, 4.8f, 192f)]
        public void SpecialIceSnapshots_PreservePlannedProgression(
            int shardCount,
            float crackMultiplier,
            float crackRadius)
        {
            var config = CreateChainEffectConfig(0f, shardCount, crackMultiplier, crackRadius);

            Assert.That(config.CrystalShardCount, Is.EqualTo(shardCount));
            Assert.That(config.CrackDamageMultiplier, Is.EqualTo(crackMultiplier));
            Assert.That(config.CrackRadiusReferencePixels, Is.EqualTo(crackRadius));
        }

        private static IceDefinition[] CreateIceDefinitions()
        {
            return new[]
            {
                new IceDefinition(IceTier.T1, "백빙", 10f, 10),
                new IceDefinition(IceTier.T2, "청빙", 60f, 80),
                new IceDefinition(IceTier.T3, "심빙", 360f, 700)
            };
        }

        private static SpecialIceDefinition[] CreateSpecialDefinitions()
        {
            return new[]
            {
                new SpecialIceDefinition(SpecialIceType.Crystal, 0.025f, IceTier.T2, 1f, 4f),
                new SpecialIceDefinition(SpecialIceType.Crack, 0.02f, IceTier.T1, 0.6f, 1f)
            };
        }

        private static IceSpawnWeight[] CreateSpawnWeights(int t1, int t2, int t3)
        {
            return new[]
            {
                new IceSpawnWeight(IceTier.T1, t1),
                new IceSpawnWeight(IceTier.T2, t2),
                new IceSpawnWeight(IceTier.T3, t3)
            };
        }

        private static CombatConfig CreateBaselineCombatConfig(
            IceDefinition[] definitions,
            IceSpawnWeight[] weights,
            SpecialIceDefinition[] specials)
        {
            return new CombatConfig(
                directAttack: new DirectAttackConfig(1f, 5f, 0.05f, 3f),
                iceField: new IceFieldConfig(20, 2, 56f, 120f, 0.25f, definitions, weights, specials),
                supportAttack: new SupportAttackConfig(false, 12, 1f, 0, 0.7f, false, 2f),
                chainEffect: new ChainEffectConfig(
                    overkillEnabled: false,
                    overkillTransferMultiplier: 0.5f,
                    hullFragmentDamageMultiplier: 0f,
                    hullFragmentRadiusReferencePixels: 90f,
                    crystalShardCount: 5,
                    crackDamageMultiplier: 3f,
                    crackRadiusReferencePixels: 120f,
                    iceCollapseEnabled: false,
                    iceCollapseRequiredDestroyCount: 5,
                    iceCollapseDamageMultiplier: 1.5f,
                    iceCollapseRadiusReferencePixels: 140f,
                    maxChainDepth: 3));
        }

        private static CombatConfig CreateFullyUpgradedCombatConfig(
            IceDefinition[] definitions,
            IceSpawnWeight[] weights,
            SpecialIceDefinition[] specials)
        {
            return new CombatConfig(
                directAttack: new DirectAttackConfig(4.096f, 11f, 0.05f, 3f),
                iceField: new IceFieldConfig(20, 2, 56f, 120f, 0.25f, definitions, weights, specials),
                supportAttack: new SupportAttackConfig(true, 12, 1f, 2, 0.7f, true, 2f),
                chainEffect: new ChainEffectConfig(
                    overkillEnabled: true,
                    overkillTransferMultiplier: 0.5f,
                    hullFragmentDamageMultiplier: 0.75f,
                    hullFragmentRadiusReferencePixels: 90f,
                    crystalShardCount: 8,
                    crackDamageMultiplier: 4.8f,
                    crackRadiusReferencePixels: 192f,
                    iceCollapseEnabled: true,
                    iceCollapseRequiredDestroyCount: 5,
                    iceCollapseDamageMultiplier: 1.5f,
                    iceCollapseRadiusReferencePixels: 140f,
                    maxChainDepth: 3));
        }

        private static ChainEffectConfig CreateChainEffectConfig(
            float hullMultiplier,
            int shardCount,
            float crackMultiplier,
            float crackRadius)
        {
            return new ChainEffectConfig(
                overkillEnabled: false,
                overkillTransferMultiplier: 0.5f,
                hullFragmentDamageMultiplier: hullMultiplier,
                hullFragmentRadiusReferencePixels: 90f,
                crystalShardCount: shardCount,
                crackDamageMultiplier: crackMultiplier,
                crackRadiusReferencePixels: crackRadius,
                iceCollapseEnabled: false,
                iceCollapseRequiredDestroyCount: 5,
                iceCollapseDamageMultiplier: 1.5f,
                iceCollapseRadiusReferencePixels: 140f,
                maxChainDepth: 3);
        }

        private sealed class FakeStageClock : IStageClock
        {
            public FakeStageClock(
                GamePhase phase,
                double durationSeconds,
                double stageElapsedSeconds,
                double remainingSeconds,
                bool isPaused)
            {
                Phase = phase;
                DurationSeconds = durationSeconds;
                StageElapsedSeconds = stageElapsedSeconds;
                RemainingSeconds = remainingSeconds;
                IsPaused = isPaused;
            }

            public GamePhase Phase { get; }

            public double DurationSeconds { get; }

            public double StageElapsedSeconds { get; }

            public double RemainingSeconds { get; }

            public bool IsPaused { get; }
        }
    }
}
