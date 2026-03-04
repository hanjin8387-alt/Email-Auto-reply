# Security Boundary

## 데이터 경계 타입
- `RawEmailContent`: Outlook adapter/workflow 내부 전용
- `RedactedEmailContent`: UI/요약/표시 전용
- `DigestEmailItem`: Digest 생성 입력 전용(redacted only)
- `ReplyDraftRequest`: Outlook draft 생성 요청

## 허용 경로
1. Outlook adapter
- raw 헤더/본문 읽기 허용
- COM 호출 실패 시 예외 타입/HResult 중심 로깅

2. Workflow
- triage 계산/본문 분석 시 raw 사용 가능
- UI 전달 전 반드시 redacted 타입으로 변환

3. UI/Clipboard/Digest/Template
- redacted 데이터만 소비
- 클립보드 복사 전 redaction 재적용
- 자동 삭제 타이머 적용

## 비허용 경로
- raw subject/sender/body/PII를
  - terminal output
  - 일반 로그 메시지
  - 문서 예시
  - 테스트 snapshot
  에 기록 금지

## 장애 대응
- settings 파일 손상 시 백업 후 복구 시도
- Outlook unsupported/unavailable 상태를 분리 안내
- 자동 분류 연속 실패 시 자동 일시 중지
