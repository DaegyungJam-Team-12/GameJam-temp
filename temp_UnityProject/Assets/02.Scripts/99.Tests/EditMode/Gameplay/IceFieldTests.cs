#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
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

            field = new IceField(1L, config, idGenerator, positioner);
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

            var critField = new IceField(
                1L, critConfig, new IceIdGenerator(),
                new IceSpawnPositioner(new Rect(0, 0, 960, 540), 1f),
                new CriticalStrike(1.0f, 3.0f)); // Always crit

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

            var mixedField = new IceField(
                1L, mixedConfig, new IceIdGenerator(),
                new IceSpawnPositioner(new Rect(0, 0, 960, 540), 1f));

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
    }
}
