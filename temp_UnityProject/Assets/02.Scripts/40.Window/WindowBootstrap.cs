#nullable enable

using System;
using UnityEngine;

namespace Icebreaker.Window
{
    public sealed class WindowBootstrap : MonoBehaviour
    {
        private const string ForceFallbackArgument = "-forceWindowFallback";
        private const string ForceFallbackEnvironmentVariable = "ICEBREAKER_FORCE_WINDOW_FALLBACK";

        public WindowStartupResult? LastResult { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (UnityEngine.Object.FindFirstObjectByType<WindowBootstrap>() != null)
            {
                return;
            }

            var bootstrapObject = new GameObject("WindowBootstrap");
            UnityEngine.Object.DontDestroyOnLoad(bootstrapObject);
            bootstrapObject.AddComponent<WindowBootstrap>();
        }

        private void Start()
        {
            var result = WindowStartupDecision.Decide(ReadForceFallbackFlag(), TryInitializePlugin);
            LastResult = result;

            if (result.Mode == WindowStartupMode.PluginWindow)
            {
                Debug.Log("[WIN-00] UniWindowController plugin path active");
                return;
            }

            Screen.SetResolution(960, 540, FullScreenMode.Windowed);
            Debug.LogWarning($"[WIN-00] normal-window fallback 960x540: {result.Reason}");
        }

        private static bool ReadForceFallbackFlag()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, ForceFallbackArgument, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return Environment.GetEnvironmentVariable(ForceFallbackEnvironmentVariable) == "1";
        }

        private bool TryInitializePlugin()
        {
            // WIN-01 will verify the real Windows-specific window behavior.
            var controller = Kirurobo.UniWindowController.current;
            return controller != null;
        }
    }
}
