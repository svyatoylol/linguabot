using Microsoft.EntityFrameworkCore;
using VoiceService;
using WebApplication1.Data;
using WebApplication1.Services.Chat;

var builder = WebApplication.CreateBuilder(args);

// 1. Подключаем базовые сервисы
builder.Services.AddHttpClient();
builder.Services.AddControllers();

// 2. Сервисы распознавания речи (Groq Whisper STT)
builder.Services.AddVoiceServices();

// 3. Сервисы диалога и контекста ИИ (Gemini)
builder.Services.AddChatServices(builder.Configuration);

// 4. Подключение к БД
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

// Автопроверка таблиц при старте
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseDefaultFiles();
app.UseStaticFiles();

// Эндпоинты
app.MapChatEndpoints();
app.MapVoiceEndpoints();

app.MapControllers();
app.Run();
