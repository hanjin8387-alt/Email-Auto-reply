$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$budgetPath = Join-Path $root ".ai\\perf_budget.json"
$metricsPath = Join-Path $env:LOCALAPPDATA "MailTriageAssistant\\perf_metrics.json"

if (!(Test-Path $budgetPath)) {
    throw "Perf budget not found: $budgetPath"
}
if (!(Test-Path $metricsPath)) {
    throw "perf_metrics.json not found: $metricsPath`nRun the app in Debug and exit via tray -> Exit to generate it."
}

$budget = Get-Content $budgetPath -Raw | ConvertFrom-Json
$json = Get-Content $metricsPath -Raw | ConvertFrom-Json

function Get-TimingLastMs([string]$name) {
    if ($null -eq $json.timings) { return $null }

    $prop = $json.timings.PSObject.Properties | Where-Object { $_.Name -eq $name } | Select-Object -First 1
    if ($null -eq $prop) { return $null }

    # PerfMetrics snapshot uses { count, last_ms, min_ms, max_ms, avg_ms }
    return $prop.Value.last_ms
}

function Show-Result([string]$label, $value, $limit, [string]$unit) {
    if ($null -eq $value) {
        Write-Host ("{0,-18} : (missing)" -f $label)
        return
    }

    if ($null -eq $limit) {
        Write-Host ("{0,-18} : {1}{2}" -f $label, $value, $unit)
        return
    }

    $ok = $value -le $limit
    $status = if ($ok) { "OK" } else { "OVER" }
    Write-Host ("{0,-18} : {1}{2} (budget <= {3}{2}) [{4}]" -f $label, $value, $unit, $limit, $status)
}

Write-Host "perf_metrics.json:"
Write-Host (" - Path: {0}" -f $metricsPath)
Write-Host (" - Generated (UTC): {0}" -f $json.generated_utc)
Write-Host ""

Show-Result "startup_ms" $json.startup_ms $budget.startup_ms "ms"
Show-Result "header_load_ms" (Get-TimingLastMs "header_load_ms") $budget.header_load_ms "ms"
Show-Result "body_load_ms" (Get-TimingLastMs "body_load_ms") $budget.body_load_ms "ms"
Show-Result "prefetch_ms" (Get-TimingLastMs "prefetch_ms") $budget.prefetch_ms "ms"
Show-Result "digest_ms" (Get-TimingLastMs "digest_ms") $budget.digest_ms "ms"

$exitWs = $json.exit_working_set_mb
Show-Result "exit_memory_mb" $exitWs $budget.memory_mb "MB"

$snapCount = 0
if ($null -ne $json.memory_snapshots) {
    $snapCount = @($json.memory_snapshots).Count
}
Show-Result "mem_snapshots" $snapCount $null ""

Write-Host ""
Write-Host "Timing keys present:"
if ($null -eq $json.timings) {
    Write-Host " - (none)"
} else {
    $names = $json.timings.PSObject.Properties.Name | Sort-Object
    $names | ForEach-Object { Write-Host (" - {0}" -f $_) }
}

