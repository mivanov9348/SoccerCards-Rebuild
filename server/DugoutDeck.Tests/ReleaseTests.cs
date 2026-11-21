using System.Text.Json.Nodes;
using DugoutDeck.Api.Data;
using DugoutDeck.Api.Data.Entities;
using DugoutDeck.Api.Game;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DugoutDeck.Tests;

/// <summary>Version 1.0: save files (migrations + seed sync by code) and difficulty.</summary>
public class ReleaseTests
{
    private static string SeedPath => Path.Combine(AppContext.BaseDirectory, "Data", "Seed", "seed.json");

    private static AppDbContext Open(string file) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={file}").Options);

    [Fact]
    public async Task NewSave_IsMigratedAndSeeded_AndSeedRenamesTeamsByCode_ExceptEditedOnes()
    {
        var dir = Directory.CreateTempSubdirectory("dd-test").FullName;
        var file = Path.Combine(dir, "save.db");
        await using (var db = Open(file))
        {
            await DatabaseUpgrade.RunAsync(db, NullLogger.Instance);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            await DbSeeder.SeedAsync(db, SeedPath, NullLogger.Instance);
            Assert.Equal(35, await db.Leagues.CountAsync());
            Assert.Equal(581, await db.Teams.CountAsync());
            Assert.DoesNotContain(await db.Teams.ToListAsync(), t => t.Code == null);
            // the user edits one club in the game editor
            var edited = await db.Teams.FirstAsync(t => t.Code == "BG1-02");
            edited.Name = "My Own Club";
            edited.EditedInGame = true;
            await db.SaveChangesAsync();
        }

        // rename two clubs and a league in a copy of seed.json
        var json = JsonNode.Parse(await File.ReadAllTextAsync(SeedPath))!;
        var bg1 = json["leagues"]![0]!;
        bg1["name"] = "Renamed League";
        bg1["teams"]![0]!["name"] = "Renamed Club One";
        bg1["teams"]![1]!["name"] = "Should Not Apply";
        // two clubs swap names
        var n3 = bg1["teams"]![2]!["name"]!.GetValue<string>();
        var n4 = bg1["teams"]![3]!["name"]!.GetValue<string>();
        bg1["teams"]![2]!["name"] = n4;
        bg1["teams"]![3]!["name"] = n3;
        var seed2 = Path.Combine(dir, "seed.json");
        await File.WriteAllTextAsync(seed2, json.ToJsonString());

        await using (var db = Open(file))
        {
            await DatabaseUpgrade.RunAsync(db, NullLogger.Instance);
            await DbSeeder.SeedAsync(db, seed2, NullLogger.Instance);
            Assert.Equal(581, await db.Teams.CountAsync()); // renamed, not duplicated
            Assert.Equal("Renamed Club One", (await db.Teams.FirstAsync(t => t.Code == "BG1-01")).Name);
            Assert.Equal("My Own Club", (await db.Teams.FirstAsync(t => t.Code == "BG1-02")).Name);
            Assert.Equal(n4, (await db.Teams.FirstAsync(t => t.Code == "BG1-03")).Name);
            Assert.Equal(n3, (await db.Teams.FirstAsync(t => t.Code == "BG1-04")).Name);
            Assert.Equal("Renamed League", (await db.Leagues.FirstAsync(l => l.Code == "BG1")).Name);
            Assert.Equal("Renamed League", (await db.Competitions.FirstAsync(c => c.Code == "BG1")).Name);
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(dir, true); } catch (IOException) { }
    }

    [Fact]
    public void Difficulty_ChangesTheComputerLevelAndYourPoints()
    {
        var engine = new MatchEngine(new Random(1), MatchEngineTestsCatalog.Get());
        Team T(int id) => new() { Id = id, Name = $"T{id}", City = $"C{id}" };
        var easy = engine.Create(new MatchSetup(T(1), T(2), 70, 70, Side.Home, false, "x", Difficulty.Easy));
        var hard = engine.Create(new MatchSetup(T(1), T(2), 70, 70, Side.Home, false, "x", Difficulty.Hard));
        var normal = engine.Create(new MatchSetup(T(1), T(2), 70, 70, Side.Home, false, "x"));
        Assert.Equal(1, normal.Away.AiLevel);
        Assert.Equal(0, easy.Away.AiLevel);
        Assert.Equal(2, hard.Away.AiLevel);
        Assert.Equal(MatchService.HumanLimits(normal).Budget + 2, MatchService.HumanLimits(easy).Budget);
        Assert.Equal(MatchService.HumanLimits(normal).Budget - 1, MatchService.HumanLimits(hard).Budget);
    }
}

internal static class MatchEngineTestsCatalog
{
    public static CardCatalog Get() => (CardCatalog)typeof(MatchEngineTests)
        .GetField("Catalog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!;
}
