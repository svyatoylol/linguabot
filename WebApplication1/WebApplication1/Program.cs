using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}
app.MapGet("/api/db-check", async (AppDbContext db) =>
{
    try
    {
        bool canConnect = await db.Database.CanConnectAsync();
        int usersCount = await db.Users.CountAsync();
        return Results.Ok(new
        {
            status = "Подключение к PostgreSQL работает!",
            connected = canConnect,
            usersInDb = usersCount
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Ошибка подключения к БД: {ex.Message}");
    }
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.Run();