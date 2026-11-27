using DugoutDeck.Api.Data;

namespace DugoutDeck.Api.Endpoints;

public record NameImportRequest(string FileName, string Content);
public record NameEnableRequest(bool Enabled);

/// <summary>Name packs (1.3): list, import, switch on/off, delete, export the current names, reset to the fictional names.</summary>
public static class NameEndpoints
{
    public static void MapNameEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/names");

        api.MapGet("/", (NamePacks packs) => Results.Ok(new { folder = packs.Folder, packs = packs.List() }));

        api.MapPost("/import", async (NameImportRequest req, NamePacks packs, AppDbContext db, ILogger<NamePacks> log) =>
        {
            var file = packs.Import(req.FileName, req.Content);
            await packs.ApplyAsync(db, log);
            return Results.Ok(new { file, packs = packs.List() });
        });

        api.MapPost("/{file}/enable", async (string file, NameEnableRequest req, NamePacks packs, AppDbContext db, ILogger<NamePacks> log) =>
        {
            packs.SetEnabled(file, req.Enabled);
            await packs.ApplyAsync(db, log);
            return Results.Ok(new { packs = packs.List() });
        });

        api.MapDelete("/{file}", async (string file, NamePacks packs, AppDbContext db, ILogger<NamePacks> log) =>
        {
            packs.Delete(file);
            await packs.ApplyAsync(db, log);
            return Results.Ok(new { packs = packs.List() });
        });

        api.MapPost("/export", async (NamePacks packs, AppDbContext db) =>
        {
            var (file, content) = await packs.ExportAsync(db);
            return Results.Ok(new { file, content, packs = packs.List() });
        });

        api.MapPost("/reset", async (NamePacks packs, AppDbContext db, ILogger<NamePacks> log) =>
        {
            packs.DisableAll();
            await packs.ApplyAsync(db, log);
            return Results.Ok(new { packs = packs.List() });
        });
    }
}
