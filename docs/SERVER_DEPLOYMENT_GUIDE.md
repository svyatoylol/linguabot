# Инструкция по установке и развертыванию ИИ-тренера «LinguaBot» на сервере

Данное руководство содержит исчерпывающие инструкции по установке, конфигурированию и запуску серверной части веб-приложения **LinguaBot** в производственной среде (Production).

---

## 1. Системные требования

### 1.1. Минимальные требования (до 50 активных пользователей онлайн)
- **Процессор:** 2 ядра x86_64 (2.0 ГГц+)
- **Оперативная память (RAM):** 2 ГБ
- **Дисковое пространство:** 10 ГБ SSD / NVMe
- **Операционная система:** Linux (Ubuntu 22.04 / 24.04 LTS, Debian 12) или Windows Server 2019/2022
- **Сетевое подключение:** Белый IP-адрес с открытыми портами 80 (HTTP) и 443 (HTTPS)

### 1.2. Рекомендуемые требования (для стабильной работы со встроенным Piper TTS и PostgreSQL)
- **Процессор:** 4 ядра x86_64
- **Оперативная память (RAM):** 4–8 ГБ
- **Дисковое пространство:** 25 ГБ NVMe
- **Наличие доменного имени:** обязательно для генерации доверенного SSL-сертификата (требование браузеров для доступа к микрофону и PWA Service Worker).

---

## 2. Способ 1: Быстрое развертывание в Docker (Рекомендуемый)

Развертывание в Docker изолирует зависимости, поднимает контейнер PostgreSQL 16 и веб-сервис LinguaBot одной командой.

### Шаг 1. Установка Docker и Docker Compose на сервер (Ubuntu)
```bash
sudo apt-get update
sudo apt-get install -y ca-certificates curl gnupg lsb-release

# Добавление ключа Docker
sudo mkdir -p /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg

# Подключение репозитория
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
  $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
```

### Шаг 2. Клонирование репозитория и настройка переменных
```bash
cd /opt
git clone <URL_ВАШЕГО_РЕПОЗИТОРИЯ> linguabot
cd linguabot

# Создание конфигурационного файла окружения
cp .env.example .env
nano .env
```
Задайте в файле `.env`:
- `POSTGRES_PASSWORD`: надежный пароль к базе данных
- `GEMINI_API_KEY`: API-ключ Google Gemini (получить бесплатно на [Google AI Studio](https://aistudio.google.com/))
- `GROK_API_KEY`: API-ключ Groq для Whisper STT (получить бесплатно на [Groq Console](https://console.groq.com/))

### Шаг 3. Сборка и запуск контейнеров
```bash
# Сборка образов и запуск в фоновом режиме
docker compose up -d --build

# Проверка статуса контейнеров
docker compose ps

# Просмотр логов приложения в реальном времени
docker compose logs -f linguabot
```

Приложение начнет принимать запросы на порту `5000` (http://IP_СЕРВЕРА:5000).

---

## 3. Способ 2: Классическая установка на Linux (Systemd + Nginx)

Подходит для выделенных VPS без использования контейнеров.

### Шаг 1. Установка .NET 8 Runtime и PostgreSQL
```bash
# Добавление репозитория Microsoft для Ubuntu 24.04 / 22.04
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0 aspnetcore-runtime-8.0 postgresql postgresql-contrib nginx certbot python3-certbot-nginx
```

### Шаг 2. Настройка базы данных PostgreSQL
```bash
# Вход в консоль PostgreSQL
sudo -u postgres psql

# Выполнение SQL-команд:
CREATE DATABASE linguabot_db;
CREATE USER linguabot_user WITH ENCRYPTED PASSWORD 'StrongPassword2026!';
GRANT ALL PRIVILEGES ON DATABASE linguabot_db TO linguabot_user;
ALTER DATABASE linguabot_db OWNER TO linguabot_user;
\q
```

### Шаг 3. Публикация и размещение проекта
```bash
# Создание рабочей директории
sudo mkdir -p /var/www/linguabot
sudo chown -R $USER:$USER /var/www/linguabot

# Сборка Release-версии
cd /opt/linguabot/WebApplication1/WebApplication1
dotnet publish -c Release -o /var/www/linguabot

# Создание каталогов для локальных данных и прав доступа
sudo mkdir -p /var/www/linguabot/App_Data
sudo chown -R www-data:www-data /var/www/linguabot
```

### Шаг 4. Настройка службы Systemd для автозапуска
Создайте файл службы:
```bash
sudo nano /etc/systemd/system/linguabot.service
```
Вставьте конфигурацию:
```ini
[Unit]
Description=LinguaBot AI Language Tutor Web Application
After=network.target postgresql.service

[Service]
WorkingDirectory=/var/www/linguabot
ExecStart=/usr/bin/dotnet /var/www/linguabot/WebApplication1.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=linguabot
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
Environment=ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=linguabot_db;Username=linguabot_user;Password=StrongPassword2026!"
Environment=Gemini__ApiKey="ВАШ_КЛЮЧ_GEMINI"
Environment=Grok__ApiKey="ВАШ_КЛЮЧ_GROQ"

[Install]
WantedBy=multi-user.target
```

Активируйте и запустите службу:
```bash
sudo systemctl daemon-reload
sudo systemctl enable linguabot
sudo systemctl start linguabot
sudo systemctl status linguabot
```

---

## 4. Настройка веб-сервера Nginx и бесплатного SSL (HTTPS)

> [!IMPORTANT]
> Для работы записи голоса (Microphone API) и PWA Service Worker современные браузеры **обязательно требуют защищенного протокола HTTPS**. Без SSL браузер заблокирует микрофон!

### Конфигурация Nginx
Создайте виртуальный хост:
```bash
sudo nano /etc/nginx/sites-available/linguabot
```
Вставьте файл конфигурации (замените `yourdomain.com` на ваше доменное имя):
```nginx
server {
    listen 80;
    server_name yourdomain.com www.yourdomain.com;

    client_max_body_size 50M;

    location / {
        proxy_pass         http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }

    # Кэширование статических ассетов PWA
    location ~* \.(css|js|png|jpg|jpeg|gif|ico|svg|woff|woff2)$ {
        proxy_pass http://127.0.0.1:5000;
        expires 7d;
        add_header Cache-Control "public, no-transform";
    }
}
```

Активируйте конфигурацию и перезапустите Nginx:
```bash
sudo ln -s /etc/nginx/sites-available/linguabot /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

### Выпуск SSL-сертификата Let's Encrypt
```bash
sudo certbot --nginx -d yourdomain.com -d www.yourdomain.com
```
Certbot автоматически сконфигурирует перенаправление с HTTP на HTTPS (301 Redirect) и настроит таймер автоматического продления сертификата.

---

## 5. Настройка голосового синтезатора Piper TTS (Офлайн)

Для полностью автономного синтеза речи на сервере без обращения к сторонним API:

1. Загрузите бинарник Piper TTS для Linux:
```bash
cd /opt
wget https://github.com/rhasspy/piper/releases/download/v1.2.0/piper_amd64.tar.gz
tar -xvf piper_amd64.tar.gz
sudo ln -s /opt/piper/piper /usr/local/bin/piper
```

2. Скачайте обученные ONNX-модели голосов в каталог `piper_models`:
```bash
cd /var/www/linguabot/piper_models
# Русская модель (Ирина):
wget https://huggingface.co/rhasspy/piper-voices/resolve/main/ru/ru_RU/irina/medium/ru_RU-irina-medium.onnx
wget https://huggingface.co/rhasspy/piper-voices/resolve/main/ru/ru_RU/irina/medium/ru_RU-irina-medium.onnx.json

# Английская модель (Lessac):
wget https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/lessac/medium/en_US-lessac-medium.onnx
wget https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/lessac/medium/en_US-lessac-medium.onnx.json
```

---

## 6. Проверка работоспособности и мониторинг

### 6.1. Проверка API Health Check
Выполните команду на сервере или в браузере:
```bash
curl -i http://localhost:5000/api/health
```
Ожидаемый ответ:
```json
HTTP/1.1 200 OK
Content-Type: application/json

{
  "status": "healthy",
  "product": "LinguaBot",
  "version": "2.0-MVP",
  "time": "2026-11-19T13:50:00Z"
}
```

### 6.2. Проверка журналов событий
- Журналы Systemd:
  ```bash
  journalctl -u linguabot -f --no-tail
  ```
- Журналы Docker:
  ```bash
  docker compose logs -f --tail=50 linguabot
  ```

### 6.3. Резервное копирование данных
Создайте скрипт ежедневного бэкапа в cron (`/etc/cron.daily/linguabot_backup`):
```bash
#!/bin/bash
BACKUP_DIR="/var/backups/linguabot"
mkdir -p "$BACKUP_DIR"
DATE=$(date +\%Y\%m\%d_\%H\%M\%S)

# Бэкап PostgreSQL
pg_dump -U linguabot_user linguabot_db | gzip > "$BACKUP_DIR/db_$DATE.sql.gz"

# Бэкап файлового хранилища LinguaDataStore
cp /var/www/linguabot/App_Data/linguabot_store.json "$BACKUP_DIR/store_$DATE.json"

# Удаление бэкапов старше 30 дней
find "$BACKUP_DIR" -type f -mtime +30 -delete
```
Сделайте скрипт исполняемым:
```bash
chmod +x /etc/cron.daily/linguabot_backup
```
