---
name: Template Engine Agent
description: 한글 답장 템플릿 서비스 및 Outlook 드래프트 연동 구현
---

# Agent 07: Template Engine

## 역할
사전 정의된 한글 답장 템플릿을 관리하고, 플레이스홀더를 치환하여 Outlook 드래프트를 생성합니다.

## 의존성
- `Services/IOutlookService.cs` (Agent 02 — CreateDraft 사용)
- `Models/ReplyTemplate.cs` (Agent 01)

## 생성 파일

### `Services/TemplateService.cs`

#### 내장 템플릿 (8개)

| ID | 이름 | 용도 |
|---|---|---|
| TMP_01 | 수신 확인 | 메일 확인 + 회신 예정 안내 |
| TMP_02 | 추가 정보 요청 | 필요 정보 목록 요청 |
| TMP_03 | 일정 제안 | 미팅 가능 시간 제안 |
| TMP_04 | 지연 안내 | 처리 지연 사유 + 새 일정 안내 |
| TMP_05 | 완료 보고 | 작업 완료 보고 |
| TMP_06 | 보류/대기 | 유관부서 확인 대기 안내 |
| TMP_07 | 승인 | 결재 승인 통보 |
| TMP_08 | 단순 감사 | 간단한 감사 인사 |

#### 플레이스홀더
```
{TargetDate}  → 회신 예정일
{MissingInfo} → 필요한 추가 정보
{Date1}       → 미팅 옵션 1
{Date2}       → 미팅 옵션 2
{Blocker}     → 지연 사유
{NewDate}     → 새 목표일
{TaskName}    → 작업명
{Dept}        → 유관부서명
{ItemName}    → 결재 건명
```

#### 메서드

```csharp
public class TemplateService
{
    public List<ReplyTemplate> GetTemplates() 
        → 8개 템플릿 목록 반환

    public string FillTemplate(string templateBody, Dictionary<string, string> values)
        → 플레이스홀더를 실제 값으로 치환
        → 미지정 플레이스홀더는 "___" 로 대체 (빈칸 표시)

    public async Task SendDraft(
        IOutlookService outlookService,
        string recipientEmail,
        string subject,
        string templateId,
        Dictionary<string, string> values)
        → 템플릿 선택 → 값 치환 → OutlookService.CreateDraft() 호출
}
```

#### 템플릿 내용 (전체)
```csharp
new ReplyTemplate
{
    Id = "TMP_01",
    Title = "수신 확인 (Acknowledge)",
    BodyContent = "안녕하세요,\n\n메일 잘 받았습니다. 내용 확인 후 {TargetDate}까지 회신 드리겠습니다.\n\n감사합니다."
},
// TMP_02 ~ TMP_08 동일 구조 (사양서의 reply_templates 참조)
```

## 완료 기준
- `GetTemplates()` → 8개 반환
- `FillTemplate("...{TargetDate}...", {"TargetDate": "2/20"})` → 치환 완료
- 미지정 플레이스홀더 → "___" 표시
- `SendDraft()` 호출 → Outlook Inspector 창 열림
