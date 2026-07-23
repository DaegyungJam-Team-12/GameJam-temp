#nullable enable

using System;
using NUnit.Framework;

namespace Icebreaker.Core.Tests
{
    public sealed class RuntimeProfileTests
    {
        [Test]
        public void Parse_ValidStandardProfile_ExposesRuntimeValues()
        {
            var profile = RuntimeProfile.Parse(CreateJson(
                "standard",
                60d,
                3d,
                30d,
                "120,600,2400",
                "standard"));

            Assert.That(profile.ProfileId, Is.EqualTo(RuntimeProfile.StandardProfileId));
            Assert.That(profile.StageDurationSeconds, Is.EqualTo(60d));
            Assert.That(profile.CountdownSeconds, Is.EqualTo(3d));
            Assert.That(profile.VoyageSeconds, Is.EqualTo(30d));
            Assert.That(profile.DestinationTargets, Is.EqualTo(new[] { 120, 600, 2_400 }));
            Assert.That(profile.MaintenanceCatalogId, Is.EqualTo("standard"));
        }

        [TestCase("preview")]
        [TestCase("")]
        public void Parse_RejectsUnsupportedProfileIds(string profileId)
        {
            Assert.Throws<InvalidOperationException>(() => RuntimeProfile.Parse(CreateJson(
                profileId,
                60d,
                3d,
                30d,
                "120,600,2400",
                "standard")));
        }

        [TestCase(0d, 3d, 30d)]
        [TestCase(60d, 0d, 30d)]
        [TestCase(60d, 3d, 0d)]
        public void Parse_RejectsNonPositiveTimes(
            double stageDurationSeconds,
            double countdownSeconds,
            double voyageSeconds)
        {
            Assert.Throws<InvalidOperationException>(() => RuntimeProfile.Parse(CreateJson(
                "standard",
                stageDurationSeconds,
                countdownSeconds,
                voyageSeconds,
                "120,600,2400",
                "standard")));
        }

        [TestCase("120,600")]
        [TestCase("120,600,2400,4800")]
        [TestCase("120,0,2400")]
        public void Parse_RejectsInvalidDestinationTargets(string targets)
        {
            Assert.Throws<InvalidOperationException>(() => RuntimeProfile.Parse(CreateJson(
                "standard",
                60d,
                3d,
                30d,
                targets,
                "standard")));
        }

        [TestCase("preview")]
        [TestCase("")]
        public void Parse_RejectsUnsupportedMaintenanceCatalogIds(string maintenanceCatalogId)
        {
            Assert.Throws<InvalidOperationException>(() => RuntimeProfile.Parse(CreateJson(
                "standard",
                60d,
                3d,
                30d,
                "120,600,2400",
                maintenanceCatalogId)));
        }

        [Test]
        public void Parse_RejectsMalformedJson()
        {
            Assert.Throws<InvalidOperationException>(() => RuntimeProfile.Parse("{"));
        }

        [Test]
        public void ResourcePaths_AreDistinctForStandardAndDemo()
        {
            Assert.That(
                RuntimeProfile.ResourcePathFor(RuntimeProfile.StandardProfileId),
                Is.EqualTo("RuntimeProfiles/standard"));
            Assert.That(
                RuntimeProfile.ResourcePathFor(RuntimeProfile.DemoProfileId),
                Is.EqualTo("RuntimeProfiles/demo"));
        }

        [Test]
        public void LoadActive_UsesCurrentBuildProfile()
        {
            var profile = RuntimeProfile.LoadActive();

#if ICEBREAKER_DEMO
            Assert.That(profile.ProfileId, Is.EqualTo(RuntimeProfile.DemoProfileId));
            Assert.That(profile.VoyageSeconds, Is.EqualTo(10d));
            Assert.That(profile.DestinationTargets, Is.EqualTo(new[] { 40, 120, 300 }));
            Assert.That(profile.MaintenanceCatalogId, Is.EqualTo(RuntimeProfile.DemoProfileId));
#else
            Assert.That(profile.ProfileId, Is.EqualTo(RuntimeProfile.StandardProfileId));
            Assert.That(profile.VoyageSeconds, Is.EqualTo(30d));
            Assert.That(profile.DestinationTargets, Is.EqualTo(new[] { 120, 600, 2_400 }));
            Assert.That(profile.MaintenanceCatalogId, Is.EqualTo(RuntimeProfile.StandardProfileId));
#endif
        }

        [Test]
        public void LoadFromResources_LoadsDemoProfileValues()
        {
            var profile = RuntimeProfile.LoadFromResources(RuntimeProfile.DemoProfileId);

            Assert.That(profile.ProfileId, Is.EqualTo(RuntimeProfile.DemoProfileId));
            Assert.That(profile.StageDurationSeconds, Is.EqualTo(60d));
            Assert.That(profile.CountdownSeconds, Is.EqualTo(3d));
            Assert.That(profile.VoyageSeconds, Is.EqualTo(10d));
            Assert.That(profile.DestinationTargets, Is.EqualTo(new[] { 40, 120, 300 }));
            Assert.That(profile.MaintenanceCatalogId, Is.EqualTo(RuntimeProfile.DemoProfileId));
        }

        private static string CreateJson(
            string profileId,
            double stageDurationSeconds,
            double countdownSeconds,
            double voyageSeconds,
            string targets,
            string maintenanceCatalogId)
        {
            return $@"{{
  ""profileId"": ""{profileId}"",
  ""stageDurationSeconds"": {stageDurationSeconds},
  ""countdownSeconds"": {countdownSeconds},
  ""voyageSeconds"": {voyageSeconds},
  ""destinationTargets"": [{targets}],
  ""maintenanceCatalogId"": ""{maintenanceCatalogId}""
}}";
        }
    }
}
