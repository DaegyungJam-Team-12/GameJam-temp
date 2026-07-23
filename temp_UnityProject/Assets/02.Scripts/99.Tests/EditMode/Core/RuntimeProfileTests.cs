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
                30d,
                3d,
                10d,
                "120,600,2400",
                "standard"));

            Assert.That(profile.ProfileId, Is.EqualTo(RuntimeProfile.DefaultProfileId));
            Assert.That(profile.StageDurationSeconds, Is.EqualTo(30d));
            Assert.That(profile.CountdownSeconds, Is.EqualTo(3d));
            Assert.That(profile.VoyageSeconds, Is.EqualTo(10d));
            Assert.That(profile.DestinationTargets, Is.EqualTo(new[] { 120, 600, 2_400 }));
            Assert.That(profile.MaintenanceCatalogId, Is.EqualTo("standard"));
        }

        [TestCase("preview")]
        [TestCase("")]
        public void Parse_RejectsUnsupportedProfileIds(string profileId)
        {
            Assert.Throws<InvalidOperationException>(() => RuntimeProfile.Parse(CreateJson(
                profileId,
                30d,
                3d,
                10d,
                "120,600,2400",
                "standard")));
        }

        [TestCase(0d, 3d, 10d)]
        [TestCase(30d, 0d, 10d)]
        [TestCase(30d, 3d, 0d)]
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
                30d,
                3d,
                10d,
                targets,
                "standard")));
        }

        [TestCase("preview")]
        [TestCase("")]
        public void Parse_RejectsUnsupportedMaintenanceCatalogIds(string maintenanceCatalogId)
        {
            Assert.Throws<InvalidOperationException>(() => RuntimeProfile.Parse(CreateJson(
                "standard",
                30d,
                3d,
                10d,
                "120,600,2400",
                maintenanceCatalogId)));
        }

        [Test]
        public void Parse_RejectsMalformedJson()
        {
            Assert.Throws<InvalidOperationException>(() => RuntimeProfile.Parse("{"));
        }

        [Test]
        public void ResourcePath_IsTheSingleStandardProfile()
        {
            Assert.That(
                RuntimeProfile.ResourcePathFor(RuntimeProfile.DefaultProfileId),
                Is.EqualTo("RuntimeProfiles/standard"));
        }

        [Test]
        public void LoadActive_UsesSingleProfileValues()
        {
            var profile = RuntimeProfile.LoadActive();

            Assert.That(profile.ProfileId, Is.EqualTo(RuntimeProfile.DefaultProfileId));
            Assert.That(profile.StageDurationSeconds, Is.EqualTo(30d));
            Assert.That(profile.VoyageSeconds, Is.EqualTo(10d));
            Assert.That(profile.DestinationTargets, Is.EqualTo(new[] { 120, 600, 2_400 }));
            Assert.That(profile.MaintenanceCatalogId, Is.EqualTo(RuntimeProfile.DefaultProfileId));
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
