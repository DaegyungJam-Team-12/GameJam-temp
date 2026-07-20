#nullable enable

using Icebreaker.Core;
using Icebreaker.Shared.Progression;
using UnityEngine;

namespace Icebreaker.Integration
{
    public sealed class IntegrationBootstrap : MonoBehaviour
    {
        private ProgressionStateService? service;

        private void Start()
        {
            var view = Object.FindFirstObjectByType<Icebreaker.Gameplay.IceFieldView>();
            var hud = Object.FindFirstObjectByType<Icebreaker.UI.Hud.LauncherHudPresenter>();
            if (view == null || hud == null)
            {
                Debug.LogError("[INT-01] missing IceFieldView or LauncherHudPresenter in scene.");
                return;
            }

            var destination = new DestinationDefinition("island-village", "섬마을", 120, "식료품·우편", 0);
            service = new ProgressionStateService(destination);
            service.AttachCombatSource(view.Source);
            hud.Bind(new GameStateSourceAdapter(service));
            Debug.Log("[INT-01] wired: IceField.Source -> ProgressionCore -> LauncherHud");
        }

        private void OnDestroy()
        {
            service?.Dispose();
        }
    }
}
