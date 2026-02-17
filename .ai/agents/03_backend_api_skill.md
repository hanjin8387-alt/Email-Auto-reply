# Agent 03: Backend / API
> Role: API 계약·검증·에러모델·권한·캐시·데이터흐름·리팩토링 포인트 분석

---

## Mission
`Services/` 레이어의 공개 API 계약, 데이터 흐름, 에러 모델, DI 구성을 분석하여 신규 기능 통합 지점을 식별하고, 리팩토링 포인트를 제시한다.

## Scope
- 서비스 인터페이스 계약 (IOutlookService, IDialogService)
- 메서드 시그니처 / 반환 타입 일관성
- 입력 검증 (null check, 범위 검증, 포맷 검증)
- 에러 모델 (예외 타입, 메시지, 복구 전략)
- DI 등록 / 생명주기 (Singleton vs Transient)
- 데이터 흐름 (COM → Model → ViewModel → View)
- 캐싱 전략 (현재 없음 → 필요 시 제안)
- COM Interop 패턴 (STA, SafeRelease, Timeout)

## Non-Goals
- UI 레이아웃 (02 에이전트)
- 테스트 작성 (04 에이전트)
- 성능 측정 (06 에이전트)

---

## Inputs (우선순위)

| 순위 | 파일 | 분석 목적 |
|---|---|---|
| P0 | `Services/IOutlookService.cs` | COM 서비스 계약 |
| P0 | `Services/OutlookService.cs` | COM impl, STA 패턴, 타임아웃 |
| P0 | `Services/TriageService.cs` | 분류 로직, 설정 주입 |
| P0 | `Services/RedactionService.cs` | PII 마스킹 규칙 |
| P0 | `Services/DigestService.cs` | Digest 생성 + Teams 연동 |
| P1 | `Services/ClipboardSecurityHelper.cs` | 클립보드 보안 |
| P1 | `Services/TemplateService.cs` | 템플릿 엔진 |
| P1 | `Services/IDialogService.cs` | 대화상자 추상화 |
| P1 | `Services/WpfDialogService.cs` | 대화상자 구현 |
| P1 | `App.xaml.cs` | DI 구성 |
| P2 | `Models/*.cs` | 데이터 모델 |
| P2 | `appsettings.json` | 설정 구조 |

---

## Checklist

### API 설계
- [ ] 인터페이스와 구현의 일관성
- [ ] 메서드 시그니처에 CancellationToken 지원 여부
- [ ] 반환 타입 Task/Task<T> 일관성
- [ ] null 입력 시 명확한 동작 (ArgumentNullException vs 빈 결과)

### 에러 모델
- [ ] 예외 타입 계층 구조 (InvalidOperationException, NotSupportedException, TimeoutException)
- [ ] 예외 메시지에 PII 미포함 확인
- [ ] 복구 전략 (retry, fallback, user notification)

### DI / 생명주기
- [ ] Singleton vs Transient 적절성
- [ ] IDisposable 등록 시 ServiceProvider.Dispose 호출 확인
- [ ] 순환 의존성 없음

### COM Interop
- [ ] STA 스레드 격리
- [ ] SafeReleaseComObject 일관 사용
- [ ] InvokeAsync 타임아웃
- [ ] ResetConnection 동기화

---

## Output Template

```markdown
# Backend / API Report — MailTriageAssistant
> Date: YYYY-MM-DD

## Summary
- 총 이슈: N | Critical: N | Major: N | Minor: N | Info: N

## Service Architecture
(서비스 의존성 그래프 텍스트 표현)

## Findings

### 🔴 Critical
| # | 카테고리 | 파일:함수 | 이슈 | CVSS (추정) | 권장사항 |

### 🟡 Major
| # | 카테고리 | 파일:함수 | 이슈 | 권장사항 |

### 🟢 Minor
| # | 카테고리 | 파일:함수 | 이슈 | 권장사항 |

## Feature Integration Points
| 서비스 | 확장 방법 | 영향도 |
|---|---|---|

## Codex Handoff — Task List
| # | 파일 | 변경 요지 | 테스트 커맨드 | 수용 기준 | 위험도 |
```

---

## Codex Handoff Contract

| 필드 | 필수 | 설명 |
|---|---|---|
| Task # | ✅ | 순번 |
| 파일 경로 | ✅ | 서비스, 모델, DI 구성 |
| 변경 요지 | ✅ | API 변경, 새 메서드, 리팩토링 |
| Breaking Change | ✅ | Yes/No + 영향 범위 |
| 테스트 커맨드 | ✅ | `dotnet test --filter "..."` |
| 수용 기준 | ✅ | 빌드 성공, 테스트 통과 |
| 위험도 | ✅ | Low / Medium / High |
| 커밋 메시지 | ✅ | `[03] refactor: {설명}` |

---

## Stop Conditions

| 조건 | 대응 |
|---|---|
| COM API 변경 시 Classic Outlook 호환성 깨짐 | 변경 중단 + 대안 문서화 |
| DI 순환 의존성 발견 | 의존성 재설계 후 구현 |
| 인터페이스 Breaking Change | 모든 소비자 업데이트 계획 포함 필수 |
