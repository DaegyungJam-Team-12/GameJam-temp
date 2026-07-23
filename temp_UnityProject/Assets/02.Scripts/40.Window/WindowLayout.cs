#nullable enable

using System;
using System.Collections.Generic;
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

        // How close (as a fraction of the smaller work-area dimension) the collapsed bar must be
        // to a work-area edge for that edge to be treated as the expansion anchor. Beyond this,
        // the bar is considered to be roughly in the middle of the screen (Center anchor).
        private const float EdgeProximityRatio = 0.2f;

        // Index 0 = Default, 1 = Large, 2 = ExtraLarge (WindowSizePreset order).
        private static readonly PixelSize[] CollapsedSizesByPreset =
        {
            new(800, 72),
            new(1000, 90),
            new(1200, 108)
        };

        private static readonly PixelSize[] ExpandedSizesByPreset =
        {
            new(960, 540),
            new(1200, 675),
            new(1440, 810)
        };

        public static PixelSize ClientSizeForView(WindowView view) =>
            ClientSizeForPreset(view, WindowSizePreset.Default);

        public static PixelSize ClientSizeForPreset(WindowView view, WindowSizePreset preset)
        {
            var sizes = view switch
            {
                WindowView.Collapsed => CollapsedSizesByPreset,
                WindowView.Expanded => ExpandedSizesByPreset,
                _ => throw new ArgumentOutOfRangeException(nameof(view), view, null)
            };

            var index = (int)preset;
            if (index < 0 || index >= sizes.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(preset), preset, null);
            }

            return sizes[index];
        }

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

        /// <summary>
        /// Places <paramref name="targetClientSize"/> at the bottom-center of the work area
        /// (the default position preset), clamped fully inside the work area.
        /// </summary>
        public static PixelRect Calculate(
            PixelRect workArea,
            PixelSize targetClientSize,
            int marginPixels = WorkAreaMarginPixels) =>
            ResolvePresetRect(workArea, targetClientSize, WindowPositionPreset.BottomCenter, marginPixels);

        /// <summary>Resolves a position preset to a clamped rect within the work area.</summary>
        public static PixelRect ResolvePresetRect(
            PixelRect workArea,
            PixelSize clientSize,
            WindowPositionPreset preset,
            int marginPixels = WorkAreaMarginPixels)
        {
            ValidateWorkArea(workArea);
            ValidateClientSize(clientSize);
            ValidateMargin(marginPixels);

            var x = preset switch
            {
                WindowPositionPreset.BottomLeft => workArea.X + marginPixels,
                WindowPositionPreset.BottomCenter => workArea.X + (workArea.Width - clientSize.Width) / 2,
                WindowPositionPreset.BottomRight => workArea.Right - marginPixels - clientSize.Width,
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null)
            };
            var y = workArea.Bottom - marginPixels - clientSize.Height;

            x = ClampAxis(x, clientSize.Width, workArea.X, workArea.Right);
            y = ClampAxis(y, clientSize.Height, workArea.Y, workArea.Bottom);

            return new PixelRect(x, y, clientSize.Width, clientSize.Height);
        }

        /// <summary>
        /// Restores a rect from normalized (0..1) coordinates within the work area. Used to
        /// persist and restore a user-dragged (Custom) position across resolution/DPI/monitor
        /// changes: apply the normalized coordinates against the current work area, then clamp.
        /// </summary>
        public static PixelRect ResolveNormalizedRect(
            PixelRect workArea,
            PixelSize clientSize,
            float normalizedX,
            float normalizedY)
        {
            ValidateWorkArea(workArea);
            ValidateClientSize(clientSize);

            var rangeX = workArea.Width - clientSize.Width;
            var rangeY = workArea.Height - clientSize.Height;
            var x = workArea.X + (int)Math.Round(Clamp01(normalizedX) * rangeX, MidpointRounding.AwayFromZero);
            var y = workArea.Y + (int)Math.Round(Clamp01(normalizedY) * rangeY, MidpointRounding.AwayFromZero);

            return ClampToWorkArea(new PixelRect(x, y, clientSize.Width, clientSize.Height), workArea);
        }

        /// <summary>Inverse of <see cref="ResolveNormalizedRect"/>: captures a position as 0..1 coordinates.</summary>
        public static (float NormalizedX, float NormalizedY) PositionToNormalized(
            PixelRect workArea,
            PixelSize clientSize,
            PixelPoint position)
        {
            ValidateWorkArea(workArea);
            ValidateClientSize(clientSize);

            var rangeX = workArea.Width - clientSize.Width;
            var rangeY = workArea.Height - clientSize.Height;
            var normalizedX = rangeX > 0 ? (float)(position.X - workArea.X) / rangeX : 0f;
            var normalizedY = rangeY > 0 ? (float)(position.Y - workArea.Y) / rangeY : 0f;

            return (Clamp01(normalizedX), Clamp01(normalizedY));
        }

        /// <summary>Clamps an arbitrary rect (e.g. a live drag position) fully inside the work area.</summary>
        public static PixelRect ClampToWorkArea(PixelRect rect, PixelRect workArea)
        {
            ValidateWorkArea(workArea);

            var x = ClampAxis(rect.X, rect.Width, workArea.X, workArea.Right);
            var y = ClampAxis(rect.Y, rect.Height, workArea.Y, workArea.Bottom);
            return new PixelRect(x, y, rect.Width, rect.Height);
        }

        /// <summary>
        /// Determines which work-area edge the collapsed bar is nearest to, to decide which
        /// direction the window should grow toward when expanding. Returns Center when the bar
        /// is not close to any edge.
        /// </summary>
        public static WindowEdgeAnchor DetermineEdgeAnchor(
            PixelRect collapsedRect,
            PixelRect workArea,
            int marginPixels = WorkAreaMarginPixels)
        {
            ValidateWorkArea(workArea);
            ValidateMargin(marginPixels);

            var distanceBottom = workArea.Bottom - collapsedRect.Bottom;
            var distanceTop = collapsedRect.Y - workArea.Y;
            var distanceLeft = collapsedRect.X - workArea.X;
            var distanceRight = workArea.Right - collapsedRect.Right;

            // Order expresses tie-break priority: Bottom > Top > Left > Right.
            var candidates = new[]
            {
                (Anchor: WindowEdgeAnchor.Bottom, Distance: distanceBottom),
                (Anchor: WindowEdgeAnchor.Top, Distance: distanceTop),
                (Anchor: WindowEdgeAnchor.Left, Distance: distanceLeft),
                (Anchor: WindowEdgeAnchor.Right, Distance: distanceRight)
            };

            var best = candidates[0];
            for (var i = 1; i < candidates.Length; i++)
            {
                if (candidates[i].Distance < best.Distance)
                {
                    best = candidates[i];
                }
            }

            var threshold = Math.Max(
                marginPixels,
                (int)(Math.Min(workArea.Width, workArea.Height) * EdgeProximityRatio));

            return best.Distance <= threshold ? best.Anchor : WindowEdgeAnchor.Center;
        }

        /// <summary>
        /// Computes the expanded rect by growing from the collapsed rect toward the given
        /// anchor's opposite edge, then clamps the result fully inside the work area.
        /// </summary>
        public static PixelRect CalculateExpandedRectFromAnchor(
            PixelRect collapsedRect,
            PixelSize expandedClientSize,
            WindowEdgeAnchor anchor,
            PixelRect workArea)
        {
            ValidateWorkArea(workArea);
            ValidateClientSize(expandedClientSize);

            int x;
            int y;
            switch (anchor)
            {
                case WindowEdgeAnchor.Bottom:
                    x = CenterAxis(collapsedRect.X, collapsedRect.Width, expandedClientSize.Width);
                    y = collapsedRect.Bottom - expandedClientSize.Height;
                    break;
                case WindowEdgeAnchor.Top:
                    x = CenterAxis(collapsedRect.X, collapsedRect.Width, expandedClientSize.Width);
                    y = collapsedRect.Y;
                    break;
                case WindowEdgeAnchor.Left:
                    x = collapsedRect.X;
                    y = CenterAxis(collapsedRect.Y, collapsedRect.Height, expandedClientSize.Height);
                    break;
                case WindowEdgeAnchor.Right:
                    x = collapsedRect.Right - expandedClientSize.Width;
                    y = CenterAxis(collapsedRect.Y, collapsedRect.Height, expandedClientSize.Height);
                    break;
                case WindowEdgeAnchor.Center:
                    x = CenterAxis(collapsedRect.X, collapsedRect.Width, expandedClientSize.Width);
                    y = CenterAxis(collapsedRect.Y, collapsedRect.Height, expandedClientSize.Height);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(anchor), anchor, null);
            }

            return ClampToWorkArea(new PixelRect(x, y, expandedClientSize.Width, expandedClientSize.Height), workArea);
        }

        /// <summary>True when both the collapsed and expanded sizes of a preset fit the work area.</summary>
        public static bool SizePresetFits(
            PixelRect workArea,
            WindowSizePreset preset,
            int marginPixels = WorkAreaMarginPixels)
        {
            ValidateWorkArea(workArea);
            ValidateMargin(marginPixels);

            return FitsWithMargin(workArea, ClientSizeForPreset(WindowView.Collapsed, preset), marginPixels)
                && FitsWithMargin(workArea, ClientSizeForPreset(WindowView.Expanded, preset), marginPixels);
        }

        /// <summary>The largest size preset (ExtraLarge..Default) that fully fits the work area.</summary>
        public static WindowSizePreset LargestFittingSizePreset(
            PixelRect workArea,
            int marginPixels = WorkAreaMarginPixels)
        {
            if (SizePresetFits(workArea, WindowSizePreset.ExtraLarge, marginPixels))
            {
                return WindowSizePreset.ExtraLarge;
            }

            if (SizePresetFits(workArea, WindowSizePreset.Large, marginPixels))
            {
                return WindowSizePreset.Large;
            }

            // Default is the guaranteed floor even if it technically does not fully fit; callers
            // still clamp the resulting rect inside the work area so the window stays on-screen.
            return WindowSizePreset.Default;
        }

        /// <summary>Returns <paramref name="preferred"/> if it fits, otherwise the largest fitting preset.</summary>
        public static WindowSizePreset ResolveFittingSizePreset(
            PixelRect workArea,
            WindowSizePreset preferred,
            int marginPixels = WorkAreaMarginPixels) =>
            SizePresetFits(workArea, preferred, marginPixels)
                ? preferred
                : LargestFittingSizePreset(workArea, marginPixels);

        /// <summary>
        /// Picks the monitor bounds that overlap <paramref name="windowRect"/> the most; ties are
        /// broken by nearest center distance. Falls back to the nearest monitor by center distance
        /// when there is no overlap at all. Pure helper for platforms without a native "nearest
        /// monitor" OS query (e.g. enumerating NSScreen.screens on macOS).
        /// </summary>
        public static int SelectMostOverlappingIndex(PixelRect windowRect, IReadOnlyList<PixelRect> monitorBounds)
        {
            if (monitorBounds == null)
            {
                throw new ArgumentNullException(nameof(monitorBounds));
            }

            if (monitorBounds.Count == 0)
            {
                throw new ArgumentException("monitorBounds must not be empty.", nameof(monitorBounds));
            }

            var bestIndex = 0;
            var bestOverlap = -1L;
            var bestDistance = long.MaxValue;

            for (var i = 0; i < monitorBounds.Count; i++)
            {
                var bounds = monitorBounds[i];
                var overlap = OverlapArea(windowRect, bounds);
                var distance = CenterDistanceSquared(windowRect, bounds);
                if (overlap > bestOverlap || (overlap == bestOverlap && distance < bestDistance))
                {
                    bestOverlap = overlap;
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static long OverlapArea(PixelRect a, PixelRect b)
        {
            var overlapWidth = Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X));
            var overlapHeight = Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y));
            return (long)overlapWidth * overlapHeight;
        }

        private static long CenterDistanceSquared(PixelRect a, PixelRect b)
        {
            var ax = a.X + a.Width / 2L;
            var ay = a.Y + a.Height / 2L;
            var bx = b.X + b.Width / 2L;
            var by = b.Y + b.Height / 2L;
            var dx = ax - bx;
            var dy = ay - by;
            return (dx * dx) + (dy * dy);
        }

        private static bool FitsWithMargin(PixelRect workArea, PixelSize size, int marginPixels) =>
            size.Width + (marginPixels * 2) <= workArea.Width &&
            size.Height + (marginPixels * 2) <= workArea.Height;

        private static int CenterAxis(int origin, int originSize, int targetSize) =>
            origin + ((originSize - targetSize) / 2);

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        private static void ValidateWorkArea(PixelRect workArea)
        {
            if (workArea.Width <= 0 || workArea.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(workArea));
            }
        }

        private static void ValidateClientSize(PixelSize clientSize)
        {
            if (clientSize.Width <= 0 || clientSize.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(clientSize));
            }
        }

        private static void ValidateMargin(int marginPixels)
        {
            if (marginPixels < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(marginPixels));
            }
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
