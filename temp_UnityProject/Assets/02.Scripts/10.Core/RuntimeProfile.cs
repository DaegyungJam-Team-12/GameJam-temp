#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Icebreaker.Core
{
    public sealed class RuntimeProfile
    {
        public const string DefaultProfileId = "standard";

        private const string ResourcePath = "RuntimeProfiles/standard";

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

        public static string ActiveProfileId => DefaultProfileId;

        public static RuntimeProfile LoadActive() => LoadFromResources(ActiveProfileId);

        public static RuntimeProfile LoadFromResources(string profileId)
        {
            if (!string.Equals(profileId, DefaultProfileId, StringComparison.Ordinal))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(profileId),
                    profileId,
                    "Runtime profile ID must be 'standard'.");
            }

            var resourcePath = ResourcePath;
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
            if (!string.Equals(profileId, DefaultProfileId, StringComparison.Ordinal))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(profileId),
                    profileId,
                    "Runtime profile ID must be 'standard'.");
            }

            return ResourcePath;
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

            if (!string.Equals(data.profileId, DefaultProfileId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Runtime profile ID must be 'standard': {sourceName}.");
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

            if (!string.Equals(data.maintenanceCatalogId, DefaultProfileId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Runtime profile maintenance catalog ID must be 'standard': {sourceName}.");
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
