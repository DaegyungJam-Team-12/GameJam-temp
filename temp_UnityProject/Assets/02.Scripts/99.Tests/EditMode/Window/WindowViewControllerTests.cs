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
            Assert.That(nativeWindow.Position.X, Is.EqualTo(952));
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
            Assert.That(nativeWindow.ClientSize.Height, Is.EqualTo(72));
            Assert.That(nativeWindow.Position.X, Is.EqualTo(728));
            Assert.That(nativeWindow.Position.Y, Is.EqualTo(744));
            Assert.That(nativeWindow.IsTopmost, Is.True);
            Assert.That(nativeWindow.ClientSizeSetCount, Is.EqualTo(1));
            Assert.That(nativeWindow.PositionSetCount, Is.EqualTo(1));
            Assert.That(nativeWindow.TopmostSetCount, Is.EqualTo(1));
            Assert.That(nativeWindow.FocusCallCount, Is.Zero);
            Assert.That(result.Bottom, Is.EqualTo(816));
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
