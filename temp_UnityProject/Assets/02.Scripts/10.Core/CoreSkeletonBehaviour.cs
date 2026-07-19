#nullable enable

using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Progression;
using UnityEngine;

namespace Icebreaker.Core
{
    public sealed class CoreSkeletonBehaviour : MonoBehaviour
    {
        [SerializeField] private long displayFunds;
        [SerializeField] private int displayProgress;

        private FakeCombatEventSource? source;
        private ProgressionCore? core;
        private long nextIceInstanceId = 1;
        private long nextChainId = 1;
        private IceDestroyedEvent lastPublishedEvent;
        private bool hasPublishedEvent;

        private void Awake()
        {
            var destination = new DestinationDefinition(
                "island-village",
                "섬마을",
                120,
                "식료품·우편",
                0);

            source = new FakeCombatEventSource();
            core = new ProgressionCore(destination);
        }

        private void OnEnable()
        {
            if (source == null || core == null)
            {
                Debug.LogError("[CORE-00] Awake has not initialized the core skeleton.", this);
                return;
            }

            source.IceDestroyed += HandleIceDestroyed;
            core.RewardGranted += OnRewardGranted;
        }

        private void OnDisable()
        {
            if (source != null)
            {
                source.IceDestroyed -= HandleIceDestroyed;
            }

            if (core != null)
            {
                core.RewardGranted -= OnRewardGranted;
            }
        }

        [ContextMenu("Fake Destroy (New Ice)")]
        private void FakeDestroyNewIce()
        {
            if (core == null)
            {
                Debug.LogError("[CORE-00] Awake has not initialized ProgressionCore.", this);
                return;
            }

            if (source == null)
            {
                Debug.LogError("[CORE-00] Awake has not initialized FakeCombatEventSource.", this);
                return;
            }

            lastPublishedEvent = new IceDestroyedEvent(
                1,
                nextIceInstanceId++,
                nextChainId++,
                0,
                IceTier.T1,
                SpecialIceType.None,
                DestroyCategory.Direct,
                EffectType.Click,
                new Vector2(480f, 270f),
                0d);
            hasPublishedEvent = true;

            source.PublishIceDestroyed(lastPublishedEvent);
            RefreshDisplayAndLog(core);
        }

        [ContextMenu("Fake Destroy (Duplicate Last)")]
        private void FakeDestroyDuplicateLast()
        {
            if (core == null)
            {
                Debug.LogError("[CORE-00] Awake has not initialized ProgressionCore.", this);
                return;
            }

            if (source == null)
            {
                Debug.LogError("[CORE-00] Awake has not initialized FakeCombatEventSource.", this);
                return;
            }

            if (!hasPublishedEvent)
            {
                Debug.LogWarning("[CORE-00] Publish a new ice destruction before publishing a duplicate.", this);
                return;
            }

            source.PublishIceDestroyed(lastPublishedEvent);
            RefreshDisplayAndLog(core);
        }

        private void HandleIceDestroyed(IceDestroyedEvent payload)
        {
            core?.HandleIceDestroyed(payload);
        }

        private void OnRewardGranted(RewardGrantedEvent payload)
        {
            Debug.Log(
                $"[CORE-00] Reward granted: funds +{payload.FundsGranted}, progress +{payload.DestinationProgressGranted}.",
                this);
        }

        private void RefreshDisplayAndLog(ProgressionCore initializedCore)
        {
            var snapshot = initializedCore.CreateSnapshot();
            displayFunds = snapshot.Funds;
            displayProgress = snapshot.DestinationProgress;

            Debug.Log(
                $"[CORE-00] Snapshot: funds {displayFunds}, progress {displayProgress}/{snapshot.DestinationTarget}.",
                this);
        }
    }
}
