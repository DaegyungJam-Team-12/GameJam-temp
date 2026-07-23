#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.State;
using UnityEngine;

namespace Icebreaker.UI.Feedback
{
    public static class Ui06AudioPhasePolicy
    {
        public static bool AllowsLoops(GamePhase phase, ManagementScreen managementScreen) =>
            managementScreen == ManagementScreen.None &&
            phase is GamePhase.Countdown or GamePhase.Playing;

        public static bool RequiresFadeOut(GamePhase phase, ManagementScreen managementScreen) =>
            managementScreen == ManagementScreen.None &&
            phase == GamePhase.StageEnding;
    }

    internal sealed class Ui06AudioClipLibrary
    {
        private readonly AudioCueCatalog? catalog;
        private readonly AudioClip? lightBreak;
        private readonly AudioClip? heavyBreak;
        private readonly AudioClip? crack;
        private readonly AudioClip? crystal;
        private readonly AudioClip? critical;
        private readonly AudioClip? button;
        private readonly AudioClip? purchase;
        private readonly AudioClip? countdown;
        private readonly AudioClip? settlement;
        private readonly AudioClip? arrival;
        private readonly AudioClip? legacyAmbienceLoop;

        public Ui06AudioClipLibrary(
            AudioCueCatalog? catalog,
            AudioClip? lightBreak,
            AudioClip? heavyBreak,
            AudioClip? crack,
            AudioClip? crystal,
            AudioClip? critical,
            AudioClip? button,
            AudioClip? purchase,
            AudioClip? countdown,
            AudioClip? settlement,
            AudioClip? arrival,
            AudioClip? legacyAmbienceLoop)
        {
            this.catalog = catalog;
            this.lightBreak = lightBreak;
            this.heavyBreak = heavyBreak;
            this.crack = crack;
            this.crystal = crystal;
            this.critical = critical;
            this.button = button;
            this.purchase = purchase;
            this.countdown = countdown;
            this.settlement = settlement;
            this.arrival = arrival;
            this.legacyAmbienceLoop = legacyAmbienceLoop;
        }

        public AudioClip? MusicLoop => catalog?.StageMusicLoop;

        public AudioClip? AmbienceLoop =>
            catalog != null ? catalog.StageAmbienceLoop : legacyAmbienceLoop;

        public AudioClip? Resolve(Ui06AudioCue cue, IceTier? tier)
        {
            if (catalog != null)
            {
                return catalog.Resolve(cue, tier);
            }

            return cue switch
            {
                Ui06AudioCue.Destroy => tier == IceTier.T1
                    ? lightBreak ?? heavyBreak
                    : heavyBreak ?? lightBreak,
                Ui06AudioCue.Critical => critical,
                Ui06AudioCue.Crystal => crystal,
                Ui06AudioCue.Crack => crack,
                Ui06AudioCue.Button => button,
                Ui06AudioCue.Purchase => purchase,
                Ui06AudioCue.Countdown => countdown,
                Ui06AudioCue.Settlement => settlement,
                Ui06AudioCue.Arrival => arrival,
                _ => null
            };
        }
    }

    internal sealed class Ui06AudioController : IDisposable
    {
        public const float LoopFadeOutSeconds = 0.2f;

        private readonly AudioSource? gameplaySource;
        private readonly AudioSource? uiSource;
        private readonly AudioSource? musicSource;
        private readonly AudioSource? ambienceSource;
        private readonly Ui06AudioClipLibrary clips;
        private readonly bool allowProceduralFallback;
        private readonly int maximumDestroyVoices;
        private readonly float destroyVoiceWindowSeconds;
        private readonly float musicVolume;
        private readonly float ambienceVolume;
        private readonly Dictionary<Ui06AudioCue, AudioClip> runtimeClips = new();
        private readonly HashSet<long> playedSettlementStageIds = new();
        private readonly HashSet<string> playedArrivalDestinationIds = new(StringComparer.Ordinal);

        private GamePhase currentPhase = GamePhase.Traveling;
        private ManagementScreen currentManagementScreen = ManagementScreen.None;
        private float fadeRemaining;
        private float fadeMusicStartVolume;
        private float fadeAmbienceStartVolume;
        private float destroyVoiceWindowAge = float.PositiveInfinity;
        private int destroyVoicesInWindow;
        private bool chainRushPlayedInWindow;
        private bool suspended;
        private bool disposed;

        public Ui06AudioController(
            AudioSource? gameplaySource,
            AudioSource? uiSource,
            AudioSource? musicSource,
            AudioSource? ambienceSource,
            Ui06AudioClipLibrary clips,
            bool allowProceduralFallback,
            int maximumDestroyVoices,
            float destroyVoiceWindowSeconds)
        {
            this.gameplaySource = gameplaySource;
            this.uiSource = uiSource;
            this.musicSource = musicSource;
            this.ambienceSource = ambienceSource;
            this.clips = clips ?? throw new ArgumentNullException(nameof(clips));
            this.allowProceduralFallback = allowProceduralFallback;
            this.maximumDestroyVoices = Math.Max(1, maximumDestroyVoices);
            this.destroyVoiceWindowSeconds = Math.Max(0.001f, destroyVoiceWindowSeconds);
            musicVolume = musicSource != null ? musicSource.volume : 1f;
            ambienceVolume = ambienceSource != null ? ambienceSource.volume : 1f;

            ConfigureOneShotSource(gameplaySource);
            ConfigureOneShotSource(uiSource);
            ConfigureLoopSource(musicSource);
            ConfigureLoopSource(ambienceSource);
            StopLoopsImmediately();
        }

        public string LastAudioCue { get; private set; } = string.Empty;

        public int SettlementSoundCount { get; private set; }

        public int ArrivalSoundCount { get; private set; }

        public int CountdownSoundCount { get; private set; }

        public int ChainRushSoundCount { get; private set; }

        public int PeakDestroyVoices { get; private set; }

        public float CurrentMasterVolume => AudioListener.volume;

        public bool ArePhaseLoopsPlaying =>
            (musicSource?.isPlaying ?? false) || (ambienceSource?.isPlaying ?? false);

        public void SetMasterVolume(float value)
        {
            ThrowIfDisposed();
            AudioListener.volume = Mathf.Clamp01(value);
        }

        public void ApplyPhase(GamePhase phase)
        {
            ThrowIfDisposed();
            currentPhase = phase;
            ApplyLoopPolicy();
        }

        public void ApplyManagementScreen(ManagementScreen screen)
        {
            ThrowIfDisposed();
            currentManagementScreen = screen;
            ApplyLoopPolicy();
        }

        public void Suspend()
        {
            ThrowIfDisposed();
            suspended = true;
            StopLoopsImmediately();
        }

        public void Resume()
        {
            ThrowIfDisposed();
            suspended = false;
            ApplyLoopPolicy();
        }

        public void Tick(float unscaledDeltaTime)
        {
            ThrowIfDisposed();
            var delta = Mathf.Max(0f, unscaledDeltaTime);
            if (!float.IsPositiveInfinity(destroyVoiceWindowAge))
            {
                destroyVoiceWindowAge += delta;
            }

            if (fadeRemaining <= 0f)
            {
                return;
            }

            fadeRemaining = Mathf.Max(0f, fadeRemaining - delta);
            var ratio = LoopFadeOutSeconds > 0f
                ? fadeRemaining / LoopFadeOutSeconds
                : 0f;
            if (musicSource != null)
            {
                musicSource.volume = fadeMusicStartVolume * ratio;
            }

            if (ambienceSource != null)
            {
                ambienceSource.volume = fadeAmbienceStartVolume * ratio;
            }

            if (fadeRemaining <= 0f)
            {
                StopLoopsImmediately();
            }
        }

        public void ResetStage()
        {
            ThrowIfDisposed();
            ResetDestroyVoiceWindow();
        }

        public void PlayCue(
            Ui06AudioCue cue,
            bool ui,
            IceTier? tier = null,
            bool useImportedClip = true)
        {
            ThrowIfDisposed();
            LastAudioCue = cue.ToString();
            if (!Application.isPlaying || CurrentMasterVolume <= 0f)
            {
                return;
            }

            var clip = useImportedClip ? clips.Resolve(cue, tier) : null;
            if (clip == null && allowProceduralFallback)
            {
                EnsureRuntimeClips();
                runtimeClips.TryGetValue(cue, out clip);
            }

            var source = ui ? uiSource : gameplaySource;
            if (source != null && clip != null)
            {
                source.PlayOneShot(clip);
            }
        }

        public void PlayDestroyCue(Ui06AudioCue cue, IceTier tier, bool useImportedClip)
        {
            ThrowIfDisposed();
            TrackDestroyVoiceWindow();
            if (destroyVoicesInWindow < maximumDestroyVoices)
            {
                destroyVoicesInWindow++;
                PeakDestroyVoices = Math.Max(PeakDestroyVoices, destroyVoicesInWindow);
                PlayCue(cue, ui: false, tier, useImportedClip);
            }
            else if (!chainRushPlayedInWindow)
            {
                chainRushPlayedInWindow = true;
                ChainRushSoundCount++;
                PlayCue(Ui06AudioCue.ChainRush, ui: false);
            }
        }

        public bool PlaySettlement(long stageId)
        {
            ThrowIfDisposed();
            if (!playedSettlementStageIds.Add(stageId))
            {
                return false;
            }

            SettlementSoundCount++;
            PlayCue(Ui06AudioCue.Settlement, ui: true);
            return true;
        }

        public bool PlayArrival(string destinationId)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(destinationId))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(destinationId));
            }

            if (!playedArrivalDestinationIds.Add(destinationId))
            {
                return false;
            }

            ArrivalSoundCount++;
            PlayCue(Ui06AudioCue.Arrival, ui: true);
            return true;
        }

        public void PlayCountdown()
        {
            ThrowIfDisposed();
            CountdownSoundCount++;
            PlayCue(Ui06AudioCue.Countdown, ui: true);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            StopLoopsImmediately();
            foreach (var clip in runtimeClips.Values)
            {
                if (clip == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(clip);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(clip);
                }
            }

            runtimeClips.Clear();
            disposed = true;
        }

        private void ApplyLoopPolicy()
        {
            if (suspended)
            {
                StopLoopsImmediately();
                return;
            }

            if (Ui06AudioPhasePolicy.AllowsLoops(currentPhase, currentManagementScreen))
            {
                StartLoops();
                return;
            }

            if (Ui06AudioPhasePolicy.RequiresFadeOut(currentPhase, currentManagementScreen))
            {
                BeginFadeOut();
                return;
            }

            StopLoopsImmediately();
        }

        private void StartLoops()
        {
            fadeRemaining = 0f;
            RestoreLoopVolumes();
            StartLoop(musicSource, clips.MusicLoop);
            StartLoop(ambienceSource, clips.AmbienceLoop);
        }

        private void BeginFadeOut()
        {
            if (!ArePhaseLoopsPlaying)
            {
                StopLoopsImmediately();
                return;
            }

            fadeRemaining = LoopFadeOutSeconds;
            fadeMusicStartVolume = musicSource != null ? musicSource.volume : 0f;
            fadeAmbienceStartVolume = ambienceSource != null ? ambienceSource.volume : 0f;
        }

        private void StopLoopsImmediately()
        {
            fadeRemaining = 0f;
            musicSource?.Stop();
            ambienceSource?.Stop();
            RestoreLoopVolumes();
        }

        private void RestoreLoopVolumes()
        {
            if (musicSource != null)
            {
                musicSource.volume = musicVolume;
            }

            if (ambienceSource != null)
            {
                ambienceSource.volume = ambienceVolume;
            }
        }

        private static void StartLoop(AudioSource? source, AudioClip? clip)
        {
            if (source == null)
            {
                return;
            }

            if (clip == null)
            {
                source.Stop();
                source.clip = null;
                return;
            }

            source.clip = clip;
            if (!source.isPlaying)
            {
                source.Play();
            }
        }

        private void TrackDestroyVoiceWindow()
        {
            if (destroyVoiceWindowAge > destroyVoiceWindowSeconds)
            {
                ResetDestroyVoiceWindow();
            }
        }

        private void ResetDestroyVoiceWindow()
        {
            destroyVoiceWindowAge = 0f;
            destroyVoicesInWindow = 0;
            chainRushPlayedInWindow = false;
        }

        private void EnsureRuntimeClips()
        {
            if (runtimeClips.Count > 0)
            {
                return;
            }

            runtimeClips[Ui06AudioCue.Hit] = CreateTone("UI06_Hit", 720f, 0.045f, 0.11f);
            runtimeClips[Ui06AudioCue.Destroy] = CreateTone("UI06_Destroy", 330f, 0.09f, 0.15f, 0.25f);
            runtimeClips[Ui06AudioCue.Critical] = CreateTone("UI06_Critical", 1320f, 0.08f, 0.12f);
            runtimeClips[Ui06AudioCue.Crystal] = CreateTone("UI06_Crystal", 1040f, 0.14f, 0.13f, 0.5f);
            runtimeClips[Ui06AudioCue.Crack] = CreateTone("UI06_Crack", 235f, 0.16f, 0.17f, 0.35f);
            runtimeClips[Ui06AudioCue.Chain] = CreateTone("UI06_Chain", 620f, 0.12f, 0.13f, 0.4f);
            runtimeClips[Ui06AudioCue.ChainRush] = CreateTone("UI06_ChainRush", 460f, 0.18f, 0.12f, 0.7f);
            runtimeClips[Ui06AudioCue.ChargeReady] = CreateTone("UI06_ChargeReady", 910f, 0.11f, 0.11f, 0.5f);
            runtimeClips[Ui06AudioCue.SupportFire] = CreateTone("UI06_SupportFire", 510f, 0.13f, 0.16f, 0.25f);
            runtimeClips[Ui06AudioCue.Button] = CreateTone("UI06_Button", 760f, 0.035f, 0.075f);
            runtimeClips[Ui06AudioCue.Countdown] = CreateTone("UI06_Countdown", 620f, 0.12f, 0.1f, 0.5f);
            runtimeClips[Ui06AudioCue.StageStart] = CreateTone("UI06_StageStart", 440f, 0.12f, 0.1f, 0.5f);
            runtimeClips[Ui06AudioCue.StageEnd] = CreateTone("UI06_StageEnd", 285f, 0.16f, 0.12f, 0.3f);
            runtimeClips[Ui06AudioCue.Settlement] = CreateTone("UI06_Settlement", 880f, 0.15f, 0.1f, 0.5f);
            runtimeClips[Ui06AudioCue.Purchase] = CreateTone("UI06_Purchase", 980f, 0.12f, 0.1f, 0.5f);
            runtimeClips[Ui06AudioCue.Arrival] = CreateTone("UI06_Arrival", 390f, 0.22f, 0.12f, 0.3f);
        }

        private static AudioClip CreateTone(
            string name,
            float frequency,
            float duration,
            float amplitude,
            float harmonic = 0f)
        {
            const int sampleRate = 44_100;
            var sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
            var samples = new float[sampleCount];
            for (var index = 0; index < sampleCount; index++)
            {
                var time = (float)index / sampleRate;
                var progress = (float)index / sampleCount;
                var envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress)) * (1f - progress * 0.35f);
                var fundamental = Mathf.Sin(2f * Mathf.PI * frequency * time);
                var overtone = Mathf.Sin(2f * Mathf.PI * frequency * 2.01f * time) * harmonic;
                samples[index] = (fundamental + overtone) * envelope * amplitude;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(samples, 0);
            return clip;
        }

        private static void ConfigureOneShotSource(AudioSource? source)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
        }

        private static void ConfigureLoopSource(AudioSource? source)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(Ui06AudioController));
            }
        }
    }
}
