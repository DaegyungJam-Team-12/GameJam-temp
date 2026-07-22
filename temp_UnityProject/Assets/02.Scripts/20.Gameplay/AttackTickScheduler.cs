#nullable enable

using UnityEngine;

namespace Icebreaker.Gameplay
{
    /// <summary>
    /// Emits continuous automatic attack ticks. The first tick is immediate after reset;
    /// subsequent ticks use the configured fixed interval and are capped per frame.
    /// </summary>
    public sealed class AttackTickScheduler
    {
        private const int MaxTicksPerFrame = 3;

        private readonly float attacksPerSecond;
        private float accumulatedSeconds;
        private bool firstTickPending;

        public AttackTickScheduler(float attacksPerSecond)
        {
            this.attacksPerSecond = Mathf.Max(1f, attacksPerSecond);
            Reset();
        }

        public float TickInterval => 1f / attacksPerSecond;

        public void Reset()
        {
            accumulatedSeconds = 0f;
            firstTickPending = true;
        }

        /// <summary>Returns 0-3 automatic ticks due for this frame.</summary>
        public int Update(float deltaTime)
        {
            if (firstTickPending)
            {
                firstTickPending = false;
                return 1;
            }

            accumulatedSeconds += Mathf.Max(0f, deltaTime);
            var ticks = 0;
            while (accumulatedSeconds >= TickInterval && ticks < MaxTicksPerFrame)
            {
                accumulatedSeconds -= TickInterval;
                ticks++;
            }

            // Do not retain an unbounded backlog after a stall.
            if (ticks == MaxTicksPerFrame && accumulatedSeconds >= TickInterval)
            {
                accumulatedSeconds %= TickInterval;
            }

            return ticks;
        }
    }
}
