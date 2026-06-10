# MyApp: архитектура и внутренняя логика

Документ для защиты диплома: кратко и по делу объясняет, как устроен проект, как подключена база данных, как работает backend и как реализована аутентификация пользователя.

## 1. Технологический стек

Один сервер на **.NET 8**: отдаёт API, статические страницы и Swagger. React, Node.js и npm **не используются**.

### Backend (C# / ASP.NET Core)

| Библиотека / технология | Где используется | Зачем |
|-------------------------|------------------|-------|
| **ASP.NET Core** (`Microsoft.NET.Sdk.Web`) | `Program.cs`, `Controllers/*` | HTTP-сервер и REST API |
| **JwtBearer** | `Program.cs` | Проверяет JWT в заголовке `Authorization` |
| **System.IdentityModel.Tokens.Jwt** | `AuthService.cs` | Создаёт JWT при логине |
| **Microsoft.IdentityModel.Tokens** | `Program.cs`, `AuthService.cs` | Ключ и подпись токена |
| **PasswordHasher** (встроен в ASP.NET) | `AuthService.cs` | Хеширует и проверяет пароль |
| **Entity Framework Core** | `AppDbContext.cs`, контроллеры, `AuthService.cs` | Работа с БД через C#-код |
| **Npgsql** (пакет `Npgsql.EntityFrameworkCore.PostgreSQL`) | `Program.cs` (`UseNpgsql`) | Подключение к PostgreSQL |
| **Swashbuckle** | `Program.cs` | Swagger UI (`/swagger`) |
| **System.Net.Mail** | `EmailSender.cs` | Отправка кода на email через SMTP |
| **System.Security.Cryptography** | `AuthService.cs`, `CaptchaService.cs` | Случайный код и выбор картинки капчи |

Свой код (не библиотеки):

| Файл | Зачем |
|------|-------|
| `AuthService.cs` | Регистрация, логин, JWT |
| `EmailSender.cs` | Обёртка над SMTP |
| `CaptchaService.cs` | Капча по картинкам из `captcha_pic/` |

NuGet-пакеты перечислены в `MyApp.csproj` (4 штуки: JwtBearer, Npgsql, EF Design, Swashbuckle).

### База данных

| Что | Где | Зачем |
|-----|-----|-------|
| **PostgreSQL** | внешний сервер | Хранит пользователей, услуги, договоры, транзакции |
| **AppDbContext** | `Data/AppDbContext.cs` | Доступ к таблицам из C# |
| **Models/** | `Models/*.cs` | C#-классы таблиц (`User`, `Product`, …) |
| **init.sql** | `Database/init.sql` | Создание таблиц и начальные данные |
| Строка подключения | `.env` → `Program.cs` | Логин/пароль к БД |

Цепочка: `Controller` → `AppDbContext` → `PostgreSQL`.

### Frontend (браузер)

| Технология | Где | Зачем |
|------------|-----|-------|
| **HTML** | `wwwroot/*.html` | Страницы: вход, регистрация, главная, профиль |
| **CSS** | `wwwroot/styles.css` | Внешний вид |
| **JavaScript** | `<script>` в html-файлах | Логика форм и запросов к API |
| **fetch** | все страницы с API | Вызовы `/api/...` |
| **localStorage** | `login.html`, `index.html`, `profile.html` | Хранит JWT после входа |

Фреймворков (React, Vue) нет — только чистый HTML/CSS/JS.

### Конфигурация

| Файл | Что там |
|------|---------|
| `.env` | Пароль БД, ключ JWT, настройки SMTP |
| `appsettings.json` | Issuer/Audience JWT, логи |
| `launchSettings.json` | URL при `dotnet run` |

## 2. Общая структура проекта

- `Program.cs` - точка входа, DI, конфигурация, middleware pipeline.
- `Data/AppDbContext.cs` - EF Core контекст и mapping сущностей.
- `Models/*` - доменные модели (`User`, `Product`, `Contract`, и т.д.).
- `Services/AuthService.cs` - регистрация, код email, логин, JWT.
- `Services/EmailSender.cs` - отправка кода через SMTP (`SmtpEmailSender`).
- `Services/CaptchaService.cs` - капча по изображениям из `captcha_pic/`.
- `Controllers/*` - HTTP API:
  - `AuthController` (`/api/auth/*`, включая `captcha/{id}`)
  - `CabinetController` (`/api/profile/*`, защищен `[Authorize]`)
  - `ProductsController` (`/api/products`)
  - `NewsController` (`/api/news`)
- `wwwroot/*.html` - клиентские страницы (`login`, `register`, `confirm`, `index`, `profile`).
- `Database/init.sql` - SQL-скрипт создания/обновления схемы и seed данных.

## 3. Подключение к базе данных PostgreSQL

### 3.1 Источник конфигурации

`Program.cs` вручную читает файл `.env` и добавляет пары ключ-значение в `Configuration`.

В `.env` используются ключи в формате:
- `ConnectionStrings__DefaultConnection`
- `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`
- `Smtp__Host`, `Smtp__Port`, `Smtp__EnableSsl`, `Smtp__Username`, `Smtp__Password`, `Smtp__FromEmail`, `Smtp__FromName`

В коде `__` преобразуется в `:`, поэтому приложение видит стандартные секции конфигурации ASP.NET (`ConnectionStrings:DefaultConnection`, `Jwt:Key` и т.д.).

### 3.2 Подключение EF Core

В `Program.cs`:
- считывается `ConnectionStrings:DefaultConnection`;
- при отсутствии строки выбрасывается `InvalidOperationException`;
- регистрируется `AppDbContext`:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
```

### 3.3 Структура таблиц и связи

Основные таблицы (см. `Database/init.sql`):

- `Users`
  - хранит учетную запись: email, `PasswordHash`, флаг подтверждения email, 6-значный `EmailConfirmationCode`, срок кода и метку `EmailCodeVerifiedAt` (после проверки кода из письма).
- `UserAccounts`
  - финансовая часть пользователя: `BalanceRub`, `DebtRub`, `UpdatedAt`.
  - связь 1:1 с `Users` по `UserId`.
- `Products`
  - каталог услуг (название, описание, цена).
- `News`
  - новости/объявления.
- `Contracts`
  - подключенные пользователем услуги (договор, сумма, дата).
  - связь many-to-one к `Users` и `Products`.
- `BalanceTransactions`
  - журнал финансовых операций пользователя (пополнение, начисление).
  - связь many-to-one к `Users`.

`AppDbContext.OnModelCreating()` явно задает:
- имена таблиц/полей;
- индексы (например, уникальный email в `Users`, уникальный номер договора в `Contracts`);
- внешние ключи и поведение удаления (`Cascade`/`Restrict`);
- точность `decimal` для денежных полей (`HasPrecision(14, 2)`).

## 4. Как работает backend (высокоуровнево)

### 4.1 Pipeline в `Program.cs`

Последовательность middleware:
1. `UseHttpsRedirection()`
2. `UseDefaultFiles()` + `UseStaticFiles()` (раздача `wwwroot`)
3. `UseAuthentication()`
4. `UseAuthorization()`
5. `MapControllers()`

Важно: порядок `UseAuthentication()` до `UseAuthorization()` обязателен для корректной проверки JWT.

### 4.2 Контроллеры и ответственность

- `AuthController`
  - регистрация, проверка кода из email, капча, финальное подтверждение email, логин.
- `ProductsController`
  - публичная выдача каталога услуг.
- `NewsController`
  - публичная выдача новостей.
- `CabinetController` (`[Authorize]`)
  - профиль пользователя;
  - подключение услуги;
  - пополнение баланса;
  - выдача договоров и финансовых операций.

## 5. Аутентификация и авторизация (самое главное)

Ниже фактический сценарий из кода проекта.

### 5.1 Модель пользователя

`Models/User.cs` содержит ключевые поля auth:
- `Email`
- `PasswordHash` (пароль хранится только в виде хеша)
- `IsEmailConfirmed`
- `EmailConfirmationCode` / `EmailConfirmationCodeExpiresAt` (код из письма, TTL 15 минут)
- `EmailCodeVerifiedAt` (код из email принят, ожидается капча)

### 5.2 Регистрация (`POST /api/auth/register`)

Поток в `AuthService.Register(...)` + `SmtpEmailSender`:

1. Нормализация email: `Trim().ToLowerInvariant()`.
2. Если пользователь уже подтверждён — `409 Conflict`.
3. Генерация 6-значного кода (`RandomNumberGenerator`, TTL 15 минут).
4. Хеширование пароля через `PasswordHasher<User>.HashPassword(...)`.
5. Создание или обновление записи в `Users` + при первой регистрации — `UserAccount` с нулевым балансом.
6. `SaveChangesAsync()`.
7. Отправка кода на email через `System.Net.Mail` / SMTP (`Smtp:Host`, `Smtp:Username`, … из `.env`).

Что важно проговорить на защите:
- plaintext-пароль в БД не хранится;
- до подтверждения email вход запрещен;
- для отправки письма нужна настройка SMTP в `.env`.

### 5.3 Подтверждение email: код + капча (два шага)

**Шаг 1 — код из письма** (`POST /api/auth/verify-email-code`):

1. `AuthService.VerifyEmailCode(email, code)` ищет пользователя и сверяет код и срок.
2. При успехе выставляется `EmailCodeVerifiedAt = UtcNow`.
3. `CaptchaService.CreateChallenge()` выбирает случайное изображение из `captcha_pic/`; ответ — имя файла без расширения.
4. Клиент получает `captchaId` и URL картинки `GET /api/auth/captcha/{captchaId}`.

**Шаг 2 — капча** (`POST /api/auth/confirm-email`):

1. `CaptchaService.VerifyAndConsume(captchaId, answer)` — одноразовая проверка (challenge удаляется из памяти).
2. `AuthService.ConfirmEmailAfterCaptcha(email)` проверяет, что код из письма был принят не позднее 10 минут назад.
3. `IsEmailConfirmed = true`, поля кода обнуляются.

Если код/капча неверны или просрочены → `400 BadRequest`.

### 5.4 Логин (`POST /api/auth/login`)

Поток в `AuthService.Login(email, password)`:

1. Поиск пользователя по нормализованному email.
2. Проверка пароля через `VerifyHashedPassword(...)`.
3. Проверка `IsEmailConfirmed`.
4. Если все успешно -> генерируется JWT.

Если email не подтвержден -> `403 Forbidden`.
Если пароль/email неверный -> `401 Unauthorized`.

### 5.5 Генерация JWT

Токен создается в `AuthService.GenerateJwt(user)`:

- считываются `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`;
- поддерживается ключ как base64 или как обычная строка;
- ключ должен быть не короче 32 байт;
- в JWT добавляются claims:
  - `ClaimTypes.NameIdentifier` = `user.Id`
  - `ClaimTypes.Email` = `user.Email`
  - `jti` = уникальный id токена
- срок жизни токена: 2 часа;
- подпись: `HmacSha256`.

### 5.6 Проверка JWT на сервере

JWT Bearer настраивается в `Program.cs`:
- `ValidateIssuer = true`
- `ValidateAudience = true`
- `ValidateLifetime = true`
- `ValidateIssuerSigningKey = true`

Любой endpoint под `[Authorize]` требует заголовок:

`Authorization: Bearer <jwt>`

### 5.7 Использование claims в бизнес-логике

`CabinetController` извлекает id пользователя из токена:

```csharp
var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
```

Далее по `userId` читает/изменяет данные именно этого пользователя (`UserAccounts`, `Contracts`, `BalanceTransactions`).

## 6. Поток работы фронтенда с auth

Фронтенд статический, но полностью рабочий.

- `register.html`
  - отправляет `POST /api/auth/register`;
  - сообщает, что код ушёл на email.
- `confirm.html`
  - шаг 1: `POST /api/auth/verify-email-code` (email + код из письма);
  - шаг 2: показ капчи и `POST /api/auth/confirm-email` (email + `captchaId` + ответ).
- `login.html`
  - отправляет `POST /api/auth/login`;
  - сохраняет полученный JWT в `localStorage` (`token`).
- `profile.html`
  - при отсутствии токена делает редирект на `login.html`;
  - для запросов к защищенным API добавляет `Authorization: Bearer ...`;
  - при `401/403` очищает токен и отправляет на вход.
- `index.html`
  - публично грузит `products` и `news`;
  - при заказе услуги вызывает защищенный `POST /api/profile/services` с JWT.

## 7. Бизнес-сценарии кабинета

`CabinetController` реализует:

1. `GET /api/profile`
   - возвращает агрегированную модель: user + account + services + transactions.
2. `POST /api/profile/services`
   - подключение услуги:
     - ищется продукт;
     - создается договор (`Contracts`);
     - увеличивается долг в `UserAccounts`;
     - пишется операция типа `Начисление` в `BalanceTransactions`.
3. `POST /api/profile/top-up`
   - пополнение:
     - сумма сначала гасит долг;
     - остаток зачисляется в баланс;
     - пишется операция `Пополнение`.