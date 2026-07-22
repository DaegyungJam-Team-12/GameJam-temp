#nullable enable

using System;
using System.Collections.Generic;
using Icebreaker.Shared.Events;
using Icebreaker.Shared.Maintenance;
using UnityEngine;

namespace Icebreaker.UI.Management
{
    public sealed class Ui05ManagementSampleSource : MonoBehaviour
    {
        [SerializeField] private ManagementViewsPresenter? presenter;

        private IReadOnlyList<MaintenanceNodeViewData>? sampleNodes;

        private void Awake() => ResetSample();

        [ContextMenu("UI-05/Reset Sample")]
        public void ResetSample()
        {
            sampleNodes ??= CreateSampleNodes();
            ResolvePresenter()?.Render(
                sampleNodes,
                CreateTravelingRoute(),
                funds: 12_400L,
                nextStageRemainingSeconds: 24d,
                canStartStage: false);
            ResolvePresenter()?.SetSettingsValues(masterVolume: 0f, screenShakeEnabled: true);
        }

        [ContextMenu("UI-05/Show Maintenance")]
        public void ShowMaintenance()
        {
            ResetSample();
            ResolvePresenter()?.ShowMaintenance();
        }

        [ContextMenu("UI-05/Show Route Status")]
        public void ShowRouteStatus()
        {
            ResetSample();
            ResolvePresenter()?.ShowRoute();
        }

        [ContextMenu("UI-05/Show Ready State")]
        public void ShowReadyState()
        {
            sampleNodes ??= CreateSampleNodes();
            ResolvePresenter()?.Render(
                sampleNodes,
                CreateTravelingRoute(),
                funds: 12_400L,
                nextStageRemainingSeconds: 0d,
                canStartStage: true);
        }

        [ContextMenu("UI-05/Show Completed State")]
        public void ShowCompletedState()
        {
            sampleNodes ??= CreateSampleNodes();
            var target = ResolvePresenter();
            target?.Render(
                sampleNodes,
                CreateCompletedRoute(),
                funds: 31_200L,
                nextStageRemainingSeconds: 0d,
                canStartStage: false);
            target?.ShowRoute();
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

        public IReadOnlyList<MaintenanceNodeViewData> GetSampleNodes()
        {
            sampleNodes ??= CreateSampleNodes();
            return sampleNodes;
        }

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

        private static IReadOnlyList<MaintenanceNodeViewData> CreateSampleNodes()
        {
            return Array.AsReadOnly(new[]
            {
                Node("C01", "강화 장비", MaintenanceBranch.Common, 1, 1,
                    MaintenanceNodeState.Owned, "파쇄 계통 개방", null, null, true, false),
                Node("C02", "정비 효율", MaintenanceBranch.Common, 1, 3,
                    MaintenanceNodeState.Owned, "파괴 자금 +10%", "파괴 자금 +20%", 2_400L, false, true),
                Node("C03", "청빙 대응", MaintenanceBranch.Common, 0, 1,
                    MaintenanceNodeState.Available, "미보유", "청빙 출현", 1_200L, false, true),
                Node("C04", "심빙 대응", MaintenanceBranch.Common, 0, 1,
                    MaintenanceNodeState.Locked, "미보유", "심빙 출현", 18_000L, false, false, "C03"),
                Node("D01", "주 파쇄기 출력", MaintenanceBranch.Direct, 1, 3,
                    MaintenanceNodeState.Owned, "직접 피해 ×1.6", "직접 피해 ×2.56", 900L, false, true),
                Node("D02", "고속 구동", MaintenanceBranch.Direct, 0, 3,
                    MaintenanceNodeState.Available, "미보유", "누르기 속도 +2/초", 600L, false, true),
                Node("D03", "과잉 파쇄", MaintenanceBranch.Direct, 0, 1,
                    MaintenanceNodeState.Locked, "미보유", "초과 피해 50% 전달", 8_000L, false, false, "D01"),
                Node("S01", "보조 파쇄기", MaintenanceBranch.Support, 0, 1,
                    MaintenanceNodeState.Available, "미보유", "12회 입력마다 보조탄", 500L, false, true),
                Node("S02", "다중 타격", MaintenanceBranch.Support, 0, 2,
                    MaintenanceNodeState.Locked, "미보유", "보조 대상 +1", 3_000L, false, false, "S01"),
                Node("S03", "표적 분석", MaintenanceBranch.Support, 0, 1,
                    MaintenanceNodeState.Locked, "미보유", "특수빙 우선 · 피해 ×2", 15_000L, false, false, "S01", "S02"),
                Node("H01", "파편 비산", MaintenanceBranch.Chain, 2, 3,
                    MaintenanceNodeState.Owned, "파괴 반경 피해 ×0.50", "파괴 반경 피해 ×0.75", 6_300L, false, true),
                Node("H02", "특수빙 증폭", MaintenanceBranch.Chain, 0, 2,
                    MaintenanceNodeState.Available, "미보유", "특수빙 효과 +30%", 4_000L, false, true),
                Node("H03", "빙판 붕괴", MaintenanceBranch.Chain, 0, 1,
                    MaintenanceNodeState.Locked, "미보유", "5연쇄 시 빙판 붕괴", 20_000L, false, false, "H01")
            });
        }

        private static MaintenanceNodeViewData Node(
            string id,
            string name,
            MaintenanceBranch branch,
            int level,
            int maxLevel,
            MaintenanceNodeState state,
            string currentEffect,
            string? nextEffect,
            long? nextCost,
            bool isMax,
            bool canAfford,
            params string[] missingRequirements)
        {
            var canPurchase = !isMax && state != MaintenanceNodeState.Locked && canAfford;
            return new MaintenanceNodeViewData(
                id,
                name,
                branch,
                level,
                maxLevel,
                state,
                currentEffect,
                nextEffect,
                nextCost,
                isMax,
                canAfford,
                canPurchase,
                missingRequirements);
        }
    }
}
