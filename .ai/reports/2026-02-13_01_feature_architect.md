# Feature Architect Report — MailTriageAssistant
> Date: 2026-02-13
> Feature Request: (빈 요청 — 범용 기능강화 프레임워크)

---

## Assumptions
- `<FEATURE_REQUEST>`가 비어있으므로, 기존 코드베이스 분석 기반으로 **가장 임팩트 있는 미구현 기능 + 아키텍처 개선**을 대상으로 구조화한다.
- 기존 사양서(spec) 미달성 항목, 기술 부채, 사용자 편의 기능을 유저스토리로 변환한다.

---

## User Stories

### US-01: 시스템 트레이 아이콘 + 백그라운드 실행
**As a** 업무 사용자  
**I want** 앱을 시스템 트레이로 최소화하여 백그라운드에서 실행  
**So that** 바탕화면 공간을 차지하지 않으면서도 빠르게 접근 가능

#### Acceptance Criteria
- AC-01: Given 앱 실행 중 / When X 버튼 클릭 / Then 창이 숨겨지고 시스템 트레이 아이콘 표시
- AC-02: Given 트레이 아이콘 표시 중 / When 더블클릭 / Then 메인 윈도우 복원
- AC-03: Given 트레이 아이콘 우클릭 / When 컨텍스트 메뉴 표시 / Then "메일 분류 실행", "Digest 복사", "열기", "종료" 메뉴 존재
- AC-04: Given 트레이 메뉴 "종료" 클릭 / When / Then `Application.Current.Shutdown()` 호출

#### Edge Cases
| # | 시나리오 | 예상 동작 |
|---|---|---|
| E-01 | 트레이 최소화 후 Outlook 종료 | 다음 "메일 분류 실행" 시 에러 메시지 |
| E-02 | 다중 인스턴스 실행 | Mutex로 단일 인스턴스 보장 (별도 Task 필요) |

---

### US-02: 자동 분류 스케줄러
**As a** 업무 사용자  
**I want** 일정 간격으로 자동으로 이메일을 분류  
**So that** 수동으로 버튼 클릭 없이 최신 상태 유지

#### Acceptance Criteria
- AC-01: Given `appsettings.json`에 `"AutoRefreshIntervalMinutes": 10` / When 앱 시작 / Then 10분마다 자동 분류 실행
- AC-02: Given 자동 분류 실행 중 / When 수동 "메일 분류 실행" 클릭 / Then 타이머 리셋 후 즉시 실행
- AC-03: Given `AutoRefreshIntervalMinutes: 0` / When 앱 시작 / Then 자동 분류 비활성화

#### Edge Cases
| # | 시나리오 | 예상 동작 |
|---|---|---|
| E-01 | 자동 분류 중 Outlook 종료 | 에러 로그 + 다음 주기까지 대기 |
| E-02 | 간격 값이 음수 | 0 처리(비활성화) |

---

### US-03: VIP 관리 UI
**As a** 업무 사용자  
**I want** 앱 내에서 VIP 발신자 목록을 추가/삭제  
**So that** `appsettings.json` 직접 편집 없이 관리 가능

#### Acceptance Criteria
- AC-01: Given 설정 화면 / When VIP 이메일 입력 + 추가 버튼 / Then `appsettings.json`의 `VipSenders` 배열에 추가
- AC-02: Given VIP 목록 / When 삭제 버튼 / Then 해당 항목 제거
- AC-03: Given VIP 변경 / When 저장 / Then 즉시 `TriageService`에 반영 (재시작 불필요)

#### Edge Cases
| # | 시나리오 | 예상 동작 |
|---|---|---|
| E-01 | 잘못된 이메일 형식 | 유효성 검증 실패 메시지 |
| E-02 | 중복 추가 | 무시 또는 경고 |

---

### US-04: 다국어 UI (한국어/영어)
**As a** 다국어 환경 사용자  
**I want** UI 언어를 한국어/영어 간 전환  
**So that** 영어 사용자도 앱을 편리하게 사용 가능

#### Acceptance Criteria
- AC-01: Given 설정 / When 언어 "English" 선택 / Then 모든 UI 레이블 영어 전환
- AC-02: Given `ResourceDictionary` 기반 / When 언어 변경 / Then 재시작 없이 즉시 반영
- AC-03: Given 기본값 / Then 한국어

---

### US-05: 이력 통계 (세션 기반)
**As a** 업무 사용자  
**I want** 현재 세션의 분류 통계를 확인  
**So that** 오늘 처리 현황을 한눈에 파악

#### Acceptance Criteria
- AC-01: Given 분류 완료 / When 통계 패널 확인 / Then 카테고리별 건수 + 비율 표시
- AC-02: Given 세션 중 / When 재분류 / Then 통계 갱신
- AC-03: Given 인메모리 전용 / Then 앱 종료 시 통계 삭제 (디스크 저장 안 함)

---

## Data Model Changes

| 파일 | 변경 유형 | 필드/클래스 | 설명 |
|---|---|---|---|
| `Models/TriageSettings.cs` | 수정 | `AutoRefreshIntervalMinutes: int` | 자동 분류 간격 (분) |
| `Models/TriageSettings.cs` | 수정 | `Language: string` | UI 언어 (`"ko"` / `"en"`) |
| `Models/SessionStats.cs` | 신규 | `ProcessedCount`, `CategoryCounts` | 세션 통계 모델 |
| `appsettings.json` | 수정 | 상기 설정 추가 | 기본값 포함 |

## Configuration Changes

| 키 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `TriageSettings.AutoRefreshIntervalMinutes` | `int` | `0` (비활성) | 자동 분류 간격 |
| `TriageSettings.Language` | `string` | `"ko"` | UI 언어 |

## Feature Flags

| 이름 | 위치 | 기본값 | 설명 |
|---|---|---|---|
| `EnableSystemTray` | `appsettings.json` | `true` | 시스템 트레이 활성화 |
| `EnableAutoRefresh` | `AutoRefreshIntervalMinutes > 0` | `false` | 자동 분류 (간격 > 0이면 활성) |
| `EnableSessionStats` | `appsettings.json` | `false` | 세션 통계 패널 |

## Affected Files

| 파일 | 변경 유형 | 근거 |
|---|---|---|
| `App.xaml.cs` | 수정 | 트레이 아이콘 초기화, 언어 리소스 로드 |
| `MainWindow.xaml` | 수정 | 트레이 최소화, 통계 패널 추가 |
| `MainWindow.xaml.cs` | 수정 | Closing 이벤트 트레이 전환 |
| `MainViewModel.cs` | 수정 | 타이머 Command, 통계 프로퍼티 |
| `Models/TriageSettings.cs` | 수정 | 신규 설정 필드 |
| `Models/SessionStats.cs` | 신규 | 통계 모델 |
| `appsettings.json` | 수정 | 신규 설정 키 |
| `Resources/Strings.ko.xaml` | 신규 | 한국어 리소스 |
| `Resources/Strings.en.xaml` | 신규 | 영어 리소스 |
| `.csproj` | 수정 | NuGet 패키지 (NotifyIcon) |

## Risk & Migration

| 리스크 | 영향도 | 완화 방안 |
|---|---|---|
| NotifyIcon NuGet 호환성 | Medium | `Hardcodet.NotifyIcon.Wpf` .NET 8 지원 확인 |
| appsettings.json Breaking Change | Low | 신규 키 추가만 — 기존 키 변경 없음 |
| ResourceDictionary 전환 시 기존 레이블 누락 | Medium | 모든 문자열 키 사전 정리 |
| 자동 분류 중 COM 에러 | Medium | per-cycle try-catch + 에러 로그 |

---

## Codex Handoff

### Task List

| # | 파일 | 변경 요지 | 테스트 커맨드 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|
| T-01 | `Models/TriageSettings.cs`, `appsettings.json` | `AutoRefreshIntervalMinutes`, `Language` 필드 추가 | `dotnet build && dotnet test` | 빌드 성공, 기존 테스트 통과 | Low |
| T-02 | `Models/SessionStats.cs` (신규) | 인메모리 세션 통계 모델 | `dotnet build` | 빌드 성공 | Low |
| T-03 | `.csproj`, `App.xaml.cs`, `MainWindow.xaml.cs` | 시스템 트레이 아이콘 + 백그라운드 실행 | `dotnet build` | 빌드 성공 + 트레이 아이콘 표시 | Medium |
| T-04 | `MainViewModel.cs` | 자동 분류 타이머 (`DispatcherTimer`) | `dotnet build && dotnet test` | 빌드 성공, 수동 테스트 | Medium |
| T-05 | `MainViewModel.cs`, `MainWindow.xaml` | 세션 통계 UI 패널 | `dotnet build` | 빌드 성공 + 통계 표시 | Low |
| T-06 | `Resources/Strings.ko.xaml`, `Strings.en.xaml` (신규) | 다국어 리소스 딕셔너리 | `dotnet build` | 빌드 성공 | Medium |
| T-07 | `MainWindow.xaml`, `App.xaml` | 모든 하드코딩 문자열 → `DynamicResource` | `dotnet build` | 빌드 성공 + 언어 전환 동작 | High |
| T-08 | `MainViewModel.cs`, 신규 `SettingsWindow.xaml` | VIP 관리 UI 창 | `dotnet build && dotnet test` | 빌드 성공 + VIP CRUD | Medium |
