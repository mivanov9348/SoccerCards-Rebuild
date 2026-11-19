using System.Text.Json;
using System.Text.Json.Serialization;
using FootballCards.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FootballCards.Api.Data;

/// <summary>
/// Loads Data/Seed/seed.json into the database. Leagues, teams and competitions are only added when missing
/// (by name/code), so edits made in the editor are kept. Cards are game rules and are always synced.
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

        var seed = JsonSerializer.Deserialize<SeedFile>(await File.ReadAllTextAsync(seedPath), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        }) ?? new SeedFile();

        var existingTeams = (await db.Teams.Select(t => t.Name).ToListAsync()).ToHashSet();
        var added = 0;

        foreach (var l in seed.Leagues)
        {
            var league = await db.Leagues.FirstOrDefaultAsync(x => x.Name == l.Name);
            if (league is null)
            {
                league = new League
                {
                    Name = l.Name, Country = l.Country, Tier = l.Tier, IsPlayable = l.IsPlayable,
                    Relegation = l.Relegation ?? 2, UclSpots = l.Ucl, UelSpots = l.Uel, UeclSpots = l.Uecl
                };
                db.Leagues.Add(league);
                await db.SaveChangesAsync();
                added++;
            }
            if (l.Code != null && !await db.Competitions.AnyAsync(c => c.Code == l.Code))
            {
                db.Competitions.Add(new Competition { Code = l.Code, Name = l.Name, Type = CompetitionType.League, Tier = l.Tier, LeagueId = league.Id });
                added++;
            }
            foreach (var row in l.Teams)
                if (AddTeam(db, existingTeams, row, l.Country, league.Id)) added++;
        }

        foreach (var row in seed.EuropeanClubs)
            if (AddTeam(db, existingTeams, row, null, null)) added++;

        foreach (var c in seed.Cups)
            if (!await db.Competitions.AnyAsync(x => x.Code == c.Code))
            {
                db.Competitions.Add(new Competition { Code = c.Code, Name = c.Name, Type = CompetitionType.Cup, Country = c.Country });
                added++;
            }

        foreach (var c in seed.Competitions)
            if (!await db.Competitions.AnyAsync(x => x.Code == c.Code))
            {
                db.Competitions.Add(new Competition { Code = c.Code, Name = c.Name, Type = c.Type, Tier = c.Tier });
                added++;
            }

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
            card.Kind = c.Kind;
            card.Rarity = c.Rarity;
            card.CopiesInDeck = c.Copies;
            card.Cost = c.Cost;
            card.Price = c.Price;
        }
        var codes = seed.Cards.Select(c => c.Code).ToList();
        db.Cards.RemoveRange(db.Cards.Where(x => !codes.Contains(x.Code)));

        await db.SaveChangesAsync();
        if (added > 0) logger.LogInformation("Seed: added {Count} records.", added);
    }

    /// <summary>Team row: [name, short, city, stadium, color1, color2, rating(, midfield, defense)(, country)].</summary>
    private static bool AddTeam(AppDbContext db, HashSet<string> existing, JsonElement[] row, string? country, int? leagueId)
    {
        var name = row[0].GetString()!;
        if (!existing.Add(name)) return false;
        var numbers = row.Skip(6).Where(e => e.ValueKind == JsonValueKind.Number).Select(e => e.GetInt32()).ToArray();
        var rowCountry = row.Skip(6).FirstOrDefault(e => e.ValueKind == JsonValueKind.String);
        var city = row[2].GetString() ?? "";
        var stadium = row[3].GetString();
        db.Teams.Add(new Team
        {
            Name = name,
            ShortName = row[1].GetString() ?? name[..Math.Min(3, name.Length)].ToUpper(),
            City = city,
            Stadium = string.IsNullOrWhiteSpace(stadium) ? $"{city} Stadium" : stadium,
            PrimaryColor = row[4].GetString() ?? "#336699",
            SecondaryColor = row[5].GetString() ?? "#ffffff",
            Attack = numbers.ElementAtOrDefault(0),
            Midfield = numbers.Length >= 3 ? numbers[1] : numbers.ElementAtOrDefault(0),
            Defense = numbers.Length >= 3 ? numbers[2] : numbers.ElementAtOrDefault(0),
            Country = country ?? (rowCountry.ValueKind == JsonValueKind.String ? rowCountry.GetString()! : ""),
            LeagueId = leagueId
        });
        return true;
    }

    private sealed class SeedFile
    {
        public List<SeedLeague> Leagues { get; set; } = new();
        public List<JsonElement[]> EuropeanClubs { get; set; } = new();
        public List<SeedCup> Cups { get; set; } = new();
        public List<SeedCompetition> Competitions { get; set; } = new();
        public List<SeedCard> Cards { get; set; } = new();
    }

    private sealed class SeedLeague
    {
        public string? Code { get; set; }
        public string Name { get; set; } = "";
        public string Country { get; set; } = "";
        public int Tier { get; set; } = 1;
        public bool IsPlayable { get; set; }
        public int? Relegation { get; set; }
        public int Ucl { get; set; }
        public int Uel { get; set; }
        public int Uecl { get; set; }
        public List<JsonElement[]> Teams { get; set; } = new();
    }

    private sealed class SeedCup
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Country { get; set; } = "";
    }

    private sealed class SeedCompetition
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public CompetitionType Type { get; set; }
        public int Tier { get; set; } = 1;
    }

    private sealed class SeedCard
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public CardKind Kind { get; set; }
        public CardRarity Rarity { get; set; }
        public int Copies { get; set; }
        public int Cost { get; set; }
        public long Price { get; set; }
    }
}
