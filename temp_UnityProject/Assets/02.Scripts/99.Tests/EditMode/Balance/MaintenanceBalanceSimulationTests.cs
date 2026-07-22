#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Icebreaker.Core;
using Icebreaker.Gameplay;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Maintenance;
using Icebreaker.Shared.State;
using NUnit.Framework;
using UnityEngine;

namespace Icebreaker.Balance.Tests
{
    public sealed class MaintenanceBalanceSimulationTests
    {
        private const int FramesPerSecond = 60;
        private const int StageSeconds = 60;

        private static readonly string[] PurchasePriority =
        {
            "C01-L1", "D01-L1", "D02-L1", "C02-L1", "C03-L1",
            "D01-L2", "S01-L1", "D04-L1", "D02-L2", "H01-L1",
            "C02-L2", "S02-L1", "D01-L3", "D04-L2", "D02-L3",
            "H01-L2", "H02-L1", "S02-L2", "D03-L1", "C04-L1",
            "C02-L3", "D04-L3", "H01-L3", "S03-L1", "H02-L2", "H03-L1"
        };

        private sealed class SimulationClock : IStageClock
        {
            public GamePhase Phase { get; set; } = GamePhase.Playing;
            public double DurationSeconds { get; set; } = StageSeconds;
            public double StageElapsedSeconds { get; set; }
            public double RemainingSeconds => Math.Max(0d, DurationSeconds - StageElapsedSeconds);
            public bool IsPaused { get; set; }
        }

        private readonly struct PurchaseRecord
        {
            public PurchaseRecord(string stepId, long cost, long fundsBeforePurchase)
            {
                StepId = stepId;
                Cost = cost;
                FundsBeforePurchase = fundsBeforePurchase;
            }

            public string StepId { get; }
            public long Cost { get; }
            public long FundsBeforePurchase { get; }
        }

        [Test]
        public void ReferenceBalance_FiveRunsMeetEarlyPurchasePacing()
        {
            var tempDirectory = Path.Combine(
                Path.GetTempPath(),
                "icebreaker-balance-" + Guid.NewGuid().ToString("N"));
            try
            {
                var save = SaveData.CreateNew("phase-g-baseline");
                var ledger = new ProgressionLedger(
                    DestinationCatalog.CreateDemo(),
                    RewardTable.CreateDefault());
                var maintenance = new MaintenanceCore(
                    MaintenanceCatalog.CreateDemo(),
                    ledger,
                    new SaveService(new SaveStore(tempDirectory), save));

                for (var runIndex = 1; runIndex <= 5; runIndex++)
                {
                    var fundsBeforeRun = ledger.Funds;
                    var config = CombatConfigFactory.Build(maintenance.MaintenanceLevels);
                    ledger.BeginStage(maintenance.MaintenanceEfficiencyLevel);
                    UnityEngine.Random.InitState(10_000 + runIndex);

                    var clock = new SimulationClock();
                    var field = new IceField(
                        runIndex,
                        config.IceField,
                        new IceIdGenerator(),
                        new IceSpawnPositioner(new Rect(56f, 56f, 848f, 428f), 120f),
                        clock,
                        new CriticalStrike(
                            config.DirectAttack.CriticalChance,
                            config.DirectAttack.CriticalDamageMultiplier),
                        config.SupportAttack,
                        config.ChainEffect);
                    var destroyedByTier = new Dictionary<IceTier, int>();
                    var damageBySource = new Dictionary<EffectType, float>();
                    field.DamageApplied += e =>
                    {
                        damageBySource.TryGetValue(e.EffectType, out var damage);
                        damageBySource[e.EffectType] = damage + e.Damage;
                    };
                    field.IceDestroyed += e =>
                    {
                        destroyedByTier.TryGetValue(e.Tier, out var count);
                        destroyedByTier[e.Tier] = count + 1;
                        ledger.TryApproveDestruction(e, out _);
                    };
                    field.Initialize(0d);

                    var scheduler = new AttackTickScheduler(config.DirectAttack.AttackTicksPerSecond);
                    var tickCount = 0;
                    var targetCount = 0;
                    for (var frame = 0; frame < FramesPerSecond * StageSeconds; frame++)
                    {
                        clock.StageElapsedSeconds = frame / (double)FramesPerSecond;
                        var ticks = scheduler.Update(frame == 0 ? 0f : 1f / FramesPerSecond);
                        for (var tick = 0; tick < ticks; tick++)
                        {
                            var target = field.ActiveIce
                                .Where(ice => !ice.IsDestroyed)
                                .OrderBy(ice => ice.RemainingHp)
                                .ThenBy(ice => ice.IceInstanceId)
                                .First();
                            targetCount += field.ApplyAreaTickAt(
                                target.ReferencePosition,
                                config.DirectAttack.CursorRadiusReferencePixels,
                                config.DirectAttack.CurrentDirectDamage,
                                clock.StageElapsedSeconds);
                            tickCount++;
                        }
                    }

                    clock.Phase = GamePhase.Settlement;
                    var settlement = ledger.EndStage();
                    var steps = maintenance.GetPurchaseStepViewData();
                    var availableSteps = steps.Count(step =>
                        step.PurchaseState == MaintenanceStepPurchaseState.Available &&
                        step.Visibility == MaintenanceStepVisibility.Visible);
                    var affordableSteps = steps.Count(step => step.CanPurchase);
                    var purchases = PurchaseAffordableSteps(maintenance);
                    var fundsAfterRun = ledger.Funds;

                    Assert.That(
                        availableSteps,
                        runIndex == 1 ? Is.EqualTo(1) : Is.InRange(2, 4),
                        $"Run {runIndex} should preserve the root onboarding exception or 2-4 choices.");
                    Assert.That(
                        affordableSteps,
                        Is.InRange(1, 2),
                        $"Run {runIndex} should offer 1-2 immediately affordable steps.");
                    Assert.That(
                        purchases,
                        Is.Not.Empty,
                        $"Run {runIndex} should not create an unpurchasable intermission.");
                    foreach (var purchase in purchases)
                    {
                        var priceRatio = purchase.Cost / (double)purchase.FundsBeforePurchase;
                        Assert.That(
                            priceRatio,
                            Is.InRange(0.25d, 0.85d),
                            $"{purchase.StepId} should cost 25-85% of funds at purchase.");
                    }

                    Debug.Log(string.Join(" | ", new[]
                    {
                        $"runIndex={runIndex}",
                        $"fundsBeforeRun={fundsBeforeRun}",
                        $"earnedFunds={settlement.EarnedFunds}",
                        $"fundsAfterRun={fundsAfterRun}",
                        $"availableSteps={availableSteps}",
                        $"affordableSteps={affordableSteps}",
                        $"purchasedSteps={string.Join(",", purchases.Select(purchase => purchase.StepId))}",
                        $"destroyedByTier={FormatCounts(destroyedByTier)}",
                        $"averageTargetsPerTick={(targetCount / (double)tickCount).ToString("0.000", CultureInfo.InvariantCulture)}",
                        $"damageBySource={FormatDamage(damageBySource)}",
                        $"destinationProgressGain={settlement.DestinationProgressGain}"
                    }));

                    if (ledger.PendingArrivalDestinationId != null)
                    {
                        ledger.ApplyArrival();
                    }
                }
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
        }

        private static IReadOnlyList<PurchaseRecord> PurchaseAffordableSteps(
            MaintenanceCore maintenance)
        {
            var purchased = new List<PurchaseRecord>();
            while (true)
            {
                var steps = maintenance.GetPurchaseStepViewData();
                MaintenancePurchaseStepViewData? selected = null;
                foreach (var stepId in PurchasePriority)
                {
                    selected = steps.FirstOrDefault(step => step.StepId == stepId && step.CanPurchase);
                    if (selected != null)
                    {
                        break;
                    }
                }

                if (selected == null)
                {
                    return purchased;
                }

                var fundsBeforePurchase = maintenance.Funds;
                if (maintenance.TryPurchaseDetailed(selected.MaintenanceId, selected.TargetLevel) !=
                    MaintenancePurchaseResult.Success)
                {
                    return purchased;
                }

                purchased.Add(new PurchaseRecord(
                    selected.StepId,
                    selected.Cost,
                    fundsBeforePurchase));
            }
        }

        private static string FormatCounts(IReadOnlyDictionary<IceTier, int> counts)
        {
            return string.Join(",", Enum.GetValues(typeof(IceTier))
                .Cast<IceTier>()
                .Select(tier => $"{tier}:{(counts.TryGetValue(tier, out var count) ? count : 0)}"));
        }

        private static string FormatDamage(IReadOnlyDictionary<EffectType, float> damage)
        {
            return string.Join(",", damage
                .OrderBy(pair => pair.Key)
                .Select(pair => $"{pair.Key}:{pair.Value.ToString("0.0", CultureInfo.InvariantCulture)}"));
        }
    }
}
