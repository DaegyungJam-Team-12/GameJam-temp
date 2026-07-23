#nullable enable

using Icebreaker.Shared.State;
using Icebreaker.UI.Hud;
using Icebreaker.Window;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Icebreaker.Integration
{
    [DefaultExecutionOrder(-900)]
    public sealed class WindowPhaseDriver : MonoBehaviour
    {
        private const string StateSourceTypeName =
            "Icebreaker.Integration.Int02IntegrationOrchestrator";

        private IGameStateSource? source;
        private IManagementScreenSource? managementSource;

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

            SceneManager.sceneLoaded += HandleSceneLoaded;
            Acquire();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Unsubscribe();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Acquire();

        // Re-finds the current scene's orchestrator and re-subscribes. Safe to call repeatedly;
        // required because this driver survives scene reloads (e.g. save reset) while the
        // orchestrator it listened to is destroyed and replaced.
        private void Acquire()
        {
            Unsubscribe();
            source = FindStateSource();
            managementSource = source as IManagementScreenSource;
            if (source == null || managementSource == null)
            {
                return;
            }

            source.StateChanged += HandleStateChanged;
            managementSource.ManagementScreenChanged += HandleManagementScreenChanged;
            ApplyWindowView();
        }

        private void Unsubscribe()
        {
            if (source != null)
            {
                source.StateChanged -= HandleStateChanged;
            }

            if (managementSource != null)
            {
                managementSource.ManagementScreenChanged -= HandleManagementScreenChanged;
            }

            source = null;
            managementSource = null;
        }

        private void HandleStateChanged(GameState state) => ApplyWindowView();

        private void HandleManagementScreenChanged(ManagementScreen screen) => ApplyWindowView();

        private void ApplyWindowView()
        {
            if (source == null || managementSource == null)
            {
                return;
            }

            WindowBootstrap.Instance?.ApplyView(WindowLayout.ViewForState(
                source.CurrentState.Phase,
                managementSource.CurrentManagementScreen));
        }

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
