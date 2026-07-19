#nullable enable

using Icebreaker.Shared;

namespace Icebreaker.Shared.Combat
{
    /// <summary>
    /// Immutable final-value snapshot created once before a stage. Upgrade levels are not exposed here.
    /// </summary>
    public sealed class CombatConfig
    {
        public CombatConfig(
            DirectAttackConfig directAttack,
            IceFieldConfig iceField,
            SupportAttackConfig supportAttack,
            ChainEffectConfig chainEffect)
        {
            DirectAttack = ContractGuards.NotNull(directAttack, nameof(directAttack));
            IceField = ContractGuards.NotNull(iceField, nameof(iceField));
            SupportAttack = ContractGuards.NotNull(supportAttack, nameof(supportAttack));
            ChainEffect = ContractGuards.NotNull(chainEffect, nameof(chainEffect));
        }

        public DirectAttackConfig DirectAttack { get; }

        public IceFieldConfig IceField { get; }

        public SupportAttackConfig SupportAttack { get; }

        public ChainEffectConfig ChainEffect { get; }
    }
}
