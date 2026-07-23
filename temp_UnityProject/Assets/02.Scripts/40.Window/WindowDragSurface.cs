#nullable enable

using UnityEngine;
using UnityEngine.EventSystems;

namespace Icebreaker.Window
{
    /// <summary>
    /// Sits behind the collapsed bar's buttons/tabs/slots as an invisible, full-bleed background.
    /// Dragging this surface moves the OS window; dragging never starts when the pointer-down
    /// lands on a button/tab/slot, because Unity's event system resolves drag/click handlers
    /// against the single topmost raycast hit and those controls have their own hit areas
    /// rendered above this surface. Position is only persisted once, when the drag ends.
    /// </summary>
    public sealed class WindowDragSurface : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private bool dragging;

        public void OnBeginDrag(PointerEventData eventData)
        {
            WindowBootstrap.Instance?.BeginDrag();
            dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            // Unity pointer delta is in screen pixels, y-up; native window space is y-down.
            WindowBootstrap.Instance?.DragBy(
                Mathf.RoundToInt(eventData.delta.x),
                Mathf.RoundToInt(-eventData.delta.y));
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            WindowBootstrap.Instance?.EndDrag();
        }
    }
}
