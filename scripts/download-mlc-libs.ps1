# download-mlc-libs.ps1
# Skrypt do pobrania gotowych bibliotek MLC LLM z oficjalnych releases
# Użycie: .\download-mlc-libs.ps1

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$libsDir = Join-Path $projectRoot "LLMClient\Platforms\Android\libs\arm64-v8a"
$tempDir = Join-Path $env:TEMP "mlc-download"

# MLC LLM biblioteki z oficjalnego repozytorium binary-mlc-llm-libs
# Najnowsza wersja: Android-09262024
# https://github.com/mlc-ai/binary-mlc-llm-libs/releases
$binaryLibsTag = "Android-09262024"
$binaryLibsRepo = "https://github.com/mlc-ai/binary-mlc-llm-libs"

# Alternatywnie - APK z głównego repo
$mlcApkUrl = "https://github.com/niconi21/niconi21.github.io/releases/download/v0.1.0/app-arm64-v8a-release.apk"
$apkFile = Join-Path $tempDir "MLCChat.apk"

Write-Host "=== MLC LLM Library Downloader ===" -ForegroundColor Cyan
Write-Host ""

# Utwórz katalogi
if (-not (Test-Path $tempDir)) {
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
}
if (-not (Test-Path $libsDir)) {
    New-Item -ItemType Directory -Path $libsDir -Force | Out-Null
}

# Sprawdź czy biblioteka już istnieje
$existingLib = Join-Path $libsDir "libtvm4j_runtime_packed.so"
if (Test-Path $existingLib) {
    $fileInfo = Get-Item $existingLib
    Write-Host "Znaleziono istniejącą bibliotekę:" -ForegroundColor Yellow
    Write-Host "  Rozmiar: $([math]::Round($fileInfo.Length / 1MB, 2)) MB"
    Write-Host "  Data: $($fileInfo.LastWriteTime)"
    Write-Host ""
    $response = Read-Host "Czy chcesz pobrać nową? (t/n)"
    if ($response -ne "t") {
        Write-Host "Anulowano." -ForegroundColor Gray
        exit 0
    }
}

Write-Host "Pobieranie APK z MLC LLM releases..." -ForegroundColor Green
Write-Host "URL: $mlcApkUrl"
Write-Host ""

try {
    # Pobierz APK
    Invoke-WebRequest -Uri $mlcApkUrl -OutFile $apkFile -UseBasicParsing
    Write-Host "Pobrano APK: $([math]::Round((Get-Item $apkFile).Length / 1MB, 2)) MB" -ForegroundColor Green
}
catch {
    Write-Host "Błąd pobierania APK: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Alternatywne źródła:" -ForegroundColor Yellow
    Write-Host "1. https://github.com/mlc-ai/mlc-llm/releases"
    Write-Host "2. https://llm.mlc.ai/"
    Write-Host ""
    Write-Host "Pobierz ręcznie APK i rozpakuj lib/arm64-v8a/libtvm4j_runtime_packed.so"
    exit 1
}

# Rozpakuj APK (to jest ZIP)
Write-Host ""
Write-Host "Rozpakowywanie APK..." -ForegroundColor Green
$extractDir = Join-Path $tempDir "extracted"
if (Test-Path $extractDir) {
    Remove-Item -Recurse -Force $extractDir
}

Expand-Archive -Path $apkFile -DestinationPath $extractDir -Force

# Znajdź bibliotekę
$sourceLib = Join-Path $extractDir "lib\arm64-v8a\libtvm4j_runtime_packed.so"
if (-not (Test-Path $sourceLib)) {
    Write-Host "Nie znaleziono biblioteki w APK!" -ForegroundColor Red
    Write-Host "Szukam w: $sourceLib"
    
    # Pokaż strukturę
    Write-Host ""
    Write-Host "Zawartość rozpakowanego APK:" -ForegroundColor Yellow
    Get-ChildItem -Path $extractDir -Recurse -Name "*.so" | ForEach-Object { Write-Host "  $_" }
    exit 1
}

# Skopiuj bibliotekę
Write-Host ""
Write-Host "Kopiowanie biblioteki do projektu..." -ForegroundColor Green
Copy-Item -Path $sourceLib -Destination $existingLib -Force

$finalLib = Get-Item $existingLib
Write-Host ""
Write-Host "=== Sukces! ===" -ForegroundColor Green
Write-Host "Biblioteka: $existingLib"
Write-Host "Rozmiar: $([math]::Round($finalLib.Length / 1MB, 2)) MB"
Write-Host ""

# Sprawdź mlc-app-config.json jeśli istnieje
$configFile = Join-Path $extractDir "assets\mlc-app-config.json"
if (Test-Path $configFile) {
    Write-Host "Znaleziono konfigurację modeli:" -ForegroundColor Cyan
    $config = Get-Content $configFile -Raw | ConvertFrom-Json
    
    if ($config.model_list) {
        Write-Host ""
        Write-Host "Wspierane modele (model_lib):" -ForegroundColor Yellow
        foreach ($model in $config.model_list) {
            Write-Host "  - $($model.model_id): $($model.model_lib)" -ForegroundColor White
        }
    }
}

# Cleanup
Write-Host ""
Write-Host "Czyszczenie plików tymczasowych..." -ForegroundColor Gray
Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Gotowe! Teraz:" -ForegroundColor Cyan
Write-Host "1. Zaktualizuj ModelLibMappings w MlcLlmBridge.cs"
Write-Host "2. Przebuduj projekt: dotnet build -t:Run -f net10.0-android"
Write-Host ""
