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

        public event Action<string> StepClicked = delegate { };
        public event Action BackgroundClicked = delegate { };

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

        private void OnDisable() => CancelPointer();

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                CancelPointer();
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
            ClampNow();
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

        public void ProcessPointerDown(PointerEventData eventData, string? stepId)
        {
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
            if (!pointerActive || eventData.pointerId != activePointerId)
            {
                return;
            }

            var movedPixels = Vector2.Distance(pointerDownScreenPosition, eventData.position);
            var clickedStepId = pointerDownStepId;
            var isShortClick = movedPixels < MaintenanceTreeViewportMath.ClickDragThresholdPixels;
            var isStepClick = isShortClick &&
                          !string.IsNullOrEmpty(clickedStepId) &&
                          string.Equals(clickedStepId, stepId, StringComparison.Ordinal);
            var isBackgroundClick = isShortClick &&
                                    string.IsNullOrEmpty(clickedStepId) &&
                                    string.IsNullOrEmpty(stepId);
            CancelPointer();
            if (isStepClick)
            {
                StepClicked(clickedStepId!);
            }
            else if (isBackgroundClick)
            {
                BackgroundClicked();
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
    }
}
