# Test Engineering Report — MailTriageAssistant
> Date: 2026-02-15
> Reviewer: Agent 03 (Test Engineering)

## Summary
- Testable Services: **6** (RedactionService, TriageService, DigestService, ClipboardSecurityHelper, TemplateService, ScoreToColorConverter)
- Proposed Test Cases: **62**
- Framework: xUnit + Moq + FluentAssertions
- Test Project Status: ❌ **존재하지 않음** — 신규 생성 필요

---

## Test Project Setup

```bash
dotnet new xunit -n MailTriageAssistant.Tests
cd MailTriageAssistant.Tests
dotnet add reference ../MailTriageAssistant/MailTriageAssistant.csproj
dotnet add package Moq --version 4.20.72
dotnet add package FluentAssertions --version 7.0.0
```

### 프로젝트 구조
```
MailTriageAssistant.Tests/
├── MailTriageAssistant.Tests.csproj
├── Services/
│   ├── RedactionServiceTests.cs
│   ├── TriageServiceTests.cs
│   ├── DigestServiceTests.cs
│   ├── ClipboardSecurityHelperTests.cs
│   └── TemplateServiceTests.cs
├── ViewModels/
│   └── MainViewModelTests.cs
└── Helpers/
    └── ScoreToColorConverterTests.cs
```

---

## Review Checklist 결과

### 테스트 인프라
- [x] 테스트 프로젝트 존재 여부 → ❌ **없음** (신규 생성 필요)
- [x] 테스트 프레임워크 선정 → **xUnit 2.9 + Moq 4.20 + FluentAssertions 7.0**
- [x] `dotnet test` 실행 가능 여부 → 프로젝트 생성 후 확인 필요
- [x] 테스트-소스 프로젝트 참조 설정 → `dotnet add reference` 명령으로 설정

### 테스트 커버리지 (현재 → 목표)
- [x] RedactionService: 0% → 100% (4개 패턴 × 단일/복합/빈/null)
- [x] TriageService: 0% → 100% (카테고리별 분류 + 점수 경계값 + VIP + Newsletter)
- [x] DigestService: 0% → 100% (빈 목록 + 단일 + 다수 + Markdown 형식)
- [x] TemplateService: 0% → 100% (모든 플레이스홀더 + 미지정 + 빈 템플릿)

### Mock 전략
- [x] `IOutlookService` → Moq 기반 Mock — 인터페이스 이미 존재 (`IOutlookService.cs`)
- [x] COM 예외 시뮬레이션 → `Mock<IOutlookService>` 에서 `Throws<InvalidOperationException>()` 사용
- [x] 클립보드 테스트 → STA 스레드 필요, `DispatcherTimer` Mock 불가 → **단위 테스트에서 `RedactionService` 위임 검증만 수행, 클립보드 실제 동작은 수동 검증**

---

## Test Cases

### 1. RedactionService Tests (P0 — 보안 핵심)

**파일**: `Services/RedactionService.cs` → **테스트 파일**: `Services/RedactionServiceTests.cs`
**SUT**: `RedactionService.Redact(string input)`

| # | Test Name | Input | Expected Output | Type |
|---|---|---|---|---|
| T-01 | `Redact_PhoneNumber_IsReplaced` | `"연락처: 010-1234-5678"` | `"연락처: [PHONE]"` | Unit |
| T-02 | `Redact_SSN_IsReplaced` | `"주민번호: 900101-1234567"` | `"주민번호: [SSN]"` | Unit |
| T-03 | `Redact_CreditCard_IsReplaced` | `"카드: 1234-5678-9012-3456"` | `"카드: [CARD]"` | Unit |
| T-04 | `Redact_Email_IsReplaced` | `"이메일: user@example.com"` | `"이메일: [EMAIL]"` | Unit |
| T-05 | `Redact_MultiplePatterns_AllReplaced` | `"010-1234-5678, 900101-1234567, user@test.com"` | `"[PHONE], [SSN], [EMAIL]"` | Unit |
| T-06 | `Redact_NullInput_ReturnsNull` | `null` | `null` | Unit |
| T-07 | `Redact_EmptyString_ReturnsEmpty` | `""` | `""` | Unit |
| T-08 | `Redact_NoSensitiveData_ReturnsOriginal` | `"일반 텍스트입니다"` | `"일반 텍스트입니다"` | Unit |
| T-09 | `Redact_PhoneInMiddleOfText_IsReplaced` | `"전화번호는 010-9876-5432 입니다"` | `"전화번호는 [PHONE] 입니다"` | Unit |
| T-10 | `Redact_MultiplePhones_AllReplaced` | `"010-1111-2222, 010-3333-4444"` | `"[PHONE], [PHONE]"` | Unit |
| T-11 | `Redact_CardBefore_SSN_OrderMatters` | `"1234-5678-9012-3456 vs 900101-1234567"` | `"[CARD] vs [SSN]"` | Unit |
| T-12 | `Redact_NonMatchingPhoneFormat_NotReplaced` | `"02-1234-5678"` | `"02-1234-5678"` (변경 없음) | Unit |

### 2. TriageService Tests (P0 — 분류 로직)

**파일**: `Services/TriageService.cs` → **테스트 파일**: `Services/TriageServiceTests.cs`
**SUT**: `TriageService.AnalyzeHeader(sender, subject)` / `AnalyzeWithBody(sender, subject, body)`

| # | Test Name | Sender | Subject | Expected Category | Expected Score Range |
|---|---|---|---|---|---|
| T-13 | `AnalyzeHeader_VipSender_ReturnsVip` | `"ceo@company.com"` | `"보고서"` | VIP | 80 (50+30) |
| T-14 | `AnalyzeHeader_ActionKeyword_ReturnsAction` | `"user@test.com"` | `"긴급 요청"` | Action | 70 (50+20) |
| T-15 | `AnalyzeHeader_ApprovalKeyword_ReturnsApproval` | `"user@test.com"` | `"승인요청 건"` | Approval | 65 (50+15) |
| T-16 | `AnalyzeHeader_MeetingKeyword_ReturnsMeeting` | `"user@test.com"` | `"Teams 회의 초대"` | Meeting | 60 (50+10) |
| T-17 | `AnalyzeHeader_NewsletterKeyword_ReturnsNewsletter` | `"noreply@news.com"` | `"Unsubscribe 가능"` | Newsletter | 0 (50-50) |
| T-18 | `AnalyzeHeader_FyiKeyword_ReturnsFYI` | `"user@test.com"` | `"FYI 참고"` | FYI | 50 |
| T-19 | `AnalyzeHeader_NoKeyword_ReturnsOther` | `"user@test.com"` | `"일반 내용"` | Other | 50 |
| T-20 | `AnalyzeHeader_VipWithAction_ScoreCapped100` | `"ceo@company.com"` | `"긴급 요청 확인"` | Action | 100 (50+30+20, capped) |
| T-21 | `AnalyzeHeader_UnknownSender_PenaltyApplied` | `"unknown"` | `"테스트"` | Other | 40 (50-10) |
| T-22 | `AnalyzeHeader_NullSender_NoVip` | `null` | `"확인 요청"` | Action | 60 (50+20-10) |
| T-23 | `AnalyzeHeader_EmptySubject_ScoreIs50` | `"user@test.com"` | `""` | Other | 50 |
| T-24 | `AnalyzeWithBody_ActionInBody_DetectedAsAction` | `"user@test.com"` | `"제목"` (body: `"긴급 요청"`) | Action | 70 |
| T-25 | `AnalyzeHeader_ActionPriorityOverVip` | `"ceo@company.com"` | `"긴급 확인 요청"` | Action | 100 |
| T-26 | `AnalyzeHeader_NewsletterDeductionClamps0` | `"no-reply@ad.com"` | `"광고 구독"` | Newsletter | 0 |
| T-27 | `AnalyzeHeader_TagsContainAllMatched` | `"ceo@company.com"` | `"긴급 요청"` | Action | Tags: `["VIP","Action"]` |
| T-28 | `AnalyzeHeader_ActionHint_MatchesCategory` | `"user@test.com"` | `"일반"` | Other | ActionHint: `"검토"` |

**점수 계산 경계값 상세:**

| 시나리오 | 기본 | VIP | Action | Approval | Meeting | Newsletter | Unknown | Clamp | 최종 |
|---|---|---|---|---|---|---|---|---|---|
| VIP만 | 50 | +30 | - | - | - | - | - | - | 80 |
| VIP+Action | 50 | +30 | +20 | - | - | - | - | Clamp(100) | 100 |
| Newsletter | 50 | - | - | - | - | -50 | - | Clamp(0) | 0 |
| Unknown sender (@ 없음) | 50 | - | - | - | - | - | -10 | - | 40 |
| 일반 (@ 있음) | 50 | - | - | - | - | - | - | - | 50 |

### 3. DigestService Tests (P1 — Markdown 생성)

**파일**: `Services/DigestService.cs` → **테스트 파일**: `Services/DigestServiceTests.cs`
**SUT**: `DigestService.GenerateDigest(IReadOnlyList<AnalyzedItem> items)` 및 private `EscapeCell` (간접 검증)
**의존성**: `RedactionService` (실제 인스턴스), `ClipboardSecurityHelper` (실제 인스턴스)

| # | Test Name | Input Items | Validation |
|---|---|---|---|
| T-29 | `GenerateDigest_EmptyList_ContainsHeaderOnly` | `[]` | Markdown 테이블 헤더 `\| Priority \| Sender \|` 포함, 데이터 행 없음 |
| T-30 | `GenerateDigest_SingleItem_ContainsOneRow` | `[{Score=80, Sender="A", Subject="S1"}]` | 데이터 행 1개, "높음" 라벨 포함 |
| T-31 | `GenerateDigest_MultipleItems_OrderedByScoreDesc` | `[{Score=30}, {Score=90}, {Score=50}]` | 첫 행 Score=90, 두 번째 Score=50, 세 번째 Score=30 |
| T-32 | `GenerateDigest_ContainsSystemPrompt` | `[{Score=50}]` | `"SYSTEM PROMPT"` 문자열 포함 |
| T-33 | `GenerateDigest_ContainsTaskListFooter` | 아무 입력 | `"Tasks:"`, `"top 3 critical"` 문자열 포함 |
| T-34 | `GenerateDigest_RedactionApplied` | `[{SenderEmail="user@test.com"}]` | Sender 에 `[EMAIL]` 포함 |
| T-35 | `GenerateDigest_PipeInText_Escaped` | `[{Subject="A|B"}]` | 출력에 `\\|` 포함 |
| T-36 | `GenerateDigest_PriorityLabel_High` | `[{Score=80}]` | `"높음"` 포함 |
| T-37 | `GenerateDigest_PriorityLabel_Medium` | `[{Score=50}]` | `"중간"` 포함 |
| T-38 | `GenerateDigest_PriorityLabel_Low` | `[{Score=29}]` | `"낮음"` 포함 |
| T-39 | `EscapeCell_NewlineReplaced` | `Subject="line1\nline2"` | 출력에 `"line1 line2"` 포함 (줄바꿈 제거) |

### 4. TemplateService Tests (P1 — 플레이스홀더 치환)

**파일**: `Services/TemplateService.cs` → **테스트 파일**: `Services/TemplateServiceTests.cs`
**SUT**: `TemplateService.FillTemplate(templateBody, values)` / `GetTemplates()` / `SendDraft()`

| # | Test Name | Template Body | Values | Expected Output |
|---|---|---|---|---|
| T-40 | `FillTemplate_SinglePlaceholder_Replaced` | `"안녕하세요, {TargetDate}까지"` | `{TargetDate: "2026-02-20"}` | `"안녕하세요, 2026-02-20까지"` |
| T-41 | `FillTemplate_MultiplePlaceholders_AllReplaced` | `"- {Date1}\n- {Date2}"` | `{Date1: "월", Date2: "화"}` | `"- 월\n- 화"` |
| T-42 | `FillTemplate_MissingValue_ReplacedWithUnderscores` | `"{MissingInfo} 확인"` | `{}` (빈 dict) | `"___ 확인"` |
| T-43 | `FillTemplate_EmptyTemplate_ReturnsEmpty` | `""` | `{any: "v"}` | `""` |
| T-44 | `FillTemplate_NullTemplate_ReturnsEmpty` | `null` | `{any: "v"}` | `""` |
| T-45 | `FillTemplate_NoPlaceholders_ReturnsOriginal` | `"플레이스홀더 없음"` | `{}` | `"플레이스홀더 없음"` |
| T-46 | `FillTemplate_WhitespaceValue_ReplacedWithUnderscores` | `"{Key}"` | `{Key: "  "}` | `"___"` |
| T-47 | `GetTemplates_Returns8Templates` | N/A | N/A | `Count == 8` |
| T-48 | `GetTemplates_ReturnsDeepCopies` | N/A | N/A | 반환 리스트 수정 시 원본 불변 |
| T-49 | `SendDraft_ValidTemplate_CallsOutlookCreateDraft` | TMP_01 | `{TargetDate: "..."}` | `outlookService.CreateDraft` 1회 호출 확인 |
| T-50 | `SendDraft_InvalidTemplateId_ThrowsInvalidOperation` | `"INVALID"` | `{}` | `InvalidOperationException` |
| T-51 | `SendDraft_NullOutlookService_ThrowsArgNull` | N/A | N/A | `ArgumentNullException` |

### 5. MainViewModel Tests (P2 — 통합 흐름)

**파일**: `ViewModels/MainViewModel.cs` → **테스트 파일**: `ViewModels/MainViewModelTests.cs`
**의존성 Mock**: `IOutlookService`, 나머지는 실제 인스턴스

| # | Test Name | Scenario | Validation |
|---|---|---|---|
| T-52 | `Constructor_InitializesTemplates` | 생성 | `Templates.Count == 8`, `SelectedTemplate != null` |
| T-53 | `LoadEmails_Success_PopulatesEmails` | `FetchInboxHeaders` 3건 반환 | `Emails.Count == 3`, 점수순 정렬 |
| T-54 | `LoadEmails_Empty_SetsStatusMessage` | `FetchInboxHeaders` 0건 | `StatusMessage` 에 "표시할 메일이 없습니다" |
| T-55 | `LoadEmails_OutlookNotRunning_ShowsError` | `FetchInboxHeaders` → `InvalidOperationException` | `StatusMessage` 에 에러 메시지 |
| T-56 | `LoadEmails_SetsIsLoadingDuringExecution` | 호출 중 | `IsLoading == true` → 완료 후 `false` |
| T-57 | `SelectedEmail_Set_TriggersPropertyChanged` | 값 변경 | `PropertyChanged` 이벤트 발생 |
| T-58 | `CopySelected_NullEmail_NoAction` | `SelectedEmail == null` | 아무 동작 없음 |

### 6. ScoreToColorConverter Tests (P2 — 경계값)

**파일**: `Helpers/ScoreToColorConverter.cs` → **테스트 파일**: `Helpers/ScoreToColorConverterTests.cs`
**SUT**: `ScoreToColorConverter.Convert(value, ...)`

| # | Test Name | Input Score | Expected Brush |
|---|---|---|---|
| T-59 | `Convert_Score80_ReturnsIndianRed` | `80` | `Brushes.IndianRed` |
| T-60 | `Convert_Score79_ReturnsDarkOrange` | `79` | `Brushes.DarkOrange` |
| T-61 | `Convert_Score50_ReturnsDarkOrange` | `50` | `Brushes.DarkOrange` |
| T-62 | `Convert_Score49_ReturnsSeaGreen` | `49` | `Brushes.SeaGreen` |
| T-63 | `Convert_Score30_ReturnsSeaGreen` | `30` | `Brushes.SeaGreen` |
| T-64 | `Convert_Score29_ReturnsGray` | `29` | `Brushes.Gray` |
| T-65 | `Convert_Score0_ReturnsGray` | `0` | `Brushes.Gray` |
| T-66 | `Convert_Score100_ReturnsIndianRed` | `100` | `Brushes.IndianRed` |
| T-67 | `Convert_StringInput_Parsed` | `"75"` | `Brushes.DarkOrange` |
| T-68 | `Convert_NullInput_ReturnsGray` | `null` | `Brushes.Gray` |

---

## 테스트 가능성 분석 (Testability Assessment)

### ✅ 높은 테스트 가능성
| 서비스 | 이유 |
|---|---|
| `RedactionService` | 순수 함수, 외부 의존성 0. Regex 패턴만 검증 |
| `TriageService` | 순수 함수, 키워드 기반 분류 로직만 검증 |
| `TemplateService.FillTemplate` | 순수 함수, Regex 치환만 검증 |
| `ScoreToColorConverter` | 순수 함수, 경계값만 검증 |

### ⚠️ 중간 테스트 가능성
| 서비스 | 이유 | 해결 방안 |
|---|---|---|
| `DigestService.GenerateDigest` | `RedactionService` 의존 | 실제 인스턴스 주입 (순수 함수이므로 Mock 불필요) |
| `TemplateService.SendDraft` | `IOutlookService` 의존 | `Mock<IOutlookService>` 사용 |
| `MainViewModel` | 6개 서비스 의존 | `IOutlookService` Mock, 나머지 실제 인스턴스 |

### ❌ 낮은 테스트 가능성 (수동 검증 대상)
| 서비스 | 이유 |
|---|---|
| `ClipboardSecurityHelper.SecureCopy` | `Application.Current.Dispatcher`, `Clipboard`, `DispatcherTimer` — WPF 런타임 의존 |
| `DigestService.OpenTeams` | `Process.Start`, `Clipboard`, `MessageBox` — 시스템 호출 의존 |
| `OutlookService` | COM Interop, STA 스레드 — E2E 영역 |

---

## Mock 전략 상세

### IOutlookService Mock (Moq 기반)

```csharp
var mockOutlook = new Mock<IOutlookService>();

// FetchInboxHeaders mock
mockOutlook.Setup(o => o.FetchInboxHeaders())
    .ReturnsAsync(new List<RawEmailHeader>
    {
        new() { EntryId = "E1", SenderName = "김대표", SenderEmail = "ceo@company.com",
                Subject = "긴급 요청", ReceivedTime = DateTime.Now, HasAttachments = false },
        new() { EntryId = "E2", SenderName = "뉴스", SenderEmail = "no-reply@news.com",
                Subject = "구독 소식", ReceivedTime = DateTime.Now.AddHours(-1), HasAttachments = false },
    });

// GetBody mock
mockOutlook.Setup(o => o.GetBody("E1"))
    .ReturnsAsync("회의 요청 본문입니다. 010-1234-5678로 연락주세요.");

// CreateDraft mock
mockOutlook.Setup(o => o.CreateDraft(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
    .Returns(Task.CompletedTask);

// COM 예외 시뮬레이션
mockOutlook.Setup(o => o.FetchInboxHeaders())
    .ThrowsAsync(new InvalidOperationException("Outlook이 실행 중이지 않습니다."));
```

### MainViewModel 테스트를 위한 MessageBox 처리

> `MainViewModel` 은 `MessageBox.Show()` 를 직접 호출하므로, 단위 테스트 시 문제가 됨.
> **권장**: `IDialogService` 인터페이스를 추출하여 DI로 주입하도록 리팩터링하되,
> 현재 단계에서는 **MessageBox 호출을 안 타는 정상 경로만 테스트**하고,
> 예외 경로는 `catch` 블록 진입만 확인 (StatusMessage 검증).

---

## Codex Handoff

### 1. 프로젝트 생성 명령

```bash
dotnet new xunit -n MailTriageAssistant.Tests
cd MailTriageAssistant.Tests
dotnet add reference ../MailTriageAssistant/MailTriageAssistant.csproj
dotnet add package Moq --version 4.20.72
dotnet add package FluentAssertions --version 7.0.0
```

### 2. 테스트 파일 구조

```
MailTriageAssistant.Tests/
├── MailTriageAssistant.Tests.csproj
├── Services/
│   ├── RedactionServiceTests.cs
│   ├── TriageServiceTests.cs
│   ├── DigestServiceTests.cs
│   ├── ClipboardSecurityHelperTests.cs   (placeholder — 수동 검증 노트만)
│   └── TemplateServiceTests.cs
├── ViewModels/
│   └── MainViewModelTests.cs
└── Helpers/
    └── ScoreToColorConverterTests.cs
```

### 3. 커밋 절차

```
1) 테스트 프로젝트 생성       → 커밋: [03] test: 테스트 프로젝트 초기화 (xUnit + Moq + FluentAssertions)
2) RedactionServiceTests.cs  → 커밋: [03] test: RedactionService 단위 테스트 12건 추가
3) TriageServiceTests.cs     → 커밋: [03] test: TriageService 단위 테스트 16건 추가
4) DigestServiceTests.cs     → 커밋: [03] test: DigestService 단위 테스트 11건 추가
5) TemplateServiceTests.cs   → 커밋: [03] test: TemplateService 단위 테스트 12건 추가
6) MainViewModelTests.cs     → 커밋: [03] test: MainViewModel 통합 테스트 7건 추가
7) ScoreToColorConverterTests.cs → 커밋: [03] test: ScoreToColorConverter 경계값 테스트 10건 추가
8) dotnet test → 전체 통과 확인
9) 커밋: [03] test: 전체 테스트 62건 통과 확인
```

### 4. 테스트 실행 명령

```bash
dotnet test --verbosity normal
dotnet test --collect:"XPlat Code Coverage"  # 커버리지 측정
```

---

## Task List (Codex 구현용 — 매우 구체적)

---

### Task 1: 테스트 프로젝트 생성 + csproj 수정

| 항목 | 내용 |
|---|---|
| **작업** | xUnit 테스트 프로젝트 생성 및 패키지 참조 설정 |
| **실행 명령** | `dotnet new xunit -n MailTriageAssistant.Tests` |
| **후속 명령** | `cd MailTriageAssistant.Tests && dotnet add reference ../MailTriageAssistant/MailTriageAssistant.csproj && dotnet add package Moq --version 4.20.72 && dotnet add package FluentAssertions --version 7.0.0` |
| **csproj 수정** | `<TargetFramework>` 를 `net8.0-windows` 로 변경, `<UseWPF>true</UseWPF>` 추가 (WPF 타입 의존성 해결) |
| **검증 명령** | `dotnet build MailTriageAssistant.Tests` |
| **커밋** | `[03] test: 테스트 프로젝트 초기화 (xUnit + Moq + FluentAssertions)` |

**완성된 csproj 형태:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.1" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="FluentAssertions" Version="7.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\MailTriageAssistant\MailTriageAssistant.csproj" />
  </ItemGroup>
</Project>
```

---

### Task 2: RedactionServiceTests.cs 작성

| 항목 | 내용 |
|---|---|
| **대상 파일** | `MailTriageAssistant.Tests/Services/RedactionServiceTests.cs` |
| **테스트 대상** | `MailTriageAssistant.Services.RedactionService.Redact(string)` |
| **테스트 메서드 수** | 12건 (T-01 ~ T-12) |
| **의존성** | 없음 (순수 함수) |
| **수정 요지** | 신규 파일 생성. `[Theory]` + `[InlineData]` 사용하여 4개 패턴(PHONE, SSN, CARD, EMAIL) 각각 단일 및 복합 검증. `null` → `null`, 빈 문자열 → 빈 문자열. 패턴 우선순위(CARD > SSN) 검증. 비매칭 포맷(`02-1234-5678`) 미변경 검증. |
| **핵심 assert** | `result.Should().Be(expected)` |
| **검증 명령** | `dotnet test --filter "FullyQualifiedName~RedactionServiceTests" --verbosity normal` |
| **커밋** | `[03] test: RedactionService 단위 테스트 12건 추가` |

**구체적 구현 가이드:**
```csharp
using FluentAssertions;
using MailTriageAssistant.Services;
using Xunit;

namespace MailTriageAssistant.Tests.Services;

public class RedactionServiceTests
{
    private readonly RedactionService _sut = new();

    [Theory]
    [InlineData("010-1234-5678", "[PHONE]")]
    [InlineData("900101-1234567", "[SSN]")]
    [InlineData("1234-5678-9012-3456", "[CARD]")]
    [InlineData("user@example.com", "[EMAIL]")]
    public void Redact_SinglePattern_IsReplaced(string input, string expected)
    {
        _sut.Redact(input).Should().Be(expected);
    }

    [Fact]
    public void Redact_NullInput_ReturnsNull()
    {
        _sut.Redact(null!).Should().BeNull();
    }

    [Fact]
    public void Redact_EmptyString_ReturnsEmpty()
    {
        _sut.Redact("").Should().BeEmpty();
    }

    [Fact]
    public void Redact_MultiplePatterns_AllReplaced()
    {
        var input = "전화: 010-1234-5678, 주민: 900101-1234567, 메일: a@b.com";
        var result = _sut.Redact(input);
        result.Should().Contain("[PHONE]")
              .And.Contain("[SSN]")
              .And.Contain("[EMAIL]");
    }

    [Fact]
    public void Redact_NoSensitiveData_ReturnsOriginal()
    {
        _sut.Redact("일반 텍스트입니다").Should().Be("일반 텍스트입니다");
    }

    [Fact]
    public void Redact_PhoneInMiddleOfText_IsReplaced()
    {
        _sut.Redact("전화번호는 010-9876-5432 입니다")
            .Should().Be("전화번호는 [PHONE] 입니다");
    }

    [Fact]
    public void Redact_MultiplePhones_AllReplaced()
    {
        _sut.Redact("010-1111-2222, 010-3333-4444")
            .Should().Be("[PHONE], [PHONE]");
    }

    [Fact]
    public void Redact_CardBeforeSSN_OrderMatters()
    {
        _sut.Redact("1234-5678-9012-3456 vs 900101-1234567")
            .Should().Be("[CARD] vs [SSN]");
    }

    [Fact]
    public void Redact_NonMatchingPhoneFormat_NotReplaced()
    {
        _sut.Redact("02-1234-5678").Should().Be("02-1234-5678");
    }
}
```

---

### Task 3: TriageServiceTests.cs 작성

| 항목 | 내용 |
|---|---|
| **대상 파일** | `MailTriageAssistant.Tests/Services/TriageServiceTests.cs` |
| **테스트 대상** | `MailTriageAssistant.Services.TriageService.AnalyzeHeader(sender, subject)` / `AnalyzeWithBody(sender, subject, body)` |
| **테스트 메서드 수** | 16건 (T-13 ~ T-28) |
| **의존성** | 없음 (순수 함수) |
| **수정 요지** | 신규 파일 생성. 카테고리 분류 7종 각각 검증. 점수 산출(VIP+30, Action+20, Approval+15, Meeting+10, Newsletter-50, Unknown-10, Clamp 0~100) 경계값 검증. Tags 배열 내용 검증. ActionHint 문자열 일치 검증. `[Theory]`/`[Fact]` 혼용. |
| **핵심 assert** | `result.Category.Should().Be(EmailCategory.Action)`, `result.Score.Should().Be(70)`, `result.Tags.Should().Contain("VIP")` |
| **검증 명령** | `dotnet test --filter "FullyQualifiedName~TriageServiceTests" --verbosity normal` |
| **커밋** | `[03] test: TriageService 단위 테스트 16건 추가` |

**구체적 구현 가이드:**
```csharp
using FluentAssertions;
using MailTriageAssistant.Models;
using MailTriageAssistant.Services;
using Xunit;

namespace MailTriageAssistant.Tests.Services;

public class TriageServiceTests
{
    private readonly TriageService _sut = new();

    [Fact]
    public void AnalyzeHeader_VipSender_ReturnsVip()
    {
        var result = _sut.AnalyzeHeader("ceo@company.com", "보고서");
        result.Category.Should().Be(EmailCategory.VIP);
        result.Score.Should().Be(80);
    }

    [Fact]
    public void AnalyzeHeader_ActionKeyword_ReturnsAction()
    {
        var result = _sut.AnalyzeHeader("user@test.com", "긴급 요청");
        result.Category.Should().Be(EmailCategory.Action);
        result.Score.Should().Be(70);
    }

    // ... 나머지 T-15 ~ T-28 동일 패턴으로 구현
}
```

---

### Task 4: DigestServiceTests.cs 작성

| 항목 | 내용 |
|---|---|
| **대상 파일** | `MailTriageAssistant.Tests/Services/DigestServiceTests.cs` |
| **테스트 대상** | `MailTriageAssistant.Services.DigestService.GenerateDigest(IReadOnlyList<AnalyzedItem>)` |
| **테스트 메서드 수** | 11건 (T-29 ~ T-39) |
| **의존성** | `ClipboardSecurityHelper` (생성자 주입), `RedactionService` (생성자 주입) — 둘 다 실제 인스턴스 |
| **수정 요지** | 신규 파일 생성. `AnalyzedItem` 객체를 직접 생성하여 테스트 입력. Markdown 형식(헤더, 정렬 순서, 우선순위 라벨, 파이프 이스케이프, 시스템 프롬프트, Tasks 섹션) 검증. |
| **SUT 생성** | `var redaction = new RedactionService(); var clipHelper = new ClipboardSecurityHelper(redaction); var sut = new DigestService(clipHelper, redaction);` |
| **검증 명령** | `dotnet test --filter "FullyQualifiedName~DigestServiceTests" --verbosity normal` |
| **커밋** | `[03] test: DigestService 단위 테스트 11건 추가` |

**주의**: `ClipboardSecurityHelper` 생성 시 `Application.Current`가 null이면 런타임 오류 발생 가능. 테스트에서 `GenerateDigest`만 호출하면 클립보드 접근이 없으므로 안전하지만, 만약 문제 발생 시 `ClipboardSecurityHelper`를 인터페이스로 추출하는 리팩토링 필요.

---

### Task 5: TemplateServiceTests.cs 작성

| 항목 | 내용 |
|---|---|
| **대상 파일** | `MailTriageAssistant.Tests/Services/TemplateServiceTests.cs` |
| **테스트 대상** | `MailTriageAssistant.Services.TemplateService.FillTemplate(body, values)`, `GetTemplates()`, `SendDraft()` |
| **테스트 메서드 수** | 12건 (T-40 ~ T-51) |
| **의존성** | `IOutlookService` (SendDraft 테스트에만 필요) |
| **Mock 전략** | `Mock<IOutlookService>` — `Setup(o => o.CreateDraft(...)).Returns(Task.CompletedTask)` |
| **수정 요지** | 신규 파일 생성. `FillTemplate`: 단일/다중 플레이스홀더, 미지정→`___`, null/빈→`""`, 공백값→`___`. `GetTemplates`: 8개 반환, DeepCopy. `SendDraft`: 유효 ID → CreateDraft 호출, 잘못된 ID → `InvalidOperationException`, null service → `ArgumentNullException`. |
| **검증 명령** | `dotnet test --filter "FullyQualifiedName~TemplateServiceTests" --verbosity normal` |
| **커밋** | `[03] test: TemplateService 단위 테스트 12건 추가` |

---

### Task 6: MainViewModelTests.cs 작성

| 항목 | 내용 |
|---|---|
| **대상 파일** | `MailTriageAssistant.Tests/ViewModels/MainViewModelTests.cs` |
| **테스트 대상** | `MailTriageAssistant.ViewModels.MainViewModel` |
| **테스트 메서드 수** | 7건 (T-52 ~ T-58) |
| **의존성** | `Mock<IOutlookService>`, `RedactionService`, `ClipboardSecurityHelper`, `TriageService`, `DigestService`, `TemplateService` (모두 실제 인스턴스) |
| **수정 요지** | 신규 파일 생성. 생성자 초기화(Templates 8개, SelectedTemplate 존재). PropertyChanged 이벤트. StatusMessage 검증. **⚠️ `MessageBox.Show()` 호출 경로는 테스트 불가** → 정상 경로만 테스트. |
| **검증 명령** | `dotnet test --filter "FullyQualifiedName~MainViewModelTests" --verbosity normal` |
| **커밋** | `[03] test: MainViewModel 통합 테스트 7건 추가` |

**⚠️ 알려진 제약 및 해결책:**
1. `MessageBox.Show()` 직접 호출 → 정상 경로만 테스트 (예외 경로 테스트 시 `IDialogService` 리팩터링 필요)
2. `LoadSelectedEmailBodyAsync` fire-and-forget → `SelectedEmail` 설정 테스트 시 `Task.Delay`로 완료 대기 필요
3. `Application.Current` null → `ClipboardSecurityHelper` 관련 호출을 피하는 정상 경로만 테스트

**헬퍼 메서드 패턴:**
```csharp
private static MainViewModel CreateSut(Mock<IOutlookService> mockOutlook)
{
    var redaction = new RedactionService();
    var clipHelper = new ClipboardSecurityHelper(redaction);
    var triage = new TriageService();
    var digest = new DigestService(clipHelper, redaction);
    var template = new TemplateService();
    return new MainViewModel(mockOutlook.Object, redaction, clipHelper,
                             triage, digest, template);
}
```

---

### Task 7: ScoreToColorConverterTests.cs 작성

| 항목 | 내용 |
|---|---|
| **대상 파일** | `MailTriageAssistant.Tests/Helpers/ScoreToColorConverterTests.cs` |
| **테스트 대상** | `MailTriageAssistant.Helpers.ScoreToColorConverter.Convert(value, ...)` |
| **테스트 메서드 수** | 10건 (T-59 ~ T-68) |
| **의존성** | 없음 (순수 함수) — WPF `Brushes` 타입만 참조 |
| **수정 요지** | 신규 파일 생성. 경계값(0, 29, 30, 49, 50, 79, 80, 100) 각각 예상 Brush 반환 검증. 문자열 입력(`"75"`) 파싱 검증. null 입력 → `Brushes.Gray`. `ConvertBack` → `Binding.DoNothing`. |
| **검증 명령** | `dotnet test --filter "FullyQualifiedName~ScoreToColorConverterTests" --verbosity normal` |
| **커밋** | `[03] test: ScoreToColorConverter 경계값 테스트 10건 추가` |

**구체적 구현 가이드:**
```csharp
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using FluentAssertions;
using MailTriageAssistant.Helpers;
using Xunit;

namespace MailTriageAssistant.Tests.Helpers;

public class ScoreToColorConverterTests
{
    private readonly ScoreToColorConverter _sut = new();

    [Theory]
    [InlineData(80, "IndianRed")]
    [InlineData(100, "IndianRed")]
    [InlineData(79, "DarkOrange")]
    [InlineData(50, "DarkOrange")]
    [InlineData(49, "SeaGreen")]
    [InlineData(30, "SeaGreen")]
    [InlineData(29, "Gray")]
    [InlineData(0, "Gray")]
    public void Convert_IntScore_ReturnsCorrectBrush(int score, string expectedBrushName)
    {
        var result = _sut.Convert(score, typeof(Brush), null, CultureInfo.InvariantCulture);
        var expected = typeof(Brushes).GetProperty(expectedBrushName)!.GetValue(null);
        result.Should().Be(expected);
    }

    [Fact]
    public void Convert_StringInput_Parsed()
    {
        var result = _sut.Convert("75", typeof(Brush), null, CultureInfo.InvariantCulture);
        result.Should().Be(Brushes.DarkOrange);
    }

    [Fact]
    public void Convert_NullInput_ReturnsGray()
    {
        var result = _sut.Convert(null, typeof(Brush), null, CultureInfo.InvariantCulture);
        result.Should().Be(Brushes.Gray);
    }
}
```

---

### Task 8: 전체 테스트 실행 및 검증

| 항목 | 내용 |
|---|---|
| **실행 명령** | `dotnet test --verbosity normal` |
| **기대 결과** | 62건 전체 통과 (Passed) |
| **실패 시** | 실패 테스트 로그 확인 → TestName, 실패 원인 분석 → 수정 → 재실행 |
| **커버리지 측정** | `dotnet test --collect:"XPlat Code Coverage"` |
| **커밋** | `[03] test: 전체 테스트 62건 통과 확인` |

---

### Task 9 (선택): ClipboardSecurityHelper 수동 검증 노트 작성

| 항목 | 내용 |
|---|---|
| **대상 파일** | `MailTriageAssistant.Tests/Services/ClipboardSecurityHelperTests.cs` |
| **수정 요지** | Placeholder 파일 생성. `// 수동 검증 대상: STA 스레드 + WPF DispatcherTimer + Clipboard 의존` 주석 기록. 향후 `IClipboardService` 인터페이스 추출 시 단위 테스트 가능 표기. |
| **커밋** | Task 4 커밋에 포함 |

---

## 향후 개선 권장사항

| 우선순위 | 항목 | 상세 |
|---|---|---|
| 🔴 P0 | `IDialogService` 추출 | `MainViewModel`의 `MessageBox.Show()` 호출을 인터페이스 분리 → 단위 테스트 가능성 확보 |
| 🔴 P0 | `IClipboardService` 추출 | `ClipboardSecurityHelper`의 `Clipboard.SetText()`/`Clear()`를 인터페이스로 분리 |
| 🟡 P1 | `RedactionService` 패턴 확장 | IP 주소, 계좌번호, 여권번호 등 추가 PII 패턴 |
| 🟡 P1 | `TriageService` VIP 목록 외부화 | 하드코딩된 VIP 목록을 설정 파일/DB로 이동 |
| 🟠 P2 | CI/CD 연동 | GitHub Actions에서 `dotnet test` 자동 실행 |
| 🟠 P2 | Mutation Testing | Stryker.NET으로 테스트 품질 검증 |
