# Feature Enhancement Master Plan — MailTriageAssistant
> Date: 2026-02-13
> Source Reports: `2026-02-13_01` ~ `2026-02-13_07`
> Total Findings: 75 across 7 agents → **42 unique commit units**

---

## Executive Summary

7개 에이전트 분석 결과, 기존 코드베이스는 보안·COM 최적화·접근성의 이전 개선 사항이 상당 부분 반영된 상태이다. 본 플랜은 **기능 확장**(시스템 트레이, 자동 분류, VIP 관리, 다국어, 세션 통계)과 **아키텍처 개선**(인터페이스 추출, CancellationToken, Serilog 활성화, 동시성 제어)을 단계적으로 실행한다.

### 우선순위 분포

| 우선순위 | 정의 | 커밋 수 | 근거 |
|---|---|---|---|
| **P0** (Critical) | 빌드/테스트 인프라, 보안, 아키텍처 기반 | 10 | Codex 후속 작업의 전제조건 |
| **P1** (Major) | 핵심 기능 구현, 성능, 관측성 | 18 | 사용자 가치 + 운영 안정성 |
| **P2** (Minor) | UI 파인튜닝, 편의 기능, 코드 품질 | 14 | Nice-to-have |

---

## Phase 0: 인프라 + 테스트 기반 (P0) — 5 commits

목표: 인터페이스 추출 → DI 정비 → 누락 테스트 추가 → 모든 후속 작업의 기반 확보

| Commit | 변경 파일 | 변경 요지 | 테스트 커맨드 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|
| 0-1 | `Services/IRedactionService.cs` (신규), `Services/ITriageService.cs` (신규), `Services/IDigestService.cs` (신규), `Services/ITemplateService.cs` (신규), 각 Service.cs | 4개 서비스 인터페이스 추출. 기존 구체 클래스에 `: I{Service}` 추가 | `dotnet build && dotnet test` | 빌드+테스트 통과 | Medium |
| 0-2 | `App.xaml.cs` | DI 등록을 인터페이스 기반으로 변경 (`AddSingleton<IRedactionService, RedactionService>()` 등). `MainWindow` → Transient | `dotnet build && dotnet test` | 빌드+테스트 통과 | Low |
| 0-3 | `IOutlookService.cs`, `OutlookService.cs`, `MainViewModel.cs` | 모든 비동기 메서드에 `CancellationToken ct = default` 추가 | `dotnet build && dotnet test` | 빌드+테스트 통과 | High |
| 0-4 | `Tests/ViewModels/MainViewModelTests.cs` (신규) | MainViewModel 단위 테스트 — `LoadEmailsAsync`, `GenerateDigestAsync`, 에러 경로 (6+ 테스트) | `dotnet test --filter "MainViewModelTests"` | 6개 이상 테스트 통과 | Medium |
| 0-5 | `Tests/Helpers/CategoryToIconConverterTests.cs` (신규), `Tests/Helpers/RangeObservableCollectionTests.cs` (신규) | 누락 Converter + Collection 테스트 | `dotnet test --filter "CategoryToIconConverter\|RangeObservable"` | 8개 이상 테스트 통과 | Low |

**Phase 0 DoD**: `dotnet build && dotnet test` 전체 Green, 인터페이스 기반 DI.

---

## Phase 1: 관측성 + Serilog 활성화 (P0) — 5 commits

목표: Serilog 구성 → 서비스 로깅 → PerfEventSource 연결 → 이후 기능의 디버깅 기반

| Commit | 변경 파일 | 변경 요지 | 테스트 커맨드 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|
| 1-1 | `App.xaml.cs` | Serilog 구성: `WriteTo.File(%LocalAppData%/MailTriageAssistant/logs/, rollingInterval: Day, retainedFileCountLimit: 7)` + DI `AddLogging(b => b.AddSerilog())` | `dotnet build` | 빌드 성공 + 로그 파일 생성 | Low |
| 1-2 | `Services/OutlookService.cs` | `ILogger<OutlookService>` 주입 + 주요 메서드 시작/완료/에러 로그. **본문 내용 절대 로깅 금지** | `dotnet build && dotnet test` | 빌드+테스트 통과 | Medium |
| 1-3 | `Services/TriageService.cs`, `Services/DigestService.cs`, `Services/RedactionService.cs`, `Services/TemplateService.cs` | 각 서비스 `ILogger<T>` 주입 + 기본 로그 포인트 (시작/완료/에러) | `dotnet build && dotnet test` | 빌드+테스트 통과 | Low |
| 1-4 | `App.xaml.cs:OnDispatcherUnhandledException` | `Log.Error(e.Exception, "Unhandled exception")` 추가 | `dotnet build` | 빌드 성공 | Low |
| 1-5 | `Helpers/PerfEventSource.cs`, `Services/OutlookService.cs` | PerfEventSource 이벤트 호출 삽입 (FetchHeaders, GetBody 시작/종료) | `dotnet build` | 빌드 성공 | Low |

**Phase 1 DoD**: `dotnet build && dotnet test` Green, 로그 파일 `%LocalAppData%`에 생성.

---

## Phase 2: 성능 + 안정성 강화 (P1) — 4 commits

목표: 동시성 제어 + 앱 시작 최적화 + 기능 구현의 안전 기반

| Commit | 변경 파일 | 변경 요지 | 테스트 커맨드 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|
| 2-1 | `OutlookService.cs` | `SemaphoreSlim _gate` 추가 → `InvokeAsync` 진입 시 acquire | `dotnet build && dotnet test` | 빌드+테스트 통과 | Medium |
| 2-2 | `OutlookService` 생성자 | Lazy 초기화 패턴 → DI 해결 시 블로킹 제거 | `dotnet build` | 앱 시작 블로킹 제거 | Medium |
| 2-3 | `TriageService`, `App.xaml.cs` | `IOptions` → `IOptionsMonitor` 전환 → 런타임 설정 변경 감지 | `dotnet build && dotnet test` | 빌드+테스트 통과 | Medium |
| 2-4 | `DigestService.cs:104-108` | `MessageBox.Show` → `IDialogService.ShowInfo()` | `dotnet build` | 빌드 성공 | Low |

**Phase 2 DoD**: `dotnet build && dotnet test` Green, `SemaphoreSlim` 직렬화 적용.

---

## Phase 3: 보안 강화 (P1) — 3 commits

목표: 신규 기능의 보안 기반 확보

| Commit | 변경 파일 | 변경 요지 | 테스트 커맨드 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|
| 3-1 | 신규 `Services/ISettingsService.cs`, `Services/JsonSettingsService.cs` | VIP 설정을 `%AppData%/MailTriageAssistant/user_settings.json`에 저장. 이메일 형식 검증 포함 | `dotnet build` | 빌드 성공 + 설정 파일 경로 | Medium |
| 3-2 | `App.xaml.cs`, DI | `ISettingsService` → Singleton 등록, VIP 로드 통합 | `dotnet build && dotnet test` | 빌드+테스트 통과 | Low |
| 3-3 | `ClipboardSecurityHelper.cs` | `GetClipboardSequenceNumber` P/Invoke → 레이스 컨디션 완화 | `dotnet build` | 빌드 성공 | Low |

**Phase 3 DoD**: `dotnet build && dotnet test` Green, `%AppData%` 경로에 설정 파일 저장.

---

## Phase 4: 다국어 기반 (P1) — 3 commits

목표: ResourceDictionary 기반 문자열 외부화 (한국어/영어)

| Commit | 변경 파일 | 변경 요지 | 테스트 커맨드 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|
| 4-1 | 신규 `Resources/Strings.ko.xaml`, `Resources/Strings.en.xaml` | 모든 UI 문자열을 ResourceDictionary 키로 정의 | `dotnet build` | 빌드 성공 | Medium |
| 4-2 | `MainWindow.xaml`, `App.xaml` | 모든 하드코딩 문자열 → `DynamicResource` | `dotnet build` | 빌드 성공 + 한국어 유지 | High |
| 4-3 | `App.xaml.cs`, `appsettings.json` | 언어 전환 메커니즘: `TriageSettings.Language` 기반 ResourceDictionary 교체 | `dotnet build` | 빌드 성공 + 언어 전환 동작 | Medium |

**Phase 4 DoD**: `dotnet build` Green, 한↔영 전환 수동 확인.

---

## Phase 5: 기능 구현 — 시스템 트레이 + 자동 분류 (P1) — 5 commits

| Commit | 변경 파일 | 변경 요지 | 테스트 커맨드 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|
| 5-1 | `.csproj`, `appsettings.json` | `Hardcodet.NotifyIcon.Wpf` NuGet 추가 + `"EnableSystemTray": true`, `"AutoRefreshIntervalMinutes": 0` | `dotnet build` | 빌드 성공 | Low |
| 5-2 | `App.xaml.cs`, `MainWindow.xaml.cs` | `NotifyIcon` 초기화 + Closing → 트레이 최소화 + 컨텍스트 메뉴 | `dotnet build` | 빌드 성공 + 트레이 아이콘 | Medium |
| 5-3 | `Models/TriageSettings.cs` | `AutoRefreshIntervalMinutes` 프로퍼티 추가 | `dotnet build && dotnet test` | 빌드+테스트 통과 | Low |
| 5-4 | `MainViewModel.cs` | 자동 분류 `DispatcherTimer` + `CancellationTokenSource` + 연속 실패 카운터(3회 → 일시 정지) | `dotnet build && dotnet test` | 빌드+테스트 통과 | Medium |
| 5-5 | `MainWindow.xaml` | 상단에 자동 분류 상태 표시 (`"다음 분류: Nm 후"`) + 수동 분류 시 타이머 리셋 | `dotnet build` | 빌드 성공 | Low |

**Phase 5 DoD**: `dotnet build && dotnet test` Green, 트레이 아이콘 + 자동 분류 수동 확인.

---

## Phase 6: 기능 구현 — VIP 관리 + 세션 통계 (P1~P2) — 5 commits

| Commit | 변경 파일 | 변경 요지 | 테스트 커맨드 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|
| 6-1 | `Models/SessionStats.cs` (신규) | 인메모리 세션 통계 모델 (카테고리별 건수, 총 처리 건수) | `dotnet build` | 빌드 성공 | Low |
| 6-2 | `MainViewModel.cs` | `SessionStats` 프로퍼티 + 분류 시 갱신 로직 + 사용자 행동 카운터 | `dotnet build && dotnet test` | 빌드+테스트 통과 | Low |
| 6-3 | `MainWindow.xaml` | StatusBar 영역에 세션 통계 카드 (카테고리별 건수) | `dotnet build` | 빌드 성공 + 시각 확인 | Low |
| 6-4 | 신규 `SettingsWindow.xaml`, `SettingsViewModel.cs` | VIP CRUD UI — 이메일 검증 + 추가/삭제, `ISettingsService` 연동 | `dotnet build` | 빌드 성공 | Medium |
| 6-5 | `MainWindow.xaml`, `MainViewModel.cs` | 설정 버튼 + `SettingsWindow` 열기 Command | `dotnet build` | 빌드 성공 | Low |

**Phase 6 DoD**: `dotnet build && dotnet test` Green, VIP CRUD + 통계 수동 확인.

---

## Phase 7: UI 파인튜닝 (P2) — 7 commits

| Commit | 변경 파일 | 변경 요지 | 테스트 커맨드 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|
| 7-1 | `App.xaml` | 전역 `FontFamily` 리소스: `"Malgun Gothic, Segoe UI"` | `dotnet build` | 빌드 성공 | Low |
| 7-2 | `MainWindow.xaml:269-325` | 버튼 영역 `WrapPanel` 전환 → 900px 이하 줄바꿈 | `dotnet build` | 빌드 성공 | Low |
| 7-3 | `MainWindow.xaml` | 본문 영역 로딩 오버레이 추가 (IsBodyLoading 시) | `dotnet build` | 빌드 성공 | Low |
| 7-4 | `MainWindow.xaml`, `App.xaml` | 에러 시 StatusBar 배경색 Red → 3초 후 복원 (Storyboard) | `dotnet build` | 에러 시 시각 피드백 | Low |
| 7-5 | `MainWindow.xaml` | 키보드 단축키 `InputBindings` (Ctrl+R → 분류, Ctrl+D → Digest, Ctrl+O → Open) | `dotnet build` | Ctrl+R/D/O 동작 | Low |
| 7-6 | `.csproj`, 아이콘 파일 | 앱 아이콘(`.ico`) 생성 + `<ApplicationIcon>` 설정 | `dotnet build` | 아이콘 표시 | Low |
| 7-7 | `Models/TriageSettings.cs` | `IValidateOptions<TriageSettings>` 구현 — 설정 값 범위 검증 | `dotnet build && dotnet test` | 빌드+테스트 통과 | Low |

**Phase 7 DoD**: `dotnet build && dotnet test` Green, UI 수동 확인.

---

## Phase 8: 통합 테스트 + 마무리 (P2) — 5 commits

| Commit | 변경 파일 | 변경 요지 | 테스트 커맨드 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|
| 8-1 | `Tests/Integration/WorkflowTests.cs` (신규) | 전체 워크플로우 통합 테스트 (Mock COM) — 로드→분류→Digest | `dotnet test --filter "WorkflowTests"` | 2개 이상 통과 | Medium |
| 8-2 | `Tests/Services/SettingsServiceTests.cs` (신규) | `ISettingsService` Read/Write 테스트 | `dotnet test --filter "SettingsService"` | 4개 이상 통과 | Low |
| 8-3 | `Tests/ViewModels/MainViewModelTests.cs` | 자동 분류 타이머 테스트 + 연속 실패 정지 테스트 | `dotnet test --filter "MainViewModelTests"` | 추가 4개 통과 | Medium |
| 8-4 | 전체 | `dotnet list package --outdated` → 필요 시 업데이트 | `dotnet build && dotnet test` | 취약 패키지 0건 | Low |
| 8-5 | `.ai/reports/2026-02-13_release_notes.md` | Release Notes 작성 | — | 문서 생성 | Low |

---

## Test Strategy

### 단위 테스트 (Unit)
```bash
dotnet test MailTriageAssistant.Tests/ --filter "Category!=Integration"
```

### 통합 테스트 (Integration)
```bash
dotnet test MailTriageAssistant.Tests/ --filter "Category=Integration"
```

### E2E (수동 체크리스트)
| # | 시나리오 | 기대 결과 |
|---|---|---|
| 1 | Classic Outlook 실행 → 메일 분류 | 50건 이상 표시 |
| 2 | 카테고리 필터 선택 → 목록 필터링 | 선택 카테고리만 |
| 3 | 이메일 선택 → 마스킹 본문 | PII 마스킹 확인 |
| 4 | Digest 복사 → Teams 열기 | Teams 활성화 |
| 5 | Win+V → 클립보드 히스토리 | 미표시 |
| 6 | 30초 대기 → Ctrl+V | 빈 클립보드 |
| 7 | 템플릿 답장 | Outlook 초안 생성 |
| 8 | 시스템 트레이 최소화 → 복원 | 정상 동작 |
| 9 | 자동 분류 (10분 후) | 자동 갱신 |
| 10 | 설정 → VIP 추가/삭제 | 즉시 반영 |
| 11 | 언어 전환 (한↔영) | 전체 UI 전환 |
| 12 | 세션 통계 확인 | 카테고리별 건수 |
| 13 | Ctrl+R/D/O 단축키 | 기능 실행 |

---

## Guardrails

### 기능 플래그
| 플래그 | 위치 | 기본값 | 사용 |
|---|---|---|---|
| `EnableSystemTray` | `appsettings.json` | `true` | 트레이 기능 On/Off |
| `AutoRefreshIntervalMinutes` | `appsettings.json` | `0` | 0=비활성, >0=자동 분류 |
| `EnableSessionStats` | `appsettings.json` | `false` | 통계 패널 On/Off |
| `Language` | `appsettings.json` | `"ko"` | UI 언어 |

### 관측 지표

| 지표 | 목표 | 측정 방법 |
|---|---|---|
| 헤더 로드 시간 | < 1000ms (50건) | Serilog + PerfEventSource |
| Digest 생성 시간 | < 500ms | Serilog |
| PII 마스킹 누락 | 0건 | Security 역테스트 통과 |
| 세션 에러율 | < 1% | 에러 카운터 |

### 롤백 기준
1. **빌드 실패**: `git revert HEAD` 즉시
2. **테스트 2회 연속 실패**: 블로커 리포트 → 해당 Task 스킵
3. **보안 불변 규칙 위반**: `git revert HEAD` + 보안 리포트
4. **COM 호환성 깨짐**: 해당 Phase 롤백

### 롤백 절차
```bash
# 빌드/테스트 실패 시
git revert HEAD
echo "Reverted: {commit_hash} — {사유}" >> .ai/reports/2026-02-13_codex_change_log.md

# 보안 위반 시
git revert HEAD
# + .ai/reports/2026-02-13_security_incident.md 생성
```

---

## Codex Instructions — 체크리스트

### 1. 보안 규칙 (절대 위반 금지)
- [ ] 이메일 본문을 디스크에 저장하지 않음
- [ ] 이메일 본문을 `Console`/`Debug`/`Trace`/`ILogger`로 출력하지 않음
- [ ] `ex.Message`를 UI에 직접 노출하지 않음
- [ ] 클립보드에 PII를 마스킹 없이 복사하지 않음
- [ ] 외부 API를 호출하지 않음
- [ ] 로그에 발신자 이메일 원본 미포함 (해시 또는 `[EMAIL]` 치환)

### 2. 커밋 규칙
- [ ] 1 Task = 1 Commit
- [ ] 커밋 메시지: `[NN] 카테고리: 설명` (feat/fix/refactor/test/ui/perf/security/observability)
- [ ] 커밋당 변경 파일 ≤ 5개
- [ ] 커밋당 변경 행 ≤ 200행

### 3. 빌드/테스트 게이트
```bash
# 매 커밋 전 필수 실행
dotnet build MailTriageAssistant/MailTriageAssistant.csproj
dotnet test MailTriageAssistant.Tests/
```

### 4. 코드 스타일
- [ ] Nullable reference type 경고 0
- [ ] `BannedSymbols.txt` 위반 0
- [ ] DI에 신규 서비스 등록 확인
- [ ] 인터페이스 기반 의존성 주입
