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

        private sealed class HitTiltFixture : IDisposable
        {
            public HitTiltFixture(IceField field, MockClock clock)
            {
                Root = new GameObject("IceFieldViewHitTiltTest");
                View = Root.AddComponent<IceFieldView>();
                SetViewField(View, "field", field);
                SetViewField(View, "activeClock", clock);
                InvokeViewMethod(View, "CreateAllVisuals");
                Renderer = Root.GetComponentInChildren<SpriteRenderer>();
                IceInstanceId = field.ActiveIce[0].IceInstanceId;
            }

            public GameObject Root { get; }
            public IceFieldView View { get; }
            public SpriteRenderer Renderer { get; }
            public long IceInstanceId { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }
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
        public void VisualDiameter_UsesFiveStableStepsPerTierAndHigherTiersAreLarger()
        {
            var method = typeof(IceField).GetMethod(
                "ResolveVisualDiameter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            var sizesByTier = new Dictionary<IceTier, HashSet<float>>();
            foreach (var tier in new[]
                     {
                         IceTier.T1,
                         IceTier.T2,
                         IceTier.T3,
                         IceTier.T4,
                         IceTier.T5
                     })
            {
                var sizes = new HashSet<float>();
                for (var iceInstanceId = 1L; iceInstanceId <= 100L; iceInstanceId++)
                {
                    var size = (float)method!.Invoke(field, new object[] { tier, iceInstanceId });
                    sizes.Add(Mathf.Round(size * 1000f) / 1000f);
                }

                Assert.That(sizes, Has.Count.EqualTo(5), tier.ToString());
                sizesByTier.Add(tier, sizes);
            }

            for (var tierIndex = 0; tierIndex < 4; tierIndex++)
            {
                var lowerTier = (IceTier)tierIndex;
                var higherTier = (IceTier)(tierIndex + 1);
                Assert.That(
                    Mathf.Max(new List<float>(sizesByTier[lowerTier]).ToArray()),
                    Is.LessThan(Mathf.Min(new List<float>(sizesByTier[higherTier]).ToArray())));
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

            clock.StageElapsedSeconds = 0.12d;
            field.UpdateRespawns();
            clock.StageElapsedSeconds = 0.3d;
            field.UpdateRespawns();

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

            clock.StageElapsedSeconds = 0.12d;
            field.UpdateRespawns();
            clock.StageElapsedSeconds = 0.3d;
            field.UpdateRespawns();

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
        public void IceField_FiftySixIceVisualLayout_StaysClearOfProtectedZonesAndSpacing()
        {
            var protectedAreas = new[]
            {
                new Rect(0f, 476f, 252f, 64f),
                new Rect(384f, 476f, 192f, 64f),
                new Rect(888f, 476f, 72f, 64f),
                new Rect(280f, 0f, 400f, 135f),
            };
            const float protectedAreaPadding = 21f;
            var paddedProtectedAreas = new[]
            {
                new Rect(-21f, 455f, 294f, 106f),
                new Rect(363f, 455f, 234f, 106f),
                new Rect(867f, 455f, 114f, 106f),
                new Rect(259f, -21f, 442f, 177f),
            };
            var visualConfig = new IceFieldConfig(
                maxActiveIceCount: 56,
                maxSpecialIceCount: 2,
                visualDiameterMinimumReferencePixels: 34f,
                visualDiameterMaximumReferencePixels: 42f,
                iceCollisionRadiusReferencePixels: 40f,
                strictExtraVisualGapReferencePixels: 18f,
                relaxedExtraVisualGapReferencePixels: 12f,
                outerMarginReferencePixels: 20f,
                protectedAreaPaddingReferencePixels: protectedAreaPadding,
                recentDestructionExclusionReferencePixels: 160f,
                recentDestructionExclusionSeconds: 1f,
                respawnGapSeconds: 0.12f,
                spawnAnimationSeconds: 0.18f,
                chainRespawnStaggerSeconds: 0.03f,
                respawnProtectionSeconds: 0.25f,
                iceDefinitions: new[] { new IceDefinition(IceTier.T1, "백빙", 10f, 10L) },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());
            var previousRandomState = UnityEngine.Random.state;

            try
            {
                foreach (var seed in new[] { 0, 1, 42, 1234 })
                {
                    UnityEngine.Random.InitState(seed);
                    var spawnMargin = visualConfig.OuterMarginReferencePixels +
                        visualConfig.VisualDiameterMaximumReferencePixels * 0.5f;
                    var field = new IceField(
                        1L,
                        visualConfig,
                        new IceIdGenerator(),
                        new IceSpawnPositioner(
                            new Rect(
                                spawnMargin,
                                spawnMargin,
                                960f - spawnMargin * 2f,
                                540f - spawnMargin * 2f),
                            visualConfig.StrictExtraVisualGapReferencePixels,
                            visualConfig.RelaxedExtraVisualGapReferencePixels,
                            protectedAreas,
                            visualConfig.ProtectedAreaPaddingReferencePixels),
                        new MockClock());

                    field.Initialize(0d);

                    Assert.That(field.ActiveIce, Has.Count.EqualTo(56), $"seed {seed}");
                    for (var index = 0; index < field.ActiveIce.Count; index++)
                    {
                        var ice = field.ActiveIce[index];
                        Assert.That(
                            ice.VisualDiameterReferencePixels,
                            Is.InRange(34f, 42f),
                            $"seed {seed}, ice {index}");
                        foreach (var protectedArea in paddedProtectedAreas)
                        {
                            Assert.That(
                                protectedArea.Contains(ice.ReferencePosition),
                                Is.False,
                                $"seed {seed}, ice {index}");
                        }

                        for (var previousIndex = 0; previousIndex < index; previousIndex++)
                        {
                            var otherIce = field.ActiveIce[previousIndex];
                            var requiredDistance =
                                (ice.VisualDiameterReferencePixels +
                                 otherIce.VisualDiameterReferencePixels) * 0.5f +
                                visualConfig.RelaxedExtraVisualGapReferencePixels;
                            Assert.That(
                                Vector2.Distance(ice.ReferencePosition, otherIce.ReferencePosition),
                                Is.GreaterThanOrEqualTo(requiredDistance),
                                $"seed {seed}, ice pair {previousIndex}/{index}");
                        }
                    }
                }
            }
            finally
            {
                UnityEngine.Random.state = previousRandomState;
            }
        }

        [Test]
        public void IceFieldView_DefaultConfiguration_UsesOnlyT1Weight()
        {
            var createDefaultConfig = typeof(IceFieldView).GetMethod(
                "CreateDefaultConfig",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(createDefaultConfig, Is.Not.Null);
            var gameObject = new GameObject("IceFieldViewTest");
            try
            {
                var view = gameObject.AddComponent<IceFieldView>();
                var defaultConfig = createDefaultConfig!.Invoke(view, null) as IceFieldConfig;
                Assert.That(defaultConfig, Is.Not.Null);
                Assert.That(defaultConfig!.SpawnWeights, Has.Count.EqualTo(1));
                Assert.That(defaultConfig.SpawnWeights[0].Tier, Is.EqualTo(IceTier.T1));
                Assert.That(defaultConfig.SpawnWeights[0].Weight, Is.EqualTo(100));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void IceFieldView_NonLethalDirectSupportAndChainDamage_StartsImmediateTiltWithoutMoving()
        {
            using var fixture = new HitTiltFixture(field, clock);
            var initialPosition = fixture.Renderer.transform.localPosition;
            var initialScale = fixture.Renderer.transform.localScale;
            var initialColor = fixture.Renderer.color;

            foreach (var effectType in new[]
                     {
                         EffectType.CursorAreaPulse,
                         EffectType.SupportShot,
                         EffectType.CrystalShard
                     })
            {
                InvokeViewMethod(
                    fixture.View,
                    "HandleDamageApplied",
                    CreateDamageEvent(fixture.IceInstanceId, effectType, remainingHp: 1f));

                Assert.That(
                    Mathf.Abs(Mathf.DeltaAngle(0f, fixture.Renderer.transform.localEulerAngles.z)),
                    Is.EqualTo(8f).Within(0.001f),
                    effectType.ToString());
                Assert.That(fixture.Renderer.transform.localPosition, Is.EqualTo(initialPosition));
                Assert.That(
                    fixture.Renderer.transform.localScale.x,
                    Is.EqualTo(initialScale.x * 1.08f).Within(0.001f));
                Assert.That(
                    fixture.Renderer.transform.localScale.y,
                    Is.EqualTo(initialScale.y * 0.90f).Within(0.001f));
                Assert.That(fixture.Renderer.color, Is.Not.EqualTo(initialColor));
                InvokeViewMethod(fixture.View, "ResetHitTilt", 0);
                Assert.That(fixture.Renderer.transform.localScale, Is.EqualTo(initialScale));
                Assert.That(fixture.Renderer.color, Is.EqualTo(initialColor));
            }
        }

        [Test]
        public void IceFieldView_HitTilt_DecaysToIdentityAndRetriggerRestarts()
        {
            using var fixture = new HitTiltFixture(field, clock);
            var initialScale = fixture.Renderer.transform.localScale;
            var initialColor = fixture.Renderer.color;
            InvokeViewMethod(
                fixture.View,
                "HandleDamageApplied",
                CreateDamageEvent(fixture.IceInstanceId, EffectType.Click, remainingHp: 1f));
            InvokeViewMethod(fixture.View, "UpdateHitTilts", 0.04f);

            var decayedAngle = Mathf.Abs(
                Mathf.DeltaAngle(0f, fixture.Renderer.transform.localEulerAngles.z));
            Assert.That(decayedAngle, Is.GreaterThan(0f).And.LessThan(8f));

            InvokeViewMethod(
                fixture.View,
                "HandleDamageApplied",
                CreateDamageEvent(fixture.IceInstanceId, EffectType.Click, remainingHp: 1f));
            Assert.That(
                Mathf.Abs(Mathf.DeltaAngle(0f, fixture.Renderer.transform.localEulerAngles.z)),
                Is.EqualTo(8f).Within(0.001f));
            Assert.That(
                fixture.Renderer.transform.localScale.x,
                Is.EqualTo(initialScale.x * 1.08f).Within(0.001f));

            InvokeViewMethod(
                fixture.View,
                "UpdateHitTilts",
                0.10f);
            Assert.That(fixture.Renderer.transform.localRotation, Is.Not.EqualTo(Quaternion.identity));

            InvokeViewMethod(
                fixture.View,
                "UpdateHitTilts",
                0.04f);
            Assert.That(fixture.Renderer.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(fixture.Renderer.transform.localScale, Is.EqualTo(initialScale));
            Assert.That(fixture.Renderer.color, Is.EqualTo(initialColor));
        }

        [Test]
        public void IceFieldView_HitTilt_PauseFreezesElapsedTime()
        {
            using var fixture = new HitTiltFixture(field, clock);
            InvokeViewMethod(
                fixture.View,
                "HandleDamageApplied",
                CreateDamageEvent(fixture.IceInstanceId, EffectType.Click, remainingHp: 1f));
            InvokeViewMethod(fixture.View, "UpdateHitTilts", 0.03f);
            var rotationBeforePause = fixture.Renderer.transform.localRotation;

            clock.IsPaused = true;
            InvokeViewMethod(fixture.View, "UpdateHitTilts", 0.20f);

            Assert.That(fixture.Renderer.transform.localRotation, Is.EqualTo(rotationBeforePause));
        }

        [Test]
        public void IceFieldView_EnsureVisualCapacity_AddsTiltSlotsForLargerLaterStage()
        {
            using var fixture = new HitTiltFixture(field, clock);
            var largeConfig = new IceFieldConfig(
                maxActiveIceCount: 56,
                maxSpecialIceCount: 0,
                hitRadiusReferencePixels: 56f,
                minimumSpawnDistanceReferencePixels: 1f,
                respawnProtectionSeconds: 0.25f,
                iceDefinitions: new[] { new IceDefinition(IceTier.T1, "백빙", 10f, 10L) },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());
            field.Reconfigure(
                largeConfig,
                new IceSpawnPositioner(new Rect(0f, 0f, 960f, 540f), 1f),
                nextCriticalStrike: null,
                nextSupportConfig: null,
                nextChainConfig: null);
            field.Initialize(0d);

            InvokeViewMethod(fixture.View, "EnsureVisualCapacity");

            Assert.That(field.ActiveIce, Has.Count.EqualTo(56));
            Assert.That(fixture.Root.GetComponentsInChildren<SpriteRenderer>(), Has.Length.EqualTo(56));

            var lastIce = field.ActiveIce[55];
            InvokeViewMethod(
                fixture.View,
                "HandleDamageApplied",
                CreateDamageEvent(lastIce.IceInstanceId, EffectType.Click, remainingHp: 1f));
            var lastRenderer = fixture.Root.transform.GetChild(55).GetComponent<SpriteRenderer>();
            Assert.That(lastRenderer, Is.Not.Null);
            Assert.That(
                Mathf.Abs(Mathf.DeltaAngle(0f, lastRenderer!.transform.localEulerAngles.z)),
                Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void IceFieldView_HitTilt_FatalDamageAndLifecycleResetsRestoreIdentity()
        {
            using var fixture = new HitTiltFixture(field, clock);
            StartHitTilt(fixture);
            InvokeViewMethod(
                fixture.View,
                "HandleDamageApplied",
                CreateDamageEvent(fixture.IceInstanceId, EffectType.Click, remainingHp: 0f));
            Assert.That(fixture.Renderer.transform.localRotation, Is.EqualTo(Quaternion.identity));

            StartHitTilt(fixture);
            InvokeViewMethod(fixture.View, "HandleIceRespawnStateChanged", 0);
            Assert.That(fixture.Renderer.transform.localRotation, Is.EqualTo(Quaternion.identity));

            StartHitTilt(fixture);
            var ice = field.ActiveIce[0];
            var replacementIceInstanceId = fixture.IceInstanceId + 1000L;
            ice.Reset(
                replacementIceInstanceId,
                ice.Tier,
                ice.SpecialType,
                ice.MaxHp,
                ice.ReferencePosition,
                spawnTime: 0d,
                ice.VisualDiameterReferencePixels);
            InvokeViewMethod(fixture.View, "UpdateHitTilts", 0.01f);
            Assert.That(fixture.Renderer.transform.localRotation, Is.EqualTo(Quaternion.identity));

            StartHitTilt(fixture, replacementIceInstanceId);
            InvokeViewMethod(fixture.View, "HandleIceRespawned", 0);
            Assert.That(fixture.Renderer.transform.localRotation, Is.EqualTo(Quaternion.identity));

            StartHitTilt(fixture, replacementIceInstanceId);
            InvokeViewMethod(fixture.View, "OnDisable");
            Assert.That(fixture.Renderer.transform.localRotation, Is.EqualTo(Quaternion.identity));

            StartHitTilt(fixture, replacementIceInstanceId);
            fixture.View.ResetStage();
            Assert.That(fixture.Renderer.transform.localRotation, Is.EqualTo(Quaternion.identity));
        }

        private static void StartHitTilt(HitTiltFixture fixture)
        {
            StartHitTilt(fixture, fixture.IceInstanceId);
        }

        private static void StartHitTilt(HitTiltFixture fixture, long iceInstanceId)
        {
            InvokeViewMethod(
                fixture.View,
                "HandleDamageApplied",
                CreateDamageEvent(iceInstanceId, EffectType.Click, remainingHp: 1f));
            Assert.That(fixture.Renderer.transform.localRotation, Is.Not.EqualTo(Quaternion.identity));
        }

        private static DamageAppliedEvent CreateDamageEvent(
            long iceInstanceId,
            EffectType effectType,
            float remainingHp)
        {
            return new DamageAppliedEvent(
                stageId: 1L,
                iceInstanceId: iceInstanceId,
                chainId: 0L,
                chainDepth: 0,
                effectType: effectType,
                damage: 1f,
                remainingHp: remainingHp,
                wasCritical: false,
                referencePosition: Vector2.zero,
                stageElapsedSeconds: 0d);
        }

        private static void SetViewField(IceFieldView view, string fieldName, object value)
        {
            var fieldInfo = typeof(IceFieldView).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(fieldInfo, Is.Not.Null);
            fieldInfo!.SetValue(view, value);
        }

        private static void InvokeViewMethod(IceFieldView view, string methodName, params object[] parameters)
        {
            var method = typeof(IceFieldView).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(view, parameters);
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
            int destroyedEventCount = 0;
            int respawnEventCount = 0;
            testField.DamageApplied += _ => damageEventCount++;
            testField.IceDestroyed += _ => destroyedEventCount++;
            testField.IceRespawned += _ => respawnEventCount++;

            // Click should be ignored
            var clicked = testField.ApplyClickAt(new Vector2(100, 100), 10f, EffectType.Click, 60d);
            
            Assert.That(clicked, Is.False, "ApplyClickAt should return false after duration has passed.");
            Assert.That(damageEventCount, Is.EqualTo(0), "No damage events should be fired after combat ends.");
            Assert.That(destroyedEventCount, Is.Zero, "No destroy events should be fired after combat ends.");
            Assert.That(respawnEventCount, Is.Zero, "No respawns should occur after combat ends.");
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
            mockClock.StageElapsedSeconds = 0.12d;
            testField.UpdateRespawns();
            mockClock.StageElapsedSeconds = 0.3d;
            testField.UpdateRespawns();

            Assert.That(damageCount, Is.EqualTo(1));
            Assert.That(destroyCount, Is.EqualTo(1));
            Assert.That(target.IceInstanceId, Is.Not.EqualTo(oldId));
            Assert.That(target.IsDestroyed, Is.False);
            Assert.That(target.RemainingHp, Is.EqualTo(10f));
        }

        [Test]
        public void RespawnLifecycle_PausesThenActivatesWithNewIdAndSpawnTime()
        {
            var lifecycleConfig = new IceFieldConfig(
                maxActiveIceCount: 1,
                maxSpecialIceCount: 0,
                visualDiameterMinimumReferencePixels: 34f,
                visualDiameterMaximumReferencePixels: 42f,
                iceCollisionRadiusReferencePixels: 40f,
                strictExtraVisualGapReferencePixels: 18f,
                relaxedExtraVisualGapReferencePixels: 12f,
                outerMarginReferencePixels: 20f,
                protectedAreaPaddingReferencePixels: 21f,
                recentDestructionExclusionReferencePixels: 160f,
                recentDestructionExclusionSeconds: 1f,
                respawnGapSeconds: 0.12f,
                spawnAnimationSeconds: 0.18f,
                chainRespawnStaggerSeconds: 0.03f,
                respawnProtectionSeconds: 0.25f,
                iceDefinitions: new[] { new IceDefinition(IceTier.T1, "백빙", 10f, 10L) },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());
            var lifecycleClock = new MockClock();
            var lifecycleField = new IceField(
                1L,
                lifecycleConfig,
                new IceIdGenerator(),
                new IceSpawnPositioner(new Rect(0f, 0f, 960f, 540f), 18f, 12f),
                lifecycleClock);
            lifecycleField.Initialize(0d);

            var target = lifecycleField.ActiveIce[0];
            var previousId = target.IceInstanceId;
            lifecycleField.ApplyClickAt(target.ReferencePosition, 10f, EffectType.Click, 0d);

            Assert.That(target.RespawnState, Is.EqualTo(IceRespawnState.RespawnGap));
            Assert.That(target.IceInstanceId, Is.EqualTo(previousId));
            Assert.That(lifecycleField.ReservedSpawnCount, Is.Zero);

            lifecycleClock.StageElapsedSeconds = 0.12d;
            lifecycleClock.IsPaused = true;
            lifecycleField.UpdateRespawns();
            Assert.That(target.RespawnState, Is.EqualTo(IceRespawnState.RespawnGap));

            lifecycleClock.IsPaused = false;
            lifecycleField.UpdateRespawns();
            Assert.That(target.RespawnState, Is.EqualTo(IceRespawnState.SpawnAnimating));
            Assert.That(target.IceInstanceId, Is.EqualTo(previousId));
            Assert.That(target.VisualIceInstanceId, Is.Not.EqualTo(previousId));
            Assert.That(target.HasPendingSpawn, Is.True);
            Assert.That(lifecycleField.ReservedSpawnCount, Is.EqualTo(1));

            lifecycleClock.StageElapsedSeconds = 0.3d;
            lifecycleField.UpdateRespawns();
            Assert.That(target.RespawnState, Is.EqualTo(IceRespawnState.Active));
            Assert.That(target.IceInstanceId, Is.Not.EqualTo(previousId));
            Assert.That(target.SpawnTime, Is.EqualTo(0.3d));
            Assert.That(lifecycleField.ReservedSpawnCount, Is.Zero);
            Assert.That(lifecycleField.ApplyClickAt(target.ReferencePosition, 1f, EffectType.Click, 0.3d), Is.True);

            for (var i = 0; i < 9; i++)
            {
                lifecycleField.ApplyClickAt(target.ReferencePosition, 1f, EffectType.Click, 0.3d);
            }

            lifecycleClock.StageElapsedSeconds = 0.42d;
            lifecycleField.UpdateRespawns();
            Assert.That(lifecycleField.ReservedSpawnCount, Is.EqualTo(1));

            lifecycleClock.Phase = GamePhase.StageEnding;
            lifecycleField.UpdateRespawns();
            Assert.That(lifecycleField.QueuedRespawnCount, Is.Zero);
            Assert.That(lifecycleField.ReservedSpawnCount, Is.Zero);
            Assert.That(target.HasPendingSpawn, Is.False);
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
            Assert.That(supportDamageEvents[0].IceInstanceId, Is.EqualTo(clickTarget.IceInstanceId),
                "Support should target the closest alive ice, including the direct target.");
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
            Assert.That(supportDamageEvents[0].IceInstanceId, Is.EqualTo(clickTarget.IceInstanceId),
                "S03 without special ice should select the highest remaining HP after direct hits.");
            Assert.That(supportDamageEvents[0].Damage, Is.EqualTo(10f),
                "Normal ice should not get ×2 multiplier.");
        }

        [Test]
        public void CursorAreaTick_HitsEveryIceWhoseCollisionCircleOverlaps()
        {
            var areaClock = new MockClock();
            var testField = CreateAreaTestField(3, 10f, areaClock);
            var center = new Vector2(100f, 100f);
            var first = testField.ActiveIce[0];
            var second = testField.ActiveIce[1];
            var outside = testField.ActiveIce[2];
            first.Reset(first.IceInstanceId, IceTier.T1, SpecialIceType.None, 100f, center, 0d);
            second.Reset(second.IceInstanceId, IceTier.T1, SpecialIceType.None, 100f, new Vector2(130f, 100f), 0d);
            outside.Reset(outside.IceInstanceId, IceTier.T1, SpecialIceType.None, 100f, new Vector2(131f, 100f), 0d);

            var directEvents = new List<DamageAppliedEvent>();
            testField.DamageApplied += e =>
            {
                if (e.EffectType == EffectType.CursorAreaPulse)
                {
                    directEvents.Add(e);
                }
            };

            var hitCount = testField.ApplyAreaTickAt(center, 20f, 10f, 1d);

            Assert.That(hitCount, Is.EqualTo(2));
            Assert.That(directEvents.Count, Is.EqualTo(2));
            Assert.That(directEvents[0].IceInstanceId, Is.EqualTo(first.IceInstanceId));
            Assert.That(directEvents[1].IceInstanceId, Is.EqualTo(second.IceInstanceId));
            Assert.That(directEvents[0].Damage, Is.EqualTo(10f));
            Assert.That(directEvents[1].Damage, Is.EqualTo(10f));
            Assert.That(directEvents[0].ChainId, Is.EqualTo(directEvents[1].ChainId));
            Assert.That(directEvents[0].ChainDepth, Is.EqualTo(0));
            Assert.That(directEvents[1].ChainDepth, Is.EqualTo(0));
            Assert.That(outside.RemainingHp, Is.EqualTo(100f));
        }

        [Test]
        public void CursorAreaTick_RollsCriticalForEachOverlappedTarget()
        {
            var areaClock = new MockClock();
            var testField = CreateAreaTestField(
                2,
                10f,
                areaClock,
                criticalStrike: new CriticalStrike(1f, 3f));
            var first = testField.ActiveIce[0];
            var second = testField.ActiveIce[1];
            first.Reset(first.IceInstanceId, IceTier.T1, SpecialIceType.None, 100f, new Vector2(100f, 100f), 0d);
            second.Reset(second.IceInstanceId, IceTier.T1, SpecialIceType.None, 100f, new Vector2(110f, 100f), 0d);

            var directEvents = new List<DamageAppliedEvent>();
            testField.DamageApplied += e =>
            {
                if (e.EffectType == EffectType.CursorAreaPulse)
                {
                    directEvents.Add(e);
                }
            };

            testField.ApplyAreaTickAt(new Vector2(100f, 100f), 20f, 10f, 1d);

            Assert.That(directEvents.Count, Is.EqualTo(2));
            Assert.That(directEvents[0].WasCritical, Is.True);
            Assert.That(directEvents[1].WasCritical, Is.True);
            Assert.That(directEvents[0].Damage, Is.EqualTo(30f));
            Assert.That(directEvents[1].Damage, Is.EqualTo(30f));
        }

        [Test]
        public void CursorAreaTick_IgnoresRespawnProtectionAndChargesSupportOnce()
        {
            var areaClock = new MockClock();
            var supportConfig = new SupportAttackConfig(
                enabled: true,
                requiredDirectHitCount: 2,
                primaryDamageMultiplier: 1f,
                additionalTargetCount: 0,
                additionalDamageMultiplier: 0.7f,
                prioritizeSpecialIce: false,
                specialIceDamageMultiplier: 2f);
            var testField = CreateAreaTestField(2, 10f, areaClock, supportConfig: supportConfig,
                respawnProtectionSeconds: 1f);
            var first = testField.ActiveIce[0];
            var second = testField.ActiveIce[1];
            first.Reset(first.IceInstanceId, IceTier.T1, SpecialIceType.None, 100f, new Vector2(100f, 100f), 0.75d);
            second.Reset(second.IceInstanceId, IceTier.T1, SpecialIceType.None, 100f, new Vector2(110f, 100f), 0.75d);

            var directEvents = 0;
            var chargeEvents = new List<SupportChargeChangedEvent>();
            testField.DamageApplied += e =>
            {
                if (e.EffectType == EffectType.CursorAreaPulse)
                {
                    directEvents++;
                }
            };
            testField.SupportChargeChanged += e => chargeEvents.Add(e);

            testField.ApplyAreaTickAt(new Vector2(100f, 100f), 20f, 10f, 1d);

            Assert.That(directEvents, Is.EqualTo(2));
            Assert.That(chargeEvents.Count, Is.EqualTo(1));
            Assert.That(chargeEvents[0].CurrentCharge, Is.EqualTo(1));
        }

        [Test]
        public void CursorAreaTick_QueuesAllDirectDamageBeforeOverkill()
        {
            var chainConfig = new ChainEffectConfig(
                overkillEnabled: true,
                overkillTransferMultiplier: 0.5f,
                hullFragmentDamageMultiplier: 0f,
                hullFragmentRadiusReferencePixels: 0f,
                crystalShardCount: 0,
                crackDamageMultiplier: 0f,
                crackRadiusReferencePixels: 0f,
                iceCollapseEnabled: false,
                iceCollapseRequiredDestroyCount: 5,
                iceCollapseDamageMultiplier: 0f,
                iceCollapseRadiusReferencePixels: 0f,
                maxChainDepth: 3);
            var testField = CreateAreaTestField(2, 10f, new MockClock(), chainConfig: chainConfig);
            var first = testField.ActiveIce[0];
            var second = testField.ActiveIce[1];
            first.Reset(first.IceInstanceId, IceTier.T1, SpecialIceType.None, 1f, new Vector2(100f, 100f), 0d);
            second.Reset(second.IceInstanceId, IceTier.T1, SpecialIceType.None, 100f, new Vector2(110f, 100f), 0d);

            var damageOrder = new List<EffectType>();
            testField.DamageApplied += e =>
            {
                if (e.EffectType == EffectType.CursorAreaPulse || e.EffectType == EffectType.Overkill)
                {
                    damageOrder.Add(e.EffectType);
                }
            };

            testField.ApplyAreaTickAt(new Vector2(100f, 100f), 20f, 10f, 1d);

            Assert.That(damageOrder, Is.EqualTo(new[]
            {
                EffectType.CursorAreaPulse,
                EffectType.CursorAreaPulse,
                EffectType.Overkill
            }));
        }

        [Test]
        public void AttackTickScheduler_FiresImmediatelyThenCapsRecoveryTicks()
        {
            var scheduler = new AttackTickScheduler(5f);

            Assert.That(scheduler.Update(0f), Is.EqualTo(1));
            Assert.That(scheduler.Update(0.2f), Is.EqualTo(1));
            Assert.That(scheduler.Update(1.1f), Is.EqualTo(3));
            Assert.That(scheduler.Update(0f), Is.EqualTo(0));
        }

        [TestCase(5f, 300)]
        [TestCase(6.25f, 375)]
        [TestCase(7.5f, 450)]
        [TestCase(8.75f, 525)]
        public void AttackTickScheduler_SixtySecondsStaysWithinOneTick(
            float attacksPerSecond,
            int expectedTicks)
        {
            var scheduler = new AttackTickScheduler(attacksPerSecond);
            var totalTicks = 0;
            for (var frame = 0; frame < 60 * 60; frame++)
            {
                var ticks = scheduler.Update(frame == 0 ? 0f : 1f / 60f);
                Assert.That(ticks, Is.InRange(0, 3));
                totalTicks += ticks;
            }

            Assert.That(totalTicks, Is.EqualTo(expectedTicks).Within(1));
        }

        [Test]
        public void AreaTick_PauseAndStageEndDoNotDamageOrChargeSupport()
        {
            var supportConfig = CreateBasicSupportConfig();
            var testClock = new MockClock();
            var testField = CreateAreaTestField(1, 10f, testClock, supportConfig);
            var damageCount = 0;
            var chargeCount = 0;
            testField.DamageApplied += _ => damageCount++;
            testField.SupportChargeChanged += _ => chargeCount++;
            var position = testField.ActiveIce[0].ReferencePosition;

            testClock.IsPaused = true;
            Assert.That(testField.ApplyAreaTickAt(position, 20f, 10f, 1d), Is.Zero);
            testClock.IsPaused = false;
            testClock.StageElapsedSeconds = testClock.DurationSeconds;
            Assert.That(testField.ApplyAreaTickAt(position, 20f, 10f, 60d), Is.Zero);

            Assert.That(damageCount, Is.Zero);
            Assert.That(chargeCount, Is.Zero);
        }

        private static IceField CreateAreaTestField(
            int iceCount,
            float collisionRadius,
            MockClock clock,
            SupportAttackConfig? supportConfig = null,
            ChainEffectConfig? chainConfig = null,
            CriticalStrike? criticalStrike = null,
            float respawnProtectionSeconds = 0f)
        {
            var areaConfig = new IceFieldConfig(
                maxActiveIceCount: iceCount,
                maxSpecialIceCount: 0,
                hitRadiusReferencePixels: collisionRadius,
                minimumSpawnDistanceReferencePixels: 1f,
                respawnProtectionSeconds: respawnProtectionSeconds,
                iceDefinitions: new[] { new IceDefinition(IceTier.T1, "백빙", 100f, 10L) },
                spawnWeights: new[] { new IceSpawnWeight(IceTier.T1, 100) },
                specialDefinitions: Array.Empty<SpecialIceDefinition>());
            var testField = new IceField(
                1L,
                areaConfig,
                new IceIdGenerator(),
                new IceSpawnPositioner(new Rect(0f, 0f, 960f, 540f), 1f),
                clock,
                criticalStrike,
                supportConfig,
                chainConfig);
            testField.Initialize(0d);
            return testField;
        }

        [Test]
        public void D03_OverkillTransfer_AppliesToClosestAliveIce()
        {
            var chainConfig = new ChainEffectConfig(
                overkillEnabled: true,
                overkillTransferMultiplier: 0.5f,
                hullFragmentDamageMultiplier: 0f,
                hullFragmentRadiusReferencePixels: 0f,
                crystalShardCount: 0,
                crackDamageMultiplier: 0f,
                crackRadiusReferencePixels: 0f,
                iceCollapseEnabled: false,
                iceCollapseRequiredDestroyCount: 5,
                iceCollapseDamageMultiplier: 0f,
                iceCollapseRadiusReferencePixels: 0f,
                maxChainDepth: 3);

            var testField = new IceField(1L, config, new IceIdGenerator(), new IceSpawnPositioner(new Rect(0,0,960,540), 1f), new MockClock(),
                chainConfig: chainConfig);
            testField.Initialize(0d);

            var target1 = testField.ActiveIce[0];
            target1.Reset(target1.IceInstanceId, IceTier.T1, SpecialIceType.None, 10f, new Vector2(100, 100), 0d);
            
            var target2 = testField.ActiveIce[1];
            target2.Reset(target2.IceInstanceId, IceTier.T1, SpecialIceType.None, 10f, new Vector2(110, 100), 0d); // Close
            var target2Id = target2.IceInstanceId;
            
            var target3 = testField.ActiveIce[2];
            target3.Reset(target3.IceInstanceId, IceTier.T1, SpecialIceType.None, 10f, new Vector2(500, 500), 0d); // Far

            var damageEvents = new List<DamageAppliedEvent>();
            testField.DamageApplied += e => { if (e.EffectType == EffectType.Overkill) damageEvents.Add(e); };

            // Apply 30 damage to a 10 HP target -> Overkill is 20.
            // Ratio is 0.5 -> 10 damage transferred to closest.
            testField.ApplyClickAt(new Vector2(100, 100), 30f, EffectType.Click, 10d);

            Assert.That(damageEvents.Count, Is.EqualTo(1));
            Assert.That(damageEvents[0].IceInstanceId, Is.EqualTo(target2Id));
            Assert.That(damageEvents[0].Damage, Is.EqualTo(10f));
            Assert.That(damageEvents[0].ChainDepth, Is.EqualTo(1));
        }

        [Test]
        public void H03_IceCollapse_TriggersOn5thDestruction()
        {
            var chainConfig = new ChainEffectConfig(
                overkillEnabled: false, overkillTransferMultiplier: 0f,
                hullFragmentDamageMultiplier: 0f, hullFragmentRadiusReferencePixels: 0f,
                crystalShardCount: 0, crackDamageMultiplier: 0f, crackRadiusReferencePixels: 0f,
                iceCollapseEnabled: true, iceCollapseRequiredDestroyCount: 5,
                iceCollapseDamageMultiplier: 1.5f, iceCollapseRadiusReferencePixels: 140f,
                maxChainDepth: 3);

            var testField = CreateAreaTestField(7, 10f, new MockClock(), chainConfig: chainConfig);
            for (var i = 0; i < 6; i++)
            {
                var ice = testField.ActiveIce[i];
                ice.Reset(ice.IceInstanceId, IceTier.T1, SpecialIceType.None, 10f, new Vector2(100 + i, 100), 0d);
            }

            var collapseTarget = testField.ActiveIce[6];
            collapseTarget.Reset(
                collapseTarget.IceInstanceId,
                IceTier.T1,
                SpecialIceType.None,
                100f,
                new Vector2(200, 100),
                0d);
            var collapseTargetId = collapseTarget.IceInstanceId;

            var collapseEvents = new List<DamageAppliedEvent>();
            testField.DamageApplied += e =>
            {
                if (e.EffectType == EffectType.IceCollapse)
                {
                    collapseEvents.Add(e);
                }
            };

            testField.ApplyAreaTickAt(new Vector2(100, 100), 30f, 10f, 1d);

            var collapseTargetEvent = collapseEvents.Find(e => e.IceInstanceId == collapseTargetId);
            Assert.That(collapseTargetEvent, Is.Not.Null);
            Assert.That(collapseTargetEvent!.Damage, Is.EqualTo(15f), "10 * 1.5 = 15");
        }

        [Test]
        public void Depth3_StopsFurtherChain()
        {
            var chainConfig = new ChainEffectConfig(
                overkillEnabled: true, overkillTransferMultiplier: 1.0f,
                hullFragmentDamageMultiplier: 0f, hullFragmentRadiusReferencePixels: 0f,
                crystalShardCount: 0, crackDamageMultiplier: 0f, crackRadiusReferencePixels: 0f,
                iceCollapseEnabled: false, iceCollapseRequiredDestroyCount: 5,
                iceCollapseDamageMultiplier: 0f, iceCollapseRadiusReferencePixels: 0f,
                maxChainDepth: 3);

            var testField = new IceField(1L, config, new IceIdGenerator(), new IceSpawnPositioner(new Rect(0,0,960,540), 1f), new MockClock(),
                chainConfig: chainConfig);
            testField.Initialize(0d);

            for (var i = 0; i < 5; i++)
            {
                var ice = testField.ActiveIce[i];
                ice.Reset(ice.IceInstanceId, IceTier.T1, SpecialIceType.None, 10f, new Vector2(100 + i, 100), 0d);
            }

            var damageEvents = new List<DamageAppliedEvent>();
            testField.DamageApplied += e => { damageEvents.Add(e); };

            // 50 damage -> 1(0) -> 2(1) -> 3(2) -> 4(3) -> 5(4? NO, depth 3 stops).
            testField.ApplyClickAt(new Vector2(100, 100), 50f, EffectType.Click, 10d);

            // Targets: 
            // Depth 0: target 0 receives 50, destroyed. Overkill 40.
            // Depth 1: target 1 receives 40, destroyed. Overkill 30.
            // Depth 2: target 2 receives 30, destroyed. Overkill 20.
            // Depth 3: target 3 receives 20, destroyed. Overkill 10.
            // But target 3 was destroyed AT depth 3. It should NOT trigger Overkill (which would be depth 4).
            // So target 4 should not be damaged.
            
            var target4Damage = damageEvents.FindAll(e => e.IceInstanceId == testField.ActiveIce[4].IceInstanceId);
            Assert.That(target4Damage.Count, Is.EqualTo(0), "Target 4 should not be damaged because depth limit is 3.");
        }

        [Test]
        public void AreaTick_D03_DoesNotHitRespawnedIce()
        {
            var chainConfig = new ChainEffectConfig(
                overkillEnabled: true, overkillTransferMultiplier: 0.5f,
                hullFragmentDamageMultiplier: 0f, hullFragmentRadiusReferencePixels: 0f,
                crystalShardCount: 0, crackDamageMultiplier: 0f, crackRadiusReferencePixels: 0f,
                iceCollapseEnabled: false, iceCollapseRequiredDestroyCount: 5,
                iceCollapseDamageMultiplier: 0f, iceCollapseRadiusReferencePixels: 0f,
                maxChainDepth: 3);
            var testField = CreateAreaTestField(2, 10f, new MockClock(), chainConfig: chainConfig);
            var first = testField.ActiveIce[0];
            var second = testField.ActiveIce[1];
            first.Reset(first.IceInstanceId, IceTier.T1, SpecialIceType.None, 1f, new Vector2(100f, 100f), 0d);
            second.Reset(second.IceInstanceId, IceTier.T1, SpecialIceType.None, 1f, new Vector2(110f, 100f), 0d);
            var originalIds = new HashSet<long> { first.IceInstanceId, second.IceInstanceId };
            var destroyedIds = new List<long>();
            var chainDamageIds = new List<long>();
            testField.IceDestroyed += e => destroyedIds.Add(e.IceInstanceId);
            testField.DamageApplied += e =>
            {
                if (e.EffectType == EffectType.Overkill)
                {
                    chainDamageIds.Add(e.IceInstanceId);
                }
            };

            var hitCount = testField.ApplyAreaTickAt(new Vector2(100f, 100f), 20f, 3f, 1d);

            Assert.That(hitCount, Is.EqualTo(2));
            Assert.That(destroyedIds, Is.EquivalentTo(originalIds));
            Assert.That(chainDamageIds, Is.Empty, "Queued D03 damage must not hit respawned ice.");
        }

        [Test]
        public void D03_OverkillSkipsRespawnProtectedTarget()
        {
            var chainConfig = new ChainEffectConfig(
                overkillEnabled: true, overkillTransferMultiplier: 0.5f,
                hullFragmentDamageMultiplier: 0f, hullFragmentRadiusReferencePixels: 0f,
                crystalShardCount: 0, crackDamageMultiplier: 0f, crackRadiusReferencePixels: 0f,
                iceCollapseEnabled: false, iceCollapseRequiredDestroyCount: 5,
                iceCollapseDamageMultiplier: 0f, iceCollapseRadiusReferencePixels: 0f,
                maxChainDepth: 3);
            var testClock = new MockClock { StageElapsedSeconds = 1d };
            var testField = CreateAreaTestField(
                2,
                10f,
                testClock,
                chainConfig: chainConfig,
                respawnProtectionSeconds: 0.25f);
            var source = testField.ActiveIce[0];
            var protectedTarget = testField.ActiveIce[1];
            source.Reset(source.IceInstanceId, IceTier.T1, SpecialIceType.None, 1f, new Vector2(100f, 100f), 0d);
            protectedTarget.Reset(
                protectedTarget.IceInstanceId,
                IceTier.T1,
                SpecialIceType.None,
                10f,
                new Vector2(110f, 100f),
                0.9d);
            var overkillCount = 0;
            testField.DamageApplied += e =>
            {
                if (e.EffectType == EffectType.Overkill)
                {
                    overkillCount++;
                }
            };

            testField.ApplyClickAt(new Vector2(100f, 100f), 3f, EffectType.Click, 1d);

            Assert.That(overkillCount, Is.Zero);
            Assert.That(protectedTarget.RemainingHp, Is.EqualTo(10f));
        }

        [Test]
        public void CrackExplosion_UsesConfiguredTotalMultiplierOnce()
        {
            var chainConfig = new ChainEffectConfig(
                overkillEnabled: false, overkillTransferMultiplier: 0f,
                hullFragmentDamageMultiplier: 0f, hullFragmentRadiusReferencePixels: 0f,
                crystalShardCount: 0, crackDamageMultiplier: 3f, crackRadiusReferencePixels: 120f,
                iceCollapseEnabled: false, iceCollapseRequiredDestroyCount: 5,
                iceCollapseDamageMultiplier: 0f, iceCollapseRadiusReferencePixels: 0f,
                maxChainDepth: 3);
            var testField = CreateAreaTestField(2, 10f, new MockClock(), chainConfig: chainConfig);
            var crack = testField.ActiveIce[0];
            var target = testField.ActiveIce[1];
            crack.Reset(crack.IceInstanceId, IceTier.T1, SpecialIceType.Crack, 10f, new Vector2(100f, 100f), 0d);
            target.Reset(target.IceInstanceId, IceTier.T1, SpecialIceType.None, 100f, new Vector2(150f, 100f), 0d);
            DamageAppliedEvent? crackDamage = null;
            testField.DamageApplied += e =>
            {
                if (e.EffectType == EffectType.CrackExplosion)
                {
                    crackDamage = e;
                }
            };

            testField.ApplyClickAt(new Vector2(100f, 100f), 10f, EffectType.Click, 1d);

            Assert.That(crackDamage.HasValue, Is.True);
            Assert.That(crackDamage!.Value.Damage, Is.EqualTo(30f));
        }

        [Test]
        public void QueueProcessing_StageEndingCancelsRemainingDamageAndRespawns()
        {
            var testClock = new MockClock();
            var testField = CreateAreaTestField(2, 10f, testClock);
            var first = testField.ActiveIce[0];
            var second = testField.ActiveIce[1];
            first.Reset(first.IceInstanceId, IceTier.T1, SpecialIceType.None, 10f, new Vector2(100f, 100f), 0d);
            second.Reset(second.IceInstanceId, IceTier.T1, SpecialIceType.None, 10f, new Vector2(110f, 100f), 0d);
            var damageCount = 0;
            var destroyedCount = 0;
            var respawnCount = 0;
            testField.DamageApplied += _ =>
            {
                damageCount++;
                if (damageCount == 1)
                {
                    testClock.Phase = GamePhase.StageEnding;
                    testClock.StageElapsedSeconds = testClock.DurationSeconds;
                }
            };
            testField.IceDestroyed += _ => destroyedCount++;
            testField.IceRespawned += _ => respawnCount++;

            testField.ApplyAreaTickAt(new Vector2(100f, 100f), 20f, 10f, 1d);

            Assert.That(damageCount, Is.EqualTo(1));
            Assert.That(destroyedCount, Is.EqualTo(1));
            Assert.That(respawnCount, Is.Zero);
            Assert.That(second.RemainingHp, Is.EqualTo(10f));
        }
    }
}
