param(
    [ValidateSet("win-x64", "win-x86", "both")]
    [string]$Rid = "both"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "MailTriageAssistant\\MailTriageAssistant.csproj"
$dist = Join-Path $root "dist"

if (!(Test-Path $project)) {
    throw "Project not found: $project"
}

New-Item -ItemType Directory -Path $dist -Force | Out-Null

$tag = Get-Date -Format "yyyyMMdd_HHmmss"

function Publish-And-Zip([string]$rid) {
    $outDir = Join-Path $dist "MailTriageAssistant-$rid-$tag"
    $zipPath = Join-Path $dist "MailTriageAssistant-$rid-$tag.zip"
    $guidePath = Join-Path $root "docs\\MailTriageAssistant_사용설명서.md"

    Write-Host "Publishing $rid -> $outDir"
    dotnet publish $project -c Release -r $rid --self-contained true -o $outDir | Write-Host

    if (Test-Path $guidePath) {
        Copy-Item $guidePath (Join-Path $outDir "UserGuide_ko.md") -Force
    }

    Write-Host "Zipping -> $zipPath"
    Compress-Archive -Path $outDir -DestinationPath $zipPath -Force

    return $zipPath
}

$zips = @()
if ($Rid -eq "both") {
    $zips += Publish-And-Zip "win-x64"
    $zips += Publish-And-Zip "win-x86"
} else {
    $zips += Publish-And-Zip $Rid
}

Write-Host ""
Write-Host "Done. ZIP outputs:"
$zips | ForEach-Object { Write-Host " - $_" }
