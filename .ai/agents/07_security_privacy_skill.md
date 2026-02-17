# Agent 07: Security & Privacy
> Role: 입력검증·비밀정보·권한·저장데이터·PII·취약점·의존성 리스크 분석

---

## Mission
`MailTriageAssistant`의 보안·프라이버시 태세를 분석한다. PII 마스킹 커버리지, 클립보드 보안, COM 보안, 입력 인젝션 방어, 의존성 취약점을 검토한다.

## Scope
- PII 마스킹 (RedactionService 패턴 커버리지 + 유니코드 우회)
- 클립보드 보안 (Win+V 히스토리, 레이스 컨디션, 자동 삭제)
- 데이터 유출 경로 (로그, 예외 메시지, XAML 바인딩, StatusBar)
- 입력 인젝션 (Markdown 인젝션, Template 인젝션, URL 인젝션)
- COM 보안 (RPC 타임아웃, 릴리스 후 접근, 동기화)
- 의존성 취약점 (NuGet Audit, 구버전 패키지)
- Banned API (Console.Write, Debug.Write 금지)

## Non-Goals
- 네트워크 보안 (앱은 로컬 전용)
- 인증/권한 (단일 사용자 데스크톱 앱)

---

## Inputs (우선순위)

| 순위 | 파일 | 분석 목적 |
|---|---|---|
| P0 | `Services/RedactionService.cs` | PII 마스킹 패턴 |
| P0 | `Services/ClipboardSecurityHelper.cs` | 클립보드 보안 |
| P0 | `ViewModels/MainViewModel.cs` | ex.Message 노출, StatusBar |
| P0 | `MainWindow.xaml` | XAML 바인딩 PII 노출 |
| P1 | `Services/DigestService.cs` | Markdown 인젝션, URL 검증 |
| P1 | `Services/TemplateService.cs` | 템플릿 인젝션 |
| P1 | `Services/OutlookService.cs` | COM 보안 |
| P1 | `App.xaml.cs` | 글로벌 에러 핸들러 |
| P2 | `MailTriageAssistant.csproj` | NuGet 취약점 |
| P2 | `BannedSymbols.txt` | Banned API 규칙 |

---

## Checklist

### PII 마스킹
- [ ] 한국 전화번호 (`010-XXXX-XXXX`)
- [ ] 한국 주민번호 (`XXXXXX-XXXXXXX` + 13자리 연속)
- [ ] 이메일 주소
- [ ] 신용카드 (하이픈 + 공백)
- [ ] 한국 계좌번호
- [ ] 여권번호
- [ ] IP 주소
- [ ] URL 토큰/키
- [ ] 유니코드 정규화 (전각 → 반각)

### 데이터 유출 경로
- [ ] `Console.Write*` 호출 0건
- [ ] `Debug.Write*` 호출 0건
- [ ] `Trace.Write*` 호출 0건
- [ ] `ex.Message` UI 직접 노출 0건
- [ ] XAML Sender/Subject 마스킹

### 클립보드
- [ ] Win+V 히스토리 차단
- [ ] 30초 자동 삭제
- [ ] 레이스 컨디션 완화

### COM
- [ ] RPC 타임아웃
- [ ] 릴리스 후 접근 방지
- [ ] 동기화 lock

### 의존성
- [ ] `dotnet list package --vulnerable` 실행
- [ ] NuGetAudit 설정

---

## Output Template

```markdown
# Security & Privacy Report — MailTriageAssistant
> Date: YYYY-MM-DD

## Summary
- 총 이슈: N | Critical: N | Major: N | Minor: N | Info: N

## Threat Model Summary
| 위협 | 벡터 | 현재 완화 | 갭 |
|---|---|---|---|

## Findings
### 🔴 Critical
| # | 카테고리 | 파일:줄 | 이슈 | CVSS (추정) | 권장사항 |

### 🟡 Major
| # | 카테고리 | 파일:줄 | 이슈 | CVSS (추정) | 권장사항 |

### 🟢 Minor
| # | 카테고리 | 파일:줄 | 이슈 | CVSS (추정) | 권장사항 |

## Redaction Coverage Matrix
| PII 유형 | 패턴 | 상태 | 비고 |
|---|---|---|---|

## Codex Handoff — Task List
| # | 파일 | 변경 요지 | 테스트 커맨드 | 수용 기준 | 위험도 |
```

---

## Codex Handoff Contract

| 필드 | 필수 | 설명 |
|---|---|---|
| Task # | ✅ | 순번 |
| 파일 경로 | ✅ | 보안 수정 대상 |
| CVSS (추정) | ✅ | 심각도 수치 |
| 역테스트 | ✅ | 우회 시도 실패 확인 테스트 |
| 테스트 커맨드 | ✅ | `dotnet test --filter "..."` |
| 커밋 메시지 | ✅ | `[07] security: {설명}` |

---

## Stop Conditions

| 조건 | 대응 |
|---|---|
| CVSS ≥ 9.0 취약점 발견 | 즉시 보고 + 모든 기능 작업 중단 |
| PII 마스킹 우회 확인 | 패턴 수정 선행 → 기능 구현 후행 |
| 의존성 Critical CVE | 즉시 패키지 업데이트 |
| 로그에 본문 포함 | BannedSymbols.txt 업데이트 + 코드 제거 |
