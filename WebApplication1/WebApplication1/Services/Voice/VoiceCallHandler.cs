using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebApplication1.Models;
using WebApplication1.Services.Chat;

namespace VoiceService
{
    /// <summary>
    /// Обработчик сессий живого голосового звонка через WebSocket (Voice Call Mode).
    /// Обеспечивает непрерывное общение с выбором языка, детекцией речи, прерыванием (barge-in) и синтезом речи.
    /// </summary>
    public class VoiceCallHandler
    {
        private readonly IVoiceOrchestrator _orchestrator;
        private readonly ILogger<VoiceCallHandler> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public VoiceCallHandler(
            IVoiceOrchestrator orchestrator,
            ILogger<VoiceCallHandler> logger)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Обработка входящего WebSocket-соединения для голосового звонка.
        /// </summary>
        public async Task HandleCallAsync(HttpContext context, WebSocket webSocket)
        {
            // 1. Инициализация параметров звонка из query-параметров
            var query = context.Request.Query;
            var callState = new CallState
            {
                Language = NormalizeLanguage(query["lang"].FirstOrDefault()),
                Scenario = query["scenario"].FirstOrDefault() ?? "free_talk",
                Level = query["level"].FirstOrDefault() ?? "A1-A2",
                SessionId = Guid.TryParse(query["sessionId"].FirstOrDefault(), out var sId) ? sId : null
            };

            _logger.LogInformation("VoiceCall: Новое соединение звонка. Язык: {Lang}, Сценарий: {Scenario}, Уровень: {Level}",
                callState.Language, callState.Scenario, callState.Level);

            // Отправляем клиенту подтверждение подключения к звонку
            await SendJsonAsync(webSocket, new
            {
                type = "call_connected",
                language = callState.Language,
                scenario = callState.Scenario,
                level = callState.Level,
                sessionId = callState.SessionId,
                message = $"Звонок подключен. Язык общения: {callState.Language}."
            }, CancellationToken.None);

            var buffer = new byte[1024 * 64]; // буфер 64 КБ

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Call ended by client", CancellationToken.None);
                        break;
                    }

                    // 1. Клиент отправил бинарное аудио (чанк речи пользователя после VAD)
                    if (receiveResult.MessageType == WebSocketMessageType.Binary)
                    {
                        var audioStream = new MemoryStream();
                        audioStream.Write(buffer, 0, receiveResult.Count);

                        while (!receiveResult.EndOfMessage)
                        {
                            receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                            audioStream.Write(buffer, 0, receiveResult.Count);
                        }

                        audioStream.Position = 0;
                        var turnCts = callState.NewTurn();

                        // Асинхронно обрабатываем голосовую реплику
                        _ = ProcessTurnAsync(webSocket, audioStream, "speech.webm", callState, turnCts.Token);
                    }
                    // 2. Клиент отправил текстовую команду (JSON)
                    else if (receiveResult.MessageType == WebSocketMessageType.Text)
                    {
                        var jsonStream = new MemoryStream();
                        jsonStream.Write(buffer, 0, receiveResult.Count);

                        while (!receiveResult.EndOfMessage)
                        {
                            receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                            jsonStream.Write(buffer, 0, receiveResult.Count);
                        }

                        var jsonString = Encoding.UTF8.GetString(jsonStream.ToArray());
                        await HandleJsonCommandAsync(webSocket, jsonString, callState);
                    }
                }
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                _logger.LogInformation("VoiceCall: Клиент отключился.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VoiceCall: Ошибка в процессе звонка.");
            }
            finally
            {
                callState.CancelTurn();
                _logger.LogInformation("VoiceCall: Звонок завершен.");
            }
        }

        private async Task HandleJsonCommandAsync(
            WebSocket webSocket,
            string jsonString,
            CallState state)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var typeProp)) return;

                var type = typeProp.GetString()?.ToLowerInvariant();

                switch (type)
                {
                    // Смена языка или параметров звонка на лету
                    case "init":
                    case "config":
                        if (root.TryGetProperty("language", out var langProp) || root.TryGetProperty("lang", out langProp))
                        {
                            state.Language = NormalizeLanguage(langProp.GetString());
                        }
                        if (root.TryGetProperty("scenario", out var scProp)) state.Scenario = scProp.GetString() ?? state.Scenario;
                        if (root.TryGetProperty("level", out var lvlProp)) state.Level = lvlProp.GetString() ?? state.Level;
                        if (root.TryGetProperty("sessionId", out var sProp) && Guid.TryParse(sProp.GetString(), out var sId)) state.SessionId = sId;

                        await SendJsonAsync(webSocket, new
                        {
                            type = "config_updated",
                            language = state.Language,
                            scenario = state.Scenario,
                            level = state.Level,
                            sessionId = state.SessionId
                        }, CancellationToken.None);
                        break;

                    // Прерывание речи бота (пользователь перебил бота)
                    case "barge_in":
                    case "interrupt":
                        _logger.LogInformation("VoiceCall: Пользователь перебил бота (barge-in). Прерываем генерацию.");
                        state.CancelTurn();
                        await SendJsonAsync(webSocket, new { type = "interrupted" }, CancellationToken.None);
                        break;

                    // Аудио, закодированное в Base64
                    case "audio":
                        if (root.TryGetProperty("data", out var dataProp))
                        {
                            var base64 = dataProp.GetString();
                            if (!string.IsNullOrWhiteSpace(base64))
                            {
                                var bytes = Convert.FromBase64String(base64);
                                var stream = new MemoryStream(bytes);
                                var format = root.TryGetProperty("format", out var fProp) ? fProp.GetString() ?? "webm" : "webm";
                                
                                var turnCts = state.NewTurn();
                                _ = ProcessTurnAsync(webSocket, stream, $"speech.{format}", state, turnCts.Token);
                            }
                        }
                        break;

                    // Завершение звонка
                    case "hang_up":
                    case "end_call":
                        state.CancelTurn();
                        await SendJsonAsync(webSocket, new { type = "call_ended" }, CancellationToken.None);
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Hang up by client", CancellationToken.None);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VoiceCall: Ошибка парсинга команды JSON: {Json}", jsonString);
            }
        }

        private async Task ProcessTurnAsync(
            WebSocket webSocket,
            Stream audioStream,
            string fileName,
            CallState state,
            CancellationToken ct)
        {
            try
            {
                if (ct.IsCancellationRequested || webSocket.State != WebSocketState.Open) return;

                // Уведомляем клиента, что речь распознается
                await SendJsonAsync(webSocket, new { type = "processing_speech" }, ct);

                var result = await _orchestrator.ProcessVoiceAsync(
                    audioStream,
                    fileName,
                    state.SessionId,
                    state.Scenario,
                    state.Language,
                    state.Level,
                    ct);

                if (ct.IsCancellationRequested || webSocket.State != WebSocketState.Open) return;

                if (!result.IsSuccess)
                {
                    await SendJsonAsync(webSocket, new
                    {
                        type = "error",
                        error = result.Error,
                        userText = result.UserText
                    }, ct);
                    return;
                }

                // 1. Отправляем распознанный текст пользователя и оценку произношения
                await SendJsonAsync(webSocket, new
                {
                    type = "user_speech",
                    text = result.UserText,
                    language = result.DetectedLanguage ?? state.Language,
                    pronunciationScore = result.PronunciationScore
                }, ct);

                // 2. Отправляем ответ ИИ (текст + аудио для немедленного воспроизведения)
                await SendJsonAsync(webSocket, new
                {
                    type = "ai_speech",
                    text = result.AiText,
                    audioBase64 = result.AudioBase64,
                    audioMimeType = result.AudioMimeType ?? "audio/wav",
                    language = state.Language
                }, ct);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("VoiceCall: Обработка реплики прервана (barge-in или отмена).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VoiceCall: Ошибка обработки реплики звонка");
                if (webSocket.State == WebSocketState.Open)
                {
                    await SendJsonAsync(webSocket, new
                    {
                        type = "error",
                        error = "Ошибка обработки реплики в звонке."
                    }, CancellationToken.None);
                }
            }
            finally
            {
                await audioStream.DisposeAsync();
            }
        }

        private static string NormalizeLanguage(string? lang)
        {
            if (string.IsNullOrWhiteSpace(lang)) return "en";
            var clean = lang.Trim().ToLowerInvariant();
            return clean switch
            {
                "ru" or "russian" or "русский" => "ru",
                "pl" or "polish" or "polski" => "pl",
                _ => "en"
            };
        }

        private static async Task SendJsonAsync(WebSocket socket, object payload, CancellationToken ct)
        {
            if (socket.State != WebSocketState.Open) return;
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }

        private class CallState
        {
            public string Language { get; set; } = "en";
            public string Scenario { get; set; } = "free_talk";
            public string Level { get; set; } = "A1-A2";
            public Guid? SessionId { get; set; }
            public CancellationTokenSource? CurrentTurnCts { get; set; }

            public CancellationTokenSource NewTurn()
            {
                CurrentTurnCts?.Cancel();
                CurrentTurnCts = new CancellationTokenSource();
                return CurrentTurnCts;
            }

            public void CancelTurn()
            {
                CurrentTurnCts?.Cancel();
                CurrentTurnCts = null;
            }
        }
    }
}
