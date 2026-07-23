#nullable enable

using UnityEngine;

namespace Icebreaker.Window
{
    /// <summary>Device-local (PlayerPrefs) backing for <see cref="WindowLocalSettings"/>.</summary>
    public sealed class PlayerPrefsWindowSettingsStore : IWindowSettingsStore
    {
        public static readonly PlayerPrefsWindowSettingsStore Instance = new();

        private PlayerPrefsWindowSettingsStore()
        {
        }

        public bool HasKey(string key) => PlayerPrefs.HasKey(key);

        public int GetInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);

        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);

        public float GetFloat(string key, float defaultValue) => PlayerPrefs.GetFloat(key, defaultValue);

        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);

        public string GetString(string key, string defaultValue) => PlayerPrefs.GetString(key, defaultValue);

        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);

        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);

        public void Save() => PlayerPrefs.Save();
    }
}
