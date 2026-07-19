# ICEBREAKER Unity 작업 소유권

이 문서는 같은 Unity Scene·Prefab을 여러 사람이 동시에 수정해 생기는 Git 충돌을 줄이기 위한 기준이다.

## 담당 Scene

| 담당 | Scene | 용도 |
|---|---|---|
| 민준 | `Assets/01.Scenes/minjun.unity` | 최종 통합·실행 Scene |
| 시연 | `Assets/01.Scenes/siyeon.unity` | 얼음 파쇄 Gameplay 시험 Scene |
| 정환 | `Assets/01.Scenes/jeonghwan.unity` | UI·피드백 시험 Scene |

각 Scene은 표의 담당자만 수정한다. 다른 담당자의 결과가 필요하면 Scene을 직접 복사하지 않고 완성된 Prefab과 공용 계약을 사용한다.

## 폴더 소유권

| 담당 | 주로 수정하는 경로 |
|---|---|
| 민준 | `02.Scripts/00.Shared`, `02.Scripts/10.Core`, `02.Scripts/40.Window`, `02.Scripts/90.Integration`, `03.Prefabs/10.Core`, `03.Prefabs/90.Integration`, `09.Data`, `Packages`, `ProjectSettings` |
| 시연 | `02.Scripts/20.Gameplay`, `03.Prefabs/20.Gameplay` |
| 정환 | `02.Scripts/30.UI`, `03.Prefabs/30.UI`, `04.Images`, `06.Sounds`, `07.Animations`, `08.Effects` |

`04.Images/20.Gameplay`의 이미지도 정환이 Unity로 가져와 Import Settings와 `.meta`를 커밋한다. 시연은 가져온 이미지를 읽어서 사용하고 설정 변경이 필요하면 정환에게 요청한다. 아티스트 원본은 Google Drive로 전달받고 정환이 Unity 프로젝트에 반영한다.

## 충돌 방지 규칙

1. Task마다 새 브랜치를 만들고 PR의 대상은 `dev`로 한다.
2. 작업 시작 전에 최신 `dev`를 반영한다.
3. 다른 담당자의 Scene이나 Prefab을 직접 수정하지 않는다.
4. 완성된 Prefab은 담당 폴더에서 제공하고, 민준이 `minjun.unity`에 배치한다.
5. Prefab을 통합 Scene에 배치한 뒤 Unpack하거나 `Apply All`을 하지 않는다.
6. 파일을 이동하거나 이름을 바꿀 때는 반드시 `.meta`를 함께 이동한다.
7. 공용 연결 코드는 `02.Scripts/90.Integration`에 두고, 다른 담당자의 구현 파일에 통합 코드를 섞지 않는다.
8. `Resources`는 P0에서 비워 둔다. 표준·시연 데이터는 `09.Data/Standard`, `09.Data/Demo`에 둔다.

## 공용 Assembly

`02.Scripts/00.Shared/Icebreaker.Shared.asmdef`만 우선 사용한다. Core·Gameplay·UI Assembly는 의존성이 실제로 필요해질 때 추가하고, 게임잼 기간에는 불필요하게 Assembly를 나누지 않는다.
