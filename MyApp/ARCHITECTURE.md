# MyApp: архитектура и внутренняя логика

Документ для защиты диплома: кратко и по делу объясняет, как устроен проект, как подключена база данных, как работает backend и как реализована аутентификация пользователя.

## 1. Технологический стек

- Backend: ASP.NET Core Web API (`net8.0`)
- База данных: PostgreSQL
- ORM: Entity Framework Core + `Npgsql`
- Аутентификация: JWT Bearer
- Frontend: нативные `HTML/CSS/JS` в `wwwroot`
- API-документация: Swagger

Ключевые NuGet-пакеты:
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `Npgsql.EntityFrameworkCore.PostgreSQL`
- `Swashbuckle.AspNetCore`

## 2. Общая структура проекта

- `Program.cs` - точка входа, DI, конфигурация, middleware pipeline.
- `Data/AppDbContext.cs` - EF Core контекст и mapping сущностей.
- `Models/*` - доменные модели (`User`, `Product`, `Contract`, и т.д.).
- `Services/AuthService.cs` - основная логика auth.
- `Controllers/*` - HTTP API:
  - `AuthController` (`/api/auth/*`)
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
- `Jwt__Key`
- `Jwt__Issuer`
- `Jwt__Audience`

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
  - хранит учетную запись: email, `PasswordHash`, флаг подтверждения email, токен подтверждения и срок его действия.
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
  - регистрация, подтверждение email, логин.
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
- `EmailConfirmationToken`
- `EmailConfirmationTokenExpiresAt`

### 5.2 Регистрация (`POST /api/auth/register`)

Поток в `AuthService.Register(...)`:

1. Нормализация email: `Trim().ToLowerInvariant()`.
2. Проверка уникальности пользователя по email.
3. Генерация одноразового токена подтверждения (`Guid.NewGuid().ToString("N")`).
4. Установка TTL токена (24 часа).
5. Хеширование пароля через `PasswordHasher<User>.HashPassword(...)`.
6. Создание пользователя в `Users`.
7. Одновременно создается запись `UserAccount` с нулевым балансом/долгом.
8. `SaveChangesAsync()`.
9. В ответ возвращается `confirmationToken` (в учебной версии показывается напрямую, без SMTP-отправки письма).

Что важно проговорить на защите:
- plaintext-пароль в БД не хранится;
- до подтверждения email вход запрещен.

### 5.3 Подтверждение email (`POST /api/auth/confirm-email`)

Поток в `AuthService.ConfirmEmail(token)`:

1. Поиск пользователя по токену.
2. Проверка срока действия токена.
3. Если токен валиден:
   - `IsEmailConfirmed = true`;
   - `EmailConfirmationToken = null`;
   - `EmailConfirmationTokenExpiresAt = null`.
4. `SaveChangesAsync()`.

Если токен неверный/просроченный -> 400 BadRequest.

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
  - показывает `confirmationToken`.
- `confirm.html`
  - отправляет `POST /api/auth/confirm-email` с токеном.
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

## 8. Что сказать на защите (краткий порядок рассказа)

1. **Старт приложения**: `Program.cs` (конфиг из `.env`, DI, EF Core, JWT middleware).
2. **База данных**: `AppDbContext` + таблицы (`Users`, `UserAccounts`, `Contracts`, `BalanceTransactions`, `Products`, `News`).
3. **Регистрация**: создание пользователя, хеш пароля, выдача токена подтверждения.
4. **Подтверждение email**: валидация токена и активация учетной записи.
5. **Логин и JWT**: валидация пароля, выпуск токена, claims внутри токена.
6. **Авторизация API**: `[Authorize]`, `Bearer` заголовок, извлечение `userId` из claims.
7. **Связка с фронтом**: `localStorage` токена, защищенные запросы из `profile.html`/`index.html`.

## 9. Ограничения текущей учебной реализации

- Подтверждение email сейчас учебное: токен возвращается в API-ответе, без реальной отправки письма.
- Регистрация не использует дополнительные политики сложности пароля (их можно добавить отдельной валидацией).
- Хранение JWT в `localStorage` удобно для демо, но для production обычно рассматривают более строгие подходы (например, httpOnly cookie + CSRF-защита в соответствующей архитектуре).

