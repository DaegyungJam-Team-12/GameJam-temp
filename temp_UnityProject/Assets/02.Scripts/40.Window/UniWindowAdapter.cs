#nullable enable

using System;
using UnityEngine;

namespace Icebreaker.Window
{
    public sealed class UniWindowAdapter : INativeWindow
    {
        private readonly Kirurobo.UniWindowController controller;
        private readonly int coordinateSpaceBottom;
        private float appliedWindowHeight;

        public UniWindowAdapter(
            Kirurobo.UniWindowController controller,
            int coordinateSpaceBottom)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));

            if (coordinateSpaceBottom <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(coordinateSpaceBottom));
            }

            this.coordinateSpaceBottom = coordinateSpaceBottom;
        }

        public PixelSize ClientSize
        {
            get => ToPixelSize(controller.clientSize);
            set
            {
                if (value.Width <= 0 || value.Height <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                // This UniWindow revision exposes clientSize as read-only. Preserve the current
                // non-client frame delta when setting windowSize so the requested client pixels
                // remain unscaled. In the P0 borderless player the delta is zero.
                var windowSize = controller.windowSize;
                var clientSize = controller.clientSize;
                var frameWidth = clientSize.x > 0f
                    ? Mathf.Max(0f, windowSize.x - clientSize.x)
                    : 0f;
                var frameHeight = clientSize.y > 0f
                    ? Mathf.Max(0f, windowSize.y - clientSize.y)
                    : 0f;
                var targetWindowSize = new Vector2(
                    value.Width + frameWidth,
                    value.Height + frameHeight);

                controller.windowSize = targetWindowSize;
                appliedWindowHeight = targetWindowSize.y;
            }
        }

        public PixelPoint Position
        {
            get
            {
                var pluginPosition = controller.windowPosition;
                return new PixelPoint(
                    Mathf.RoundToInt(pluginPosition.x),
                    Mathf.RoundToInt(coordinateSpaceBottom - pluginPosition.y - WindowHeight));
            }
            set
            {
                // WindowLayout uses top-left/y-down coordinates. UniWindowController uses the
                // main monitor's lower-left origin, y-up, and the window's lower-left corner.
                controller.windowPosition = new Vector2(
                    value.X,
                    coordinateSpaceBottom - value.Y - WindowHeight);
            }
        }

        public bool IsTopmost
        {
            get => controller.isTopmost;
            set => controller.isTopmost = value;
        }

        public void Focus() => controller.Focus();

        private float WindowHeight => appliedWindowHeight > 0f
            ? appliedWindowHeight
            : controller.windowSize.y;

        private static PixelSize ToPixelSize(Vector2 value) => new(
            Mathf.RoundToInt(value.x),
            Mathf.RoundToInt(value.y));
    }
}
