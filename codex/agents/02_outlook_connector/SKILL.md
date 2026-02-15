---
name: Outlook Connector Agent
description: Outlook Classic COM Interop 연동 서비스 구현 (헤더 읽기, 본문 on-demand, 드래프트 생성)
---

# Agent 02: Outlook Connector

## 역할
Classic Outlook COM Interop을 통해 이메일 헤더를 읽고, 요청 시 본문을 가져오며, 답장 드래프트를 생성합니다.

## ⚠️ 핵심 제약
1. **Classic Outlook 전용** — New Outlook(`olk.exe`)은 COM Interop을 지원하지 않음
2. **헤더 우선** — `FetchInboxHeaders()`는 본문을 읽지 않음
3. **본문 로그 금지** — `GetBody()` 결과를 로그에 기록하지 않음
4. **예외 정화** — `COMException` 메시지에 이메일 본문이 포함될 수 있으므로 일반 에러로 변환

## 입력
- `Models/RawEmailHeader.cs` (Agent 01 산출물)

## 생성 파일

### `Services/IOutlookService.cs`
```csharp
public interface IOutlookService
{
    Task<List<RawEmailHeader>> FetchInboxHeaders();
    Task<string> GetBody(string entryId);
    Task CreateDraft(string to, string subject, string body);
}
```

### `Services/OutlookService.cs`
```csharp
public class OutlookService : IOutlookService
```

#### 구현 세부사항

**1. 생성자 / 초기화:**
- `Process.GetProcessesByName("olk")` 로 New Outlook 감지
- New Outlook 감지 시 `throw new NotSupportedException("Classic Outlook이 필요합니다. New Outlook은 COM Interop을 지원하지 않습니다.")`
- `Microsoft.Office.Interop.Outlook.Application` 인스턴스 생성
- `COMException` 발생 시 "Outlook이 실행되지 않았습니다. 먼저 Outlook을 시작해 주세요." 에러

**2. `FetchInboxHeaders()`:**
- `NameSpace.GetDefaultFolder(OlDefaultFolders.olFolderInbox)` 접근
- `Items.Sort("[ReceivedTime]", true)` — 최신순 정렬
- 상위 50개 아이템에서 **헤더만** 읽기:
  - `SenderName`, `SenderEmailAddress`, `Subject`, `ReceivedTime`, `Attachments.Count > 0`
- **Body 속성 접근 금지** (성능 최적화)

**3. `GetBody(string entryId)`:**
- `NameSpace.GetItemFromID(entryId)` 로 아이템 검색
- `MailItem.Body` 가져온 후 **1500자로 잘라내기**
- 결과를 메모리에서만 반환 (로그 기록 금지)
- `COMException` 발생 시 빈 문자열 반환 + 일반 에러 로그

**4. `CreateDraft(string to, string subject, string body)`:**
- `Application.CreateItem(OlItemType.olMailItem)` 으로 새 메일 생성
- `To`, `Subject`, `Body` 설정
- `MailItem.Display()` 로 Inspector 창 열기 (전송하지 않음)

## 완료 기준
- Outlook 미실행 시: "Waiting for Outlook" 상태 메시지
- New Outlook 실행 시: "Classic Outlook이 필요합니다" 에러
- 헤더 50개 성공 로드
- `GetBody()` 결과 1500자 이하
