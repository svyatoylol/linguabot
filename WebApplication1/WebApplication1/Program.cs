using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// 1. Включаем поддержку статических файлов (чтобы сервер видел наш index.html)
app.UseDefaultFiles();
app.UseStaticFiles();

// 2. Создаем API-эндпоинт, который будет принимать сообщения и отвечать как ИИ
app.MapPost("/api/chat", async (ChatRequest request) =>
{
    // Имитируем задержку ответа нейросети (1 секунда)
    await System.Threading.Tasks.Task.Delay(1000);

    // Базовый тестовый ответ от искусственного интеллекта
    var aiResponse = new
    {
        text = $"Привет! Я нейросеть сайта swinki.ru. Получил твой запрос: \"{request.Message}\". Связка фронтенда и бэкенда на .NET Core работает идеально!"
    };

    return Results.Ok(aiResponse);
});

app.Run();

// ОБЪЯВЛЕНИЕ МОДЕЛЕЙ ДАННЫХ (Строго в самом низу файла!)
public record ChatRequest(string Message);
