#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Icebreaker.Window
{
    public readonly struct WindowWorkAreaSnapshot
    {
        public WindowWorkAreaSnapshot(PixelRect workArea, int coordinateSpaceBottom, string monitorId)
        {
            WorkArea = workArea;
            CoordinateSpaceBottom = coordinateSpaceBottom;
            MonitorId = monitorId ?? string.Empty;
        }

        public PixelRect WorkArea { get; }

        public int CoordinateSpaceBottom { get; }

        /// <summary>
        /// Stable-for-this-arrangement identifier for the monitor this work area belongs to.
        /// Derived from the work-area bounds; changes if the monitor arrangement or resolution
        /// changes, which is exactly when a previously saved identifier should be treated as
        /// stale.
        /// </summary>
        public string MonitorId { get; }
    }

    public static class WindowWorkAreaProvider
    {
        public static WindowWorkAreaSnapshot GetPrimary()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (TryGetWindowsWorkArea(out var windowsWorkArea))
            {
                return windowsWorkArea;
            }
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (TryGetMacWorkArea(out var macWorkArea))
            {
                return macWorkArea;
            }
#endif
            return GetFullScreenFallback();
        }

        /// <summary>
        /// Resolves the work area of the monitor that <paramref name="windowRect"/> (in
        /// WindowLayout's top-left/y-down pixel space) currently most overlaps. Falls back to
        /// the primary monitor when no native multi-monitor query is available.
        /// </summary>
        public static WindowWorkAreaSnapshot GetForWindow(PixelRect windowRect)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (TryGetWindowsWorkAreaForWindow(windowRect, out var windowsWorkArea))
            {
                return windowsWorkArea;
            }
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (TryGetMacWorkAreaForWindow(windowRect, out var macWorkArea))
            {
                return macWorkArea;
            }
#endif
            return GetPrimary();
        }

        private static string MakeMonitorId(PixelRect workArea) =>
            $"{workArea.X}:{workArea.Y}:{workArea.Width}:{workArea.Height}";

        private static WindowWorkAreaSnapshot GetFullScreenFallback()
        {
            var resolution = Screen.currentResolution;
            var width = resolution.width > 0 ? resolution.width : Display.main.systemWidth;
            var height = resolution.height > 0 ? resolution.height : Display.main.systemHeight;
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            var workArea = new PixelRect(0, 0, width, height);
            return new WindowWorkAreaSnapshot(workArea, height, MakeMonitorId(workArea));
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const uint SpiGetWorkArea = 0x0030;
        private const int SmCyScreen = 1;

        private static bool TryGetWindowsWorkArea(out WindowWorkAreaSnapshot snapshot)
        {
            snapshot = default;
            if (!SystemParametersInfo(SpiGetWorkArea, 0, out var workArea, 0))
            {
                return false;
            }

            var width = workArea.Right - workArea.Left;
            var height = workArea.Bottom - workArea.Top;
            var coordinateSpaceBottom = GetSystemMetrics(SmCyScreen);
            if (width <= 0 || height <= 0 || coordinateSpaceBottom <= 0)
            {
                return false;
            }

            var resultWorkArea = new PixelRect(workArea.Left, workArea.Top, width, height);
            snapshot = new WindowWorkAreaSnapshot(resultWorkArea, coordinateSpaceBottom, MakeMonitorId(resultWorkArea));
            return true;
        }

        private const uint MonitorDefaultToNearest = 2;

        private static bool TryGetWindowsWorkAreaForWindow(PixelRect windowRect, out WindowWorkAreaSnapshot snapshot)
        {
            snapshot = default;

            var rect = new NativeRect
            {
                Left = windowRect.X,
                Top = windowRect.Y,
                Right = windowRect.Right,
                Bottom = windowRect.Bottom
            };

            var monitor = MonitorFromRect(ref rect, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero)
            {
                return false;
            }

            var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info))
            {
                return false;
            }

            var width = info.WorkArea.Right - info.WorkArea.Left;
            var height = info.WorkArea.Bottom - info.WorkArea.Top;
            var coordinateSpaceBottom = GetSystemMetrics(SmCyScreen);
            if (width <= 0 || height <= 0 || coordinateSpaceBottom <= 0)
            {
                return false;
            }

            var resultWorkArea = new PixelRect(info.WorkArea.Left, info.WorkArea.Top, width, height);
            snapshot = new WindowWorkAreaSnapshot(resultWorkArea, coordinateSpaceBottom, MakeMonitorId(resultWorkArea));
            return true;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int cbSize;
            public NativeRect Monitor;
            public NativeRect WorkArea;
            public uint Flags;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SystemParametersInfo(
            uint action,
            uint parameter,
            out NativeRect data,
            uint flags);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromRect(ref NativeRect rect, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
#endif

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        private const string ObjectiveCLibrary = "/usr/lib/libobjc.A.dylib";

        private static bool TryGetMacWorkArea(out WindowWorkAreaSnapshot snapshot)
        {
            snapshot = default;

            try
            {
                var screenClass = objc_getClass("NSScreen");
                var screen = SendIntPtr(screenClass, sel_registerName("mainScreen"));
                if (screen == IntPtr.Zero)
                {
                    return false;
                }

                var frame = SendRect(screen, sel_registerName("frame"));
                var visibleFrame = SendRect(screen, sel_registerName("visibleFrame"));
                if (frame.Width <= 0d || frame.Height <= 0d ||
                    visibleFrame.Width <= 0d || visibleFrame.Height <= 0d)
                {
                    return false;
                }

                var x = Round(visibleFrame.X - frame.X);
                var y = Round(frame.Height - ((visibleFrame.Y - frame.Y) + visibleFrame.Height));
                var width = Round(visibleFrame.Width);
                var height = Round(visibleFrame.Height);
                var coordinateSpaceBottom = Round(frame.Height);
                var workArea = new PixelRect(x, y, width, height);
                snapshot = new WindowWorkAreaSnapshot(workArea, coordinateSpaceBottom, MakeMonitorId(workArea));
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // NSScreen.frame/visibleFrame are all expressed in a single global coordinate space
        // whose origin is the bottom-left of the main (menu-bar) screen. Converting a screen's
        // visibleFrame directly (without subtracting the main screen's own origin, which is
        // always (0,0)) yields a WindowLayout-space rect relative to the main screen's top-left,
        // correctly handling negative origins for monitors placed left of/above the main screen.
        private static bool TryGetMacWorkAreaForWindow(PixelRect windowRect, out WindowWorkAreaSnapshot snapshot)
        {
            snapshot = default;

            try
            {
                var screenClass = objc_getClass("NSScreen");
                var mainScreen = SendIntPtr(screenClass, sel_registerName("mainScreen"));
                if (mainScreen == IntPtr.Zero)
                {
                    return false;
                }

                var mainFrame = SendRect(mainScreen, sel_registerName("frame"));
                if (mainFrame.Height <= 0d)
                {
                    return false;
                }

                var coordinateSpaceBottom = Round(mainFrame.Height);

                var screens = SendIntPtr(screenClass, sel_registerName("screens"));
                if (screens == IntPtr.Zero)
                {
                    return false;
                }

                var count = (long)SendUIntPtr(screens, sel_registerName("count"));
                if (count <= 0)
                {
                    return false;
                }

                var bounds = new List<PixelRect>();
                var workAreas = new List<PixelRect>();

                for (var i = 0L; i < count; i++)
                {
                    var screen = SendIntPtrIndex(screens, sel_registerName("objectAtIndex:"), (IntPtr)i);
                    if (screen == IntPtr.Zero)
                    {
                        continue;
                    }

                    var frame = SendRect(screen, sel_registerName("frame"));
                    var visibleFrame = SendRect(screen, sel_registerName("visibleFrame"));
                    if (frame.Width <= 0d || frame.Height <= 0d ||
                        visibleFrame.Width <= 0d || visibleFrame.Height <= 0d)
                    {
                        continue;
                    }

                    bounds.Add(new PixelRect(
                        Round(frame.X),
                        Round(mainFrame.Height - (frame.Y + frame.Height)),
                        Round(frame.Width),
                        Round(frame.Height)));
                    workAreas.Add(new PixelRect(
                        Round(visibleFrame.X),
                        Round(mainFrame.Height - (visibleFrame.Y + visibleFrame.Height)),
                        Round(visibleFrame.Width),
                        Round(visibleFrame.Height)));
                }

                if (bounds.Count == 0)
                {
                    return false;
                }

                var bestIndex = WindowLayout.SelectMostOverlappingIndex(windowRect, bounds);
                var workArea = workAreas[bestIndex];
                snapshot = new WindowWorkAreaSnapshot(workArea, coordinateSpaceBottom, MakeMonitorId(workArea));
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static NativeRect SendRect(IntPtr receiver, IntPtr selector)
        {
            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                SendRectStret(out var result, receiver, selector);
                return result;
            }

            return SendRectDirect(receiver, selector);
        }

        private static int Round(double value) =>
            (int)Math.Round(value, MidpointRounding.AwayFromZero);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public double X;
            public double Y;
            public double Width;
            public double Height;
        }

        [DllImport(ObjectiveCLibrary)]
        private static extern IntPtr objc_getClass(string name);

        [DllImport(ObjectiveCLibrary)]
        private static extern IntPtr sel_registerName(string name);

        [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
        private static extern IntPtr SendIntPtr(IntPtr receiver, IntPtr selector);

        [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
        private static extern UIntPtr SendUIntPtr(IntPtr receiver, IntPtr selector);

        [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
        private static extern IntPtr SendIntPtrIndex(IntPtr receiver, IntPtr selector, IntPtr index);

        [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
        private static extern NativeRect SendRectDirect(IntPtr receiver, IntPtr selector);

        [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend_stret")]
        private static extern void SendRectStret(
            out NativeRect result,
            IntPtr receiver,
            IntPtr selector);
#endif
    }
}
