#nullable enable

namespace Icebreaker.Shared.State
{
    /// <summary>
    /// Authoritative live clock used immediately before combat input or damage is accepted.
    /// Paused time is excluded from StageElapsedSeconds.
    /// </summary>
    public interface IStageClock
    {
        GamePhase Phase { get; }

        double StageElapsedSeconds { get; }

        double RemainingSeconds { get; }

        bool IsPaused { get; }
    }
}
