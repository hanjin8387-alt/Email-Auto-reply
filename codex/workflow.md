---
description: MailTriageAssistant 전체 빌드 워크플로우 — 단계별 실행 가이드
---

# MailTriageAssistant 빌드 워크플로우

## Phase 0: 프로젝트 초기화
```bash
# 1. WPF 프로젝트 생성
dotnet new wpf -n MailTriageAssistant
cd MailTriageAssistant

# 2. NuGet 패키지 추가
dotnet add package NetOfficeFw.Outlook
```

### 폴더 구조 생성
```
MailTriageAssistant/
├── MailTriageAssistant.csproj
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs
├── Models/
│   ├── RawEmailHeader.cs
│   ├── AnalyzedItem.cs
│   ├── EmailCategory.cs (Enum)
│   └── ReplyTemplate.cs
├── Services/
│   ├── IOutlookService.cs
│   ├── OutlookService.cs
│   ├── RedactionService.cs
│   ├── TriageService.cs
│   ├── DigestService.cs
│   ├── ClipboardSecurityHelper.cs
│   └── TemplateService.cs
├── ViewModels/
│   ├── MainViewModel.cs
│   └── RelayCommand.cs
└── Helpers/
    └── ScoreToColorConverter.cs
```

## Phase 1: 데이터 모델 (Agent 01)
1. `Models/EmailCategory.cs` — Enum 생성 (Action, VIP, Meeting, Approval, FYI, Newsletter, Other)
2. `Models/RawEmailHeader.cs` — 헤더 전용 DTO
3. `Models/AnalyzedItem.cs` — 분석 결과 모델 (INotifyPropertyChanged 구현)
4. `Models/ReplyTemplate.cs` — 답장 템플릿 모델

## Phase 2: 핵심 서비스 (Agent 02-05)

### 2a. RedactionService (Agent 03)
1. `Services/RedactionService.cs` 생성
2. 4개 정규식 패턴 등록 (SSN, Phone, Email, CreditCard)
3. `Redact(string input)` 메서드 구현
4. **절대 input/output 로그 기록 금지**

### 2b. ClipboardSecurityHelper (Agent 03)
1. `Services/ClipboardSecurityHelper.cs` 생성
2. 클립보드 복사 후 30초 타이머 → 자동 삭제 로직

### 2c. OutlookService (Agent 02)
1. `Services/IOutlookService.cs` — 인터페이스 정의
2. `Services/OutlookService.cs` — COM Interop 구현
3. New Outlook (`olk.exe`) 런타임 감지
4. `FetchInboxHeaders()` — 헤더만 읽기 (본문 X)
5. `GetBody(entryId)` — 선택 시 본문 로드 (1500자 제한)
6. `CreateDraft()` — Inspector 창 생성

### 2d. TriageService (Agent 04)
1. `Services/TriageService.cs` 생성
2. `AnalyzeHeader()` — 1단계 분석 (제목/발신자만)
3. `AnalyzeWithBody()` — 2단계 분석 (본문 포함)
4. 키워드 매칭 + 점수 산출

### 2e. DigestService + Teams (Agent 05)
1. `Services/DigestService.cs` 생성
2. Markdown 테이블 생성 + Copilot 프롬프트 조합
3. `OpenTeams()` — `https://` 기본 → `msteams://` 폴백 → MessageBox 최종 폴백
4. ClipboardSecurityHelper 연동

## Phase 3: UI (Agent 06)
1. `ViewModels/RelayCommand.cs` — ICommand 구현
2. `ViewModels/MainViewModel.cs` — MVVM 패턴
   - 3단계 로딩: Load(헤더) → Select(본문) → Digest(Top-N)
3. `Helpers/ScoreToColorConverter.cs` — 점수→색상 변환
4. `MainWindow.xaml` — 2컬럼 레이아웃

## Phase 4: 템플릿 엔진 (Agent 07)
1. `Services/TemplateService.cs` — 8개 한글 답장 템플릿
2. 플레이스홀더 치환 로직 (`{TargetDate}`, `{MissingInfo}` 등)
3. Outlook Draft 연동

## Phase 5: 통합 검증
```bash
dotnet build
dotnet run
```

### 검증 항목
| # | 테스트 | 기대 결과 |
|---|---|---|
| V-1 | `dotnet build` | 에러/경고 0 |
| V-2 | "010-1234-5678" 입력 | `[PHONE]` 출력 |
| V-3 | New Outlook 실행 중 앱 시작 | "Classic Outlook이 필요합니다" 에러 |
| V-4 | Teams 미설치 + "Copy Digest" 클릭 | 폴백 MessageBox 출력 |
| V-5 | 클립보드 복사 후 30초 대기 | 클립보드 자동 비워짐 |
| V-6 | 출력 창 검사 | 이메일 본문 미노출 |
