#nullable enable

using System;
using Icebreaker.Shared.State;

namespace Icebreaker.Window
{
    public readonly struct PixelSize
    {
        public PixelSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }

        public int Height { get; }
    }

    public readonly struct PixelPoint
    {
        public PixelPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }
    }

    /// <summary>
    /// Integer client-pixel rectangle in a top-left-origin, x-right, y-down coordinate system.
    /// Values are used as supplied; no DPI scaling is performed.
    /// </summary>
    public readonly struct PixelRect
    {
        public PixelRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; }

        public int Y { get; }

        public int Width { get; }

        public int Height { get; }

        public int Right => X + Width;

        public int Bottom => Y + Height;
    }

    public static class WindowLayout
    {
        public const int WorkAreaMarginPixels = 8;

        private static readonly PixelSize CollapsedClientSize = new(800, 72);
        private static readonly PixelSize ExpandedClientSize = new(960, 540);

        public static PixelSize ClientSizeForView(WindowView view) => view switch
        {
            WindowView.Collapsed => CollapsedClientSize,
            WindowView.Expanded => ExpandedClientSize,
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, null)
        };

        public static WindowView ViewForPhase(GamePhase phase) => phase switch
        {
            GamePhase.Traveling => WindowView.Collapsed,
            GamePhase.Ready => WindowView.Collapsed,
            GamePhase.Countdown => WindowView.Expanded,
            GamePhase.Playing => WindowView.Expanded,
            GamePhase.StageEnding => WindowView.Expanded,
            GamePhase.Settlement => WindowView.Expanded,
            GamePhase.Arrival => WindowView.Expanded,
            GamePhase.Completed => WindowView.Collapsed,
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
        };

        public static WindowView ViewForState(
            GamePhase phase,
            ManagementScreen managementScreen) =>
            managementScreen != ManagementScreen.None
                ? WindowView.Expanded
                : ViewForPhase(phase);

        public static PixelRect Calculate(
            PixelRect workArea,
            PixelSize targetClientSize,
            int marginPixels = WorkAreaMarginPixels)
        {
            if (workArea.Width <= 0 || workArea.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(workArea));
            }

            if (targetClientSize.Width <= 0 || targetClientSize.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetClientSize));
            }

            if (marginPixels < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(marginPixels));
            }

            var x = workArea.Right - marginPixels - targetClientSize.Width;
            var y = workArea.Bottom - marginPixels - targetClientSize.Height;

            x = ClampAxis(x, targetClientSize.Width, workArea.X, workArea.Right);
            y = ClampAxis(y, targetClientSize.Height, workArea.Y, workArea.Bottom);

            return new PixelRect(x, y, targetClientSize.Width, targetClientSize.Height);
        }

        private static int ClampAxis(int position, int size, int areaStart, int areaEnd)
        {
            if (size > areaEnd - areaStart)
            {
                return areaStart;
            }

            return Math.Max(areaStart, Math.Min(position, areaEnd - size));
        }
    }
}
