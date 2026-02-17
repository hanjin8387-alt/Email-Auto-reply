# Agent 05: Build / Bundle Size
> Role: Publish 크기 분석, Trimming 최적화, 의존성 경량화, 빌드 플래그 정리

---

## Mission
.NET 8 Publish 바이너리 크기를 분석하고, Trimming/SingleFile/의존성/리소스 최적화를 통한 크기 감소를 설계한다.

## Scope
- `dotnet publish` 출력 크기 분석
- `PublishTrimmed`, `TrimMode`, `PublishSingleFile` 설정 검토
- 불필요 NuGet 패키지 식별
- COM Interop PIA 크기
- 리소스(XAML) 임베딩 크기
- ReadyToRun (R2R) 적용 가능성

## Non-Goals
- 런타임 성능 (03 에이전트)
- 외부 CI/CD 구성

---

## Inputs

| 순위 | 파일 | 분석 목적 |
|---|---|---|
| P0 | `MailTriageAssistant.csproj` | 빌드 설정, NuGet, Publish 플래그 |
| P1 | `App.xaml` | 리소스 딕셔너리 크기 |
| P2 | `Helpers/`, `Services/` | 미사용 코드 식별 |

---

## Checklist
- [ ] `dotnet publish -c Release -r win-x64 --self-contained` 출력 크기
- [ ] `PublishTrimmed` = true
- [ ] `TrimMode` 값 (full vs partial)
- [ ] `PublishSingleFile` = true
- [ ] COM Interop PIA 크기 (`Microsoft.Office.Interop.Outlook`)
- [ ] NuGet 기여도 (Serilog, Microsoft.Extensions.*)
- [ ] ReadyToRun 적용 여부
- [ ] Debug 심볼 제외 확인

---

## Output Template

```markdown
# Build / Bundle Size Report
> Date: YYYY-MM-DD

## Baseline & Measurement
| 항목 | 크기 | 비고 |

## Findings
| # | 영역 | 파일 | 이슈 | 크기 영향 | 권장사항 |

## Recommendations
### 크기 감소
### 빌드 속도 개선

## Codex Handoff — Task List
| # | 파일 | 변경 요지 | 벤치 커맨드 | 수용 기준 | 위험도 |
```

## Stop Conditions
| 조건 | 대응 |
|---|---|
| TrimMode=full이 COM Interop 파괴 | TrimMode=partial 유지 |
| R2R이 크기 오히려 증가 | R2R 미적용 |
