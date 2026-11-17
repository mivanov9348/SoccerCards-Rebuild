using System.Text.Json.Serialization;
using FootballCards.Api.Careers;
using FootballCards.Api.Data;
using FootballCards.Api.Endpoints;
using FootballCards.Api.Game;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// База данни. По подразбиране SQLite (без инсталация). За SQL Server/PostgreSQL вижте README.md.
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var connection = builder.Configuration["Database:ConnectionString"] ?? "Data Source=football-cards.db";
builder.Services.AddDbContext<AppDbContext>(options =>
{
    switch (provider)
    {
        case "Sqlite":
            options.UseSqlite(connection);
            break;
        // case "SqlServer": options.UseSqlServer(connection); break;   // пакет Microsoft.EntityFrameworkCore.SqlServer
        // case "Postgres":  options.UseNpgsql(connection); break;      // пакет Npgsql.EntityFrameworkCore.PostgreSQL
        default:
            throw new InvalidOperationException($"Неподдържан доставчик на база данни: {provider}");
    }
});

builder.Services.AddScoped<MatchService>();
builder.Services.AddScoped<CareerService>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Създаване на базата и зареждане на началните данни.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    var seedPath = Path.Combine(AppContext.BaseDirectory, "Data", "Seed", "seed.json");
    await DbSeeder.SeedAsync(db, seedPath, app.Logger);
}

// Грешките в правилата на играта се връщат като 400 с текст на български.
app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var isRule = ex is GameRuleException or BadHttpRequestException;
    ctx.Response.StatusCode = isRule ? 400 : 500;
    await ctx.Response.WriteAsJsonAsync(new
    {
        error = ex is GameRuleException ? ex.Message
            : ex is BadHttpRequestException ? "Невалидна заявка."
            : "Възникна неочаквана грешка на сървъра."
    });
}));

app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapDataEndpoints();
app.MapMatchEndpoints();
app.MapCareerEndpoints();

// Ако клиентът е компилиран (npm run build), сървърът го обслужва директно.
var clientDist = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "client", "dist"));
if (Directory.Exists(clientDist))
{
    var files = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(clientDist);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
    app.MapFallback(async ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api")) { ctx.Response.StatusCode = 404; return; }
        ctx.Response.ContentType = "text/html; charset=utf-8";
        await ctx.Response.SendFileAsync(Path.Combine(clientDist, "index.html"));
    });
}

app.Run();
