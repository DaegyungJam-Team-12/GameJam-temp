#nullable enable

using System.Collections.Generic;
using NUnit.Framework;

namespace Icebreaker.Window.Tests
{
    public sealed class WindowLocalSettingsTests
    {
        [Test]
        public void Load_WithNoSavedData_ReturnsBottomCenterDefaultPresetDefaults()
        {
            var settings = new WindowLocalSettings(new FakeWindowSettingsStore());

            var data = settings.Load();

            Assert.That(data.PositionMode, Is.EqualTo(WindowPositionMode.Preset));
            Assert.That(data.PositionPreset, Is.EqualTo(WindowPositionPreset.BottomCenter));
            Assert.That(data.SizePreset, Is.EqualTo(WindowSizePreset.Default));
        }

        [Test]
        public void SavePositionPreset_RoundTripsThroughStore()
        {
            var store = new FakeWindowSettingsStore();
            var settings = new WindowLocalSettings(store);

            settings.SavePositionPreset(WindowPositionPreset.BottomRight);
            var data = settings.Load();

            Assert.That(data.PositionMode, Is.EqualTo(WindowPositionMode.Preset));
            Assert.That(data.PositionPreset, Is.EqualTo(WindowPositionPreset.BottomRight));
        }

        [Test]
        public void SaveCustomPosition_SwitchesModeToCustomAndPersistsNormalizedCoordinates()
        {
            var store = new FakeWindowSettingsStore();
            var settings = new WindowLocalSettings(store);

            settings.SaveCustomPosition("1920:0:1920:1040", 0.25f, 0.9f);
            var data = settings.Load();

            Assert.That(data.PositionMode, Is.EqualTo(WindowPositionMode.Custom));
            Assert.That(data.MonitorId, Is.EqualTo("1920:0:1920:1040"));
            Assert.That(data.NormalizedX, Is.EqualTo(0.25f));
            Assert.That(data.NormalizedY, Is.EqualTo(0.9f));
        }

        [Test]
        public void ResetPosition_RestoresDefaultPresetWithoutTouchingSize()
        {
            var store = new FakeWindowSettingsStore();
            var settings = new WindowLocalSettings(store);
            settings.SaveCustomPosition("monitor-a", 0.1f, 0.2f);
            settings.SaveSizePreset(WindowSizePreset.ExtraLarge);

            settings.ResetPosition();
            var data = settings.Load();

            Assert.That(data.PositionMode, Is.EqualTo(WindowPositionMode.Preset));
            Assert.That(data.PositionPreset, Is.EqualTo(WindowPositionPreset.BottomCenter));
            Assert.That(data.SizePreset, Is.EqualTo(WindowSizePreset.ExtraLarge));
        }

        [Test]
        public void ResetSize_RestoresDefaultSizeWithoutTouchingPosition()
        {
            var store = new FakeWindowSettingsStore();
            var settings = new WindowLocalSettings(store);
            settings.SavePositionPreset(WindowPositionPreset.BottomLeft);
            settings.SaveSizePreset(WindowSizePreset.Large);

            settings.ResetSize();
            var data = settings.Load();

            Assert.That(data.SizePreset, Is.EqualTo(WindowSizePreset.Default));
            Assert.That(data.PositionPreset, Is.EqualTo(WindowPositionPreset.BottomLeft));
        }

        [Test]
        public void WindowSettings_UseKeysSeparateFromGameSaveData()
        {
            // The window local-settings keys must never collide with (or be cleared by) the
            // game-progress SaveData keys; a distinct "icebreaker.window." prefix keeps a
            // save-reset from touching window placement.
            Assert.That(WindowLocalSettings.VersionKey, Does.StartWith("icebreaker.window."));
            Assert.That(WindowLocalSettings.PositionModeKey, Does.StartWith("icebreaker.window."));
            Assert.That(WindowLocalSettings.PositionPresetKey, Does.StartWith("icebreaker.window."));
            Assert.That(WindowLocalSettings.SizePresetKey, Does.StartWith("icebreaker.window."));
            Assert.That(WindowLocalSettings.MonitorIdKey, Does.StartWith("icebreaker.window."));
            Assert.That(WindowLocalSettings.NormalizedXKey, Does.StartWith("icebreaker.window."));
            Assert.That(WindowLocalSettings.NormalizedYKey, Does.StartWith("icebreaker.window."));
        }

        private sealed class FakeWindowSettingsStore : IWindowSettingsStore
        {
            private readonly Dictionary<string, int> ints = new();
            private readonly Dictionary<string, float> floats = new();
            private readonly Dictionary<string, string> strings = new();
            private readonly HashSet<string> keys = new();

            public bool HasKey(string key) => keys.Contains(key);

            public int GetInt(string key, int defaultValue) => ints.TryGetValue(key, out var value) ? value : defaultValue;

            public void SetInt(string key, int value)
            {
                ints[key] = value;
                keys.Add(key);
            }

            public float GetFloat(string key, float defaultValue) => floats.TryGetValue(key, out var value) ? value : defaultValue;

            public void SetFloat(string key, float value)
            {
                floats[key] = value;
                keys.Add(key);
            }

            public string GetString(string key, string defaultValue) => strings.TryGetValue(key, out var value) ? value : defaultValue;

            public void SetString(string key, string value)
            {
                strings[key] = value;
                keys.Add(key);
            }

            public void DeleteKey(string key)
            {
                ints.Remove(key);
                floats.Remove(key);
                strings.Remove(key);
                keys.Remove(key);
            }

            public void Save()
            {
                // No-op: in-memory store has nothing to flush.
            }
        }
    }
}
