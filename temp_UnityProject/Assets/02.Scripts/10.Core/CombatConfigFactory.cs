#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Combat;
using Icebreaker.Shared.State;

namespace Icebreaker.Core
{
    public static class CombatConfigFactory
    {
        public static CombatConfig Build(IReadOnlyList<MaintenanceLevel> levels)
        {
            var levelsById = CreateLevelMap(levels);
            var c03Level = GetLevel(levelsById, MaintenanceCatalog.C03, 1);
            var c04Level = GetLevel(levelsById, MaintenanceCatalog.C04, 1);
            var d01Level = GetLevel(levelsById, MaintenanceCatalog.D01, 3);
            var d02Level = GetLevel(levelsById, MaintenanceCatalog.D02, 3);
            var d03Level = GetLevel(levelsById, MaintenanceCatalog.D03, 1);
            var d04Level = GetLevel(levelsById, MaintenanceCatalog.D04, 3);
            var s01Level = GetLevel(levelsById, MaintenanceCatalog.S01, 1);
            var s02Level = GetLevel(levelsById, MaintenanceCatalog.S02, 2);
            var s03Level = GetLevel(levelsById, MaintenanceCatalog.S03, 1);
            var h01Level = GetLevel(levelsById, MaintenanceCatalog.H01, 3);
            var h02Level = GetLevel(levelsById, MaintenanceCatalog.H02, 2);
            var h03Level = GetLevel(levelsById, MaintenanceCatalog.H03, 1);
            var specialEffectScale = 1d + 0.3d * h02Level;

            return new CombatConfig(
                directAttack: new DirectAttackConfig(
                    d01Level == 0 ? 1f : 1f + 2f * d01Level,
                    5f + 1.25f * d02Level,
                    56f + 16f * d04Level,
                    0.05f,
                    3f),
                iceField: CreateIceField(c03Level, c04Level),
                supportAttack: new SupportAttackConfig(
                    s01Level >= 1,
                    12,
                    1f,
                    s02Level,
                    0.7f,
                    s03Level >= 1,
                    2f),
                chainEffect: new ChainEffectConfig(
                    overkillEnabled: d03Level >= 1,
                    overkillTransferMultiplier: 0.5f,
                    hullFragmentDamageMultiplier: 0.25f * h01Level,
                    hullFragmentRadiusReferencePixels: 90f,
                    crystalShardCount: (int)Math.Round(
                        5d * specialEffectScale,
                        MidpointRounding.AwayFromZero),
                    crackDamageMultiplier: 3f * (float)specialEffectScale,
                    crackRadiusReferencePixels: 120f * (float)specialEffectScale,
                    iceCollapseEnabled: h03Level >= 1,
                    iceCollapseRequiredDestroyCount: 5,
                    iceCollapseDamageMultiplier: 1.5f,
                    iceCollapseRadiusReferencePixels: 140f,
                    maxChainDepth: 3));
        }

        public static int GetMaintenanceEfficiencyLevel(IReadOnlyList<MaintenanceLevel> levels)
        {
            return GetLevel(CreateLevelMap(levels), MaintenanceCatalog.C02, 3);
        }

        private static IceFieldConfig CreateIceField(int c03Level, int c04Level)
        {
            IceDefinition[] definitions;
            IceSpawnWeight[] weights;
            if (c04Level >= 1)
            {
                definitions = new[]
                {
                    CreateT1Definition(),
                    CreateT2Definition(),
                    CreateT3Definition()
                };
                weights = new[]
                {
                    new IceSpawnWeight(IceTier.T1, 20),
                    new IceSpawnWeight(IceTier.T2, 45),
                    new IceSpawnWeight(IceTier.T3, 35)
                };
            }
            else if (c03Level >= 1)
            {
                definitions = new[]
                {
                    CreateT1Definition(),
                    CreateT2Definition()
                };
                weights = new[]
                {
                    new IceSpawnWeight(IceTier.T1, 60),
                    new IceSpawnWeight(IceTier.T2, 40)
                };
            }
            else
            {
                definitions = new[] { CreateT1Definition() };
                weights = new[] { new IceSpawnWeight(IceTier.T1, 100) };
            }

            return new IceFieldConfig(
                20,
                2,
                56f,
                120f,
                0.25f,
                definitions,
                weights,
                new[]
                {
                    new SpecialIceDefinition(SpecialIceType.Crystal, 0.025f, IceTier.T2, 1f, 4f),
                    new SpecialIceDefinition(SpecialIceType.Crack, 0.02f, IceTier.T1, 0.6f, 1f)
                });
        }

        private static IceDefinition CreateT1Definition()
        {
            return new IceDefinition(IceTier.T1, "백빙", 10f, 10);
        }

        private static IceDefinition CreateT2Definition()
        {
            return new IceDefinition(IceTier.T2, "청빙", 60f, 80);
        }

        private static IceDefinition CreateT3Definition()
        {
            return new IceDefinition(IceTier.T3, "심빙", 360f, 700);
        }

        private static Dictionary<string, int> CreateLevelMap(IReadOnlyList<MaintenanceLevel> levels)
        {
            if (levels == null)
            {
                throw new ArgumentNullException(nameof(levels));
            }

            var levelsById = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var level in levels)
            {
                if (!levelsById.TryAdd(level.Id, level.Level))
                {
                    throw new ArgumentException("Maintenance level IDs must be unique.", nameof(levels));
                }
            }

            return levelsById;
        }

        private static int GetLevel(
            IReadOnlyDictionary<string, int> levelsById,
            string id,
            int maxLevel)
        {
            if (!levelsById.TryGetValue(id, out var level))
            {
                return 0;
            }

            if (level > maxLevel)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(levelsById),
                    level,
                    $"{id} level cannot exceed {maxLevel}.");
            }

            return level;
        }
    }
}
