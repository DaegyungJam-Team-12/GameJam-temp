#nullable enable

using System;
using Icebreaker.Core;
using Icebreaker.Shared.State;

namespace Icebreaker.Integration
{
    public sealed class GameStateSourceAdapter : Icebreaker.UI.Hud.IGameStateSource
    {
        private readonly ProgressionStateService service;

        public GameStateSourceAdapter(ProgressionStateService service)
        {
            this.service = service;
            service.StateChanged += HandleStateChanged;
        }

        public event Action<GameState> StateChanged = delegate { };

        public GameState CurrentState => service.CurrentState;

        public void EnsureInitialized() => service.EnsureInitialized();

        private void HandleStateChanged(GameState state) => StateChanged(state);
    }
}
