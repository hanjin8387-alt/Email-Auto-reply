# Master Execution Plan — MailTriageAssistant
> Date: 2026-02-15
> Source Reports: 01_code_review, 02_uiux, 03_test_engineering, 04_feature_discovery, 05_perf_reliability, 06_security_privacy
> Target: Codex 확장프로그램 직접 실행용

---

## Executive Summary

| 에이전트 | 발견 항목 | Critical | Major | Minor | Info |
|---|---|---|---|---|---|
| 01 Code Review | 28 | 6 | 8 | 9 | 5 |
| 02 UI/UX | 22 | 5 | 8 | 6 | 3 |
| 03 Test Engineering | — | — | — | — | — |
| 04 Feature Discovery | 18 features + 8 tech debt | — | — | — | — |
| 05 Perf & Reliability | 15 | 4 | 5 | 4 | 2 |
| 06 Security & Privacy | 16 | 4 | 6 | 4 | 2 |
| **합계** | **~97** | **19** | **27** | **23** | **12** |

**중복 제거 후 고유 커밋 단위: 47건** (아래 Phase별 분해)

---

## 우선순위 분류 기준

| 등급 | 정의 | 기준 |
|---|---|---|
| **P0** | 즉시 수정 | 보안 취약점 · 데이터 유출 · 빌드 실패 · 앱 크래시 |
| **P1** | 다음 릴리즈 필수 | 아키텍처 결함 · 성능 병목 · 사양서 미달성 · 테스트 인프라 |
| **P2** | 품질 향상 | UX 개선 · 코드 스타일 · 부가 기능 · 기술 부채 |

---

## Phase 0: 인프라 (테스트 프로젝트 + DI)

> 다른 모든 Phase의 선행 조건. 테스트 없이 수정 불가, DI 없이 리팩토링 불가.

### Commit 0-1: 테스트 프로젝트 초기화
| 항목 | 내용 |
|---|---|
| **우선순위** | P0 |
| **근거** | 테스트 없으면 회귀 검증 불가. 모든 수정의 선행 조건 |
| **대상 파일** | `MailTriageAssistant.Tests/MailTriageAssistant.Tests.csproj` (신규) |
| **변경 요지** | xUnit + Moq + FluentAssertions 프로젝트 생성, `net8.0-windows` + `UseWPF`, 프로젝트 참조 설정 |
| **명령** | `dotnet new xunit -n MailTriageAssistant.Tests && cd MailTriageAssistant.Tests && dotnet add reference ../MailTriageAssistant/MailTriageAssistant.csproj && dotnet add package Moq && dotnet add package FluentAssertions` |
| **테스트** | `dotnet build MailTriageAssistant.Tests/` |
| **커밋** | `[03] test: 테스트 프로젝트 초기화 (xUnit + Moq + FluentAssertions)` |

### Commit 0-2: RedactionService 단위 테스트 12건
| 항목 | 내용 |
|---|---|
| **우선순위** | P0 |
| **근거** | 보안 핵심 서비스. 패턴 수정(Phase 1) 전 기준선 확보 |
| **대상 파일** | `MailTriageAssistant.Tests/Services/RedactionServiceTests.cs` (신규) |
| **변경 요지** | 4개 PII 패턴(Phone/SSN/Card/Email) × 단일/복합/빈/null, 패턴 우선순위, 비매칭 |
| **테스트** | `dotnet test --filter "FullyQualifiedName~RedactionServiceTests"` |
| **커밋** | `[03] test: RedactionService 단위 테스트 12건 추가` |

### Commit 0-3: TriageService 단위 테스트 16건
| 항목 | 내용 |
|---|---|
| **우선순위** | P0 |
| **근거** | 분류 로직 정확성 = 제품 가치 |
| **대상 파일** | `MailTriageAssistant.Tests/Services/TriageServiceTests.cs` (신규) |
| **변경 요지** | 7개 카테고리 분류, 점수 경계값, VIP+Action 누적, Tags, ActionHint |
| **테스트** | `dotnet test --filter "FullyQualifiedName~TriageServiceTests"` |
| **커밋** | `[03] test: TriageService 단위 테스트 16건 추가` |

### Commit 0-4: DigestService + TemplateService + ScoreToColorConverter 테스트
| 항목 | 내용 |
|---|---|
| **우선순위** | P1 |
| **대상 파일** | `Tests/Services/DigestServiceTests.cs`, `Tests/Services/TemplateServiceTests.cs`, `Tests/Helpers/ScoreToColorConverterTests.cs` (신규 3파일) |
| **변경 요지** | DigestService 11건, TemplateService 12건, ScoreToColorConverter 10건 = 33건 |
| **테스트** | `dotnet test --verbosity normal` |
| **커밋** | `[03] test: DigestService·TemplateService·ScoreToColorConverter 테스트 33건 추가` |

### Commit 0-5: DI 컨테이너 도입
| 항목 | 내용 |
|---|---|
| **우선순위** | P1 |
| **근거** | IDialogService 추출, IDisposable 관리, 팩토리 패턴 등 후속 리팩토링의 기반 |
| **대상 파일** | `.csproj` (NuGet 추가), `App.xaml.cs`, `App.xaml`, `MainWindow.xaml.cs` |
| **변경 요지** | `Microsoft.Extensions.DependencyInjection` → ServiceCollection 구성 → `StartupUri` 제거 → `OnStartup`에서 resolve |
| **테스트** | `dotnet build && dotnet test` |
| **커밋** | `[04] refactor: DI 컨테이너 도입 (Microsoft.Extensions.DependencyInjection)` |

---

## Phase 1: 보안 (P0 — Critical)

> 데이터 유출 경로 차단. Phase 0 이후 즉시 실행.

### Commit 1-1: PII 패턴 확장 (계좌·여권·IP·URL 토큰·공백 카드)
| 항목 | 내용 |
|---|---|
| **우선순위** | P0 — CVSS 7.5 |
| **근거** | 미구현 PII가 마스킹 없이 클립보드·Digest·UI 전달 |
| **출처** | 06-S1, 01-C3, 06-S11 |
| **대상 파일** | `Services/RedactionService.cs` |
| **변경 요지** | Rules 배열에 6종 패턴 추가 (ACCOUNT, PASSPORT, IP, URL_TOKEN, 공백카드, 하이픈없는SSN), 기존 패턴 변형 보강, 순서 재정렬 |
| **테스트** | `dotnet test --filter "FullyQualifiedName~RedactionServiceTests"` — 기존 12건 통과 + 새 패턴 테스트 추가 |
| **커밋** | `[06] security: PII 마스킹 패턴 6종 추가 (계좌·여권·IP·URL 토큰)` |

### Commit 1-2: 유니코드 정규화 (전각 숫자 우회 차단)
| 항목 | 내용 |
|---|---|
| **우선순위** | P0 — CVSS 7.0 |
| **근거** | 전각 숫자 입력 시 기존 패턴도 우회됨 |
| **출처** | 06-S2 |
| **대상 파일** | `Services/RedactionService.cs` |
| **변경 요지** | `Redact()` 진입 시 `NormalizeToAsciiDigits()` 전처리 추가 (전각→반각, NormalizationForm.FormKC) |
| **테스트** | `Redact("０１０-１２３４-５６７８")` → `"[PHONE]"` |
| **커밋** | `[06] security: 유니코드 정규화 적용 (전각 숫자 우회 차단)` |

### Commit 1-3: Win+V 클립보드 히스토리 방어
| 항목 | 내용 |
|---|---|
| **우선순위** | P0 — CVSS 7.0 |
| **근거** | 30초 삭제만으로는 Win+V 히스토리에 잔존 |
| **출처** | 06-S3, 04-FE011 |
| **대상 파일** | `Services/ClipboardSecurityHelper.cs` |
| **변경 요지** | `Clipboard.SetText()` → `Clipboard.SetDataObject(dataObj, false)` + `ExcludeClipboardContentFromMonitorProcessing` 포맷 추가 |
| **테스트** | `dotnet build` + 수동: Win+V에 미표시 확인 |
| **커밋** | `[06] security: Win+V 클립보드 히스토리 노출 방어` |

### Commit 1-4: XAML 바인딩 PII 마스킹 (Sender/Subject)
| 항목 | 내용 |
|---|---|
| **우선순위** | P0 — CVSS 7.0 |
| **근거** | UI에 원본 이메일·이름 노출 |
| **출처** | 06-S4 |
| **대상 파일** | `Helpers/RedactionConverter.cs` (신규), `MainWindow.xaml` |
| **변경 요지** | `RedactionConverter : IValueConverter` 생성 → Sender/Subject 바인딩에 적용 |
| **테스트** | `dotnet build` + 앱 실행 시 이메일 주소 `[EMAIL]` 표시 확인 |
| **커밋** | `[06] security: XAML 바인딩에 PII 마스킹 컨버터 적용` |

### Commit 1-5: DispatcherTimer 누수 수정
| 항목 | 내용 |
|---|---|
| **우선순위** | P0 |
| **근거** | 매 복사마다 타이머 누수 → GC 불가 |
| **출처** | 01-C6, 05-m02 |
| **대상 파일** | `Services/ClipboardSecurityHelper.cs` |
| **변경 요지** | 단일 인스턴스 재사용 (Stop→Start 패턴) + IDisposable 구현 |
| **테스트** | `dotnet build && dotnet test` |
| **커밋** | `[01] fix: ClipboardSecurityHelper DispatcherTimer 누수 수정` |

### Commit 1-6: OutlookService IDisposable 구현
| 항목 | 내용 |
|---|---|
| **우선순위** | P0 |
| **근거** | STA 스레드 + COM 객체 미해제 → 앱 종료 시 리소스 잔존 |
| **출처** | 01-C1 |
| **대상 파일** | `Services/OutlookService.cs` |
| **변경 요지** | `IDisposable` 구현 → Dispose에서 ResetConnection + InvokeShutdown + Thread Join |
| **테스트** | `dotnet build` |
| **커밋** | `[01] fix: OutlookService에 IDisposable 구현` |

### Commit 1-7: ex.Message 직접 노출 제거
| 항목 | 내용 |
|---|---|
| **우선순위** | P0 |
| **근거** | 예외 메시지에 본문 포함 가능성 → UI 노출 |
| **출처** | 06-S5, 01-C5 (MessageBox 분리는 Phase 2) |
| **대상 파일** | `ViewModels/MainViewModel.cs` |
| **변경 요지** | 8곳의 `StatusMessage = ex.Message` → 사전 정의 상수 메시지로 교체 |
| **테스트** | `dotnet build && dotnet test` |
| **커밋** | `[06] security: 예외 메시지 직접 노출 대신 사전 정의 메시지 사용` |

### Commit 1-8: Markdown 인젝션 방어 (EscapeCell 강화)
| 항목 | 내용 |
|---|---|
| **우선순위** | P1 |
| **출처** | 06-S7 |
| **대상 파일** | `Services/DigestService.cs` |
| **변경 요지** | `EscapeCell()`에 `[`, `]`, `(`, `)`, `!`, `<`, `>` 이스케이프 추가 |
| **테스트** | `dotnet test --filter "DigestServiceTests"` |
| **커밋** | `[06] security: Markdown 인젝션 방어 (EscapeCell 특수문자 이스케이프)` |

### Commit 1-9: 템플릿 입력 검증 (중괄호 제거 + 길이 제한)
| 항목 | 내용 |
|---|---|
| **우선순위** | P1 |
| **출처** | 06-S8 |
| **대상 파일** | `Services/TemplateService.cs` |
| **변경 요지** | 값에서 `{`, `}` 제거, 200자 제한, `___` → `[미입력]` |
| **테스트** | `dotnet test --filter "TemplateServiceTests"` |
| **커밋** | `[06] security: 템플릿 값 검증 (중괄호 제거, 길이 제한)` |

### Commit 1-10: COM 타임아웃 30초 + 동기화 lock
| 항목 | 내용 |
|---|---|
| **우선순위** | P1 |
| **출처** | 05-M05, 06-S9, 06-S10 |
| **대상 파일** | `Services/OutlookService.cs` |
| **변경 요지** | `InvokeAsync`에 `Task.WhenAny(task, Task.Delay(30s))` + `_comLock` 잠금 추가 |
| **테스트** | `dotnet build` |
| **커밋** | `[05] reliability: COM 타임아웃 30초 + 동기화 lock 추가` |

---

## Phase 2: 성능 + 아키텍처 (P1)

### Commit 2-1: FetchInboxHeaders → Restrict + GetFirst/GetNext
| 항목 | 내용 |
|---|---|
| **우선순위** | P1 |
| **근거** | 전체 Inbox Sort → O(n²) 인덱서 접근, 대량 Inbox 시 수 초 지연 |
| **출처** | 05-C01, 05-C02 |
| **대상 파일** | `Services/OutlookService.cs` |
| **변경 요지** | `Items.Sort` 제거 → `Items.Restrict("[ReceivedTime] >= '7일전'")` → `GetFirst()/GetNext()` 순차 열거 → C# 측 정렬 |
| **테스트** | `dotnet build` + 수동: 50개 이메일 로드 확인 |
| **커밋** | `[05] perf: FetchInboxHeaders에 Restrict+GetFirst/GetNext 적용` |

### Commit 2-2: 개별 아이템 partial failure 허용
| 항목 | 내용 |
|---|---|
| **우선순위** | P1 |
| **출처** | 05-C03 (Reliability Matrix) |
| **대상 파일** | `Services/OutlookService.cs` |
| **변경 요지** | while 루프 내부에 per-item try-catch → 실패 항목 스킵, 나머지 계속 |
| **테스트** | `dotnet build` |
| **커밋** | `[05] reliability: FetchInboxHeaders 개별 아이템 partial failure 허용` |

### Commit 2-3: ObservableCollection Batch 갱신
| 항목 | 내용 |
|---|---|
| **우선순위** | P1 |
| **출처** | 01-M3, 05-M02 |
| **대상 파일** | `Helpers/RangeObservableCollection.cs` (신규), `ViewModels/MainViewModel.cs` |
| **변경 요지** | `AddRange()` 1회 호출 → CollectionChanged 1회 |
| **테스트** | `dotnet build && dotnet test` |
| **커밋** | `[05] perf: RangeObservableCollection으로 Batch 갱신` |

### Commit 2-4: IDialogService 추출 (MVVM MessageBox 분리)
| 항목 | 내용 |
|---|---|
| **우선순위** | P1 |
| **근거** | 테스트 불가능 + MVVM 위반 |
| **출처** | 01-C5, 02-M3 |
| **대상 파일** | `Services/IDialogService.cs` (신규), `Services/WpfDialogService.cs` (신규), `ViewModels/MainViewModel.cs` |
| **변경 요지** | 인터페이스 + 구현체 생성 → ViewModel 8곳 교체 |
| **테스트** | `dotnet build && dotnet test` |
| **커밋** | `[01] refactor: IDialogService 추출 (MVVM MessageBox 분리)` |

### Commit 2-5: fire-and-forget 안전 래퍼 도입
| 항목 | 내용 |
|---|---|
| **우선순위** | P1 |
| **출처** | 01-C4 |
| **대상 파일** | `Helpers/TaskExtensions.cs` (신규), `ViewModels/MainViewModel.cs` |
| **변경 요지** | `SafeFireAndForget(onException)` 확장 메서드 → SelectedEmail setter 적용 |
| **테스트** | `dotnet build` |
| **커밋** | `[01] fix: fire-and-forget 안전 래퍼 (SafeFireAndForget) 도입` |

### Commit 2-6: New Outlook 프로세스 검사 캐싱
| 항목 | 내용 |
|---|---|
| **우선순위** | P1 |
| **출처** | 01-M2, 05-M01 |
| **대상 파일** | `Services/OutlookService.cs` |
| **변경 요지** | `_newOutlookChecked` 플래그 → 최초 1회만 검사, ResetConnection 시 리셋 |
| **테스트** | `dotnet build` |
| **커밋** | `[05] perf: New Outlook 프로세스 검사 1회 캐싱` |

### Commit 2-7: Top-10 본문 백그라운드 프리페치
| 항목 | 내용 |
|---|---|
| **우선순위** | P1 |
| **근거** | Digest 생성 시 순차 GetBody ×10 → ~2초 |
| **출처** | 05-C03 |
| **대상 파일** | `ViewModels/MainViewModel.cs` |
| **변경 요지** | `LoadEmailsAsync` 완료 후 `PrefetchTopBodiesAsync()` fire-and-forget |
| **테스트** | `dotnet build` |
| **커밋** | `[05] perf: Top-10 본문 백그라운드 프리페치` |

### Commit 2-8: VIP 리스트 + 키워드 설정 외부화 (appsettings.json)
| 항목 | 내용 |
|---|---|
| **우선순위** | P1 |
| **출처** | 01-M4, 01-M5, 04-TD03 |
| **대상 파일** | `appsettings.json` (신규), `Models/TriageSettings.cs` (신규), `Services/TriageService.cs`, `.csproj` |
| **변경 요지** | 하드코딩 VIP/키워드/매직넘버 → JSON 설정 파일 + DI Options 패턴 |
| **테스트** | `dotnet build && dotnet test` |
| **커밋** | `[04] refactor: VIP·키워드·점수 가중치를 appsettings.json으로 외부화` |

### Commit 2-9: DigestService 이중 Redact 방지
| 항목 | 내용 |
|---|---|
| **우선순위** | P1 |
| **출처** | 04-TD08, 05-m01 |
| **대상 파일** | `Services/DigestService.cs`, `Services/ClipboardSecurityHelper.cs` |
| **변경 요지** | `SecureCopy(text, alreadyRedacted: true)` 파라미터 + RedactedSummary 재마스킹 제거 |
| **테스트** | `dotnet test --filter "DigestServiceTests"` |
| **커밋** | `[05] fix: DigestService 이중 Redact 방지` |

### Commit 2-10: Dead Code 제거 (TemplateService.SendDraft)
| 항목 | 내용 |
|---|---|
| **우선순위** | P1 |
| **출처** | 01-M7 |
| **대상 파일** | `Services/TemplateService.cs` |
| **변경 요지** | 미사용 `SendDraft` 메서드 + `using System.Threading.Tasks` 제거 |
| **테스트** | `dotnet build` |
| **커밋** | `[01] fix: TemplateService.SendDraft dead code 제거` |

---

## Phase 3: UI/UX (P1~P2)

### Commit 3-1: App.xaml 색상 리소스 딕셔너리
| 항목 | 내용 |
|---|---|
| **우선순위** | P1 |
| **출처** | 02-M1, 02-M2 |
| **대상 파일** | `App.xaml` |
| **변경 요지** | 15개 하드코딩 hex 색상 → `SolidColorBrush` 리소스 정의 |
| **커밋** | `[02] ui: App.xaml에 색상 리소스 딕셔너리 정의` |

### Commit 3-2: MainWindow.xaml 색상 → StaticResource 교체
| 항목 | 내용 |
|---|---|
| **대상 파일** | `MainWindow.xaml` |
| **변경 요지** | 15개 인라인 색상 → `StaticResource` 참조 교체 |
| **커밋** | `[02] ui: MainWindow.xaml 하드코딩 색상을 StaticResource로 교체` |

### Commit 3-3: 접근성 (AutomationProperties + TabIndex + ToolTip)
| 항목 | 내용 |
|---|---|
| **우선순위** | P0 (접근성) |
| **출처** | 02-C1, 02-C5 |
| **대상 파일** | `MainWindow.xaml` |
| **변경 요지** | 8개 인터랙티브 요소에 `AutomationProperties.Name` + `TabIndex` + `ToolTip` 일괄 추가 |
| **커밋** | `[02] ui: 접근성 속성 추가 (AutomationProperties, TabIndex, ToolTip)` |

### Commit 3-4: ScoreToColor WCAG 대비율 + ScoreToLabel 컨버터
| 항목 | 내용 |
|---|---|
| **우선순위** | P0 (색각이상자 대응) |
| **출처** | 02-C2 |
| **대상 파일** | `Helpers/ScoreToColorConverter.cs`, `Helpers/ScoreToLabelConverter.cs` (신규), `MainWindow.xaml` |
| **변경 요지** | 색상 WCAG 4.5:1+ 교체 + 긴급/중요/보통/참고 텍스트 레이블 추가 |
| **커밋** | `[02] ui: 점수 색상 WCAG 준수 + 긴급도 텍스트 레이블 추가` |

### Commit 3-5: 빈 상태(Empty State) 오버레이 2종
| 항목 | 내용 |
|---|---|
| **출처** | 02-C3, 02-m6 |
| **대상 파일** | `MainWindow.xaml` |
| **변경 요지** | 이메일 리스트 빈 상태 안내 + 상세 패널 미선택 Placeholder |
| **커밋** | `[02] ui: 빈 리스트+미선택 Empty State 오버레이 추가` |

### Commit 3-6: 이메일 선택 시 본문 로딩 ProgressBar
| 항목 | 내용 |
|---|---|
| **출처** | 02-C4 |
| **대상 파일** | `ViewModels/MainViewModel.cs` |
| **변경 요지** | `LoadSelectedEmailBodyAsync`에 `IsLoading = true/false` 추가 |
| **커밋** | `[02] ui: 이메일 선택 시 본문 로딩 ProgressBar 표시` |

### Commit 3-7: 영어 UI 레이블 한국어 통일
| 항목 | 내용 |
|---|---|
| **출처** | 02-M4, 02-m1~m3 |
| **대상 파일** | `MainWindow.xaml` |
| **변경 요지** | 9개 영어 문자열 → 한국어 교체 (타이틀, 버튼, 섹션 헤더) |
| **커밋** | `[02] ui: 영어 UI 레이블을 한국어로 통일` |

### Commit 3-8: 카테고리 아이콘 뱃지 + ListBox 커스텀 스타일
| 항목 | 내용 |
|---|---|
| **출처** | 02-M8, 02-m5 |
| **대상 파일** | `Helpers/CategoryToIconConverter.cs` (신규), `MainWindow.xaml` |
| **변경 요지** | CategoryToIconConverter + ListBoxItem 선택/호버 커스텀 스타일 |
| **커밋** | `[02] ui: 카테고리 아이콘 뱃지 + ListBox 커스텀 스타일` |

### Commit 3-9: 레이아웃 정리 (패딩 통일 + TextTrimming + 가상화)
| 항목 | 내용 |
|---|---|
| **출처** | 02-m4, 02-M5, 05-M03 |
| **대상 파일** | `MainWindow.xaml` |
| **변경 요지** | Padding 12 통일, 메타데이터 TextTrimming, VirtualizingStackPanel 속성 추가 |
| **커밋** | `[02] ui: 레이아웃 정리 (패딩·TextTrimming·가상화)` |

---

## Phase 4: 부가 기능 (P1~P2)

### Commit 4-1: 카테고리 필터 UI
| 항목 | 내용 |
|---|---|
| **우선순위** | P1 |
| **출처** | 04-FE008, 04-Task9 |
| **대상 파일** | `MainWindow.xaml`, `ViewModels/MainViewModel.cs` |
| **변경 요지** | ComboBox + ICollectionView Filter |
| **커밋** | `[04] feat: 카테고리 필터 UI 추가` |

### Commit 4-2: "Outlook에서 열기" 버튼
| 항목 | 내용 |
|---|---|
| **우선순위** | P1 |
| **출처** | 04-FE009, 04-Task10 |
| **대상 파일** | `Services/IOutlookService.cs`, `Services/OutlookService.cs`, `ViewModels/MainViewModel.cs`, `MainWindow.xaml` |
| **변경 요지** | `OpenItem(entryId)` → Inspector 열기 |
| **커밋** | `[04] feat: "Outlook에서 열기" 버튼 추가` |

### Commit 4-3: 첨부파일 📎 아이콘 표시
| 항목 | 내용 |
|---|---|
| **우선순위** | P2 |
| **출처** | 04-FE016, 04-Task12 |
| **대상 파일** | `MainWindow.xaml` |
| **변경 요지** | `HasAttachments` BoolToVis 바인딩 📎 아이콘 |
| **커밋** | `[04] feat: 첨부파일 아이콘 표시` |

### Commit 4-4: RawEmailHeader/ReplyTemplate init 접근자 변경
| 항목 | 내용 |
|---|---|
| **우선순위** | P2 |
| **출처** | 01-m1, 01-m2, 04-TD07 |
| **대상 파일** | `Models/RawEmailHeader.cs`, `Models/ReplyTemplate.cs` |
| **변경 요지** | `set` → `init` 변경 |
| **커밋** | `[01] refactor: RawEmailHeader·ReplyTemplate init 접근자로 변경` |

### Commit 4-5: 매직 넘버 상수화 (OutlookService + ScoreToColorConverter)
| 항목 | 내용 |
|---|---|
| **우선순위** | P2 |
| **출처** | 01-m8, 01-m9, 01-m4 |
| **대상 파일** | `Services/OutlookService.cs`, `Helpers/ScoreToColorConverter.cs` |
| **변경 요지** | `MaxFetchCount=50`, `MaxBodyLength=1500`, 색상 임계값 상수화 |
| **커밋** | `[01] refactor: 매직 넘버 상수 추출` |

### Commit 4-6: userEmail 입력 검증
| 항목 | 내용 |
|---|---|
| **우선순위** | P2 |
| **출처** | 06-S14 |
| **대상 파일** | `Services/DigestService.cs` |
| **변경 요지** | 이메일 정규식 검증 → 실패 시 기본 Teams URL |
| **커밋** | `[06] security: userEmail 입력 검증` |

### Commit 4-7: NuGet 취약점 검사 + Audit 설정
| 항목 | 내용 |
|---|---|
| **우선순위** | P2 |
| **출처** | 06-S13 |
| **대상 파일** | `MailTriageAssistant.csproj` |
| **변경 요지** | `<NuGetAudit>true</NuGetAudit>` + `dotnet list package --vulnerable` 실행 |
| **커밋** | `[06] security: NuGet Audit 활성화` |

---

## Phase 5: 계측 + 최종 검증

### Commit 5-1: Stopwatch 성능 계측 (#if DEBUG)
| 항목 | 내용 |
|---|---|
| **출처** | 05-Task11 |
| **대상 파일** | `Services/OutlookService.cs`, `ViewModels/MainViewModel.cs` |
| **변경 요지** | 주요 4개 메서드에 `#if DEBUG Stopwatch` 삽입. **본문 내용 절대 출력 금지** |
| **커밋** | `[05] perf: 주요 메서드 Stopwatch 계측 코드 삽입` |

### Commit 5-2: 보안 역테스트 (마스킹 우회 시도)
| 항목 | 내용 |
|---|---|
| **출처** | 06-Task14 |
| **대상 파일** | `Tests/Security/RedactionSecurityTests.cs` (신규) |
| **변경 요지** | 전각 숫자, 계좌, 여권, Markdown/Template 인젝션 역테스트 |
| **커밋** | `[06] test: 보안 역테스트 (마스킹 우회 시도 실패 확인)` |

### Commit 5-3: 빌드 최적화 (PublishTrimmed + SingleFile)
| 항목 | 내용 |
|---|---|
| **우선순위** | P2 |
| **출처** | 05-m03 |
| **대상 파일** | `MailTriageAssistant.csproj` |
| **변경 요지** | `PublishTrimmed + SingleFile + SelfContained + TrimMode=partial`. COM Interop TrimmerRoot 설정 |
| **커밋** | `[05] build: PublishTrimmed + SingleFile 설정` |

### Commit 5-4: Banned API Analyzer (Console/Debug Write 금지)
| 항목 | 내용 |
|---|---|
| **우선순위** | P2 |
| **출처** | 06-S15 |
| **대상 파일** | `.csproj`, `BannedSymbols.txt` (신규) |
| **변경 요지** | `Microsoft.CodeAnalysis.BannedApiAnalyzers` 추가 → Console/Debug/Trace Write 빌드 경고 |
| **커밋** | `[06] security: Banned API Analyzer 추가 (PII 로그 방지)` |

---

## 테스트 전략

### 단위 테스트 (Unit)
```bash
# 전체 실행
dotnet test --verbosity normal

# 서비스별 필터
dotnet test --filter "FullyQualifiedName~RedactionServiceTests"
dotnet test --filter "FullyQualifiedName~TriageServiceTests"
dotnet test --filter "FullyQualifiedName~DigestServiceTests"
dotnet test --filter "FullyQualifiedName~TemplateServiceTests"
dotnet test --filter "FullyQualifiedName~ScoreToColorConverterTests"
dotnet test --filter "FullyQualifiedName~MainViewModelTests"
dotnet test --filter "FullyQualifiedName~RedactionSecurityTests"

# 커버리지 측정
dotnet test --collect:"XPlat Code Coverage"
```

### 통합 테스트 (Integration)
- `MainViewModelTests` — `Mock<IOutlookService>` + 실제 서비스 조합
- COM Interop 통합 테스트는 수동 검증 (Outlook 실행 필요)

### E2E / 수동 검증 체크리스트
- [ ] Classic Outlook 실행 → "메일 분류 실행" → 50개 이메일 표시
- [ ] 이메일 선택 → ProgressBar → 마스킹된 본문 표시
- [ ] Sender/Subject에 원본 이메일 주소 미노출 (`[EMAIL]` 표시)
- [ ] "Digest 복사 & Teams 열기" → Teams 열림 or 폴백 MessageBox
- [ ] Win+V → 클립보드 히스토리에 Digest 미표시
- [ ] 30초 후 클립보드 비워짐
- [ ] New Outlook(olk.exe) 실행 → 에러 메시지 출력
- [ ] Outlook 미실행 → 15초 이내 에러 메시지
- [ ] 카테고리 필터 → 선택 카테고리만 표시
- [ ] 텔플릿 답장 → Outlook 초안 생성
- [ ] 빈 Inbox → Empty State 안내 표시
- [ ] 키보드 Tab 탐색 → 논리적 순서
- [ ] 창 크기 줄임 → 컨텐츠 잘림 없음

---

## 롤백 / 가드레일

### 롤백 기준
| 조건 | 대응 |
|---|---|
| `dotnet build` 실패 | 즉시 `git revert HEAD` |
| 기존 테스트 실패 (`dotnet test` 빨간색) | 즉시 롤백 후 원인 분석 |
| 보안 불변 규칙 위반 | 즉시 롤백 (본문 로그, 클립보드 미삭제 등) |
| UI 렌더링 깨짐 | 해당 커밋만 롤백 |
| COM 크래시 (AccessViolation) | Phase 2 COM 관련 커밋 전체 롤백 |

### 기능 플래그 (Feature Flags)
- **카테고리 필터**: `appsettings.json`에 `"EnableCategoryFilter": true` 추가 → false 시 ComboBox 숨김
- **본문 프리페치**: `appsettings.json`에 `"EnablePrefetch": true` → false 시 on-demand만 유지
- **XAML PII 마스킹**: `RedactionConverter` 적용 여부를 debug 빌드에서 토글 가능

### 단계적 적용 순서
```
Phase 0 (인프라)    → 빌드+테스트 통과 확인 → Phase 1 진입
Phase 1 (보안)      → 보안 역테스트 통과 → Phase 2 진입
Phase 2 (성능+아키) → 전체 테스트 + 수동 검증 → Phase 3 진입
Phase 3 (UI/UX)     → 시각 확인 + 접근성 검증 → Phase 4 진입
Phase 4 (부가기능)  → 기능 테스트 → Phase 5 진입
Phase 5 (최종)      → 최종 검증 + 배포 준비
```

### 관측 지표
| 지표 | 측정 방법 | 목표 |
|---|---|---|
| 빌드 성공률 | `dotnet build` exit code | 100% |
| 테스트 통과율 | `dotnet test` (전체 62건+) | 100% |
| 헤더 로드 시간 | `[PERF] FetchInboxHeaders` Debug 로그 | < 1000ms (50건) |
| Digest 생성 시간 | `[PERF] GenerateDigestAsync` Debug 로그 | < 500ms (프리페치 후) |
| PII 마스킹 누락 | 보안 역테스트 | 0건 |

---

## Codex Instructions (체크리스트)

> Codex가 각 커밋 실행 시 반드시 지켜야 할 규칙

### 🔴 절대 위반 금지
- [ ] 이메일 본문을 `Console.WriteLine`, `Debug.WriteLine`, `Trace.Write`로 출력하지 않음
- [ ] 이메일 본문을 디스크(파일/DB/temp)에 저장하지 않음
- [ ] `ex.Message`를 StatusMessage나 MessageBox에 직접 노출하지 않음 (사전 정의 상수만 사용)
- [ ] 외부 AI API를 호출하지 않음
- [ ] `Clipboard.SetText()` 대신 히스토리 제외 방식 사용

### 🟡 커밋 규칙
- [ ] 커밋 당 단일 관심사 (Single Concern)
- [ ] 커밋 당 최대 5개 파일, 200줄 이하
- [ ] 커밋 메시지 형식: `[에이전트번호] 카테고리: 한줄 설명`
- [ ] 카테고리: `fix`, `feat`, `refactor`, `test`, `ui`, `perf`, `reliability`, `security`, `build`

### 🔵 빌드/테스트
- [ ] 매 커밋 전 `dotnet build MailTriageAssistant/MailTriageAssistant.csproj` → 성공 확인
- [ ] 매 커밋 전 `dotnet test` → 전체 통과 확인 (Phase 0 이후)
- [ ] 새 기능/수정에 대한 단위 테스트를 동일 커밋 or 직전 커밋에 포함

### 🟢 코드 스타일
- [ ] `#nullable enable` 일관 유지
- [ ] `ConfigureAwait(true)` — UI 바인딩 갱신이 필요한 경우
- [ ] `ConfigureAwait(false)` — 서비스 내부 비동기 호출
- [ ] COM 객체는 반드시 `SafeReleaseComObject()` + finally 블록에서 해제
- [ ] 매직 넘버 사용 금지 (const로 추출)
- [ ] 한국어 UI 문자열은 주석에 영어 설명 추가

### 🟣 Phase 전환 조건
- [ ] Phase 0 → Phase 1: `dotnet test` 62건+ 전체 통과
- [ ] Phase 1 → Phase 2: 보안 Critical 0건 잔여, 역테스트 통과
- [ ] Phase 2 → Phase 3: `dotnet build` 경고 0건, 전체 테스트 통과
- [ ] Phase 3 → Phase 4: 접근성 체크리스트 완료
- [ ] Phase 4 → Phase 5: 기능 테스트 + 수동 검증 완료
- [ ] 전체 완료: E2E 수동 검증 체크리스트 전체 ✅
