using System.Text.Json;
using System.Text.Json.Serialization;
using FootballCards.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FootballCards.Api.Data;

/// <summary>
/// Зарежда Data/Seed/seed.json в базата. Добавя само липсващи записи (по име/код),
/// така че можете да допишете нови отбори в JSON файла и да рестартирате сървъра —
/// без да губите промените, направени през редактора.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, string seedPath, ILogger logger)
    {
        if (!File.Exists(seedPath))
        {
            logger.LogWarning("Seed file {Path} not found.", seedPath);
            return;
        }

        var json = await File.ReadAllTextAsync(seedPath);
        var seed = JsonSerializer.Deserialize<SeedFile>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        }) ?? new SeedFile();

        var added = 0;
        foreach (var l in seed.Leagues)
        {
            var league = await db.Leagues.FirstOrDefaultAsync(x => x.Name == l.Name);
            if (league is null)
            {
                league = new League { Name = l.Name, Country = l.Country, Tier = l.Tier, IsPlayable = l.IsPlayable };
                db.Leagues.Add(league);
                await db.SaveChangesAsync();
                added++;
            }
            foreach (var t in l.Teams)
                added += await AddTeamIfMissing(db, t, l.Country, league.Id);
        }

        foreach (var t in seed.EuropeanClubs)
            added += await AddTeamIfMissing(db, t, t.Country ?? "", null);

        foreach (var c in seed.Competitions)
        {
            if (await db.Competitions.AnyAsync(x => x.Code == c.Code)) continue;
            int? leagueId = c.League is null ? null
                : (await db.Leagues.FirstOrDefaultAsync(x => x.Name == c.League))?.Id;
            db.Competitions.Add(new Competition { Code = c.Code, Name = c.Name, Type = c.Type, Tier = c.Tier, LeagueId = leagueId });
            added++;
        }

        // Cards are game rules, not user data: always keep them in sync with seed.json.
        foreach (var c in seed.Cards)
        {
            var card = await db.Cards.FirstOrDefaultAsync(x => x.Code == c.Code);
            if (card is null)
            {
                db.Cards.Add(card = new CardDefinition { Code = c.Code });
                added++;
            }
            card.Name = c.Name;
            card.Description = c.Description;
            card.CopiesInDeck = c.Copies;
        }
        var codes = seed.Cards.Select(c => c.Code).ToList();
        db.Cards.RemoveRange(db.Cards.Where(x => !codes.Contains(x.Code)));

        await db.SaveChangesAsync();
        if (added > 0) logger.LogInformation("Seed: added {Count} records.", added);
    }

    private static async Task<int> AddTeamIfMissing(AppDbContext db, SeedTeam t, string country, int? leagueId)
    {
        if (await db.Teams.AnyAsync(x => x.Name == t.Name)) return 0;
        db.Teams.Add(new Team
        {
            Name = t.Name,
            ShortName = string.IsNullOrWhiteSpace(t.ShortName) ? t.Name[..Math.Min(3, t.Name.Length)].ToUpper() : t.ShortName,
            City = t.City, Country = t.Country ?? country, Stadium = t.Stadium,
            PrimaryColor = t.PrimaryColor, SecondaryColor = t.SecondaryColor,
            Attack = t.Attack, Midfield = t.Midfield, Defense = t.Defense, LeagueId = leagueId
        });
        await db.SaveChangesAsync();
        return 1;
    }

    private sealed class SeedFile
    {
        public List<SeedLeague> Leagues { get; set; } = new();
        public List<SeedTeam> EuropeanClubs { get; set; } = new();
        public List<SeedCompetition> Competitions { get; set; } = new();
        public List<SeedCard> Cards { get; set; } = new();
    }

    private sealed class SeedLeague
    {
        public string Name { get; set; } = "";
        public string Country { get; set; } = "";
        public int Tier { get; set; } = 1;
        public bool IsPlayable { get; set; }
        public List<SeedTeam> Teams { get; set; } = new();
    }

    private sealed class SeedTeam
    {
        public string Name { get; set; } = "";
        public string? ShortName { get; set; }
        public string City { get; set; } = "";
        public string? Country { get; set; }
        public string Stadium { get; set; } = "";
        public string PrimaryColor { get; set; } = "#336699";
        public string SecondaryColor { get; set; } = "#ffffff";
        public int Attack { get; set; } = 60;
        public int Midfield { get; set; } = 60;
        public int Defense { get; set; } = 60;
    }

    private sealed class SeedCompetition
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public CompetitionType Type { get; set; }
        public int Tier { get; set; } = 1;
        public string? League { get; set; }
    }

    private sealed class SeedCard
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int Copies { get; set; }
    }
}
