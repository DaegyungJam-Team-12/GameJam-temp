#nullable enable

using System;
using System.Collections.Generic;

namespace Icebreaker.Core
{
    [System.Serializable]
    public sealed class SaveData
    {
        public const int CurrentSaveVersion = 1;

        public int saveVersion;
        public string profileId = "";
        public long funds;
        public List<SaveMaintenanceLevel> maintenanceLevels = new();
        public int currentDestinationIndex;
        public int destinationProgress;
        public List<string> completedDestinationIds = new();
        public string pendingArrivalDestinationId = "";
        public bool firstDestroyShown;
        public string nextAvailableAtUtc = "";
        public bool runInProgress;
        public bool gameCompleted;
        public float masterVolume = 0f;
        public bool screenShakeEnabled = true;

        public static SaveData CreateNew(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(profileId));
            }

            return new SaveData
            {
                saveVersion = CurrentSaveVersion,
                profileId = profileId,
                funds = 0,
                maintenanceLevels = new List<SaveMaintenanceLevel>(),
                currentDestinationIndex = 0,
                destinationProgress = 0,
                completedDestinationIds = new List<string>(),
                pendingArrivalDestinationId = "",
                firstDestroyShown = false,
                nextAvailableAtUtc = "",
                runInProgress = false,
                gameCompleted = false,
                masterVolume = 0f,
                screenShakeEnabled = true
            };
        }
    }
}
