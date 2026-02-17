# Observability & Analytics Report — MailTriageAssistant
> Date: 2026-02-13
> Reviewer: Agent 05 (Observability & Analytics)

## Summary
- 로깅 포인트 현황: **구성됨, 미활용**
- 메트릭 수집: **기초** (PerfEventSource 존재, 호출 없음)
- 이벤트 스키마: **미정의**
- 총 이슈: 10 | Critical: 1 | Major: 4 | Minor: 3 | Info: 2

## Current State

### 로깅
- Serilog 패키지 설치됨: `Serilog 4.3.1`, `Serilog.Extensions.Logging 10.0.0`, `Serilog.Sinks.File 7.0.0`
- `App.xaml.cs`에 **Serilog 구성 코드 없음** — 패키지만 설치된 상태
- `ILogger<T>` 주입이 **어떤 서비스에도 없음**
- `BannedSymbols.txt`로 `Console.Write`/`Debug.Write` 금지 규칙 존재 ✅

### 메트릭
- `Helpers/PerfEventSource.cs` (518 바이트) — ETW EventSource 클래스 존재
- **호출 코드 없음** — 어디서도 이벤트를 발생시키지 않음

### 에러 리포팅
- `App.xaml.cs:OnDispatcherUnhandledException`: 글로벌 핸들러 — 안전한 메시지만 표시 ✅
- 예외 정보를 어디에도 기록하지 않음 ❌

---

## Findings

### 🔴 Critical

| # | 영역 | 파일 | 이슈 | 권장사항 |
|---|---|---|---|---|
| C-01 | 로깅 | `App.xaml.cs` | Serilog 패키지가 설치되었으나 **구성·사용 코드 전무**. 운영 시 디버깅 정보 0 | Serilog `WriteTo.File()` 구성 + 서비스별 `ILogger<T>` 주입 |

### 🟡 Major

| # | 영역 | 파일 | 이슈 | 권장사항 |
|---|---|---|---|---|
| M-01 | 메트릭 | `Helpers/PerfEventSource.cs` | ETW EventSource가 정의만 되고 **호출 없음**. 성능 측정 불가 | 주요 서비스 메서드(FetchInboxHeaders, GenerateDigest)에 이벤트 발생 코드 삽입 |
| M-02 | 에러 기록 | `App.xaml.cs:61-69` | `OnDispatcherUnhandledException`에서 예외 정보를 **로깅하지 않음**. 재현 불가능한 버그 추적 불가 | `Log.Error(e.Exception, "Unhandled")` 추가 (PII 필터 적용) |
| M-03 | 서비스 로깅 | `Services/*.cs` 전체 | 7개 서비스 중 `ILogger<T>` 주입 0개. 서비스 호출 시작/완료/에러 로그 없음 | 각 서비스 생성자에 `ILogger<ServiceName>` 주입 + 적절한 로그 포인트 |
| M-04 | 사용자 행동 | `MainViewModel.cs` | 사용자 행동 로그 없음 (어떤 기능을 몇 번 사용했는지 추적 불가) | 세션 통계와 연계: `LoadEmails`, `GenerateDigest`, `Reply`, `OpenInOutlook` 카운터 |

### 🟢 Minor

| # | 영역 | 파일 | 이슈 | 권장사항 |
|---|---|---|---|---|
| m-01 | 로그 포맷 | — | 구조화 로깅(Structured Logging) 미사용. 향후 도입 시 검색 비효율 | Serilog 구조화 템플릿 사용 (`{Elapsed}ms`, `{Count}건`) |
| m-02 | 로그 위치 | — | 로그 파일 경로 미정의. 사용자마다 다른 위치 | `%LocalAppData%/MailTriageAssistant/logs/` 표준 경로 사용 |
| m-03 | 로그 보존 | — | 로그 로테이션/보존 정책 미정의 | `rollingInterval: Day`, `retainedFileCountLimit: 7` |

### ⚪ Info

| # | 영역 | 이슈 |
|---|---|---|
| I-01 | 긍정 | `BannedSymbols.txt` — Console/Debug 출력 금지 규칙 ✅ |
| I-02 | 긍정 | `#if DEBUG` Stopwatch 계측 코드가 일부 서비스에 이미 존재 (이전 수정에서 추가) |

---

## Proposed Event Schema

| 이벤트명 | 트리거 | 페이로드 (PII 제외) | 용도 |
|---|---|---|---|
| `EmailsLoaded` | `LoadEmailsAsync` 완료 | `Count`, `ElapsedMs` | 성능 모니터링 |
| `EmailBodyLoaded` | `LoadSelectedEmailBodyAsync` 완료 | `EntryId`(해시), `ElapsedMs` | 개별 로드 시간 |
| `DigestGenerated` | `GenerateDigestAsync` 완료 | `ItemCount`, `ElapsedMs` | Digest 성능 |
| `ReplyCreated` | `ReplyAsync` 완료 | `TemplateId` | 템플릿 사용 빈도 |
| `OutlookError` | COM 예외 발생 | `ErrorKind`, `HResult` | 에러 추적 |
| `AppStarted` | `OnStartup` | `Version`, `OutlookType` | 앱 시작 기록 |
| `AppShutdown` | `OnExit` | `SessionDurationMin`, `TotalProcessed` | 세션 요약 |

## Proposed Metrics

| 메트릭명 | 타입 | 수집 위치 | 목표 |
|---|---|---|---|
| `email_load_duration_ms` | Histogram | `OutlookService.FetchInboxHeaders` | < 1000ms |
| `body_load_duration_ms` | Histogram | `OutlookService.GetBody` | < 200ms |
| `digest_duration_ms` | Histogram | `DigestService.GenerateDigest` | < 500ms |
| `triage_count` | Counter | `MainViewModel.LoadEmailsAsync` | 세션당 누적 |
| `error_count` | Counter | 모든 catch 블록 | 세션당 0 목표 |

---

## Codex Handoff — Task List

| # | 파일 | 변경 요지 | 테스트 커맨드 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|
| T-01 | `App.xaml.cs` | Serilog 구성: `Log.Logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.File(path, rollingInterval: Day).CreateLogger()` + DI `AddLogging(b => b.AddSerilog())` | `dotnet build` | 빌드 성공 + 로그 파일 생성 | Low |
| T-02 | `Services/OutlookService.cs` | `ILogger<OutlookService>` 주입 + 주요 메서드 시작/완료/에러 로그. **본문 내용 절대 로깅 금지** | `dotnet build && dotnet test` | 빌드+테스트 통과 | Medium |
| T-03 | `Services/TriageService.cs` | `ILogger<TriageService>` 주입 + 분류 결과 로그 (카테고리, 점수만) | `dotnet build && dotnet test` | 빌드+테스트 통과 | Low |
| T-04 | `Services/DigestService.cs`, `Services/RedactionService.cs`, `Services/TemplateService.cs` | 각 서비스 `ILogger<T>` 주입 + 기본 로그 포인트 | `dotnet build && dotnet test` | 빌드+테스트 통과 | Low |
| T-05 | `App.xaml.cs:OnDispatcherUnhandledException` | `Log.Error(e.Exception, "Unhandled exception")` 추가 (PII 필터) | `dotnet build` | 빌드 성공 | Low |
| T-06 | `Helpers/PerfEventSource.cs`, `Services/OutlookService.cs` | PerfEventSource 이벤트 호출 코드 삽입 (FetchHeaders, GetBody) | `dotnet build` | 빌드 성공 | Low |
| T-07 | `ViewModels/MainViewModel.cs` | 사용자 행동 카운터 (`_loadCount`, `_digestCount` 등) + 로그 | `dotnet build` | 빌드 성공 | Low |
