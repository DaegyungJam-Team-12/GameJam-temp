#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Events;
using TMPro;
using UnityEngine;

namespace Icebreaker.UI.Management
{
    /// <summary>Queues and plays each destination arrival overlay exactly once.</summary>
    public sealed class ArrivalOverlayView : MonoBehaviour
    {
        public const float DurationSeconds = 1.5f;

        [SerializeField] private CanvasGroup? canvasGroup;
        [SerializeField] private TMP_Text? destinationText;
        [SerializeField] private TMP_Text? statusText;

        private readonly HashSet<string> acceptedDestinationIds = new(StringComparer.Ordinal);
        private readonly Queue<ArrivalPresentationRequested> queuedArrivals = new();

        private ArrivalPresentationRequested currentArrival;
        private float elapsed;

        public event Action<string> PresentationCompleted = delegate { };

        public bool IsPlaying { get; private set; }

        public string LastCompletedArrivalId { get; private set; } = string.Empty;

        private void Update()
        {
            if (IsPlaying)
            {
                Advance(Time.unscaledDeltaTime);
            }
        }

        public bool Present(ArrivalPresentationRequested request)
        {
            if (!acceptedDestinationIds.Add(request.DestinationId))
            {
                return false;
            }

            queuedArrivals.Enqueue(request);
            StartNextIfNeeded();
            return true;
        }

        public void AdvanceForValidation(float unscaledDeltaTime) => Advance(unscaledDeltaTime);

        private void StartNextIfNeeded()
        {
            if (IsPlaying || queuedArrivals.Count == 0)
            {
                return;
            }

            currentArrival = queuedArrivals.Dequeue();
            elapsed = 0f;
            IsPlaying = true;
            SetText(destinationText, currentArrival.DestinationDisplayName);
            SetText(statusText, "보급 항로 연결 완료");
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        private void Advance(float unscaledDeltaTime)
        {
            if (!IsPlaying)
            {
                return;
            }

            elapsed += Mathf.Max(0f, unscaledDeltaTime);
            if (canvasGroup != null)
            {
                var fadeIn = Mathf.Clamp01(elapsed / 0.2f);
                var fadeOut = Mathf.Clamp01((DurationSeconds - elapsed) / 0.3f);
                canvasGroup.alpha = Mathf.Min(fadeIn, fadeOut);
            }

            if (elapsed < DurationSeconds)
            {
                return;
            }

            IsPlaying = false;
            gameObject.SetActive(false);
            LastCompletedArrivalId = currentArrival.DestinationId;
            PresentationCompleted(currentArrival.DestinationId);
            StartNextIfNeeded();
        }

        private static void SetText(TMP_Text? target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
