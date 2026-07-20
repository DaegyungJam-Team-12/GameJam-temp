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
                field.ApplyClickAt(pos, 1f, i * 0.1d);
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
                field.ApplyClickAt(pos, 1f, i * 0.1d);
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
            var result = field.ApplyClickAt(new Vector2(-9999f, -9999f), 1f, 0d);
            Assert.That(result, Is.False);
        }

        [Test]
        public void IceDestroyedEvent_PublishedExactlyOnce()
        {
            var target = field.ActiveIce[0];
            var pos = target.ReferencePosition;

            for (var i = 0; i < 10; i++)
            {
                field.ApplyClickAt(pos, 1f, i * 0.1d);
            }

            Assert.That(destroyedCount, Is.EqualTo(1));
            Assert.That(lastDestroyed.Tier, Is.EqualTo(IceTier.T1));
            Assert.That(lastDestroyed.DestroyCategory, Is.EqualTo(DestroyCategory.Direct));
        }
    }
}
