#nullable enable

using System;
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
        [SerializeField] private Button? purchaseButton;
        [SerializeField] private TMP_Text? purchaseButtonText;

        public event Action PurchaseClicked = delegate { };

        private void OnEnable()
        {
            if (purchaseButton != null)
            {
                purchaseButton.onClick.AddListener(HandlePurchaseClicked);
            }
        }

        private void OnDisable()
        {
            if (purchaseButton != null)
            {
                purchaseButton.onClick.RemoveListener(HandlePurchaseClicked);
            }
        }

        public void Show(
            MaintenancePurchaseStepViewData data,
            Vector2 desiredTopLeft,
            Rect overlayRect)
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

            ConfigurePurchaseButton(data);

            gameObject.SetActive(true);
            var rect = (RectTransform)transform;
            rect.anchoredPosition = MaintenanceTreeViewportMath.ClampTooltipTopLeft(
                desiredTopLeft,
                rect.rect.size,
                overlayRect,
                16f);
        }

        public void Hide() => gameObject.SetActive(false);

        private void HandlePurchaseClicked() => PurchaseClicked();

        private void ConfigurePurchaseButton(MaintenancePurchaseStepViewData data)
        {
            if (purchaseButton != null)
            {
                purchaseButton.interactable = data.CanPurchase;
            }

            if (purchaseButtonText == null)
            {
                return;
            }

            purchaseButtonText.text = data.PurchaseState switch
            {
                MaintenanceStepPurchaseState.Purchased => "구매 완료",
                MaintenanceStepPurchaseState.Available when data.CanPurchase => "구매",
                MaintenanceStepPurchaseState.Available => "자금 부족",
                _ when data.Visibility == MaintenanceStepVisibility.Preview => "미리보기",
                _ => "잠김"
            };
        }

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
