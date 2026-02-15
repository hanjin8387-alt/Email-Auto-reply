# Agent 03: Test Engineering

## Mission
테스트 프로젝트를 설계하고, 단위 테스트·통합 테스트 전략을 수립하며, Codex가 바로 구현 가능한 테스트 명세를 산출한다.

## Scope
- 테스트 프로젝트 구조 설계 (`MailTriageAssistant.Tests`)
- 단위 테스트 대상 서비스 식별 및 테스트 케이스 작성
- Mock 전략 (IOutlookService 기반)
- 테스트 자동화 (dotnet test)

## Non-Goals
- E2E 테스트 (COM Interop이 필요하므로 수동 검증 영역)
- UI 테스트 자동화 (Appium 등은 현재 범위 밖)

---

## Inputs (우선순위 파일 목록)

| 우선순위 | 파일 | 테스트 필요도 | 이유 |
|---|---|---|---|
| P0 | `Services/RedactionService.cs` | 🔴 매우 높음 | 보안 핵심. 패턴 누락 시 PII 유출 |
| P0 | `Services/TriageService.cs` | 🔴 매우 높음 | 분류 로직 정확성 = 제품 가치 |
| P1 | `Services/DigestService.cs` | 🟡 높음 | Markdown 생성 정합성 |
| P1 | `Services/ClipboardSecurityHelper.cs` | 🟡 높음 | 보안 기능 (타이머 동작) |
| P1 | `Services/TemplateService.cs` | 🟡 높음 | 플레이스홀더 치환 정합성 |
| P2 | `ViewModels/MainViewModel.cs` | 🟠 중간 | 3단계 로딩 흐름 (IOutlookService Mock) |
| P2 | `Helpers/ScoreToColorConverter.cs` | 🟠 중간 | 경계값 테스트 |
| P3 | `Models/*.cs` | 🟢 낮음 | POCO 클래스 (PropertyChanged 제외) |

---

## Review Checklist

### 테스트 인프라
- [ ] 테스트 프로젝트 존재 여부 (현재: ❌ 없음)
- [ ] 테스트 프레임워크 선정 (권장: xUnit + Moq)
- [ ] `dotnet test` 실행 가능 여부
- [ ] 테스트-소스 프로젝트 참조 설정

### 테스트 커버리지
- [ ] RedactionService: 4개 패턴 각각 + 복합 + 빈 입력 + null
- [ ] TriageService: 카테고리별 분류 + 점수 경계값 + VIP + Newsletter
- [ ] DigestService: 빈 목록 + 단일 + 다수 + Markdown 형식
- [ ] TemplateService: 모든 플레이스홀더 + 미지정 + 빈 템플릿

### Mock 전략
- [ ] `IOutlookService` → Moq 기반 Mock
- [ ] COM 예외 시뮬레이션 (`COMException` Mock)
- [ ] 클립보드 테스트 (STA 스레드 필요)

---

## Output Template

산출물 경로: `.ai/reports/YYYY-MM-DD_test_engineering.md`

```markdown
# Test Engineering Report — MailTriageAssistant
> Date: YYYY-MM-DD
> Reviewer: Agent 03 (Test Engineering)

## Summary
- Testable Services: N
- Proposed Test Cases: N
- Framework: xUnit + Moq

## Test Project Setup
```bash
dotnet new xunit -n MailTriageAssistant.Tests
cd MailTriageAssistant.Tests
dotnet add reference ../MailTriageAssistant/MailTriageAssistant.csproj
dotnet add package Moq
```

## Test Cases

### RedactionService Tests
| # | Test Name | Input | Expected Output | Type |
|---|---|---|---|---|
| T-01 | Phone_IsRedacted | "010-1234-5678" | "[PHONE]" | Unit |
| T-02 | SSN_IsRedacted | "123456-1234567" | "[SSN]" | Unit |
| ... | ... | ... | ... | ... |

### TriageService Tests
| # | Test Name | Sender | Subject | Expected Category | Expected Score Range |
|---|---|---|---|---|---|
| T-10 | VipSender_HighScore | "ceo@company.com" | "보고서" | VIP | 70-100 |
| ... | ... | ... | ... | ... | ... |

### DigestService Tests
| # | Test Name | Input Items | Validation |
|---|---|---|---|
| T-20 | EmptyList_ReturnsHeader | [] | Markdown 테이블 헤더만 포함 |
| ... | ... | ... | ... |

## Codex Handoff
```

---

## Codex Handoff

1. **프로젝트 생성 명령**
   ```bash
   dotnet new xunit -n MailTriageAssistant.Tests
   cd MailTriageAssistant.Tests
   dotnet add reference ../MailTriageAssistant/MailTriageAssistant.csproj
   dotnet add package Moq
   dotnet add package FluentAssertions  # 선택
   ```

2. **테스트 파일 구조**
   ```
   MailTriageAssistant.Tests/
   ├── Services/
   │   ├── RedactionServiceTests.cs
   │   ├── TriageServiceTests.cs
   │   ├── DigestServiceTests.cs
   │   └── TemplateServiceTests.cs
   ├── ViewModels/
   │   └── MainViewModelTests.cs
   └── Helpers/
       └── ScoreToColorConverterTests.cs
   ```

3. **커밋 절차**
   ```
   1) 테스트 프로젝트 생성 → 커밋: [03] test: 테스트 프로젝트 초기화
   2) 서비스별 테스트 파일 생성 → 커밋 단위별
   3) dotnet test → 전체 통과 확인
   4) 커밋: [03] test: {서비스명} 단위 테스트 N건 추가
   ```

4. **테스트 실행 명령**
   ```bash
   dotnet test --verbosity normal
   dotnet test --collect:"XPlat Code Coverage"  # 커버리지 측정
   ```
