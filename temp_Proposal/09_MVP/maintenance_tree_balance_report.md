# 정비 트리 Phase G 밸런스 기록

## 기록 조건

- Unity Test Runner에서 실제 `CombatConfigFactory`, `AttackTickScheduler`, `IceField`, `ProgressionLedger`, `MaintenanceCore`를 연결했다.
- 각 작업은 60초·60FPS로 진행하고 커서는 남은 HP가 가장 낮은 얼음을 추적했다.
- 난수 시드는 회차별 `10000 + runIndex`로 고정했다.
- 구매는 고정 우선순위에서 현재 자금으로 살 수 있는 Step을 순서대로 처리했다.
- `availableSteps`와 `affordableSteps`는 구매 직전 값, `fundsAfterRun`은 구매 완료 뒤 값이다.

## 변경 전 기준 5회

기준값은 D01 `1/1.6/2.56/4.096`, D02 `5/7/9/11회/초`, 시연 비용 `표준/10 올림`이다.

| runIndex | fundsBeforeRun | earnedFunds | fundsAfterRun | availableSteps | affordableSteps | purchasedSteps | destroyedByTier | averageTargetsPerTick | damageBySource | destinationProgressGain |
|---:|---:|---:|---:|---:|---:|---|---|---:|---|---:|
| 1 | 0 | 340 | 30 | 1 | 1 | C01-L1, D01-L1, D02-L1, C02-L1, C03-L1, S01-L1 | T1:34, T2:0, T3:0 | 1.000 | CursorAreaPulse:338.0 | 34 |
| 2 | 30 | 1,111 | 161 | 6 | 5 | D01-L2, D04-L1, D02-L2, H01-L1, C02-L2, S02-L1 | T1:45, T2:4, T3:0 | 1.000 | CursorAreaPulse:726.4, SupportShot:56.0 | 6 |
| 3 | 161 | 2,760 | 301 | 10 | 10 | D01-L3, D04-L2, D02-L3, H01-L2, H02-L1, S02-L2 | T1:86, T2:18, T3:0 | 1.239 | CursorAreaPulse:1,897.0, SupportShot:195.8 | 104 |
| 4 | 301 | 8,352 | 423 | 7 | 7 | D03-L1, C04-L1, C02-L3, D04-L3, H01-L3, S03-L1, H02-L2 | T1:144, T2:69, T3:0 | 1.826 | CursorAreaPulse:5,402.6, SupportShot:540.7, CrackExplosion:479.2 | 16 |
| 5 | 423 | 21,294 | 19,717 | 1 | 1 | H03-L1 | T1:14, T2:28, T3:14 | 3.047 | CursorAreaPulse:9,048.2, Overkill:62.0, SupportShot:723.8 | 56 |

변경 전에는 1회차에 6개, 2~4회차에 각각 6~7개 Step을 한꺼번에 구매했다. 직접 피해 비중은 간접 피해가 발생한 회차에도 약 84~93%여서 특수·연쇄 피해 80% 초과 조건에는 해당하지 않았다.

## 변경 후 기준 5회

적용값은 D01 `1/3/5/7`, D02 `5/6.25/7.5/8.75회/초`, 항목별 시연 비용이다.

| runIndex | fundsBeforeRun | earnedFunds | fundsAfterRun | availableSteps | affordableSteps | purchasedSteps | destroyedByTier | averageTargetsPerTick | damageBySource | destinationProgressGain |
|---:|---:|---:|---:|---:|---:|---|---|---:|---|---:|
| 1 | 0 | 340 | 240 | 1 | 1 | C01-L1 | T1:34, T2:0, T3:0 | 1.000 | CursorAreaPulse:338.0 | 34 |
| 2 | 240 | 320 | 90 | 4 | 2 | C02-L1 | T1:32, T2:0, T3:0 | 1.000 | CursorAreaPulse:324.0 | 6 |
| 3 | 90 | 374 | 114 | 4 | 2 | C03-L1 | T1:34, T2:0, T3:0 | 1.000 | CursorAreaPulse:332.0 | 34 |
| 4 | 114 | 352 | 336 | 4 | 1 | C02-L2 | T1:32, T2:0, T3:0 | 1.000 | CursorAreaPulse:328.0 | 32 |
| 5 | 336 | 372 | 138 | 4 | 1 | D01-L1 | T1:23, T2:1, T3:0 | 1.000 | CursorAreaPulse:334.0 | 24 |

루트 온보딩 뒤에는 매 회차 4개 선택지와 1~2개 즉시 구매 항목을 유지했고, 5회 연속 구매가 가능했다. 각 실제 구매 가격은 구매 직전 보유액의 25~85% 범위다. 5회차의 T2 1개는 C02 2단계 기준 96 자금을 지급해, 파괴 수가 24개로 줄어도 32개를 파괴한 직전 회차보다 총 수입이 증가했다.

## 결정

- 표준 비용은 변경하지 않는다.
- 시연 비용의 일괄 `/10` 변환은 제거하고 `03_Data/balance_tables.md`의 항목별 값을 사용한다.
- D01과 D02는 레퍼런스형 후보값으로 확정한다.
- 60초 틱 오차, 프레임당 최대 3회, 저프레임 복구, S01 유효 틱, 일시정지, 작업 종료 경계는 자동 테스트로 고정한다.
