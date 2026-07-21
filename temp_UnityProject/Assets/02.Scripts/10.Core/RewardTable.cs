#nullable enable

using System;
using Icebreaker.Shared.Combat;

namespace Icebreaker.Core
{
    public sealed class RewardTable
    {
        private readonly long t1Funds;
        private readonly long t2Funds;
        private readonly long t3Funds;
        private readonly double crystalMultiplier;

        public RewardTable(long t1Funds, long t2Funds, long t3Funds, double crystalMultiplier)
        {
            if (t1Funds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(t1Funds), t1Funds, "Value cannot be negative.");
            }

            if (t2Funds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(t2Funds), t2Funds, "Value cannot be negative.");
            }

            if (t3Funds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(t3Funds), t3Funds, "Value cannot be negative.");
            }

            if (!(crystalMultiplier >= 1.0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(crystalMultiplier),
                    crystalMultiplier,
                    "Value must be at least one.");
            }

            this.t1Funds = t1Funds;
            this.t2Funds = t2Funds;
            this.t3Funds = t3Funds;
            this.crystalMultiplier = crystalMultiplier;
        }

        public static RewardTable CreateDefault()
        {
            return new RewardTable(10, 80, 700, 4.0);
        }

        public long ComputeFunds(
            IceTier tier,
            SpecialIceType specialType,
            int maintenanceEfficiencyLevel)
        {
            if (maintenanceEfficiencyLevel < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maintenanceEfficiencyLevel),
                    maintenanceEfficiencyLevel,
                    "Value cannot be negative.");
            }

            var baseFunds = tier switch
            {
                IceTier.T1 => t1Funds,
                IceTier.T2 => t2Funds,
                IceTier.T3 => t3Funds,
                _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown ice tier.")
            };
            var specialMultiplier = specialType == SpecialIceType.Crystal
                ? crystalMultiplier
                : 1.0;

            return (long)Math.Floor(
                baseFunds
                * specialMultiplier
                * (1.0 + maintenanceEfficiencyLevel * 0.10));
        }
    }
}
