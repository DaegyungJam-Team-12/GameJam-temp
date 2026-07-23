#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using UnityEngine;

namespace Icebreaker.Gameplay
{
    public sealed class ChainEffectProcessor
    {
        private ChainEffectConfig? config;
        
        private int destroyedCountInCurrentChain;
        private bool hasTriggeredIceCollapseInCurrentChain;

        private readonly List<IceInstance> tempTargets = new List<IceInstance>(32);
        private readonly Action<IceInstance, float, EffectType, DestroyCategory, bool, int> onEnqueueDamage;

        public ChainEffectProcessor(
            ChainEffectConfig? config,
            Action<IceInstance, float, EffectType, DestroyCategory, bool, int> onEnqueueDamage)
        {
            this.config = config;
            this.onEnqueueDamage = onEnqueueDamage;
        }

        public void Reconfigure(ChainEffectConfig? newConfig)
        {
            config = newConfig;
        }

        public void StartNewChain()
        {
            destroyedCountInCurrentChain = 0;
            hasTriggeredIceCollapseInCurrentChain = false;
        }

        public void ProcessIceDestroyed(
            IceInstance destroyedTarget,
            int chainDepth,
            DestroyCategory category,
            float hpBeforeDamage,
            float appliedDamage,
            float lastClickDamage,
            double stageElapsedSeconds,
            IReadOnlyList<IceInstance> activeIce,
            float respawnProtectionSeconds)
        {
            destroyedCountInCurrentChain++;

            var maxChainDepth = config?.MaxChainDepth ?? 3;
            if (chainDepth < maxChainDepth)
            {
                // 1. D03 Overkill
                if (category == DestroyCategory.Direct && config != null && config.OverkillEnabled)
                {
                    var overkillDamage = appliedDamage - hpBeforeDamage;
                    if (overkillDamage > 0f)
                    {
                        var transferDamage = overkillDamage * config.OverkillTransferMultiplier;
                        var closest = FindClosestAliveExclude(
                            destroyedTarget.ReferencePosition,
                            destroyedTarget,
                            stageElapsedSeconds,
                            activeIce,
                            respawnProtectionSeconds);
                        if (closest != null)
                        {
                            onEnqueueDamage(closest, transferDamage, EffectType.Overkill, DestroyCategory.Chain, false, chainDepth + 1);
                        }
                    }
                }

                // 2. Special Ice
                if (destroyedTarget.SpecialType == SpecialIceType.Crystal)
                {
                    TriggerCrystalEffect(destroyedTarget, chainDepth + 1, stageElapsedSeconds, lastClickDamage, activeIce, respawnProtectionSeconds);
                }
                else if (destroyedTarget.SpecialType == SpecialIceType.Crack)
                {
                    TriggerCrackEffect(destroyedTarget, chainDepth + 1, stageElapsedSeconds, lastClickDamage, activeIce, respawnProtectionSeconds);
                }
                
                // 3. H01 Hull Fragment
                if (config != null && config.HullFragmentDamageMultiplier > 0f)
                {
                    TriggerHullFragment(destroyedTarget.ReferencePosition, chainDepth + 1, stageElapsedSeconds, lastClickDamage, activeIce, respawnProtectionSeconds);
                }
            }

            // 4. H03 Ice Collapse
            if (config != null && config.IceCollapseEnabled &&
                !hasTriggeredIceCollapseInCurrentChain &&
                destroyedCountInCurrentChain >= config.IceCollapseRequiredDestroyCount)
            {
                hasTriggeredIceCollapseInCurrentChain = true;
                TriggerIceCollapse(destroyedTarget.ReferencePosition, stageElapsedSeconds, lastClickDamage, activeIce, respawnProtectionSeconds);
            }
        }

        private void TriggerCrystalEffect(IceInstance source, int nextDepth, double stageElapsedSeconds, float lastClickDamage, IReadOnlyList<IceInstance> activeIce, float respawnProtectionSeconds)
        {
            var shardCount = config != null ? config.CrystalShardCount : 5;
            var targets = FindCrystalTargets(source, shardCount, stageElapsedSeconds, activeIce, respawnProtectionSeconds);
            
            foreach (var target in targets)
            {
                onEnqueueDamage(target, target.MaxHp * 2f, EffectType.CrystalShard, DestroyCategory.Chain, false, nextDepth);
            }
        }

        private List<IceInstance> FindCrystalTargets(IceInstance source, int count, double stageElapsedSeconds, IReadOnlyList<IceInstance> activeIce, float respawnProtectionSeconds)
        {
            tempTargets.Clear();
            for (var i = 0; i < activeIce.Count; i++)
            {
                var ice = activeIce[i];
                if (ice.IsDestroyed || ice == source) continue;

                if (stageElapsedSeconds - ice.SpawnTime < respawnProtectionSeconds)
                {
                    continue;
                }

                if (ice.Tier < source.Tier)
                {
                    tempTargets.Add(ice);
                }
            }

            tempTargets.Sort(new DistanceHpIdComparer(source.ReferencePosition));

            if (tempTargets.Count > count)
            {
                tempTargets.RemoveRange(count, tempTargets.Count - count);
            }

            return tempTargets;
        }

        private void TriggerCrackEffect(IceInstance source, int nextDepth, double stageElapsedSeconds, float lastClickDamage, IReadOnlyList<IceInstance> activeIce, float respawnProtectionSeconds)
        {
            var crackRadius = config != null ? config.CrackRadiusReferencePixels : 120f;
            var crackMultiplier = config != null ? config.CrackDamageMultiplier : 3.0f;
            
            var targets = FindCrackTargets(source, crackRadius, stageElapsedSeconds, activeIce, respawnProtectionSeconds);
            var damageAmount = lastClickDamage * crackMultiplier;

            foreach (var target in targets)
            {
                onEnqueueDamage(target, damageAmount, EffectType.CrackExplosion, DestroyCategory.Chain, false, nextDepth);
            }
        }

        private List<IceInstance> FindCrackTargets(IceInstance source, float radius, double stageElapsedSeconds, IReadOnlyList<IceInstance> activeIce, float respawnProtectionSeconds)
        {
            tempTargets.Clear();
            var radiusSqr = radius * radius;
            for (var i = 0; i < activeIce.Count; i++)
            {
                var ice = activeIce[i];
                if (ice.IsDestroyed || ice == source) continue;

                if (stageElapsedSeconds - ice.SpawnTime < respawnProtectionSeconds)
                {
                    continue;
                }

                var distSqr = Vector2.SqrMagnitude(ice.ReferencePosition - source.ReferencePosition);
                if (distSqr <= radiusSqr)
                {
                    tempTargets.Add(ice);
                }
            }

            tempTargets.Sort(new HpDistanceIdComparer(source.ReferencePosition));

            return tempTargets;
        }

        private void TriggerHullFragment(Vector2 position, int nextDepth, double stageElapsedSeconds, float lastClickDamage, IReadOnlyList<IceInstance> activeIce, float respawnProtectionSeconds)
        {
            if (config == null || config.HullFragmentDamageMultiplier <= 0f) return;

            var radius = config.HullFragmentRadiusReferencePixels;
            var damageAmount = lastClickDamage * config.HullFragmentDamageMultiplier;

            var targets = FindTargetsInRadius(position, radius, stageElapsedSeconds, activeIce, respawnProtectionSeconds);
            foreach (var target in targets)
            {
                onEnqueueDamage(target, damageAmount, EffectType.HullFragment, DestroyCategory.Chain, false, nextDepth);
            }
        }

        private void TriggerIceCollapse(Vector2 position, double stageElapsedSeconds, float lastClickDamage, IReadOnlyList<IceInstance> activeIce, float respawnProtectionSeconds)
        {
            if (config == null || !config.IceCollapseEnabled) return;

            var radius = config.IceCollapseRadiusReferencePixels;
            var damageAmount = lastClickDamage * config.IceCollapseDamageMultiplier;

            var targets = FindTargetsInRadius(position, radius, stageElapsedSeconds, activeIce, respawnProtectionSeconds);
            foreach (var target in targets)
            {
                onEnqueueDamage(target, damageAmount, EffectType.IceCollapse, DestroyCategory.Chain, false,
                    config.MaxChainDepth);
            }
        }

        private List<IceInstance> FindTargetsInRadius(Vector2 position, float radius, double stageElapsedSeconds, IReadOnlyList<IceInstance> activeIce, float respawnProtectionSeconds)
        {
            tempTargets.Clear();
            var radiusSqr = radius * radius;
            for (var i = 0; i < activeIce.Count; i++)
            {
                var ice = activeIce[i];
                if (ice.IsDestroyed) continue;

                if (stageElapsedSeconds - ice.SpawnTime < respawnProtectionSeconds)
                {
                    continue;
                }

                var distSqr = Vector2.SqrMagnitude(ice.ReferencePosition - position);
                if (distSqr <= radiusSqr)
                {
                    tempTargets.Add(ice);
                }
            }

            tempTargets.Sort(new HpDistanceIdComparer(position));

            return tempTargets;
        }

        private IceInstance? FindClosestAliveExclude(
            Vector2 referencePosition,
            IceInstance exclude,
            double stageElapsedSeconds,
            IReadOnlyList<IceInstance> activeIce,
            float respawnProtectionSeconds)
        {
            IceInstance? closest = null;
            var closestDist = float.MaxValue;

            for (var i = 0; i < activeIce.Count; i++)
            {
                var ice = activeIce[i];
                if (ice.IsDestroyed || ice == exclude)
                {
                    continue;
                }

                if (stageElapsedSeconds - ice.SpawnTime < respawnProtectionSeconds)
                {
                    continue;
                }

                var dist = Vector2.Distance(ice.ReferencePosition, referencePosition);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = ice;
                }
            }

            return closest;
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

        private readonly struct HpDistanceIdComparer : IComparer<IceInstance>
        {
            private readonly Vector2 origin;
            public HpDistanceIdComparer(Vector2 origin) => this.origin = origin;
            public int Compare(IceInstance a, IceInstance b)
            {
                var hpCmp = b.RemainingHp.CompareTo(a.RemainingHp);
                if (hpCmp != 0) return hpCmp;

                var distA = Vector2.SqrMagnitude(a.ReferencePosition - origin);
                var distB = Vector2.SqrMagnitude(b.ReferencePosition - origin);
                var distCmp = distA.CompareTo(distB);
                if (distCmp != 0) return distCmp;

                return a.IceInstanceId.CompareTo(b.IceInstanceId);
            }
        }
    }
}
