#nullable enable

using Icebreaker.Shared.State;
using NUnit.Framework;

namespace Icebreaker.Window.Tests
{
    public sealed class WindowLayoutTests
    {
        [Test]
        public void Windows100Percent_PlacesBothViewsAtBottomRightMargin()
        {
            var workArea = new PixelRect(0, 0, 1920, 1040);

            var collapsed = WindowLayout.Calculate(
                workArea,
                WindowLayout.ClientSizeForView(WindowView.Collapsed));
            var expanded = WindowLayout.Calculate(
                workArea,
                WindowLayout.ClientSizeForView(WindowView.Expanded));

            AssertRect(collapsed, 1112, 960, 800, 72);
            AssertRect(expanded, 952, 492, 960, 540);
            Assert.That(collapsed.Right, Is.EqualTo(1912));
            Assert.That(collapsed.Bottom, Is.EqualTo(1032));
            Assert.That(expanded.Right, Is.EqualTo(collapsed.Right));
            Assert.That(expanded.Bottom, Is.EqualTo(collapsed.Bottom));
            Assert.That(expanded.X, Is.LessThan(collapsed.X));
            Assert.That(expanded.Y, Is.LessThan(collapsed.Y));
        }

        [Test]
        public void Windows125Percent_PlacesBothViewsAtBottomRightMargin()
        {
            var workArea = new PixelRect(0, 0, 1536, 824);

            var collapsed = WindowLayout.Calculate(
                workArea,
                WindowLayout.ClientSizeForView(WindowView.Collapsed));
            var expanded = WindowLayout.Calculate(
                workArea,
                WindowLayout.ClientSizeForView(WindowView.Expanded));

            AssertRect(collapsed, 728, 744, 800, 72);
            AssertRect(expanded, 568, 276, 960, 540);
            Assert.That(collapsed.Right, Is.EqualTo(1528));
            Assert.That(collapsed.Bottom, Is.EqualTo(816));
            Assert.That(expanded.Right, Is.EqualTo(collapsed.Right));
            Assert.That(expanded.Bottom, Is.EqualTo(collapsed.Bottom));
            Assert.That(expanded.X, Is.LessThan(collapsed.X));
            Assert.That(expanded.Y, Is.LessThan(collapsed.Y));
        }

        [Test]
        public void ExpandedNearWorkAreaEdge_ShiftsFullyInside()
        {
            var workArea = new PixelRect(100, 50, 964, 544);

            var expanded = WindowLayout.Calculate(
                workArea,
                WindowLayout.ClientSizeForView(WindowView.Expanded));

            AssertRect(expanded, 100, 50, 960, 540);
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
