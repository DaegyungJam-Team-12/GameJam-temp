#nullable enable

using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using NUnit.Framework;
using UnityEngine;

namespace Icebreaker.Gameplay.Tests
{
    public sealed class T1DestructionSandboxTests
    {
        private T1DestructionSandbox sandbox = null!;
        private int damageCount;
        private int destroyedCount;
        private DamageAppliedEvent lastDamage;
        private IceDestroyedEvent lastDestroyed;

        [SetUp]
        public void SetUp()
        {
            sandbox = new T1DestructionSandbox(7L, 42L, new Vector2(480f, 270f));
            damageCount = 0;
            destroyedCount = 0;
            sandbox.DamageApplied += CaptureDamage;
            sandbox.IceDestroyed += CaptureDestroyed;
        }

        [TearDown]
        public void TearDown()
        {
            sandbox.DamageApplied -= CaptureDamage;
            sandbox.IceDestroyed -= CaptureDestroyed;
        }

        [Test]
        public void TenClicks_DestroyT1AndPublishOneDestruction()
        {
            for (var i = 0; i < 10; i++)
            {
                Assert.That(sandbox.ApplyClick(i * 0.1d), Is.True);
            }

            Assert.That(sandbox.Target.IsDestroyed, Is.True);
            Assert.That(sandbox.Target.RemainingHp, Is.Zero);
            Assert.That(damageCount, Is.EqualTo(10));
            Assert.That(destroyedCount, Is.EqualTo(1));
            Assert.That(lastDestroyed.StageId, Is.EqualTo(7L));
            Assert.That(lastDestroyed.IceInstanceId, Is.EqualTo(42L));
            Assert.That(lastDestroyed.Tier, Is.EqualTo(IceTier.T1));
            Assert.That(lastDestroyed.SpecialType, Is.EqualTo(SpecialIceType.None));
            Assert.That(lastDestroyed.DestroyCategory, Is.EqualTo(DestroyCategory.Direct));
            Assert.That(lastDestroyed.EffectType, Is.EqualTo(EffectType.Click));
        }

        [Test]
        public void ClicksAfterDestruction_DoNotPublishDuplicateEvents()
        {
            for (var i = 0; i < 10; i++)
            {
                sandbox.ApplyClick(i * 0.1d);
            }

            Assert.That(sandbox.ApplyClick(2d), Is.False);
            Assert.That(sandbox.ApplyClick(3d), Is.False);

            Assert.That(damageCount, Is.EqualTo(10));
            Assert.That(destroyedCount, Is.EqualTo(1));
            Assert.That(sandbox.Target.RemainingHp, Is.Zero);
        }

        [Test]
        public void DamageEvents_ReportOneClickDamageWithoutCritical()
        {
            sandbox.ApplyClick(0.25d);

            Assert.That(damageCount, Is.EqualTo(1));
            Assert.That(lastDamage.Damage, Is.EqualTo(1f));
            Assert.That(lastDamage.RemainingHp, Is.EqualTo(9f));
            Assert.That(lastDamage.WasCritical, Is.False);
            Assert.That(lastDamage.EffectType, Is.EqualTo(EffectType.Click));
            Assert.That(lastDamage.ReferencePosition, Is.EqualTo(new Vector2(480f, 270f)));
        }

        private void CaptureDamage(DamageAppliedEvent e)
        {
            damageCount++;
            lastDamage = e;
        }

        private void CaptureDestroyed(IceDestroyedEvent e)
        {
            destroyedCount++;
            lastDestroyed = e;
        }
    }
}
