# MailTriageAssistant 사용설명서

## 1. 개요
MailTriageAssistant는 Classic Outlook 받은편지함을 분류하고, 개인정보 마스킹 기반 요약을 제공합니다.

핵심 기능:
- Outlook triage (우선순위 점수/카테고리 분류)
- PII redaction (요약/Digest/클립보드 경로에서 개인정보 마스킹)
- Digest 생성 및 Teams 열기
- 클립보드 30초 자동 삭제(설정값 기반)
- 외부 AI API 비호출
- New Outlook(olk.exe) 단독 환경 미지원

## 2. 실행 전 확인
1. Classic Outlook(`outlook.exe`) 실행 상태를 확인합니다.
2. New Outlook만 실행 중인 환경에서는 COM 연동이 차단됩니다.
3. `appsettings.json`의 `TriageSettings`, `OutlookOptions` 값을 필요 시 조정합니다.

## 3. 기본 사용 흐름
1. `메일 분류 실행` 클릭
2. 좌측 목록에서 메일 선택
3. 우측에서 마스킹 요약 확인
4. 필요 시
   - `Copilot용 복사`
   - `Digest 복사 + Teams`
   - `템플릿 답장`
   - `Outlook에서 열기`

## 4. 자동 분류
- `AutoRefreshIntervalMinutes`가 0보다 크면 자동 분류가 활성화됩니다.
- 연속 실패가 `AutoRefreshFailurePauseThreshold`에 도달하면 자동 분류가 일시 중지됩니다.
- 수동 `메일 분류 실행` 성공 시 자동 분류가 재개됩니다.

## 5. 보안 경계
- UI/클립보드/Digest/템플릿 입력은 redacted 데이터만 사용합니다.
- raw 본문/제목/발신자 정보는 Outlook adapter 및 workflow 내부 경계에서만 유지됩니다.
- 로그에는 예외 메시지 대신 예외 타입/HResult만 기록합니다.

## 6. 템플릿과 프롬프트
- 답장 템플릿: `Resources/Templates/reply_templates.ko.json`
- Digest 프롬프트: `Resources/Prompts/digest_prompt.ko.md`

템플릿 파일이 손상되거나 스키마가 맞지 않으면 앱 시작/로드 시 검증 오류가 발생합니다.

## 7. 설정 파일
- 사용자 VIP 설정 파일: `%APPDATA%/MailTriageAssistant/user_settings.json`
- 저장 방식: temp write -> validate -> atomic replace
- 손상 파일은 `*.corrupt-*.bak`로 백업 후 복구를 시도합니다.

## 8. 상태 메시지 가이드
- `Outlook에서 메일 헤더를 불러오는 중입니다.`: 인박스 헤더 조회 단계
- `본문을 불러오는 중입니다.`: 선택 메일 본문 조회 단계
- `Digest 생성 중입니다.`: Digest workflow 실행 단계
- `자동 분류가 반복 실패로 일시 중지되었습니다.`: 자동 분류 보호 중지 상태

## 9. 지원/제한 사항
- 지원: Windows + Classic Outlook COM 환경
- 제한: New Outlook only 환경, Outlook 미실행 환경, 보안 정책에 의한 COM 차단 환경
