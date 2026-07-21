#nullable enable

using UnityEngine;

namespace Icebreaker.Gameplay
{
    /// <summary>
    /// Tracks mouse-hold state and fires attack ticks at a configurable rate.
    /// Spec: click fires immediately once, then holding fires at (5 + D02Level * 2) per second, max 11/s.
    /// </summary>
    public sealed class HoldInputHandler
    {
        private readonly float attacksPerSecond;
        private float holdTimer;
        private bool isHolding;

        /// <param name="attacksPerSecond">
        /// Hold attack frequency. Formula: 5 + D02Level * 2. Max 11.
        /// At D02 level 0 = 5/s, level 1 = 7/s, level 2 = 9/s, level 3 = 11/s.
        /// </param>
        public HoldInputHandler(float attacksPerSecond)
        {
            this.attacksPerSecond = Mathf.Max(1f, attacksPerSecond);
            holdTimer = 0f;
            isHolding = false;
        }

        /// <summary>The interval between hold ticks in seconds.</summary>
        public float TickInterval => 1f / attacksPerSecond;

        /// <summary>
        /// Call every frame with the current button state.
        /// Returns the number of attack ticks that should fire this frame.
        /// </summary>
        /// <param name="isPressed">True if the left mouse button is currently held down.</param>
        /// <param name="wasPressedThisFrame">True on the frame the button was first pressed.</param>
        /// <param name="deltaTime">Time.deltaTime for this frame.</param>
        /// <returns>Number of attacks to perform this frame (0, 1, or more if lag spike).</returns>
        public int Update(bool isPressed, bool wasPressedThisFrame, float deltaTime)
        {
            if (wasPressedThisFrame)
            {
                // First press: fire immediately, start hold timer.
                isHolding = true;
                holdTimer = 0f;
                return 1; // The initial click attack.
            }

            if (!isPressed)
            {
                // Button released.
                isHolding = false;
                holdTimer = 0f;
                return 0;
            }

            if (!isHolding)
            {
                return 0;
            }

            // Holding: accumulate time and fire ticks.
            holdTimer += deltaTime;
            var interval = TickInterval;
            var ticks = 0;

            while (holdTimer >= interval)
            {
                holdTimer -= interval;
                ticks++;
            }

            return ticks;
        }
    }
}
