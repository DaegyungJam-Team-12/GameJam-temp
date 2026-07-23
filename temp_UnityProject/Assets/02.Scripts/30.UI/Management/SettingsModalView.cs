#nullable enable

using System;
using Icebreaker.UI.Feedback;
using Icebreaker.Window;
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

        private const string PositionBottomLeftLabel = "왼쪽 아래";
        private const string PositionBottomCenterLabel = "가운데 아래";
        private const string PositionBottomRightLabel = "오른쪽 아래";
        private const string SizeDefaultLabel = "기본";
        private const string SizeLargeLabel = "크게";
        private const string SizeExtraLargeLabel = "매우 크게";
        private const string SelectedPrefix = "✓ ";

        [SerializeField] private Slider? masterVolumeSlider;
        [SerializeField] private Toggle? screenShakeToggle;
        [SerializeField] private Button? closeButton;
        [SerializeField] private Button? quitButton;
        [SerializeField] private Button? resetSaveButton;
        [SerializeField] private TMP_Text? resetSaveButtonText;

        [SerializeField] private Button? positionBottomLeftButton;
        [SerializeField] private Button? positionBottomCenterButton;
        [SerializeField] private Button? positionBottomRightButton;
        [SerializeField] private Button? positionResetButton;
        [SerializeField] private TMP_Text? positionBottomLeftText;
        [SerializeField] private TMP_Text? positionBottomCenterText;
        [SerializeField] private TMP_Text? positionBottomRightText;

        [SerializeField] private Button? sizeDefaultButton;
        [SerializeField] private Button? sizeLargeButton;
        [SerializeField] private Button? sizeExtraLargeButton;
        [SerializeField] private Button? sizeResetButton;
        [SerializeField] private TMP_Text? sizeDefaultText;
        [SerializeField] private TMP_Text? sizeLargeText;
        [SerializeField] private TMP_Text? sizeExtraLargeText;

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

            positionBottomLeftButton?.onClick.RemoveListener(HandlePositionBottomLeft);
            positionBottomCenterButton?.onClick.RemoveListener(HandlePositionBottomCenter);
            positionBottomRightButton?.onClick.RemoveListener(HandlePositionBottomRight);
            positionResetButton?.onClick.RemoveListener(HandlePositionReset);
            sizeDefaultButton?.onClick.RemoveListener(HandleSizeDefault);
            sizeLargeButton?.onClick.RemoveListener(HandleSizeLarge);
            sizeExtraLargeButton?.onClick.RemoveListener(HandleSizeExtraLarge);
            sizeResetButton?.onClick.RemoveListener(HandleSizeReset);
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

            positionBottomLeftButton?.onClick.AddListener(HandlePositionBottomLeft);
            positionBottomCenterButton?.onClick.AddListener(HandlePositionBottomCenter);
            positionBottomRightButton?.onClick.AddListener(HandlePositionBottomRight);
            positionResetButton?.onClick.AddListener(HandlePositionReset);
            sizeDefaultButton?.onClick.AddListener(HandleSizeDefault);
            sizeLargeButton?.onClick.AddListener(HandleSizeLarge);
            sizeExtraLargeButton?.onClick.AddListener(HandleSizeExtraLarge);
            sizeResetButton?.onClick.AddListener(HandleSizeReset);

            initialized = true;
            RefreshWindowControls();
        }

        public void Open()
        {
            EnsureInitialized();
            RefreshWindowControls();
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

        private void HandlePositionBottomLeft() => ApplyPositionPreset(WindowPositionPreset.BottomLeft);

        private void HandlePositionBottomCenter() => ApplyPositionPreset(WindowPositionPreset.BottomCenter);

        private void HandlePositionBottomRight() => ApplyPositionPreset(WindowPositionPreset.BottomRight);

        private void HandlePositionReset()
        {
            WindowBootstrap.Instance?.ResetPosition();
            RefreshWindowControls();
        }

        private void ApplyPositionPreset(WindowPositionPreset preset)
        {
            WindowBootstrap.Instance?.ApplyPositionPreset(preset);
            RefreshWindowControls();
        }

        private void HandleSizeDefault() => ApplySizePreset(WindowSizePreset.Default);

        private void HandleSizeLarge() => ApplySizePreset(WindowSizePreset.Large);

        private void HandleSizeExtraLarge() => ApplySizePreset(WindowSizePreset.ExtraLarge);

        private void HandleSizeReset()
        {
            WindowBootstrap.Instance?.ResetSize();
            RefreshWindowControls();
        }

        private void ApplySizePreset(WindowSizePreset preset)
        {
            WindowBootstrap.Instance?.ApplySizePreset(preset);
            RefreshWindowControls();
        }

        /// <summary>Reflects the current position/size preset selection and disables size
        /// presets that would not fit the current monitor's work area.</summary>
        private void RefreshWindowControls()
        {
            var bootstrap = WindowBootstrap.Instance;
            var positionMode = bootstrap?.CurrentPositionMode ?? WindowPositionMode.Preset;
            var positionPreset = bootstrap?.CurrentPositionPreset ?? WindowPositionPreset.BottomCenter;

            SetPresetLabel(
                positionBottomLeftText,
                PositionBottomLeftLabel,
                positionMode == WindowPositionMode.Preset && positionPreset == WindowPositionPreset.BottomLeft);
            SetPresetLabel(
                positionBottomCenterText,
                PositionBottomCenterLabel,
                positionMode == WindowPositionMode.Preset && positionPreset == WindowPositionPreset.BottomCenter);
            SetPresetLabel(
                positionBottomRightText,
                PositionBottomRightLabel,
                positionMode == WindowPositionMode.Preset && positionPreset == WindowPositionPreset.BottomRight);

            var sizePreset = bootstrap?.CurrentSizePreset ?? WindowSizePreset.Default;
            SetPresetLabel(sizeDefaultText, SizeDefaultLabel, sizePreset == WindowSizePreset.Default);
            SetPresetLabel(sizeLargeText, SizeLargeLabel, sizePreset == WindowSizePreset.Large);
            SetPresetLabel(sizeExtraLargeText, SizeExtraLargeLabel, sizePreset == WindowSizePreset.ExtraLarge);

            if (sizeLargeButton != null)
            {
                sizeLargeButton.interactable = bootstrap?.IsSizePresetAvailable(WindowSizePreset.Large) ?? true;
            }

            if (sizeExtraLargeButton != null)
            {
                sizeExtraLargeButton.interactable = bootstrap?.IsSizePresetAvailable(WindowSizePreset.ExtraLarge) ?? true;
            }
        }

        private static void SetPresetLabel(TMP_Text? text, string baseLabel, bool selected)
        {
            if (text != null)
            {
                text.text = selected ? SelectedPrefix + baseLabel : baseLabel;
            }
        }

        private void HandleScreenShakeChanged(bool value) => ScreenShakeChanged(value);
    }
}
