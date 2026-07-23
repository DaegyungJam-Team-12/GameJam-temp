#nullable enable

namespace Icebreaker.Window
{
    /// <summary>
    /// Thin key-value store seam so <see cref="WindowLocalSettings"/> can be exercised by
    /// EditMode tests without touching PlayerPrefs.
    /// </summary>
    public interface IWindowSettingsStore
    {
        bool HasKey(string key);

        int GetInt(string key, int defaultValue);

        void SetInt(string key, int value);

        float GetFloat(string key, float defaultValue);

        void SetFloat(string key, float value);

        string GetString(string key, string defaultValue);

        void SetString(string key, string value);

        void DeleteKey(string key);

        void Save();
    }
}
