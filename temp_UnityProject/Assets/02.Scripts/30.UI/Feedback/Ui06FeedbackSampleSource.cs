#nullable enable

using System;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Progression;
using UnityEngine;

namespace Icebreaker.UI.Feedback
{
    public sealed class Ui06FeedbackSampleSource : MonoBehaviour, ICombatEventSource, IProgressionEventSource
    {
        private const int DefaultMaximumCharge = 12;

        private long stageId = 1L;
        private long nextIceId = 1L;
        private long nextChainId = 1L;
        private int currentCharge;

        public event Action<DamageAppliedEvent> DamageApplied = delegate { };
        public event Action<SupportChargeChangedEvent> SupportChargeChanged = delegate { };
        public event Action<IceDestroyedEvent> IceDestroyed = delegate { };
        public event Action<StageStarted> StageStarted = delegate { };
        public event Action<RewardGrantedEvent> RewardGranted = delegate { };
        public event Action<StageEnded> StageEnded = delegate { };
        public event Action<SettlementReady> SettlementReady = delegate { };
        public event Action<ArrivalPresentationRequested> ArrivalPresentationRequested = delegate { };

        public int CurrentCharge => currentCharge;

        public long CurrentStageId => stageId;

        [ContextMenu("UI-06/Reset Sample")]
        public void ResetSample()
        {
            stageId = 1L;
            nextIceId = 1L;
            nextChainId = 1L;
            currentCharge = 0;
            SupportChargeChanged(new SupportChargeChangedEvent(stageId, 0, DefaultMaximumCharge));
        }

        [ContextMenu("UI-06/Add Valid Charge")]
        public void AddValidCharge()
        {
            currentCharge = Mathf.Min(DefaultMaximumCharge, currentCharge + 1);
            SupportChargeChanged(new SupportChargeChangedEvent(stageId, currentCharge, DefaultMaximumCharge));
        }

        [ContextMenu("UI-06/Complete Charge")]
        public void CompleteCharge()
        {
            currentCharge = DefaultMaximumCharge;
            SupportChargeChanged(new SupportChargeChangedEvent(stageId, currentCharge, DefaultMaximumCharge));
        }

        [ContextMenu("UI-06/Fire Support Shot")]
        public void FireSupportShot()
        {
            currentCharge = 0;
            SupportChargeChanged(new SupportChargeChangedEvent(stageId, currentCharge, DefaultMaximumCharge));
            DamageApplied(new DamageAppliedEvent(
                stageId,
                nextIceId++,
                nextChainId++,
                0,
                EffectType.SupportShot,
                12f,
                18f,
                false,
                new Vector2(640f, 260f),
                18d));
        }

        [ContextMenu("UI-06/Show Critical")]
        public void ShowCritical()
        {
            DamageApplied(new DamageAppliedEvent(
                stageId,
                nextIceId,
                nextChainId++,
                0,
                EffectType.Click,
                30f,
                4f,
                true,
                new Vector2(470f, 260f),
                20d));
        }

        [ContextMenu("UI-06/Show Crystal Ice")]
        public void ShowCrystalIce()
        {
            PublishDestroyed(
                SpecialIceType.Crystal,
                DestroyCategory.Direct,
                EffectType.Click,
                0,
                nextChainId++,
                new Vector2(390f, 250f));
        }

        [ContextMenu("UI-06/Show Crack Ice")]
        public void ShowCrackIce()
        {
            PublishDestroyed(
                SpecialIceType.Crack,
                DestroyCategory.Direct,
                EffectType.Click,
                0,
                nextChainId++,
                new Vector2(560f, 250f));
        }

        [ContextMenu("UI-06/Show Five Chain")]
        public void ShowFiveChain()
        {
            var chainId = nextChainId++;
            PublishDestroyed(
                SpecialIceType.None,
                DestroyCategory.Direct,
                EffectType.Click,
                0,
                chainId,
                new Vector2(370f, 235f));
            for (var index = 1; index < 5; index++)
            {
                PublishDestroyed(
                    SpecialIceType.None,
                    DestroyCategory.Chain,
                    EffectType.HullFragment,
                    Mathf.Min(index, 3),
                    chainId,
                    new Vector2(370f + index * 58f, 235f + (index % 2 == 0 ? 35f : -25f)));
            }
        }

        [ContextMenu("UI-06/Show Twenty Destroy Burst")]
        public void ShowTwentyDestroyBurst()
        {
            var chainId = nextChainId++;
            for (var index = 0; index < 20; index++)
            {
                PublishDestroyed(
                    SpecialIceType.None,
                    index == 0 ? DestroyCategory.Direct : DestroyCategory.Chain,
                    index == 0 ? EffectType.Click : EffectType.HullFragment,
                    Mathf.Min(index, 3),
                    chainId,
                    new Vector2(280f + index * 20f, 240f));
            }
        }

        [ContextMenu("UI-06/Start Stage")]
        public void StartStage()
        {
            stageId++;
            currentCharge = 0;
            StageStarted(new StageStarted(stageId, DateTimeOffset.UtcNow.ToString("O"), 60f));
            SupportChargeChanged(new SupportChargeChangedEvent(stageId, 0, DefaultMaximumCharge));
        }

        [ContextMenu("UI-06/End Stage")]
        public void EndStage() => StageEnded(new StageEnded(stageId, DateTimeOffset.UtcNow.ToString("O")));

        [ContextMenu("UI-06/Show Settlement Twice")]
        public void ShowSettlementTwice()
        {
            var settlement = new SettlementReady(
                stageId,
                new SettlementSummary(
                    earnedFunds: 1_240L,
                    destroyedCount: 37,
                    destinationProgressGain: 37,
                    reachedDestination: false,
                    destinationId: null));
            SettlementReady(settlement);
            SettlementReady(settlement);
        }

        [ContextMenu("UI-06/Show Arrival")]
        public void ShowArrival() =>
            ArrivalPresentationRequested(new ArrivalPresentationRequested("harbor-sample", "샘플 항구"));

        private void PublishDestroyed(
            SpecialIceType specialType,
            DestroyCategory category,
            EffectType effectType,
            int chainDepth,
            long chainId,
            Vector2 position)
        {
            IceDestroyed(new IceDestroyedEvent(
                stageId,
                nextIceId++,
                chainId,
                chainDepth,
                IceTier.T2,
                specialType,
                category,
                effectType,
                position,
                22d));
        }
    }
}
