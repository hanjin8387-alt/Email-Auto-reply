# MailTriageAssistant — AI 리뷰/개선 오케스트레이터 상시 규칙

## Assumptions (저장소 스캔 결과)

| 항목 | 추정 | 확신도 |
|---|---|---|
| 언어 / 런타임 | C# / .NET 8.0 (`net8.0-windows`) | 🟢 확실 |
| UI 프레임워크 | WPF (XAML + MVVM) | 🟢 확실 |
| COM 연동 | `Microsoft.Office.Interop.Outlook` v15.0 | 🟢 확실 |
| 빌드 도구 | `dotnet build` (MSBuild) | 🟢 확실 |
| 테스트 프로젝트 | **없음** — xUnit/NUnit/MSTest 프로젝트 미존재 | 🟢 확실 |
| CI/CD | 미확인 — GitHub Actions / Azure DevOps 설정 없음 | 🟡 추정 |
| 패키지 관리 | NuGet (PackageReference 방식) | 🟢 확실 |
| 배포 형태 | MSIX / ClickOnce (사양서 명시, 미구현) | 🟡 추정 |
| 코드 양 | C# 17파일 (~1,300줄), XAML 2파일 (~190줄) | 🟢 확실 |
| 아키텍처 | MVVM (`Models/`, `Services/`, `ViewModels/`, `Helpers/`) | 🟢 확실 |

## 소스 파일 인벤토리

```
MailTriageAssistant/
├── MailTriageAssistant.csproj          (프로젝트 정의)
├── App.xaml / App.xaml.cs              (앱 진입점)
├── AssemblyInfo.cs                     (어셈블리 메타데이터)
├── MainWindow.xaml / .cs               (메인 UI 창)
├── Models/
│   ├── EmailCategory.cs                (Enum: 7개 카테고리)
│   ├── RawEmailHeader.cs               (헤더 DTO)
│   ├── AnalyzedItem.cs                 (분석 결과 모델)
│   └── ReplyTemplate.cs               (답장 템플릿 모델)
├── Services/
│   ├── IOutlookService.cs              (인터페이스)
│   ├── OutlookService.cs               (COM Interop 328줄)
│   ├── RedactionService.cs             (PII 마스킹 33줄)
│   ├── ClipboardSecurityHelper.cs      (클립보드 보안)
│   ├── TriageService.cs                (분류 엔진 164줄)
│   ├── DigestService.cs                (Copilot 요약 134줄)
│   └── TemplateService.cs             (답장 템플릿)
├── ViewModels/
│   ├── MainViewModel.cs                (메인 VM 379줄)
│   └── RelayCommand.cs                (ICommand 구현)
└── Helpers/
    └── ScoreToColorConverter.cs        (점수→색상)
```

---

## 상시 규칙 (모든 에이전트 공통)

### 1. 커밋 정책
- **커밋 단위:** 단일 관심사(Single Concern) 원칙. 한 커밋에 하나의 변경 목적만 포함.
- **커밋 메시지:** `[에이전트번호] 카테고리: 한 줄 설명` (예: `[03] fix: RedactionService에 IP 패턴 추가`)
- **변경 제한:** 커밋 당 최대 5개 파일, 200줄 이하.

### 2. 위험도 분류

| 등급 | 정의 | 필요 조건 |
|---|---|---|
| 🔴 Critical | 보안 취약점, 데이터 유출, 빌드 실패 | 즉시 수정 + 테스트 필수 |
| 🟡 Major | 기능 결함, 성능 저하, 아키텍처 위반 | PR 리뷰 + 테스트 권장 |
| 🟢 Minor | 코드 스타일, 네이밍, 주석 부족 | 백로그 등록, 일괄 처리 가능 |
| ⚪ Info | 개선 제안, 리팩토링 기회 | 로드맵 항목으로 기록 |

### 3. 테스트 요구사항
- **현재 상태:** 테스트 프로젝트 없음. 최우선으로 `MailTriageAssistant.Tests` 프로젝트 생성 필요.
- **최소 커버리지:** `RedactionService`, `TriageService`, `DigestService.GenerateDigest()` 대상 단위 테스트
- **COM 의존성:** `OutlookService`는 `IOutlookService` 인터페이스 기반 Mock 테스트
- **테스트 실행:** `dotnet test` (프로젝트 생성 후)

### 4. 롤백 기준
- `dotnet build` 실패 → 즉시 롤백
- 기존 기능이 깨지는 회귀 발생 → 롤백 후 원인 분석
- 보안 규칙(본문 로그 금지, 클립보드 보안) 위반 → 즉시 롤백

### 5. 산출물 경로 규칙
```
.ai/reports/YYYY-MM-DD_<agent_name>.md
```
예: `.ai/reports/2026-02-15_code_review.md`

### 6. 보안 불변 규칙 (절대 위반 금지)
1. 이메일 본문을 디스크에 저장하지 않음
2. 이메일 본문을 로그에 기록하지 않음 (`Console.WriteLine`, `Debug.WriteLine`, `Trace.Write`)
3. 클립보드 복사 후 30초 내 자동 삭제
4. 예외 메시지에 본문 내용 포함 금지
5. 외부 AI API 호출 금지
