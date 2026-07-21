#nullable enable

using UnityEngine;

namespace Icebreaker.Gameplay
{
    /// <summary>
    /// Determines whether a direct attack is a critical hit and calculates the final damage.
    /// Critical hits only apply to direct Click/Hold attacks (not chain, support, etc.).
    /// </summary>
    public sealed class CriticalStrike
    {
        private readonly float chance;
        private readonly float damageMultiplier;

        /// <param name="chance">Probability of a critical hit (0.0 to 1.0). Spec default: 0.05 (5%).</param>
        /// <param name="damageMultiplier">Damage multiplier on critical. Spec default: 3.0.</param>
        public CriticalStrike(float chance, float damageMultiplier)
        {
            this.chance = Mathf.Clamp01(chance);
            this.damageMultiplier = Mathf.Max(1f, damageMultiplier);
        }

        /// <summary>
        /// Roll for a critical hit. Returns true if the roll succeeds.
        /// </summary>
        public bool Roll()
        {
            return Random.value < chance;
        }

        /// <summary>
        /// Calculate final damage for a direct attack, applying critical if the roll succeeds.
        /// </summary>
        /// <param name="baseDamage">The current click damage (before critical).</param>
        /// <param name="wasCritical">Set to true if the attack was a critical hit.</param>
        /// <returns>The final damage value.</returns>
        public float Apply(float baseDamage, out bool wasCritical)
        {
            wasCritical = Roll();
            return wasCritical ? baseDamage * damageMultiplier : baseDamage;
        }
    }
}
