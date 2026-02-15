===== SPEC_PACK.json =====
{
  "meta": {
    "project_name": "MailTriageAssistant",
    "version": "1.0.0",
    "language": "C#",
    "framework": ".NET 8",
    "ui_framework": "WPF",
    "distribution": "MSIX / ClickOnce",
    "target_os": "Windows 10/11"
  },
  "constraints": {
    "security": {
      "no_external_ai_api": true,
      "no_data_persistence_on_disk": true,
      "memory_only_processing": true,
      "enforce_redaction_before_clipboard": true,
      "clear_clipboard_after_paste_seconds": 30,
      "disable_windows_clipboard_history_for_app": true,
      "no_debug_logging_of_body_content": true,
      "sanitize_exception_messages": true
    },
    "integration": {
      "use_graph_api": false,
      "use_outlook_com_interop": true,
      "teams_copilot_interaction": "MANUAL_PASTE",
      "outlook_client_requirement": "Desktop Classic Only (COM Interop)",
      "_note_outlook": "New Outlook (Project Monarch) does NOT support COM Interop. Classic Outlook required."
    },
    "performance": {
      "max_body_char_read": 1500,
      "max_processing_time_per_100_items": "3000ms",
      "body_fetch_mode": "on_select"
    }
  },
  "integrations": {
    "inbound": {
      "source": "Outlook Desktop (Classic Only)",
      "method": "COM Interop (Microsoft.Office.Interop.Outlook)",
      "scope": "Inbox Read-Only",
      "strategy": "Fetch headers first, fetch body on-demand (on user select) in memory",
      "_note": "New Outlook does NOT expose COM objects. Runtime detection required."
    },
    "outbound": {
      "target": "Microsoft Teams",
      "method": "Clipboard + Deep Link",
      "protocol_primary": "https://teams.microsoft.com/l/chat/0/0?users={UserEmail}",
      "protocol_fallback": "msteams:/l/chat/0/0?users={UserEmail}",
      "fallback_message": "Teams를 열 수 없습니다. 요약이 클립보드에 복사되었으니 직접 붙여넣어 주세요.",
      "action": "Open Self-Chat window"
    }
  },
  "features": [
    {
      "id": "FE-001",
      "name": "Outlook COM Connector",
      "priority": "P0",
      "description": "Connects to local Outlook instance to fetch email metadata and body without disk I/O."
    },
    {
      "id": "FE-002",
      "name": "Local Triage Engine",
      "priority": "P0",
      "description": "Categorizes emails based on keywords, sender reputation, and regex rules locally."
    },
    {
      "id": "FE-003",
      "name": "In-Memory Redaction Service",
      "priority": "P0",
      "description": "Masks PII (Phone, SSN, Email, Money) in memory before any data leaves the app context."
    },
    {
      "id": "FE-004",
      "name": "Secure Digest Bridge",
      "priority": "P0",
      "description": "Formats redacted data into a Copilot-optimized prompt, copies to clipboard, and launches Teams."
    },
    {
      "id": "FE-005",
      "name": "WPF Dashboard",
      "priority": "P1",
      "description": "Provides a visual list of prioritized emails with category filtering and manual review."
    },
    {
      "id": "FE-006",
      "name": "Contextual Reply Templates",
      "priority": "P1",
      "description": "Generates Outlook draft emails based on selected categories and predefined templates."
    }
  ],
  "data_contracts": {
    "RawEmailHeader": {
      "EntryId": "string",
      "SenderName": "string",
      "SenderEmail": "string",
      "Subject": "string",
      "ReceivedTime": "DateTime",
      "HasAttachments": "bool"
    },
    "AnalyzedItem": {
      "EntryId": "string",
      "Category": "Enum(Action, VIP, Info, Newsletter, etc.)",
      "PriorityScore": "int (0-100)",
      "RedactedBodySnippet": "string",
      "ActionHint": "string",
      "Tags": "string[]"
    },
    "DigestPayload": {
      "Header": "string (Copilot Instruction)",
      "Body": "string (Markdown Table of AnalyzedItems)",
      "Footer": "string (Context constraints)"
    }
  },
  "rules_engine": {
    "scoring_logic": "BaseScore + (IsVIP * 30) + (HasActionKeyword * 20) - (IsNewsletter * 50)",
    "categories": [
      "Action",
      "VIP",
      "Meeting",
      "Approval",
      "FYI",
      "Newsletter",
      "Other"
    ],
    "default_keywords": {
      "Action": ["요청", "승인", "긴급", "ASAP", "기한", "Due"],
      "Meeting": ["초대", "Invite", "회의", "미팅", "Zoom", "Teams"],
      "Newsletter": ["구독", "광고", "No-Reply", "News"]
    },
    "redaction_patterns": {
      "SSN": "\d{6}-\d{7}",
      "Phone": "010-\d{4}-\d{4}",
      "Email": "[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
      "CreditCard": "\d{4}-\d{4}-\d{4}-\d{4}"
    }
  },
  "reply_templates": [
    {
      "id": "TMP_01",
      "name": "수신 확인 (Acknowledge)",
      "content": "안녕하세요,\n\n메일 잘 받았습니다. 내용 확인 후 {TargetDate}까지 회신 드리겠습니다.\n\n감사합니다."
    },
    {
      "id": "TMP_02",
      "name": "추가 정보 요청 (Request Info)",
      "content": "안녕하세요,\n\n검토를 위해 아래 정보가 추가로 필요합니다.\n- {MissingInfo}\n\n공유 부탁드립니다.\n\n감사합니다."
    },
    {
      "id": "TMP_03",
      "name": "일정 제안 (Propose Time)",
      "content": "안녕하세요,\n\n요청하신 미팅 가능합니다. 다음 슬롯 중 편하신 시간 말씀해주세요.\n- 옵션1: {Date1}\n- 옵션2: {Date2}\n\n감사합니다."
    },
    {
      "id": "TMP_04",
      "name": "지연 안내 (Delay Notice)",
      "content": "안녕하세요,\n\n현재 {Blocker} 이슈로 인해 검토가 지연되고 있습니다. {NewDate}에 업데이트 드리겠습니다.\n\n양해 부탁드립니다."
    },
    {
      "id": "TMP_05",
      "name": "완료 보고 (Task Done)",
      "content": "안녕하세요,\n\n요청하신 {TaskName} 건 처리 완료했습니다. 결과 파일 첨부 드리오니 확인 부탁드립니다.\n\n감사합니다."
    },
    {
      "id": "TMP_06",
      "name": "보류/대기 (On Hold)",
      "content": "안녕하세요,\n\n유관부서({Dept}) 확인이 필요하여 잠시 보류 중입니다. 피드백 받는 대로 공유하겠습니다.\n\n감사합니다."
    },
    {
      "id": "TMP_07",
      "name": "승인 (Approve)",
      "content": "안녕하세요,\n\n상신하신 {ItemName} 건 승인합니다. 계획대로 진행해 주세요.\n\n감사합니다."
    },
    {
      "id": "TMP_08",
      "name": "단순 감사 (Thank You)",
      "content": "안녕하세요,\n\n공유 감사합니다. 업무에 참고하겠습니다.\n\n감사합니다."
    }
  ],
  "digest_format": {
    "instruction_header": "⚠️ SYSTEM PROMPT: You are my executive assistant. Analyze the following REDACTED email digest.",
    "tasks": [
      "Identify the top 3 critical items requiring immediate action.",
      "List any deadlines or meeting requests.",
      "Draft a polite 1-sentence reply for the top item."
    ],
    "markdown_structure": "| Priority | Sender | Subject | Summary (Redacted) |\n|---|---|---|---|\n| {Score} | {Sender} | {Subject} | {BodySnippet} |"
  },
  "ui_spec": {
    "tray_menu": [
      "Status: Idle/Processing",
      "Run Triage Now",
      "Copy Digest to Teams",
      "Open Dashboard",
      "Exit"
    ],
    "dashboard_window": {
      "layout": "TwoColumn",
      "left_panel": "Priority ListBox (Color Coded)",
      "right_panel": "Detail View (Redacted Body + Action Buttons)",
      "action_buttons": ["Open in Outlook", "Copy for Copilot", "Reply with Template"]
    }
  },
  "backlog": [
    {
      "id": "TASK-01",
      "title": "Implement Outlook COM Wrapper",
      "definition_of_done": "Successfully fetch Inbox items and read body strings into memory."
    },
    {
      "id": "TASK-02",
      "title": "Develop Triage Algorithm",
      "definition_of_done": "Unit tests pass for keyword scoring and category assignment."
    },
    {
      "id": "TASK-03",
      "title": "Implement Regex Redactor",
      "definition_of_done": "Sensitive patterns are replaced with [MASKED] tags in output strings."
    },
    {
      "id": "TASK-04",
      "title": "Build Digest Generator",
      "definition_of_done": "Constructs valid Markdown string combining instruction and data."
    },
    {
      "id": "TASK-05",
      "title": "Teams Deep Link Integration",
      "definition_of_done": "Launches Teams chat window and verifies clipboard content."
    },
    {
      "id": "TASK-06",
      "title": "WPF UI Implementation",
      "definition_of_done": "Main dashboard displays analyzed items with correct bindings."
    },
    {
      "id": "TASK-07",
      "title": "Template Engine Logic",
      "definition_of_done": "Clicking a template button opens a new Outlook Inspector window with populated text."
    }
  ],
  "acceptance_tests": [
    {
      "scenario": "Security Compliance",
      "test": "Verify no file creation in app directory or temp folder containing email body text."
    },
    {
      "scenario": "Redaction Quality",
      "test": "Input text with '010-1234-5678' must result in '[PHONE]' or '***' in the clipboard output."
    },
    {
      "scenario": "Offline Handling",
      "test": "If Outlook is closed, app should show 'Waiting for Outlook' status instead of crashing."
    },
    {
      "scenario": "Workflow Efficiency",
      "test": "Time from clicking 'Copy to Teams' to Teams window appearing must be under 2 seconds."
    }
  ]
}

===== CODEX BUILD PROMPT =====
# CODEX_PROMPT
```markdown
You are an expert .NET/C# Developer. Create a complete, compile-ready WPF application named "MailTriageAssistant" using .NET 8. 

**CRITICAL SECURITY & ARCHITECTURE CONSTRAINTS:**
1.  **NO External AI APIs:** Do not add OpenAI, Azure, or any cloud AI SDKs. All logic must be local.
2.  **Memory-Only Processing:** Do not save email bodies to disk (DB, SQLite, Files). Fetch, process in RAM, and discard.
3.  **Redaction:** All PII (Phone, SSN, Email, Credit Cards) must be redacted before copying to clipboard.
4.  **Outlook Integration:** Use `Microsoft.Office.Interop.Outlook` to read the local Inbox. **NOTE: Classic Outlook ONLY. New Outlook does NOT expose COM objects.** Add runtime detection for New Outlook (`olk.exe` process) and show user-friendly error.
5.  **Teams Integration:** No Graph API. Generate a secure Digest string, copy it to the Clipboard, and open a Deep Link. Use `https://teams.microsoft.com/l/chat/...` as primary protocol with `msteams://` as fallback.
6.  **Clipboard Security:** After copying digest, start 30-second timer. On expiry, clear clipboard if it still contains the digest. Use `Clipboard.Clear()` via Dispatcher.
7.  **No Body Logging:** Do NOT use `Console.WriteLine`, `Debug.WriteLine`, or any logger to output email body content. Only log EntryId, Subject (redacted), and metadata.
8.  **Exception Sanitization:** Catch all `COMException` and general exceptions. Do NOT rethrow or log full exception messages that may contain email body text. Wrap in a generic error message.

---

### 1. Project Setup
- Project Name: `MailTriageAssistant`
- Target Framework: `.NET 8.0-windows`
- References: `Microsoft.Office.Interop.Outlook`

### 2. Data Models (`Models/`)
Create class `RawEmailHeader`:
- Properties: `EntryId` (string), `SenderName` (string), `SenderEmail` (string), `Subject` (string), `ReceivedTime` (DateTime), `HasAttachments` (bool).

Create class `AnalyzedItem`:
- Properties: `EntryId` (string), `Sender` (string), `Subject` (string), `ReceivedTime` (DateTime), `Category` (Enum: Action, VIP, Meeting, Approval, FYI, Newsletter, Other), `Score` (int), `RedactedSummary` (string), `ActionHint` (string), `Tags` (string[]).

Create class `ReplyTemplate`:
- Properties: `Id`, `Title`, `BodyContent`.

### 3. Services (`Services/`)

**A0. IOutlookService.cs (Interface)**
- Define interface with methods:
  - `Task<List<RawEmailHeader>> FetchInboxHeaders()`
  - `Task<string> GetBody(string entryId)`
  - `Task CreateDraft(string to, string subject, string body)`
- OutlookService.cs implements this interface.
- Enables unit testing with mocks.

**A. RedactionService.cs**
- Implement a method `Redact(string input)` using these Regex patterns:
  - SSN: `\d{6}-\d{7}` -> `[SSN]`
  - Phone: `010-\d{4}-\d{4}` -> `[PHONE]`
  - Email: `[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}` -> `[EMAIL]`
  - CreditCard: `\d{4}-\d{4}-\d{4}-\d{4}` -> `[CARD]`
- **CRITICAL:** Do NOT log input or output of this method.

**B. TriageService.cs**
- Method `AnalyzeHeader(string sender, string subject)` returning `(Category, int Score, string ActionHint)` — for Phase 1 (headers only).
- Method `AnalyzeWithBody(string sender, string subject, string body)` returning `(Category, int Score, string ActionHint)` — for Phase 2/3 (body available).
- **Scoring Logic:** Base = 0.
  - +30 if VIP (sender not in list, just simulate logic).
  - +20 if subject/body has Action keywords: "요청", "승인", "긴급", "ASAP", "기한", "Due".
  - -50 if Newsletter keywords: "구독", "광고", "No-Reply", "News".
- **Category Logic:** Map keywords to Enums (Meeting: "Zoom", "Teams", "Invite"; Action: "요청", "승인"; etc.).

**C. OutlookService.cs**
- Implements `IOutlookService`.
- Use `Microsoft.Office.Interop.Outlook`.
- **Runtime Detection:** Check for `olk.exe` process (New Outlook). If detected, throw `NotSupportedException` with user-friendly message.
- Method `FetchInboxHeaders()`: Get top 50 items from Inbox. READ ONLY HEADERS (Subject, Sender, ReceivedTime, HasAttachments). Do NOT read Body.
- Method `GetBody(string entryId)`: Fetch `Body` text only when needed, truncate to 1500 chars max. Do NOT log body content.
- Method `CreateDraft(string to, string subject, string body)`: Open a new Outlook inspector window with the draft.

**D. DigestService.cs**
- Method `GenerateDigest(List<AnalyzedItem> items)`:
  - Create a Markdown table: `| Priority | Sender | Subject | Summary |`
  - Prepend this System Prompt: "⚠️ SYSTEM PROMPT: You are my executive assistant. Analyze the following REDACTED email digest."
  - Append tasks: "1. Identify top 3 critical items. 2. List deadlines. 3. Draft reply for #1."
- Method `OpenTeams()`:
  - Copy digest to Clipboard.
  - **Start 30-second clipboard clear timer** (`DispatcherTimer`). On tick, check if clipboard still contains digest text; if so, call `Clipboard.Clear()`.
  - **Primary:** `Process.Start(new ProcessStartInfo { FileName = "https://teams.microsoft.com/l/chat/0/0?users={CurrentUserEmail}", UseShellExecute = true })`
  - **Fallback 1:** If primary throws, try `msteams:/l/chat/0/0?users={CurrentUserEmail}`
  - **Fallback 2:** If both fail, show MessageBox: "Teams를 열 수 없습니다. 요약이 클립보드에 복사되었으니 직접 붙여넣어 주세요."
  - Log success/failure status only (no body content in logs).

**E. TemplateService.cs**
- Hardcode the following templates (Korean):
  - "수신 확인": "안녕하세요,\n\n메일 잘 받았습니다. 내용 확인 후 {TargetDate}까지 회신 드리겠습니다.\n\n감사합니다."
  - "승인": "안녕하세요,\n\n상신하신 {ItemName} 건 승인합니다. 계획대로 진행해 주세요.\n\n감사합니다."
  - "일정 제안": "안녕하세요,\n\n요청하신 미팅 가능합니다. 다음 슬롯 중 편하신 시간 말씀해주세요.\n\n- 옵션1: \n- 옵션2: \n\n감사합니다."
  - (Add "단순 감사", "지연 안내" placeholders as well).

### 4. ViewModel (`ViewModels/MainViewModel.cs`)
- Implement `INotifyPropertyChanged`.
- `ObservableCollection<AnalyzedItem> Emails`.
- `AnalyzedItem? SelectedEmail` with change notification.
- Commands: `LoadEmailsCommand`, `GenerateDigestCommand`, `ReplyCommand`.
- Logic:
  - **Phase 1 (Load):** Call `OutlookService.FetchInboxHeaders()`. For each header, run `TriageService.AnalyzeHeader(sender, subject)` for preliminary score/category. Add to list WITHOUT body.
  - **Phase 2 (Select):** When user selects an item, call `GetBody(entryId)`, run `RedactionService.Redact()`, update `RedactedSummary`. Cache result in memory for session.
  - **Phase 3 (Digest):** On "Copy Digest" click, fetch bodies for Top-N priority items (default 10) that haven't been fetched yet, redact, then call DigestService.
  - On Digest: Filter high-priority items, call DigestService, show MessageBox "클립보드에 복사 완료! Teams를 열고 있습니다...".
  - On Reply: Take selected email, pick a template, replace placeholders (simple regex), call OutlookService.CreateDraft.

### 5. View (`MainWindow.xaml`)
- Layout: Grid with 2 Columns.
- **Left Column:** ListBox of `Emails`. ItemTemplate showing Score (Color coded: High=Red, Low=Gray), Sender, Subject.
- **Right Column:** 
  - TextBlock displaying `RedactedSummary` of selected item.
  - ComboBox for `Template` selection.
  - Button "Reply with Template" (Opens Outlook).
  - Button "Copy Digest & Open Teams" (Bottom area).

### 6. Implementation Notes
- Ensure `COMException` handling (if Outlook is closed).
- Use `Dispatcher` for UI updates from background threads.
- **Generate all files in a single pass.**
```

# FILES_EXPECTED
```text
MailTriageAssistant/
├── MailTriageAssistant.csproj
├── App.xaml
├── App.xaml.cs
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── Models/
│   ├── AnalyzedItem.cs
│   └── ReplyTemplate.cs
├── Services/
│   ├── IOutlookService.cs
│   ├── OutlookService.cs
│   ├── RedactionService.cs
│   ├── TriageService.cs
│   ├── DigestService.cs
│   └── TemplateService.cs
├── ViewModels/
│   ├── MainViewModel.cs
│   └── RelayCommand.cs
└── Helpers/
    └── BooleanToColorConverter.cs
```

# RUN_COMMANDS
```bash
# 1. Initialize Project
dotnet new wpf -n MailTriageAssistant
cd MailTriageAssistant

# 2. Add COM Reference (correct NuGet package)
dotnet add package NetOfficeFw.Outlook
# OR strictly use the COM reference if available on the machine:
# <COMReference Include="Microsoft.Office.Interop.Outlook">... in csproj

# 3. Build and Run
dotnet build
dotnet run
```