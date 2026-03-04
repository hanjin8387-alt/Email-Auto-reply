# Outlook COM 연결 실패 분석

## 1. 목적
MailTriageAssistant에서 Outlook COM 연결 실패 시 원인 범위를 빠르게 축소하기 위한 기술 메모입니다.

## 2. 현재 정책
- Classic Outlook 필수
- New Outlook(olk.exe) 단독 환경은 미지원
- 연결 실패 시 진단 코드 기반으로 사용자 안내

## 3. 점검 순서
1. 프로세스 확인
   - `outlook.exe` 실행 여부
   - `olk.exe`만 실행 중인지 여부
2. 세션/권한 확인
   - 동일 사용자 세션에서 Outlook과 앱 실행
   - UAC 무결성 수준 불일치 여부
3. Outlook 상태 확인
   - Outlook 로그인/프로필 초기화 완료 여부
   - 보안 정책(ObjectModelGuard) 차단 여부
4. 네트워크/정책
   - 조직 보안 정책으로 COM 자동화 차단 여부

## 4. 애플리케이션 진단 포인트
- `OutlookCapabilityDetector`: classic/new 프로세스 탐지 및 진단 코드 생성
- `OutlookSessionHost`: STA COM thread, timeout, 재연결, priority gate
- `OutlookOptions`:
  - `ComTimeoutSeconds`
  - `HeadersCacheTtlSeconds`
  - `MaxFetchCount`
  - `MaxBodyLength`
  - `RestrictDays`

## 5. 사용자 노출 메시지 매핑
- `Classic Outlook이 필요합니다. New Outlook은 지원하지 않습니다.`
  - 원인: `new_outlook_only`
- `Outlook 연결이 불가능합니다. Classic Outlook 실행 상태를 확인하세요.`
  - 원인: Outlook 미실행/세션 불일치/COM 초기화 실패

## 6. 재현이 어려운 경우
- 인터롭 스모크 테스트(`InteropSmokeTests`)는 환경 의존 테스트로 분리되어 있으며
  Outlook 환경 미구성 시 skip됩니다.
