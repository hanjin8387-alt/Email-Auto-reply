# Test Engineering Report — MailTriageAssistant
> Date: 2026-02-13
> Reviewer: Agent 04 (Test Engineering)

## Summary
- 기존 테스트: 7 파일 (Services 4 + Helpers 2 + Security 1)
- 커버리지 갭: 3개 영역 (MainViewModel, 신규 기능, 통합)
- 제안 신규 테스트: 22개+

---

## Test Pyramid

### 단위 테스트 (Unit) — 현황

| 서비스 | 기존 테스트 파일 | 추정 테스트 수 | 상태 |
|---|---|---|---|
| RedactionService | `Services/RedactionServiceTests.cs` | ~12 | ✅ 양호 |
| TriageService | `Services/TriageServiceTests.cs` | ~16 | ✅ 양호 |
| DigestService | `Services/DigestServiceTests.cs` | ~11 | ✅ 양호 |
| TemplateService | `Services/TemplateServiceTests.cs` | ~12 | ✅ 양호 |
| ScoreToColorConverter | `Helpers/ScoreToColorConverterTests.cs` | ~10 | ✅ 양호 |
| ScoreToLabelConverter | `Helpers/` (포함 여부 확인 필요) | ~5 | ⚠️ 불확실 |
| RedactionSecurity | `Security/RedactionSecurityTests.cs` | ~8 | ✅ 보안 역테스트 |
| **MainViewModel** | **없음** | 0 | ❌ 미구현 |
| **ClipboardSecurityHelper** | **없음** | 0 | ❌ (WPF Dispatcher 의존) |
| **CategoryToIconConverter** | **없음** | 0 | ❌ 미구현 |

### 커버리지 갭

| 서비스/클래스 | 미테스트 메서드 | 우선순위 | 이유 |
|---|---|---|---|
| `MainViewModel` | `LoadEmailsAsync`, `GenerateDigestAsync`, `ReplyAsync`, `OpenInOutlookAsync`, `CopySelected`, `PrefetchTopBodiesAsync`, `FilterEmailByCategory` | P0 | 핵심 워크플로우 |
| `CategoryToIconConverter` | `Convert` | P2 | 단순 매핑 |
| `RangeObservableCollection` | `AddRange` | P1 | Batch 로직 |
| `TaskExtensions` | `SafeFireAndForget` | P1 | fire-and-forget 안전성 |

---

### 신규 기능 테스트 계획

| 기능 | 테스트 대상 | Mock 필요 | 우선순위 |
|---|---|---|---|
| 자동 분류 타이머 | `MainViewModel` 타이머 시작/중지/리셋 | `IOutlookService` | P1 |
| VIP 관리 CRUD | `ISettingsService` (신규) | 파일 시스템 Mock | P1 |
| IOptionsMonitor 반영 | `TriageService` 설정 변경 감지 | — | P1 |
| 세션 통계 | `SessionStats` 누적/리셋 | — | P2 |
| 다국어 리소스 | ResourceDictionary 키 일관성 | — | P2 |

---

### 통합 테스트 (Integration)

| 시나리오 | Mock 대상 | 검증 항목 |
|---|---|---|
| 전체 워크플로우: 헤더 로드 → 분류 → Digest | `IOutlookService` | 50건 헤더 → 분류 결과 정상 → Digest 문자열 생성 |
| 템플릿 답장: 선택 → 템플릿 → 초안 생성 | `IOutlookService.CreateDraft` | 플레이스홀더 치환 → CreateDraft 호출 |
| 에러 경로: Outlook 미실행 | `IOutlookService` throw | StatusMessage 에러 표시, MessageBox 호출 |

### E2E (수동)

| # | 시나리오 | 검증 방법 |
|---|---|---|
| 1 | Classic Outlook 실행 → 메일 분류 → 50개 표시 | 앱 실행 |
| 2 | 이메일 선택 → 마스킹 본문 표시 | 시각 확인 |
| 3 | Digest 복사 → Teams 열기 | Teams 앱 확인 |
| 4 | Win+V → 히스토리에 미표시 | 클립보드 히스토리 |
| 5 | 30초 후 클립보드 비워짐 | 대기 후 Ctrl+V |
| 6 | 템플릿 답장 → Outlook 초안 생성 | Outlook 초안 폴더 |
| 7 | 카테고리 필터 → 선택 카테고리만 표시 | 시각 확인 |
| 8 | 키보드 Tab 순서 | Tab 키 탐색 |
| 9 | 시스템 트레이 (구현 후) | 트레이 아이콘 |
| 10 | 자동 분류 (구현 후) | 타이머 실행 |

---

## Mock Strategy

| 인터페이스 | Mock 방법 | 제약사항 |
|---|---|---|
| `IOutlookService` | `Mock<IOutlookService>()` — `FetchInboxHeaders`, `GetBody`, `CreateDraft`, `OpenItem` 설정 | COM 직접 테스트 불가 |
| `IDialogService` | `Mock<IDialogService>()` — `ShowError`, `ShowInfo` 호출 Verify | — |
| 신규 `ISettingsService` | `Mock<ISettingsService>()` | — |
| `IOptionsMonitor<TriageSettings>` | `Mock<IOptionsMonitor<TriageSettings>>()` — `CurrentValue` + `OnChange` | — |
| `ClipboardSecurityHelper` | 직접 테스트 어려움 (WPF Dispatcher 필요) | STA Thread + Dispatcher 초기화 필요 |

---

## Codex Handoff — Task List

| # | 파일 | 변경 요지 | 테스트 커맨드 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|
| T-01 | `Tests/ViewModels/MainViewModelTests.cs` (신규) | `LoadEmailsAsync` — Mock IOutlookService + 50건 → Emails.Count 검증, StatusMessage 검증 | `dotnet test --filter "MainViewModelTests"` | 3개 이상 테스트 통과 | Medium |
| T-02 | `Tests/ViewModels/MainViewModelTests.cs` | `GenerateDigestAsync` — Digest 문자열 포함 검증, ClipboardHelper 호출 검증 | `dotnet test --filter "MainViewModelTests"` | 3개 이상 테스트 통과 | Medium |
| T-03 | `Tests/ViewModels/MainViewModelTests.cs` | `ReplyAsync` — 템플릿 선택 → CreateDraft 호출 검증 | `dotnet test --filter "MainViewModelTests"` | 2개 이상 테스트 통과 | Medium |
| T-04 | `Tests/ViewModels/MainViewModelTests.cs` | 에러 경로 — OutlookService throw → StatusMessage 에러 + DialogService.ShowError 호출 | `dotnet test --filter "MainViewModelTests"` | 2개 이상 테스트 통과 | Low |
| T-05 | `Tests/Helpers/CategoryToIconConverterTests.cs` (신규) | 7개 카테고리 → 아이콘 매핑 + null/Unknown 입력 | `dotnet test --filter "CategoryToIconConverterTests"` | 9개 테스트 통과 | Low |
| T-06 | `Tests/Helpers/RangeObservableCollectionTests.cs` (신규) | `AddRange()` — CollectionChanged 1회 발생, Count 검증, 빈 리스트 | `dotnet test --filter "RangeObservableCollectionTests"` | 4개 테스트 통과 | Low |
| T-07 | `Tests/Integration/WorkflowTests.cs` (신규) | 전체 워크플로우 통합 테스트 (Mock COM) | `dotnet test --filter "WorkflowTests"` | 2개 테스트 통과 | High |
