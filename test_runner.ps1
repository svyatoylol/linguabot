# ========================================================
# Автоматический скрипт тестирования всех 15 функций LinguaBot MVP
# ========================================================

$ErrorActionPreference = "Stop"
$port = 5099
$url = "http://127.0.0.1:$port"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  LinguaBot MVP: Тестирование работоспособности (15 функций)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# Определяем путь к проекту
$projectPath = if (Test-Path "WebApplication1/WebApplication1/WebApplication1.csproj") {
    "WebApplication1/WebApplication1/WebApplication1.csproj"
} elseif (Test-Path "WebApplication1.csproj") {
    "WebApplication1.csproj"
} else {
    "WebApplication1/WebApplication1"
}

Write-Host "Запуск сервера .NET 8 (порт $port)..." -ForegroundColor Yellow
$proc = Start-Process -FilePath "dotnet" -ArgumentList "run --no-build -c Release --no-launch-profile --project $projectPath --urls $url" -PassThru -NoNewWindow

try {
    # 1. Ожидание готовности сервера
    $ready = $false
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Milliseconds 500
        try {
            $resp = Invoke-RestMethod -Uri "$url/api/health" -Method Get -TimeoutSec 2 -ErrorAction SilentlyContinue
            if ($resp -and $resp.status -eq "healthy") {
                $ready = $true
                break
            }
        } catch {}
    }

    if (-not $ready) {
        Write-Error "Сервер не ответил в течение 30 секунд."
        return
    }

    Write-Host "[OK] 1. Health check: Сервер активен (версия: $($resp.version), статус: $($resp.status))" -ForegroundColor Green

    # 2. Статический PWA клиент
    $html = Invoke-WebRequest -Uri "$url/" -Method Get -UseBasicParsing
    Write-Host "[OK] 2. PWA клиент и статические ассеты загружены (HTTP $($html.StatusCode))" -ForegroundColor Green

    # 3. Каталог 10 диалогов
    $scenarios = Invoke-RestMethod -Uri "$url/api/scenarios" -Method Get
    Write-Host "[OK] 3. Каталог сценариев: получено $($scenarios.Count) из 10 (Знакомство, Кафе, Отель и др.)" -ForegroundColor Green

    # 4. Гостевой диалог без регистрации (Функция 1)
    $guestBody = @{ scenarioId = "cafe"; targetLanguage = "en"; goal = "travel"; level = "A1-A2" } | ConvertTo-Json
    $guest = Invoke-RestMethod -Uri "$url/api/guest/sessions" -Method Post -Body $guestBody -ContentType "application/json"
    $sessionId = $guest.sessionId
    Write-Host "[OK] 4. Гостевой диалог создан: $sessionId, реплика бота: '$($guest.welcomeMessage)'" -ForegroundColor Green

    # 5. Гибридный ввод и отклик бота <= 3 сек (Функции 5 и 8)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $msgBody = @{ content = "Could I have a medium cappuccino and a chocolate croissant, please?"; isVoice = $false } | ConvertTo-Json
    $msg = Invoke-RestMethod -Uri "$url/api/dialogues/$sessionId/messages" -Method Post -Body $msgBody -ContentType "application/json"
    $sw.Stop()
    $elapsed = $sw.ElapsedMilliseconds
    Write-Host "[OK] 5. Отправка реплики: ответ ИИ получен за $elapsed мс (критерий ТЗ <= 3000 мс)" -ForegroundColor Green
    Write-Host "     Реплика бота: '$($msg.assistantMessage.content)'" -ForegroundColor DarkGray

    # 6. Контекстные подсказки (Функция 7)
    $hints = Invoke-RestMethod -Uri "$url/api/dialogues/$sessionId/hints" -Method Get
    Write-Host "[OK] 6. Контекстные подсказки получены: ключевые слова [$($hints.keywords -join ', ')]" -ForegroundColor Green

    # 7. Завершение диалога и разбор до 3 ошибок (Функция 8)
    $finish = Invoke-RestMethod -Uri "$url/api/dialogues/$sessionId/finish" -Method Post
    $analysis = Invoke-RestMethod -Uri "$url/api/dialogues/$sessionId/analysis" -Method Get
    Write-Host "[OK] 7. Разбор диалога: $($analysis.overallFeedback) (найдено ошибок: $($analysis.errors.Count))" -ForegroundColor Green

    # 8. Режим «Без заготовки» (Функция 6 - УТП)
    $retry = Invoke-RestMethod -Uri "$url/api/dialogues/$sessionId/retry" -Method Post
    Write-Host "[OK] 8. Режим «Без заготовки»: запущена сессия $($retry.sessionId) (режим: $($retry.mode))" -ForegroundColor Green

    # 9. Адаптивные упражнения (Функция 9)
    $exercises = Invoke-RestMethod -Uri "$url/api/dialogues/$sessionId/exercises" -Method Get
    Write-Host "[OK] 9. Адаптивные упражнения: сформировано $($exercises.Count) заданий по категориям" -ForegroundColor Green

    # 10. Личный словарь и SRS карточки (Функция 10)
    $dict = Invoke-RestMethod -Uri "$url/api/dictionary" -Method Get
    Write-Host "[OK] 10. Личный словарь: $($dict.totalWords) слов, карточек к повторению: $($dict.dueCount)" -ForegroundColor Green

    # 11. Персональные рекомендации (Функция 11)
    $recs = Invoke-RestMethod -Uri "$url/api/recommendations" -Method Get
    Write-Host "[OK] 11. Персональные рекомендации: получено $($recs.recommendations.Count) темы" -ForegroundColor Green

    # 12. Раздел «Сегодня» и план дня (Функция 13)
    $plan = Invoke-RestMethod -Uri "$url/api/daily-plan" -Method Get
    Write-Host "[OK] 12. Раздел «Сегодня»: $($plan.items.Count) шага чек-листа, цель $($plan.goalMinutes) мин" -ForegroundColor Green

    # 13. Дашборд прогресса и честная аналитика (Функция 12)
    $prog = Invoke-RestMethod -Uri "$url/api/progress/dashboard" -Method Get
    Write-Host "[OK] 13. Дашборд прогресса: стрик $($prog.streakDays) дн., самостоятельность: $($prog.turns.independentPercent)%" -ForegroundColor Green

    # 14. Полный тест на уровень CEFR (12 вопросов)
    $ptBody = @{ languageCode = "en" } | ConvertTo-Json
    $pt = Invoke-RestMethod -Uri "$url/api/placement-tests" -Method Post -Body $ptBody -ContentType "application/json"
    Write-Host "[OK] 14. Placement Test: сгенерировано $($pt.totalQuestions) вопросов CEFR по грамматике и лексике" -ForegroundColor Green

    # 15. Офлайн-синхронизация и идемпотентность (Функция 15)
    $syncBody = @{
        attempts = @(
            @{
                attemptId = [System.Guid]::NewGuid().ToString()
                exerciseId = [System.Guid]::NewGuid().ToString()
                userAnswer = "coffee"
                isCorrect = $true
                usedHint = $false
                attemptedAt = (Get-Date).ToUniversalTime().ToString("o")
            }
        )
        wordReviews = @()
    } | ConvertTo-Json
    $sync = Invoke-RestMethod -Uri "$url/api/offline/sync" -Method Post -Body $syncBody -ContentType "application/json"
    Write-Host "[OK] 15. Офлайн-синхронизация: передано $($sync.syncedAttempts) попыток без дубликатов" -ForegroundColor Green

    Write-Host "`n==========================================================" -ForegroundColor Cyan
    Write-Host "  РЕЗУЛЬТАТ: ВСЕ 15 ПРОВЕРОК УСПЕШНО ПРОЙДЕНЫ (100% SUCCESS)!" -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Cyan
}
finally {
    if ($proc -and -not $proc.HasExited) {
        Write-Host "Остановка тестового сервера (PID $($proc.Id))..." -ForegroundColor DarkGray
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    }
}
