# Feature Discovery Report — MailTriageAssistant
> Date: 2026-02-15
> Reviewer: Agent 04 (Feature Discovery)

---

## Spec Gap Analysis

### Implemented ✅

| Feature ID | Name | Status | Notes |
|---|---|---|---|
| FE-001 | Outlook COM Connector | ✅ 구현 완료 | `OutlookService.cs` 328줄, Classic 전용, `olk.exe` 감지, STA 스레드 분리, `GetActiveObject` P/Invoke 사용 |
| FE-002 | Local Triage Engine | ✅ 구현 완료 | `TriageService.cs` 164줄, 2단계 분석(Header-only / WithBody), 7개 카테고리, VIP/Action/Newsletter 가중치 스코어링 |
| FE-003 | In-Memory Redaction Service | ✅ 구현 완료 | `RedactionService.cs` 33줄, SSN/Phone/Email/CreditCard 4개 패턴 Regex, 입출력 로그 없음 |
| FE-004 | Secure Digest Bridge | ✅ 구현 완료 | `DigestService.cs` 134줄 + `ClipboardSecurityHelper.cs` 66줄, 30초 자동 삭제, Teams https/msteams 2단 폴백, 마크다운 테이블 포맷 |
| FE-005 | WPF Dashboard | ✅ 구현 완료 | `MainWindow.xaml` 185줄, 2컬럼 레이아웃, 우선순위 색상 코딩, 상태바, MVVM 바인딩 |
| FE-006 | Contextual Reply Templates | ✅ 구현 완료 | `TemplateService.cs` 107줄, 8개 템플릿 전부 구현(TMP_01~TMP_08), 플레이스홀더 치환, Outlook 초안 생성 |

### Backlog (TASK) 진행 상태

| Task ID | Title | Status | Evidence |
|---|---|---|---|
| TASK-01 | Implement Outlook COM Wrapper | ✅ 완료 | `OutlookService.FetchInboxHeaders()`, `GetBody()`, `CreateDraft()` 구현 완료 |
| TASK-02 | Develop Triage Algorithm | ⚠️ 부분 완료 | 키워드 기반 스코어링 구현됨. **단위 테스트 없음** (DoD: "Unit tests pass" 미달성) |
| TASK-03 | Implement Regex Redactor | ⚠️ 부분 완료 | Regex 치환 구현됨. **단위 테스트 없음** (DoD 확인 불가) |
| TASK-04 | Build Digest Generator | ✅ 완료 | `DigestService.GenerateDigest()` 마크다운 테이블 + System Prompt + Tasks 포함 |
| TASK-05 | Teams Deep Link Integration | ✅ 완료 | `DigestService.OpenTeams()` https → msteams → MessageBox 3단 폴백 |
| TASK-06 | WPF UI Implementation | ✅ 완료 | `MainWindow.xaml` + `MainViewModel.cs` MVVM 바인딩 동작 확인 |
| TASK-07 | Template Engine Logic | ✅ 완료 | `TemplateService.SendDraft()` → `OutlookService.CreateDraft()` 연동 확인 |

### Acceptance Tests 기준 충족 여부

| Scenario | Spec Requirement | Status | Gap |
|---|---|---|---|
| Security Compliance | 파일/temp에 이메일 본문 저장 불가 | ✅ 충족 | 메모리 전용 처리, 디스크 I/O 없음 |
| Redaction Quality | `010-1234-5678` → `[PHONE]` | ⚠️ 코드상 구현됨 | **자동화 테스트 없음** — 수동으로만 검증 가능 |
| Offline Handling | Outlook 미실행 시 "Waiting for Outlook" | ✅ 충족 | `InvalidOperationException` 메시지로 대체 ("Outlook이 실행 중이지 않습니다") |
| Workflow Efficiency | "Copy to Teams" → Teams 열림 < 2초 | ⚠️ 미검증 | 성능 테스트 인프라 부재 |

### Reply Templates 구현 확인

| Template ID | Name | Status |
|---|---|---|
| TMP_01 | 수신 확인 (Acknowledge) | ✅ |
| TMP_02 | 추가 정보 요청 (Request Info) | ✅ |
| TMP_03 | 일정 제안 (Propose Time) | ✅ |
| TMP_04 | 지연 안내 (Delay Notice) | ✅ |
| TMP_05 | 완료 보고 (Task Done) | ✅ |
| TMP_06 | 보류/대기 (On Hold) | ✅ |
| TMP_07 | 승인 (Approve) | ✅ |
| TMP_08 | 단순 감사 (Thank You) | ✅ |

---

## Not Yet Implemented ❌

| Feature ID | Name | Effort | Priority | Description |
|---|---|---|---|---|
| FE-007 | 시스템 트레이 아이콘 | M | P1 | 사양서 `ui_spec.tray_menu`에 명시됨 (Status/Run/Copy/Open/Exit) — 현재 미구현 |
| FE-008 | 카테고리 필터링 | S | P1 | 사양서 `dashboard_window`: "Priority ListBox (Color Coded)" — ListBox 존재하나 **카테고리 필터 ComboBox 없음** |
| FE-009 | "Open in Outlook" 버튼 | S | P1 | 사양서 `action_buttons`에 명시 — 현재 UI에 없음 |
| FE-010 | 다중 계정 지원 | L | P2 | 현재 `GetDefaultFolder(olFolderInbox)` 기본 계정만 사용 |
| FE-011 | Windows Clipboard History 비활성화 | S | P0 | 사양서 `disable_windows_clipboard_history_for_app: true` — 미구현 |
| FE-012 | 분류 규칙 커스터마이징 | M | P2 | VIP 리스트/키워드가 하드코딩됨 — 사용자 편집 UI 없음 |
| FE-013 | VIP 목록 관리 UI | M | P2 | `TriageService.VipSenders` 하드코딩 3개만 존재 |
| FE-014 | 이력 통계 대시보드 | M | P2 | 일/주간 분류 현황 없음 |
| FE-015 | 자동 분류 주기(스케줄러) | M | P1 | 현재 수동 "Run Triage Now" 버튼만 존재 |
| FE-016 | 첨부파일 미리보기 | S | P2 | `HasAttachments` 필드는 모델에 존재하나 **UI에 미노출** |
| FE-017 | 답장 이력 추적 | M | P2 | 어떤 메일에 어떤 템플릿으로 답장했는지 기록 없음 |
| FE-018 | 다국어 UI(영어/한국어 전환) | M | P2 | 현재 한국어/영어 혼합 하드코딩 |

---

## New Feature Proposals

### 🌟 High Value

| # | Feature | Description | Effort | Dependencies | Rationale |
|---|---|---|---|---|---|
| F-01 | 시스템 트레이 아이콘 + 백그라운드 실행 | `NotifyIcon` 기반 트레이 메뉴 (Status, Run, Copy, Open, Exit) | M | MainWindow, ViewModel | 사양서 `tray_menu` 스펙 직접 요구사항 |
| F-02 | 카테고리 필터 콤보박스 | ListBox 상단에 "All / Action / VIP / Meeting / ..." 필터링 | S | MainViewModel, MainWindow.xaml | 대량 메일 시 사용성 향상 |
| F-03 | "Open in Outlook" 버튼 | 선택 메일의 EntryId로 Outlook Inspector 열기 | S | OutlookService | 사양서 `action_buttons` 직접 요구 |
| F-04 | 자동 분류 스케줄러 | 설정 가능한 간격(5/10/15/30분)으로 자동 재분류 | M | MainViewModel, Timer | 수동 클릭 없이 알림 가능 |
| F-05 | Windows Clipboard History 비활성화 | `SetClipboardData` 시 `ExcludeClipboardContentFromMonitorProcessing` 포맷 사용 | S | ClipboardSecurityHelper | 사양서 보안 제약조건 P0 |
| F-06 | VIP 관리 UI | 앱 내에서 VIP 발신자 추가/삭제, JSON 설정 파일 연동 | M | TriageService, 새 SettingsService | 하드코딩 제거, 사용자 맞춤 |
| F-07 | DI 컨테이너 도입 | `Microsoft.Extensions.DependencyInjection` 으로 수동 생성자 주입 대체 | M | App.xaml.cs, MainWindow.xaml.cs, 모든 Service | 테스트 용이성 + 유지보수성 |

### 💡 Nice-to-Have

| # | Feature | Description | Effort | Dependencies |
|---|---|---|---|---|
| F-08 | 첨부파일 아이콘 표시 | `HasAttachments=true` 시 📎 아이콘 ListBox 아이템에 표시 | S | MainWindow.xaml |
| F-09 | 이력 통계 (일/주간) | 인메모리 세션 통계 — 오늘 분류 개수, 카테고리 분포 차트 | M | 새 StatisticsService |
| F-10 | 다국어 UI (ResourceDictionary) | `Strings.ko.xaml`, `Strings.en.xaml` 리소스 딕셔너리 전환 | M | 전체 XAML |
| F-11 | 답장 이력 추적 (인메모리) | 세션 중 답장 이력 Dictionary 관리, UI 표시 | S | MainViewModel |
| F-12 | 분류 규칙 커스터마이징 UI | 키워드/카테고리 매핑을 Settings UI에서 편집 | L | 새 SettingsWindow, TriageService 리팩토링 |
| F-13 | 다중 계정 지원 | Outlook `Stores` 컬렉션 순회, 계정 선택 ComboBox | L | OutlookService, MainViewModel |
| F-14 | 알림 기능 | 새 중요 메일 검출 시 Toast/Notification 알림 | M | 스케줄러, Windows SDK |
| F-15 | 성능 벤치마크 테스트 | "Copy to Teams" < 2초 확인용 자동화 성능 테스트 | S | 테스트 프로젝트 |

---

## Tech Debt Backlog

| # | Item | Impact | Effort | Recommendation |
|---|---|---|---|---|
| TD-01 | **테스트 프로젝트 완전 부재** | 회귀 방지 불가, DoD 미달성 (TASK-02, TASK-03) | M | 즉시 `MailTriageAssistant.Tests` xUnit 프로젝트 생성. RedactionService, TriageService 최소 커버리지 확보 |
| TD-02 | **DI 컨테이너 미사용** | `MainWindow.xaml.cs`에서 7개 서비스 수동 `new` 생성. 테스트/교체 어려움 | M | `Microsoft.Extensions.DependencyInjection` 도입, `App.xaml.cs`에서 ServiceProvider 구성 |
| TD-03 | **설정 파일 부재** | VIP 리스트(`ceo@company.com` 등), 키워드, Teams 이메일 전부 하드코딩 | M | `appsettings.json` 또는 `IConfiguration` 기반 설정 외부화 |
| TD-04 | **로깅 프레임워크 부재** | 디버깅 시 정보 부족, 운영 모니터링 불가 | S | `Microsoft.Extensions.Logging` + `Serilog` (본문 제외 정책 필터 적용) |
| TD-05 | **에러 리포팅 부재** | 앱 크래시 시 사용자 피드백/원격 수집 불가 | S | Sentry 또는 Application Insights (PII 필터 필수) |
| TD-06 | **AnalyzedItem 이중 INotifyPropertyChanged** | AnalyzedItem과 MainViewModel 모두 INPC 수동 구현 — CommunityToolkit.Mvvm 사용 시 보일러플레이트 제거 가능 | S | `ObservableObject` 상속으로 대체 검토 |
| TD-07 | **RawEmailHeader에 init 대신 set 사용** | 불변성 미보장 — `init` accessor 가 더 적절 | S | `set` → `init` 변경 |
| TD-08 | **DigestService에 RedactionService 이중 적용** | `ClipboardSecurityHelper.SecureCopy()`에서 이미 Redact 호출 + `DigestService.GenerateDigest()`에서도 Redact 호출 → 이중 마스킹 | S | 책임 분리 정리: Digest 생성 시에만 Redact, 클립보드는 이미 마스킹된 텍스트 전달 |

---

## Recommended Roadmap

### v1.1 (다음 릴리즈) — Tech Debt 해소 + P0/P1 기능
1. **TD-01**: xUnit 테스트 프로젝트 생성 (`RedactionService`, `TriageService`, `TemplateService` 단위 테스트)
2. **TD-02**: DI 컨테이너 도입 (`Microsoft.Extensions.DependencyInjection`)
3. **TD-03**: `appsettings.json` 설정 외부화 (VIP 리스트, 키워드)
4. **F-05**: Windows Clipboard History 비활성화 (보안 P0)
5. **F-02**: 카테고리 필터 UI
6. **F-03**: "Open in Outlook" 버튼 추가
7. **TD-08**: Redaction 이중 적용 정리

### v1.2 — 사용자 경험 향상
1. **F-01**: 시스템 트레이 아이콘 + 백그라운드 실행
2. **F-04**: 자동 분류 스케줄러
3. **F-06**: VIP 관리 UI
4. **F-08**: 첨부파일 아이콘 표시
5. **TD-04**: 로깅 프레임워크 도입

### v1.3 — 고급 기능
1. **F-09**: 이력 통계 대시보드
2. **F-10**: 다국어 UI
3. **F-11**: 답장 이력 추적
4. **F-12**: 분류 규칙 커스터마이징 UI
5. **F-13**: 다중 계정 지원
6. **F-14**: 알림 기능

---

## Codex Handoff

### 구현 순서 원칙
1. Tech Debt 해소 먼저 (테스트 프로젝트, DI, 설정 외부화)
2. 보안 P0 (Clipboard History 비활성화)
3. 사양서 누락 P1 기능 (필터, Open in Outlook)
4. High Value 기능
5. Nice-to-Have → 백로그

### 커밋 절차
```
1) 기능 브랜치 생성: feature/{기능명}
2) 구현 + 테스트 작성
3) dotnet build + dotnet test
4) 커밋: [04] feat: {기능명}
5) PR 생성
```

---

## Task List (Codex 즉시 구현용)

> **모든 Task 공통 커밋 절차:**
> 1. `git checkout -b feature/{기능명}`
> 2. 수정 및 신규 파일 작성
> 3. `dotnet build MailTriageAssistant/MailTriageAssistant.csproj` → 빌드 성공
> 4. `dotnet test MailTriageAssistant.Tests/` → 테스트 통과 (Task 1 이후)
> 5. `git commit -m "[04] feat: {기능명}"`
> 6. PR 생성

---

### Task 1: xUnit 테스트 프로젝트 생성

| 항목 | 내용 |
|---|---|
| **기능명** | TD-01 — 테스트 프로젝트 생성 |
| **대상 파일** | `MailTriageAssistant.Tests/MailTriageAssistant.Tests.csproj` (신규) |
| **변경 요약** | (1) `dotnet new xunit -n MailTriageAssistant.Tests -o MailTriageAssistant.Tests`<br>(2) `dotnet add MailTriageAssistant.Tests/ reference MailTriageAssistant/MailTriageAssistant.csproj`<br>(3) `dotnet add MailTriageAssistant.Tests/ package Moq` |
| **의존성** | 없음 (가장 먼저 수행) |
| **예상 공수** | S |
| **테스트 명령** | `dotnet test MailTriageAssistant.Tests/` |

---

### Task 2: RedactionService 단위 테스트 작성

| 항목 | 내용 |
|---|---|
| **기능명** | TD-01a — RedactionService 테스트 |
| **대상 파일** | `MailTriageAssistant.Tests/Services/RedactionServiceTests.cs` (신규) |
| **변경 요약** | (1) `Redact("010-1234-5678")` → `"[PHONE]"` 검증<br>(2) `Redact("123456-1234567")` → `"[SSN]"` 검증<br>(3) `Redact("test@example.com")` → `"[EMAIL]"` 검증<br>(4) `Redact("1234-5678-9012-3456")` → `"[CARD]"` 검증<br>(5) 복합 패턴 테스트: 본문에 다중 PII 포함 시 전부 마스킹<br>(6) 빈 문자열/null 입력 Edge case |
| **함수명** | `RedactionServiceTests.Redact_PhoneNumber_ReturnsMasked()` 등 6개+ 메서드 |
| **의존성** | Task 1 |
| **예상 공수** | S |
| **테스트 명령** | `dotnet test --filter "FullyQualifiedName~RedactionServiceTests"` |

---

### Task 3: TriageService 단위 테스트 작성

| 항목 | 내용 |
|---|---|
| **기능명** | TD-01b — TriageService 테스트 |
| **대상 파일** | `MailTriageAssistant.Tests/Services/TriageServiceTests.cs` (신규) |
| **변경 요약** | (1) VIP 발신자(`ceo@company.com`) → `EmailCategory.VIP`, Score ≥ 80 검증<br>(2) Action 키워드("요청") → `EmailCategory.Action`, Score += 20 검증<br>(3) Newsletter 키워드("구독") → `EmailCategory.Newsletter`, Score -= 50 검증<br>(4) `AnalyzeWithBody()` — body 포함 시 카테고리 변경 검증<br>(5) 복합 키워드(VIP + Action) 시 점수 누적 검증<br>(6) Approval 키워드("결재") → `EmailCategory.Approval` 검증<br>(7) Meeting 키워드("초대") → `EmailCategory.Meeting` 검증<br>(8) 빈 입력/null Edge case |
| **함수명** | `TriageServiceTests.AnalyzeHeader_VipSender_ReturnsVipCategory()` 등 8개+ 메서드 |
| **의존성** | Task 1 |
| **예상 공수** | S |
| **테스트 명령** | `dotnet test --filter "FullyQualifiedName~TriageServiceTests"` |

---

### Task 4: TemplateService 단위 테스트 작성

| 항목 | 내용 |
|---|---|
| **기능명** | TD-01c — TemplateService 테스트 |
| **대상 파일** | `MailTriageAssistant.Tests/Services/TemplateServiceTests.cs` (신규) |
| **변경 요약** | (1) `GetTemplates()` — 8개 반환 검증<br>(2) `FillTemplate()` — 플레이스홀더 치환 검증 (`{TargetDate}` → 실제 날짜)<br>(3) 미제공 플레이스홀더 → `"___"` 대체 검증<br>(4) 빈 템플릿 입력 Edge case |
| **함수명** | `TemplateServiceTests.GetTemplates_Returns8Templates()` 등 4개+ 메서드 |
| **의존성** | Task 1 |
| **예상 공수** | S |
| **테스트 명령** | `dotnet test --filter "FullyQualifiedName~TemplateServiceTests"` |

---

### Task 5: DigestService 단위 테스트 작성

| 항목 | 내용 |
|---|---|
| **기능명** | TD-01d — DigestService 테스트 |
| **대상 파일** | `MailTriageAssistant.Tests/Services/DigestServiceTests.cs` (신규) |
| **변경 요약** | (1) `GenerateDigest()` — System Prompt 포함 검증 (`"⚠️ SYSTEM PROMPT"`)<br>(2) 마크다운 테이블 헤더 형식 검증 (`"| Priority | Sender |"`)<br>(3) Tasks 3개 항목 포함 검증 (`"top 3 critical"`, `"deadlines"`, `"Draft"`)<br>(4) Context footer 포함 검증 (`"All PII has been redacted"`)<br>(5) 빈 리스트 입력 시 헤더+footer만 출력 검증<br>(6) PII가 포함된 sender → `_redactionService.Redact()` 호출 검증 (Mock/Verify) |
| **함수명** | `DigestServiceTests.GenerateDigest_IncludesSystemPrompt()` 등 6개+ 메서드 |
| **의존성** | Task 1, Moq |
| **예상 공수** | S |
| **테스트 명령** | `dotnet test --filter "FullyQualifiedName~DigestServiceTests"` |

---

### Task 6: DI 컨테이너 도입

| 항목 | 내용 |
|---|---|
| **기능명** | TD-02 — DI 컨테이너 |
| **대상 파일** | (1) `MailTriageAssistant/MailTriageAssistant.csproj` — NuGet 추가<br>(2) `MailTriageAssistant/App.xaml.cs` — ServiceProvider 구성<br>(3) `MailTriageAssistant/App.xaml` — `StartupUri` 제거<br>(4) `MailTriageAssistant/MainWindow.xaml.cs` — 수동 `new` 제거, DI 주입으로 변경 |
| **변경 요약** | (1) `dotnet add MailTriageAssistant/ package Microsoft.Extensions.DependencyInjection`<br>(2) `App.xaml.cs`에 `ConfigureServices()` 메서드 추가:<br>&nbsp;&nbsp;— `services.AddSingleton<RedactionService>()`<br>&nbsp;&nbsp;— `services.AddSingleton<ClipboardSecurityHelper>()`<br>&nbsp;&nbsp;— `services.AddSingleton<IOutlookService, OutlookService>()`<br>&nbsp;&nbsp;— `services.AddSingleton<TriageService>()`<br>&nbsp;&nbsp;— `services.AddSingleton<DigestService>()`<br>&nbsp;&nbsp;— `services.AddSingleton<TemplateService>()`<br>&nbsp;&nbsp;— `services.AddTransient<MainViewModel>()`<br>&nbsp;&nbsp;— `services.AddTransient<MainWindow>()`<br>(3) `App.xaml`에서 `StartupUri="MainWindow.xaml"` 제거<br>(4) `OnStartup`에서: `var mainWindow = ServiceProvider.GetRequiredService<MainWindow>(); mainWindow.Show();`<br>(5) `MainWindow.xaml.cs` 생성자를 `public MainWindow(MainViewModel viewModel)` 으로 변경, `DataContext = viewModel;` |
| **의존성** | 없음 |
| **예상 공수** | M |
| **테스트 명령** | `dotnet build MailTriageAssistant/` |

---

### Task 7: 설정 파일 외부화 (appsettings.json)

| 항목 | 내용 |
|---|---|
| **기능명** | TD-03 — 설정 외부화 |
| **대상 파일** | (1) `MailTriageAssistant/appsettings.json` (신규)<br>(2) `MailTriageAssistant/MailTriageAssistant.csproj` — NuGet + Copy to Output<br>(3) `MailTriageAssistant/Services/TriageService.cs` — VIP 리스트, 키워드를 설정에서 로드<br>(4) `MailTriageAssistant/Models/TriageSettings.cs` (신규) — 설정 모델 |
| **변경 요약** | (1) `appsettings.json` 생성:<br>```json<br>{<br>  "Triage": {<br>    "VipSenders": ["ceo@company.com", "cto@company.com", "manager@company.com"],<br>    "ActionKeywords": ["요청", "확인", "긴급", "ASAP", "기한", "Due"],<br>    "ApprovalKeywords": ["결재", "상신", "승인요청"],<br>    "MeetingKeywords": ["초대", "Invite", "회의", "미팅", "Zoom", "Teams"],<br>    "NewsletterKeywords": ["구독", "광고", "No-Reply", "News", "Unsubscribe"],<br>    "FyiKeywords": ["참고", "공유", "FYI", "공지"]<br>  },<br>  "Teams": {<br>    "DefaultUserEmail": ""<br>  },<br>  "Outlook": {<br>    "MaxFetchCount": 50,<br>    "MaxBodyLength": 1500<br>  }<br>}<br>```<br>(2) csproj에 `Microsoft.Extensions.Configuration.Json` NuGet 추가 + `<Content Include="appsettings.json"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>`<br>(3) `TriageSettings.cs` POCO 클래스 생성<br>(4) `TriageService` 생성자에 `IOptions<TriageSettings>` 또는 `TriageSettings` 직접 주입<br>(5) 하드코딩된 `static readonly HashSet/string[]` → 설정 기반 인스턴스 필드로 변경 |
| **의존성** | Task 6 (DI 필요) |
| **예상 공수** | M |
| **테스트 명령** | `dotnet build && dotnet test` |

---

### Task 8: Windows Clipboard History 비활성화

| 항목 | 내용 |
|---|---|
| **기능명** | F-05 — Clipboard History 비활성화 (보안 P0) |
| **대상 파일** | `MailTriageAssistant/Services/ClipboardSecurityHelper.cs` |
| **함수명** | `ClipboardSecurityHelper.SecureCopy()` — L18~L28 수정 |
| **변경 요약** | (1) `Clipboard.SetText(redacted)` → `Clipboard.SetDataObject(dataObj)` 로 변경<br>(2) DataObject 생성 시 `ExcludeClipboardContentFromMonitorProcessing` 포맷 추가:<br>```csharp<br>var dataObj = new DataObject();<br>dataObj.SetText(redacted);<br>dataObj.SetData("ExcludeClipboardContentFromMonitorProcessing", "");<br>Clipboard.SetDataObject(dataObj, true);<br>```<br>(3) 이후 30초 타이머의 `Clipboard.GetText()` 비교 로직도 `Clipboard.GetDataObject()?.GetData(DataFormats.Text)` 로 변환 필요 여부 확인 |
| **의존성** | 없음 |
| **예상 공수** | S |
| **테스트 명령** | `dotnet build MailTriageAssistant/` + 수동 검증: Win+V → 히스토리에 미표시 확인 |

---

### Task 9: 카테고리 필터 UI 추가

| 항목 | 내용 |
|---|---|
| **기능명** | F-02 — 카테고리 필터 |
| **대상 파일** | (1) `MailTriageAssistant/MainWindow.xaml` — 좌측 패널 상단에 ComboBox 추가<br>(2) `MailTriageAssistant/ViewModels/MainViewModel.cs` — 필터 로직 추가 |
| **변경 요약** | **(1) MainWindow.xaml**: ListBox 위에 ComboBox 삽입:<br>```xml<br><ComboBox ItemsSource="{Binding CategoryFilters}"<br>          SelectedItem="{Binding SelectedCategoryFilter, Mode=TwoWay}"<br>          Margin="4,0,0,8" /><br>```<br>**(2) MainViewModel.cs**:<br>— `public ObservableCollection<string> CategoryFilters { get; }` = `["전체", "Action", "VIP", "Meeting", "Approval", "FYI", "Newsletter", "Other"]`<br>— `private string _selectedCategoryFilter = "전체";`<br>— `public string SelectedCategoryFilter { get; set; }` (setter에서 `ApplyFilter()` 호출)<br>— `private ICollectionView _emailsView;` : `CollectionViewSource.GetDefaultView(Emails)` 로 초기화<br>— `ApplyFilter()`: `_emailsView.Filter = obj => ...` 카테고리 매칭<br>— ListBox의 `ItemsSource`를 `Emails` → `EmailsView`로 변경 |
| **의존성** | 없음 |
| **예상 공수** | S |
| **테스트 명령** | `dotnet build MailTriageAssistant/` |

---

### Task 10: "Open in Outlook" 버튼 추가

| 항목 | 내용 |
|---|---|
| **기능명** | F-03 — Open in Outlook |
| **대상 파일** | (1) `MailTriageAssistant/Services/IOutlookService.cs` — 메서드 추가<br>(2) `MailTriageAssistant/Services/OutlookService.cs` — 구현 추가<br>(3) `MailTriageAssistant/ViewModels/MainViewModel.cs` — Command 추가<br>(4) `MailTriageAssistant/MainWindow.xaml` — 버튼 추가 |
| **변경 요약** | **(1) IOutlookService.cs**: `Task OpenItem(string entryId);` 추가<br>**(2) OutlookService.cs**:<br>```csharp<br>public Task OpenItem(string entryId) => InvokeAsync(() => OpenItemInternal(entryId));<br><br>private void OpenItemInternal(string entryId)<br>{<br>    EnsureClassicOutlookOrThrow();<br>    object? raw = null;<br>    try<br>    {<br>        raw = _session!.GetItemFromID(entryId);<br>        if (raw is Outlook.MailItem mail)<br>            mail.Display(false);<br>    }<br>    catch (COMException) { /* 에러 처리 */ }<br>    finally { SafeReleaseComObject(raw); }<br>}<br>```<br>**(3) MainViewModel.cs**: `OpenInOutlookCommand = new AsyncRelayCommand(OpenInOutlookAsync, () => SelectedEmail is not null);`<br>**(4) MainWindow.xaml**: 우측 하단 버튼 그리드에 "Open in Outlook" 버튼 추가 |
| **의존성** | 없음 |
| **예상 공수** | S |
| **테스트 명령** | `dotnet build MailTriageAssistant/` |

---

### Task 11: Redaction 이중 적용 정리

| 항목 | 내용 |
|---|---|
| **기능명** | TD-08 — Redaction 이중 마스킹 해결 |
| **대상 파일** | (1) `MailTriageAssistant/Services/ClipboardSecurityHelper.cs`<br>(2) `MailTriageAssistant/Services/DigestService.cs` |
| **함수명** | `ClipboardSecurityHelper.SecureCopy()`, `DigestService.OpenTeams()` |
| **변경 요약** | **현재 흐름 (문제)**:<br>`GenerateDigest()` → 각 항목 Redact 적용 → digest 문자열 생성<br>→ `OpenTeams(digest)` → `SecureCopy(digest)` → **또** Redact 적용 (이중)<br><br>**해결 방안**: `SecureCopy()`에 `bool alreadyRedacted = false` 파라미터 추가:<br>```csharp<br>public void SecureCopy(string text, bool alreadyRedacted = false)<br>{<br>    var content = alreadyRedacted ? (text ?? string.Empty) : _redactionService.Redact(text ?? string.Empty);<br>    // ... 기존 클립보드 로직<br>}<br>```<br>`DigestService.OpenTeams()`에서 `_clipboardHelper.SecureCopy(digest, alreadyRedacted: true);` 호출<br><br>**기존 `CopySelected`** (MainViewModel L353): 원본 `RedactedSummary`를 전달하므로 `alreadyRedacted: true`로 호출하도록 변경 |
| **의존성** | 없음 |
| **예상 공수** | S |
| **테스트 명령** | `dotnet build && dotnet test` |

---

### Task 12: 첨부파일 아이콘 UI 표시

| 항목 | 내용 |
|---|---|
| **기능명** | F-08 — 첨부파일 아이콘 |
| **대상 파일** | `MailTriageAssistant/MainWindow.xaml` |
| **변경 요약** | ListBox ItemTemplate 수정 — Sender/Subject StackPanel 내에 📎 아이콘 추가:<br>```xml<br><StackPanel Grid.Column="1"><br>    <StackPanel Orientation="Horizontal"><br>        <TextBlock Text="{Binding Sender}" FontWeight="SemiBold" TextTrimming="CharacterEllipsis" /><br>        <TextBlock Text=" 📎" FontSize="13"<br>                   Visibility="{Binding HasAttachments, Converter={StaticResource BoolToVis}}" /><br>    </StackPanel><br>    <!-- Subject, ReceivedTime 동일 --><br></StackPanel><br>```<br>우측 상세 패널에도 첨부 여부 TextBlock 추가 |
| **의존성** | 없음 |
| **예상 공수** | S |
| **테스트 명령** | `dotnet build MailTriageAssistant/` |

---

### Task 13: RawEmailHeader init 접근자 변경

| 항목 | 내용 |
|---|---|
| **기능명** | TD-07 — 불변성 강화 |
| **대상 파일** | `MailTriageAssistant/Models/RawEmailHeader.cs` |
| **함수명** | `RawEmailHeader` 클래스 전체 |
| **변경 요약** | 모든 프로퍼티의 `{ get; set; }` → `{ get; init; }` 으로 변경:<br>```csharp<br>public sealed class RawEmailHeader<br>{<br>    public string EntryId { get; init; } = string.Empty;<br>    public string SenderName { get; init; } = string.Empty;<br>    public string SenderEmail { get; init; } = string.Empty;<br>    public string Subject { get; init; } = string.Empty;<br>    public DateTime ReceivedTime { get; init; }<br>    public bool HasAttachments { get; init; }<br>}<br>```<br>객체 생성 후 수정 불가하도록 불변성 보장. `OutlookService.FetchInboxHeadersInternal()`에서 object initializer 사용 확인 (이미 사용 중) |
| **의존성** | 없음 |
| **예상 공수** | S |
| **테스트 명령** | `dotnet build MailTriageAssistant/` |

---

### Task 14: 시스템 트레이 아이콘 구현

| 항목 | 내용 |
|---|---|
| **기능명** | F-01 — 시스템 트레이 |
| **대상 파일** | (1) `MailTriageAssistant/MailTriageAssistant.csproj` — NuGet 추가<br>(2) `MailTriageAssistant/App.xaml` — NotifyIcon 리소스<br>(3) `MailTriageAssistant/App.xaml.cs` — 트레이 아이콘 초기화<br>(4) `MailTriageAssistant/MainWindow.xaml.cs` — 닫기 시 트레이 전환 |
| **변경 요약** | (1) `dotnet add MailTriageAssistant/ package Hardcodet.NotifyIcon.Wpf`<br>(2) 트레이 메뉴 아이템 (사양서 `tray_menu` 준수):<br>&nbsp;&nbsp;— "Status: Idle/Processing" (ReadOnly TextBlock)<br>&nbsp;&nbsp;— "Run Triage Now" → `LoadEmailsCommand` 실행<br>&nbsp;&nbsp;— "Copy Digest to Teams" → `GenerateDigestCommand` 실행<br>&nbsp;&nbsp;— "Open Dashboard" → `MainWindow.Show(); Activate();`<br>&nbsp;&nbsp;— "Exit" → `Application.Current.Shutdown()`<br>(3) MainWindow `Closing` 이벤트에서 `e.Cancel = true; this.Hide();` (트레이로 최소화)<br>(4) 트레이 아이콘 더블클릭 → `MainWindow.Show(); Activate();` |
| **의존성** | Task 6 (DI 권장) |
| **예상 공수** | M |
| **테스트 명령** | `dotnet build MailTriageAssistant/` + 수동: 트레이 아이콘 동작 확인 |

---

### Task 15: 로깅 프레임워크 도입

| 항목 | 내용 |
|---|---|
| **기능명** | TD-04 — 로깅 |
| **대상 파일** | (1) `MailTriageAssistant/MailTriageAssistant.csproj` — NuGet 추가<br>(2) `MailTriageAssistant/App.xaml.cs` — 로깅 구성<br>(3) 모든 Service 파일 — `ILogger<T>` 주입 |
| **변경 요약** | (1) NuGet 패키지 추가:<br>&nbsp;&nbsp;— `Microsoft.Extensions.Logging`<br>&nbsp;&nbsp;— `Serilog.Extensions.Logging`<br>&nbsp;&nbsp;— `Serilog.Sinks.File`<br>(2) `App.xaml.cs`의 `ConfigureServices()`에서 로깅 구성:<br>```csharp<br>Log.Logger = new LoggerConfiguration()<br>    .MinimumLevel.Information()<br>    .WriteTo.File(<br>        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),<br>            "MailTriageAssistant", "logs", "app-.log"),<br>        rollingInterval: RollingInterval.Day)<br>    .CreateLogger();<br>services.AddLogging(builder => builder.AddSerilog());<br>```<br>(3) **본문 로그 금지 규칙** (사양서 준수):<br>&nbsp;&nbsp;— `RedactionService.Redact()` 입출력 로깅 금지<br>&nbsp;&nbsp;— `OutlookService.GetBody()` 결과 로깅 금지<br>&nbsp;&nbsp;— 허용: EntryId, Subject(마스킹 후), 메타데이터, 카테고리, 스코어<br>(4) 각 Service에 `ILogger<ServiceName>` 생성자 주입 + 적절한 로그 포인트 추가 |
| **의존성** | Task 6 (DI 필요) |
| **예상 공수** | M |
| **테스트 명령** | `dotnet build && dotnet test` |

---

## Summary Matrix

| Task | 유형 | 우선순위 | 공수 | 의존성 |
|---|---|---|---|---|
| Task 1 — 테스트 프로젝트 생성 | Tech Debt | 🔴 P0 | S | — |
| Task 2 — RedactionService 테스트 | Tech Debt | 🔴 P0 | S | Task 1 |
| Task 3 — TriageService 테스트 | Tech Debt | 🔴 P0 | S | Task 1 |
| Task 4 — TemplateService 테스트 | Tech Debt | 🟠 P1 | S | Task 1 |
| Task 5 — DigestService 테스트 | Tech Debt | 🟠 P1 | S | Task 1 |
| Task 6 — DI 컨테이너 도입 | Tech Debt | 🟠 P1 | M | — |
| Task 7 — 설정 파일 외부화 | Tech Debt | 🟠 P1 | M | Task 6 |
| Task 8 — Clipboard History 비활성화 | 보안 | 🔴 P0 | S | — |
| Task 9 — 카테고리 필터 UI | 기능 | 🟠 P1 | S | — |
| Task 10 — Open in Outlook 버튼 | 기능 | 🟠 P1 | S | — |
| Task 11 — Redaction 이중 적용 정리 | Tech Debt | 🟠 P1 | S | — |
| Task 12 — 첨부파일 아이콘 | 기능 | 🟡 P2 | S | — |
| Task 13 — RawEmailHeader init | Tech Debt | 🟡 P2 | S | — |
| Task 14 — 시스템 트레이 아이콘 | 기능 | 🟠 P1 | M | Task 6 |
| Task 15 — 로깅 프레임워크 | Tech Debt | 🟠 P1 | M | Task 6 |

---

## Critical Path (병렬 실행 가능 그룹)

```
Group A (독립 — 즉시 병렬 실행 가능):
  ├─ Task 1 → Task 2, 3, 4, 5 (순차)
  ├─ Task 6 → Task 7, 14, 15 (순차)
  ├─ Task 8  (독립)
  ├─ Task 9  (독립)
  ├─ Task 10 (독립)
  ├─ Task 11 (독립)
  ├─ Task 12 (독립)
  └─ Task 13 (독립)
```
