#nullable enable

using Icebreaker.Shared.Maintenance;
using Icebreaker.UI.Sandbox;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.UI.Maintenance
{
    public sealed class MaintenanceNodeView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup? canvasGroup;
        [SerializeField] private Image? frame;
        [SerializeField] private Image? icon;
        [SerializeField] private TMP_Text? idText;
        [SerializeField] private TMP_Text? nameText;
        [SerializeField] private TMP_Text? levelText;
        [SerializeField] private TMP_Text? statusText;
        [SerializeField] private TMP_Text? branchLabelText;

        public string StepId { get; private set; } = "";

        public void Render(
            MaintenancePurchaseStepViewData data,
            MaintenanceTreeNodeLayout layout,
            UiThemeAsset? theme)
        {
            StepId = data.StepId;
            var isHidden = data.Visibility == MaintenanceStepVisibility.Hidden;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = isHidden
                    ? 0f
                    : data.Visibility == MaintenanceStepVisibility.Preview ? 0.42f : 1f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(!isHidden);
            if (isHidden)
            {
                return;
            }

            var primary = theme != null ? theme.PrimaryText : Color.white;
            var panel = theme != null ? theme.Panel : new Color(0.05f, 0.13f, 0.2f, 1f);
            var success = theme != null ? theme.Success : new Color(0.4f, 0.83f, 0.73f, 1f);
            var action = theme != null ? theme.ActionAccent : new Color(0.95f, 0.6f, 0.24f, 1f);

            if (frame != null)
            {
                frame.rectTransform.sizeDelta = layout.VisualSize;
                frame.raycastTarget = false;
                frame.color = data.PurchaseState switch
                {
                    MaintenanceStepPurchaseState.Purchased => success,
                    MaintenanceStepPurchaseState.Available => data.CanAfford ? action : panel,
                    _ => panel
                };
            }

            if (icon != null)
            {
                icon.sprite = layout.Icon;
                icon.enabled = layout.Icon != null;
                icon.raycastTarget = false;
                icon.color = data.PurchaseState == MaintenanceStepPurchaseState.Locked
                    ? new Color(primary.r, primary.g, primary.b, 0.35f)
                    : primary;
            }

            SetText(idText, data.MaintenanceId, primary);
            SetText(nameText, data.DisplayName, primary);
            SetText(levelText, $"{data.TargetLevel}/{data.MaxLevel}", primary);
            SetText(statusText, ResolveStatus(data), ResolveStatusColor(data, primary, success, action));

            if (branchLabelText != null)
            {
                branchLabelText.text = layout.BranchLabel;
                branchLabelText.color = primary;
                branchLabelText.gameObject.SetActive(!string.IsNullOrEmpty(layout.BranchLabel));
            }
        }

        private static string ResolveStatus(MaintenancePurchaseStepViewData data)
        {
            if (data.Visibility == MaintenanceStepVisibility.Preview)
            {
                return "미리보기";
            }

            return data.PurchaseState switch
            {
                MaintenanceStepPurchaseState.Purchased => "✓ 구매됨",
                MaintenanceStepPurchaseState.Available when data.CanAfford => "구매 가능",
                MaintenanceStepPurchaseState.Available => "! 자금 부족",
                _ => "잠김"
            };
        }

        private static Color ResolveStatusColor(
            MaintenancePurchaseStepViewData data,
            Color primary,
            Color success,
            Color action)
        {
            if (data.Visibility == MaintenanceStepVisibility.Preview)
            {
                return new Color(primary.r, primary.g, primary.b, 0.65f);
            }

            return data.PurchaseState switch
            {
                MaintenanceStepPurchaseState.Purchased => success,
                MaintenanceStepPurchaseState.Available => action,
                _ => new Color(primary.r, primary.g, primary.b, 0.55f)
            };
        }

        private static void SetText(TMP_Text? target, string value, Color color)
        {
            if (target == null)
            {
                return;
            }

            target.text = value;
            target.color = color;
        }
    }
}
