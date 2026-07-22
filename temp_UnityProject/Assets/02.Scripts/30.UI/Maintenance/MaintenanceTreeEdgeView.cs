#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.UI.Maintenance
{
    public sealed class MaintenanceTreeEdgeView : MonoBehaviour
    {
        [SerializeField] private Image? lineImage;

        public void Render(Vector2 start, Vector2 end, float thickness, Color color)
        {
            if (lineImage == null)
            {
                return;
            }

            var delta = end - start;
            var rect = lineImage.rectTransform;
            rect.anchoredPosition = (start + end) * 0.5f;
            rect.sizeDelta = new Vector2(delta.magnitude, thickness);
            rect.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            lineImage.color = color;
            lineImage.raycastTarget = false;
        }
    }
}
