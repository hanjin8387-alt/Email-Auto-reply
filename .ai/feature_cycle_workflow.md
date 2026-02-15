# Feature Enhancement Cycle Workflow — MailTriageAssistant
> Version: 1.0
> Date: 2026-02-13
> Scope: 모든 기능강화 요청에 적용되는 표준 개발 사이클

---

## 핵심 원칙

| # | 원칙 | 설명 |
|---|---|---|
| 1 | **작은 커밋** | 1 task = 1 commit, 최대 5파일·200줄 |
| 2 | **테스트 게이트** | 커밋마다 `dotnet build && dotnet test` 통과 필수 |
| 3 | **즉시 롤백** | 빌드/테스트 실패 시 `git revert HEAD` |
| 4 | **기능 플래그** | 대규모 변경은 런타임 플래그로 비활성화 가능 |
| 5 | **보안 불변** | 본문 로그 금지, 클립보드 30초 삭제, PII 마스킹 |

---

## Phase 1: 요청 정규화 (Request Normalization)

### 목적
기능 요청을 구조화된 유저스토리 + 수용기준으로 변환

### 담당 에이전트
- **01 Feature Architect**

### 산출물
- 유저스토리 (As a / I want / So that)
- 수용기준 (Given/When/Then)
- 엣지케이스 목록
- 데이터 모델 변경 사항
- 마이그레이션 필요 여부
- 기능 플래그 설계

### Definition of Done
- [ ] 유저스토리 1개 이상 작성
- [ ] 수용기준 3개 이상 정의
- [ ] 엣지케이스 2개 이상 식별
- [ ] 데이터 모델 영향 분석 완료
- [ ] 기존 기능과의 충돌 분석 완료

### 실패/중단 기준
- 요청이 모호하여 유저스토리 정의 불가 → 요청자에게 명확화 요청
- 기존 아키텍처와 근본적 충돌 → 아키텍처 리팩토링 선행 필요 보고서 생성

---

## Phase 2: 영향 분석 (Impact Analysis)

### 목적
변경이 미치는 영향을 코드·UI·성능·보안·테스트 관점에서 분석

### 담당 에이전트
- **02 UI/UX** — 화면 영향
- **03 Backend/API** — 로직/데이터 영향
- **06 Perf/Reliability** — 성능 영향
- **07 Security/Privacy** — 보안 영향

### 산출물
- 영향 범위 맵 (파일/함수 단위)
- 위험 등급 평가 (Low/Medium/High/Critical)
- 의존성 그래프 (이 변경이 영향 미치는 컴포넌트)

### Definition of Done
- [ ] 영향받는 파일 목록 확정
- [ ] 위험 등급 할당 완료
- [ ] 의존성 충돌 0건 확인 또는 해결방안 제시
- [ ] 보안 영향 평가 완료

### 실패/중단 기준
- 영향 범위가 코드베이스 30% 이상 → 마이크로 단위 분해 후 재진입
- 보안 위험 High 이상 → 보안 선행 수정 후 기능 구현

---

## Phase 3: 설계 + 백로그 구성 (Design & Backlog)

### 목적
구현 계획을 커밋 단위 태스크로 분해하고 우선순위 배정

### 담당 에이전트
- **01 Feature Architect** — 기술 설계
- **04 Test Engineering** — 테스트 설계
- **05 Observability/Analytics** — 관측 설계

### 산출물
- 기술 설계 문서 (클래스/인터페이스/시퀀스 다이어그램)
- 커밋 단위 태스크 목록 (P0→P1→P2)
- 테스트 계획 (단위/통합/E2E)
- 관측 지표 정의 (로그/메트릭/이벤트)

### Definition of Done
- [ ] 커밋 단위 태스크 생성 (ID 부여)
- [ ] 각 태스크에 테스트 명령 할당
- [ ] 의존성 순서 확정 (Critical Path)
- [ ] 관측 지표 1개 이상 정의

### 실패/중단 기준
- 태스크 간 순환 의존성 → 설계 재검토
- 테스트 불가능한 컴포넌트 존재 → 테스트 가능하도록 리팩토링 태스크 선행

---

## Phase 4: Codex 구현 (Implementation)

### 목적
설계된 태스크를 코드로 구현

### 담당 에이전트
- **Codex** (CODEX_Skill.md 규칙 준수)

### 산출물
- 구현 코드 (1 task = 1 commit)
- 단위 테스트 (기능 커밋과 동일 or 직전 커밋)
- 변경 로그: `.ai/reports/2026-02-13_codex_change_log.md`

### Definition of Done
- [ ] 매 커밋: `dotnet build MailTriageAssistant/MailTriageAssistant.csproj` 성공
- [ ] 매 커밋: `dotnet test` 전체 통과 (테스트 프로젝트 존재 시)
- [ ] 커밋 메시지 규칙 준수: `[에이전트번호] 카테고리: 한줄 설명`
- [ ] 변경 로그 업데이트

### 실패/중단 기준
- 빌드 실패 → 즉시 `git revert HEAD`
- 테스트 2회 연속 실패 → 중단 + `.ai/reports/2026-02-13_codex_blockers.md` 생성
- 보안 불변 규칙 위반 → 즉시 롤백

---

## Phase 5: 테스트 + 회귀 검증 (Testing & Regression)

### 목적
구현된 기능의 정확성과 기존 기능의 동작 보전 검증

### 담당 에이전트
- **04 Test Engineering** — 테스트 검증
- **06 Perf/Reliability** — 성능 회귀 검증
- **07 Security/Privacy** — 보안 회귀 검증

### 산출물
- 테스트 결과 리포트
- 성능 벤치마크 (Before/After)
- 보안 역테스트 결과

### Definition of Done
- [ ] 단위 테스트 100% 통과
- [ ] 새 기능 커버리지 ≥ 80%
- [ ] 성능 회귀 없음 (±10% 이내)
- [ ] 보안 역테스트 통과
- [ ] E2E 수동 검증 체크리스트 완료

### 실패/중단 기준
- 기존 테스트 실패 → 구현 Phase로 롤백
- 성능 20% 이상 저하 → 최적화 태스크 추가 후 재검증
- PII 유출 감지 → 즉시 전체 기능 롤백

---

## Phase 6: 릴리즈 체크 (Release Check)

### 목적
배포 준비 상태 확인

### 담당 에이전트
- **05 Observability/Analytics** — 관측 도구 확인
- **07 Security/Privacy** — 최종 보안 스캔

### 산출물
- 릴리즈 노트 초안
- 관측 대시보드/로그 설정 확인
- 의존성 취약점 스캔 결과

### Definition of Done
- [ ] `dotnet build -c Release` 성공
- [ ] `dotnet list package --vulnerable` 취약점 0건
- [ ] 모든 기능 플래그 상태 문서화
- [ ] 릴리즈 노트 작성

### 실패/중단 기준
- 취약점 High 이상 → 수정 후 재스캔
- 빌드 경고 10건 이상 → 경고 해소 후 재시도

---

## 사이클 전체 흐름도

```
                    ┌──────────────────────────┐
                    │   기능 요청 (Feature Req) │
                    └────────────┬─────────────┘
                                 ▼
                 ┌───────────────────────────────┐
Phase 1          │  요청 정규화 (Normalization)   │
                 │  Agent: 01 Feature Architect   │
                 └───────────────┬───────────────┘
                                 ▼
                 ┌───────────────────────────────┐
Phase 2          │  영향 분석 (Impact Analysis)   │
                 │  Agents: 02,03,06,07           │
                 └───────────────┬───────────────┘
                                 ▼
                 ┌───────────────────────────────┐
Phase 3          │  설계 + 백로그 (Design)        │
                 │  Agents: 01,04,05              │
                 └───────────────┬───────────────┘
                                 ▼
                 ┌───────────────────────────────┐
Phase 4          │  Codex 구현 (Implementation)   │
                 │  Executor: Codex               │
                 └───────────────┬───────────────┘
                                 ▼
                 ┌───────────────────────────────┐
Phase 5          │  테스트 + 회귀 (Test/Regress)  │
                 │  Agents: 04,06,07              │
                 └───────────────┬───────────────┘
                                 ▼
                 ┌───────────────────────────────┐
Phase 6          │  릴리즈 체크 (Release Check)   │
                 │  Agents: 05,07                 │
                 └───────────────┴───────────────┘
```

---

## 실패 시 에스컬레이션 경로

| 실패 유형 | 대응 |
|---|---|
| Phase 1 실패 (요청 모호) | 요청자에게 명확화 질문 생성 |
| Phase 2 실패 (영향 범위 과대) | 기능을 Milestone 단위로 분해 |
| Phase 3 실패 (순환 의존성) | 아키텍처 리팩토링 선행 Phase 추가 |
| Phase 4 실패 (빌드/테스트 실패 2회) | Blocker 리포트 생성, 사람 검토 요청 |
| Phase 5 실패 (회귀) | 해당 커밋 롤백 후 Phase 4 재진입 |
| Phase 6 실패 (취약점) | Phase 1~4 보안 패치 사이클 실행 |

---

## 환경 정보 (Assumptions)

| 항목 | 값 | 확실성 |
|---|---|---|
| 언어 | C# (.NET 8) | ✅ 확인 |
| 프레임워크 | WPF (UseWPF=true) | ✅ 확인 |
| 빌드 커맨드 | `dotnet build MailTriageAssistant/MailTriageAssistant.csproj` | ✅ 확인 |
| 테스트 커맨드 | `dotnet test MailTriageAssistant.Tests/` | ⚠️ 추정 (테스트 프로젝트 미생성) |
| 패키지 매니저 | NuGet (dotnet CLI) | ✅ 확인 |
| VCS | Git (로컬) | ⚠️ 추정 |
| 아키텍처 패턴 | MVVM | ✅ 확인 |
| COM 의존성 | Microsoft.Office.Interop.Outlook 15.0 | ✅ 확인 |
| 테스트 프레임워크 | xUnit + Moq (권장) | ⚠️ 추정 |
| DI 컨테이너 | 미사용 (수동 new) | ✅ 확인 |
