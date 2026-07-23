#nullable enable

using Icebreaker.Shared.Combat;
using UnityEngine;

namespace Icebreaker.UI.Feedback
{
    public enum Ui06AudioCue
    {
        Hit,
        Destroy,
        Critical,
        Crystal,
        Crack,
        Chain,
        ChainRush,
        ChargeReady,
        SupportFire,
        Button,
        Countdown,
        StageStart,
        StageEnd,
        Settlement,
        Purchase,
        Arrival
    }

    [CreateAssetMenu(
        fileName = "AudioCueCatalog",
        menuName = "Icebreaker/Audio/Audio Cue Catalog")]
    public sealed class AudioCueCatalog : ScriptableObject
    {
        [Header("Phase Loops")]
        [SerializeField] private AudioClip? stageMusicLoop;
        [SerializeField] private AudioClip? stageAmbienceLoop;

        [Header("Gameplay SFX")]
        [SerializeField] private AudioClip? hit;
        [SerializeField] private AudioClip? tier1Destroy;
        [SerializeField] private AudioClip? tier2Destroy;
        [SerializeField] private AudioClip? tier3Destroy;
        [SerializeField] private AudioClip? critical;
        [SerializeField] private AudioClip? crystal;
        [SerializeField] private AudioClip? crack;
        [SerializeField] private AudioClip? chain;
        [SerializeField] private AudioClip? chainRush;
        [SerializeField] private AudioClip? chargeReady;
        [SerializeField] private AudioClip? supportFire;

        [Header("Progression and UI SFX")]
        [SerializeField] private AudioClip? button;
        [SerializeField] private AudioClip? countdown;
        [SerializeField] private AudioClip? stageStart;
        [SerializeField] private AudioClip? stageEnd;
        [SerializeField] private AudioClip? settlement;
        [SerializeField] private AudioClip? purchase;
        [SerializeField] private AudioClip? arrival;

        public AudioClip? StageMusicLoop => stageMusicLoop;

        public AudioClip? StageAmbienceLoop => stageAmbienceLoop;

        public AudioClip? Resolve(Ui06AudioCue cue, IceTier? tier = null) =>
            cue switch
            {
                Ui06AudioCue.Hit => hit,
                Ui06AudioCue.Destroy => ResolveDestroy(tier),
                Ui06AudioCue.Critical => critical,
                Ui06AudioCue.Crystal => crystal,
                Ui06AudioCue.Crack => crack,
                Ui06AudioCue.Chain => chain,
                Ui06AudioCue.ChainRush => chainRush,
                Ui06AudioCue.ChargeReady => chargeReady,
                Ui06AudioCue.SupportFire => supportFire,
                Ui06AudioCue.Button => button,
                Ui06AudioCue.Countdown => countdown,
                Ui06AudioCue.StageStart => stageStart,
                Ui06AudioCue.StageEnd => stageEnd,
                Ui06AudioCue.Settlement => settlement,
                Ui06AudioCue.Purchase => purchase,
                Ui06AudioCue.Arrival => arrival,
                _ => null
            };

        private AudioClip? ResolveDestroy(IceTier? tier) =>
            tier switch
            {
                IceTier.T1 => tier1Destroy ?? tier2Destroy ?? tier3Destroy,
                IceTier.T2 => tier2Destroy ?? tier1Destroy ?? tier3Destroy,
                IceTier.T3 => tier3Destroy ?? tier2Destroy ?? tier1Destroy,
                _ => tier1Destroy ?? tier2Destroy ?? tier3Destroy
            };
    }
}
