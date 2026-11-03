using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// Добавляем HTTP-клиент для отправки запросов к нейросети
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/chat", async (ChatRequest request, HttpClient http) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { text = "Сообщение не может быть пустым." });

    try
    {
        // Настраиваем запрос к API искусственного интеллекта
        // В данном примере используется OpenRouter (он дает легкий доступ к DeepSeek/Llama)
        var apiKey = "YOUR_OPENROUTER_OR_OPENAI_API_KEY"; // Сюда вставляется ваш API-ключ

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai");
        requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        // Формируем тело запроса для нейросети
        var jsonBody = new
        {
            model = "google/gemma-2-9b-it:free", // Бесплатная быстрая нейросеть (можно заменить на deepseek, gpt-4o и т.д.)
            messages = new[] {
                new { role = "system", content = "Ты дружелюбный ассистент на сайте swinki.ru. Отвечай кратко и емко." },
                new { role = "user", content = request.Message }
            }
        };

        requestMessage.Content = JsonContent.Create(jsonBody);

        // Отправляем запрос в ИИ
        var response = await http.SendAsync(requestMessage);

        if (!response.IsSuccessStatusCode)
        {
            return Results.Ok(new { text = "ИИ временно перегружен, но бэкенд .NET работает стабильно!" });
        }

        // Разбираем JSON-ответ от нейросети
        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
        var aiText = jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

        return Results.Ok(new { text = aiText });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { text = $"Ошибка при обращении к ИИ: {ex.Message}" });
    }
});

app.Run();

public record ChatRequest(string Message);
