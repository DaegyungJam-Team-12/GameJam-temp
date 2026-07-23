#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using UnityEngine;

namespace Icebreaker.Gameplay
{
    public sealed class SupportAttackProcessor
    {
        private readonly long stageId;
        private SupportAttackConfig? config;
        
        private int chargeCount;
        private readonly List<IceInstance> tempTargets = new List<IceInstance>(32);

        // Callbacks to interact with IceField without allocations
        private readonly Action<SupportChargeChangedEvent> onChargeChanged;
        private readonly Action<IceInstance, float, EffectType, DestroyCategory, bool, int> onEnqueueDamage;
        private readonly Action<double> onProcessQueue;

        public SupportAttackProcessor(
            long stageId,
            SupportAttackConfig? config,
            Action<SupportChargeChangedEvent> onChargeChanged,
            Action<IceInstance, float, EffectType, DestroyCategory, bool, int> onEnqueueDamage,
            Action<double> onProcessQueue)
        {
            this.stageId = stageId;
            this.config = config;
            this.onChargeChanged = onChargeChanged;
            this.onEnqueueDamage = onEnqueueDamage;
            this.onProcessQueue = onProcessQueue;
        }

        public void Reconfigure(SupportAttackConfig? newConfig)
        {
            config = newConfig;
        }

        public void Reset()
        {
            chargeCount = 0;
        }

        public void ChargeForDirectTick(Vector2 firePosition, double stageElapsedSeconds, float lastClickDamage, IReadOnlyList<IceInstance> activeIce, float respawnProtectionSeconds)
        {
            if (config == null || !config.Enabled)
            {
                return;
            }

            chargeCount++;
            if (chargeCount >= config.RequiredDirectHitCount)
            {
                chargeCount = 0;
                onChargeChanged(new SupportChargeChangedEvent(stageId, chargeCount, config.RequiredDirectHitCount));
                FireSupport(firePosition, stageElapsedSeconds, lastClickDamage, activeIce, respawnProtectionSeconds);
                return;
            }

            onChargeChanged(new SupportChargeChangedEvent(stageId, chargeCount, config.RequiredDirectHitCount));
        }

        private void FireSupport(Vector2 firePosition, double stageElapsedSeconds, float lastClickDamage, IReadOnlyList<IceInstance> activeIce, float respawnProtectionSeconds)
        {
            if (config == null || !config.Enabled)
            {
                return;
            }

            var targets = SelectSupportTargets(firePosition, stageElapsedSeconds, activeIce, respawnProtectionSeconds);
            if (targets.Count == 0)
            {
                return;
            }

            // Primary target: full damage
            var primaryDamage = lastClickDamage * config.PrimaryDamageMultiplier;
            if (config.PrioritizeSpecialIce &&
                targets[0].SpecialType != SpecialIceType.None)
            {
                primaryDamage *= config.SpecialIceDamageMultiplier;
            }

            onEnqueueDamage(targets[0], primaryDamage, EffectType.SupportShot, DestroyCategory.Support, false, 0);

            // Additional targets (S02): reduced damage
            var additionalDamage = lastClickDamage * config.AdditionalDamageMultiplier;
            for (var i = 1; i < targets.Count; i++)
            {
                var damage = additionalDamage;
                if (config.PrioritizeSpecialIce &&
                    targets[i].SpecialType != SpecialIceType.None)
                {
                    damage *= config.SpecialIceDamageMultiplier;
                }

                onEnqueueDamage(targets[i], damage, EffectType.SupportShot, DestroyCategory.Support, false, 0);
            }

            onProcessQueue(stageElapsedSeconds);
        }

        private List<IceInstance> SelectSupportTargets(Vector2 firePosition, double stageElapsedSeconds, IReadOnlyList<IceInstance> activeIce, float respawnProtectionSeconds)
        {
            tempTargets.Clear();
            for (var i = 0; i < activeIce.Count; i++)
            {
                var ice = activeIce[i];
                if (ice.IsDestroyed) continue;

                // Respawn protection: non-direct damage excluded
                if (stageElapsedSeconds - ice.SpawnTime < respawnProtectionSeconds)
                {
                    continue;
                }

                tempTargets.Add(ice);
            }

            if (tempTargets.Count == 0)
            {
                return tempTargets;
            }

            var totalTargets = 1 + (config?.AdditionalTargetCount ?? 0);

            if (config != null && config.PrioritizeSpecialIce)
            {
                tempTargets.Sort(new SupportTargetComparer(firePosition));
            }
            else
            {
                tempTargets.Sort(new DistanceHpIdComparer(firePosition));
            }

            if (tempTargets.Count > totalTargets)
            {
                tempTargets.RemoveRange(totalTargets, tempTargets.Count - totalTargets);
            }

            return tempTargets;
        }

        private readonly struct SupportTargetComparer : IComparer<IceInstance>
        {
            private readonly Vector2 origin;
            public SupportTargetComparer(Vector2 origin) => this.origin = origin;
            public int Compare(IceInstance a, IceInstance b)
            {
                var specialA = a.SpecialType != SpecialIceType.None ? 1 : 0;
                var specialB = b.SpecialType != SpecialIceType.None ? 1 : 0;
                var specialCmp = specialB.CompareTo(specialA);
                if (specialCmp != 0) return specialCmp;

                var hpCmp = b.RemainingHp.CompareTo(a.RemainingHp);
                if (hpCmp != 0) return hpCmp;

                var distA = Vector2.SqrMagnitude(a.ReferencePosition - origin);
                var distB = Vector2.SqrMagnitude(b.ReferencePosition - origin);
                var distCmp = distA.CompareTo(distB);
                if (distCmp != 0) return distCmp;

                return a.IceInstanceId.CompareTo(b.IceInstanceId);
            }
        }

        private readonly struct DistanceHpIdComparer : IComparer<IceInstance>
        {
            private readonly Vector2 origin;
            public DistanceHpIdComparer(Vector2 origin) => this.origin = origin;
            public int Compare(IceInstance a, IceInstance b)
            {
                var distA = Vector2.SqrMagnitude(a.ReferencePosition - origin);
                var distB = Vector2.SqrMagnitude(b.ReferencePosition - origin);
                var distCmp = distA.CompareTo(distB);
                if (distCmp != 0) return distCmp;
                
                var hpCmp = b.RemainingHp.CompareTo(a.RemainingHp);
                if (hpCmp != 0) return hpCmp;
                
                return a.IceInstanceId.CompareTo(b.IceInstanceId);
            }
        }
    }
}
