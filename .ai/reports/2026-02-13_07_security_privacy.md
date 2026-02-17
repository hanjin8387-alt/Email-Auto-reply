# Security & Privacy Report — MailTriageAssistant
> Date: 2026-02-13
> Reviewer: Agent 07 (Security & Privacy)

## Summary
- 총 이슈: 8 | Critical: 1 | Major: 3 | Minor: 3 | Info: 1

## Threat Model Summary

| 위협 | 벡터 | 현재 완화 | 상태 |
|---|---|---|---|
| PII 유출 (클립보드) | Win+V 히스토리 | `ExcludeClipboardContentFromMonitorProcessing` 포맷 적용 | ✅ 완화됨 |
| PII 유출 (미마스킹 패턴) | 계좌·IP·여권·URL 토큰 | 패턴 구현 완료 (10종) | ✅ 완화됨 |
| PII 유출 (유니코드 우회) | 전각 숫자 | `NormalizeToAsciiDigits` (FormKC) 적용 | ✅ 완화됨 |
| PII 유출 (XAML 바인딩) | Sender/Subject 원본 | `RedactionConverter` 적용 | ✅ 완화됨 |
| PII 유출 (예외 메시지) | StatusBar `ex.Message` | 상수 메시지 사용 | ✅ 완화됨 |
| 입력 인젝션 (Markdown) | Digest EscapeCell | 특수문자 이스케이프 (`[`,`]`,`(`,`)`,`!`,`<`,`>`) | ✅ 완화됨 |
| 입력 인젝션 (Template) | 플레이스홀더 | 중괄호 제거 + 200자 제한 | ✅ 완화됨 |
| COM 보안 | RPC 타임아웃 | 30초 타임아웃 적용 | ✅ 완화됨 |
| **설정 파일 변조** | `appsettings.json` 직접 편집 | **미처리** | ❌ |
| **VIP UI 입력 인젝션** | 악의적 이메일 주소 입력 | **미처리 (기능 미구현)** | ⚠️ |

---

## Findings

### 🔴 Critical

| # | 카테고리 | 파일 | 이슈 | CVSS | 권장사항 |
|---|---|---|---|---|---|
| S-01 | 설정 파일 보안 | `appsettings.json` | 앱 디렉토리에 **평문 설정 파일**. VIP 이메일 주소, Teams 이메일이 노출됨. 다른 프로세스/사용자가 읽기 가능 | 5.5 | (1) `user_settings.json`은 `%AppData%`에 저장. (2) 민감 설정은 DPAPI 암호화 검토. (3) 최소한 VIP 이메일은 해시 저장 고려 |

### 🟡 Major

| # | 카테고리 | 파일 | 이슈 | CVSS | 권장사항 |
|---|---|---|---|---|---|
| S-02 | VIP 입력 검증 | 신규 `SettingsWindow` | VIP 관리 UI에서 사용자 입력 시 **이메일 형식 검증 없이 저장하면 TriageService에 오작동 가능**. 악의적 Regex 패턴 삽입 시도 | 4.5 | 이메일 정규식 검증 + 길이 제한(254자) + 금지 문자 필터 |
| S-03 | 자동 분류 보안 | `MainViewModel` (신규 타이머) | 자동 분류 시 COM 에러가 반복되면 **에러 로그 플러딩** + 무한 재시도 | 4.0 | 연속 실패 카운터 → 3회 실패 시 자동 분류 일시 정지 + 사용자 알림 |
| S-04 | 세션 통계 PII | 신규 `SessionStats` | 세션 통계에 **발신자별 카운트** 등 PII-인접 데이터가 포함될 수 있음 | 3.5 | 통계는 카테고리별 집계만 허용, 개별 발신자 정보 미포함 |

### 🟢 Minor

| # | 카테고리 | 파일 | 이슈 | CVSS | 권장사항 |
|---|---|---|---|---|---|
| S-05 | 클립보드 | `ClipboardSecurityHelper.cs:83-85` | `Clipboard.ContainsText() → GetText()` 연쇄 호출 사이에 미세한 **레이스 윈도우** 존재 | 2.5 | `GetClipboardSequenceNumber` P/Invoke로 원자적 비교 (이전 리포트에서 제안됨) |
| S-06 | 의존성 | `.csproj:27` | `Microsoft.Office.Interop.Outlook 15.0.4797.1004` — Office 2013 시절. NuGet 패키지 자체에 보안 패치 메커니즘 없음 | 2.0 | `dotnet list package --outdated` 확인, COM PIA는 Office 설치에 의존하므로 제한적 |
| S-07 | 다국어 XSS | 신규 `Strings.en.xaml` | ResourceDictionary 문자열에 **`<` `>` 문자 포함 시 XAML 파싱 오류** | 2.0 | 문자열에 XML 특수문자 포함 시 `xml:space="preserve"` 또는 `&lt;` 이스케이프 |

### ⚪ Info

| # | 카테고리 | 이슈 |
|---|---|---|
| I-01 | 긍정 | `BannedSymbols.txt` + `NuGetAudit=true` + `RedactionConverter` + `EscapeCell` 강화 등 이전 보안 수정 사항 대부분 적용됨 ✅ |

---

## Redaction Coverage Matrix (현재 상태)

| PII 유형 | 패턴 | 상태 |
|---|---|---|
| 한국 전화번호 | `010-\d{4}-\d{4}` | ✅ |
| 한국 주민번호 (하이픈) | `\d{6}-\d{7}` | ✅ |
| 한국 주민번호 (연속) | `\d{6}[1-8]\d{6}` | ✅ |
| 이메일 | RFC 기본 패턴 | ✅ |
| 신용카드 (하이픈) | `\d{4}-\d{4}-\d{4}-\d{4}` | ✅ |
| 신용카드 (공백) | `\d{4}\s\d{4}\s\d{4}\s\d{4}` | ✅ |
| 계좌번호 | 라벨+숫자 복합 패턴 | ✅ |
| 여권번호 | 라벨+`[A-Z]\d{8}` | ✅ |
| IPv4 | octet 패턴 | ✅ |
| URL 토큰 | `token/key/secret/password=...` | ✅ |
| 유니코드 정규화 | `NormalizationForm.FormKC` | ✅ |

---

## Codex Handoff — Task List

| # | 파일 | 변경 요지 | 테스트 커맨드 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|
| T-01 | 신규 `Services/JsonSettingsService.cs` | VIP 설정 파일을 `%AppData%/MailTriageAssistant/user_settings.json`에 저장 | `dotnet build` | 빌드 성공 + 설정 파일 경로 확인 | Medium |
| T-02 | 신규 `SettingsWindow.xaml` (VIP UI) | VIP 이메일 입력 시 **이메일 형식 검증** + 길이 제한 254자 | `dotnet build` | 빌드 성공 | Medium |
| T-03 | `MainViewModel.cs` (자동 분류) | 연속 실패 카운터 구현 → 3회 실패 시 `AutoRefreshPaused = true` + 사용자 알림 | `dotnet build && dotnet test` | 빌드+테스트 통과 | Low |
| T-04 | 신규 `SessionStats.cs` | 통계에 **카테고리별 집계만 포함** — 개별 발신자/제목 미저장 확인 | `dotnet build` | 빌드 성공 | Low |
| T-05 | `ClipboardSecurityHelper.cs` | `GetClipboardSequenceNumber` P/Invoke 적용 — 레이스 컨디션 완화 | `dotnet build` | 빌드 성공 | Low |
