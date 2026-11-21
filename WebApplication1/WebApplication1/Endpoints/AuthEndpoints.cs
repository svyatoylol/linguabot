using LinguaBot.Models;
using LinguaBot.Services;

namespace LinguaBot.Endpoints;

public static class AuthEndpoints
{
    public const string CookieName = "linguabot_guest";

    public static void MapAuthEndpoints(this WebApplication app)
    {
        // Регистрация нового пользователя
        app.MapPost("/api/auth/register", (RegisterRequest req, HttpContext ctx, LinguaDataStore store) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            {
                return Results.BadRequest(new { error = "Укажите Email и пароль." });
            }

            if (req.Password.Trim().Length < 4)
            {
                return Results.BadRequest(new { error = "Пароль должен содержать не менее 4 символов." });
            }

            var email = req.Email.Trim().ToLowerInvariant();
            var currentUser = GetOrCreateCurrentUser(ctx, store);
            User targetUser = string.IsNullOrEmpty(currentUser.PasswordHash) 
                ? currentUser 
                : store.GetOrCreateUser(null);

            // Проверяем, существует ли ДРУГОЙ зарегистрированный аккаунт с таким Email
            var existingOther = store.Users.Values.FirstOrDefault(u => 
                u.Id != targetUser.Id && 
                (u.Email ?? "").Equals(email, StringComparison.OrdinalIgnoreCase) && 
                !string.IsNullOrEmpty(u.PasswordHash));

            if (existingOther != null)
            {
                return Results.Conflict(new { error = "Пользователь с таким Email уже зарегистрирован. Пожалуйста, перейдите на вкладку «Вход»." });
            }

            targetUser.Email = email;
            targetUser.Name = string.IsNullOrWhiteSpace(req.Name) ? email.Split('@')[0] : req.Name.Trim();
            targetUser.PasswordHash = LinguaDataStore.HashPassword(req.Password.Trim());

            store.Users[targetUser.Id] = targetUser;
            store.SaveToDisk();
            SetUserCookie(ctx, targetUser.Id);

            return Results.Ok(new
            {
                message = "Аккаунт успешно создан!",
                user = BuildUserProfile(targetUser)
            });
        });

        // Вход по Email и паролю
        app.MapPost("/api/auth/login", (LoginRequest req, HttpContext ctx, LinguaDataStore store) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            {
                return Results.BadRequest(new { error = "Укажите Email и пароль." });
            }

            var email = req.Email.Trim().ToLowerInvariant();
            var user = store.Users.Values.FirstOrDefault(u => (u.Email ?? "").Equals(email, StringComparison.OrdinalIgnoreCase));

            if (user == null)
            {
                return Results.BadRequest(new { error = "Пользователь с таким Email не найден. Зарегистрируйтесь на вкладке «Регистрация»." });
            }

            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                return Results.BadRequest(new { error = "Для этого аккаунта ещё не установлен пароль. Пожалуйста, перейдите на вкладку «Регистрация»." });
            }

            if (!LinguaDataStore.VerifyPassword(req.Password.Trim(), user.PasswordHash))
            {
                return Results.BadRequest(new { error = "Неверный пароль. Попробуйте ещё раз." });
            }

            SetUserCookie(ctx, user.Id);

            return Results.Ok(new
            {
                message = "Вход выполнен успешно!",
                user = BuildUserProfile(user)
            });
        });

        // Запрос на сброс пароля
        app.MapPost("/api/auth/password/reset", (PasswordResetRequest req, LinguaDataStore store) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email))
            {
                return Results.BadRequest(new { error = "Укажите Email." });
            }

            return Results.Ok(new
            {
                message = $"Инструкции по восстановлению пароля отправлены на {req.Email.Trim()}."
            });
        });

        // OAuth провайдеры (Yandex, VK и др.)
        app.MapPost("/api/auth/oauth/{provider}", (string provider, HttpContext ctx, LinguaDataStore store) =>
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return Results.BadRequest(new { error = "Провайдер авторизации не указан." });
            }

            var providerCode = provider.Trim().ToLowerInvariant();
            var providerTitle = providerCode switch
            {
                "yandex" => "Яндекс",
                "vk" => "VK",
                _ => char.ToUpper(providerCode[0]) + providerCode[1..]
            };

            var user = GetOrCreateCurrentUser(ctx, store);
            user.Email ??= $"{providerCode}_user@linguabot.app";

            var defaultName = providerCode switch
            {
                "yandex" => "Yandex User",
                "vk" => "VK User",
                _ => $"{char.ToUpper(providerCode[0])}{providerCode[1..]} User"
            };

            if (string.IsNullOrEmpty(user.Name) || user.Name == "Гость" || user.Name.EndsWith("User"))
            {
                user.Name = defaultName;
            }

            store.Users[user.Id] = user;
            store.SaveToDisk();
            SetUserCookie(ctx, user.Id);

            return Results.Ok(new
            {
                message = $"Авторизация через {providerTitle} успешна.",
                user = BuildUserProfile(user)
            });
        });

        // Вход по номеру телефона: отправка кода
        app.MapPost("/api/auth/phone/send-code", (PhoneCodeRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.Phone))
            {
                return Results.BadRequest(new { error = "Укажите номер телефона." });
            }

            return Results.Ok(new
            {
                message = "Код подтверждения отправлен (демо-код: 1234)."
            });
        });

        // Вход по номеру телефона: проверка кода
        app.MapPost("/api/auth/phone/verify", (PhoneVerifyRequest req, HttpContext ctx, LinguaDataStore store) =>
        {
            if (string.IsNullOrWhiteSpace(req.Phone) || string.IsNullOrWhiteSpace(req.Code))
            {
                return Results.BadRequest(new { error = "Укажите телефон и код." });
            }

            var user = GetOrCreateCurrentUser(ctx, store);
            user.Phone = req.Phone.Trim();
            if (string.IsNullOrEmpty(user.Name) || user.Name == "Гость")
            {
                user.Name = $"Пользователь {req.Phone[^4..]}";
            }

            store.Users[user.Id] = user;
            store.SaveToDisk();
            SetUserCookie(ctx, user.Id);

            return Results.Ok(new
            {
                message = "Номер телефона подтверждён.",
                user = BuildUserProfile(user)
            });
        });

        // Выход
        app.MapPost("/api/auth/logout", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
            return Results.Ok(new { message = "Вы вышли из аккаунта." });
        });
    }

    public static User GetOrCreateCurrentUser(HttpContext ctx, LinguaDataStore store)
    {
        // 1. Проверяем явный заголовок X-User-Id
        if (ctx.Request.Headers.TryGetValue("X-User-Id", out var headerVal) && Guid.TryParse(headerVal, out var headerId))
        {
            if (store.Users.TryGetValue(headerId, out var headerUser))
            {
                SetUserCookie(ctx, headerUser.Id);
                return headerUser;
            }
        }

        // 2. Проверяем Http-Cookie
        if (ctx.Request.Cookies.TryGetValue(CookieName, out var val) && Guid.TryParse(val, out var id))
        {
            return store.GetOrCreateUser(id);
        }

        var newUser = store.GetOrCreateUser(null);
        SetUserCookie(ctx, newUser.Id);
        return newUser;
    }

    public static void SetUserCookie(HttpContext ctx, Guid userId)
    {
        ctx.Response.Cookies.Append(CookieName, userId.ToString(), new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(365),
            IsEssential = true
        });
    }

    public static object BuildUserProfile(User user)
    {
        return new
        {
            user.Id,
            name = user.Name,
            email = user.Email ?? "email не указан",
            phone = user.Phone,
            nativeLanguage = user.Settings.NativeLanguage,
            targetLanguage = user.Settings.LearningLanguage,
            level = user.Settings.Level,
            role = user.Role,
            remindersEnabled = user.RemindersEnabled,
            remindersTime = user.RemindersTime,
            streakDays = user.StreakDays,
            daysLearning = user.DaysLearning
        };
    }
}

public record RegisterRequest(string? Name, string? Email, string? Password);
public record LoginRequest(string? Email, string? Password);
public record PasswordResetRequest(string Email);
public record PhoneCodeRequest(string Phone);
public record PhoneVerifyRequest(string Phone, string Code);
