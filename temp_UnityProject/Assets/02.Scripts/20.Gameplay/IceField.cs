#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.State;
using UnityEngine;

namespace Icebreaker.Gameplay
{
    /// <summary>
    /// Manages up to <see cref="IceFieldConfig.MaxActiveIceCount"/> ice instances.
    /// Handles click hit-detection, damage, destruction events, and automatic respawn.
    /// </summary>
    public sealed class IceField : ICombatEventSource
    {
        private const int MaxInitialLayoutAttempts = 10;

        private readonly long stageId;
        private IceFieldConfig config;
        private readonly IceIdGenerator idGenerator;
        private IceSpawnPositioner positioner;
        private CriticalStrike? criticalStrike;
        private readonly List<IceInstance> activeIce;
        private readonly IStageClock clock;
        private SupportAttackConfig? supportConfig;
        private ChainEffectConfig? chainConfig;
        
        private float lastClickDamage;
        
        private long nextChainId = 1L;
        private long currentChainId;
        private int supportChargeCount;
        private int destroyedCountInCurrentChain;
        private bool hasTriggeredIceCollapseInCurrentChain;

        private struct QueuedDamage
        {
            public IceInstance Target;
            public float Damage;
            public EffectType EffectType;
            public DestroyCategory Category;
            public bool WasCritical;
            public int ChainDepth;
        }

        private readonly Queue<QueuedDamage> effectQueue = new Queue<QueuedDamage>();
        
        // GP-08: Reusable lists to prevent GC allocations
        private readonly List<IceInstance> tempTargets = new List<IceInstance>(32);
        private readonly List<Vector2> tempPositions = new List<Vector2>(32);

        // GP-08: Allocation-free comparers
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

        public IceField(long stageId, IceFieldConfig config, IceIdGenerator idGenerator, IceSpawnPositioner positioner,
            IStageClock clock, CriticalStrike? criticalStrike = null, SupportAttackConfig? supportConfig = null,
            ChainEffectConfig? chainConfig = null)
        {
            this.stageId = stageId;
            this.config = config;
            this.idGenerator = idGenerator;
            this.positioner = positioner;
            this.clock = clock;
            this.criticalStrike = criticalStrike;
            this.supportConfig = supportConfig;
            this.chainConfig = chainConfig;
            activeIce = new List<IceInstance>(config.MaxActiveIceCount);
        }

        public event Action<DamageAppliedEvent> DamageApplied = delegate { };
        public event Action<SupportChargeChangedEvent> SupportChargeChanged = delegate { };
        public event Action<IceDestroyedEvent> IceDestroyed = delegate { };

        /// <summary>Fires after a destroyed ice has been respawned at a new position.</summary>
        public event Action<int>? IceRespawned;

        public IReadOnlyList<IceInstance> ActiveIce => activeIce;

        public void Reconfigure(
            IceFieldConfig nextConfig,
            IceSpawnPositioner nextPositioner,
            CriticalStrike? nextCriticalStrike,
            SupportAttackConfig? nextSupportConfig,
            ChainEffectConfig? nextChainConfig)
        {
            config = nextConfig ?? throw new ArgumentNullException(nameof(nextConfig));
            positioner = nextPositioner ?? throw new ArgumentNullException(nameof(nextPositioner));
            criticalStrike = nextCriticalStrike;
            supportConfig = nextSupportConfig;
            chainConfig = nextChainConfig;
        }

        private bool CanProcessCombat()
        {
            return clock.Phase == GamePhase.Playing && 
                   !clock.IsPaused && 
                   clock.StageElapsedSeconds < clock.DurationSeconds;
        }

        /// <summary>
        /// Populate the field with the configured number of ice instances.
        /// Call once at the start of a stage.
        /// </summary>
        public void Initialize(double stageElapsedSeconds)
        {
            supportChargeCount = 0;

            for (var layoutAttempt = 0; layoutAttempt < MaxInitialLayoutAttempts; layoutAttempt++)
            {
                activeIce.Clear();
                for (var i = 0; i < config.MaxActiveIceCount; i++)
                {
                    if (!TrySpawnNewIce(stageElapsedSeconds, out var ice))
                    {
                        break;
                    }

                    activeIce.Add(ice!);
                }

                if (activeIce.Count == config.MaxActiveIceCount)
                {
                    return;
                }
            }

            throw new InvalidOperationException("Could not create a valid ice-field layout.");
        }

        /// <summary>
        /// Apply a single direct attack at the given reference position. Returns true if an ice was hit.
        /// Critical hit is rolled automatically for direct attacks.
        /// </summary>
        /// <param name="referencePosition">Click position in 960x540 space.</param>
        /// <param name="clickDamage">Base damage before critical multiplier.</param>
        /// <param name="effectType">Click or Hold to distinguish input type.</param>
        /// <param name="stageElapsedSeconds">Time since stage started.</param>
        public bool ApplyClickAt(Vector2 referencePosition, float clickDamage, EffectType effectType, double stageElapsedSeconds)
        {
            if (!CanProcessCombat())
            {
                return false;
            }

            var target = FindClosestAliveAt(referencePosition);
            if (target == null)
            {
                return false;
            }

            lastClickDamage = clickDamage;

            // Start a new chain for this direct input
            currentChainId = nextChainId++;
            destroyedCountInCurrentChain = 0;
            hasTriggeredIceCollapseInCurrentChain = false;

            // Roll critical for direct attacks only.
            var wasCritical = false;
            var finalDamage = clickDamage;
            if (criticalStrike != null)
            {
                finalDamage = criticalStrike.Apply(clickDamage, out wasCritical);
            }

            EnqueueDamage(target, finalDamage, effectType, DestroyCategory.Direct, wasCritical, chainDepth: 0);
            ProcessQueue(stageElapsedSeconds);

            ChargeSupportForDirectTick(referencePosition, stageElapsedSeconds);

            return true;
        }

        /// <summary>
        /// Applies one automatic cursor-area tick to every alive ice whose collision circle overlaps
        /// the cursor circle. Direct area damage ignores respawn protection; secondary effects do not.
        /// Returns the count of overlapped targets at the moment the tick begins.
        /// </summary>
        public int ApplyAreaTickAt(
            Vector2 referencePosition,
            float cursorRadiusReferencePixels,
            float directDamage,
            double stageElapsedSeconds)
        {
            if (!CanProcessCombat())
            {
                return 0;
            }

            var targets = FindAliveOverlappingCursor(referencePosition, cursorRadiusReferencePixels);
            if (targets.Count == 0)
            {
                return 0;
            }

            lastClickDamage = directDamage;
            currentChainId = nextChainId++;
            destroyedCountInCurrentChain = 0;
            hasTriggeredIceCollapseInCurrentChain = false;

            // Enqueue every direct target before processing any destruction-driven secondary effect.
            // Critical rolls are intentionally independent for each target.
            for (var i = 0; i < targets.Count; i++)
            {
                var wasCritical = false;
                var finalDamage = directDamage;
                if (criticalStrike != null)
                {
                    finalDamage = criticalStrike.Apply(directDamage, out wasCritical);
                }

                EnqueueDamage(
                    targets[i],
                    finalDamage,
                    EffectType.CursorAreaPulse,
                    DestroyCategory.Direct,
                    wasCritical,
                    chainDepth: 0);
            }

            ProcessQueue(stageElapsedSeconds);
            ChargeSupportForDirectTick(referencePosition, stageElapsedSeconds);
            return targets.Count;
        }

        private void ChargeSupportForDirectTick(Vector2 firePosition, double stageElapsedSeconds)
        {
            if (supportConfig == null || !supportConfig.Enabled)
            {
                return;
            }

            supportChargeCount++;
            if (supportChargeCount >= supportConfig.RequiredDirectHitCount)
            {
                supportChargeCount = 0;
                SupportChargeChanged(new SupportChargeChangedEvent(
                    stageId, supportChargeCount, supportConfig.RequiredDirectHitCount));
                FireSupport(firePosition, stageElapsedSeconds);
                return;
            }

            SupportChargeChanged(new SupportChargeChangedEvent(
                stageId, supportChargeCount, supportConfig.RequiredDirectHitCount));
        }

        private void FireSupport(Vector2 firePosition, double stageElapsedSeconds)
        {
            if (supportConfig == null || !supportConfig.Enabled)
            {
                return;
            }

            var targets = SelectSupportTargets(firePosition, stageElapsedSeconds);
            if (targets.Count == 0)
            {
                return;
            }

            // Primary target: full damage
            var primaryDamage = lastClickDamage * supportConfig.PrimaryDamageMultiplier;
            if (supportConfig.PrioritizeSpecialIce &&
                targets[0].SpecialType != SpecialIceType.None)
            {
                primaryDamage *= supportConfig.SpecialIceDamageMultiplier;
            }

            EnqueueDamage(targets[0], primaryDamage, EffectType.SupportShot, DestroyCategory.Support, false, 0);

            // Additional targets (S02): reduced damage
            var additionalDamage = lastClickDamage * supportConfig.AdditionalDamageMultiplier;
            for (var i = 1; i < targets.Count; i++)
            {
                var damage = additionalDamage;
                if (supportConfig.PrioritizeSpecialIce &&
                    targets[i].SpecialType != SpecialIceType.None)
                {
                    damage *= supportConfig.SpecialIceDamageMultiplier;
                }

                EnqueueDamage(targets[i], damage, EffectType.SupportShot, DestroyCategory.Support, false, 0);
            }

            ProcessQueue(stageElapsedSeconds);
        }

        /// <summary>
        /// Select support targets: 1 primary + AdditionalTargetCount additional.
        /// S03: prioritize special ice, then highest HP.
        /// Default: closest to fire position.
        /// Tie-break: HP desc → distance asc → iceInstanceId asc.
        /// </summary>
        private List<IceInstance> SelectSupportTargets(Vector2 firePosition, double stageElapsedSeconds)
        {
            tempTargets.Clear();
            for (var i = 0; i < activeIce.Count; i++)
            {
                var ice = activeIce[i];
                if (ice.IsDestroyed) continue;

                // Respawn protection: non-direct damage excluded
                if (stageElapsedSeconds - ice.SpawnTime < config.RespawnProtectionSeconds)
                {
                    continue;
                }

                tempTargets.Add(ice);
            }

            if (tempTargets.Count == 0)
            {
                return tempTargets;
            }

            var totalTargets = 1 + (supportConfig?.AdditionalTargetCount ?? 0);

            if (supportConfig != null && supportConfig.PrioritizeSpecialIce)
            {
                // S03: special ice first → HP desc → distance asc → ID asc
                tempTargets.Sort(new SupportTargetComparer(firePosition));
            }
            else
            {
                // Default (no S03): closest first → HP desc → ID asc
                tempTargets.Sort(new DistanceHpIdComparer(firePosition));
            }

            if (tempTargets.Count > totalTargets)
            {
                tempTargets.RemoveRange(totalTargets, tempTargets.Count - totalTargets);
            }

            return tempTargets;
        }

        private void EnqueueDamage(IceInstance target, float damage, EffectType effectType, DestroyCategory category, bool wasCritical, int chainDepth)
        {
            effectQueue.Enqueue(new QueuedDamage
            {
                Target = target,
                Damage = damage,
                EffectType = effectType,
                Category = category,
                WasCritical = wasCritical,
                ChainDepth = chainDepth
            });
        }

        private void ProcessQueue(double stageElapsedSeconds)
        {
            if (!CanProcessCombat())
            {
                effectQueue.Clear();
                return;
            }

            while (effectQueue.Count > 0)
            {
                var queued = effectQueue.Dequeue();
                
                // "같은 얼음에 여러 피해가 예약돼도 순서대로 처리하고, 처음 HP가 0 이하가 된 순간 한 번만 파괴한다. 이후 예약 피해는 취소한다."
                if (queued.Target.IsDestroyed)
                {
                    continue;
                }

                var hpBeforeDamage = queued.Target.RemainingHp;

                if (queued.Target.TryApplyDamage(
                        queued.Damage,
                        queued.EffectType,
                        queued.Category,
                        queued.WasCritical,
                        currentChainId,
                        queued.ChainDepth,
                        stageElapsedSeconds,
                        out var damageEvent,
                        out var destroyEvent))
                {
                    DamageApplied(damageEvent);

                    if (queued.Target.IsDestroyed && damageEvent.RemainingHp <= 0f)
                    {
                        IceDestroyed(destroyEvent);
                        destroyedCountInCurrentChain++;
                        
                        var maxChainDepth = chainConfig?.MaxChainDepth ?? 3;
                        if (queued.ChainDepth < maxChainDepth)
                        {
                            // 1. D03 과잉 파쇄
                            if (queued.Category == DestroyCategory.Direct && chainConfig != null && chainConfig.OverkillEnabled)
                            {
                                var overkillDamage = queued.Damage - hpBeforeDamage;
                                if (overkillDamage > 0f)
                                {
                                    var transferDamage = overkillDamage * chainConfig.OverkillTransferMultiplier;
                                    var closest = FindClosestAliveExclude(queued.Target.ReferencePosition, queued.Target);
                                    if (closest != null)
                                    {
                                        EnqueueDamage(closest, transferDamage, EffectType.Overkill, DestroyCategory.Chain, false, queued.ChainDepth + 1);
                                    }
                                }
                            }

                            // 2. 특수빙 효과
                            if (queued.Target.SpecialType == SpecialIceType.Crystal)
                            {
                                TriggerCrystalEffect(queued.Target, queued.ChainDepth + 1, stageElapsedSeconds);
                            }
                            else if (queued.Target.SpecialType == SpecialIceType.Crack)
                            {
                                TriggerCrackEffect(queued.Target, queued.ChainDepth + 1, stageElapsedSeconds);
                            }
                            
                            // 3. H01 파편 비산
                            if (chainConfig != null && chainConfig.HullFragmentDamageMultiplier > 0f)
                            {
                                TriggerHullFragment(queued.Target.ReferencePosition, queued.ChainDepth + 1, stageElapsedSeconds);
                            }
                        }

                        // 4. H03 빙판 붕괴 (depth 3이어도 피해 발동)
                        if (chainConfig != null && chainConfig.IceCollapseEnabled &&
                            !hasTriggeredIceCollapseInCurrentChain &&
                            destroyedCountInCurrentChain >= chainConfig.IceCollapseRequiredDestroyCount)
                        {
                            hasTriggeredIceCollapseInCurrentChain = true;
                            TriggerIceCollapse(queued.Target.ReferencePosition, stageElapsedSeconds);
                        }

                        RespawnAt(queued.Target, stageElapsedSeconds);
                    }
                }
            }
        }

        private void TriggerCrystalEffect(IceInstance source, int nextDepth, double stageElapsedSeconds)
        {
            var shardCount = chainConfig != null ? chainConfig.CrystalShardCount : 5;
            var targets = FindCrystalTargets(source, shardCount, stageElapsedSeconds);
            
            foreach (var target in targets)
            {
                // 파편 데미지는 대상을 즉시 파괴하므로 충분히 큰 데미지(MaxHp * 2)를 적용
                EnqueueDamage(target, target.MaxHp * 2f, EffectType.CrystalShard, DestroyCategory.Chain, false, nextDepth);
            }
        }

        private List<IceInstance> FindCrystalTargets(IceInstance source, int count, double stageElapsedSeconds)
        {
            tempTargets.Clear();
            for (var i = 0; i < activeIce.Count; i++)
            {
                var ice = activeIce[i];
                if (ice.IsDestroyed || ice == source) continue;

                // 재생성 보호 (0.25초) 확인
                if (stageElapsedSeconds - ice.SpawnTime < config.RespawnProtectionSeconds)
                {
                    continue;
                }

                // 결정빙 파편은 자신보다 낮은 단계의 얼음만 타겟
                if (ice.Tier < source.Tier)
                {
                    tempTargets.Add(ice);
                }
            }

            // 거리 오름차순 -> HP 내림차순 -> ID 오름차순
            tempTargets.Sort(new DistanceHpIdComparer(source.ReferencePosition));

            if (tempTargets.Count > count)
            {
                tempTargets.RemoveRange(count, tempTargets.Count - count);
            }

            return tempTargets;
        }

        private void TriggerCrackEffect(IceInstance source, int nextDepth, double stageElapsedSeconds)
        {
            var crackRadius = chainConfig != null ? chainConfig.CrackRadiusReferencePixels : 120f;
            var crackMultiplier = chainConfig != null ? chainConfig.CrackDamageMultiplier : 1.0f;
            
            var targets = FindCrackTargets(source, crackRadius, stageElapsedSeconds);
            var damageAmount = lastClickDamage * 3f * crackMultiplier;

            foreach (var target in targets)
            {
                EnqueueDamage(target, damageAmount, EffectType.CrackExplosion, DestroyCategory.Chain, false, nextDepth);
            }
        }

        private List<IceInstance> FindCrackTargets(IceInstance source, float radius, double stageElapsedSeconds)
        {
            tempTargets.Clear();
            var radiusSqr = radius * radius;
            for (var i = 0; i < activeIce.Count; i++)
            {
                var ice = activeIce[i];
                if (ice.IsDestroyed || ice == source) continue;

                // 재생성 보호 확인
                if (stageElapsedSeconds - ice.SpawnTime < config.RespawnProtectionSeconds)
                {
                    continue;
                }

                var distSqr = Vector2.SqrMagnitude(ice.ReferencePosition - source.ReferencePosition);
                if (distSqr <= radiusSqr)
                {
                    tempTargets.Add(ice);
                }
            }

            // 균열빙 폭발은 범위 내 모든 대상을 타격하지만, 
            // 큐에 넣는 순서를 명확히 하기 위해 기획 규칙(HP -> 거리 -> ID)에 따라 정렬
            tempTargets.Sort(new HpDistanceIdComparer(source.ReferencePosition));

            return tempTargets;
        }

        private void TriggerHullFragment(Vector2 position, int nextDepth, double stageElapsedSeconds)
        {
            if (chainConfig == null || chainConfig.HullFragmentDamageMultiplier <= 0f) return;

            var radius = chainConfig.HullFragmentRadiusReferencePixels;
            var damageAmount = lastClickDamage * chainConfig.HullFragmentDamageMultiplier;

            var targets = FindTargetsInRadius(position, radius, stageElapsedSeconds);
            foreach (var target in targets)
            {
                EnqueueDamage(target, damageAmount, EffectType.HullFragment, DestroyCategory.Chain, false, nextDepth);
            }
        }

        private void TriggerIceCollapse(Vector2 position, double stageElapsedSeconds)
        {
            if (chainConfig == null || !chainConfig.IceCollapseEnabled) return;

            var radius = chainConfig.IceCollapseRadiusReferencePixels;
            var damageAmount = lastClickDamage * chainConfig.IceCollapseDamageMultiplier;

            var targets = FindTargetsInRadius(position, radius, stageElapsedSeconds);
            foreach (var target in targets)
            {
                // H03은 최대 깊이를 우회하여 무조건 깊이 3으로 기록.
                EnqueueDamage(target, damageAmount, EffectType.IceCollapse, DestroyCategory.Chain, false,
                    chainConfig.MaxChainDepth);
            }
        }

        private List<IceInstance> FindTargetsInRadius(Vector2 position, float radius, double stageElapsedSeconds)
        {
            tempTargets.Clear();
            var radiusSqr = radius * radius;
            for (var i = 0; i < activeIce.Count; i++)
            {
                var ice = activeIce[i];
                if (ice.IsDestroyed) continue;

                if (stageElapsedSeconds - ice.SpawnTime < config.RespawnProtectionSeconds)
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

        private List<IceInstance> FindAliveOverlappingCursor(
            Vector2 cursorPosition,
            float cursorRadiusReferencePixels)
        {
            tempTargets.Clear();
            var overlapDistance = cursorRadiusReferencePixels + config.IceCollisionRadiusReferencePixels;
            var overlapDistanceSquared = overlapDistance * overlapDistance;

            for (var i = 0; i < activeIce.Count; i++)
            {
                var ice = activeIce[i];
                if (ice.IsDestroyed) continue;

                if (Vector2.SqrMagnitude(ice.ReferencePosition - cursorPosition) <= overlapDistanceSquared)
                {
                    tempTargets.Add(ice);
                }
            }

            tempTargets.Sort(new DistanceHpIdComparer(cursorPosition));

            return tempTargets;
        }

        /// <summary>Find the closest alive ice excluding a specific instance.</summary>
        private IceInstance? FindClosestAliveExclude(Vector2 referencePosition, IceInstance exclude)
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

                var dist = Vector2.Distance(ice.ReferencePosition, referencePosition);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = ice;
                }
            }

            return closest;
        }

        /// <summary>Find the closest alive ice within hit radius of the given position.</summary>
        private IceInstance? FindClosestAliveAt(Vector2 referencePosition)
        {
            IceInstance? closest = null;
            var closestDist = float.MaxValue;

            for (var i = 0; i < activeIce.Count; i++)
            {
                var ice = activeIce[i];
                if (ice.IsDestroyed)
                {
                    continue;
                }

                var dist = Vector2.Distance(ice.ReferencePosition, referencePosition);
                if (dist <= config.IceCollisionRadiusReferencePixels && dist < closestDist)
                {
                    closestDist = dist;
                    closest = ice;
                }
            }

            return closest;
        }

        private void RespawnAt(IceInstance destroyed, double stageElapsedSeconds)
        {
            var tier = PickRandomTier();
            var def = GetDefinition(tier);
            var positions = CollectAlivePositions(destroyed);

            if (!positioner.TryGetPosition(positions, out var newPosition))
            {
                throw new InvalidOperationException("No valid position remained for an ice respawn.");
            }
            
            DetermineSpecialIce(tier, out var specialType, out var hpMultiplier);

            destroyed.Reset(
                idGenerator.NextId(),
                tier,
                specialType,
                def.MaxHp * hpMultiplier,
                newPosition,
                stageElapsedSeconds);

            var index = activeIce.IndexOf(destroyed);
            if (index >= 0)
            {
                IceRespawned?.Invoke(index);
            }
        }

        private bool TrySpawnNewIce(double stageElapsedSeconds, out IceInstance? ice)
        {
            var tier = PickRandomTier();
            var def = GetDefinition(tier);
            var positions = CollectAlivePositions(null);

            if (!positioner.TryGetPosition(positions, out var position))
            {
                ice = null;
                return false;
            }
            
            DetermineSpecialIce(tier, out var specialType, out var hpMultiplier);

            ice = new IceInstance(
                stageId,
                idGenerator.NextId(),
                tier,
                specialType,
                def.MaxHp * hpMultiplier,
                position,
                stageElapsedSeconds);
            return true;
        }

        private List<Vector2> CollectAlivePositions(IceInstance? exclude)
        {
            tempPositions.Clear();
            for (var i = 0; i < activeIce.Count; i++)
            {
                var ice = activeIce[i];
                if (!ice.IsDestroyed && ice != exclude)
                {
                    tempPositions.Add(ice.ReferencePosition);
                }
            }

            return tempPositions;
        }

        private void DetermineSpecialIce(IceTier tier, out SpecialIceType specialType, out float hpMultiplier)
        {
            specialType = SpecialIceType.None;
            hpMultiplier = 1f;

            if (config.SpecialDefinitions == null || config.SpecialDefinitions.Count == 0)
            {
                return;
            }

            var currentSpecialCount = 0;
            for (var i = 0; i < activeIce.Count; i++)
            {
                if (!activeIce[i].IsDestroyed && activeIce[i].SpecialType != SpecialIceType.None)
                {
                    currentSpecialCount++;
                }
            }

            if (currentSpecialCount >= config.MaxSpecialIceCount)
            {
                return;
            }

            var roll = UnityEngine.Random.value;
            for (var i = 0; i < config.SpecialDefinitions.Count; i++)
            {
                var def = config.SpecialDefinitions[i];
                if (tier >= def.MinimumTier)
                {
                    if (roll < def.SpawnChance)
                    {
                        specialType = def.Type;
                        hpMultiplier = def.HpMultiplier;
                        return;
                    }
                    roll -= def.SpawnChance;
                }
            }
        }

        private IceTier PickRandomTier()
        {
            long totalWeight = 0;
            for (var i = 0; i < config.SpawnWeights.Count; i++)
            {
                totalWeight += config.SpawnWeights[i].Weight;
            }

            var roll = (long)(UnityEngine.Random.value * totalWeight);
            long cumulative = 0;

            for (var i = 0; i < config.SpawnWeights.Count; i++)
            {
                cumulative += config.SpawnWeights[i].Weight;
                if (roll < cumulative)
                {
                    return config.SpawnWeights[i].Tier;
                }
            }

            return config.SpawnWeights[config.SpawnWeights.Count - 1].Tier;
        }

        private IceDefinition GetDefinition(IceTier tier)
        {
            for (var i = 0; i < config.IceDefinitions.Count; i++)
            {
                if (config.IceDefinitions[i].Tier == tier)
                {
                    return config.IceDefinitions[i];
                }
            }

            throw new InvalidOperationException($"No IceDefinition found for tier {tier}.");
        }
    }
}
