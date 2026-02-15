# Security & Privacy Report — MailTriageAssistant
> Date: 2026-02-15
> Reviewer: Agent 06 (Security & Privacy)
> Classification: CONFIDENTIAL

## Summary
- Total Issues: 16
- Critical: 4 | Major: 6 | Minor: 4 | Info: 2

---

## Threat Model Summary

| Threat | Vector | Current Mitigation | Gap |
|---|---|---|---|
| PII 유출 (클립보드) | 다른 앱이 클립보드 읽기 | 30초 DispatcherTimer 자동 삭제 | Win+V 히스토리 미대응, 레이스 컨디션 |
| PII 유출 — 미마스킹 패턴 | 계좌·IP·여권 등 미구현 PII가 마스킹 없이 전달 | 4종 패턴만 구현 | 6종 이상 패턴 누락 |
| PII 유출 (로그/예외) | `ex.Message`에 본문 데이터 포함 가능 | 정의된 예외만 재throw | `StatusMessage`에 `ex.Message` 직접 노출 |
| PII 유출 (XAML 바인딩) | `Sender`, `Subject` 비마스킹 노출 | 없음 | 리스트·상세 모두 원본 출력 |
| 입력 인젝션 (Markdown) | Digest Markdown 테이블에 `|`, 제어 문자 삽입 | `EscapeCell` | Markdown 링크/이미지 인젝션 미방어 |
| 입력 인젝션 (Template) | 사용자 값 → `{Placeholder}` 대체 | Regex 기반 치환 | 재귀 인젝션 `{TargetDate}` 체인 가능 |
| COM 보안 | Outlook COM 무한 대기, 릴리스 후 재접근 | STA 스레드, SafeRelease | RPC 타임아웃 미설정 |
| 의존성 취약점 | NuGet 패키지 명시적 취약점 | 없음 | `--vulnerable` 검사 미수행, Interop 15.0 구버전 |
| 유니코드 우회 | 전각 숫자(０１０-…) 등으로 마스킹 회피 | 없음 | 정규식이 ASCII 숫자만 인식 |

---

## Findings

### 🔴 Critical

| # | Category | File | Line | Issue | CVSS (Est.) | Recommendation |
|---|---|---|---|---|---|---|
| S-1 | PII Leak — 미구현 패턴 | `Services/RedactionService.cs` | L7-14 | 계좌번호, 여권번호(`M12345678`), IP 주소(`192.168.x.x`), URL 내 토큰/키 패턴이 구현되어 있지 않아 해당 PII가 마스킹 없이 클립보드·Digest·UI로 전달됨 | 7.5 | 각 PII 패턴에 대한 정규식 추가. 한국 계좌번호(은행별 형식 고려), 여권번호 `[A-Z]\d{8}`, IPv4 `\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}`, URL 토큰 `[?&](token\|key\|api_key)=[^\s&]+` |
| S-2 | PII Leak — 유니코드 우회 | `Services/RedactionService.cs` | L10-13 | `\d`가 `RegexOptions.CultureInvariant`여도 전각 숫자(U+FF10-FF19)를 매치하지 않음. 공격자 또는 특수 시스템이 전각 숫자로 된 전화번호를 보내면 마스킹 실패 | 7.0 | `Redact()` 진입 시 `NormalizeUnicode(input)` 전처리를 추가하여 전각→반각, 유니코드 정규화(NFC) 수행 후 패턴 매칭 |
| S-3 | Clipboard — Win+V 히스토리 잔존 | `Services/ClipboardSecurityHelper.cs` | L26 | `Clipboard.SetText()`는 Windows 클립보드 히스토리(Win+V)에 항목을 추가함. 30초 후 `Clipboard.Clear()`를 호출해도 히스토리에 남아있어 PII 복구 가능 | 7.0 | P/Invoke로 `SetClipboardData` 시 `CLIPBOARD_FORMAT` 플래그 중 `ExcludeClipboardHistory` (Windows 10 1809+) 사용, 또는 `AddClipboardFormatListener` 후 히스토리 비활성화 API 호출 |
| S-4 | PII Leak — XAML 비마스킹 바인딩 | `MainWindow.xaml` | L93-94, L118-120 | `{Binding Sender}`, `{Binding Subject}`, `{Binding SelectedEmail.Sender}`, `{Binding SelectedEmail.Subject}`가 마스킹 없이 원본 텍스트를 UI에 직접 표시. 이메일 주소·숟가락(Subject에 포함된 PII)가 그대로 노출 | 7.0 | `IValueConverter`를 만들어 바인딩 시 `RedactionService.Redact()`를 경유하도록 설정하거나, ViewModel에서 `RedactedSender`, `RedactedSubject` 프로퍼티를 추가하여 바인딩 |

### 🟡 Major

| # | Category | File | Line | Issue | CVSS (Est.) | Recommendation |
|---|---|---|---|---|---|---|
| S-5 | Data Leak — StatusBar | `ViewModels/MainViewModel.cs` | L156, L161, L203, L208, L265, L270, L324, L329 | `StatusMessage = ex.Message;` — OutlookService에서 throw하는 `InvalidOperationException`의 메시지는 현재 안전하나, 향후 예외 체인에 본문 내용이 포함될 가능성 있음. 방어적으로 정해진 메시지만 사용해야 함 | 5.5 | 예외별 사전 정의 메시지 매핑 도입. `catch (InvalidOperationException)` → 상수 문자열 사용, `ex.Message` 직접 노출 금지 |
| S-6 | Clipboard — Race Condition | `Services/ClipboardSecurityHelper.cs` | L45-49 | 30초 후 `Clipboard.ContainsText()` → `Clipboard.GetText()` → `Clipboard.Clear()` 사이에 다른 프로세스가 클립보드를 변경하면, (1) 자신의 콘텐츠가 이미 교체되었는데 Clear 안 함 (의도 동작이나 원본 데이터가 이미 다른 앱에 캡처됨), (2) 비교-삭제 사이에 끼어들면 잘못된 데이터를 삭제 | 5.0 | `OpenClipboard`/`CloseClipboard` P/Invoke로 원자적 접근 확보, 또는 시퀀스 넘버 비교(`GetClipboardSequenceNumber`) 사용 |
| S-7 | Injection — Markdown | `Services/DigestService.cs` | L30-57 | `EscapeCell()`이 `|`와 줄바꿈만 이스케이프. Markdown 이미지(`![](url)`), 링크(`[text](url)`), HTML 태그 삽입이 가능. Teams Copilot이 렌더링할 때 피싱 링크 삽입 가능 | 5.5 | `EscapeCell()`에 `[`, `]`, `(`, `)`, `!`, `<`, `>` 추가 이스케이프. 또는 원본 텍스트를 코드 블록(`` ` ``)으로 감싸기 |
| S-8 | Injection — Template (재귀) | `Services/TemplateService.cs` | L78-87 | `FillTemplate()`에서 사용자 `values`의 값에 `{AnotherPlaceholder}` 형태가 포함되면, 현재는 1회 치환이라 재귀 인젝션은 발생 안 하지만, 값 자체에 `{TargetDate}` 같은 패턴이 있으면 혼란 유발 가능. 또한 `___` 폴백이 사용자에게 노출 | 4.5 | 치환 후 결과에 대해 잔여 플레이스홀더 검증 추가. `___` 대신 `[미입력]`과 같이 명시적 표시. 값 길이 제한(예: 200자) 적용 |
| S-9 | COM — RPC 타임아웃 미설정 | `Services/OutlookService.cs` | L57-61 | `_comDispatcher.InvokeAsync(func)` 호출이 무한 대기 가능. Outlook이 응답 불능 상태(모달 대화상자 표시 등)에서 UI 스레드 행이 아닌 COM 스레드가 영구 블록됨 | 4.5 | `InvokeAsync` 결과에 `Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30)))` 패턴으로 타임아웃 적용. 타임아웃 시 `TimeoutException` throw |
| S-10 | COM — FinalReleaseComObject 후 필드 접근 | `Services/OutlookService.cs` | L300-306 | `ResetConnection()`에서 `_session`, `_app`을 `FinalReleaseComObject`한 후 null 할당. 그러나 다른 스레드에서 동시에 `_session!.GetDefaultFolder()`를 호출 중이면 릴리스된 COM 객체에 접근하여 `AccessViolationException` 발생 가능 | 4.0 | `lock` 또는 `SemaphoreSlim`으로 `_app`/`_session` 접근 동기화. `ResetConnection()`과 `EnsureClassicOutlookOrThrow()` 양쪽에 동일 잠금 적용 |

### 🟢 Minor

| # | Category | File | Line | Issue | CVSS (Est.) | Recommendation |
|---|---|---|---|---|---|---|
| S-11 | PII — 패턴 순서 | `Services/RedactionService.cs` | L10-13 | 신용카드 `\d{4}-\d{4}-\d{4}-\d{4}`가 먼저 매치되데, 만약 주민번호 형식이 하이픈 없이 13자리로 올 경우(`1234561234567`) 매치 실패. 또한 공백 포함 카드번호(`1234 1234 1234 1234`) 미매치 | 3.5 | 공백·하이픈 선택적 매치 변형 추가. 주민번호 13자리 연속 패턴 추가 |
| S-12 | Data Leak — Digest 원본 발신자 | `Services/DigestService.cs` | L39-41 | `senderDisplay`에 `<이메일>` 형식 포함 후 `Redact()` 적용하지만, `Sender` (이름)은 마스킹 대상이 아님. 발신자 실명이 그대로 Copilot에 전달됨 | 3.0 | Digest 생성 시 발신자명도 이니셜 처리 또는 도메인만 표시 옵션 제공 |
| S-13 | Dependency — 구버전 Interop | `MailTriageAssistant.csproj` | L12 | `Microsoft.Office.Interop.Outlook 15.0.4797.1004` — Office 2013 시절 패키지. 최신 NuGet에서 보안 패치 반영 여부 불명 | 3.0 | `dotnet list package --vulnerable` 실행 후 결과 확인. 가능하면 최신 버전으로 업데이트 |
| S-14 | Process — URL 실행 | `Services/DigestService.cs` | L106-109 | `Process.Start(url, UseShellExecute=true)` — `userEmail`이 악의적 값이면 임의 URL 실행 가능 (예: `javascript:` 스킴 등은 브라우저에 따라 무시되지만 `file://` 등은 위험할 수 있음) | 3.5 | `userEmail` 입력을 이메일 형식 정규식으로 검증 후 사용. URL 조합 전 화이트리스트 스킴(https, msteams)만 허용하도록 검증 |

### ⚪ Info

| # | Category | File | Line | Issue | Recommendation |
|---|---|---|---|---|---|
| S-15 | Logging — Console/Debug/Trace 전수 | 전체 | — | `Console.Write*`, `Debug.Write*`, `Trace.Write*` 호출 0건 확인 ✅ | 현 상태 유지. CI에 Roslyn Analyzer 추가하여 자동 금지 규칙 적용 권장 |
| S-16 | Exception — App.xaml.cs 글로벌 핸들러 | `App.xaml.cs` | L15-23 | 미처리 예외를 잡아서 안전한 메시지만 표시 ✅ | 현 상태 유지, 다만 로깅 프레임워크 도입 시 예외 내용을 안전하게 (PII 제거 후) 로깅하는 것 권장 |

---

## Redaction Coverage Matrix

| PII Type | Pattern | Status | Notes |
|---|---|---|---|
| 한국 전화번호 | `010-\d{4}-\d{4}` | ✅ 구현 | 공백 변형(`010 1234 5678`) 미대응 |
| 한국 주민번호 | `\d{6}-\d{7}` | ✅ 구현 | 하이픈 없는 연속 13자리 미대응 |
| 이메일 | `[a-zA-Z0-9._%+-]+@...` | ✅ 구현 | IDN(국제화 도메인) 미대응 |
| 신용카드 | `\d{4}-\d{4}-\d{4}-\d{4}` | ✅ 구현 | 공백 구분자 미대응 |
| 한국 계좌번호 | — | ❌ 미구현 | 은행별 형식 (10~14자리) |
| 여권번호 | — | ❌ 미구현 | `[A-Z]\d{8}` |
| IP 주소 | — | ❌ 미구현 | IPv4 `\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}` |
| URL 내 토큰 | — | ❌ 미구현 | `[?&](token\|key\|api_key)=...` |
| 전각 숫자 우회 | — | ❌ 미대응 | 유니코드 정규화 필요 |

---

## Review Checklist 결과

### PII 마스킹 검증
- [x] 한국 전화번호: `010-XXXX-XXXX` ✅ 구현
- [x] 한국 주민번호: `XXXXXX-XXXXXXX` ✅ 구현
- [x] 이메일: `user@domain.com` ✅ 구현
- [x] 신용카드: `XXXX-XXXX-XXXX-XXXX` ✅ 구현
- **미구현 패턴:**
  - [ ] 한국 계좌번호 (은행별 형식) → S-1
  - [ ] 여권번호 (`M12345678`) → S-1
  - [ ] IP 주소 (`192.168.x.x`) → S-1
  - [ ] URL에 포함된 토큰/키 → S-1
- [ ] 유니코드 변형 우회 (전각 숫자 등) → S-2
- [ ] 패턴 순서 충돌 (신용카드 vs 일반 숫자) → S-11

### 데이터 유출 경로
- [x] `Console.WriteLine` 호출 0건 ✅
- [x] `Debug.WriteLine` 호출 0건 ✅
- [x] `Trace.Write*` 호출 0건 ✅
- [ ] `MessageBox.Show`에 `ex.Message` 직접 전달 → S-5
- [ ] 예외 `Message`에 본문 포함 가능성 → S-5
- [x] WPF 바인딩 오류 시 Output 창 — 현재 특이사항 없음 ✅
- [x] `ToString()` 오버라이드 — 없음 ✅

### 클립보드 보안
- [x] 30초 자동 삭제 동작 ✅ (DispatcherTimer 기반)
- [ ] `Clipboard.ContainsText()` 레이스 컨디션 → S-6
- [ ] Windows 클립보드 히스토리(Win+V) 대응 → S-3
- [ ] 다른 프로세스의 클립보드 접근 → 완전 방어 불가, 30초 삭제로 부분 완화

### COM 보안
- [ ] DCOM 권한 설정 (로컬 실행 전용) — 별도 설정 없으나 `GetActiveObject`로 로컬 전용 ✅
- [x] `Marshal.FinalReleaseComObject` 사용 ✅
- [ ] RPC 타임아웃 설정 → S-9
- [ ] 릴리스 후 재접근 방지 → S-10

### 의존성 보안
- [ ] NuGet 패키지 취약점 검사 미수행 → S-13
- [ ] `Microsoft.Office.Interop.Outlook` 15.0 (2013 시절) → S-13

---

## Codex Handoff

### Task List

---

#### Task 1: RedactionService — PII 패턴 확장 (S-1, S-11)
- **파일**: `MailTriageAssistant/Services/RedactionService.cs`
- **함수**: `Rules` 배열 (L7-14)
- **수정 요지**:
  1. 기존 `Rules` 배열에 아래 패턴 추가 (순서: 구체적 → 범용):
     - 한국 계좌번호: `\d{3,6}-\d{2,6}-\d{2,8}` → `[ACCOUNT]`
     - 여권번호: `[A-Z]\d{8}` → `[PASSPORT]`
     - IPv4 주소: `\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}` → `[IP]`
     - URL 토큰: `[?&](token|key|api_key|apikey|secret|password)=[^\s&]+` → `[URL_TOKEN]` (대소문자 무시)
     - 공백 구분 카드번호: `\d{4}\s\d{4}\s\d{4}\s\d{4}` → `[CARD]`
     - 하이픈 없는 주민번호 (13자리 연속): `\d{13}` → `[SSN]` (컨텍스트에 따라 오탐 주의, 선택적)
  2. 패턴 순서를 재정렬하여 길이가 긴/구체적 패턴이 먼저 매치되도록
- **테스트 명령**:
  ```bash
  dotnet test --filter "FullyQualifiedName~RedactionServiceTests"
  ```
- **테스트 케이스 (추가)**:
  - `Redact("계좌: 110-123-456789")` → `"계좌: [ACCOUNT]"`
  - `Redact("여권 M12345678")` → `"여권 [PASSPORT]"`
  - `Redact("서버 192.168.1.100")` → `"서버 [IP]"`
  - `Redact("https://ex.com?token=abc123&key=xyz")` → `"https://ex.com[URL_TOKEN]&[URL_TOKEN]"` 또는 유사
  - `Redact("카드 1234 5678 9012 3456")` → `"카드 [CARD]"`
- **커밋**: `[06] security: add missing PII redaction patterns (account, passport, IP, URL token)`

---

#### Task 2: RedactionService — 유니코드 정규화 (S-2)
- **파일**: `MailTriageAssistant/Services/RedactionService.cs`
- **함수**: `Redact(string input)` (L16-30)
- **수정 요지**:
  1. `Redact()` 메서드 시작 부분에 유니코드 정규화 전처리 추가:
     ```csharp
     result = NormalizeToAsciiDigits(result);
     ```
  2. 새 private 메서드 `NormalizeToAsciiDigits(string input)` 추가:
     - 전각 숫자 (U+FF10-U+FF19) → 반각 (0-9) 변환
     - 전각 하이픈 (U+FF0D) → `-` 변환
     - `string.Normalize(NormalizationForm.FormKC)` 사용 고려
- **테스트 명령**:
  ```bash
  dotnet test --filter "FullyQualifiedName~RedactionServiceTests.Unicode"
  ```
- **테스트 케이스**:
  - `Redact("０１０-１２３４-５６７８")` → `"[PHONE]"`
  - `Redact("０１０－１２３４－５６７８")` (전각 하이픈) → `"[PHONE]"`
- **커밋**: `[06] security: normalize unicode before PII redaction`

---

#### Task 3: ClipboardSecurityHelper — Win+V 히스토리 방어 (S-3)
- **파일**: `MailTriageAssistant/Services/ClipboardSecurityHelper.cs`
- **함수**: `SecureCopy(string text)` (L18-29)
- **수정 요지**:
  1. `Clipboard.SetText()` 대신 P/Invoke로 직접 클립보드에 데이터 설정:
     ```csharp
     [DllImport("user32.dll")] static extern bool OpenClipboard(IntPtr hWndNewOwner);
     [DllImport("user32.dll")] static extern bool CloseClipboard();
     [DllImport("user32.dll")] static extern bool EmptyClipboard();
     [DllImport("user32.dll")] static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
     ```
  2. 클립보드 설정 시 `SetClipboardData`에 `ExcludeClipboardContentFromMonitorProcessing` 옵션 적용, 또는
  3. 대안: 클립보드 설정 직후 `HKEY_CURRENT_USER\Software\Microsoft\Clipboard` 의 `EnableClipboardHistory` 체크 후 경고 메시지 표시
  4. 최소 구현: `Clipboard.SetDataObject(dataObject, copy: false)` 사용 (`copy: false`이면 프로세스 종료 시 데이터 삭제, 히스토리 저장 감소)
- **테스트 명령**:
  ```bash
  dotnet test --filter "FullyQualifiedName~ClipboardSecurityTests"
  ```
- **테스트 케이스**:
  - `SecureCopy("test")` 후 `Clipboard.GetText()` == `"test"` 확인
  - 타이머 만료 후 클립보드가 비어있는지 확인 (모킹 필요)
- **커밋**: `[06] security: mitigate Win+V clipboard history exposure`

---

#### Task 4: ClipboardSecurityHelper — 레이스 컨디션 완화 (S-6)
- **파일**: `MailTriageAssistant/Services/ClipboardSecurityHelper.cs`
- **함수**: `StartClearTimer()` 내부 Tick 핸들러 (L40-59)
- **수정 요지**:
  1. P/Invoke `GetClipboardSequenceNumber()`를 사용하여 설정 시점과 삭제 시점의 시퀀스 넘버 비교:
     ```csharp
     [DllImport("user32.dll")] static extern uint GetClipboardSequenceNumber();
     ```
  2. `SecureCopy` 시점에 시퀀스 넘버 저장 → Tick 시점에 비교 → 동일하면 Clear, 다르면 Skip
  3. 이렇게 하면 `ContainsText()` → `GetText()` 사이에 다른 앱이 끼어들어도 안전하게 동작
- **테스트 명령**:
  ```bash
  dotnet test --filter "FullyQualifiedName~ClipboardSecurityTests.RaceCondition"
  ```
- **커밋**: `[06] security: use clipboard sequence number to prevent race condition`

---

#### Task 5: MainWindow.xaml — XAML 바인딩 PII 마스킹 (S-4)
- **파일**: `MailTriageAssistant/MainWindow.xaml` + `MailTriageAssistant/Helpers/RedactionConverter.cs` (신규)
- **관련 라인**: MainWindow.xaml L93, L94, L118, L120
- **수정 요지**:
  1. 새 `IValueConverter` 클래스 `RedactionConverter` 생성:
     ```csharp
     public class RedactionConverter : IValueConverter
     {
         private static readonly RedactionService _redaction = new();
         public object Convert(object value, ...) => _redaction.Redact(value?.ToString() ?? "");
         ...
     }
     ```
  2. `MainWindow.xaml` 리소스에 등록:
     ```xml
     <helpers:RedactionConverter x:Key="RedactConv" />
     ```
  3. 민감 바인딩에 컨버터 적용:
     - `{Binding Sender, Converter={StaticResource RedactConv}}`
     - `{Binding Subject, Converter={StaticResource RedactConv}}`
     - `{Binding SelectedEmail.Sender, Converter={StaticResource RedactConv}}`
     - `{Binding SelectedEmail.Subject, Converter={StaticResource RedactConv}}`
  4. **대안** (더 깔끔): ViewModel의 `AnalyzedItem`에 `RedactedSender`, `RedactedSubject` 프로퍼티 추가
- **테스트 명령**:
  ```bash
  dotnet build
  # UI 수동 확인: 이메일 주소가 포함된 Sender가 [EMAIL]로 마스킹되는지
  ```
- **커밋**: `[06] security: mask PII in XAML bindings via RedactionConverter`

---

#### Task 6: MainViewModel — ex.Message 직접 노출 제거 (S-5)
- **파일**: `MailTriageAssistant/ViewModels/MainViewModel.cs`
- **함수**: `LoadEmailsAsync()` L154-168, `LoadSelectedEmailBodyAsync()` L200-215, `GenerateDigestAsync()` L263-277, `ReplyAsync()` L322-340
- **수정 요지**:
  1. 모든 `catch (NotSupportedException ex)` / `catch (InvalidOperationException ex)` 블록에서:
     - `StatusMessage = ex.Message;` 를 사전 정의 상수 메시지로 교체
     - 예: `StatusMessage = "Outlook 연결 오류가 발생했습니다.";`
     - `MessageBox.Show`에도 동일 상수 사용
  2. 상수 정의:
     ```csharp
     private const string OutlookNotSupportedMsg = "Classic Outlook이 필요합니다.";
     private const string OutlookConnectionErrorMsg = "Outlook 연결에 실패했습니다. 상태를 확인해 주세요.";
     private const string GenericErrorMsg = "작업 중 오류가 발생했습니다.";
     ```
  3. `ex.Message`는 향후 로깅 프레임워크에서만 사용 (PII 제거 후)
- **테스트 명령**:
  ```bash
  dotnet build
  dotnet test --filter "FullyQualifiedName~MainViewModelTests"
  ```
- **커밋**: `[06] security: replace ex.Message exposure with predefined messages`

---

#### Task 7: DigestService — Markdown 인젝션 방어 (S-7)
- **파일**: `MailTriageAssistant/Services/DigestService.cs`
- **함수**: `EscapeCell(string text)` (L119-131)
- **수정 요지**:
  1. `EscapeCell()` 메서드에 Markdown 특수 문자 이스케이프 추가:
     ```csharp
     .Replace("[", "\\[", StringComparison.Ordinal)
     .Replace("]", "\\]", StringComparison.Ordinal)
     .Replace("(", "\\(", StringComparison.Ordinal)
     .Replace(")", "\\)", StringComparison.Ordinal)
     .Replace("!", "\\!", StringComparison.Ordinal)
     .Replace("<", "&lt;", StringComparison.Ordinal)
     .Replace(">", "&gt;", StringComparison.Ordinal)
     ```
  2. 또는 더 안전하게: 각 셀 값을 `` `backtick` ``으로 감싸서 코드 블록 처리
- **테스트 명령**:
  ```bash
  dotnet test --filter "FullyQualifiedName~DigestServiceTests.EscapeCell"
  ```
- **테스트 케이스**:
  - `EscapeCell("![image](http://evil.com/img.png)")` → 이스케이프된 문자열
  - `EscapeCell("[Click here](http://phishing.com)")` → 이스케이프된 문자열
  - `EscapeCell("<script>alert(1)</script>")` → `"&lt;script&gt;alert(1)&lt;/script&gt;"`
- **커밋**: `[06] security: escape markdown special chars in digest cells`

---

#### Task 8: TemplateService — 입력 검증 강화 (S-8)
- **파일**: `MailTriageAssistant/Services/TemplateService.cs`
- **함수**: `FillTemplate(string templateBody, IReadOnlyDictionary<string, string> values)` (L71-88)
- **수정 요지**:
  1. 값에 `{`, `}` 문자가 포함된 경우 제거 또는 이스케이프:
     ```csharp
     var sanitized = val.Replace("{", "").Replace("}", "");
     ```
  2. 값 길이 제한 추가 (200자):
     ```csharp
     if (sanitized.Length > 200) sanitized = sanitized[..200] + "…";
     ```
  3. `___` 폴백을 `[미입력]`으로 변경
- **테스트 명령**:
  ```bash
  dotnet test --filter "FullyQualifiedName~TemplateServiceTests"
  ```
- **테스트 케이스**:
  - `FillTemplate("{TaskName} done", {"TaskName": "{Blocker}"})` → `"Blocker done"` (중괄호 제거됨)
  - `FillTemplate("{X}", {"X": "a"*300})` → 200자 + "…"
  - `FillTemplate("{Missing}", {})` → `"[미입력]"`
- **커밋**: `[06] security: sanitize template values and add length limit`

---

#### Task 9: OutlookService — RPC 타임아웃 (S-9)
- **파일**: `MailTriageAssistant/Services/OutlookService.cs`
- **함수**: `InvokeAsync<T>(Func<T> func)` (L57-58), `InvokeAsync(Action action)` (L60-61)
- **수정 요지**:
  1. 타임아웃 래퍼 추가:
     ```csharp
     private static readonly TimeSpan ComTimeout = TimeSpan.FromSeconds(30);

     private async Task<T> InvokeAsync<T>(Func<T> func)
     {
         var task = _comDispatcher.InvokeAsync(func).Task;
         if (await Task.WhenAny(task, Task.Delay(ComTimeout)) != task)
             throw new TimeoutException("Outlook COM 호출이 30초 내에 응답하지 않았습니다.");
         return await task;
     }
     ```
  2. `InvokeAsync(Action)` 오버로드에도 동일 적용
- **테스트 명령**:
  ```bash
  dotnet build
  dotnet test --filter "FullyQualifiedName~OutlookServiceTests.Timeout"
  ```
- **커밋**: `[06] security: add 30s timeout to Outlook COM calls`

---

#### Task 10: OutlookService — COM 동기화 (S-10)
- **파일**: `MailTriageAssistant/Services/OutlookService.cs`
- **함수**: `ResetConnection()` (L300-306), `EnsureClassicOutlookOrThrow()` (L63-95)
- **수정 요지**:
  1. `private readonly object _comLock = new();` 필드 추가
  2. `EnsureClassicOutlookOrThrow()` 와 `ResetConnection()` 양쪽을 `lock (_comLock)` 로 감싸기
  3. 또는 COM 디스패처 스레드에서만 접근하므로, 모든 호출이 `InvokeAsync`를 통하는지 확인하고 `ResetConnection`도 디스패처 스레드에서만 호출되도록 보장
- **테스트 명령**:
  ```bash
  dotnet build
  ```
- **커밋**: `[06] security: synchronize COM object access in OutlookService`

---

#### Task 11: DigestService — userEmail 입력 검증 (S-14)
- **파일**: `MailTriageAssistant/Services/DigestService.cs`
- **함수**: `OpenTeams(string digest, string? userEmail)` (L72-100)
- **수정 요지**:
  1. `email` 값에 대해 이메일 형식 정규식 검증 추가:
     ```csharp
     private static readonly Regex EmailValidator = new(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);
     ```
  2. 검증 실패 시 `email = string.Empty` 처리 (기본 Teams 페이지 열기)
  3. URL 조합 전 스킴 화이트리스트 검증 확인 (현재 `https://`, `msteams:` 하드코딩이므로 안전하나 명시적 주석 추가)
- **테스트 명령**:
  ```bash
  dotnet test --filter "FullyQualifiedName~DigestServiceTests.OpenTeams"
  ```
- **테스트 케이스**:
  - `OpenTeams(digest, "valid@email.com")` → 정상 동작
  - `OpenTeams(digest, "file:///etc/passwd")` → 무시되고 기본 Teams URL 사용
- **커밋**: `[06] security: validate userEmail input before URL construction`

---

#### Task 12: 의존성 취약점 검사 (S-13)
- **파일**: `MailTriageAssistant/MailTriageAssistant.csproj`
- **수정 요지**:
  1. 다음 명령으로 취약점 검사:
     ```bash
     dotnet list package --vulnerable
     dotnet list package --outdated
     ```
  2. 결과에 따라 `Microsoft.Office.Interop.Outlook` 버전 업데이트
  3. `.csproj`에 `<NuGetAudit>true</NuGetAudit>` 추가하여 빌드 시 자동 검사:
     ```xml
     <PropertyGroup>
       <NuGetAudit>true</NuGetAudit>
       <NuGetAuditLevel>low</NuGetAuditLevel>
     </PropertyGroup>
     ```
- **테스트 명령**:
  ```bash
  dotnet restore
  dotnet build
  dotnet list package --vulnerable
  ```
- **커밋**: `[06] security: enable NuGet audit and update dependencies`

---

#### Task 13: Roslyn Analyzer — 로그 유출 방지 자동화 (S-15)
- **파일**: `MailTriageAssistant/MailTriageAssistant.csproj`
- **수정 요지**:
  1. `.editorconfig` 또는 `Directory.Build.props`에 `Console.Write*`, `Debug.Write*` 사용 금지 규칙 추가
  2. 또는 `Microsoft.CodeAnalysis.BannedApiAnalyzers` NuGet 패키지 추가:
     ```xml
     <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="3.3.4">
       <PrivateAssets>all</PrivateAssets>
       <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
     </PackageReference>
     ```
  3. `BannedSymbols.txt` 파일 생성:
     ```
     M:System.Console.WriteLine(System.String);Console output may leak PII
     M:System.Diagnostics.Debug.WriteLine(System.String);Debug output may leak PII
     ```
- **테스트 명령**:
  ```bash
  dotnet build
  # 빌드 경고로 금지 API 사용 시 감지
  ```
- **커밋**: `[06] security: add banned API analyzer to prevent PII logging`

---

#### Task 14: 테스트 파일 생성 — 보안 역테스트
- **파일**: `MailTriageAssistant.Tests/Security/RedactionSecurityTests.cs` (신규)
- **수정 요지**:
  1. 마스킹 우회 시도 테스트:
     - 전각 숫자로 전화번호 입력 → 마스킹 확인
     - 계좌번호, 여권, IP 등 새 패턴 테스트
     - 공백 구분 카드번호 테스트
  2. 클립보드 보안 테스트 (UI 스레드 모킹 필요):
     - SecureCopy → 30초 후 Clear 동작
  3. Markdown 인젝션 테스트:
     - 악의적 Markdown 링크/이미지가 이스케이프되는지
  4. 템플릿 인젝션 테스트:
     - `{Placeholder}` 값에 중괄호 포함 시 무해화 확인
- **테스트 명령**:
  ```bash
  dotnet test --filter "FullyQualifiedName~RedactionSecurityTests"
  ```
- **커밋**: `[06] security: add reverse security tests for PII bypass attempts`

---

### 커밋 절차

```
1) 보안 수정 1건
2) 역테스트 코드 작성 (우회 시도가 실패하는지)
3) dotnet build + dotnet test
4) 커밋: [06] security: {한줄 설명}
```

### 코드 스캔 명령 (Codex가 실행)

```bash
# 본문 로그 유출 검색
grep -rn "Console.Write\|Debug.Write\|Trace.Write" Services/ ViewModels/

# 의존성 취약점 검사
dotnet list package --vulnerable

# 전체 PII 패턴 미적용 경로 확인
grep -rn "\.Body\|\.Subject\|\.Sender" Services/ ViewModels/ --include="*.cs"
```

### PR 요약 형식

```
## Security Fixes (2026-02-15)
- Fixed 4 critical security issues (PII pattern gaps, unicode bypass, clipboard history, XAML binding exposure)
- Fixed 6 major issues (StatusBar leak, race condition, markdown injection, template injection, COM timeout/sync)
- Added 5+ redaction patterns (account, passport, IP, URL token, fullwidth)
- Added N security-related reverse tests
⚠️ REQUIRES SECURITY REVIEW BEFORE MERGE
```

### 우선순위 실행 순서

| 순서 | Task | 근거 |
|---|---|---|
| 1 | Task 1 (PII 패턴 확장) | Critical — 미마스킹 데이터가 외부로 유출됨 |
| 2 | Task 2 (유니코드 정규화) | Critical — 기존 패턴도 우회 가능 |
| 3 | Task 5 (XAML 바인딩 마스킹) | Critical — UI에 원본 PII 노출 |
| 4 | Task 3 (Win+V 방어) | Critical — 클립보드 히스토리 잔존 |
| 5 | Task 7 (Markdown 인젝션) | Major — 피싱 링크 삽입 가능 |
| 6 | Task 6 (ex.Message 제거) | Major — 예외 메시지 노출 |
| 7 | Task 4 (레이스 컨디션) | Major — 클립보드 경합 |
| 8 | Task 8 (템플릿 검증) | Major — 입력 인젝션 |
| 9 | Task 9 (RPC 타임아웃) | Major — 무한 대기 |
| 10 | Task 10 (COM 동기화) | Major — 크래시 가능 |
| 11 | Task 11 (이메일 검증) | Minor — URL 인젝션 가능 |
| 12 | Task 12 (의존성) | Minor — 취약점 여부 확인 필요 |
| 13 | Task 13 (Analyzer) | Info — 예방적 조치 |
| 14 | Task 14 (역테스트) | 전 Task와 병행 |
