#nullable enable

namespace Icebreaker.Window
{
    /// <summary>
    /// The work-area edge a collapsed window is nearest to, used to decide which direction
    /// the window should grow toward when expanding.
    /// </summary>
    public enum WindowEdgeAnchor
    {
        Bottom,
        Top,
        Left,
        Right,
        Center
    }
}
