---
name: MailTriageAssistant Orchestrator
description: .NET 8 WPF 이메일 분류 보조 앱 전체 빌드를 오케스트레이션하는 메인 스킬
---

# MailTriageAssistant — Codex 메인 스킬

## 개요
이 스킬은 **MailTriageAssistant** 프로젝트의 전체 빌드를 오케스트레이션합니다.
Outlook Classic COM Interop을 통해 이메일을 읽고, 로컬에서 분류/마스킹 후, Teams 딥링크로 Copilot에게 전달하는 WPF 앱입니다.

## 핵심 제약 조건 (반드시 준수)
1. **외부 AI API 사용 금지** — OpenAI, Azure AI 등 클라우드 AI SDK 임포트 불가
2. **메모리 전용 처리** — 이메일 본문은 디스크(DB/파일/SQLite)에 절대 저장하지 않음
3. **Classic Outlook 전용** — New Outlook(Project Monarch)은 COM Interop을 지원하지 않음
4. **PII 마스킹 필수** — 클립보드에 복사 전 반드시 개인정보 제거
5. **클립보드 30초 자동 삭제** — 붙여넣기 후 30초 뒤 클립보드 자동 비우기
6. **본문 로그 기록 금지** — `Console.WriteLine`, `Debug.WriteLine` 등으로 이메일 본문 출력 금지

## 기술 스택
| 항목 | 값 |
|---|---|
| Runtime | .NET 8.0-windows |
| UI | WPF (XAML + MVVM) |
| COM Interop | Microsoft.Office.Interop.Outlook via `NetOfficeFw.Outlook` |
| 타겟 OS | Windows 10/11 |
| 배포 | MSIX / ClickOnce |

## 빌드 순서
아래 순서대로 서브 에이전트를 실행하세요. 각 에이전트의 SKILL.md는 `codex/agents/` 폴더에 있습니다.

| 순서 | Agent | 폴더 | 역할 |
|---|---|---|---|
| 1 | Project Setup | `01_project_setup/` | 프로젝트 초기화, csproj, 데이터 모델 생성 |
| 2 | Security & Redaction | `03_security_redaction/` | RedactionService, 클립보드 보안 유틸 |
| 3 | Outlook Connector | `02_outlook_connector/` | IOutlookService, OutlookService 구현 |
| 4 | Triage Engine | `04_triage_engine/` | TriageService (2단계 분석) |
| 5 | Digest & Teams | `05_digest_teams/` | DigestService, Teams 딥링크+폴백 |
| 6 | WPF UI | `06_wpf_ui/` | MainWindow.xaml, MainViewModel, 컨버터 |
| 7 | Template Engine | `07_template_engine/` | TemplateService, 답장 템플릿 |

## 검증 체크리스트
- [ ] `dotnet build` 성공 (경고 0)
- [ ] RedactionService 단위 테스트: "010-1234-5678" → "[PHONE]"
- [ ] New Outlook(`olk.exe`) 감지 시 에러 메시지 출력
- [ ] Teams 딥링크 실패 시 폴백 MessageBox 출력
- [ ] 클립보드 30초 후 자동 삭제
- [ ] 본문 로그 미출력 확인 (Output 창 검사)

## 참조 파일
- 사양서: `AI App Agent 2-saved.md`
- 업무 플로우: `codex/workflow.md`
- 명령문: `codex/CODEX_COMMAND.md`
