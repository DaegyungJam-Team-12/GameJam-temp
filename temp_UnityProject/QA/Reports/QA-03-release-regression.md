# QA-03 릴리스 회귀 테스트

## 최종 판정

**조건부 통과**

- 자동 회귀 테스트: **212/212 PASS**
- Windows Standalone 빌드: **PASS**
- 최초 실행 및 종료 후 재실행: **PASS**
- 릴리스 차단 결함: **0건**
- 남은 비차단 결함: **1건**
- 수동 확인 잔여 항목: **확장 전투 화면과 정산 화면의 실기 캡처**

자동화된 기능 검증과 실제 Windows 빌드의 시작·재실행은 모두 통과했다. 다만 자동 입력 주입이 Standalone 플레이어 버튼 입력으로 인식되지 않아 확장 전투 화면과 정산 화면의 실기 캡처는 수동 확인 항목으로 남긴다.

## 테스트 정보

| 항목 | 값 |
|---|---|
| 테스트 일시 | 2026-07-23 |
| 브랜치 | `test/qa-03-release-regression` |
| 기준 브랜치 | `origin/dev` |
| 기준 커밋 | `8c44bc7b5dd38328b23a73a1407157326858189b` |
| 최신 상태 확인 | `HEAD == origin/dev` 확인 |
| OS | Windows 11 |
| Unity | 6000.3.19f1 |
| 빌드 대상 | StandaloneWindows64 |
| 실행 Scene | `Assets/01.Scenes/minjun.unity` |
| 테스트 방식 | 원본과 분리한 임시 프로젝트에서 BatchMode 테스트·빌드 후 Windows 플레이어 실행 |

## 실행 결과

| 검증 | 결과 | 통과 | 실패 | 실행 시간/비고 |
|---|---|---:|---:|---|
| 전체 EditMode | PASS | 204 | 0 | 3.183초 |
| 전체 PlayMode | PASS | 8 | 0 | 151.253초 |
| Windows Standalone 빌드 | PASS | 1 | 0 | 117,252,410 bytes |
| Windows 플레이어 최초 실행 | PASS | 1 | 0 | 네이티브 창 연결 0.38초 |
| Windows 플레이어 재실행 | PASS | 1 | 0 | 네이티브 창 연결 0.33초 |

## 회귀 체크 결과

### 새 저장·저장·불러오기

- [x] 저장 파일이 없을 때 새 상태로 시작
- [x] 저장 데이터 직렬화·역직렬화
- [x] 기존 저장 덮어쓰기
- [x] 임시 파일이 남지 않는 원자적 저장
- [x] 손상 저장 백업 후 안전한 초기화
- [x] 프로필별 저장 파일 분리
- [x] 지연 저장 및 강제 Flush
- [x] 저장 관련 `SavePersistenceTests` **12/12 PASS**

### 종료·재실행·복구

- [x] 진행 중 종료 시 전체 항해 기준으로 안전 복구
- [x] 정산 중 종료 시 승인된 결과 유지
- [x] 저장된 상태를 불러와 다음 사이클 진행
- [x] Windows 플레이어 정상 종료
- [x] 동일 빌드 재실행 후 초기 화면 정상 표시
- [x] 재실행 로그에 `NullReferenceException`, `MissingReferenceException`, 미처리 예외 없음

### 창 동작

- [x] 축소·확장 상태 매핑 자동 테스트
- [x] 100%·125% DPI 배치 자동 테스트
- [x] 작업 영역 경계 보정 자동 테스트
- [x] Topmost 적용 및 포커스 비탈취 자동 테스트
- [x] 플러그인 실패 시 일반 창 Fallback 자동 테스트
- [x] 창 관련 `Icebreaker.Window.Tests` **34/34 PASS**
- [x] 실제 Windows 플레이어에서 UniWindow 네이티브 창 연결
- [x] 최초 실행과 재실행에서 축소 런처 렌더링 정상
- [ ] 실제 버튼 입력 후 확장 창 전환 실기 캡처

### 정산·게임 루프

- [x] StageEnding 후 정산 진입
- [x] 정산 결과의 보상·진행도 반영
- [x] 정산 승인 결과 중복 지급 방지
- [x] 두 사이클 누적 및 저장 복구
- [x] 실제 Scene에서 시작 버튼·카운트다운·두 사이클 저장 검증
- [x] 실제 Scene에서 정비 구매 후 Reload 지속성 검증
- [ ] 실제 정산 화면 실기 캡처

### 기존 기능

- [x] 게임 루프 및 HUD 연결
- [x] 저장된 정비 효과의 다음 스테이지 반영
- [x] 정비 트리 구매·표시·툴팁·입력
- [x] 게임플레이 파쇄·연쇄·지원 공격 관련 기존 테스트
- [x] 전체 EditMode 및 PlayMode 테스트에서 Assertion 실패 없음

## 캡처

### 최초 실행

![최초 실행 축소 런처](../Captures/QA-03/01-launcher-collapsed.png)

### 종료 후 재실행

![재실행 축소 런처](../Captures/QA-03/02-relaunch-collapsed.png)

## 남은 비차단 결함

### QA-03-NB-01 `IceFieldView.Awake` nullable 경고

- 심각도: 낮음
- 상태: 비차단
- 위치: `Assets/02.Scripts/20.Gameplay/IceFieldView.cs:142`
- 현상: Windows 빌드 중 `CS8602: Dereference of a possibly null reference` 경고가 1건 발생한다.
- 영향: 현재 Scene 구성과 전체 자동 테스트에서는 실패나 런타임 예외가 재현되지 않았다.
- 제안: `field` 초기화 계약을 명시하거나 `Awake` 이전 할당을 보장하는 검증 코드를 추가한다.

## 수동 확인 잔여 항목

다음 항목은 기능 자동 테스트를 통과했지만 `-nographics` 테스트와 합성 입력의 한계로 최종 실기 캡처가 남아 있다.

- `다음 쇄빙` 버튼을 직접 눌렀을 때 960×540 확장 전투 창 전환
- 실제 전투 화면의 한글 폰트 가독성·겹침
- 실제 정산 화면의 보상 수치·버튼 렌더링
- 정산 종료 후 축소 창 복귀 및 포커스 비탈취

## 증빙 파일

- `QA/Artifacts/QA-03/EditMode-results.xml`
- `QA/Artifacts/QA-03/PlayMode-results.xml`
- `QA/Artifacts/QA-03/Player-relaunch.log`
- `QA/Captures/QA-03/01-launcher-collapsed.png`
- `QA/Captures/QA-03/02-relaunch-collapsed.png`
