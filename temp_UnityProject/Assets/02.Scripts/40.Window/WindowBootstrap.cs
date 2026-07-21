#nullable enable

using System;
using System.Collections;
using UnityEngine;

namespace Icebreaker.Window
{
    public sealed class WindowBootstrap : MonoBehaviour
    {
        private const string ForceFallbackArgument = "-forceWindowFallback";
        private const string ForceFallbackEnvironmentVariable = "ICEBREAKER_FORCE_WINDOW_FALLBACK";
        private const double AttachTimeoutSeconds = 2.0;

        private Kirurobo.UniWindowController? _controller;
        private bool _finalized;

        /// <summary>
        /// Gets the final startup result, or null while native attach is still pending.
        /// </summary>
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
            var result = WindowStartupDecision.Decide(ReadForceFallbackFlag(), CreateController);
            if (result.Mode == WindowStartupMode.NormalWindowFallback)
            {
                FinalizeResult(result);
                return;
            }

            StartCoroutine(WaitForAttach());
        }

        private IEnumerator WaitForAttach()
        {
            var startTime = Time.realtimeSinceStartupAsDouble;

            while (!_finalized)
            {
                var finished = false;

                try
                {
                    var attached = _controller != null
                        && _controller.windowSize.x > 0f
                        && _controller.windowSize.y > 0f;
                    var elapsed = Time.realtimeSinceStartupAsDouble - startTime;
                    var status = WindowAttachWait.Evaluate(attached, elapsed, AttachTimeoutSeconds);

                    if (status == AttachWaitStatus.Attached)
                    {
                        FinalizeResult(new WindowStartupResult(
                            WindowStartupMode.PluginWindow,
                            $"Native window attached in {elapsed:F2}s."));
                        finished = true;
                    }
                    else if (status == AttachWaitStatus.TimedOut)
                    {
                        Destroy(_controller);
                        FinalizeResult(new WindowStartupResult(
                            WindowStartupMode.NormalWindowFallback,
                            $"Native attach timeout after {AttachTimeoutSeconds:F1}s."));
                        finished = true;
                    }
                }
                catch (Exception exception)
                {
                    Destroy(_controller);
                    FinalizeResult(new WindowStartupResult(
                        WindowStartupMode.NormalWindowFallback,
                        $"Native attach polling threw {exception.GetType().Name}: {exception.Message}"));
                    finished = true;
                }

                if (finished)
                {
                    yield break;
                }

                yield return null;
            }
        }

        private void FinalizeResult(WindowStartupResult result)
        {
            if (_finalized)
            {
                return;
            }

            _finalized = true;
            LastResult = result;

            if (result.Mode == WindowStartupMode.PluginWindow)
            {
                Debug.Log("[WIN-00] UniWindowController plugin path active: " + result.Reason);
                return;
            }

            Screen.SetResolution(960, 540, FullScreenMode.Windowed);
            Debug.LogWarning("[WIN-00] normal-window fallback 960x540: " + result.Reason);
        }

        private static bool ReadForceFallbackFlag()
        {
            // In the editor the native plugin attaches to the editor's own window and can leave
            // it mangled (invisible/zero-size) when Play Mode exits; never attach in the editor.
            if (Application.isEditor)
            {
                return true;
            }

            // Headless/batch and CI (including PlayMode test runs) have no attachable OS window;
            // the native plugin crashes if it tries to grab one, so always use the plain window.
            if (Application.isBatchMode)
            {
                return true;
            }

            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, ForceFallbackArgument, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return Environment.GetEnvironmentVariable(ForceFallbackEnvironmentVariable) == "1";
        }

        private bool CreateController()
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<Kirurobo.UniWindowController>();
            _controller = existing != null
                ? existing
                : gameObject.AddComponent<Kirurobo.UniWindowController>();
            return _controller != null;
        }
    }
}
