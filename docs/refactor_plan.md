# Refactor Plan

## 목표
기능 유지 + 구조 재설계 + 보안 경계 강화 + 테스트 재구성.

## 구조 변경 요약
1. MainViewModel slimming
- `InboxRefreshCoordinator`
- `SelectedEmailBodyLoader`
- `AutoRefreshController`
- `GenerateDigestWorkflow`
- `CreateReplyDraftWorkflow`
- `EmailListProjectionService`

2. Outlook infrastructure split
- `OutlookCapabilityDetector`
- `OutlookSessionHost`
- `OutlookInboxReader`
- `OutlookBodyReader`
- `OutlookDraftComposer`
- `OutlookItemLauncher`
- `OutlookOptions`

3. Template/Digest externalization
- `Resources/Templates/reply_templates.ko.json`
- `Resources/Prompts/digest_prompt.ko.md`
- `TemplateService = loader + validator + renderer` 조합

4. Settings reliability
- `UserSettingsV1`
- atomic save
- corrupt backup/recovery

5. Triage modernization
- `CompiledTriageRules`
- `VipSenderProvider`
- `MatchedRules/Reasons`
- sync-over-async 제거

## 적용 원칙
- UI/clipboard/digest/template/logging 경로에서 raw 데이터 사용 금지
- 기능 회귀 없이 기존 사용자 흐름 유지
- 테스트 더블 가능한 추상화 도입(`IClock`, `IClipboardService`, `IExternalLauncher`, `IOutlookMailGateway`)
