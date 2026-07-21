#nullable enable

using System;
using System.IO;
using UnityEngine;

namespace Icebreaker.Core
{
    public sealed class SaveStore
    {
        private readonly string directoryPath;

        public SaveStore(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(directoryPath));
            }

            this.directoryPath = directoryPath;
        }

        public string PathFor(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(profileId));
            }

            return Path.Combine(directoryPath, $"save_{profileId}.json");
        }

        public void Save(SaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (string.IsNullOrEmpty(data.profileId))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(data));
            }

            Directory.CreateDirectory(directoryPath);

            var path = PathFor(data.profileId);
            var json = JsonUtility.ToJson(data, true);
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }

        public SaveData? TryLoad(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(profileId));
            }

            var path = PathFor(profileId);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null || string.IsNullOrEmpty(data.profileId))
                {
                    throw new InvalidDataException("Malformed save.");
                }

                return data;
            }
            catch (Exception)
            {
                var backup = path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                try
                {
                    File.Move(path, backup);
                }
                catch
                {
                    // Ignore backup failures and start fresh.
                }

                return null;
            }
        }
    }
}
