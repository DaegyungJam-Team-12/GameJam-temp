#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Progression;
using Icebreaker.Shared.State;
using UnityEditor;
using UnityEngine;

namespace Icebreaker.Core.Editor
{
    public static class Core00Verification
    {
        public static void Run()
        {
            var failures = new List<string>();
            var destination = new DestinationDefinition(
                "island-village",
                "섬마을",
                120,
                "식료품·우편",
                0);
            var core = new ProgressionCore(destination);
            var source = new FakeCombatEventSource();
            var rewardCount = 0;
            var lastReward = default(RewardGrantedEvent);

            Action<IceDestroyedEvent> forwardDestruction = payload => core.HandleIceDestroyed(payload);
            Action<RewardGrantedEvent> captureReward = payload =>
            {
                rewardCount++;
                lastReward = payload;
            };

            source.IceDestroyed += forwardDestruction;
            core.RewardGranted += captureReward;

            var firstDestruction = new IceDestroyedEvent(
                1,
                1,
                1,
                0,
                IceTier.T1,
                SpecialIceType.None,
                DestroyCategory.Direct,
                EffectType.Click,
                new Vector2(480f, 270f),
                0d);

            source.PublishIceDestroyed(firstDestruction);

            if (core.Funds != 10)
            {
                failures.Add($"First destruction funds: expected 10, got {core.Funds}.");
            }

            if (core.DestinationProgress != 1)
            {
                failures.Add($"First destruction progress: expected 1, got {core.DestinationProgress}.");
            }

            if (rewardCount != 1)
            {
                failures.Add($"First destruction reward count: expected 1, got {rewardCount}.");
            }

            if (lastReward.FundsGranted != 10)
            {
                failures.Add($"First reward funds: expected 10, got {lastReward.FundsGranted}.");
            }

            if (lastReward.DestinationProgressGranted != 1)
            {
                failures.Add(
                    $"First reward progress: expected 1, got {lastReward.DestinationProgressGranted}.");
            }

            source.PublishIceDestroyed(firstDestruction);

            if (core.Funds != 10)
            {
                failures.Add($"Duplicate destruction funds: expected 10, got {core.Funds}.");
            }

            if (core.DestinationProgress != 1)
            {
                failures.Add($"Duplicate destruction progress: expected 1, got {core.DestinationProgress}.");
            }

            if (rewardCount != 1)
            {
                failures.Add($"Duplicate destruction reward count: expected 1, got {rewardCount}.");
            }

            var secondDestruction = new IceDestroyedEvent(
                1,
                2,
                2,
                0,
                IceTier.T1,
                SpecialIceType.None,
                DestroyCategory.Direct,
                EffectType.Click,
                new Vector2(480f, 270f),
                0d);

            source.PublishIceDestroyed(secondDestruction);

            if (core.Funds != 20)
            {
                failures.Add($"Second unique destruction funds: expected 20, got {core.Funds}.");
            }

            if (core.DestinationProgress != 2)
            {
                failures.Add($"Second unique destruction progress: expected 2, got {core.DestinationProgress}.");
            }

            if (rewardCount != 2)
            {
                failures.Add($"Second unique destruction reward count: expected 2, got {rewardCount}.");
            }

            var snapshot = core.CreateSnapshot();

            if (snapshot.Phase != GamePhase.Ready)
            {
                failures.Add($"Snapshot phase: expected Ready, got {snapshot.Phase}.");
            }

            if (snapshot.Funds != 20)
            {
                failures.Add($"Snapshot funds: expected 20, got {snapshot.Funds}.");
            }

            if (snapshot.CurrentDestinationId != "island-village")
            {
                failures.Add(
                    $"Snapshot destination: expected island-village, got {snapshot.CurrentDestinationId}.");
            }

            if (snapshot.DestinationProgress != 2)
            {
                failures.Add($"Snapshot progress: expected 2, got {snapshot.DestinationProgress}.");
            }

            if (snapshot.DestinationTarget != 120)
            {
                failures.Add($"Snapshot target: expected 120, got {snapshot.DestinationTarget}.");
            }

            if (snapshot.MaintenanceLevels.Count != 0)
            {
                failures.Add(
                    $"Snapshot maintenance count: expected 0, got {snapshot.MaintenanceLevels.Count}.");
            }

            if (snapshot.FirstDestroyShown)
            {
                failures.Add("Snapshot FirstDestroyShown: expected false, got true.");
            }

            if (!snapshot.CanStartStage)
            {
                failures.Add("Snapshot CanStartStage: expected true, got false.");
            }

            source.IceDestroyed -= forwardDestruction;
            core.RewardGranted -= captureReward;

            if (failures.Count == 0)
            {
                Debug.Log("[CORE-00] verification passed");
            }
            else
            {
                foreach (var failure in failures)
                {
                    Debug.LogError($"[CORE-00] {failure}");
                }
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
            }
        }
    }
}
