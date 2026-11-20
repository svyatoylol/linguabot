# ========================================================
# Dockerfile для ИИ-тренера LinguaBot (.NET 8 ASP.NET Core)
# ========================================================

# Этап 1: Сборка и публикация
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копирование исходных файлов проекта
COPY WebApplication1/WebApplication1/WebApplication1.csproj WebApplication1/WebApplication1/
RUN dotnet restore WebApplication1/WebApplication1/WebApplication1.csproj

COPY WebApplication1/WebApplication1/ WebApplication1/WebApplication1/
WORKDIR /src/WebApplication1/WebApplication1
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Этап 2: Финальный образ среды выполнения (Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Установка системных утилит (curl, ca-certificates, alsa-utils для аудио)
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    ca-certificates \
    libasound2 \
    && rm -rf /var/lib/apt/lists/*

# Создание каталогов для постоянного хранения данных
RUN mkdir -p /app/App_Data /app/piper_models /app/wwwroot

COPY --from=build /app/publish .

# Настройка переменных окружения
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "WebApplication1.dll"]
