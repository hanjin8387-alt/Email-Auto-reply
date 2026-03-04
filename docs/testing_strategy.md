# Testing Strategy

## 범위
서비스/워크플로 중심 단위 테스트를 우선하고 Outlook 실기 의존은 분리합니다.

## 테스트 계층
1. Unit tests
- `JsonSettingsService`
  - atomic write
  - corrupt recovery
- `AutoRefreshController`
  - 실패 누적 후 pause
  - 수동 성공 후 resume
- `SelectedEmailBodyLoader`
  - 선택 변경 시 취소/중복 방지
- `EmailListProjectionService`
  - selection restore

2. Integration-like tests (mocked)
- `GenerateDigestWorkflow`
- `CreateReplyDraftWorkflow`

3. Interop smoke tests
- `InteropSmokeTests`
- Outlook 환경 미구성 시 skip

## 환경 의존 검증
`InteropSmokeTests`는 조건부 테스트이며 Classic Outlook을 사용할 수 없는 환경에서는 skip됩니다.

## 테스트 더블 추상화
- `IClock`
- `IClipboardService`
- `IExternalLauncher`
- `IOutlookMailGateway`

## 실행
```powershell
dotnet build MailTriageAssistant/MailTriageAssistant.csproj
dotnet test MailTriageAssistant.Tests/MailTriageAssistant.Tests.csproj
```
