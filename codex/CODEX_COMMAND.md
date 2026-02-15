# Codex 명령문 — MailTriageAssistant

> 이 파일의 내용을 Codex에게 프롬프트로 전달하세요.

---

## 명령문 (복사하여 사용)

```
You are building a complete, compile-ready WPF application called "MailTriageAssistant" using .NET 8.

## Context Files
Read these files FIRST before writing any code:
- `codex/SKILL.md` — Master orchestration skill (build order, constraints)
- `codex/workflow.md` — Phased build workflow (Phase 0-5)
- `AI App Agent 2-saved.md` — Full specification (SPEC_PACK.json + CODEX BUILD PROMPT)

## Sub-Agent Skills (read in order):
1. `codex/agents/01_project_setup/SKILL.md` — Project scaffolding + data models
2. `codex/agents/02_outlook_connector/SKILL.md` — Outlook COM Interop service
3. `codex/agents/03_security_redaction/SKILL.md` — PII redaction + clipboard security
4. `codex/agents/04_triage_engine/SKILL.md` — Email categorization engine
5. `codex/agents/05_digest_teams/SKILL.md` — Copilot digest + Teams deep link
6. `codex/agents/06_wpf_ui/SKILL.md` — WPF dashboard (MVVM)
7. `codex/agents/07_template_engine/SKILL.md` — Reply templates

## CRITICAL CONSTRAINTS (never violate):
1. NO external AI APIs (OpenAI, Azure AI, etc.)
2. NO saving email bodies to disk (memory-only processing)
3. Classic Outlook ONLY — detect New Outlook (olk.exe) and show error
4. ALL PII must be redacted BEFORE clipboard copy
5. Clipboard auto-clear after 30 seconds
6. NEVER log email body content (Console.WriteLine, Debug.WriteLine, etc.)
7. Sanitize all exception messages that may contain email content
8. Teams deep link: use https:// primary → msteams:// fallback → MessageBox fallback

## Build Order:
Execute the sub-agent skills in numerical order (01 → 07).
After all files are generated, run:
  dotnet build
Ensure 0 errors and 0 warnings.

## Expected Output Files:
MailTriageAssistant/
├── MailTriageAssistant.csproj
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs
├── Models/
│   ├── EmailCategory.cs
│   ├── RawEmailHeader.cs
│   ├── AnalyzedItem.cs
│   └── ReplyTemplate.cs
├── Services/
│   ├── IOutlookService.cs
│   ├── OutlookService.cs
│   ├── RedactionService.cs
│   ├── ClipboardSecurityHelper.cs
│   ├── TriageService.cs
│   ├── DigestService.cs
│   └── TemplateService.cs
├── ViewModels/
│   ├── MainViewModel.cs
│   └── RelayCommand.cs
└── Helpers/
    └── ScoreToColorConverter.cs

Generate ALL files in a single pass. Every file must compile without errors.
```

---

## 사용 방법

### OpenAI Codex CLI 사용 시:
```bash
codex --model o4-mini --full-auto \
  "$(cat codex/CODEX_COMMAND.md)"
```

### Codex 웹 UI 사용 시:
1. 위 명령문(``` 블록 내용)을 복사
2. Codex 채팅에 붙여넣기
3. 프로젝트 폴더(`Email 정리/`)를 작업 디렉토리로 설정
4. 실행

### GitHub Copilot Agent / Cursor 사용 시:
1. `codex/SKILL.md`를 먼저 읽도록 지시
2. "workflow.md의 Phase 0부터 순서대로 실행해줘"라고 요청
3. 각 Phase 완료 후 `dotnet build` 확인

---

## 검증 명령어
```bash
# 빌드 검증
dotnet build

# 실행 (Classic Outlook이 실행 중이어야 함)
dotnet run

# 단위 테스트 (선택)
dotnet test
```
