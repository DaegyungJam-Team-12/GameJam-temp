#nullable enable

namespace Icebreaker.Window
{
    public interface INativeWindow
    {
        PixelSize ClientSize { get; set; }

        PixelPoint Position { get; set; }

        bool IsTopmost { get; set; }

        void Focus();
    }
}
