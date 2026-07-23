#nullable enable

using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.UI.Management
{
    /// <summary>Renders the read-only route status panel.</summary>
    public sealed class RouteStatusView : MonoBehaviour
    {
        [SerializeField] private TMP_Text? destinationNameText;
        [SerializeField] private TMP_Text? destinationProgressText;
        [SerializeField] private Image? destinationProgressFill;
        [SerializeField] private TMP_Text? cargoText;
        [SerializeField] private TMP_Text? completedDestinationsText;
        [SerializeField] private TMP_Text? upcomingDestinationsText;
        [SerializeField] private GameObject? completedBadge;

        public bool IsVisible => gameObject.activeInHierarchy;

        public RouteStatusViewData? CurrentData { get; private set; }

        public void Show()
        {
            SetVisible(true);
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        public void Render(RouteStatusViewData data)
        {
            CurrentData = data ?? throw new ArgumentNullException(nameof(data));
            SetText(destinationNameText, data.GameCompleted
                ? $"{data.CurrentDestinationName} · 운항 완료"
                : data.CurrentDestinationName);
            SetText(
                destinationProgressText,
                $"목적지 진행  {data.Progress.ToString("N0", CultureInfo.InvariantCulture)} / {data.Target.ToString("N0", CultureInfo.InvariantCulture)}");

            if (destinationProgressFill != null)
            {
                destinationProgressFill.fillAmount = data.Target <= 0
                    ? 0f
                    : Mathf.Clamp01((float)data.Progress / data.Target);
            }

            SetText(cargoText, $"운송 화물\n{data.CargoText}");
            SetText(completedDestinationsText, $"완료한 목적지\n{data.CompletedDestinationsText}");
            SetText(upcomingDestinationsText, $"이후 목적지\n{data.UpcomingDestinationsText}");
            SetActive(completedBadge, data.GameCompleted);
        }

        private static void SetText(TMP_Text? target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static void SetActive(GameObject? target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
