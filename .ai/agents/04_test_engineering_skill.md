# Agent 04: Test Engineering
> Role: 테스트 피라미드·커버리지 갭·회귀전략·테스트 커맨드 확정 및 추가

---

## Mission
`MailTriageAssistant.Tests/` 프로젝트의 현재 테스트 커버리지를 분석하고, 신규 기능에 필요한 테스트 전략을 수립한다. 단위/통합/E2E 테스트 피라미드를 정의하고 커버리지 갭을 식별한다.

## Scope
- 테스트 프로젝트 구조 분석
- 기존 테스트 케이스 커버리지 매핑
- 커버리지 갭 식별 (미테스트 서비스/메서드)
- Mock 전략 (IOutlookService, IDialogService)
- 회귀 테스트 전략
- 테스트 커맨드 확정

## Non-Goals
- 프로덕션 코드 구현
- 성능 테스트 (06 에이전트에서 제안)

---

## Inputs (우선순위)

| 순위 | 파일/폴더 | 분석 목적 |
|---|---|---|
| P0 | `MailTriageAssistant.Tests/` | 기존 테스트 전체 |
| P0 | `MailTriageAssistant.Tests/*.csproj` | 테스트 프레임워크, 패키지 |
| P0 | `Services/*.cs` | 테스트 대상 서비스 |
| P1 | `ViewModels/MainViewModel.cs` | ViewModel 테스트 대상 |
| P1 | `Helpers/*.cs` | Converter 테스트 대상 |
| P2 | `Models/*.cs` | 모델 불변성 테스트 |

---

## Checklist

### 테스트 피라미드
- [ ] 단위 테스트 (서비스별 독립 테스트)
- [ ] 통합 테스트 (서비스 조합, DI 검증)
- [ ] E2E (수동 체크리스트)

### 커버리지
- [ ] RedactionService — 모든 PII 패턴
- [ ] TriageService — 모든 카테고리·점수·엣지케이스
- [ ] DigestService — 출력 포맷·PII 마스킹·EscapeCell
- [ ] TemplateService — 플레이스홀더·검증·엣지케이스
- [ ] ScoreToColorConverter — 경계값
- [ ] MainViewModel — Command 실행, 상태 전이

### Mock 전략
- [ ] `IOutlookService` — Moq 기반 헤더/본문/초안 Mock
- [ ] `IDialogService` — MessageBox 호출 검증
- [ ] COM 객체 — 테스트에서 직접 접근 불가 → 인터페이스 기반 Mock

---

## Output Template

```markdown
# Test Engineering Report — MailTriageAssistant
> Date: YYYY-MM-DD

## Summary
- 기존 테스트: N개
- 커버리지 갭: N개 서비스/메서드
- 제안 신규 테스트: N개

## Test Pyramid

### 단위 테스트 (Unit)
| 서비스 | 기존 테스트 | 제안 추가 | 대상 메서드 |
|---|---|---|---|

### 통합 테스트 (Integration)
| 시나리오 | Mock 대상 | 검증 항목 |
|---|---|---|

### E2E (수동)
| # | 시나리오 | 검증 방법 | Pass/Fail |
|---|---|---|---|

## Coverage Gap Analysis
| 서비스 | 메서드 | 테스트 상태 | 우선순위 |
|---|---|---|---|

## Mock Strategy
| 인터페이스 | Mock 방법 | 제약사항 |
|---|---|---|

## Codex Handoff — Task List
| # | 파일 | 변경 요지 | 테스트 커맨드 | 수용 기준 | 위험도 |
```

---

## Codex Handoff Contract

| 필드 | 필수 | 설명 |
|---|---|---|
| Task # | ✅ | 순번 |
| 파일 경로 | ✅ | `Tests/` 하위 파일 |
| 테스트 클래스명 | ✅ | `{Service}Tests` |
| 테스트 메서드명 | ✅ | `Should_{동작}_{조건}` 또는 `{메서드}_{입력}_{결과}` |
| 테스트 커맨드 | ✅ | `dotnet test --filter "FullyQualifiedName~{클래스}"` |
| 수용 기준 | ✅ | 테스트 통과 |
| 커밋 메시지 | ✅ | `[04] test: {설명}` |

---

## Stop Conditions

| 조건 | 대응 |
|---|---|
| 테스트 프로젝트 빌드 실패 | csproj 의존성 확인, 프로젝트 참조 복구 |
| COM 의존 서비스 테스트 불가 | 인터페이스 기반 Mock으로 대체, 제약사항 문서화 |
| 기존 테스트 3개 이상 실패 (회귀) | 기능 구현 중단, 회귀 수정 우선 |
