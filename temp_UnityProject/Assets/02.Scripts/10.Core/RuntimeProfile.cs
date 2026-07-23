#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Icebreaker.Core
{
    public sealed class RuntimeProfile
    {
        public const string StandardProfileId = "standard";
        public const string DemoProfileId = "demo";

        private const string StandardResourcePath = "RuntimeProfiles/standard";
        private const string DemoResourcePath = "RuntimeProfiles/demo";

        private RuntimeProfile(
            string profileId,
            double stageDurationSeconds,
            double countdownSeconds,
            double voyageSeconds,
            int[] destinationTargets,
            string maintenanceCatalogId)
        {
            ProfileId = profileId;
            StageDurationSeconds = stageDurationSeconds;
            CountdownSeconds = countdownSeconds;
            VoyageSeconds = voyageSeconds;
            DestinationTargets = Array.AsReadOnly(destinationTargets);
            MaintenanceCatalogId = maintenanceCatalogId;
        }

        public string ProfileId { get; }

        public double StageDurationSeconds { get; }

        public double CountdownSeconds { get; }

        public double VoyageSeconds { get; }

        public IReadOnlyList<int> DestinationTargets { get; }

        public string MaintenanceCatalogId { get; }

        public static string ActiveProfileId
        {
            get
            {
#if ICEBREAKER_DEMO
                return DemoProfileId;
#else
                return StandardProfileId;
#endif
            }
        }

        public static RuntimeProfile LoadActive() => LoadFromResources(ActiveProfileId);

        public static RuntimeProfile LoadFromResources(string profileId)
        {
            var resourcePath = ResourcePathFor(profileId);
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Runtime profile '{profileId}' is missing at Resources/{resourcePath}.json.");
            }

            var profile = Parse(asset.text, $"Resources/{resourcePath}.json");
            if (!string.Equals(profile.ProfileId, profileId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Runtime profile at Resources/{resourcePath}.json has ID '{profile.ProfileId}', expected '{profileId}'.");
            }

            return profile;
        }

        public static string ResourcePathFor(string profileId)
        {
            return profileId switch
            {
                StandardProfileId => StandardResourcePath,
                DemoProfileId => DemoResourcePath,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(profileId),
                    profileId,
                    "Runtime profile ID must be 'standard' or 'demo'.")
            };
        }

        public static RuntimeProfile Parse(string json, string sourceName = "runtime profile JSON")
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException($"Runtime profile JSON is empty: {sourceName}.");
            }

            RuntimeProfileData? data;
            try
            {
                data = JsonUtility.FromJson<RuntimeProfileData>(json);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Runtime profile JSON is invalid: {sourceName}.",
                    exception);
            }

            if (data == null)
            {
                throw new InvalidOperationException($"Runtime profile JSON has no data: {sourceName}.");
            }

            if (!string.Equals(data.profileId, StandardProfileId, StringComparison.Ordinal) &&
                !string.Equals(data.profileId, DemoProfileId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Runtime profile ID must be 'standard' or 'demo': {sourceName}.");
            }

            if (data.stageDurationSeconds <= 0d || data.countdownSeconds <= 0d ||
                data.voyageSeconds <= 0d)
            {
                throw new InvalidOperationException(
                    $"Runtime profile times must be positive: {sourceName}.");
            }

            if (data.destinationTargets == null || data.destinationTargets.Length != 3)
            {
                throw new InvalidOperationException(
                    $"Runtime profile must contain exactly three destination targets: {sourceName}.");
            }

            for (var index = 0; index < data.destinationTargets.Length; index++)
            {
                if (data.destinationTargets[index] <= 0)
                {
                    throw new InvalidOperationException(
                        $"Runtime profile destination targets must be positive: {sourceName}.");
                }
            }

            if (!string.Equals(data.maintenanceCatalogId, StandardProfileId, StringComparison.Ordinal) &&
                !string.Equals(data.maintenanceCatalogId, DemoProfileId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Runtime profile maintenance catalog ID must be 'standard' or 'demo': {sourceName}.");
            }

            return new RuntimeProfile(
                data.profileId,
                data.stageDurationSeconds,
                data.countdownSeconds,
                data.voyageSeconds,
                (int[])data.destinationTargets.Clone(),
                data.maintenanceCatalogId);
        }

        [Serializable]
        private sealed class RuntimeProfileData
        {
            public string profileId = "";
            public double stageDurationSeconds;
            public double countdownSeconds;
            public double voyageSeconds;
            public int[]? destinationTargets;
            public string maintenanceCatalogId = "";
        }
    }
}
