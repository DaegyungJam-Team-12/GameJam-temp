#nullable enable

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Icebreaker.Window
{
    public readonly struct WindowWorkAreaSnapshot
    {
        public WindowWorkAreaSnapshot(PixelRect workArea, int coordinateSpaceBottom)
        {
            WorkArea = workArea;
            CoordinateSpaceBottom = coordinateSpaceBottom;
        }

        public PixelRect WorkArea { get; }

        public int CoordinateSpaceBottom { get; }
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

        private static WindowWorkAreaSnapshot GetFullScreenFallback()
        {
            var resolution = Screen.currentResolution;
            var width = resolution.width > 0 ? resolution.width : Display.main.systemWidth;
            var height = resolution.height > 0 ? resolution.height : Display.main.systemHeight;
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            return new WindowWorkAreaSnapshot(new PixelRect(0, 0, width, height), height);
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

            snapshot = new WindowWorkAreaSnapshot(
                new PixelRect(workArea.Left, workArea.Top, width, height),
                coordinateSpaceBottom);
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

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SystemParametersInfo(
            uint action,
            uint parameter,
            out NativeRect data,
            uint flags);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);
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
                snapshot = new WindowWorkAreaSnapshot(
                    new PixelRect(x, y, width, height),
                    coordinateSpaceBottom);
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
        private static extern NativeRect SendRectDirect(IntPtr receiver, IntPtr selector);

        [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend_stret")]
        private static extern void SendRectStret(
            out NativeRect result,
            IntPtr receiver,
            IntPtr selector);
#endif
    }
}
