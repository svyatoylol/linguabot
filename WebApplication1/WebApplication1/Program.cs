using LinguaBot.Data;
using LinguaBot.Endpoints;
using LinguaBot.Services;
using Microsoft.EntityFrameworkCore;
using VoiceService;
using WebApplication1.Services.Chat;

string contentRoot = Directory.GetCurrentDirectory();
if (!Directory.Exists(Path.Combine(contentRoot, "wwwroot")))
{
    var subDir = Path.Combine(contentRoot, "WebApplication1", "WebApplication1");
    if (Directory.Exists(Path.Combine(subDir, "wwwroot")))
    {
        contentRoot = subDir;
    }
    else if (Directory.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot")))
    {
        contentRoot = AppContext.BaseDirectory;
    }
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot
});

// 1. База данных PostgreSQL (ApplicationDbContext)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = "Host=localhost;Port=5432;Database=linguabot_db;Username=postgres;Password=postgre";
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString)
);

// 2. Отказоустойчивое ядро данных и сервисы обучения
builder.Services.AddSingleton<LinguaDataStore>();
builder.Services.AddSingleton<ScenarioCatalog>();
builder.Services.AddSingleton<IntelligentLanguageEngine>();
builder.Services.AddScoped<PlacementTestGeneratorService>();
builder.Services.AddScoped<ExerciseGeneratorService>();
builder.Services.AddScoped<DialogueAnalysisService>();
builder.Services.AddHttpClient<GeminiService>();

// 3. Сервисы голоса (Piper TTS / Whisper STT) и чата
builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddVoiceServices();
builder.Services.AddChatServices(builder.Configuration);

// 4. Настройка CORS для поддержки PWA и fetch credentials
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Безопасное автоприменение миграций БД (с грациозным фоллбэком при оффлайн-сервере PostgreSQL)
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    try
    {
        db.Database.ExecuteSqlRaw(@"
            ALTER TABLE ""DialogueSessions"" ADD COLUMN IF NOT EXISTS ""UsedHintsCount"" integer NOT NULL DEFAULT 0;
            ALTER TABLE ""DialogueSessions"" ADD COLUMN IF NOT EXISTS ""IndependentTurnsCount"" integer NOT NULL DEFAULT 0;
            ALTER TABLE ""DialogueSessions"" ADD COLUMN IF NOT EXISTS ""Mode"" text NOT NULL DEFAULT 'Standard';
            ALTER TABLE ""DialogueSessions"" ADD COLUMN IF NOT EXISTS ""ScenarioTitle"" text NOT NULL DEFAULT 'Диалог';
            ALTER TABLE ""DialogueSessions"" ADD COLUMN IF NOT EXISTS ""LanguageCode"" text NOT NULL DEFAULT 'en';
            ALTER TABLE ""DialogueSessions"" ADD COLUMN IF NOT EXISTS ""Level"" text NOT NULL DEFAULT 'A1-A2';
        ");
    }
    catch {}
    Console.WriteLine("[Database] PostgreSQL migrations applied successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"[Database Warning] PostgreSQL is not reachable: {ex.Message}. Using resilient LinguaDataStore (App_Data/linguabot_store.json) for offline/demo mode.");
}

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

// Поддержка WebSocket для режима живого звонка (Voice Call)
app.UseWebSockets();

// 5. Подключение модульных эндпоинтов 15 функций MVP
app.MapAuthEndpoints();
app.MapProfileEndpoints();
app.MapOnboardingEndpoints();
app.MapDialogueEndpoints();
app.MapExerciseEndpoints();
app.MapDictionaryEndpoints();
app.MapDailyPlanAndStatsEndpoints();
app.MapPlacementTestEndpoints();
app.MapVoiceEndpoints();
app.MapChatEndpoints();
app.MapControllers();

// Health check
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    product = "LinguaBot",
    version = "2.0-MVP",
    time = DateTime.UtcNow
}));

app.Run();
