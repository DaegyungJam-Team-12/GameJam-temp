#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared;

namespace Icebreaker.Shared.Combat
{
    public sealed class IceFieldConfig
    {
        public IceFieldConfig(
            int maxActiveIceCount,
            int maxSpecialIceCount,
            float hitRadiusReferencePixels,
            float minimumSpawnDistanceReferencePixels,
            float respawnProtectionSeconds,
            IReadOnlyList<IceDefinition> iceDefinitions,
            IReadOnlyList<IceSpawnWeight> spawnWeights,
            IReadOnlyList<SpecialIceDefinition> specialDefinitions)
        {
            MaxActiveIceCount = ContractGuards.Positive(maxActiveIceCount, nameof(maxActiveIceCount));
            MaxSpecialIceCount = ContractGuards.NonNegative(maxSpecialIceCount, nameof(maxSpecialIceCount));
            if (MaxSpecialIceCount > MaxActiveIceCount)
            {
                throw new ArgumentException(
                    "The special ice limit cannot exceed the active ice limit.",
                    nameof(maxSpecialIceCount));
            }

            HitRadiusReferencePixels = ContractGuards.Positive(
                hitRadiusReferencePixels,
                nameof(hitRadiusReferencePixels));
            MinimumSpawnDistanceReferencePixels = ContractGuards.Positive(
                minimumSpawnDistanceReferencePixels,
                nameof(minimumSpawnDistanceReferencePixels));
            RespawnProtectionSeconds = ContractGuards.NonNegative(
                respawnProtectionSeconds,
                nameof(respawnProtectionSeconds));
            IceDefinitions = ContractGuards.Copy(iceDefinitions, nameof(iceDefinitions));
            SpawnWeights = ContractGuards.Copy(spawnWeights, nameof(spawnWeights));
            SpecialDefinitions = ContractGuards.Copy(specialDefinitions, nameof(specialDefinitions));

            ValidateDefinitions();
        }

        public int MaxActiveIceCount { get; }

        public int MaxSpecialIceCount { get; }

        /// <summary>Radius in the 960 x 540 bottom-left-origin reference space.</summary>
        public float HitRadiusReferencePixels { get; }

        /// <summary>Distance in the 960 x 540 bottom-left-origin reference space.</summary>
        public float MinimumSpawnDistanceReferencePixels { get; }

        public float RespawnProtectionSeconds { get; }

        public IReadOnlyList<IceDefinition> IceDefinitions { get; }

        public IReadOnlyList<IceSpawnWeight> SpawnWeights { get; }

        public IReadOnlyList<SpecialIceDefinition> SpecialDefinitions { get; }

        private void ValidateDefinitions()
        {
            if (IceDefinitions.Count == 0)
            {
                throw new ArgumentException("At least one ice definition is required.", nameof(IceDefinitions));
            }

            var tiers = new HashSet<IceTier>();
            foreach (var definition in IceDefinitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException("Ice definitions cannot contain null.", nameof(IceDefinitions));
                }

                if (!tiers.Add(definition.Tier))
                {
                    throw new ArgumentException("Ice definition tiers must be unique.", nameof(IceDefinitions));
                }
            }

            if (SpawnWeights.Count == 0)
            {
                throw new ArgumentException("At least one spawn weight is required.", nameof(SpawnWeights));
            }

            var weightedTiers = new HashSet<IceTier>();
            long totalWeight = 0;
            foreach (var weight in SpawnWeights)
            {
                if (!weightedTiers.Add(weight.Tier))
                {
                    throw new ArgumentException("Spawn weight tiers must be unique.", nameof(SpawnWeights));
                }

                if (!tiers.Contains(weight.Tier))
                {
                    throw new ArgumentException("Each spawn weight needs a matching ice definition.", nameof(SpawnWeights));
                }

                totalWeight += weight.Weight;
            }

            if (totalWeight <= 0)
            {
                throw new ArgumentException("The spawn weight total must be greater than zero.", nameof(SpawnWeights));
            }

            var specialTypes = new HashSet<SpecialIceType>();
            foreach (var definition in SpecialDefinitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException("Special ice definitions cannot contain null.", nameof(SpecialDefinitions));
                }

                if (!specialTypes.Add(definition.Type))
                {
                    throw new ArgumentException(
                        "Special ice definition types must be unique.",
                        nameof(SpecialDefinitions));
                }
            }
        }
    }
}
