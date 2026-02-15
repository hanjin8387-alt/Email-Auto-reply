# Codex Change Log — MailTriageAssistant
Date: 2026-02-15
Plan: `.ai/plans/2026-02-13_feature_master_plan.md`

## Regression Status
- Build: OK (`dotnet build MailTriageAssistant/MailTriageAssistant.csproj`)
- Tests: OK (86 passed) (`dotnet test MailTriageAssistant.Tests/`)

## Key Changes (High Level)
- Security: PII redaction 강화(계좌/여권/IP/URL토큰 등) + 유니코드 정규화(FormKC), Win+V 클립보드 히스토리 제외 + 30초 자동 삭제, Markdown/Template 인젝션 방어, 예외 메시지 직접 노출 방지, New Outlook(`olk.exe`) 차단.
- Reliability/Perf: Outlook COM STA 스레드 + 타임아웃/lock, Restrict+GetFirst/GetNext 최적화, partial failure 허용, 본문 프리페치, RangeObservableCollection 배치 갱신.
- UI/UX: 점수 색상/긴급도 레이블, Empty State/ProgressBar, 카테고리 필터, "Outlook에서 열기", 첨부 아이콘 등.
- Observability: Serilog 파일 로깅 + `ILogger<T>` 주입, `SessionStatsService`(인메모리 통계), `#if DEBUG` Stopwatch 계측(ETW EventSource).
- Guardrails: Banned API Analyzer로 `Console`/`Debug`/`Trace` 사용 방지.

## Log File Location
- `%LOCALAPPDATA%\\MailTriageAssistant\\logs\\MailTriageAssistant-*.log` (일 단위 롤링)

## Publish Notes
- .NET SDK는 기본적으로 WPF trimming을 차단합니다(NETSDK1168). 배포용 publish는 trimming을 끄는 것을 권장합니다:
  - `dotnet publish MailTriageAssistant/MailTriageAssistant.csproj -c Release -r win-x64 -o dist/MailTriageAssistant-win-x64 -p:PublishTrimmed=false`
  - `dotnet publish MailTriageAssistant/MailTriageAssistant.csproj -c Release -r win-x86 -o dist/MailTriageAssistant-win-x86 -p:PublishTrimmed=false`
- ZIP 산출물:
  - `dist/MailTriageAssistant-win-x64.zip`
  - `dist/MailTriageAssistant-win-x86.zip`

## Manual E2E Checklist
- Classic Outlook 실행 → "메일 분류 실행" → 50건 로드/표시
- 이메일 선택 → 마스킹된 본문 표시 + 30초 후 클립보드 자동 삭제
- Digest 복사 & Teams 열기 → Teams 열림(https → msteams 폴백) / 실패 시 안내 다이얼로그
- Win+V → 클립보드 히스토리에 Digest 미표시
- New Outlook(olk.exe) 실행 중 → 에러 안내

