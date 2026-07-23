#nullable enable

using System;
using Icebreaker.UI.Feedback;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.UI.Management
{
    /// <summary>Owns settings modal visibility, controls, and user interaction.</summary>
    public sealed class SettingsModalView : MonoBehaviour
    {
        [SerializeField] private Slider? masterVolumeSlider;
        [SerializeField] private Toggle? screenShakeToggle;
        [SerializeField] private Button? closeButton;
        [SerializeField] private Button? quitButton;

        private bool initialized;

        public event Action<bool> VisibilityChanged = delegate { };
        public event Action<float> MasterVolumeChanged = delegate { };
        public event Action<bool> ScreenShakeChanged = delegate { };
        public event Action QuitRequested = delegate { };

        public bool IsVisible => gameObject.activeSelf;

        private void Awake() => EnsureInitialized();

        private void OnEnable() => EnsureInitialized();

        private void OnDestroy()
        {
            if (!initialized)
            {
                return;
            }

            closeButton?.onClick.RemoveListener(Close);
            quitButton?.onClick.RemoveListener(HandleQuit);
            masterVolumeSlider?.onValueChanged.RemoveListener(HandleMasterVolumeChanged);
            screenShakeToggle?.onValueChanged.RemoveListener(HandleScreenShakeChanged);
        }

        public void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            closeButton?.onClick.AddListener(Close);
            quitButton?.onClick.AddListener(HandleQuit);
            masterVolumeSlider?.onValueChanged.AddListener(HandleMasterVolumeChanged);
            screenShakeToggle?.onValueChanged.AddListener(HandleScreenShakeChanged);
            masterVolumeSlider?.SetValueWithoutNotify(UiAudioSettings.LoadAndApplyMasterVolume());
            initialized = true;
        }

        public void Open()
        {
            EnsureInitialized();
            if (IsVisible)
            {
                return;
            }

            gameObject.SetActive(true);
            VisibilityChanged(true);
        }

        public void Close()
        {
            if (!IsVisible)
            {
                return;
            }

            gameObject.SetActive(false);
            VisibilityChanged(false);
        }

        public void SetValues(float masterVolume, bool screenShakeEnabled)
        {
            masterVolumeSlider?.SetValueWithoutNotify(Mathf.Clamp01(masterVolume));
            screenShakeToggle?.SetIsOnWithoutNotify(screenShakeEnabled);
        }

        private void HandleQuit() => QuitRequested();

        private void HandleMasterVolumeChanged(float value)
        {
            var clamped = Mathf.Clamp01(value);
            UiAudioSettings.SetMasterVolume(clamped);
            MasterVolumeChanged(clamped);
        }

        private void HandleScreenShakeChanged(bool value) => ScreenShakeChanged(value);
    }
}
