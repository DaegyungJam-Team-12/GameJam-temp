#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared;

namespace Icebreaker.Shared.Combat
{
    public sealed class IceFieldConfig
    {
        private readonly float legacyMinimumSpawnDistanceReferencePixels;

        /// <summary>
        /// Compatibility constructor for existing callers that use a fixed centre-to-centre
        /// spawn distance. New stage configs should use the visual-spacing constructor.
        /// </summary>
        public IceFieldConfig(
            int maxActiveIceCount,
            int maxSpecialIceCount,
            float hitRadiusReferencePixels,
            float minimumSpawnDistanceReferencePixels,
            float respawnProtectionSeconds,
            IReadOnlyList<IceDefinition> iceDefinitions,
            IReadOnlyList<IceSpawnWeight> spawnWeights,
            IReadOnlyList<SpecialIceDefinition> specialDefinitions)
            : this(
                maxActiveIceCount,
                maxSpecialIceCount,
                visualDiameterMinimumReferencePixels: 34f,
                visualDiameterMaximumReferencePixels: 42f,
                iceCollisionRadiusReferencePixels: hitRadiusReferencePixels,
                strictExtraVisualGapReferencePixels: 18f,
                relaxedExtraVisualGapReferencePixels: 12f,
                outerMarginReferencePixels: 20f,
                protectedAreaPaddingReferencePixels: 21f,
                recentDestructionExclusionReferencePixels: 160f,
                recentDestructionExclusionSeconds: 1f,
                respawnGapSeconds: 0.12f,
                spawnAnimationSeconds: 0.18f,
                chainRespawnStaggerSeconds: 0.03f,
                respawnProtectionSeconds: respawnProtectionSeconds,
                iceDefinitions: iceDefinitions,
                spawnWeights: spawnWeights,
                specialDefinitions: specialDefinitions,
                useVisualSpawnSpacing: false,
                legacyMinimumSpawnDistanceReferencePixels: minimumSpawnDistanceReferencePixels)
        {
        }

        public IceFieldConfig(
            int maxActiveIceCount,
            int maxSpecialIceCount,
            float visualDiameterMinimumReferencePixels,
            float visualDiameterMaximumReferencePixels,
            float iceCollisionRadiusReferencePixels,
            float strictExtraVisualGapReferencePixels,
            float relaxedExtraVisualGapReferencePixels,
            float outerMarginReferencePixels,
            float protectedAreaPaddingReferencePixels,
            float recentDestructionExclusionReferencePixels,
            float recentDestructionExclusionSeconds,
            float respawnGapSeconds,
            float spawnAnimationSeconds,
            float chainRespawnStaggerSeconds,
            float respawnProtectionSeconds,
            IReadOnlyList<IceDefinition> iceDefinitions,
            IReadOnlyList<IceSpawnWeight> spawnWeights,
            IReadOnlyList<SpecialIceDefinition> specialDefinitions)
            : this(
                maxActiveIceCount,
                maxSpecialIceCount,
                visualDiameterMinimumReferencePixels,
                visualDiameterMaximumReferencePixels,
                iceCollisionRadiusReferencePixels,
                strictExtraVisualGapReferencePixels,
                relaxedExtraVisualGapReferencePixels,
                outerMarginReferencePixels,
                protectedAreaPaddingReferencePixels,
                recentDestructionExclusionReferencePixels,
                recentDestructionExclusionSeconds,
                respawnGapSeconds,
                spawnAnimationSeconds,
                chainRespawnStaggerSeconds,
                respawnProtectionSeconds,
                iceDefinitions,
                spawnWeights,
                specialDefinitions,
                useVisualSpawnSpacing: true,
                legacyMinimumSpawnDistanceReferencePixels: 0f)
        {
        }

        private IceFieldConfig(
            int maxActiveIceCount,
            int maxSpecialIceCount,
            float visualDiameterMinimumReferencePixels,
            float visualDiameterMaximumReferencePixels,
            float iceCollisionRadiusReferencePixels,
            float strictExtraVisualGapReferencePixels,
            float relaxedExtraVisualGapReferencePixels,
            float outerMarginReferencePixels,
            float protectedAreaPaddingReferencePixels,
            float recentDestructionExclusionReferencePixels,
            float recentDestructionExclusionSeconds,
            float respawnGapSeconds,
            float spawnAnimationSeconds,
            float chainRespawnStaggerSeconds,
            float respawnProtectionSeconds,
            IReadOnlyList<IceDefinition> iceDefinitions,
            IReadOnlyList<IceSpawnWeight> spawnWeights,
            IReadOnlyList<SpecialIceDefinition> specialDefinitions,
            bool useVisualSpawnSpacing,
            float legacyMinimumSpawnDistanceReferencePixels)
        {
            MaxActiveIceCount = ContractGuards.Positive(maxActiveIceCount, nameof(maxActiveIceCount));
            MaxSpecialIceCount = ContractGuards.NonNegative(maxSpecialIceCount, nameof(maxSpecialIceCount));
            if (MaxSpecialIceCount > MaxActiveIceCount)
            {
                throw new ArgumentException(
                    "The special ice limit cannot exceed the active ice limit.",
                    nameof(maxSpecialIceCount));
            }

            VisualDiameterMinimumReferencePixels = ContractGuards.Positive(
                visualDiameterMinimumReferencePixels,
                nameof(visualDiameterMinimumReferencePixels));
            VisualDiameterMaximumReferencePixels = ContractGuards.Positive(
                visualDiameterMaximumReferencePixels,
                nameof(visualDiameterMaximumReferencePixels));
            if (VisualDiameterMaximumReferencePixels < VisualDiameterMinimumReferencePixels)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(visualDiameterMaximumReferencePixels),
                    "The visual diameter maximum cannot be smaller than the minimum.");
            }

            IceCollisionRadiusReferencePixels = ContractGuards.Positive(
                iceCollisionRadiusReferencePixels,
                nameof(iceCollisionRadiusReferencePixels));
            StrictExtraVisualGapReferencePixels = ContractGuards.NonNegative(
                strictExtraVisualGapReferencePixels,
                nameof(strictExtraVisualGapReferencePixels));
            RelaxedExtraVisualGapReferencePixels = ContractGuards.NonNegative(
                relaxedExtraVisualGapReferencePixels,
                nameof(relaxedExtraVisualGapReferencePixels));
            if (RelaxedExtraVisualGapReferencePixels > StrictExtraVisualGapReferencePixels)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(relaxedExtraVisualGapReferencePixels),
                    "The relaxed visual gap cannot exceed the strict visual gap.");
            }

            OuterMarginReferencePixels = ContractGuards.NonNegative(
                outerMarginReferencePixels,
                nameof(outerMarginReferencePixels));
            ProtectedAreaPaddingReferencePixels = ContractGuards.NonNegative(
                protectedAreaPaddingReferencePixels,
                nameof(protectedAreaPaddingReferencePixels));
            RecentDestructionExclusionReferencePixels = ContractGuards.Positive(
                recentDestructionExclusionReferencePixels,
                nameof(recentDestructionExclusionReferencePixels));
            RecentDestructionExclusionSeconds = ContractGuards.NonNegative(
                recentDestructionExclusionSeconds,
                nameof(recentDestructionExclusionSeconds));
            RespawnGapSeconds = ContractGuards.NonNegative(
                respawnGapSeconds,
                nameof(respawnGapSeconds));
            SpawnAnimationSeconds = ContractGuards.NonNegative(
                spawnAnimationSeconds,
                nameof(spawnAnimationSeconds));
            ChainRespawnStaggerSeconds = ContractGuards.NonNegative(
                chainRespawnStaggerSeconds,
                nameof(chainRespawnStaggerSeconds));
            RespawnProtectionSeconds = ContractGuards.NonNegative(
                respawnProtectionSeconds,
                nameof(respawnProtectionSeconds));
            UsesVisualSpawnSpacing = useVisualSpawnSpacing;
            this.legacyMinimumSpawnDistanceReferencePixels = useVisualSpawnSpacing
                ? 0f
                : ContractGuards.Positive(
                    legacyMinimumSpawnDistanceReferencePixels,
                    nameof(legacyMinimumSpawnDistanceReferencePixels));
            IceDefinitions = ContractGuards.Copy(iceDefinitions, nameof(iceDefinitions));
            SpawnWeights = ContractGuards.Copy(spawnWeights, nameof(spawnWeights));
            SpecialDefinitions = ContractGuards.Copy(specialDefinitions, nameof(specialDefinitions));

            ValidateDefinitions();
        }

        public int MaxActiveIceCount { get; }

        public int MaxSpecialIceCount { get; }

        /// <summary>Ice collision radius in the 960 x 540 bottom-left-origin reference space.</summary>
        public float IceCollisionRadiusReferencePixels { get; }

        /// <summary>Legacy alias for the ice collision radius.</summary>
        public float HitRadiusReferencePixels => IceCollisionRadiusReferencePixels;

        public float VisualDiameterMinimumReferencePixels { get; }

        public float VisualDiameterMaximumReferencePixels { get; }

        public float StrictExtraVisualGapReferencePixels { get; }

        public float RelaxedExtraVisualGapReferencePixels { get; }

        public float OuterMarginReferencePixels { get; }

        public float ProtectedAreaPaddingReferencePixels { get; }

        public float RecentDestructionExclusionReferencePixels { get; }

        public float RecentDestructionExclusionSeconds { get; }

        public float RespawnGapSeconds { get; }

        public float SpawnAnimationSeconds { get; }

        public float ChainRespawnStaggerSeconds { get; }

        /// <summary>True when spacing uses each ice visual radius plus the configured gap.</summary>
        public bool UsesVisualSpawnSpacing { get; }

        /// <summary>
        /// Legacy centre-distance view retained for existing callers. Visual-spacing configs
        /// expose the strict maximum-diameter distance instead.
        /// </summary>
        public float MinimumSpawnDistanceReferencePixels => UsesVisualSpawnSpacing
            ? VisualDiameterMaximumReferencePixels + StrictExtraVisualGapReferencePixels
            : legacyMinimumSpawnDistanceReferencePixels;

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
