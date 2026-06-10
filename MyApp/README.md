# MyApp (ASP.NET Core Web API + PostgreSQL + нативный фронт)

MVP: регистрация/подтверждение email/логин (JWT) и новости (требуется авторизация). Фронт — обычные `html/css/js` из `wwwroot/`. База — PostgreSQL через EF Core и `Npgsql`.

## 0) Что нужно заранее (Windows)

1. Установить .NET SDK (без него команда `dotnet` не работает).
2. Поднять PostgreSQL и подготовить БД/пользователя (можно по вашим SQL-запросам в DBeaver).
3. Иметь файл `MyApp/.env` (он уже добавлен в проект; при желании замените значения).

Если .NET SDK не установлен, перейдите к шагу **1**.

## 1) Установка .NET SDK

1. Скачайте .NET SDK 8:
   - https://dotnet.microsoft.com/en-us/download/dotnet/8.0
2. Установите SDK (обычно это один установщик).
3. Перезапустите терминал/IDE (чтобы PATH обновился).
4. Проверьте в PowerShell:

```powershell
dotnet --info
```

Если команды нет — установка не завершилась или не применился PATH.

## 2) Настройка подключения к PostgreSQL

1. Откройте файл `MyApp/.env` и убедитесь, что там совпадают параметры вашей БД:
   - `ConnectionStrings__DefaultConnection`
   - `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`
   - SMTP для отправки кода регистрации:
     - `Smtp__Host`
     - `Smtp__Port` (обычно `587`)
     - `Smtp__EnableSsl` (`true`/`false`)
     - `Smtp__Username`
     - `Smtp__Password`
     - `Smtp__FromEmail`
     - `Smtp__FromName`
2. Убедитесь, что в PostgreSQL есть:
   - БД `tgk_bd`
   - пользователь `tgk2_user` (пароль `tgk2_password`)
   - права на чтение/запись схемы `public`

Проще всего: если вы уже выполнили SQL в DBeaver (как писали), то этот шаг можно пропустить.

## 3) Запуск приложения

Выполните команды из папки проекта `MyApp`:

```powershell
cd "c:\repos\YouDo\29.03.26-asp.net+pgsql\MyApp"
dotnet restore
dotnet build
dotnet run
```

После `dotnet run` в консоли будет указан URL вида `https://localhost:...` и `http://localhost:...`.

Открывайте:
- Swagger (удобно тестировать API): `https://localhost:<порт>/swagger`
- Нативный фронт:
  - `https://localhost:<порт>/login.html`
  - `https://localhost:<порт>/register.html`
  - `https://localhost:<порт>/index.html`

## 4) Как пользоваться (API)

1. Регистрация:
   - `POST /api/auth/register`
   - тело: `{ "email": "...", "password": "..." }`
   - код подтверждения отправляется на email.
2. Проверка кода из email:
   - `POST /api/auth/verify-email-code`
   - тело: `{ "email": "...", "code": "123456" }`
   - ответ: `captchaId` и `captchaImageUrl`.
3. Подтверждение email капчей:
   - `POST /api/auth/confirm-email`
   - тело: `{ "email": "...", "captchaId": "...", "captchaAnswer": "..." }`
4. Логин:
   - `POST /api/auth/login`
   - тело: `{ "email": "...", "password": "..." }`
   - ответ вернёт `token` (JWT).
5. Новости:
   - `GET /api/news`
   - нужен заголовок `Authorization: Bearer <token>`

## 5) Где лежит фронт и статика

Фронтовые страницы:
- `MyApp/wwwroot/login.html`
- `MyApp/wwwroot/register.html`
- `MyApp/wwwroot/confirm.html`
- `MyApp/wwwroot/index.html`
- стили: `MyApp/wwwroot/styles.css`

Статика подключена в `Program.cs` через `UseStaticFiles()` и `UseDefaultFiles()`.

## 6) Частые причины “ничего не запускается”

1. Нет .NET SDK: `dotnet` не найден (решается установкой SDK и `dotnet --info`).
2. Не настроен `.env`:
   - нет `ConnectionStrings__DefaultConnection`
   - или нет `Jwt__Key / Jwt__Issuer / Jwt__Audience`
3. Проблемы с БД/правами: EF/приложение не может подключиться.

Если приложение всё равно падает — пришлите текст ошибки из консоли (самое важное: первая ошибка/stack trace).

## 7) Обновление БД и SMTP (пошагово)

### Шаг 1. Обновите таблицу `Users` в PostgreSQL

Выполните в DBeaver/psql:

```sql
ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "EmailConfirmationCode" character varying(16) NULL;
ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "EmailConfirmationCodeExpiresAt" timestamp with time zone NULL;
ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "EmailCodeVerifiedAt" timestamp with time zone NULL;
ALTER TABLE "Users" DROP COLUMN IF EXISTS "EmailConfirmationToken";
ALTER TABLE "Users" DROP COLUMN IF EXISTS "EmailConfirmationTokenExpiresAt";
```

### Шаг 2. Настройте SMTP в `MyApp/.env`

Пример для Gmail:

```env
Smtp__Host=smtp.gmail.com
Smtp__Port=587
Smtp__EnableSsl=true
Smtp__Username=your_sender@gmail.com
Smtp__Password=your_app_password
Smtp__FromEmail=your_sender@gmail.com
Smtp__FromName=TGK-2 Portal
```

Где взять `Smtp__Password` для Gmail:
- включите 2FA в Google-аккаунте;
- создайте App Password на странице [Google App Passwords](https://myaccount.google.com/apppasswords);
- вставьте выданный 16-символьный пароль в `Smtp__Password`.

### Шаг 3. Перезапустите приложение

После изменения `.env` обязательно перезапустите `dotnet run`, иначе новые переменные не подхватятся.

### Если в логах или в alert: `Smtp:Host не настроен`

Приложение читает `.env` из **корня контента** (в логах при старте есть строка `Content root path: ...`). Файл должен лежать **именно там**, а не только в исходниках на другом диске.

Частые причины:

1. **`dotnet publish` / IIS / служба** — рабочая папка это `publish`, и в ней не оказалось `.env`. Скопируйте `MyApp/.env` в ту же папку, где лежит `MyApp.dll`, либо пересоберите проект: в `MyApp.csproj` настроено копирование `.env` в output/publish, если файл есть локально.
2. **`.env` не в репозитории** (он в `.gitignore`) — после `git pull` на машине заказчика файл нужно **создать заново** или скопировать с вашей машины; иначе на сервере его просто нет.
3. **Имена ключей** — только так: `Smtp__Host`, `Smtp__Port`, … (два подчёркивания `__`, без пробелов вокруг `=`).

### Шаг 4. Если при регистрации видите `500`

- проверьте сообщение ошибки в ответе API: теперь там показывается причина SMTP;
- проверьте, что `Smtp__Username` и `Smtp__FromEmail` совпадают и существуют;
- убедитесь, что используется App Password, а не обычный пароль Gmail;
- не нажимайте кнопку регистрации несколько раз подряд (на странице добавлена блокировка повторного клика).