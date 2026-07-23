#nullable enable

using System;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Maintenance;
using UnityEngine;

namespace Icebreaker.UI.Management
{
    public sealed class Ui05ManagementSampleSource : MonoBehaviour
    {
        private static readonly MaintenanceNodeViewData[] NoMaintenanceNodes =
            Array.Empty<MaintenanceNodeViewData>();

        [SerializeField] private ManagementViewsPresenter? presenter;

        private void Awake() => ResetSample();

        [ContextMenu("UI-05/Reset Sample")]
        public void ResetSample()
        {
            ResolvePresenter()?.Render(
                NoMaintenanceNodes,
                CreateTravelingRoute(),
                funds: 12_400L,
                nextStageRemainingSeconds: 24d,
                canStartStage: false);
            ResolvePresenter()?.SetSettingsValues(masterVolume: 0f, screenShakeEnabled: true);
        }

        [ContextMenu("UI-05/Show Route Status")]
        public void ShowRouteStatus()
        {
            ResetSample();
            ResolvePresenter()?.SetRouteVisible(true);
        }

        [ContextMenu("UI-05/Show Ready State")]
        public void ShowReadyState()
        {
            ResolvePresenter()?.Render(
                NoMaintenanceNodes,
                CreateTravelingRoute(),
                funds: 12_400L,
                nextStageRemainingSeconds: 0d,
                canStartStage: true);
        }

        [ContextMenu("UI-05/Show Completed State")]
        public void ShowCompletedState()
        {
            ResolvePresenter()?.Render(
                NoMaintenanceNodes,
                CreateCompletedRoute(),
                funds: 31_200L,
                nextStageRemainingSeconds: 0d,
                canStartStage: false);
        }

        [ContextMenu("UI-05/Open Settings")]
        public void OpenSettings() => ResolvePresenter()?.OpenSettings();

        [ContextMenu("UI-05/Show Island Arrival")]
        public void ShowIslandArrival() => ResolvePresenter()?.PresentArrival(
            new ArrivalPresentationRequested("island-village", "섬마을"));

        [ContextMenu("UI-05/Show Lighthouse Arrival")]
        public void ShowLighthouseArrival() => ResolvePresenter()?.PresentArrival(
            new ArrivalPresentationRequested("lighthouse-port", "등대항"));

        [ContextMenu("UI-05/Show Northern Base Arrival")]
        public void ShowNorthernBaseArrival() => ResolvePresenter()?.PresentArrival(
            new ArrivalPresentationRequested("northern-base", "북쪽 기지"));

        private ManagementViewsPresenter? ResolvePresenter()
        {
            if (presenter != null)
            {
                return presenter;
            }

            presenter = GetComponent<ManagementViewsPresenter>();
            return presenter;
        }

        private static RouteStatusViewData CreateTravelingRoute() => new(
            currentDestinationId: "island-village",
            currentDestinationName: "섬마을",
            progress: 37,
            target: 120,
            cargoText: "식료품 · 우편",
            completedDestinationsText: "출항 기지",
            upcomingDestinationsText: "등대항  →  북쪽 기지",
            gameCompleted: false);

        private static RouteStatusViewData CreateCompletedRoute() => new(
            currentDestinationId: "northern-base",
            currentDestinationName: "북쪽 기지",
            progress: 300,
            target: 300,
            cargoText: "기계 부품 · 우편  전달 완료",
            completedDestinationsText: "섬마을  ·  등대항  ·  북쪽 기지",
            upcomingDestinationsText: "모든 보급 항로 연결 완료",
            gameCompleted: true);
    }
}
