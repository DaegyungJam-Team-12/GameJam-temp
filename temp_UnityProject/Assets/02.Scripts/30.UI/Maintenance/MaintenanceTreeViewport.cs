#nullable enable

using System;
using Icebreaker.Shared.Maintenance;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Icebreaker.UI.Maintenance
{
    public sealed class MaintenanceTreeViewport : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IDragHandler,
        IScrollHandler
    {
        [SerializeField] private RectTransform? content;
        [SerializeField] private Canvas? canvas;
        [SerializeField] private Vector2 initialContentPosition = new Vector2(0f, 56f);
        [SerializeField] private float initialZoom = MaintenanceTreeViewportMath.MinimumZoom;
        [SerializeField] private float keyboardPanPixelsPerSecond = 320f;

        private RectTransform? viewportRect;
        private bool initialized;
        private bool pointerActive;
        private int activePointerId;
        private Vector2 pointerDownScreenPosition;
        private string? pointerDownStepId;
        private string? hoveredStepId;

        public event Action<string> StepDoubleClicked = delegate { };
        public event Action<string> StepHovered = delegate { };
        public event Action<string> StepHoverExited = delegate { };

        public float CurrentZoom => content != null
            ? content.localScale.x
            : MaintenanceTreeViewportMath.MinimumZoom;

        public bool IsPointerActive => pointerActive;

        private void Awake()
        {
            viewportRect = (RectTransform)transform;
            canvas ??= GetComponentInParent<Canvas>();
        }

        private void OnEnable()
        {
            viewportRect ??= (RectTransform)transform;
            canvas ??= GetComponentInParent<Canvas>();
            if (content == null)
            {
                return;
            }

            if (!initialized)
            {
                content.anchoredPosition = initialContentPosition;
                content.localScale = Vector3.one * Mathf.Clamp(
                    initialZoom,
                    MaintenanceTreeViewportMath.MinimumZoom,
                    MaintenanceTreeViewportMath.MaximumZoom);
                initialized = true;
            }

            ClampNow();
        }

        private void OnDisable()
        {
            CancelPointer();
            ClearHover();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                CancelPointer();
                ClearHover();
            }
        }

        private void Update()
        {
            if (content == null || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                ResetView();
                return;
            }

            var direction = MaintenanceTreeViewportMath.KeyboardPanDirection(
                Keyboard.current.aKey.isPressed,
                Keyboard.current.dKey.isPressed,
                Keyboard.current.wKey.isPressed,
                Keyboard.current.sKey.isPressed);
            if (direction.sqrMagnitude <= 0f)
            {
                return;
            }

            direction.Normalize();
            content.anchoredPosition +=
                direction * (keyboardPanPixelsPerSecond * Time.unscaledDeltaTime);
            ClampKeyboardPan();
        }

        public void OnPointerDown(PointerEventData eventData) =>
            ProcessPointerDown(eventData, null);

        public void OnDrag(PointerEventData eventData) => ProcessPointerDrag(eventData);

        public void OnPointerUp(PointerEventData eventData) =>
            ProcessPointerUp(eventData, null);

        public void OnScroll(PointerEventData eventData)
        {
            if (viewportRect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    viewportRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var pointerInViewport))
            {
                return;
            }

            ApplyZoomAtPointer(eventData.scrollDelta.y, pointerInViewport);
        }

        public void ProcessPointerEnter(string? stepId)
        {
            // Hover shows the tooltip immediately, but ignore hovers while the pointer is held
            // (dragging/panning) so panning across nodes doesn't hijack the selection.
            if (pointerActive || string.IsNullOrEmpty(stepId) ||
                string.Equals(hoveredStepId, stepId, StringComparison.Ordinal))
            {
                return;
            }

            hoveredStepId = stepId;
            StepHovered(stepId);
        }

        public void ProcessPointerExit(string? stepId)
        {
            if (pointerActive || string.IsNullOrEmpty(stepId) ||
                !string.Equals(hoveredStepId, stepId, StringComparison.Ordinal))
            {
                return;
            }

            hoveredStepId = null;
            StepHoverExited(stepId);
        }

        public void ProcessPointerDown(PointerEventData eventData, string? stepId)
        {
            if (pointerActive)
            {
                CancelPointer();
            }

            pointerActive = true;
            activePointerId = eventData.pointerId;
            pointerDownScreenPosition = eventData.position;
            pointerDownStepId = stepId;
        }

        public void ProcessPointerDrag(PointerEventData eventData)
        {
            if (!pointerActive || eventData.pointerId != activePointerId || content == null)
            {
                return;
            }

            var scaleFactor = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            content.anchoredPosition += eventData.delta / scaleFactor;
            ClampNow();
        }

        public void ProcessPointerUp(PointerEventData eventData, string? stepId)
        {
            if (!pointerActive)
            {
                return;
            }

            if (eventData.pointerId != activePointerId)
            {
                CancelPointer();
                return;
            }

            var movedPixels = Vector2.Distance(pointerDownScreenPosition, eventData.position);
            var clickedStepId = pointerDownStepId;
            var isDoubleClick = eventData.clickCount == 2;
            var isShortClick = movedPixels < MaintenanceTreeViewportMath.ClickDragThresholdPixels;
            var isStepClick = isShortClick &&
                          !string.IsNullOrEmpty(clickedStepId) &&
                          string.Equals(clickedStepId, stepId, StringComparison.Ordinal);
            CancelPointer();
            if (!isShortClick)
            {
                ClearHover();
            }

            if (isStepClick && isDoubleClick)
            {
                StepDoubleClicked(clickedStepId!);
            }
        }

        public void ApplyZoomAtPointer(float scrollY, Vector2 pointerInViewport)
        {
            if (content == null)
            {
                return;
            }

            var oldZoom = CurrentZoom;
            var newZoom = MaintenanceTreeViewportMath.ApplyScroll(oldZoom, scrollY);
            if (Mathf.Approximately(oldZoom, newZoom))
            {
                return;
            }

            content.anchoredPosition = MaintenanceTreeViewportMath.KeepPointerStable(
                content.anchoredPosition,
                oldZoom,
                newZoom,
                pointerInViewport);
            content.localScale = Vector3.one * newZoom;
            ClampNow();
        }

        public void ResetView()
        {
            if (content == null)
            {
                return;
            }

            content.anchoredPosition = initialContentPosition;
            content.localScale = Vector3.one * Mathf.Clamp(
                initialZoom,
                MaintenanceTreeViewportMath.MinimumZoom,
                MaintenanceTreeViewportMath.MaximumZoom);
            ClampNow();
        }

        public void CancelPointer()
        {
            pointerActive = false;
            pointerDownStepId = null;
        }

        public void ClearHover()
        {
            if (hoveredStepId == null)
            {
                return;
            }

            var previousStepId = hoveredStepId;
            hoveredStepId = null;
            StepHoverExited(previousStepId);
        }

        public void ClampNow()
        {
            if (content == null)
            {
                return;
            }

            viewportRect ??= (RectTransform)transform;
            content.anchoredPosition = MaintenanceTreeViewportMath.ClampContentPosition(
                content.anchoredPosition,
                content.rect.size,
                viewportRect.rect.size,
                CurrentZoom);
        }

        private void ClampKeyboardPan()
        {
            if (content == null)
            {
                return;
            }

            viewportRect ??= (RectTransform)transform;
            content.anchoredPosition = MaintenanceTreeViewportMath.ClampKeyboardContentPosition(
                content.anchoredPosition,
                content.rect.size,
                viewportRect.rect.size,
                CurrentZoom);
        }
    }
}
