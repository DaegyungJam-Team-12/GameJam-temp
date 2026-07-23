#nullable enable

using System.Collections.Generic;
using Icebreaker.Shared.State;
using NUnit.Framework;

namespace Icebreaker.Window.Tests
{
    public sealed class WindowLayoutTests
    {
        [Test]
        public void Windows100Percent_PlacesBothViewsAtBottomCenterMargin()
        {
            var workArea = new PixelRect(0, 0, 1920, 1040);

            var collapsed = WindowLayout.Calculate(
                workArea,
                WindowLayout.ClientSizeForView(WindowView.Collapsed));
            var expanded = WindowLayout.Calculate(
                workArea,
                WindowLayout.ClientSizeForView(WindowView.Expanded));

            AssertRect(collapsed, 560, 960, 800, 72);
            AssertRect(expanded, 480, 492, 960, 540);
            Assert.That(collapsed.Bottom, Is.EqualTo(1032));
            Assert.That(expanded.Bottom, Is.EqualTo(collapsed.Bottom));
            Assert.That(expanded.X + (expanded.Width / 2), Is.EqualTo(collapsed.X + (collapsed.Width / 2)));
            Assert.That(expanded.X, Is.LessThan(collapsed.X));
            Assert.That(expanded.Y, Is.LessThan(collapsed.Y));
        }

        [Test]
        public void Windows125Percent_PlacesBothViewsAtBottomCenterMargin()
        {
            var workArea = new PixelRect(0, 0, 1536, 824);

            var collapsed = WindowLayout.Calculate(
                workArea,
                WindowLayout.ClientSizeForView(WindowView.Collapsed));
            var expanded = WindowLayout.Calculate(
                workArea,
                WindowLayout.ClientSizeForView(WindowView.Expanded));

            AssertRect(collapsed, 368, 744, 800, 72);
            AssertRect(expanded, 288, 276, 960, 540);
            Assert.That(collapsed.Bottom, Is.EqualTo(816));
            Assert.That(expanded.Bottom, Is.EqualTo(collapsed.Bottom));
            Assert.That(expanded.X + (expanded.Width / 2), Is.EqualTo(collapsed.X + (collapsed.Width / 2)));
        }

        [Test]
        public void ExpandedNearWorkAreaEdge_ShiftsFullyInside()
        {
            var workArea = new PixelRect(100, 50, 964, 544);

            var expanded = WindowLayout.Calculate(
                workArea,
                WindowLayout.ClientSizeForView(WindowView.Expanded));

            AssertRect(expanded, 102, 50, 960, 540);
            Assert.That(expanded.X, Is.GreaterThanOrEqualTo(workArea.X));
            Assert.That(expanded.Y, Is.GreaterThanOrEqualTo(workArea.Y));
            Assert.That(expanded.Right, Is.LessThanOrEqualTo(workArea.Right));
            Assert.That(expanded.Bottom, Is.LessThanOrEqualTo(workArea.Bottom));
        }

        [Test]
        public void WindowLargerThanWorkArea_AlignsToWorkAreaOrigin()
        {
            var workArea = new PixelRect(100, 50, 500, 300);

            var expanded = WindowLayout.Calculate(
                workArea,
                WindowLayout.ClientSizeForView(WindowView.Expanded));

            AssertRect(expanded, 100, 50, 960, 540);
        }

        [TestCase(GamePhase.Traveling, WindowView.Collapsed)]
        [TestCase(GamePhase.Ready, WindowView.Collapsed)]
        [TestCase(GamePhase.Countdown, WindowView.Expanded)]
        [TestCase(GamePhase.Playing, WindowView.Expanded)]
        [TestCase(GamePhase.StageEnding, WindowView.Expanded)]
        [TestCase(GamePhase.Settlement, WindowView.Expanded)]
        [TestCase(GamePhase.Arrival, WindowView.Expanded)]
        [TestCase(GamePhase.Completed, WindowView.Collapsed)]
        public void ViewForPhase_MapsLockedPhaseTable(GamePhase phase, WindowView expected)
        {
            Assert.That(WindowLayout.ViewForPhase(phase), Is.EqualTo(expected));
        }

        [TestCase(GamePhase.Traveling, ManagementScreen.None, WindowView.Collapsed)]
        [TestCase(GamePhase.Traveling, ManagementScreen.Maintenance, WindowView.Expanded)]
        [TestCase(GamePhase.Traveling, ManagementScreen.Route, WindowView.Expanded)]
        [TestCase(GamePhase.Traveling, ManagementScreen.Settings, WindowView.Expanded)]
        [TestCase(GamePhase.Ready, ManagementScreen.Maintenance, WindowView.Expanded)]
        [TestCase(GamePhase.Ready, ManagementScreen.Route, WindowView.Expanded)]
        [TestCase(GamePhase.Ready, ManagementScreen.Settings, WindowView.Expanded)]
        [TestCase(GamePhase.Ready, ManagementScreen.None, WindowView.Collapsed)]
        [TestCase(GamePhase.Playing, ManagementScreen.None, WindowView.Expanded)]
        [TestCase(GamePhase.Completed, ManagementScreen.Settings, WindowView.Expanded)]
        public void ViewForState_ManagementScreenOverridesPhase(
            GamePhase phase,
            ManagementScreen managementScreen,
            WindowView expected)
        {
            Assert.That(
                WindowLayout.ViewForState(phase, managementScreen),
                Is.EqualTo(expected));
        }

        [Test]
        public void ResolvePresetRect_BottomLeft_UsesEightPixelMargin()
        {
            var workArea = new PixelRect(0, 0, 1920, 1040);

            var rect = WindowLayout.ResolvePresetRect(
                workArea,
                new PixelSize(800, 72),
                WindowPositionPreset.BottomLeft);

            AssertRect(rect, 8, 960, 800, 72);
        }

        [Test]
        public void ResolvePresetRect_BottomCenter_CentersHorizontally()
        {
            var workArea = new PixelRect(0, 0, 1920, 1040);

            var rect = WindowLayout.ResolvePresetRect(
                workArea,
                new PixelSize(800, 72),
                WindowPositionPreset.BottomCenter);

            AssertRect(rect, 560, 960, 800, 72);
        }

        [Test]
        public void ResolvePresetRect_BottomRight_UsesEightPixelMargin()
        {
            var workArea = new PixelRect(0, 0, 1920, 1040);

            var rect = WindowLayout.ResolvePresetRect(
                workArea,
                new PixelSize(800, 72),
                WindowPositionPreset.BottomRight);

            AssertRect(rect, 1112, 960, 800, 72);
        }

        [Test]
        public void NormalizedRoundTrip_RestoresSamePosition()
        {
            var workArea = new PixelRect(0, 0, 1920, 1040);
            var clientSize = new PixelSize(800, 72);
            var original = new PixelPoint(160, 200);

            var normalized = WindowLayout.PositionToNormalized(workArea, clientSize, original);
            var restored = WindowLayout.ResolveNormalizedRect(
                workArea,
                clientSize,
                normalized.NormalizedX,
                normalized.NormalizedY);

            Assert.That(restored.X, Is.EqualTo(original.X));
            Assert.That(restored.Y, Is.EqualTo(original.Y));
        }

        [Test]
        public void NormalizedRoundTrip_SecondaryMonitorWithNegativeOrigin()
        {
            var workArea = new PixelRect(-1920, -40, 1920, 1040);
            var clientSize = new PixelSize(800, 72);
            var original = new PixelPoint(-1200, 300);

            var normalized = WindowLayout.PositionToNormalized(workArea, clientSize, original);
            var restored = WindowLayout.ResolveNormalizedRect(
                workArea,
                clientSize,
                normalized.NormalizedX,
                normalized.NormalizedY);

            Assert.That(restored.X, Is.EqualTo(original.X));
            Assert.That(restored.Y, Is.EqualTo(original.Y));
            Assert.That(restored.X, Is.GreaterThanOrEqualTo(workArea.X));
            Assert.That(restored.Right, Is.LessThanOrEqualTo(workArea.Right));
        }

        [Test]
        public void ResolveNormalizedRect_ClampsOutOfRangeNormalizedValues()
        {
            var workArea = new PixelRect(0, 0, 1920, 1040);
            var clientSize = new PixelSize(800, 72);

            var rect = WindowLayout.ResolveNormalizedRect(workArea, clientSize, -5f, 5f);

            Assert.That(rect.X, Is.EqualTo(workArea.X));
            Assert.That(rect.Bottom, Is.EqualTo(workArea.Bottom));
        }

        [Test]
        public void ClampToWorkArea_PullsDraggedRectFullyInside()
        {
            var workArea = new PixelRect(0, 0, 1920, 1040);
            var draggedOffScreen = new PixelRect(-50, -30, 800, 72);

            var clamped = WindowLayout.ClampToWorkArea(draggedOffScreen, workArea);

            Assert.That(clamped.X, Is.EqualTo(0));
            Assert.That(clamped.Y, Is.EqualTo(0));
        }

        [TestCase(WindowSizePreset.Default, 800, 72, 960, 540)]
        [TestCase(WindowSizePreset.Large, 1000, 90, 1200, 675)]
        [TestCase(WindowSizePreset.ExtraLarge, 1200, 108, 1440, 810)]
        public void ClientSizeForPreset_MapsLockedSizeTable(
            WindowSizePreset preset,
            int collapsedWidth,
            int collapsedHeight,
            int expandedWidth,
            int expandedHeight)
        {
            var collapsed = WindowLayout.ClientSizeForPreset(WindowView.Collapsed, preset);
            var expanded = WindowLayout.ClientSizeForPreset(WindowView.Expanded, preset);

            Assert.That(collapsed.Width, Is.EqualTo(collapsedWidth));
            Assert.That(collapsed.Height, Is.EqualTo(collapsedHeight));
            Assert.That(expanded.Width, Is.EqualTo(expandedWidth));
            Assert.That(expanded.Height, Is.EqualTo(expandedHeight));
        }

        [Test]
        public void SizePresetFits_DisablesPresetsThatDoNotFitSmallWorkArea()
        {
            var smallWorkArea = new PixelRect(0, 0, 1024, 700);

            Assert.That(WindowLayout.SizePresetFits(smallWorkArea, WindowSizePreset.Default), Is.True);
            Assert.That(WindowLayout.SizePresetFits(smallWorkArea, WindowSizePreset.Large), Is.False);
            Assert.That(WindowLayout.SizePresetFits(smallWorkArea, WindowSizePreset.ExtraLarge), Is.False);
        }

        [Test]
        public void ResolveFittingSizePreset_FallsBackToLargestFittingPreset()
        {
            var smallWorkArea = new PixelRect(0, 0, 1024, 700);

            var resolved = WindowLayout.ResolveFittingSizePreset(smallWorkArea, WindowSizePreset.ExtraLarge);

            Assert.That(resolved, Is.EqualTo(WindowSizePreset.Default));
        }

        [Test]
        public void ResolveFittingSizePreset_KeepsPreferredWhenItFits()
        {
            var largeWorkArea = new PixelRect(0, 0, 1920, 1040);

            var resolved = WindowLayout.ResolveFittingSizePreset(largeWorkArea, WindowSizePreset.ExtraLarge);

            Assert.That(resolved, Is.EqualTo(WindowSizePreset.ExtraLarge));
        }

        [Test]
        public void DetermineEdgeAnchor_BottomBarExpandsUpward()
        {
            var workArea = new PixelRect(0, 0, 1920, 1040);
            var collapsedRect = WindowLayout.ResolvePresetRect(
                workArea,
                new PixelSize(800, 72),
                WindowPositionPreset.BottomCenter);

            var anchor = WindowLayout.DetermineEdgeAnchor(collapsedRect, workArea);

            Assert.That(anchor, Is.EqualTo(WindowEdgeAnchor.Bottom));

            var expandedRect = WindowLayout.CalculateExpandedRectFromAnchor(
                collapsedRect,
                new PixelSize(960, 540),
                anchor,
                workArea);

            Assert.That(expandedRect.Bottom, Is.EqualTo(collapsedRect.Bottom));
            Assert.That(expandedRect.Y, Is.LessThan(collapsedRect.Y));
        }

        [Test]
        public void DetermineEdgeAnchor_LeftBarExpandsRightward()
        {
            var workArea = new PixelRect(0, 0, 1920, 1040);
            var collapsedRect = new PixelRect(workArea.X + 8, 400, 800, 72);

            var anchor = WindowLayout.DetermineEdgeAnchor(collapsedRect, workArea);

            Assert.That(anchor, Is.EqualTo(WindowEdgeAnchor.Left));

            var expandedRect = WindowLayout.CalculateExpandedRectFromAnchor(
                collapsedRect,
                new PixelSize(960, 540),
                anchor,
                workArea);

            Assert.That(expandedRect.X, Is.EqualTo(collapsedRect.X));
            Assert.That(expandedRect.Right, Is.GreaterThan(collapsedRect.Right));
        }

        [Test]
        public void DetermineEdgeAnchor_RightBarExpandsLeftward()
        {
            var workArea = new PixelRect(0, 0, 1920, 1040);
            var collapsedRect = new PixelRect(workArea.Right - 8 - 800, 400, 800, 72);

            var anchor = WindowLayout.DetermineEdgeAnchor(collapsedRect, workArea);

            Assert.That(anchor, Is.EqualTo(WindowEdgeAnchor.Right));

            var expandedRect = WindowLayout.CalculateExpandedRectFromAnchor(
                collapsedRect,
                new PixelSize(960, 540),
                anchor,
                workArea);

            Assert.That(expandedRect.Right, Is.EqualTo(collapsedRect.Right));
            Assert.That(expandedRect.X, Is.LessThan(collapsedRect.X));
        }

        [Test]
        public void DetermineEdgeAnchor_TopBarExpandsDownward()
        {
            var workArea = new PixelRect(0, 0, 1920, 1040);
            var collapsedRect = new PixelRect(560, workArea.Y + 8, 800, 72);

            var anchor = WindowLayout.DetermineEdgeAnchor(collapsedRect, workArea);

            Assert.That(anchor, Is.EqualTo(WindowEdgeAnchor.Top));

            var expandedRect = WindowLayout.CalculateExpandedRectFromAnchor(
                collapsedRect,
                new PixelSize(960, 540),
                anchor,
                workArea);

            Assert.That(expandedRect.Y, Is.EqualTo(collapsedRect.Y));
            Assert.That(expandedRect.Bottom, Is.GreaterThan(collapsedRect.Bottom));
        }

        [Test]
        public void DetermineEdgeAnchor_MiddleOfScreenKeepsCenterPoint()
        {
            var workArea = new PixelRect(0, 0, 1920, 1040);
            var collapsedRect = new PixelRect(560, 484, 800, 72);

            var anchor = WindowLayout.DetermineEdgeAnchor(collapsedRect, workArea);

            Assert.That(anchor, Is.EqualTo(WindowEdgeAnchor.Center));

            var expandedRect = WindowLayout.CalculateExpandedRectFromAnchor(
                collapsedRect,
                new PixelSize(960, 540),
                anchor,
                workArea);

            var collapsedCenterX = collapsedRect.X + (collapsedRect.Width / 2);
            var collapsedCenterY = collapsedRect.Y + (collapsedRect.Height / 2);
            var expandedCenterX = expandedRect.X + (expandedRect.Width / 2);
            var expandedCenterY = expandedRect.Y + (expandedRect.Height / 2);
            Assert.That(expandedCenterX, Is.EqualTo(collapsedCenterX));
            Assert.That(expandedCenterY, Is.EqualTo(collapsedCenterY));
        }

        [Test]
        public void CalculateExpandedRectFromAnchor_ClampsInsideWorkAreaWhenAnchoredNearEdge()
        {
            var workArea = new PixelRect(0, 0, 1000, 600);
            // Collapsed bar flush against the left edge: centering the wider expanded view on it
            // would push the raw X negative, so the clamp must pull it back to the work area edge.
            var collapsedRect = new PixelRect(0, 520, 800, 72);

            var expandedRect = WindowLayout.CalculateExpandedRectFromAnchor(
                collapsedRect,
                new PixelSize(960, 540),
                WindowEdgeAnchor.Bottom,
                workArea);

            Assert.That(expandedRect.X, Is.EqualTo(0));
            Assert.That(expandedRect.X, Is.GreaterThanOrEqualTo(workArea.X));
            Assert.That(expandedRect.Y, Is.GreaterThanOrEqualTo(workArea.Y));
            Assert.That(expandedRect.Right, Is.LessThanOrEqualTo(workArea.Right));
            Assert.That(expandedRect.Bottom, Is.LessThanOrEqualTo(workArea.Bottom));
        }

        [Test]
        public void SelectMostOverlappingIndex_PicksSecondaryMonitorWithNegativeOrigin()
        {
            var primary = new PixelRect(0, 0, 1920, 1080);
            var secondary = new PixelRect(-1920, -40, 1920, 1040);
            var monitors = new List<PixelRect> { primary, secondary };

            var windowOnSecondary = new PixelRect(-1200, 300, 800, 72);

            var index = WindowLayout.SelectMostOverlappingIndex(windowOnSecondary, monitors);

            Assert.That(index, Is.EqualTo(1));
        }

        [Test]
        public void SelectMostOverlappingIndex_FallsBackToNearestWhenNoOverlap()
        {
            var primary = new PixelRect(0, 0, 1920, 1080);
            var secondary = new PixelRect(-1920, -40, 1920, 1040);
            var monitors = new List<PixelRect> { primary, secondary };

            // Off both monitors, but closer (by center distance) to the secondary on the left.
            var windowFarLeft = new PixelRect(-3000, 300, 800, 72);

            var index = WindowLayout.SelectMostOverlappingIndex(windowFarLeft, monitors);

            Assert.That(index, Is.EqualTo(1));
        }

        private static void AssertRect(
            PixelRect actual,
            int x,
            int y,
            int width,
            int height)
        {
            Assert.That(actual.X, Is.EqualTo(x));
            Assert.That(actual.Y, Is.EqualTo(y));
            Assert.That(actual.Width, Is.EqualTo(width));
            Assert.That(actual.Height, Is.EqualTo(height));
        }
    }
}
