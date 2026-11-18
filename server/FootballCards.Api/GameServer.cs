using System.Text.Json.Serialization;
using FootballCards.Api.Careers;
using FootballCards.Api.Data;
using FootballCards.Api.Data.Entities;
using FootballCards.Api.Endpoints;
using FootballCards.Api.Game;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

namespace FootballCards.Api;

/// <summary>Options that differ between the web server and the desktop app.</summary>
public record GameServerOptions
{
    /// <summary>SQLite connection string; null = use appsettings.json.</summary>
    public string? ConnectionString { get; init; }
    /// <summary>Folder with the built React client (index.html); null or missing = API only.</summary>
    public string? ClientPath { get; init; }
    /// <summary>Address(es) to listen on; null = launchSettings / ASPNETCORE_URLS.</summary>
    public string? Urls { get; init; }
    public string? ContentRoot { get; init; }
}

/// <summary>Builds the game server. Used by both the web server (Program.cs) and the desktop app.</summary>
public static class GameServer
{
    public static WebApplication Build(string[] args, GameServerOptions? options = null)
    {
        options ??= new GameServerOptions();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = options.ContentRoot
        });
        if (options.Urls != null) builder.WebHost.UseUrls(options.Urls);

        // Database. SQLite by default (no installation). For SQL Server/PostgreSQL see README.md.
        var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
        var connection = options.ConnectionString
                         ?? builder.Configuration["Database:ConnectionString"] ?? "Data Source=soccer-cards.db";
        builder.Services.AddDbContext<AppDbContext>(o =>
        {
            switch (provider)
            {
                case "Sqlite":
                    o.UseSqlite(connection);
                    break;
                // case "SqlServer": o.UseSqlServer(connection); break;   // package Microsoft.EntityFrameworkCore.SqlServer
                // case "Postgres":  o.UseNpgsql(connection); break;      // package Npgsql.EntityFrameworkCore.PostgreSQL
                default:
                    throw new InvalidOperationException($"Unsupported database provider: {provider}");
            }
        });

        builder.Services.AddScoped<MatchService>();
        builder.Services.AddScoped<CareerService>();
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));

        var app = builder.Build();

        // Game rule errors are returned as 400 with a readable message.
        app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
        {
            var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
            var isRule = ex is GameRuleException or BadHttpRequestException;
            ctx.Response.StatusCode = isRule ? 400 : 500;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = ex is GameRuleException ? ex.Message
                    : ex is BadHttpRequestException ? "Invalid request."
                    : "Unexpected server error."
            });
        }));

        // Expected rule errors (e.g. "the match is over") are not logged as crashes.
        app.Use(async (ctx, next) =>
        {
            try { await next(); }
            catch (GameRuleException ex) when (!ctx.Response.HasStarted)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        });

        app.UseCors();

        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

        // Where the saves live (shown on the Load game screen).
        var saveFile = provider == "Sqlite"
            ? Path.GetFullPath(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connection).DataSource, builder.Environment.ContentRootPath)
            : provider;
        app.MapGet("/api/info", () => Results.Ok(new { saveFile }));
        app.MapDataEndpoints();
        app.MapMatchEndpoints();
        app.MapCareerEndpoints();

        // Serve the built React client, if there is one.
        var clientPath = options.ClientPath;
        if (clientPath != null && File.Exists(Path.Combine(clientPath, "index.html")))
        {
            var files = new PhysicalFileProvider(clientPath);
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
            app.MapFallback(async ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api")) { ctx.Response.StatusCode = 404; return; }
                ctx.Response.ContentType = "text/html; charset=utf-8";
                await ctx.Response.SendFileAsync(Path.Combine(clientPath, "index.html"));
            });
        }

        return app;
    }

    /// <summary>Creates the database if needed and loads the seed data.</summary>
    public static async Task InitializeDatabaseAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        var seedPath = Path.Combine(AppContext.BaseDirectory, "Data", "Seed", "seed.json");
        await DbSeeder.SeedAsync(db, seedPath, app.Logger);

        // Unfinished matches saved by an older version of the game cannot be resumed — drop them.
        var version = $"\"Version\":{MatchState.CurrentVersion}";
        db.Matches.RemoveRange(db.Matches.Where(m => m.Status == MatchStatus.InProgress && !m.StateJson.Contains(version)));
        await db.SaveChangesAsync();
    }
}
