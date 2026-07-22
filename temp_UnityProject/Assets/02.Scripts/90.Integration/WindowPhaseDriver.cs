#nullable enable

using Icebreaker.Shared.State;
using Icebreaker.UI.Hud;
using Icebreaker.Window;
using UnityEngine;

namespace Icebreaker.Integration
{
    [DefaultExecutionOrder(-900)]
    public sealed class WindowPhaseDriver : MonoBehaviour
    {
        private const string StateSourceTypeName =
            "Icebreaker.Integration.Int02IntegrationOrchestrator";

        private IGameStateSource? source;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Application.isEditor || Application.isBatchMode ||
                Object.FindFirstObjectByType<WindowPhaseDriver>() != null)
            {
                return;
            }

            var driverObject = new GameObject("WindowPhaseDriver");
            Object.DontDestroyOnLoad(driverObject);
            driverObject.AddComponent<WindowPhaseDriver>();
        }

        private void Start()
        {
            if (Application.isEditor || Application.isBatchMode)
            {
                enabled = false;
                return;
            }

            source = FindStateSource();
            if (source == null)
            {
                return;
            }

            source.StateChanged += HandleStateChanged;
            WindowBootstrap.Instance?.ApplyPhase(source.CurrentState.Phase);
        }

        private void OnDestroy()
        {
            if (source != null)
            {
                source.StateChanged -= HandleStateChanged;
            }
        }

        private static void HandleStateChanged(GameState state) =>
            WindowBootstrap.Instance?.ApplyPhase(state.Phase);

        private static IGameStateSource? FindStateSource()
        {
            var behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var behaviour in behaviours)
            {
                if (behaviour.enabled &&
                    behaviour.GetType().FullName == StateSourceTypeName &&
                    behaviour is IGameStateSource candidate)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
