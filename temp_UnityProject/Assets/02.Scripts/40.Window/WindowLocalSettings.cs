#nullable enable

using System;

namespace Icebreaker.Window
{
    /// <summary>Snapshot of the persisted window placement settings.</summary>
    public readonly struct WindowSettingsData
    {
        public WindowSettingsData(
            int version,
            WindowPositionMode positionMode,
            WindowPositionPreset positionPreset,
            WindowSizePreset sizePreset,
            string monitorId,
            float normalizedX,
            float normalizedY)
        {
            Version = version;
            PositionMode = positionMode;
            PositionPreset = positionPreset;
            SizePreset = sizePreset;
            MonitorId = monitorId ?? string.Empty;
            NormalizedX = normalizedX;
            NormalizedY = normalizedY;
        }

        public int Version { get; }

        public WindowPositionMode PositionMode { get; }

        public WindowPositionPreset PositionPreset { get; }

        public WindowSizePreset SizePreset { get; }

        public string MonitorId { get; }

        public float NormalizedX { get; }

        public float NormalizedY { get; }

        public WindowSettingsData WithPositionPreset(WindowPositionPreset preset) =>
            new(Version, WindowPositionMode.Preset, preset, SizePreset, MonitorId, NormalizedX, NormalizedY);

        public WindowSettingsData WithCustomPosition(string monitorId, float normalizedX, float normalizedY) =>
            new(Version, WindowPositionMode.Custom, PositionPreset, SizePreset, monitorId, normalizedX, normalizedY);

        public WindowSettingsData WithSizePreset(WindowSizePreset preset) =>
            new(Version, PositionMode, PositionPreset, preset, MonitorId, NormalizedX, NormalizedY);
    }

    /// <summary>
    /// Device-local window placement settings (PlayerPrefs-backed), kept entirely separate from
    /// the game-progress SaveData. A game save-reset must not clear any of these keys, and
    /// position/size resets here must not touch SaveData.
    /// </summary>
    public sealed class WindowLocalSettings
    {
        public const int CurrentVersion = 1;

        public const string VersionKey = "icebreaker.window.version-v1";
        public const string PositionModeKey = "icebreaker.window.position-mode-v1";
        public const string PositionPresetKey = "icebreaker.window.position-preset-v1";
        public const string SizePresetKey = "icebreaker.window.size-preset-v1";
        public const string MonitorIdKey = "icebreaker.window.monitor-id-v1";
        public const string NormalizedXKey = "icebreaker.window.normalized-x-v1";
        public const string NormalizedYKey = "icebreaker.window.normalized-y-v1";

        public static readonly WindowPositionMode DefaultPositionMode = WindowPositionMode.Preset;
        public static readonly WindowPositionPreset DefaultPositionPreset = WindowPositionPreset.BottomCenter;
        public static readonly WindowSizePreset DefaultSizePreset = WindowSizePreset.Default;

        private readonly IWindowSettingsStore store;

        public WindowLocalSettings(IWindowSettingsStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public static WindowLocalSettings CreateDefault() => new(PlayerPrefsWindowSettingsStore.Instance);

        public WindowSettingsData Load()
        {
            var positionMode = store.HasKey(PositionModeKey)
                ? (WindowPositionMode)store.GetInt(PositionModeKey, (int)DefaultPositionMode)
                : DefaultPositionMode;
            var positionPreset = store.HasKey(PositionPresetKey)
                ? (WindowPositionPreset)store.GetInt(PositionPresetKey, (int)DefaultPositionPreset)
                : DefaultPositionPreset;
            var sizePreset = store.HasKey(SizePresetKey)
                ? (WindowSizePreset)store.GetInt(SizePresetKey, (int)DefaultSizePreset)
                : DefaultSizePreset;
            var monitorId = store.GetString(MonitorIdKey, string.Empty);
            var normalizedX = store.GetFloat(NormalizedXKey, 0.5f);
            var normalizedY = store.GetFloat(NormalizedYKey, 1f);

            return new WindowSettingsData(
                CurrentVersion,
                positionMode,
                positionPreset,
                sizePreset,
                monitorId,
                normalizedX,
                normalizedY);
        }

        /// <summary>Persists a position preset selection (switches mode to Preset).</summary>
        public void SavePositionPreset(WindowPositionPreset preset)
        {
            store.SetInt(VersionKey, CurrentVersion);
            store.SetInt(PositionModeKey, (int)WindowPositionMode.Preset);
            store.SetInt(PositionPresetKey, (int)preset);
            store.Save();
        }

        /// <summary>Persists a user-dragged (Custom) position, exactly once, at drag end.</summary>
        public void SaveCustomPosition(string monitorId, float normalizedX, float normalizedY)
        {
            store.SetInt(VersionKey, CurrentVersion);
            store.SetInt(PositionModeKey, (int)WindowPositionMode.Custom);
            store.SetString(MonitorIdKey, monitorId ?? string.Empty);
            store.SetFloat(NormalizedXKey, normalizedX);
            store.SetFloat(NormalizedYKey, normalizedY);
            store.Save();
        }

        public void SaveSizePreset(WindowSizePreset preset)
        {
            store.SetInt(VersionKey, CurrentVersion);
            store.SetInt(SizePresetKey, (int)preset);
            store.Save();
        }

        /// <summary>Resets only the position fields; size fields are left untouched.</summary>
        public void ResetPosition()
        {
            store.DeleteKey(PositionModeKey);
            store.DeleteKey(PositionPresetKey);
            store.DeleteKey(MonitorIdKey);
            store.DeleteKey(NormalizedXKey);
            store.DeleteKey(NormalizedYKey);
            store.Save();
        }

        /// <summary>Resets only the size field; position fields are left untouched.</summary>
        public void ResetSize()
        {
            store.DeleteKey(SizePresetKey);
            store.Save();
        }
    }
}
