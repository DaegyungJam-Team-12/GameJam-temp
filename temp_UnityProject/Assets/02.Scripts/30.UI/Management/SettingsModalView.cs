#nullable enable

using System;
using Icebreaker.UI.Feedback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.UI.Management
{
    /// <summary>Owns settings modal visibility, controls, and user interaction.</summary>
    public sealed class SettingsModalView : MonoBehaviour
    {
        public const float ResetSaveConfirmWindowSeconds = 3f;

        private const string ResetSaveDefaultText = "저장 초기화";
        private const string ResetSaveConfirmText = "정말 초기화? (다시 클릭)";

        [SerializeField] private Slider? masterVolumeSlider;
        [SerializeField] private Toggle? screenShakeToggle;
        [SerializeField] private Button? closeButton;
        [SerializeField] private Button? quitButton;
        [SerializeField] private Button? resetSaveButton;
        [SerializeField] private TMP_Text? resetSaveButtonText;

        private bool initialized;
        private bool resetSaveConfirmArmed;
        private float resetSaveConfirmElapsed;

        public event Action<bool> VisibilityChanged = delegate { };
        public event Action<float> MasterVolumeChanged = delegate { };
        public event Action<bool> ScreenShakeChanged = delegate { };
        public event Action QuitRequested = delegate { };
        public event Action ResetSaveRequested = delegate { };

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
            resetSaveButton?.onClick.RemoveListener(HandleResetSaveClicked);
            masterVolumeSlider?.onValueChanged.RemoveListener(HandleMasterVolumeChanged);
            screenShakeToggle?.onValueChanged.RemoveListener(HandleScreenShakeChanged);
        }

        private void Update()
        {
            if (!resetSaveConfirmArmed)
            {
                return;
            }

            resetSaveConfirmElapsed += Time.unscaledDeltaTime;
            if (resetSaveConfirmElapsed >= ResetSaveConfirmWindowSeconds)
            {
                CancelResetSaveConfirm();
            }
        }

        public void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            closeButton?.onClick.AddListener(Close);
            quitButton?.onClick.AddListener(HandleQuit);
            resetSaveButton?.onClick.AddListener(HandleResetSaveClicked);
            masterVolumeSlider?.onValueChanged.AddListener(HandleMasterVolumeChanged);
            screenShakeToggle?.onValueChanged.AddListener(HandleScreenShakeChanged);
            masterVolumeSlider?.SetValueWithoutNotify(UiAudioSettings.LoadAndApplyMasterVolume());
            SetResetSaveButtonText(ResetSaveDefaultText);
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

            CancelResetSaveConfirm();
            gameObject.SetActive(false);
            VisibilityChanged(false);
        }

        public void SetValues(float masterVolume, bool screenShakeEnabled)
        {
            masterVolumeSlider?.SetValueWithoutNotify(Mathf.Clamp01(masterVolume));
            screenShakeToggle?.SetIsOnWithoutNotify(screenShakeEnabled);
        }

        private void HandleQuit() => QuitRequested();

        private void HandleResetSaveClicked()
        {
            if (!resetSaveConfirmArmed)
            {
                resetSaveConfirmArmed = true;
                resetSaveConfirmElapsed = 0f;
                SetResetSaveButtonText(ResetSaveConfirmText);
                return;
            }

            CancelResetSaveConfirm();
            ResetSaveRequested();
        }

        private void CancelResetSaveConfirm()
        {
            resetSaveConfirmArmed = false;
            resetSaveConfirmElapsed = 0f;
            SetResetSaveButtonText(ResetSaveDefaultText);
        }

        private void SetResetSaveButtonText(string value)
        {
            if (resetSaveButtonText != null)
            {
                resetSaveButtonText.text = value;
            }
        }

        private void HandleMasterVolumeChanged(float value)
        {
            var clamped = Mathf.Clamp01(value);
            UiAudioSettings.SetMasterVolume(clamped);
            MasterVolumeChanged(clamped);
        }

        private void HandleScreenShakeChanged(bool value) => ScreenShakeChanged(value);
    }
}
