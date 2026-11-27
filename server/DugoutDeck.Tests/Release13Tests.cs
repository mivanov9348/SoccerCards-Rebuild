using System.Text.Json;
using DugoutDeck.Api.Careers;
using DugoutDeck.Api.Data;
using DugoutDeck.Api.Data.Entities;
using DugoutDeck.Api.Game;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DugoutDeck.Tests;

/// <summary>Version 1.3: the economy (weekly expenses, league pack prices, merging), the club history and name packs.</summary>
public class Release13Tests
{
    private static string SeedPath => Path.Combine(AppContext.BaseDirectory, "Data", "Seed", "seed.json");

    private static AppDbContext Open(string file) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={file}").Options);

    private static async Task<(string Dir, string File)> NewSaveAsync()
    {
        var dir = Directory.CreateTempSubdirectory("dd13").FullName;
        var file = Path.Combine(dir, "save.db");
        await using var db = Open(file);
        await DatabaseUpgrade.RunAsync(db, NullLogger.Instance);
        await DbSeeder.SeedAsync(db, SeedPath, NullLogger.Instance);
        return (dir, file);
    }

    private static void Cleanup(string dir)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(dir, true); } catch (IOException) { }
    }

    [Fact]
    public void Expenses_AndPackPrices_FollowTheLeagueAndTheClub()
    {
        // a top-five club pays more than a Bulgarian one, a strong club more than a weak one, upgrades add upkeep
        Assert.True(CareerService.WeeklyExpenses(2.0, 85, 0) > CareerService.WeeklyExpenses(1.0, 85, 0));
        Assert.True(CareerService.WeeklyExpenses(1.0, 85, 0) > CareerService.WeeklyExpenses(1.0, 60, 0));
        Assert.Equal(CareerService.WeeklyExpenses(1.0, 70, 0) + 3 * CareerService.UpkeepPerLevel, CareerService.WeeklyExpenses(1.0, 70, 3));
        var small = CareerService.Packs[0];
        Assert.Equal(500_000, CareerService.PackPrice(small, 1.0));     // Bulgaria
        Assert.Equal(750_000, CareerService.PackPrice(small, 2.0));     // England: ×1.5
        Assert.Equal(350_000, CareerService.PackPrice(small, 0.4));     // a second division: ×0.7
    }

    [Fact]
    public async Task Career_DraftMergeWeeklyBill_AndHistory()
    {
        var (dir, file) = await NewSaveAsync();
        await using (var db = Open(file))
        {
            var careers = new CareerService(db, new MatchService(db));
            var team = await db.Teams.FirstAsync(t => t.Code == "BG1-01");
            var career = await careers.CreateAsync("t", "m", team.Id);
            Assert.NotNull(CareerService.Draft(career));
            await Assert.ThrowsAsync<GameRuleException>(() => careers.ContinueAsync(career)); // the draft comes first
            for (var i = 0; i < CareerService.DraftRounds; i++) await careers.DraftPickAsync(career, 0);
            Assert.Null(CareerService.Draft(career));
            Assert.Equal(CareerService.DraftRounds, (await careers.CollectionAsync(career.Id)).Values.Sum());

            // merging: 5 Normal Long Shots → 1 Rare
            db.CareerCards.RemoveRange(db.CareerCards.Where(c => c.CareerId == career.Id && c.Code == Cards.LongShot));
            await db.SaveChangesAsync();
            db.CareerCards.Add(new CareerCard { CareerId = career.Id, Code = Cards.LongShot, Tier = Tiers.Normal, Count = 6 });
            await db.SaveChangesAsync();
            var money = career.Money;
            Assert.Equal(Tiers.Rare, await careers.MergeAsync(career, Cards.LongShot, Tiers.Normal));
            var coll = await careers.CollectionAsync(career.Id);
            Assert.Equal(1, coll[(Cards.LongShot, Tiers.Normal)]);
            Assert.Equal(1, coll[(Cards.LongShot, Tiers.Rare)]);
            Assert.True(career.Money <= money - CareerService.MergeFee[0] + 100_000); // the fee (the "Upgrade!" achievement pays 100K back)
            await Assert.ThrowsAsync<GameRuleException>(() => careers.MergeAsync(career, Cards.LongShot, Tiers.Rare)); // only 1 Rare

            // one week: the club pays its wages
            var season = await careers.CurrentSeasonAsync(career.Id);
            await careers.CompleteWeekAsync(career, season);
            Assert.True(season.Expenses > 0);

            // the history tab has every achievement and the "Upgrade!" one is unlocked
            var json = JsonSerializer.Serialize(await careers.HistoryAsync(career));
            Assert.Contains("\"code\":\"merge\",", json.Replace("\"Code\"", "\"code\""), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(25, CareerService.Achievements.Length);
            Assert.Contains("merge", career.AchievementsJson);
        }
        Cleanup(dir);
    }

    [Fact]
    public async Task NamePacks_RenameClubsLeaguesAndCups_AndCanBeSwitchedOff()
    {
        var (dir, file) = await NewSaveAsync();
        var packs = new NamePacks(Path.Combine(dir, "names"), SeedPath);
        string original;
        await using (var db = Open(file)) original = (await db.Teams.FirstAsync(t => t.Code == "BG1-01")).Name;

        var pack = new NamePackFile
        {
            Name = "Test pack",
            Teams = [new TeamNameOverride { Code = "BG1-01", Name = "Pack Club", Short = "pck", Colors = ["#112233", "nonsense"] },
                     new TeamNameOverride { Code = "BG1-02", Name = original }], // a name swap with BG1-01's old name
            Leagues = [new CodeName { Code = "BG1", Name = "Pack League" }],
            Competitions = [new CodeName { Code = "UCL", Name = "Pack Champions" }]
        };
        var f = packs.Import("my pack!.json", JsonSerializer.Serialize(pack));
        Assert.Equal("my pack.json", f);
        await using (var db = Open(file))
        {
            await packs.ApplyAsync(db, NullLogger.Instance);
            var t = await db.Teams.FirstAsync(x => x.Code == "BG1-01");
            Assert.Equal("Pack Club", t.Name);
            Assert.Equal("PCK", t.ShortName);
            Assert.Equal("#112233", t.PrimaryColor);
            Assert.NotEqual("nonsense", t.SecondaryColor); // invalid colours are ignored
            Assert.Equal(original, (await db.Teams.FirstAsync(x => x.Code == "BG1-02")).Name);
            Assert.Equal("Pack League", (await db.Leagues.FirstAsync(l => l.Code == "BG1")).Name);
            Assert.Equal("Pack Champions", (await db.Competitions.FirstAsync(c => c.Code == "UCL")).Name);
            Assert.Equal(581, await db.Teams.CountAsync());

            // export writes a switched-off template with every club
            var (exportFile, content) = await packs.ExportAsync(db);
            Assert.EndsWith(".json.off", exportFile);
            Assert.Equal(581, JsonSerializer.Deserialize<NamePackFile>(content, NamePacks.Json)!.Teams.Count);

            // switched off: back to the fictional names
            packs.SetEnabled(f, false);
            await packs.ApplyAsync(db, NullLogger.Instance);
            Assert.Equal(original, (await db.Teams.FirstAsync(x => x.Code == "BG1-01")).Name);
        }
        Assert.Throws<GameRuleException>(() => packs.Import("bad.json", "{ not json"));
        Cleanup(dir);
    }

    [Fact]
    public void RareCardsWithoutAChance_AddOneToThePassStreak()
    {
        var engine = new MatchEngine(new Random(4), MatchEngineTestsCatalog.Get());
        var s = engine.Create(new MatchSetup(new Team { Id = 1, Name = "A" }, new Team { Id = 2, Name = "B" }, 70, 70, Side.Home, false, "T"));
        engine.Start(s, TacticRules.Default(), null);
        s.Turn = Side.Home;
        for (var i = 0; i < s.Table.Count; i++) s.Table[i] = new TableCell { Card = Cards.Pass };
        s.Table[0] = new TableCell { Card = Cards.Captain, Owner = Side.Home, Tier = Tiers.Rare };
        s.Table[1] = new TableCell { Card = Cards.Wall, Owner = Side.Home, Tier = Tiers.Normal };
        engine.FlipHuman(s, 0);
        Assert.Equal(1, s.Home.PassStreak); // Rare Captain: +1
        s.Home.OwnCardUsedThisTurn = false;
        engine.FlipHuman(s, 1);
        Assert.Equal(1, s.Home.PassStreak); // Normal Wall: nothing
        Assert.Equal("+1 to your pass streak", Tiers.Bonus(Cards.Wall, Tiers.Rare));
    }
}
