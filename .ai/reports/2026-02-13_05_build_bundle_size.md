# Build / Bundle Size Report — MailTriageAssistant
> Date: 2026-02-13

## Baseline & Measurement

| 항목 | 크기 (추정) | 비고 |
|---|---|---|
| **Release 단일 파일 (SelfContained + Trimmed)** | 15-25 MB | `TrimMode=partial`, `SelfContained=true`, `PublishSingleFile=true` |
| .NET 런타임 (SelfContained 포함) | ~12-18 MB | Trimming으로 감소 |
| WPF 프레임워크 | ~5-8 MB | 부분 포함 |
| 앱 코드 + 리소스 | ~1-2 MB | Services, Models, XAML |
| NuGet: Serilog (3 패키지) | ~0.5-1 MB | Serilog + Sinks.File + Extensions.Logging |
| NuGet: Microsoft.Extensions.* | ~0.5-1 MB | DI, Logging, Options, Configuration |
| NuGet: COM PIA | ~0.2 MB | Microsoft.Office.Interop.Outlook |
| NuGet: Moq, FluentAssertions, xUnit | 0 (테스트 전용) | 퍼블리시에 미포함 ✅ |

### 측정 커맨드
```powershell
dotnet publish MailTriageAssistant/MailTriageAssistant.csproj -c Release -r win-x64 --self-contained -o ./publish_measure
(Get-ChildItem ./publish_measure -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
```

---

## Findings

| # | 영역 | 파일 | 이슈 | 크기 영향 | 권장사항 |
|---|---|---|---|---|---|
| BS-01 | TrimMode | `.csproj` | `TrimMode=partial` 사용 중. `full`이면 추가 ~2-5MB 절감 가능하지만 **COM Interop + Reflection 파괴 위험** | ~2-5 MB | COM Interop 호환성 문제로 `partial` 유지 권장. 전환 시 반드시 전체 기능 테스트 |
| BS-02 | ReadyToRun | `.csproj` | `PublishReadyToRun` 미적용. R2R 활성화 시 **시작 속도 개선**하지만 **바이너리 크기 증가**(~20-40%) | +3-8 MB | 시작 속도 vs 크기 트레이드오프. 시작 속도가 더 중요하면 적용 |
| BS-03 | Serilog | `.csproj` | Serilog 3패키지 합산 ~0.5-1MB. 사용 규모 대비 적절. | ~0.5-1 MB | 현 상태 유지 |
| BS-04 | 미사용 코드 | `Helpers/TaskExtensions.cs` | `SafeFireAndForget` 외 추가 메서드 없음. 26줄. Trimmer가 미사용 코드 제거 | 미미 | 현 상태 유지 |
| BS-05 | Debug 심볼 | `.csproj` | Release 빌드에 PDB 포함 여부 확인 필요 | ~1-3 MB | `<DebugType>none</DebugType>` 또는 `<DebugSymbols>false</DebugSymbols>` (Release 전용) |

---

## Recommendations

### 크기 감소
1. **BS-05**: Release 빌드에서 PDB 제거 → ~1-3 MB 절감
2. **BS-01**: `TrimMode=full` 테스트 (COM 호환성 확인 후 결정)

### 시작 속도 개선 (크기 증가 트레이드오프)
3. **BS-02**: `PublishReadyToRun=true` → JIT 컴파일 시간 절감 → 시작 ~200-500ms 단축

---

## Codex Handoff — Task List

| # | 파일 | 변경 요지 | 벤치 커맨드 | 수용 기준 | 위험도 |
|---|---|---|---|---|---|
| T-01 | `.csproj` | Release 빌드에 `<DebugType>none</DebugType>` 추가 | `dotnet publish -c Release -r win-x64 --self-contained -o ./publish_measure` → 크기 확인 | 크기 감소 확인 | Low |
| T-02 | `.csproj` | `<PublishReadyToRun>true</PublishReadyToRun>` 추가 (Release 전용) | `dotnet publish` → 앱 시작 시간 비교 | 시작 시간 단축 확인 | Medium |
| T-03 | `.csproj` | (선택) `TrimMode=full` 테스트 → COM 동작 확인 | `dotnet publish` → 전체 기능 수동 테스트 | 모든 기능 정상 | High |
