#nullable enable

using System;
using Icebreaker.Shared.State;

namespace Icebreaker.Window
{
    public sealed class WindowViewController
    {
        private readonly INativeWindow nativeWindow;

        public WindowViewController(INativeWindow nativeWindow)
        {
            this.nativeWindow = nativeWindow ?? throw new ArgumentNullException(nameof(nativeWindow));
        }

        public PixelRect ApplyPhase(GamePhase phase, PixelRect workArea) =>
            ApplyView(WindowLayout.ViewForPhase(phase), workArea);

        public PixelRect ApplyView(WindowView view, PixelRect workArea)
        {
            var clientSize = WindowLayout.ClientSizeForView(view);
            var targetRect = WindowLayout.Calculate(workArea, clientSize);
            return ApplyRect(clientSize, targetRect);
        }

        /// <summary>
        /// Applies an already-resolved client size and target rect (e.g. from a position/size
        /// preset or an expand/collapse anchor calculation) directly, bypassing the default
        /// bottom-center placement.
        /// </summary>
        public PixelRect ApplyRect(PixelSize clientSize, PixelRect targetRect)
        {
            nativeWindow.ClientSize = clientSize;
            nativeWindow.Position = new PixelPoint(targetRect.X, targetRect.Y);
            nativeWindow.IsTopmost = true;

            return targetRect;
        }
    }
}
