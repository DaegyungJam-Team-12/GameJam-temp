#nullable enable

using System;
using System.Globalization;
using System.IO;
using Icebreaker.Core;
using Icebreaker.Shared.State;
using NUnit.Framework;

namespace Icebreaker.Core.Tests
{
    public sealed class SavePersistenceTests
    {
        private string tempDir = null!;

        [SetUp]
        public void SetUp()
        {
            tempDir = Path.Combine(
                Path.GetTempPath(),
                "icebreaker-save-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Test]
        public void SaveData_RoundTripsThroughStore()
        {
            var store = new SaveStore(tempDir);
            var data = SaveData.CreateNew("standard");
            data.funds = 1234;
            data.maintenanceLevels.Add(new SaveMaintenanceLevel("hull", 2));
            data.currentDestinationIndex = 1;
            data.destinationProgress = 37;
            data.completedDestinationIds.Add("island-village");
            data.firstDestroyShown = true;
            data.gameCompleted = false;

            store.Save(data);
            var loaded = store.TryLoad("standard");

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.profileId, Is.EqualTo("standard"));
            Assert.That(loaded.funds, Is.EqualTo(1234));
            Assert.That(loaded.maintenanceLevels, Has.Count.EqualTo(1));
            Assert.That(loaded.maintenanceLevels[0].id, Is.EqualTo("hull"));
            Assert.That(loaded.maintenanceLevels[0].level, Is.EqualTo(2));
            Assert.That(loaded.currentDestinationIndex, Is.EqualTo(1));
            Assert.That(loaded.destinationProgress, Is.EqualTo(37));
            Assert.That(loaded.completedDestinationIds, Is.EqualTo(new[] { "island-village" }));
            Assert.That(loaded.firstDestroyShown, Is.True);
            Assert.That(loaded.gameCompleted, Is.False);
        }

        [Test]
        public void Save_IsAtomic_NoTempLeftBehind()
        {
            var store = new SaveStore(tempDir);
            var path = store.PathFor("standard");

            store.Save(SaveData.CreateNew("standard"));

            Assert.That(File.Exists(path), Is.True);
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void Save_OverwritesExisting()
        {
            var store = new SaveStore(tempDir);
            var data = SaveData.CreateNew("standard");
            data.funds = 100;
            store.Save(data);

            data.funds = 200;
            store.Save(data);

            var loaded = store.TryLoad("standard");
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.funds, Is.EqualTo(200));
        }

        [Test]
        public void TryLoad_MissingFile_ReturnsNull()
        {
            var store = new SaveStore(tempDir);

            var loaded = store.TryLoad("standard");

            Assert.That(loaded, Is.Null);
        }

        [Test]
        public void TryLoad_CorruptFile_PreservesBackup_AndReturnsNull()
        {
            var store = new SaveStore(tempDir);
            var path = store.PathFor("standard");
            Directory.CreateDirectory(tempDir);
            File.WriteAllBytes(path, new byte[] { 0x00, 0xff, 0x13, 0x37 });

            var loaded = store.TryLoad("standard");

            Assert.That(loaded, Is.Null);
            Assert.That(File.Exists(path), Is.False);
            Assert.That(
                Directory.GetFiles(tempDir, "save_standard.json.corrupt-*"),
                Has.Length.EqualTo(1));
        }

        [Test]
        public void Profiles_AreSeparateFiles()
        {
            var store = new SaveStore(tempDir);
            store.Save(SaveData.CreateNew("standard"));
            store.Save(SaveData.CreateNew("demo"));

            Assert.That(File.Exists(Path.Combine(tempDir, "save_standard.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(tempDir, "save_demo.json")), Is.True);
            Assert.That(store.TryLoad("standard")!.profileId, Is.EqualTo("standard"));
            Assert.That(store.TryLoad("demo")!.profileId, Is.EqualTo("demo"));
        }

        [Test]
        public void SaveService_DebouncesThenFlushes()
        {
            var store = new SaveStore(tempDir);
            var service = new SaveService(store, SaveData.CreateNew("standard"));
            var path = store.PathFor("standard");

            service.MarkDirty();
            service.Tick(0.5d);

            Assert.That(service.HasPendingWrite, Is.True);
            Assert.That(File.Exists(path), Is.False);

            service.Tick(0.6d);

            Assert.That(service.HasPendingWrite, Is.False);
            Assert.That(File.Exists(path), Is.True);
        }

        [Test]
        public void SaveService_Flush_ForcesImmediateWrite()
        {
            var store = new SaveStore(tempDir);
            var service = new SaveService(store, SaveData.CreateNew("standard"));
            var path = store.PathFor("standard");

            service.MarkDirty();
            service.Flush();

            Assert.That(File.Exists(path), Is.True);
            Assert.That(service.HasPendingWrite, Is.False);
        }

        [Test]
        public void BootResolver_NoSave_IsReady()
        {
            var state = SaveBootResolver.Resolve(null, DateTimeOffset.UtcNow, 30d);

            Assert.That(state.Phase, Is.EqualTo(GamePhase.Ready));
            Assert.That(state.VoyageRemainingSeconds, Is.Zero);
        }

        [Test]
        public void BootResolver_RunInProgress_RestartsFullVoyage()
        {
            var data = SaveData.CreateNew("standard");
            data.runInProgress = true;

            var state = SaveBootResolver.Resolve(data, DateTimeOffset.UtcNow, 30d);

            Assert.That(state.Phase, Is.EqualTo(GamePhase.Traveling));
            Assert.That(state.VoyageRemainingSeconds, Is.EqualTo(30d));
        }

        [Test]
        public void BootResolver_GameCompleted_IsCompleted()
        {
            var data = SaveData.CreateNew("standard");
            data.gameCompleted = true;

            var state = SaveBootResolver.Resolve(data, DateTimeOffset.UtcNow, 30d);

            Assert.That(state.Phase, Is.EqualTo(GamePhase.Completed));
            Assert.That(state.VoyageRemainingSeconds, Is.Zero);
        }

        [Test]
        public void BootResolver_VoyageRemaining_ClampsAndReadies()
        {
            var now = DateTimeOffset.UtcNow;
            var data = SaveData.CreateNew("standard");
            data.nextAvailableAtUtc = (now + TimeSpan.FromSeconds(10d)).ToString(
                "O",
                CultureInfo.InvariantCulture);

            var active = SaveBootResolver.Resolve(data, now, 30d);

            Assert.That(active.Phase, Is.EqualTo(GamePhase.Traveling));
            Assert.That(active.VoyageRemainingSeconds, Is.GreaterThan(0d).And.LessThanOrEqualTo(30d));

            data.nextAvailableAtUtc = (now - TimeSpan.FromSeconds(1d)).ToString(
                "O",
                CultureInfo.InvariantCulture);

            var ready = SaveBootResolver.Resolve(data, now, 30d);

            Assert.That(ready.Phase, Is.EqualTo(GamePhase.Ready));
            Assert.That(ready.VoyageRemainingSeconds, Is.Zero);

            data.nextAvailableAtUtc = (now + TimeSpan.FromSeconds(1000d)).ToString(
                "O",
                CultureInfo.InvariantCulture);

            var clamped = SaveBootResolver.Resolve(data, now, 30d);

            Assert.That(clamped.Phase, Is.EqualTo(GamePhase.Traveling));
            Assert.That(clamped.VoyageRemainingSeconds, Is.EqualTo(30d));
        }
    }
}
