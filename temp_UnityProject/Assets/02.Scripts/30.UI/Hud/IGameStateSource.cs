#nullable enable

using System;
using Icebreaker.Shared.State;

namespace Icebreaker.UI.Hud
{
    /// <summary>
    /// UI-facing state boundary. Core or a preview source can provide the same read-only snapshots.
    /// </summary>
    public interface IGameStateSource
    {
        event Action<GameState> StateChanged;

        GameState CurrentState { get; }

        void EnsureInitialized();
    }
}
