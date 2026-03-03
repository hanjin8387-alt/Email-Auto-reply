# MailTriageAssistant — Outlook COM 연결 실패 분석 보고서

> 작성일: 2026-02-25  
> 증상: 회사 PC에서 Classic Outlook 실행 중임에도 앱 실행 시 "Outlook 연결 실패" 오류 발생  
> 참고: 터미널 명령어(`Get-Process outlook`)로는 프로세스 확인됨

---

## 1. 연결 메커니즘 요약

앱의 Outlook 연결 경로 (`OutlookService.cs`):

```
EnsureClassicOutlookOrThrow()
  ├─ RefreshOutlookProcessState()          → outlook.exe / olk.exe 프로세스 확인
  ├─ GetActiveOrCreateOutlookApplication()
  │    ├─ GetActiveOutlookApplication()    → oleaut32.dll GetActiveObject() (P/Invoke)
  │    └─ fallback: new Outlook.Application() → COM CoCreateInstance
  └─ _app.Session / _app.GetNamespace("MAPI")
```

**핵심**: `GetActiveObject("Outlook.Application")`은 Win32 COM ROT(Running Object Table)에서 실행 중인 Outlook 인스턴스를 검색합니다.

---

## 2. 실패 원인 후보 (유력도순)

### 🔴 원인 1: 권한 수준(Integrity Level) 불일치 — 가장 유력

| 항목 | 설명 |
|------|------|
| **원리** | Windows COM은 동일한 Integrity Level에서만 객체를 공유 |
| **증상** | `GetActiveObject()` 실패 → fallback `new Outlook.Application()`도 실패 |
| **조건** | 앱 = 일반 사용자, Outlook = 관리자 권한 (또는 반대) |
| **회사에서 발생 이유** | IT 정책으로 Outlook이 관리자 권한으로 자동 실행되거나, 앱을 제한된 권한으로 실행 |

**확인:**
```powershell
# 현재 PowerShell이 관리자인지 확인
whoami /groups | findstr "S-1-16-12288"
# 결과 있으면 = 관리자 권한

# Outlook 프로세스 권한 확인
Get-Process outlook | Select-Object ProcessName, Id, SessionId
```

**해결:**
- 앱을 **"관리자 권한으로 실행"** 해보기
- 또는 Outlook을 일반 권한으로 재시작

---

### 🟡 원인 2: Group Policy (GPO)로 COM 자동화 차단

| 항목 | 설명 |
|------|------|
| **원리** | 회사 IT가 레지스트리/GPO로 외부 프로그램의 Outlook COM 접근을 차단 |
| **레지스트리 키** | `HKCU\Software\Policies\Microsoft\Office\16.0\Outlook\Security` |
| **값** | `ObjectModelGuard = 2` → 모든 COM 자동화 거부 |

**확인:**
```powershell
reg query "HKCU\Software\Policies\Microsoft\Office\16.0\Outlook\Security" 2>$null
reg query "HKLM\Software\Policies\Microsoft\Office\16.0\Outlook\Security" 2>$null
```

**ObjectModelGuard 값:**
| 값 | 의미 |
|----|------|
| 0 또는 없음 | 기본값 (프롬프트 표시) |
| 1 | 항상 허용 |
| **2** | **항상 차단** ← 이것이면 IT에 요청 필요 |

**해결:** IT 부서에 COM Automation 허용 요청

---

### 🟡 원인 3: 세션 격리 (RDP/Citrix/VDI)

| 항목 | 설명 |
|------|------|
| **원리** | `GetActiveObject()`는 동일 터미널 세션의 COM 객체만 검색 |
| **증상** | Outlook이 세션 0, 앱이 세션 1 → 서로를 찾지 못함 |
| **환경** | 원격 데스크톱, Citrix, VMware Horizon 등 |

**확인:**
```powershell
# 현재 세션 ID
[System.Diagnostics.Process]::GetCurrentProcess().SessionId

# Outlook 세션 ID
Get-Process outlook | Select-Object ProcessName, SessionId
```

두 값이 다르면 세션 격리가 원인.

---

### 🟢 원인 4: 32/64비트 아키텍처 불일치

| 항목 | 설명 |
|------|------|
| **원리** | 앱(x64)과 Outlook(x86)의 비트가 다르면 COM 마샬링 실패 |
| **확인** | 앱의 `net8.0-windows`는 기본 x64, Office는 32비트일 수 있음 |

**확인:**
```powershell
# 앱의 비트
[Environment]::Is64BitProcess

# Outlook 실행 경로 확인
(Get-Process outlook).MainModule.FileName
# "Program Files\" → 64비트
# "Program Files (x86)\" → 32비트
```

**해결:** `.csproj`에 `<PlatformTarget>x86</PlatformTarget>` 추가하거나 AnyCPU + Prefer32Bit

---

### 🟢 원인 5: COM 타임아웃 (30초)

앱의 `OutlookService.cs`에 COM 작업 타임아웃이 30초로 설정되어 있습니다 (L19):
```csharp
private static readonly TimeSpan ComTimeout = TimeSpan.FromSeconds(30);
```

회사 네트워크 드라이브 연결이나 Exchange 동기화로 Outlook이 느리면 타임아웃 가능.

---

## 3. 진단 스크립트

동봉된 `scripts/diagnose-outlook-com.ps1`을 회사 PC에서 실행하면 위 5가지 원인을 **자동으로 진단**합니다.

```powershell
cd "MailTriageAssistant가 있는 폴더"
.\scripts\diagnose-outlook-com.ps1
```

출력 결과를 확인하여 어떤 원인에 해당하는지 특정합니다.

---

## 4. 빠른 대응표

| 진단 결과 | 해결 방법 |
|-----------|-----------|
| 권한 불일치 | 앱을 **우클릭 → 관리자 권한으로 실행** |
| ObjectModelGuard = 2 | IT에 COM 자동화 허용 요청 |
| 세션 ID 불일치 | 같은 세션에서 Outlook + 앱 모두 실행 |
| 32/64비트 불일치 | 앱을 x86으로 빌드하거나 64비트 Office 설치 |
| 타임아웃 | Outlook 완전 로딩 후 앱 시작, 또는 타임아웃 값 증가 |
