#nullable enable

using UnityEngine;

namespace Icebreaker.UI.Feedback
{
    public static class UiAudioSettings
    {
        public const float DefaultMasterVolume = 0f;
        public const string MasterVolumePlayerPrefsKey = "icebreaker.master-volume-v1";

        public static bool HasSavedMasterVolume => PlayerPrefs.HasKey(MasterVolumePlayerPrefsKey);

        public static float LoadAndApplyMasterVolume()
        {
            var volume = PlayerPrefs.HasKey(MasterVolumePlayerPrefsKey)
                ? Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumePlayerPrefsKey))
                : DefaultMasterVolume;
            AudioListener.volume = volume;
            return volume;
        }

        public static void SetMasterVolume(float value)
        {
            var volume = Mathf.Clamp01(value);
            AudioListener.volume = volume;
            PlayerPrefs.SetFloat(MasterVolumePlayerPrefsKey, volume);
            PlayerPrefs.Save();
        }
    }
}
