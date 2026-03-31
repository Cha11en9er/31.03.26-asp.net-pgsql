using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MyApp.Data;
using MyApp.Models;
using MyApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Подхватываем значения из файла .env (для учебного проекта: без SMTP и без секретов в appsettings.json)
var envPath = Path.Combine(builder.Environment.ContentRootPath, ".env");
if (File.Exists(envPath))
{
    var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    foreach (var rawLine in File.ReadAllLines(envPath))
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith("#"))
            continue;

        var idx = line.IndexOf('=');
        if (idx <= 0)
            continue;

        var key = line.Substring(0, idx).Trim();
        var value = line[(idx + 1)..].Trim();

        if ((value.StartsWith("\"") && value.EndsWith("\"")) || (value.StartsWith("'") && value.EndsWith("'")))
            value = value.Substring(1, value.Length - 2);

        // .env удобнее писать как Jwt__Key / ConnectionStrings__DefaultConnection
        dict[key.Replace("__", ":")] = value;
    }

    builder.Configuration.AddInMemoryCollection(dict);
}

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection не настроен. Добавьте строку в .env.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<AuthService>();

var jwtKeyString = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKeyString))
    throw new InvalidOperationException("Jwt:Key не настроен. Добавьте Jwt__Key в .env.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
if (string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
    throw new InvalidOperationException("Jwt:Issuer / Jwt:Audience не настроены (оставьте в appsettings.json или .env).");

byte[] jwtKeyBytes;
try
{
    // Если вы вставите base64-ключ — используем как есть
    jwtKeyBytes = Convert.FromBase64String(jwtKeyString);
}
catch
{
    // Иначе ключ воспринимаем как обычную строку (UTF-8 байты)
    jwtKeyBytes = Encoding.UTF8.GetBytes(jwtKeyString);
}

if (jwtKeyBytes.Length < 32)
    throw new InvalidOperationException("Jwt:Key слишком короткий. Используйте ключ минимум 32+ байта.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MyApp API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Вставьте JWT: Bearer {токен}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
