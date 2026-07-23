#nullable enable

using Icebreaker.Shared.Maintenance;
using NUnit.Framework;
using UnityEngine;

namespace Icebreaker.Shared.Tests
{
    public sealed class MaintenanceTreeViewportMathTests
    {
        [Test]
        public void ApplyScroll_UsesPointOneStepsAndClampsToLockedZoomRange()
        {
            Assert.That(MaintenanceTreeViewportMath.ApplyScroll(1f, 1f), Is.EqualTo(1.1f).Within(0.0001f));
            Assert.That(MaintenanceTreeViewportMath.ApplyScroll(1f, -1f), Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(MaintenanceTreeViewportMath.ApplyScroll(1.25f, 1f), Is.EqualTo(1.25f));
            Assert.That(MaintenanceTreeViewportMath.ApplyScroll(0.8f, -1f), Is.EqualTo(0.8f));
        }

        [Test]
        public void KeyboardPanDirection_MapsWUpAndSDownWithoutChangingHorizontalControls()
        {
            Assert.That(
                MaintenanceTreeViewportMath.KeyboardPanDirection(false, false, true, false),
                Is.EqualTo(Vector2.down));
            Assert.That(
                MaintenanceTreeViewportMath.KeyboardPanDirection(false, false, false, true),
                Is.EqualTo(Vector2.up));
            Assert.That(
                MaintenanceTreeViewportMath.KeyboardPanDirection(true, false, false, false),
                Is.EqualTo(Vector2.right));
            Assert.That(
                MaintenanceTreeViewportMath.KeyboardPanDirection(false, true, false, false),
                Is.EqualTo(Vector2.left));
        }

        [Test]
        public void KeepPointerStable_PreservesTheSameContentPointDuringZoom()
        {
            var currentPosition = new Vector2(-120f, 80f);
            var pointer = new Vector2(420f, -180f);
            const float oldZoom = 0.8f;
            const float newZoom = 1.2f;

            var nextPosition = MaintenanceTreeViewportMath.KeepPointerStable(
                currentPosition,
                oldZoom,
                newZoom,
                pointer);

            var oldContentPoint = (pointer - currentPosition) / oldZoom;
            var newContentPoint = (pointer - nextPosition) / newZoom;
            Assert.That(newContentPoint.x, Is.EqualTo(oldContentPoint.x).Within(0.0001f));
            Assert.That(newContentPoint.y, Is.EqualTo(oldContentPoint.y).Within(0.0001f));
        }

        [Test]
        public void ClampContentPosition_PreventsBlankSpaceAtEveryEdge()
        {
            var contentSize = new Vector2(1600f, 900f);
            var viewportSize = new Vector2(928f, 408f);

            Assert.That(
                MaintenanceTreeViewportMath.ClampContentPosition(
                    new Vector2(100f, -10f),
                    contentSize,
                    viewportSize,
                    0.8f),
                Is.EqualTo(Vector2.zero));
            Assert.That(
                MaintenanceTreeViewportMath.ClampContentPosition(
                    new Vector2(-1000f, 1000f),
                    contentSize,
                    viewportSize,
                    0.8f),
                Is.EqualTo(new Vector2(-352f, 312f)));
        }

        [Test]
        public void ClampKeyboardContentPosition_AddsOnlyBottomBarVerticalClearance()
        {
            var contentSize = new Vector2(1600f, 900f);
            var viewportSize = new Vector2(928f, 408f);

            Assert.That(
                MaintenanceTreeViewportMath.ClampKeyboardContentPosition(
                    new Vector2(-1000f, 1000f),
                    contentSize,
                    viewportSize,
                    0.8f),
                Is.EqualTo(new Vector2(-352f, 384f)));
            Assert.That(
                MaintenanceTreeViewportMath.ClampKeyboardContentPosition(
                    new Vector2(100f, -10f),
                    contentSize,
                    viewportSize,
                    0.8f),
                Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void PositionTooltip_PreservesPreferredVerticalOffsetForUnobstructedAndBottomAnchors()
        {
            var viewport = new Rect(-464f, -204f, 928f, 408f);
            var tooltipSize = new Vector2(300f, 184f);
            var offset = new Vector2(54f, 28f);

            var unobstructed = MaintenanceTreeViewportMath.PositionTooltip(
                Vector2.zero,
                tooltipSize,
                viewport,
                offset,
                16f);

            var bottomRight = MaintenanceTreeViewportMath.PositionTooltip(
                new Vector2(430f, -180f),
                tooltipSize,
                viewport,
                offset,
                16f);

            Assert.That(unobstructed.y, Is.EqualTo(28f));
            Assert.That(bottomRight, Is.EqualTo(new Vector2(76f, -152f)));
        }

        [Test]
        public void PositionTooltip_ClampsToEachHorizontalEdge()
        {
            var viewport = new Rect(-464f, -204f, 928f, 408f);

            var left = MaintenanceTreeViewportMath.PositionTooltip(
                new Vector2(-1000f, 1000f),
                new Vector2(300f, 184f),
                viewport,
                new Vector2(54f, 28f),
                16f);
            var right = MaintenanceTreeViewportMath.PositionTooltip(
                new Vector2(1000f, 1000f),
                new Vector2(300f, 184f),
                viewport,
                new Vector2(54f, 28f),
                16f);

            Assert.That(left.x, Is.EqualTo(-448f));
            Assert.That(right.x, Is.EqualTo(148f));
        }
    }
}
