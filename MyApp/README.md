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
   - ответ вернёт `confirmationToken` (в учебном режиме без отправки email).
2. Подтверждение email:
   - `POST /api/auth/confirm-email`
   - тело: `{ "token": "..." }`
3. Логин:
   - `POST /api/auth/login`
   - тело: `{ "email": "...", "password": "..." }`
   - ответ вернёт `token` (JWT).
4. Новости:
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

