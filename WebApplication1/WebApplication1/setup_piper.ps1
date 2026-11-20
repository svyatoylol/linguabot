$ErrorActionPreference = "Stop"

$projectDir = $PSScriptRoot
$piperDir = Join-Path $projectDir "piper"
$modelsDir = Join-Path $projectDir "piper_models"

Write-Host "=== Настройка Piper TTS для LinguaBot ===" -ForegroundColor Cyan

# 1. Скачивание и распаковка бинарника Piper для Windows
if (-not (Test-Path (Join-Path $piperDir "piper.exe"))) {
    Write-Host "`n1. Скачивание бинарника Piper (Windows x64)..." -ForegroundColor Yellow
    $zipUrl = "https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip"
    $zipPath = Join-Path $projectDir "piper_windows.zip"
    
    Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath
    Write-Host "Распаковка в $piperDir..."
    Expand-Archive -Path $zipPath -DestinationPath $projectDir -Force
    Remove-Item $zipPath -Force
    Write-Host "Piper успешно распакован!" -ForegroundColor Green
} else {
    Write-Host "`n1. piper.exe уже установлен в $piperDir" -ForegroundColor Green
}

# 2. Скачивание модели для русского языка (ru_RU-irina-medium)
if (-not (Test-Path (Join-Path $modelsDir "ru_RU-irina-medium.onnx"))) {
    Write-Host "`n2. Скачивание русской модели (ru_RU-irina-medium)..." -ForegroundColor Yellow
    $ruModelUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/ru/ru_RU/irina/medium/ru_RU-irina-medium.onnx"
    $ruJsonUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/ru/ru_RU/irina/medium/ru_RU-irina-medium.onnx.json"
    
    Invoke-WebRequest -Uri $ruModelUrl -OutFile (Join-Path $modelsDir "ru_RU-irina-medium.onnx")
    Invoke-WebRequest -Uri $ruJsonUrl -OutFile (Join-Path $modelsDir "ru_RU-irina-medium.onnx.json")
    Write-Host "Русская модель успешно скачана!" -ForegroundColor Green
} else {
    Write-Host "`n2. Русская модель уже присутствует в $modelsDir" -ForegroundColor Green
}

# 3. Скачивание модели для английского языка (en_US-lessac-medium)
if (-not (Test-Path (Join-Path $modelsDir "en_US-lessac-medium.onnx"))) {
    Write-Host "`n3. Скачивание английской модели (en_US-lessac-medium)..." -ForegroundColor Yellow
    $enModelUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/lessac/medium/en_US-lessac-medium.onnx"
    $enJsonUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/lessac/medium/en_US-lessac-medium.onnx.json"
    
    Invoke-WebRequest -Uri $enModelUrl -OutFile (Join-Path $modelsDir "en_US-lessac-medium.onnx")
    Invoke-WebRequest -Uri $enJsonUrl -OutFile (Join-Path $modelsDir "en_US-lessac-medium.onnx.json")
    Write-Host "Английская модель успешно скачана!" -ForegroundColor Green
} else {
    Write-Host "`n3. Английская модель уже присутствует в $modelsDir" -ForegroundColor Green
}

Write-Host "`n[ГОТОВО] Piper TTS настроен и готов к работе!" -ForegroundColor Green
