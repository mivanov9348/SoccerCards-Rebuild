using System.Text.Json;
using System.Text.Json.Serialization;
using DugoutDeck.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DugoutDeck.Api.Data;

/// <summary>
/// Loads Data/Seed/seed.json into the save file on every start.
/// <list type="bullet">
/// <item>Leagues, teams and competitions that are missing are added.</item>
/// <item>Every team, league and competition has a stable code. Names, short names, cities, stadiums, colours and ratings
/// are synced from seed.json by that code — so renaming a club in seed.json renames it in every save. Teams changed in
/// the in-game editor (<see cref="Team.EditedInGame"/>) are left alone.</item>
/// <item>Cards are game rules and are always synced.</item>
/// </list>
/// </summary>
public static class DbSeeder
{
    /// <param name="names">active name packs (1.3): club, league and cup names that replace the ones in seed.json</param>
    public static async Task SeedAsync(AppDbContext db, string seedPath, ILogger logger, NameOverlay? names = null)
    {
        names ??= new NameOverlay();
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

        var changes = 0;
        var teams = await db.Teams.ToListAsync();
        var byCode = teams.Where(t => t.Code != null).ToDictionary(t => t.Code!);
        var byName = teams.GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.First());
        var leagueComps = await db.Competitions.Where(c => c.Type == CompetitionType.League && c.LeagueId != null).ToListAsync();

        // Renames happen in two steps, so clubs can swap names (e.g. A gets B's old name) without breaking the unique name rule:
        // first every club that will be renamed gets a temporary name, then the new names are applied.
        var seedNames = seed.Leagues.SelectMany(l => l.Teams).Concat(seed.EuropeanClubs)
            .Select(e => Overlay(SeedTeam.From(e, null), names)).Where(t => t.Code != null).ToDictionary(t => t.Code!, t => t.Name);
        foreach (var l in seed.Leagues) if (l.Code != null && names.Leagues.TryGetValue(l.Code, out var ln)) l.Name = ln;
        foreach (var c in seed.Cups) if (names.Competitions.TryGetValue(c.Code, out var cn)) c.Name = cn;
        foreach (var c in seed.Competitions) if (names.Competitions.TryGetValue(c.Code, out var cn)) c.Name = cn;
        var blocked = teams.Where(t => t.EditedInGame || t.Code == null || !seedNames.ContainsKey(t.Code)).Select(t => t.Name).ToHashSet();
        var renaming = teams.Where(t => t.Code != null && !t.EditedInGame && seedNames.TryGetValue(t.Code, out var n) && n != t.Name && !blocked.Contains(n)).ToList();
        if (renaming.Count > 0)
        {
            foreach (var t in renaming) t.Name = $"~{t.Code}";
            await db.SaveChangesAsync();
            byName = teams.GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.First());
        }

        foreach (var l in seed.Leagues)
        {
            // find the league by code, or (older saves) through its competition code, or by name
            var league = l.Code == null ? null : await db.Leagues.FirstOrDefaultAsync(x => x.Code == l.Code);
            if (league == null && l.Code != null && leagueComps.FirstOrDefault(c => c.Code == l.Code) is { } comp)
                league = await db.Leagues.FindAsync(comp.LeagueId);
            league ??= await db.Leagues.FirstOrDefaultAsync(x => x.Name == l.Name);
            if (league is null)
            {
                league = new League
                {
                    Name = l.Name, Country = l.Country, Tier = l.Tier, IsPlayable = l.IsPlayable,
                    Relegation = l.Relegation ?? 2, UclSpots = l.Ucl, UelSpots = l.Uel, UeclSpots = l.Uecl
                };
                db.Leagues.Add(league);
                changes++;
            }
            if (league.Code != l.Code || league.Name != l.Name)
            {
                if (!await db.Leagues.AnyAsync(x => x.Name == l.Name && x.Id != league.Id) || league.Name == l.Name)
                {
                    league.Code = l.Code;
                    league.Name = l.Name;
                    changes++;
                }
            }
            await db.SaveChangesAsync();

            if (l.Code != null)
            {
                var c = await db.Competitions.FirstOrDefaultAsync(x => x.Code == l.Code);
                if (c == null) db.Competitions.Add(new Competition { Code = l.Code, Name = l.Name, Type = CompetitionType.League, Tier = l.Tier, LeagueId = league.Id });
                else c.Name = l.Name;
            }

            foreach (var row in l.Teams)
                changes += SyncTeam(db, Overlay(SeedTeam.From(row, l.Country), names), league.Id, byCode, byName);
        }

        foreach (var row in seed.EuropeanClubs)
            changes += SyncTeam(db, Overlay(SeedTeam.From(row, null), names), null, byCode, byName);

        foreach (var c in seed.Cups)
        {
            var comp = await db.Competitions.FirstOrDefaultAsync(x => x.Code == c.Code);
            if (comp == null) { db.Competitions.Add(new Competition { Code = c.Code, Name = c.Name, Type = CompetitionType.Cup, Country = c.Country }); changes++; }
            else comp.Name = c.Name;
        }

        foreach (var c in seed.Competitions)
        {
            var comp = await db.Competitions.FirstOrDefaultAsync(x => x.Code == c.Code);
            if (comp == null) { db.Competitions.Add(new Competition { Code = c.Code, Name = c.Name, Type = c.Type, Tier = c.Tier }); changes++; }
            else comp.Name = c.Name;
        }

        foreach (var c in seed.Cards)
        {
            var card = await db.Cards.FirstOrDefaultAsync(x => x.Code == c.Code);
            if (card is null)
            {
                db.Cards.Add(card = new CardDefinition { Code = c.Code });
                changes++;
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
        if (changes > 0) logger.LogInformation("Seed: {Count} changes applied.", changes);
    }

    /// <summary>A name pack entry replaces the name, short name, city, stadium and colours it gives (ratings stay).</summary>
    private static SeedTeam Overlay(SeedTeam s, NameOverlay names)
    {
        if (s.Code == null || !names.Teams.TryGetValue(s.Code, out var o)) return s;
        return s with
        {
            Name = string.IsNullOrWhiteSpace(o.Name) ? s.Name : o.Name.Trim(),
            Short = string.IsNullOrWhiteSpace(o.Short) ? s.Short : o.Short.Trim(),
            City = string.IsNullOrWhiteSpace(o.City) ? s.City : o.City.Trim(),
            Stadium = string.IsNullOrWhiteSpace(o.Stadium) ? s.Stadium : o.Stadium.Trim(),
            Color1 = o.Colors is { Count: > 0 } && NameOverlay.IsColor(o.Colors[0]) ? o.Colors[0] : s.Color1,
            Color2 = o.Colors is { Count: > 1 } && NameOverlay.IsColor(o.Colors[1]) ? o.Colors[1] : s.Color2,
        };
    }

    /// <summary>Adds a team that is missing, or updates it from seed.json. Returns 1 when something changed.</summary>
    private static int SyncTeam(AppDbContext db, SeedTeam s, int? leagueId, Dictionary<string, Team> byCode, Dictionary<string, Team> byName)
    {
        var team = (s.Code != null ? byCode.GetValueOrDefault(s.Code) : null) ?? byName.GetValueOrDefault(s.Name);
        var country = s.Country ?? "";
        if (team == null)
        {
            if (byName.ContainsKey(s.Name)) return 0; // the name is taken by another team (made in the editor)
            team = new Team { Code = s.Code, LeagueId = leagueId };
            Apply(team, s, country);
            db.Teams.Add(team);
            if (s.Code != null) byCode[s.Code] = team;
            byName[team.Name] = team;
            return 1;
        }

        var changed = false;
        if (team.Code == null && s.Code != null) { team.Code = s.Code; byCode[s.Code] = team; changed = true; }
        // a club from the European pool that now has a league (e.g. when a league was added)
        if (team.LeagueId == null && leagueId != null) { team.LeagueId = leagueId; changed = true; }
        if (team.EditedInGame) return changed ? 1 : 0;

        var before = (team.Name, team.ShortName, team.City, team.Stadium, team.PrimaryColor, team.SecondaryColor, team.Attack, team.Midfield, team.Defense, team.Country);
        if (team.Name != s.Name && !byName.ContainsKey(s.Name))
        {
            byName.Remove(team.Name);
            team.Name = s.Name;
            byName[s.Name] = team;
        }
        Apply(team, s, country, keepName: true);
        return changed || before != (team.Name, team.ShortName, team.City, team.Stadium, team.PrimaryColor, team.SecondaryColor, team.Attack, team.Midfield, team.Defense, team.Country) ? 1 : 0;
    }

    private static void Apply(Team t, SeedTeam s, string country, bool keepName = false)
    {
        if (!keepName) t.Name = s.Name;
        t.ShortName = s.Short;
        t.City = s.City;
        t.Stadium = s.Stadium;
        t.PrimaryColor = s.Color1;
        t.SecondaryColor = s.Color2;
        t.Attack = s.Attack;
        t.Midfield = s.Midfield;
        t.Defense = s.Defense;
        if (country != "") t.Country = country;
    }

    /// <summary>One team from seed.json — an object (current format) or a legacy array row.</summary>
    private sealed record SeedTeam(string? Code, string Name, string Short, string City, string Stadium, string Color1, string Color2,
        int Attack, int Midfield, int Defense, string? Country)
    {
        public static SeedTeam From(JsonElement e, string? leagueCountry)
        {
            if (e.ValueKind == JsonValueKind.Array) return FromArray(e.EnumerateArray().ToArray(), leagueCountry);
            string? Str(string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            var name = Str("name") ?? "Unnamed";
            var city = Str("city") ?? "";
            var colors = e.TryGetProperty("colors", out var c) && c.ValueKind == JsonValueKind.Array ? c.EnumerateArray().Select(x => x.GetString() ?? "").ToArray() : [];
            int[] r = e.TryGetProperty("ratings", out var rs) && rs.ValueKind == JsonValueKind.Array
                ? rs.EnumerateArray().Select(x => x.GetInt32()).ToArray()
                : e.TryGetProperty("rating", out var one) && one.ValueKind == JsonValueKind.Number ? [one.GetInt32()] : [60];
            var stadium = Str("stadium");
            return new SeedTeam(Str("code"), name, Str("short") ?? name[..Math.Min(3, name.Length)].ToUpperInvariant(), city,
                string.IsNullOrWhiteSpace(stadium) ? $"{city} Stadium" : stadium,
                colors.ElementAtOrDefault(0) is { Length: > 0 } c1 ? c1 : "#336699", colors.ElementAtOrDefault(1) is { Length: > 0 } c2 ? c2 : "#ffffff",
                r[0], r.Length >= 3 ? r[1] : r[0], r.Length >= 3 ? r[2] : r[0], leagueCountry ?? Str("country"));
        }

        /// <summary>Legacy row: [name, short, city, stadium, color1, color2, rating(, midfield, defense)(, country)].</summary>
        private static SeedTeam FromArray(JsonElement[] row, string? leagueCountry)
        {
            var name = row[0].GetString()!;
            var n = row.Skip(6).Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt32()).ToArray();
            var country = row.Skip(6).FirstOrDefault(x => x.ValueKind == JsonValueKind.String);
            var city = row[2].GetString() ?? "";
            var stadium = row[3].GetString();
            return new SeedTeam(null, name, row[1].GetString() ?? name[..Math.Min(3, name.Length)].ToUpperInvariant(), city,
                string.IsNullOrWhiteSpace(stadium) ? $"{city} Stadium" : stadium, row[4].GetString() ?? "#336699", row[5].GetString() ?? "#ffffff",
                n.ElementAtOrDefault(0), n.Length >= 3 ? n[1] : n.ElementAtOrDefault(0), n.Length >= 3 ? n[2] : n.ElementAtOrDefault(0),
                leagueCountry ?? (country.ValueKind == JsonValueKind.String ? country.GetString() : null));
        }
    }

    private sealed class SeedFile
    {
        public List<SeedLeague> Leagues { get; set; } = new();
        public List<JsonElement> EuropeanClubs { get; set; } = new();
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
        public List<JsonElement> Teams { get; set; } = new();
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
        public string? Description { get; set; }
        public CardKind Kind { get; set; }
        public CardRarity Rarity { get; set; }
        public int Copies { get; set; }
        public int Cost { get; set; }
        public int Price { get; set; }
    }
}
