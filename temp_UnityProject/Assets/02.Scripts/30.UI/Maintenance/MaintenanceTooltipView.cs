#nullable enable

using System.Globalization;
using Icebreaker.Shared.Maintenance;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.UI.Maintenance
{
    public sealed class MaintenanceTooltipView : MonoBehaviour
    {
        [SerializeField] private TMP_Text? titleText;
        [SerializeField] private TMP_Text? effectText;
        [SerializeField] private TMP_Text? costText;
        [SerializeField] private TMP_Text? lockText;

        private void Awake()
        {
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }

        public void Show(
            MaintenancePurchaseStepViewData data,
            Vector2 anchor,
            Rect viewportBounds)
        {
            if (titleText != null)
            {
                titleText.text = $"{data.DisplayName} · {data.TargetLevel}/{data.MaxLevel}";
            }

            if (effectText != null)
            {
                effectText.text = data.EffectText +
                                  $"\n단계 {data.TargetLevel - 1} → {data.TargetLevel}";
            }

            if (costText != null)
            {
                costText.text = "가격 " + data.Cost.ToString("N0", CultureInfo.InvariantCulture);
            }

            if (lockText != null)
            {
                lockText.text = ResolveLockText(data);
            }

            gameObject.SetActive(true);
            var rect = (RectTransform)transform;
            rect.localScale = Vector3.one;
            rect.anchoredPosition = MaintenanceTreeViewportMath.PositionTooltip(
                anchor,
                rect.rect.size,
                viewportBounds,
                new Vector2(54f, 28f),
                16f);
        }

        public void Hide() => gameObject.SetActive(false);

        private static string ResolveLockText(MaintenancePurchaseStepViewData data)
        {
            if (data.PurchaseState == MaintenanceStepPurchaseState.Purchased)
            {
                return "✓ 구매 완료";
            }

            if (data.PurchaseState == MaintenanceStepPurchaseState.Available)
            {
                return data.CanAfford ? "구매 가능" : "! 정비 자금 부족";
            }

            return data.MissingRequirementIds.Count == 0
                ? "잠김"
                : "필요 정비: " + string.Join(", ", data.MissingRequirementIds);
        }
    }
}
