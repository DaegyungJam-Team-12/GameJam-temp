#nullable enable

using UnityEngine;

namespace Icebreaker.Shared.Maintenance
{
    public static class MaintenanceTreeViewportMath
    {
        public const float MinimumZoom = 0.8f;
        public const float MaximumZoom = 1.25f;
        public const float ZoomStep = 0.1f;
        public const float ClickDragThresholdPixels = 8f;

        public static float ApplyScroll(float currentZoom, float scrollY)
        {
            if (Mathf.Approximately(scrollY, 0f))
            {
                return Mathf.Clamp(currentZoom, MinimumZoom, MaximumZoom);
            }

            var direction = scrollY > 0f ? 1f : -1f;
            return Mathf.Clamp(
                currentZoom + direction * ZoomStep,
                MinimumZoom,
                MaximumZoom);
        }

        public static Vector2 KeepPointerStable(
            Vector2 currentContentPosition,
            float oldZoom,
            float newZoom,
            Vector2 pointerInViewport)
        {
            var contentPoint = (pointerInViewport - currentContentPosition) / oldZoom;
            return pointerInViewport - contentPoint * newZoom;
        }

        public static Vector2 ClampContentPosition(
            Vector2 contentPosition,
            Vector2 contentSize,
            Vector2 viewportSize,
            float zoom)
        {
            var scaledSize = contentSize * zoom;
            var minimumX = Mathf.Min(0f, viewportSize.x - scaledSize.x);
            var maximumY = Mathf.Max(0f, scaledSize.y - viewportSize.y);
            return new Vector2(
                Mathf.Clamp(contentPosition.x, minimumX, 0f),
                Mathf.Clamp(contentPosition.y, 0f, maximumY));
        }

        public static Vector2 ClampTooltipTopLeft(
            Vector2 desiredTopLeft,
            Vector2 tooltipSize,
            Rect overlayRect,
            float padding)
        {
            return new Vector2(
                Mathf.Clamp(
                    desiredTopLeft.x,
                    overlayRect.xMin + padding,
                    overlayRect.xMax - tooltipSize.x - padding),
                Mathf.Clamp(
                    desiredTopLeft.y,
                    overlayRect.yMin + tooltipSize.y + padding,
                    overlayRect.yMax - padding));
        }
    }
}
