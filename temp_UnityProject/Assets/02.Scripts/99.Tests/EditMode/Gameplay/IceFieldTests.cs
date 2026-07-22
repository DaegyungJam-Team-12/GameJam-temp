#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.State;
using NUnit.Framework;
using UnityEngine;

namespace Icebreaker.Gameplay.Tests
{
    public sealed class IceFieldTests
    {
        private IceField field = null!;
        private IceFieldConfig config = null!;
        private int destroyedCount;
        private int respawnedCount;
        private IceDestroyedEvent lastDestroyed;
        private DamageAppliedEvent lastDamage;

        private sealed class MockClock : IStageClock
        {
            public GamePhase Phase { get; set; } = GamePhase.Playing;
            public double DurationSeconds { get; set; } = 60d;
            public double StageElapsedSeconds { get; set; } = 0d;
            public double RemainingSeconds => Math.Max(0d, DurationSeconds - StageElapsedSeconds);
            public bool IsPaused { get; set; } = false;
        }

        private MockClock clock = null!;

        [SetUp]
        public void SetUp()
        {
            config = new IceFieldConfig(
                maxActiveIceCount: 20,
                maxSpecialIceCount: 2,
                hitRadiusReferencePixels: 56f,
                minimumSpawnDistanceReferencePixels: 120f,
                respawnProtectionSeconds: 0.25f,
                iceDefinitions: new[] { new IceDefinition(IceTier.T1, "백빙", 10f, 10L) },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());

            var idGenerator = new IceIdGenerator();
            var spawnBounds = new Rect(56f, 56f, 848f, 428f);
            var positioner = new IceSpawnPositioner(spawnBounds, config.MinimumSpawnDistanceReferencePixels);

            clock = new MockClock();
            field = new IceField(1L, config, idGenerator, positioner, clock);
            field.DamageApplied += e => { lastDamage = e; };
            field.IceDestroyed += e => { destroyedCount++; lastDestroyed = e; };
            field.IceRespawned += _ => respawnedCount++;

            destroyedCount = 0;
            respawnedCount = 0;

            field.Initialize(0d);
        }

        [Test]
        public void Initialize_Creates20IceInstances()
        {
            Assert.That(field.ActiveIce.Count, Is.EqualTo(20));

            foreach (var ice in field.ActiveIce)
            {
                Assert.That(ice.IsDestroyed, Is.False);
                Assert.That(ice.RemainingHp, Is.EqualTo(10f));
                Assert.That(ice.Tier, Is.EqualTo(IceTier.T1));
            }
        }

        [Test]
        public void AllIceInstanceIds_AreUnique()
        {
            var ids = new HashSet<long>();
            foreach (var ice in field.ActiveIce)
            {
                Assert.That(ids.Add(ice.IceInstanceId), Is.True,
                    $"Duplicate iceInstanceId {ice.IceInstanceId}");
            }
        }

        [Test]
        public void DestroyOneIce_CountRemainsAt20()
        {
            var target = field.ActiveIce[0];
            var pos = target.ReferencePosition;

            // Apply 10 clicks (T1 HP = 10, damage = 1).
            for (var i = 0; i < 10; i++)
            {
                field.ApplyClickAt(pos, 1f, EffectType.Click, i * 0.1d);
            }

            Assert.That(destroyedCount, Is.EqualTo(1));
            Assert.That(respawnedCount, Is.EqualTo(1));

            // All 20 should be alive again (destroyed one was respawned).
            var aliveCount = 0;
            foreach (var ice in field.ActiveIce)
            {
                if (!ice.IsDestroyed) aliveCount++;
            }

            Assert.That(aliveCount, Is.EqualTo(20));
        }

        [Test]
        public void RespawnedIce_HasNewUniqueId()
        {
            var target = field.ActiveIce[0];
            var oldId = target.IceInstanceId;
            var pos = target.ReferencePosition;

            for (var i = 0; i < 10; i++)
            {
                field.ApplyClickAt(pos, 1f, EffectType.Click, i * 0.1d);
            }

            // The same IceInstance object is reused but with a new ID.
            Assert.That(target.IceInstanceId, Is.Not.EqualTo(oldId));

            // And no duplicates across the entire field.
            var ids = new HashSet<long>();
            foreach (var ice in field.ActiveIce)
            {
                Assert.That(ids.Add(ice.IceInstanceId), Is.True);
            }
        }

        [Test]
        public void ClickOnEmptySpace_ReturnsFalse()
        {
            // Click far away from any ice.
            var result = field.ApplyClickAt(new Vector2(-9999f, -9999f), 1f, EffectType.Click, 0d);
            Assert.That(result, Is.False);
        }

        [Test]
        public void IceDestroyedEvent_PublishedExactlyOnce()
        {
            var target = field.ActiveIce[0];
            var pos = target.ReferencePosition;

            for (var i = 0; i < 10; i++)
            {
                field.ApplyClickAt(pos, 1f, EffectType.Click, i * 0.1d);
            }

            Assert.That(destroyedCount, Is.EqualTo(1));
            Assert.That(lastDestroyed.Tier, Is.EqualTo(IceTier.T1));
            Assert.That(lastDestroyed.DestroyCategory, Is.EqualTo(DestroyCategory.Direct));
        }

        // --- GP-03 Tests ---

        [Test]
        public void CriticalStrike_Always_MultipliesDamage()
        {
            // 100% critical chance for deterministic testing.
            var crit = new CriticalStrike(1.0f, 3.0f);
            var finalDamage = crit.Apply(10f, out var wasCritical);

            Assert.That(wasCritical, Is.True);
            Assert.That(finalDamage, Is.EqualTo(30f));
        }

        [Test]
        public void CriticalStrike_Never_ReturnsBaseDamage()
        {
            // 0% critical chance.
            var crit = new CriticalStrike(0f, 3.0f);
            var finalDamage = crit.Apply(10f, out var wasCritical);

            Assert.That(wasCritical, Is.False);
            Assert.That(finalDamage, Is.EqualTo(10f));
        }

        [Test]
        public void HoldInput_FirstPress_ReturnsOneTick()
        {
            var handler = new HoldInputHandler(5f);
            var ticks = handler.Update(isPressed: true, wasPressedThisFrame: true, deltaTime: 0.016f);

            Assert.That(ticks, Is.EqualTo(1), "First press should fire exactly 1 tick.");
        }

        [Test]
        public void HoldInput_HoldLongEnough_FiresMultipleTicks()
        {
            var handler = new HoldInputHandler(5f); // interval = 0.2s

            // First press.
            handler.Update(isPressed: true, wasPressedThisFrame: true, deltaTime: 0.016f);

            // Simulate holding for 0.5s (should fire 2 ticks at 5/s = 0.2s interval).
            var ticks = handler.Update(isPressed: true, wasPressedThisFrame: false, deltaTime: 0.5f);

            Assert.That(ticks, Is.GreaterThanOrEqualTo(2),
                "Holding for 0.5s at 5/s should fire at least 2 ticks.");
        }

        [Test]
        public void HoldInput_Released_ReturnsZero()
        {
            var handler = new HoldInputHandler(5f);
            handler.Update(isPressed: true, wasPressedThisFrame: true, deltaTime: 0.016f);
            var ticks = handler.Update(isPressed: false, wasPressedThisFrame: false, deltaTime: 0.5f);

            Assert.That(ticks, Is.EqualTo(0), "Releasing should return 0 ticks.");
        }

        [Test]
        public void CriticalHit_AppliedToDirectAttack_InIceField()
        {
            // Create a field with 100% critical chance.
            var critConfig = new IceFieldConfig(
                maxActiveIceCount: 1,
                maxSpecialIceCount: 0,
                hitRadiusReferencePixels: 9999f,
                minimumSpawnDistanceReferencePixels: 1f,
                respawnProtectionSeconds: 0f,
                iceDefinitions: new[] { new IceDefinition(IceTier.T1, "백빙", 10f, 10L) },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());

            var positioner = new IceSpawnPositioner(new Rect(0, 0, 960, 540), 1f);
            var mockClock = new MockClock();
            var criticalStrike = new CriticalStrike(1f, 3f);
            var critField = new IceField(
                1L,
                critConfig,
                new IceIdGenerator(),
                positioner,
                mockClock,
                criticalStrike);

            DamageAppliedEvent capturedDamage = default;
            critField.DamageApplied += e => capturedDamage = e;
            critField.IceDestroyed += _ => { };
            critField.Initialize(0d);

            var pos = critField.ActiveIce[0].ReferencePosition;
            critField.ApplyClickAt(pos, 1f, EffectType.Click, 0d);

            Assert.That(capturedDamage.WasCritical, Is.True, "Direct attack should be critical.");
            Assert.That(capturedDamage.Damage, Is.EqualTo(3f), "Critical should multiply damage by 3.");
        }

        [Test]
        public void T1T2T3_MixedSpawn_AllTiersPresent()
        {
            // Config with all tiers at equal weight.
            var mixedConfig = new IceFieldConfig(
                maxActiveIceCount: 60,
                maxSpecialIceCount: 0,
                hitRadiusReferencePixels: 56f,
                minimumSpawnDistanceReferencePixels: 1f,
                respawnProtectionSeconds: 0f,
                iceDefinitions: new[]
                {
                    new IceDefinition(IceTier.T1, "백빙", 10f, 10L),
                    new IceDefinition(IceTier.T2, "청빙", 60f, 80L),
                    new IceDefinition(IceTier.T3, "심빙", 360f, 700L),
                },
                spawnWeights: new[]
                {
                    new IceSpawnWeight(IceTier.T1, 34),
                    new IceSpawnWeight(IceTier.T2, 33),
                    new IceSpawnWeight(IceTier.T3, 33),
                },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());

            var positioner = new IceSpawnPositioner(new Rect(0, 0, 960, 540), 1f);
            var mockClock = new MockClock();
            var mixedField = new IceField(1L, mixedConfig, new IceIdGenerator(), positioner, mockClock);

            mixedField.IceDestroyed += _ => { };
            mixedField.Initialize(0d);

            var hasT1 = false;
            var hasT2 = false;
            var hasT3 = false;

            foreach (var ice in mixedField.ActiveIce)
            {
                switch (ice.Tier)
                {
                    case IceTier.T1: hasT1 = true; break;
                    case IceTier.T2: hasT2 = true; break;
                    case IceTier.T3: hasT3 = true; break;
                }
            }

            // With 60 ice at ~33% each, all tiers should appear.
            Assert.That(hasT1, Is.True, "T1 should appear.");
            Assert.That(hasT2, Is.True, "T2 should appear.");
            Assert.That(hasT3, Is.True, "T3 should appear.");
        }

        [Test]
        public void IceSpawnPositioner_ExcludesProtectedAreas()
        {
            var protectedAreas = new[]
            {
                new Rect(0f, 476f, 252f, 64f),
                new Rect(384f, 476f, 192f, 64f),
                new Rect(888f, 476f, 72f, 64f),
                new Rect(280f, 0f, 400f, 135f),
            };
            var positioner = new IceSpawnPositioner(
                new Rect(56f, 56f, 848f, 428f),
                1f,
                protectedAreas);

            for (var i = 0; i < 1000; i++)
            {
                Assert.That(positioner.TryGetPosition(Array.Empty<Vector2>(), out var position), Is.True);
                foreach (var protectedArea in protectedAreas)
                {
                    Assert.That(protectedArea.Contains(position), Is.False);
                }
            }
        }

        [Test]
        public void IceSpawnPositioner_TwentyIceStayClearOfHudAndAtLeast104Apart()
        {
            var protectedAreas = new[]
            {
                new Rect(0f, 476f, 252f, 64f),
                new Rect(384f, 476f, 192f, 64f),
                new Rect(888f, 476f, 72f, 64f),
                new Rect(280f, 0f, 400f, 135f),
            };
            var paddedProtectedAreas = new[]
            {
                new Rect(-56f, 420f, 364f, 176f),
                new Rect(328f, 420f, 304f, 176f),
                new Rect(832f, 420f, 184f, 176f),
                new Rect(224f, -56f, 512f, 247f),
            };

            for (var seed = 0; seed < 25; seed++)
            {
                UnityEngine.Random.InitState(seed);
                var positioner = new IceSpawnPositioner(
                    new Rect(56f, 56f, 848f, 428f),
                    120f,
                    protectedAreas,
                    excludedAreaPadding: 56f);
                var positions = new List<Vector2>(20);

                for (var layoutAttempt = 0; layoutAttempt < 10 && positions.Count < 20; layoutAttempt++)
                {
                    positions.Clear();
                    for (var i = 0; i < 20; i++)
                    {
                        if (!positioner.TryGetPosition(positions, out var position))
                        {
                            break;
                        }

                        foreach (var protectedArea in paddedProtectedAreas)
                        {
                            Assert.That(protectedArea.Contains(position), Is.False);
                        }

                        foreach (var existingPosition in positions)
                        {
                            Assert.That(Vector2.Distance(position, existingPosition), Is.GreaterThanOrEqualTo(104f));
                        }

                        positions.Add(position);
                    }
                }

                Assert.That(positions, Has.Count.EqualTo(20), $"seed {seed} did not produce a valid layout");
            }
        }

        [Test]
        public void IceFieldView_DefaultConfiguration_UsesOnlyT1Weight()
        {
            var createDefaultConfig = typeof(IceFieldView).GetMethod(
                "CreateDefaultConfig",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(createDefaultConfig, Is.Not.Null);
            var defaultConfig = createDefaultConfig!.Invoke(null, null) as IceFieldConfig;
            Assert.That(defaultConfig, Is.Not.Null);
            Assert.That(defaultConfig!.SpawnWeights, Has.Count.EqualTo(1));
            Assert.That(defaultConfig.SpawnWeights[0].Tier, Is.EqualTo(IceTier.T1));
            Assert.That(defaultConfig.SpawnWeights[0].Weight, Is.EqualTo(100));
        }

        // --- GP-04 Tests ---

        [Test]
        public void SpecialIce_SpawnLimit_EnforcedTo2()
        {
            var crackConfig = new IceFieldConfig(
                maxActiveIceCount: 20,
                maxSpecialIceCount: 2,
                hitRadiusReferencePixels: 56f,
                minimumSpawnDistanceReferencePixels: 1f,
                respawnProtectionSeconds: 0f,
                iceDefinitions: new[] { new IceDefinition(IceTier.T1, "백빙", 10f, 10L) },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: new[] { new SpecialIceDefinition(SpecialIceType.Crack, 1.0f, IceTier.T1, 0.6f, 1.0f) });

            var positioner = new IceSpawnPositioner(new Rect(0, 0, 960, 540), 1f);
            var mockClock = new MockClock();
            var testField = new IceField(1L, crackConfig, new IceIdGenerator(), positioner, mockClock);
            testField.Initialize(0d);

            var specialCount = 0;
            foreach (var ice in testField.ActiveIce)
            {
                if (ice.SpecialType == SpecialIceType.Crack) specialCount++;
            }

            Assert.That(specialCount, Is.EqualTo(2), "Exactly 2 special ice should spawn even with 100% chance.");
        }

        [Test]
        public void CrystalIce_Destroyed_Spawns5Shards_ThatDestroyLowerTier()
        {
            var testConfig = new IceFieldConfig(
                maxActiveIceCount: 10,
                maxSpecialIceCount: 1,
                hitRadiusReferencePixels: 56f,
                minimumSpawnDistanceReferencePixels: 1f,
                respawnProtectionSeconds: 0f,
                iceDefinitions: new[] { 
                    new IceDefinition(IceTier.T1, "백빙", 10f, 10L),
                    new IceDefinition(IceTier.T2, "청빙", 60f, 80L)
                },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: new[] { new SpecialIceDefinition(SpecialIceType.Crystal, 1.0f, IceTier.T1, 1f, 1f) });

            var positioner = new IceSpawnPositioner(new Rect(0, 0, 960, 540), 1f);
            var mockClock = new MockClock();
            var idGenerator = new IceIdGenerator();
            var testField = new IceField(1L, testConfig, idGenerator, positioner, mockClock);
            testField.Initialize(0d);
            
            var crystal = testField.ActiveIce[0];
            crystal.Reset(crystal.IceInstanceId, IceTier.T2, SpecialIceType.Crystal, 60f, new Vector2(500, 500), 0d);
            
            for (var i = 1; i < testField.ActiveIce.Count; i++)
            {
                var ice = testField.ActiveIce[i];
                ice.Reset(ice.IceInstanceId, IceTier.T1, SpecialIceType.None, 10f, new Vector2(0, 0), 0d);
            }

            var shardDamageCount = 0;
            testField.DamageApplied += e => {
                if (e.EffectType == EffectType.CrystalShard) shardDamageCount++;
            };

            testField.ApplyClickAt(new Vector2(500, 500), 60f, EffectType.Click, 100d);

            Assert.That(shardDamageCount, Is.EqualTo(5), "Crystal should emit exactly 5 shards.");
        }

        [Test]
        public void CrackIce_Destroyed_Explosion_DamagesRadius3x()
        {
            var testConfig = new IceFieldConfig(
                maxActiveIceCount: 3,
                maxSpecialIceCount: 1,
                hitRadiusReferencePixels: 999f,
                minimumSpawnDistanceReferencePixels: 1f,
                respawnProtectionSeconds: 0f,
                iceDefinitions: new[] { new IceDefinition(IceTier.T1, "백빙", 1000f, 10L) },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());

            var positioner = new IceSpawnPositioner(new Rect(0, 0, 960, 540), 1f);
            var mockClock = new MockClock();
            var testField = new IceField(1L, testConfig, new IceIdGenerator(), positioner, mockClock);
            testField.Initialize(0d);

            var crack = testField.ActiveIce[0];
            crack.Reset(crack.IceInstanceId, IceTier.T1, SpecialIceType.Crack, 10f, new Vector2(100, 100), 0d);
            
            var targetInRadius = testField.ActiveIce[1];
            targetInRadius.Reset(targetInRadius.IceInstanceId, IceTier.T1, SpecialIceType.None, 1000f, new Vector2(100, 200), 0d); // dist 100 <= 120

            var targetOutRadius = testField.ActiveIce[2];
            targetOutRadius.Reset(targetOutRadius.IceInstanceId, IceTier.T1, SpecialIceType.None, 1000f, new Vector2(100, 300), 0d); // dist 200 > 120

            DamageAppliedEvent? explosionEvent = null;
            testField.DamageApplied += e => {
                if (e.EffectType == EffectType.CrackExplosion && e.IceInstanceId == targetInRadius.IceInstanceId)
                {
                    explosionEvent = e;
                }
            };

            testField.ApplyClickAt(new Vector2(100, 100), 10f, EffectType.Click, 100d);

            Assert.That(explosionEvent.HasValue, Is.True, "Explosion should hit target in radius.");
            Assert.That(explosionEvent!.Value.Damage, Is.EqualTo(30f), "Explosion damage should be 3x click damage.");
            Assert.That(targetOutRadius.RemainingHp, Is.EqualTo(1000f), "Target outside radius should not be damaged.");
        }

        [Test]
        public void ChainDepth_Exceeds3_DoesNotTriggerFurtherChain()
        {
            var testConfig = new IceFieldConfig(
                maxActiveIceCount: 5,
                maxSpecialIceCount: 0,
                hitRadiusReferencePixels: 999f,
                minimumSpawnDistanceReferencePixels: 1f,
                respawnProtectionSeconds: 0f,
                iceDefinitions: new[] { new IceDefinition(IceTier.T1, "백빙", 10f, 10L) },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());

            var positioner = new IceSpawnPositioner(new Rect(0, 0, 960, 540), 1f);
            var mockClock = new MockClock();
            var testField = new IceField(1L, testConfig, new IceIdGenerator(), positioner, mockClock);
            testField.Initialize(0d);

            for (var i = 0; i < 5; i++)
            {
                var ice = testField.ActiveIce[i];
                ice.Reset(ice.IceInstanceId, IceTier.T1, SpecialIceType.Crack, 10f, new Vector2(0, i * 110), 0d); 
            }

            testField.ApplyClickAt(new Vector2(0, 0), 10f, EffectType.Click, 100d);

            var ice4Survived = false;
            foreach (var ice in testField.ActiveIce)
            {
                if (ice.IceInstanceId == testField.ActiveIce[4].IceInstanceId && !ice.IsDestroyed)
                {
                    ice4Survived = true;
                }
            }

            Assert.That(ice4Survived, Is.True, "Ice 4 should survive because depth 3 destruction does not trigger chains.");
        }
        [Test]
        public void CombatBoundary_ExceedsDuration_BlocksClickAndClearsQueue()
        {
            var testConfig = new IceFieldConfig(
                maxActiveIceCount: 5,
                maxSpecialIceCount: 0,
                hitRadiusReferencePixels: 56f,
                minimumSpawnDistanceReferencePixels: 10f,
                respawnProtectionSeconds: 0f,
                iceDefinitions: new[] { new IceDefinition(IceTier.T1, "백빙", 10f, 10L) },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());

            var positioner = new IceSpawnPositioner(new Rect(0, 0, 960, 540), 1f);
            var mockClock = new MockClock();
            var testField = new IceField(1L, testConfig, new IceIdGenerator(), positioner, mockClock);
            testField.Initialize(0d);
            
            var target = testField.ActiveIce[0];
            target.Reset(target.IceInstanceId, IceTier.T1, SpecialIceType.None, 10f, new Vector2(100, 100), 0d);

            // Fast forward clock to 60 seconds (duration)
            mockClock.StageElapsedSeconds = 60d;

            int damageEventCount = 0;
            testField.DamageApplied += _ => damageEventCount++;

            // Click should be ignored
            var clicked = testField.ApplyClickAt(new Vector2(100, 100), 10f, EffectType.Click, 60d);
            
            Assert.That(clicked, Is.False, "ApplyClickAt should return false after duration has passed.");
            Assert.That(damageEventCount, Is.EqualTo(0), "No damage events should be fired after combat ends.");
            Assert.That(target.RemainingHp, Is.EqualTo(10f), "Target should not take damage after combat ends.");
        }

        [Test]
        public void FatalDamage_PublishesOnce_ThenRespawnsWithNewId()
        {
            var testConfig = new IceFieldConfig(
                maxActiveIceCount: 5,
                maxSpecialIceCount: 0,
                hitRadiusReferencePixels: 56f,
                minimumSpawnDistanceReferencePixels: 10f,
                respawnProtectionSeconds: 0f,
                iceDefinitions: new[] { new IceDefinition(IceTier.T1, "백빙", 10f, 10L) },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());

            var positioner = new IceSpawnPositioner(new Rect(0, 0, 960, 540), 1f);
            var mockClock = new MockClock();
            var testField = new IceField(1L, testConfig, new IceIdGenerator(), positioner, mockClock);
            testField.Initialize(0d);

            var target = testField.ActiveIce[0];
            var oldId = target.IceInstanceId;
            target.Reset(target.IceInstanceId, IceTier.T1, SpecialIceType.None, 10f, new Vector2(100, 100), 0d);

            int damageCount = 0;
            int destroyCount = 0;
            testField.DamageApplied += _ => damageCount++;
            testField.IceDestroyed += _ => destroyCount++;

            testField.ApplyClickAt(new Vector2(100, 100), 10f, EffectType.Click, 0d);

            Assert.That(damageCount, Is.EqualTo(1));
            Assert.That(destroyCount, Is.EqualTo(1));
            Assert.That(target.IceInstanceId, Is.Not.EqualTo(oldId));
            Assert.That(target.IsDestroyed, Is.False);
            Assert.That(target.RemainingHp, Is.EqualTo(10f));
        }

        // ===== GP-06: S01 Support Charge =====

        private static SupportAttackConfig CreateBasicSupportConfig()
        {
            return new SupportAttackConfig(
                enabled: true,
                requiredDirectHitCount: 12,
                primaryDamageMultiplier: 1.0f,
                additionalTargetCount: 0,
                additionalDamageMultiplier: 0.7f,
                prioritizeSpecialIce: false,
                specialIceDamageMultiplier: 2.0f);
        }

        [Test]
        public void S01_ValidDirectHit_IncrementsSupportCharge()
        {
            var supportConfig = CreateBasicSupportConfig();
            var testConfig = new IceFieldConfig(
                maxActiveIceCount: 5,
                maxSpecialIceCount: 0,
                hitRadiusReferencePixels: 56f,
                minimumSpawnDistanceReferencePixels: 1f,
                respawnProtectionSeconds: 0f,
                iceDefinitions: new[] { new IceDefinition(IceTier.T1, "백빙", 1000f, 10L) },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());

            var positioner = new IceSpawnPositioner(new Rect(0, 0, 960, 540), 1f);
            var mockClock = new MockClock();
            var testField = new IceField(1L, testConfig, new IceIdGenerator(), positioner, mockClock,
                supportConfig: supportConfig);
            testField.Initialize(0d);

            var chargeEvents = new List<SupportChargeChangedEvent>();
            testField.SupportChargeChanged += e => chargeEvents.Add(e);

            var target = testField.ActiveIce[0];
            target.Reset(target.IceInstanceId, IceTier.T1, SpecialIceType.None, 1000f, new Vector2(100, 100), 0d);

            // 3 valid hits → charge should be 1, 2, 3
            for (var i = 0; i < 3; i++)
            {
                testField.ApplyClickAt(new Vector2(100, 100), 1f, EffectType.Click, 0d);
            }

            Assert.That(chargeEvents.Count, Is.EqualTo(3));
            Assert.That(chargeEvents[0].CurrentCharge, Is.EqualTo(1));
            Assert.That(chargeEvents[1].CurrentCharge, Is.EqualTo(2));
            Assert.That(chargeEvents[2].CurrentCharge, Is.EqualTo(3));
            Assert.That(chargeEvents[0].MaxCharge, Is.EqualTo(12));
        }

        [Test]
        public void S01_12thValidHit_ResetsChargeToZero()
        {
            var supportConfig = CreateBasicSupportConfig();
            var testConfig = new IceFieldConfig(
                maxActiveIceCount: 5,
                maxSpecialIceCount: 0,
                hitRadiusReferencePixels: 56f,
                minimumSpawnDistanceReferencePixels: 1f,
                respawnProtectionSeconds: 0f,
                iceDefinitions: new[] { new IceDefinition(IceTier.T1, "백빙", 1000f, 10L) },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());

            var positioner = new IceSpawnPositioner(new Rect(0, 0, 960, 540), 1f);
            var mockClock = new MockClock();
            var testField = new IceField(1L, testConfig, new IceIdGenerator(), positioner, mockClock,
                supportConfig: supportConfig);
            testField.Initialize(0d);

            var chargeEvents = new List<SupportChargeChangedEvent>();
            testField.SupportChargeChanged += e => chargeEvents.Add(e);

            var target = testField.ActiveIce[0];
            target.Reset(target.IceInstanceId, IceTier.T1, SpecialIceType.None, 1000f, new Vector2(100, 100), 0d);

            // 12 valid hits
            for (var i = 0; i < 12; i++)
            {
                testField.ApplyClickAt(new Vector2(100, 100), 1f, EffectType.Click, 0d);
            }

            Assert.That(chargeEvents.Count, Is.EqualTo(12));
            // 12th hit should reset to 0
            Assert.That(chargeEvents[11].CurrentCharge, Is.EqualTo(0));

            // Hit 13 should increment to 1 again
            testField.ApplyClickAt(new Vector2(100, 100), 1f, EffectType.Click, 0d);
            Assert.That(chargeEvents.Count, Is.EqualTo(13));
            Assert.That(chargeEvents[12].CurrentCharge, Is.EqualTo(1));
        }

        [Test]
        public void S01_MissedClick_DoesNotCharge()
        {
            var supportConfig = CreateBasicSupportConfig();
            var testConfig = new IceFieldConfig(
                maxActiveIceCount: 5,
                maxSpecialIceCount: 0,
                hitRadiusReferencePixels: 56f,
                minimumSpawnDistanceReferencePixels: 1f,
                respawnProtectionSeconds: 0f,
                iceDefinitions: new[] { new IceDefinition(IceTier.T1, "백빙", 1000f, 10L) },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());

            var positioner = new IceSpawnPositioner(new Rect(0, 0, 960, 540), 1f);
            var mockClock = new MockClock();
            var testField = new IceField(1L, testConfig, new IceIdGenerator(), positioner, mockClock,
                supportConfig: supportConfig);
            testField.Initialize(0d);

            var chargeEvents = new List<SupportChargeChangedEvent>();
            testField.SupportChargeChanged += e => chargeEvents.Add(e);

            // Click in empty space (far from any ice)
            testField.ApplyClickAt(new Vector2(-9999, -9999), 1f, EffectType.Click, 0d);

            Assert.That(chargeEvents.Count, Is.EqualTo(0), "Missed clicks should not charge support.");
        }

        [Test]
        public void S01_TimerExpired_DoesNotCharge()
        {
            var supportConfig = CreateBasicSupportConfig();
            var testConfig = new IceFieldConfig(
                maxActiveIceCount: 5,
                maxSpecialIceCount: 0,
                hitRadiusReferencePixels: 56f,
                minimumSpawnDistanceReferencePixels: 1f,
                respawnProtectionSeconds: 0f,
                iceDefinitions: new[] { new IceDefinition(IceTier.T1, "백빙", 1000f, 10L) },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());

            var positioner = new IceSpawnPositioner(new Rect(0, 0, 960, 540), 1f);
            var mockClock = new MockClock();
            var testField = new IceField(1L, testConfig, new IceIdGenerator(), positioner, mockClock,
                supportConfig: supportConfig);
            testField.Initialize(0d);

            var chargeEvents = new List<SupportChargeChangedEvent>();
            testField.SupportChargeChanged += e => chargeEvents.Add(e);

            // Expire the timer
            mockClock.StageElapsedSeconds = 60d;

            var target = testField.ActiveIce[0];
            target.Reset(target.IceInstanceId, IceTier.T1, SpecialIceType.None, 1000f, new Vector2(100, 100), 0d);

            testField.ApplyClickAt(new Vector2(100, 100), 1f, EffectType.Click, 60d);

            Assert.That(chargeEvents.Count, Is.EqualTo(0), "Timer expired clicks should not charge support.");
        }

        [Test]
        public void S01_Initialize_ResetsCharge()
        {
            var supportConfig = CreateBasicSupportConfig();
            var testConfig = new IceFieldConfig(
                maxActiveIceCount: 5,
                maxSpecialIceCount: 0,
                hitRadiusReferencePixels: 56f,
                minimumSpawnDistanceReferencePixels: 1f,
                respawnProtectionSeconds: 0f,
                iceDefinitions: new[] { new IceDefinition(IceTier.T1, "백빙", 1000f, 10L) },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());

            var positioner = new IceSpawnPositioner(new Rect(0, 0, 960, 540), 1f);
            var mockClock = new MockClock();
            var testField = new IceField(1L, testConfig, new IceIdGenerator(), positioner, mockClock,
                supportConfig: supportConfig);
            testField.Initialize(0d);

            var target = testField.ActiveIce[0];
            target.Reset(target.IceInstanceId, IceTier.T1, SpecialIceType.None, 1000f, new Vector2(100, 100), 0d);

            // Hit 5 times to accumulate charge
            for (var i = 0; i < 5; i++)
            {
                testField.ApplyClickAt(new Vector2(100, 100), 1f, EffectType.Click, 0d);
            }

            // Re-initialize the field (new stage)
            testField.Initialize(0d);

            var chargeEvents = new List<SupportChargeChangedEvent>();
            testField.SupportChargeChanged += e => chargeEvents.Add(e);

            target = testField.ActiveIce[0];
            target.Reset(target.IceInstanceId, IceTier.T1, SpecialIceType.None, 1000f, new Vector2(100, 100), 0d);

            // First hit after re-init should be charge 1, not 6
            testField.ApplyClickAt(new Vector2(100, 100), 1f, EffectType.Click, 0d);
            Assert.That(chargeEvents[0].CurrentCharge, Is.EqualTo(1),
                "Support charge should reset to 0 on Initialize.");
        }

        // ===== GP-06 Step 2: S01 Single Target Fire =====

        [Test]
        public void S01_12thHit_FiresSupportShot_AtClosestTarget()
        {
            var supportConfig = CreateBasicSupportConfig();
            var testConfig = new IceFieldConfig(
                maxActiveIceCount: 3,
                maxSpecialIceCount: 0,
                hitRadiusReferencePixels: 999f,
                minimumSpawnDistanceReferencePixels: 1f,
                respawnProtectionSeconds: 0f,
                iceDefinitions: new[] { new IceDefinition(IceTier.T1, "백빙", 1000f, 10L) },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());

            var positioner = new IceSpawnPositioner(new Rect(0, 0, 960, 540), 1f);
            var mockClock = new MockClock();
            var testField = new IceField(1L, testConfig, new IceIdGenerator(), positioner, mockClock,
                supportConfig: supportConfig);
            testField.Initialize(0d);

            // Place targets at known positions
            var clickTarget = testField.ActiveIce[0];
            clickTarget.Reset(clickTarget.IceInstanceId, IceTier.T1, SpecialIceType.None, 1000f, new Vector2(100, 100), 0d);
            var nearTarget = testField.ActiveIce[1];
            nearTarget.Reset(nearTarget.IceInstanceId, IceTier.T1, SpecialIceType.None, 1000f, new Vector2(200, 100), 0d);
            var farTarget = testField.ActiveIce[2];
            farTarget.Reset(farTarget.IceInstanceId, IceTier.T1, SpecialIceType.None, 1000f, new Vector2(800, 400), 0d);

            var supportDamageEvents = new List<DamageAppliedEvent>();
            testField.DamageApplied += e =>
            {
                if (e.EffectType == EffectType.SupportShot)
                    supportDamageEvents.Add(e);
            };

            // 12 valid hits to trigger support fire
            for (var i = 0; i < 12; i++)
            {
                testField.ApplyClickAt(new Vector2(100, 100), 10f, EffectType.Click, 0d);
            }

            Assert.That(supportDamageEvents.Count, Is.EqualTo(1), "Should fire exactly 1 support shot.");
            Assert.That(supportDamageEvents[0].IceInstanceId, Is.EqualTo(nearTarget.IceInstanceId),
                "Support should target the closest non-click-target ice.");
            Assert.That(supportDamageEvents[0].Damage, Is.EqualTo(10f),
                "Primary damage = clickDamage × 1.0.");
            Assert.That(supportDamageEvents[0].WasCritical, Is.False,
                "Support shots should never be critical.");
        }

        // ===== GP-06 Step 3: S02 Multi-Target =====

        [Test]
        public void S02_AdditionalTargets_FireWithReducedDamage()
        {
            var multiTargetConfig = new SupportAttackConfig(
                enabled: true,
                requiredDirectHitCount: 12,
                primaryDamageMultiplier: 1.0f,
                additionalTargetCount: 2,
                additionalDamageMultiplier: 0.7f,
                prioritizeSpecialIce: false,
                specialIceDamageMultiplier: 2.0f);

            var testConfig = new IceFieldConfig(
                maxActiveIceCount: 5,
                maxSpecialIceCount: 0,
                hitRadiusReferencePixels: 999f,
                minimumSpawnDistanceReferencePixels: 1f,
                respawnProtectionSeconds: 0f,
                iceDefinitions: new[] { new IceDefinition(IceTier.T1, "백빙", 1000f, 10L) },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());

            var positioner = new IceSpawnPositioner(new Rect(0, 0, 960, 540), 1f);
            var mockClock = new MockClock();
            var testField = new IceField(1L, testConfig, new IceIdGenerator(), positioner, mockClock,
                supportConfig: multiTargetConfig);
            testField.Initialize(0d);

            // Place targets at known positions
            var clickTarget = testField.ActiveIce[0];
            clickTarget.Reset(clickTarget.IceInstanceId, IceTier.T1, SpecialIceType.None, 1000f, new Vector2(100, 100), 0d);
            testField.ActiveIce[1].Reset(testField.ActiveIce[1].IceInstanceId, IceTier.T1, SpecialIceType.None, 1000f, new Vector2(200, 100), 0d);
            testField.ActiveIce[2].Reset(testField.ActiveIce[2].IceInstanceId, IceTier.T1, SpecialIceType.None, 1000f, new Vector2(300, 100), 0d);
            testField.ActiveIce[3].Reset(testField.ActiveIce[3].IceInstanceId, IceTier.T1, SpecialIceType.None, 1000f, new Vector2(400, 100), 0d);
            testField.ActiveIce[4].Reset(testField.ActiveIce[4].IceInstanceId, IceTier.T1, SpecialIceType.None, 1000f, new Vector2(500, 100), 0d);

            var supportDamageEvents = new List<DamageAppliedEvent>();
            testField.DamageApplied += e =>
            {
                if (e.EffectType == EffectType.SupportShot)
                    supportDamageEvents.Add(e);
            };

            // 12 valid hits to trigger support fire
            for (var i = 0; i < 12; i++)
            {
                testField.ApplyClickAt(new Vector2(100, 100), 10f, EffectType.Click, 0d);
            }

            // 1 primary + 2 additional = 3 total
            Assert.That(supportDamageEvents.Count, Is.EqualTo(3), "Should fire 1 primary + 2 additional.");
            Assert.That(supportDamageEvents[0].Damage, Is.EqualTo(10f), "Primary = 10 × 1.0.");
            Assert.That(supportDamageEvents[1].Damage, Is.EqualTo(7f), "Additional = 10 × 0.7.");
            Assert.That(supportDamageEvents[2].Damage, Is.EqualTo(7f), "Additional = 10 × 0.7.");

            // No duplicate targets
            var targetIds = new HashSet<long>();
            foreach (var e in supportDamageEvents)
            {
                Assert.That(targetIds.Add(e.IceInstanceId), Is.True,
                    $"Duplicate support target: {e.IceInstanceId}");
            }
        }

        // ===== GP-06 Step 4: S03 Special Ice Priority =====

        [Test]
        public void S03_PrioritizesSpecialIce_WithDoubledDamage()
        {
            var s03Config = new SupportAttackConfig(
                enabled: true,
                requiredDirectHitCount: 12,
                primaryDamageMultiplier: 1.0f,
                additionalTargetCount: 1,
                additionalDamageMultiplier: 0.7f,
                prioritizeSpecialIce: true,
                specialIceDamageMultiplier: 2.0f);

            var testConfig = new IceFieldConfig(
                maxActiveIceCount: 4,
                maxSpecialIceCount: 2,
                hitRadiusReferencePixels: 999f,
                minimumSpawnDistanceReferencePixels: 1f,
                respawnProtectionSeconds: 0f,
                iceDefinitions: new[] {
                    new IceDefinition(IceTier.T1, "백빙", 1000f, 10L),
                    new IceDefinition(IceTier.T2, "청빙", 2000f, 80L)
                },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());

            var positioner = new IceSpawnPositioner(new Rect(0, 0, 960, 540), 1f);
            var mockClock = new MockClock();
            var testField = new IceField(1L, testConfig, new IceIdGenerator(), positioner, mockClock,
                supportConfig: s03Config);
            testField.Initialize(0d);

            // Click target (normal, closest)
            var clickTarget = testField.ActiveIce[0];
            clickTarget.Reset(clickTarget.IceInstanceId, IceTier.T1, SpecialIceType.None, 1000f, new Vector2(100, 100), 0d);
            // Normal ice, close to fire position
            var normalClose = testField.ActiveIce[1];
            normalClose.Reset(normalClose.IceInstanceId, IceTier.T1, SpecialIceType.None, 1000f, new Vector2(150, 100), 0d);
            // Crystal ice, far from fire position
            var crystalFar = testField.ActiveIce[2];
            crystalFar.Reset(crystalFar.IceInstanceId, IceTier.T2, SpecialIceType.Crystal, 2000f, new Vector2(800, 400), 0d);
            // Normal ice, far
            var normalFar = testField.ActiveIce[3];
            normalFar.Reset(normalFar.IceInstanceId, IceTier.T1, SpecialIceType.None, 1000f, new Vector2(700, 300), 0d);

            var supportDamageEvents = new List<DamageAppliedEvent>();
            testField.DamageApplied += e =>
            {
                if (e.EffectType == EffectType.SupportShot)
                    supportDamageEvents.Add(e);
            };

            for (var i = 0; i < 12; i++)
            {
                testField.ApplyClickAt(new Vector2(100, 100), 10f, EffectType.Click, 0d);
            }

            // S03: crystal should be primary (special ice priority), normal should be additional
            Assert.That(supportDamageEvents.Count, Is.EqualTo(2));
            Assert.That(supportDamageEvents[0].IceInstanceId, Is.EqualTo(crystalFar.IceInstanceId),
                "S03 should prioritize special ice even if it's farther.");
            Assert.That(supportDamageEvents[0].Damage, Is.EqualTo(20f),
                "Special ice primary damage = 10 × 1.0 × 2.0 = 20.");
            // Second target: highest HP normal ice (normalClose and normalFar have same HP=1000,
            // normalClose is closer)
            Assert.That(supportDamageEvents[1].IceInstanceId, Is.EqualTo(normalClose.IceInstanceId));
            Assert.That(supportDamageEvents[1].Damage, Is.EqualTo(7f),
                "Normal additional damage = 10 × 0.7 (no ×2 for non-special).");
        }

        [Test]
        public void S03_NoSpecialIce_FallsBackToHighestHp()
        {
            var s03Config = new SupportAttackConfig(
                enabled: true,
                requiredDirectHitCount: 12,
                primaryDamageMultiplier: 1.0f,
                additionalTargetCount: 0,
                additionalDamageMultiplier: 0.7f,
                prioritizeSpecialIce: true,
                specialIceDamageMultiplier: 2.0f);

            var testConfig = new IceFieldConfig(
                maxActiveIceCount: 3,
                maxSpecialIceCount: 0,
                hitRadiusReferencePixels: 999f,
                minimumSpawnDistanceReferencePixels: 1f,
                respawnProtectionSeconds: 0f,
                iceDefinitions: new[] { new IceDefinition(IceTier.T1, "백빙", 1000f, 10L) },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());

            var positioner = new IceSpawnPositioner(new Rect(0, 0, 960, 540), 1f);
            var mockClock = new MockClock();
            var testField = new IceField(1L, testConfig, new IceIdGenerator(), positioner, mockClock,
                supportConfig: s03Config);
            testField.Initialize(0d);

            var clickTarget = testField.ActiveIce[0];
            clickTarget.Reset(clickTarget.IceInstanceId, IceTier.T1, SpecialIceType.None, 1000f, new Vector2(100, 100), 0d);
            // Low HP, close
            var lowHpClose = testField.ActiveIce[1];
            lowHpClose.Reset(lowHpClose.IceInstanceId, IceTier.T1, SpecialIceType.None, 200f, new Vector2(150, 100), 0d);
            // High HP, far
            var highHpFar = testField.ActiveIce[2];
            highHpFar.Reset(highHpFar.IceInstanceId, IceTier.T1, SpecialIceType.None, 800f, new Vector2(500, 300), 0d);

            var supportDamageEvents = new List<DamageAppliedEvent>();
            testField.DamageApplied += e =>
            {
                if (e.EffectType == EffectType.SupportShot)
                    supportDamageEvents.Add(e);
            };

            for (var i = 0; i < 12; i++)
            {
                testField.ApplyClickAt(new Vector2(100, 100), 10f, EffectType.Click, 0d);
            }

            Assert.That(supportDamageEvents.Count, Is.EqualTo(1));
            Assert.That(supportDamageEvents[0].IceInstanceId, Is.EqualTo(highHpFar.IceInstanceId),
                "S03 without special ice should fallback to highest HP.");
            Assert.That(supportDamageEvents[0].Damage, Is.EqualTo(10f),
                "Normal ice should not get ×2 multiplier.");
        }
    }
}
