#nullable enable

using System;
using Icebreaker.Shared.Maintenance;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Icebreaker.UI.Management
{
    public sealed class MaintenanceNodeView : MonoBehaviour
    {
        [SerializeField] private string nodeId = string.Empty;
        [SerializeField] private Button? button;
        [SerializeField] private Image? background;
        [SerializeField] private Image? selectionBorder;
        [SerializeField] private TMP_Text? nameText;
        [SerializeField] private TMP_Text? levelText;
        [SerializeField] private TMP_Text? stateText;

        private bool listening;

        public event Action<string> Clicked = delegate { };

        public string NodeId => nodeId;

        public MaintenanceNodeState RenderedState { get; private set; }

        private void Awake() => EnsureInitialized();

        private void OnEnable() => EnsureInitialized();

        private void OnDestroy()
        {
            if (listening)
            {
                button?.onClick.RemoveListener(HandleClicked);
            }
        }

        public void EnsureInitialized()
        {
            if (listening || button == null)
            {
                return;
            }

            button.onClick.AddListener(HandleClicked);
            listening = true;
        }

        public void Render(
            MaintenanceNodeViewData data,
            bool selected,
            Color lockedColor,
            Color availableColor,
            Color ownedColor,
            Color textColor)
        {
            if (!string.Equals(data.Id, nodeId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Node view {nodeId} cannot render data for {data.Id}.",
                    nameof(data));
            }

            RenderedState = data.State;
            if (nameText != null)
            {
                nameText.text = data.DisplayName;
                nameText.color = textColor;
            }

            if (levelText != null)
            {
                levelText.text = $"{data.Id}  Lv.{data.CurrentLevel}/{data.MaxLevel}";
                levelText.color = textColor;
            }

            if (stateText != null)
            {
                stateText.text = data.State switch
                {
                    MaintenanceNodeState.Owned => "보유",
                    MaintenanceNodeState.Available => "구매 가능",
                    _ => "잠김"
                };
                stateText.color = textColor;
            }

            if (background != null)
            {
                background.color = data.State switch
                {
                    MaintenanceNodeState.Owned => ownedColor,
                    MaintenanceNodeState.Available => availableColor,
                    _ => lockedColor
                };
            }

            SetSelected(selected, textColor);
        }

        public void SetSelected(bool selected, Color borderColor)
        {
            if (selectionBorder == null)
            {
                return;
            }

            selectionBorder.enabled = selected;
            selectionBorder.color = borderColor;
        }

        private void HandleClicked() => Clicked(nodeId);
    }
}
