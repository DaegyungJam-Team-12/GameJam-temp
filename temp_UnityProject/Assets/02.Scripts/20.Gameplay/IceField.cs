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
        private readonly SupportAttackProcessor supportProcessor;
        private readonly ChainEffectProcessor chainProcessor;
        
        private float lastClickDamage;
        
        private long nextChainId = 1L;
        private long currentChainId;
        

        private struct QueuedDamage
        {
            public IceInstance Target;
            public float Damage;
            public EffectType EffectType;
            public DestroyCategory Category;
            public bool WasCritical;
            public int ChainDepth;
        }

        private struct ScheduledRespawn
        {
            public IceInstance Target;
            public double SpawnAnimationStartTime;
            public double ActivationTime;
            public bool HasPreview;
        }

        private struct RecentDestruction
        {
            public Vector2 Position;
            public double Time;
        }

        private readonly Queue<QueuedDamage> effectQueue = new Queue<QueuedDamage>();
        private readonly List<IceInstance> pendingRespawns = new List<IceInstance>(32);
        private readonly List<ScheduledRespawn> scheduledRespawns = new List<ScheduledRespawn>(32);
        private readonly List<SpawnPositionBlocker> reservedSpawnPositions = new List<SpawnPositionBlocker>(32);
        private readonly List<RecentDestruction> recentDestructions = new List<RecentDestruction>(32);
        private int queueProcessingDepth;
        
        // GP-08: Reusable lists to prevent GC allocations
        private readonly List<IceInstance> tempTargets = new List<IceInstance>(32);
        private readonly List<Vector2> tempPositions = new List<Vector2>(32);
        private readonly List<SpawnPositionBlocker> tempSpawnBlockers = new List<SpawnPositionBlocker>(32);
        private readonly List<Vector2> tempRecentDestructionPositions = new List<Vector2>(32);

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
            activeIce = new List<IceInstance>(config.MaxActiveIceCount);

            supportProcessor = new SupportAttackProcessor(
                stageId,
                supportConfig,
                e => SupportChargeChanged(e),
                EnqueueDamage,
                ProcessQueue);

            chainProcessor = new ChainEffectProcessor(
                chainConfig,
                EnqueueDamage);
        }

        public event Action<DamageAppliedEvent> DamageApplied = delegate { };
        public event Action<SupportChargeChangedEvent> SupportChargeChanged = delegate { };
        public event Action<IceDestroyedEvent> IceDestroyed = delegate { };

        /// <summary>Fires after a destroyed ice has been respawned at a new position.</summary>
        public event Action<int>? IceRespawned;

        /// <summary>Fires when an ice enters a visual-only respawn state.</summary>
        public event Action<int>? IceRespawnStateChanged;

        public IReadOnlyList<IceInstance> ActiveIce => activeIce;
        public int QueuedRespawnCount => scheduledRespawns.Count;
        public int ReservedSpawnCount => reservedSpawnPositions.Count;

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
            supportProcessor.Reconfigure(nextSupportConfig);
            chainProcessor.Reconfigure(nextChainConfig);
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
            supportProcessor.Reset();
            effectQueue.Clear();
            pendingRespawns.Clear();
            scheduledRespawns.Clear();
            reservedSpawnPositions.Clear();
            recentDestructions.Clear();
            queueProcessingDepth = 0;

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
            chainProcessor.StartNewChain();

            // Roll critical for direct attacks only.
            var wasCritical = false;
            var finalDamage = clickDamage;
            if (criticalStrike != null)
            {
                finalDamage = criticalStrike.Apply(clickDamage, out wasCritical);
            }

            EnqueueDamage(target, finalDamage, effectType, DestroyCategory.Direct, wasCritical, chainDepth: 0);
            ProcessQueue(stageElapsedSeconds);

            supportProcessor.ChargeForDirectTick(referencePosition, stageElapsedSeconds, lastClickDamage, activeIce, config.RespawnProtectionSeconds);

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
            chainProcessor.StartNewChain();

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
            supportProcessor.ChargeForDirectTick(referencePosition, stageElapsedSeconds, lastClickDamage, activeIce, config.RespawnProtectionSeconds);
            return targets.Count;
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
            queueProcessingDepth++;
            try
            {
                while (effectQueue.Count > 0)
                {
                    if (!CanProcessCombat())
                    {
                        CancelQueuedCombatWork();
                        return;
                    }

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
                            recentDestructions.Add(new RecentDestruction
                            {
                                Position = destroyEvent.ReferencePosition,
                                Time = clock.StageElapsedSeconds
                            });
                            if (!CanProcessCombat())
                            {
                                CancelQueuedCombatWork();
                                return;
                            }

                            chainProcessor.ProcessIceDestroyed(
                                queued.Target,
                                queued.ChainDepth,
                                queued.Category,
                                hpBeforeDamage,
                                queued.Damage,
                                lastClickDamage,
                                stageElapsedSeconds,
                                activeIce,
                                config.RespawnProtectionSeconds);

                            pendingRespawns.Add(queued.Target);
                        }
                    }
                }
            }
            finally
            {
                queueProcessingDepth--;
                if (queueProcessingDepth == 0)
                {
                    if (CanProcessCombat())
                    {
                        FlushPendingRespawns(stageElapsedSeconds);
                    }
                    else
                    {
                        CancelQueuedCombatWork();
                    }
                }
            }
        }

        private void CancelQueuedCombatWork()
        {
            effectQueue.Clear();
            pendingRespawns.Clear();
        }

        private void FlushPendingRespawns(double stageElapsedSeconds)
        {
            for (var i = 0; i < pendingRespawns.Count; i++)
            {
                if (!CanProcessCombat())
                {
                    CancelQueuedCombatWork();
                    return;
                }

                ScheduleRespawn(pendingRespawns[i], clock.StageElapsedSeconds, i);
            }

            pendingRespawns.Clear();
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

        /// <summary>
        /// Advances respawn state using the authoritative work-time clock. Pausing leaves the
        /// scheduled state untouched; leaving Playing discards queued reservations immediately.
        /// </summary>
        public void UpdateRespawns()
        {
            if (clock.Phase != GamePhase.Playing || clock.StageElapsedSeconds >= clock.DurationSeconds)
            {
                CancelScheduledRespawns();
                return;
            }

            if (clock.IsPaused)
            {
                return;
            }

            var stageElapsedSeconds = clock.StageElapsedSeconds;
            RemoveExpiredRecentDestructions(stageElapsedSeconds);
            for (var i = 0; i < scheduledRespawns.Count; i++)
            {
                var scheduled = scheduledRespawns[i];
                if (!scheduled.HasPreview &&
                    stageElapsedSeconds >= scheduled.SpawnAnimationStartTime)
                {
                    if (!TryPrepareSpawnAnimation(ref scheduled, stageElapsedSeconds))
                    {
                        scheduledRespawns[i] = scheduled;
                        continue;
                    }

                    scheduledRespawns[i] = scheduled;
                }

                if (!scheduled.HasPreview ||
                    stageElapsedSeconds + 0.000001d < scheduled.ActivationTime)
                {
                    continue;
                }

                ActivateRespawn(scheduled.Target, stageElapsedSeconds);
                RemoveReservedPosition(scheduled.Target.VisualReferencePosition);
                scheduledRespawns.RemoveAt(i);
                i--;
            }
        }

        private void ScheduleRespawn(IceInstance destroyed, double stageElapsedSeconds, int chainOrder)
        {
            destroyed.BeginRespawnGap(stageElapsedSeconds);
            var spawnAnimationStartTime = stageElapsedSeconds + config.RespawnGapSeconds +
                config.ChainRespawnStaggerSeconds * chainOrder;
            scheduledRespawns.Add(new ScheduledRespawn
            {
                Target = destroyed,
                SpawnAnimationStartTime = spawnAnimationStartTime,
                ActivationTime = 0d,
                HasPreview = false
            });
            NotifyRespawnStateChanged(destroyed);
        }

        private bool TryPrepareSpawnAnimation(
            ref ScheduledRespawn scheduled,
            double stageElapsedSeconds)
        {
            var tier = PickRandomTier();
            var nextId = idGenerator.PeekNextId();
            var visualDiameter = ResolveVisualDiameter(tier, nextId);
            if (!TryFindSpawnPosition(
                    scheduled.Target,
                    visualDiameter * 0.5f,
                    stageElapsedSeconds,
                    out var position))
            {
                return false;
            }

            nextId = idGenerator.NextId();
            var definition = GetDefinition(tier);
            DetermineSpecialIce(tier, out var specialType, out var hpMultiplier);
            scheduled.Target.BeginSpawnAnimation(
                nextId,
                tier,
                specialType,
                definition.MaxHp * hpMultiplier,
                position,
                visualDiameter,
                stageElapsedSeconds);
            reservedSpawnPositions.Add(new SpawnPositionBlocker(position, visualDiameter * 0.5f));
            scheduled.ActivationTime = stageElapsedSeconds + config.SpawnAnimationSeconds;
            scheduled.HasPreview = true;
            NotifyRespawnStateChanged(scheduled.Target);
            return true;
        }

        private void ActivateRespawn(IceInstance destroyed, double stageElapsedSeconds)
        {
            destroyed.ActivatePendingSpawn(stageElapsedSeconds);

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
            var nextId = idGenerator.PeekNextId();
            var visualDiameter = ResolveVisualDiameter(tier, nextId);
            if (!TryFindSpawnPosition(null, visualDiameter * 0.5f, stageElapsedSeconds, out var position))
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
                stageElapsedSeconds,
                visualDiameter);
            return true;
        }

        private bool TryFindSpawnPosition(
            IceInstance? excluded,
            float candidateVisualRadius,
            double stageElapsedSeconds,
            out Vector2 position)
        {
            if (!config.UsesVisualSpawnSpacing)
            {
                return positioner.TryGetPosition(CollectAlivePositions(excluded), out position);
            }

            var blockers = CollectSpawnBlockers(excluded);
            var recentPositions = CollectRecentDestructionPositions(stageElapsedSeconds);
            try
            {
                return positioner.TryGetPosition(
                    blockers,
                    candidateVisualRadius,
                    recentPositions,
                    config.RecentDestructionExclusionReferencePixels,
                    out position);
            }
            catch (InvalidOperationException)
            {
                position = default;
                return false;
            }
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

        private List<SpawnPositionBlocker> CollectSpawnBlockers(IceInstance? exclude)
        {
            tempSpawnBlockers.Clear();
            for (var i = 0; i < activeIce.Count; i++)
            {
                var ice = activeIce[i];
                if (ice.IsDestroyed || ice == exclude)
                {
                    continue;
                }

                var radius = ice.VisualDiameterReferencePixels > 0f
                    ? ice.VisualDiameterReferencePixels * 0.5f
                    : config.VisualDiameterMaximumReferencePixels * 0.5f;
                tempSpawnBlockers.Add(new SpawnPositionBlocker(ice.ReferencePosition, radius));
            }

            for (var i = 0; i < reservedSpawnPositions.Count; i++)
            {
                tempSpawnBlockers.Add(reservedSpawnPositions[i]);
            }

            return tempSpawnBlockers;
        }

        private List<Vector2> CollectRecentDestructionPositions(double stageElapsedSeconds)
        {
            RemoveExpiredRecentDestructions(stageElapsedSeconds);
            tempRecentDestructionPositions.Clear();
            for (var i = 0; i < recentDestructions.Count; i++)
            {
                tempRecentDestructionPositions.Add(recentDestructions[i].Position);
            }

            return tempRecentDestructionPositions;
        }

        private void RemoveExpiredRecentDestructions(double stageElapsedSeconds)
        {
            for (var i = recentDestructions.Count - 1; i >= 0; i--)
            {
                if (stageElapsedSeconds - recentDestructions[i].Time >= config.RecentDestructionExclusionSeconds)
                {
                    recentDestructions.RemoveAt(i);
                }
            }
        }

        private void RemoveReservedPosition(Vector2 position)
        {
            for (var i = 0; i < reservedSpawnPositions.Count; i++)
            {
                if (reservedSpawnPositions[i].ReferencePosition == position)
                {
                    reservedSpawnPositions.RemoveAt(i);
                    return;
                }
            }
        }

        private void CancelScheduledRespawns()
        {
            for (var i = 0; i < scheduledRespawns.Count; i++)
            {
                var target = scheduledRespawns[i].Target;
                target.CancelPendingSpawn();
                NotifyRespawnStateChanged(target);
            }

            scheduledRespawns.Clear();
            reservedSpawnPositions.Clear();
            recentDestructions.Clear();
        }

        private void NotifyRespawnStateChanged(IceInstance ice)
        {
            var index = activeIce.IndexOf(ice);
            if (index >= 0)
            {
                IceRespawnStateChanged?.Invoke(index);
            }
        }

        private float ResolveVisualDiameter(IceTier tier, long iceInstanceId)
        {
            var minimum = config.VisualDiameterMinimumReferencePixels;
            var maximum = config.VisualDiameterMaximumReferencePixels;
            if (maximum <= minimum)
            {
                return minimum;
            }

            unchecked
            {
                var hash = (ulong)iceInstanceId * 11400714819323198485UL;
                var sizeStep = (int)((hash >> 40) % 5UL);
                var tierIndex = Mathf.Clamp((int)tier, 0, 4);
                var sizeIndex = tierIndex * 5 + sizeStep;
                return minimum + (maximum - minimum) * (sizeIndex / 24f);
            }
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

            for (var i = 0; i < scheduledRespawns.Count; i++)
            {
                if (scheduledRespawns[i].Target.HasPendingSpawn &&
                    scheduledRespawns[i].Target.PendingSpecialType != SpecialIceType.None)
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
