#nullable enable

using NUnit.Framework;

namespace Icebreaker.Window.Tests
{
    public sealed class WindowViewControllerTests
    {
        [Test]
        public void ApplyExpanded_SetsSizePositionAndTopmost_WithoutFocus()
        {
            var nativeWindow = new FakeNativeWindow();
            var controller = new WindowViewController(nativeWindow);

            var result = controller.ApplyView(
                WindowView.Expanded,
                new PixelRect(0, 0, 1920, 1040));

            Assert.That(nativeWindow.ClientSize.Width, Is.EqualTo(960));
            Assert.That(nativeWindow.ClientSize.Height, Is.EqualTo(540));
            Assert.That(nativeWindow.Position.X, Is.EqualTo(480));
            Assert.That(nativeWindow.Position.Y, Is.EqualTo(492));
            Assert.That(nativeWindow.IsTopmost, Is.True);
            Assert.That(nativeWindow.ClientSizeSetCount, Is.EqualTo(1));
            Assert.That(nativeWindow.PositionSetCount, Is.EqualTo(1));
            Assert.That(nativeWindow.TopmostSetCount, Is.EqualTo(1));
            Assert.That(nativeWindow.FocusCallCount, Is.Zero);
            Assert.That(result.Bottom, Is.EqualTo(1032));
        }

        [Test]
        public void ApplyCollapsed_SetsSizePositionAndTopmost_WithoutFocus()
        {
            var nativeWindow = new FakeNativeWindow();
            var controller = new WindowViewController(nativeWindow);

            var result = controller.ApplyView(
                WindowView.Collapsed,
                new PixelRect(0, 0, 1536, 824));

            Assert.That(nativeWindow.ClientSize.Width, Is.EqualTo(800));
            Assert.That(nativeWindow.ClientSize.Height, Is.EqualTo(158));
            Assert.That(nativeWindow.Position.X, Is.EqualTo(368));
            Assert.That(nativeWindow.Position.Y, Is.EqualTo(658));
            Assert.That(nativeWindow.IsTopmost, Is.True);
            Assert.That(nativeWindow.ClientSizeSetCount, Is.EqualTo(1));
            Assert.That(nativeWindow.PositionSetCount, Is.EqualTo(1));
            Assert.That(nativeWindow.TopmostSetCount, Is.EqualTo(1));
            Assert.That(nativeWindow.FocusCallCount, Is.Zero);
            Assert.That(result.Bottom, Is.EqualTo(816));
        }

        [Test]
        public void ApplyRect_AppliesExplicitSizeAndRectDirectly()
        {
            var nativeWindow = new FakeNativeWindow();
            var controller = new WindowViewController(nativeWindow);

            var result = controller.ApplyRect(new PixelSize(1000, 90), new PixelRect(12, 34, 1000, 90));

            Assert.That(nativeWindow.ClientSize.Width, Is.EqualTo(1000));
            Assert.That(nativeWindow.ClientSize.Height, Is.EqualTo(90));
            Assert.That(nativeWindow.Position.X, Is.EqualTo(12));
            Assert.That(nativeWindow.Position.Y, Is.EqualTo(34));
            Assert.That(nativeWindow.IsTopmost, Is.True);
            Assert.That(result.Width, Is.EqualTo(1000));
            Assert.That(result.Height, Is.EqualTo(90));
        }

        private sealed class FakeNativeWindow : INativeWindow
        {
            private PixelSize clientSize;
            private PixelPoint position;
            private bool isTopmost;

            public int ClientSizeSetCount { get; private set; }

            public int PositionSetCount { get; private set; }

            public int TopmostSetCount { get; private set; }

            public int FocusCallCount { get; private set; }

            public PixelSize ClientSize
            {
                get => clientSize;
                set
                {
                    clientSize = value;
                    ClientSizeSetCount++;
                }
            }

            public PixelPoint Position
            {
                get => position;
                set
                {
                    position = value;
                    PositionSetCount++;
                }
            }

            public bool IsTopmost
            {
                get => isTopmost;
                set
                {
                    isTopmost = value;
                    TopmostSetCount++;
                }
            }

            public void Focus()
            {
                FocusCallCount++;
            }
        }
    }
}
