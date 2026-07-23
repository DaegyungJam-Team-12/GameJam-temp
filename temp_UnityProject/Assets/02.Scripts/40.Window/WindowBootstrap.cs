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

        private readonly WindowLocalSettings localSettings = WindowLocalSettings.CreateDefault();
        private WindowSettingsData settingsData;
        private int coordinateSpaceBottom;
        private PixelRect collapsedReferenceRect;
        private bool dragActive;
        private WindowWorkAreaSnapshot dragSnapshot;

        public static WindowBootstrap? Instance { get; private set; }

        /// <summary>
        /// Gets the final startup result, or null while native attach is still pending.
        /// </summary>
        public WindowStartupResult? LastResult { get; private set; }

        public WindowPositionMode CurrentPositionMode => settingsData.PositionMode;

        public WindowPositionPreset CurrentPositionPreset => settingsData.PositionPreset;

        public WindowSizePreset CurrentSizePreset => settingsData.SizePreset;

        private void Awake()
        {
            Instance = this;
            settingsData = localSettings.Load();
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

            // Borderless transparent window so the launcher's ship/box/ice float over the desktop.
            // The per-pixel alpha comes from the rendered content, so each phase must supply its
            // own opaque background where it should not be see-through (e.g. the gameplay ocean
            // backdrop). The camera therefore clears to fully transparent instead of the opaque
            // navy fill used in the editor fallback.
            _controller.isTransparent = true;
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }

            var snapshot = ResolveWorkAreaSnapshot();

            var sizePreset = WindowLayout.ResolveFittingSizePreset(snapshot.WorkArea, settingsData.SizePreset);
            if (sizePreset != settingsData.SizePreset)
            {
                settingsData = settingsData.WithSizePreset(sizePreset);
                localSettings.SaveSizePreset(sizePreset);
            }

            var collapsedSize = WindowLayout.ClientSizeForPreset(WindowView.Collapsed, sizePreset);
            collapsedReferenceRect = ResolveCollapsedRect(snapshot.WorkArea, collapsedSize);

            PixelSize targetSize;
            PixelRect targetRect;
            if (view == WindowView.Collapsed)
            {
                targetSize = collapsedSize;
                targetRect = collapsedReferenceRect;
            }
            else
            {
                targetSize = WindowLayout.ClientSizeForPreset(WindowView.Expanded, sizePreset);
                var anchor = WindowLayout.DetermineEdgeAnchor(collapsedReferenceRect, snapshot.WorkArea);
                targetRect = WindowLayout.CalculateExpandedRectFromAnchor(
                    collapsedReferenceRect,
                    targetSize,
                    anchor,
                    snapshot.WorkArea);
            }

            var nativeWindow = new UniWindowAdapter(_controller, snapshot.CoordinateSpaceBottom);
            var viewController = new WindowViewController(nativeWindow);
            viewController.ApplyRect(targetSize, targetRect);
        }

        private PixelRect ResolveCollapsedRect(PixelRect workArea, PixelSize collapsedSize) =>
            settingsData.PositionMode == WindowPositionMode.Preset
                ? WindowLayout.ResolvePresetRect(workArea, collapsedSize, settingsData.PositionPreset)
                : WindowLayout.ResolveNormalizedRect(
                    workArea,
                    collapsedSize,
                    settingsData.NormalizedX,
                    settingsData.NormalizedY);

        // Re-resolves the current monitor's work area from the native window's live position,
        // so DPI/resolution/monitor changes are honored on every call rather than sticking to
        // whatever monitor the window started on.
        private WindowWorkAreaSnapshot ResolveWorkAreaSnapshot()
        {
            if (_controller == null || coordinateSpaceBottom <= 0)
            {
                var primary = WindowWorkAreaProvider.GetPrimary();
                coordinateSpaceBottom = primary.CoordinateSpaceBottom;
                return primary;
            }

            var probe = new UniWindowAdapter(_controller, coordinateSpaceBottom);
            var currentRect = new PixelRect(
                probe.Position.X,
                probe.Position.Y,
                probe.ClientSize.Width,
                probe.ClientSize.Height);

            var snapshot = WindowWorkAreaProvider.GetForWindow(currentRect);
            coordinateSpaceBottom = snapshot.CoordinateSpaceBottom;
            return snapshot;
        }

        public bool IsSizePresetAvailable(WindowSizePreset preset) =>
            WindowLayout.SizePresetFits(ResolveWorkAreaSnapshot().WorkArea, preset);

        public void ApplyPositionPreset(WindowPositionPreset preset)
        {
            settingsData = settingsData.WithPositionPreset(preset);
            localSettings.SavePositionPreset(preset);
            ReapplyCurrentView();
        }

        public void ResetPosition() => ApplyPositionPreset(WindowLocalSettings.DefaultPositionPreset);

        public void ApplySizePreset(WindowSizePreset preset)
        {
            settingsData = settingsData.WithSizePreset(preset);
            localSettings.SaveSizePreset(preset);
            ReapplyCurrentView();
        }

        public void ResetSize() => ApplySizePreset(WindowLocalSettings.DefaultSizePreset);

        /// <summary>Starts a manual drag of the collapsed bar. No-op outside the native plugin path.</summary>
        public void BeginDrag()
        {
            if (LastResult?.Mode != WindowStartupMode.PluginWindow || _controller == null)
            {
                return;
            }

            dragSnapshot = ResolveWorkAreaSnapshot();
            dragActive = true;
        }

        /// <summary>Moves the window by a pixel delta during an active drag. Never persists.</summary>
        public void DragBy(int deltaX, int deltaY)
        {
            if (!dragActive || _controller == null)
            {
                return;
            }

            var adapter = new UniWindowAdapter(_controller, coordinateSpaceBottom);
            var current = adapter.Position;
            var size = adapter.ClientSize;
            var moved = new PixelRect(current.X + deltaX, current.Y + deltaY, size.Width, size.Height);
            var clamped = WindowLayout.ClampToWorkArea(moved, dragSnapshot.WorkArea);
            adapter.Position = new PixelPoint(clamped.X, clamped.Y);
        }

        /// <summary>Ends a manual drag and persists the resulting Custom position exactly once.</summary>
        public void EndDrag()
        {
            if (!dragActive)
            {
                return;
            }

            dragActive = false;

            if (_controller == null)
            {
                return;
            }

            var adapter = new UniWindowAdapter(_controller, coordinateSpaceBottom);
            var finalPosition = adapter.Position;
            var finalSize = adapter.ClientSize;
            var normalized = WindowLayout.PositionToNormalized(dragSnapshot.WorkArea, finalSize, finalPosition);

            settingsData = settingsData.WithCustomPosition(
                dragSnapshot.MonitorId,
                normalized.NormalizedX,
                normalized.NormalizedY);
            localSettings.SaveCustomPosition(dragSnapshot.MonitorId, normalized.NormalizedX, normalized.NormalizedY);
            collapsedReferenceRect = new PixelRect(
                finalPosition.X,
                finalPosition.Y,
                finalSize.Width,
                finalSize.Height);
        }

        private void ReapplyCurrentView()
        {
            if (!_finalized || LastResult?.Mode != WindowStartupMode.PluginWindow || _controller == null)
            {
                return;
            }

            try
            {
                ApplyPluginView(_requestedView);
            }
            catch (Exception exception)
            {
                Destroy(_controller);
                _controller = null;
                UseNormalWindowFallback(
                    $"Native window apply threw {exception.GetType().Name}: {exception.Message}");
            }
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
