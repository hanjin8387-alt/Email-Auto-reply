---
name: Security & Redaction Agent
description: PII 마스킹 서비스와 클립보드 보안 유틸리티 구현
---

# Agent 03: Security & Redaction

## 역할
개인정보(PII)를 정규식으로 마스킹하고, 클립보드 보안을 관리합니다.

## ⚠️ 보안 규칙 (절대 위반 금지)
1. `Redact()` 메서드의 입력/출력을 **어떤 형태로든 로그에 기록하지 않음**
2. `Console.WriteLine`, `Debug.WriteLine`, `Trace.Write` 사용 금지
3. 예외 발생 시 원본 텍스트가 포함된 메시지를 로그에 남기지 않음

## 생성 파일

### `Services/RedactionService.cs`

```csharp
public class RedactionService
{
    private static readonly (Regex Pattern, string Replacement)[] Rules = new[]
    {
        // 순서 중요: 더 구체적인 패턴을 먼저 배치
        (new Regex(@"\d{4}-\d{4}-\d{4}-\d{4}"), "[CARD]"),   // 신용카드
        (new Regex(@"\d{6}-\d{7}"), "[SSN]"),                  // 주민번호
        (new Regex(@"010-\d{4}-\d{4}"), "[PHONE]"),            // 전화번호
        (new Regex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}"), "[EMAIL]"),
    };

    public string Redact(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        var result = input;
        foreach (var (pattern, replacement) in Rules)
        {
            result = pattern.Replace(result, replacement);
        }
        return result;
    }
}
```

#### 주의사항
- **패턴 순서가 중요합니다:** 신용카드(`\d{4}-\d{4}-\d{4}-\d{4}`)는 전화번호(`010-\d{4}-\d{4}`)보다 먼저 처리해야 합니다.
- Regex는 `static readonly`로 컴파일하여 성능 최적화합니다.
- 입력이 null/empty인 경우 바로 반환합니다.

### `Services/ClipboardSecurityHelper.cs`

```csharp
public class ClipboardSecurityHelper
{
    private DispatcherTimer? _clearTimer;
    private string? _copiedContent;

    /// <summary>
    /// 클립보드에 텍스트를 복사하고, 30초 후 자동 삭제 타이머를 시작합니다.
    /// </summary>
    public void SecureCopy(string text)
    {
        _copiedContent = text;
        Clipboard.SetText(text);
        StartClearTimer();
    }

    private void StartClearTimer()
    {
        _clearTimer?.Stop();
        _clearTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _clearTimer.Tick += (s, e) =>
        {
            _clearTimer.Stop();
            try
            {
                if (Clipboard.ContainsText() && 
                    Clipboard.GetText() == _copiedContent)
                {
                    Clipboard.Clear();
                }
            }
            catch { /* 클립보드 접근 실패 무시 */ }
            _copiedContent = null;
        };
        _clearTimer.Start();
    }
}
```

## 출력 파일
- `Services/RedactionService.cs`
- `Services/ClipboardSecurityHelper.cs`

## 완료 기준
- "010-1234-5678" → "[PHONE]"
- "123456-1234567" → "[SSN]"
- "test@example.com" → "[EMAIL]"
- "1234-5678-9012-3456" → "[CARD]"
- 클립보드 복사 후 30초 뒤 자동 삭제
- Output 창에 원본 텍스트 미노출
