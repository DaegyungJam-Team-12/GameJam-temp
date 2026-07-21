#nullable enable

using System;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Progression;
using Icebreaker.Shared.State;

namespace Icebreaker.Core
{
    public sealed class ProgressionStateService : IDisposable
    {
        private readonly ProgressionCore core;
        private ICombatEventSource? attachedSource;
        private GameState? currentState;

        public ProgressionStateService(DestinationDefinition destination, long initialFunds = 0)
        {
            core = new ProgressionCore(destination, initialFunds);
        }

        public event Action<GameState> StateChanged = delegate { };

        public GameState CurrentState
        {
            get
            {
                EnsureInitialized();
                return currentState!;
            }
        }

        public void EnsureInitialized()
        {
            if (currentState == null)
            {
                currentState = core.CreateSnapshot();
            }
        }

        public void AttachCombatSource(ICombatEventSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (attachedSource != null)
            {
                throw new InvalidOperationException("A combat source is already attached.");
            }

            attachedSource = source;
            source.IceDestroyed += OnIceDestroyed;
            EnsureInitialized();
        }

        public void DetachCombatSource()
        {
            if (attachedSource != null)
            {
                attachedSource.IceDestroyed -= OnIceDestroyed;
                attachedSource = null;
            }
        }

        public void Dispose()
        {
            DetachCombatSource();
        }

        private void OnIceDestroyed(IceDestroyedEvent e)
        {
            if (core.HandleIceDestroyed(e))
            {
                currentState = core.CreateSnapshot();
                StateChanged(currentState);
            }
        }
    }
}
