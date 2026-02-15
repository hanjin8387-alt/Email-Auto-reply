---
name: Digest & Teams Agent
description: Copilot 요약 생성 및 Teams 딥링크 연동 (폴백 체인 포함)
---

# Agent 05: Digest & Teams Integration

## 역할
마스킹된 이메일 데이터를 Copilot 최적화 프롬프트로 변환하고, Teams 자기 채팅 창에 전달합니다.

## ⚠️ 핵심 제약
1. 요약 생성 시 **마스킹된 데이터만** 사용 (원본 접근 금지)
2. 클립보드 복사 시 반드시 `ClipboardSecurityHelper.SecureCopy()` 사용
3. Teams 딥링크는 **3단계 폴백 체인** 필수

## 의존성
- `Services/ClipboardSecurityHelper.cs` (Agent 03 산출물)
- `Models/AnalyzedItem.cs` (Agent 01 산출물)

## 생성 파일

### `Services/DigestService.cs`

#### `GenerateDigest(List<AnalyzedItem> items)` → `string`

**출력 형식:**
```markdown
⚠️ SYSTEM PROMPT: You are my executive assistant. Analyze the following REDACTED email digest.

| Priority | Sender | Subject | Summary (Redacted) |
|---|---|---|---|
| 95 🔴 | [EMAIL] | 긴급 보고서 요청 | [마스킹된 본문 요약] |
| 82 🟡 | [EMAIL] | 미팅 일정 | [마스킹된 본문 요약] |
| ...

---
Tasks:
1. Identify the top 3 critical items requiring immediate action.
2. List any deadlines or meeting requests.
3. Draft a polite 1-sentence reply for the top item.

Context: All PII has been redacted. Do NOT ask for unredacted information.
```

- Score ≥ 80: 🔴 (빨강)
- Score ≥ 50: 🟡 (노랑)  
- Score < 50: ⚪ (기본)
- 점수 내림차순 정렬

#### `OpenTeams()` — 3단계 폴백 체인

```csharp
public void OpenTeams(string digest, string? userEmail = null)
{
    // 1. ClipboardSecurityHelper로 안전하게 복사 (30초 자동 삭제)
    _clipboardHelper.SecureCopy(digest);

    var email = userEmail ?? "me"; // 사용자 이메일 또는 기본값

    // 2. 폴백 체인
    try
    {
        // 시도 1: https:// (Microsoft 권장)
        Process.Start(new ProcessStartInfo
        {
            FileName = $"https://teams.microsoft.com/l/chat/0/0?users={email}",
            UseShellExecute = true
        });
    }
    catch
    {
        try
        {
            // 시도 2: msteams:// 프로토콜
            Process.Start(new ProcessStartInfo
            {
                FileName = $"msteams:/l/chat/0/0?users={email}",
                UseShellExecute = true
            });
        }
        catch
        {
            // 시도 3: 수동 안내
            MessageBox.Show(
                "Teams를 열 수 없습니다.\n요약이 클립보드에 복사되었으니 직접 붙여넣어 주세요.",
                "Teams 연결 실패",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
```

## 완료 기준
- Markdown 테이블 생성 확인 (형식 일치)
- Copilot 프롬프트 헤더/태스크/컨텍스트 포함
- Teams 미설치: 폴백 MessageBox 출력
- 클립보드 30초 후 자동 삭제 (ClipboardSecurityHelper 연동 확인)
