#nullable enable

using System;
using System.Collections;
using Icebreaker.Shared.State;
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
        private bool _hasRequestedView;
        private WindowView _requestedView = WindowView.Collapsed;

        public static WindowBootstrap? Instance { get; private set; }

        /// <summary>
        /// Gets the final startup result, or null while native attach is still pending.
        /// </summary>
        public WindowStartupResult? LastResult { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

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

            if (result.Mode == WindowStartupMode.PluginWindow)
            {
                try
                {
                    ApplyPluginView(_requestedView);
                    _hasRequestedView = true;
                    LastResult = result;
                    Debug.Log("[WIN-00] UniWindowController plugin path active: " + result.Reason);
                    return;
                }
                catch (Exception exception)
                {
                    Destroy(_controller);
                    _controller = null;
                    UseNormalWindowFallback(
                        $"Native window apply threw {exception.GetType().Name}: {exception.Message}");
                    return;
                }
            }

            UseNormalWindowFallback(result.Reason);
        }

        public void ApplyPhase(GamePhase phase) =>
            ApplyView(WindowLayout.ViewForPhase(phase));

        public void ApplyView(WindowView view)
        {
            WindowLayout.ClientSizeForView(view);
            var viewChanged = !_hasRequestedView || _requestedView != view;
            _requestedView = view;
            _hasRequestedView = true;

            if (!_finalized || !viewChanged)
            {
                return;
            }

            if (LastResult?.Mode == WindowStartupMode.PluginWindow && _controller != null)
            {
                try
                {
                    ApplyPluginView(view);
                }
                catch (Exception exception)
                {
                    Destroy(_controller);
                    _controller = null;
                    UseNormalWindowFallback(
                        $"Native window apply threw {exception.GetType().Name}: {exception.Message}");
                }

                return;
            }
            // The safe fallback deliberately remains a normal 960x540 window.
        }

        private void ApplyPluginView(WindowView view)
        {
            if (_controller == null)
            {
                throw new InvalidOperationException("UniWindowController is not attached.");
            }

            var workArea = WindowWorkAreaProvider.GetPrimary();
            var nativeWindow = new UniWindowAdapter(_controller, workArea.CoordinateSpaceBottom);
            var viewController = new WindowViewController(nativeWindow);
            viewController.ApplyView(view, workArea.WorkArea);
        }

        private void UseNormalWindowFallback(string reason)
        {
            LastResult = new WindowStartupResult(WindowStartupMode.NormalWindowFallback, reason);
            Screen.SetResolution(960, 540, FullScreenMode.Windowed);
            Debug.LogWarning("[WIN-00] normal-window fallback 960x540: " + reason);
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

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
