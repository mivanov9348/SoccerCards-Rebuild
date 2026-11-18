using System.Text.RegularExpressions;
using FootballCards.Api.Data;
using FootballCards.Api.Data.Entities;
using FootballCards.Api.Game;
using Microsoft.EntityFrameworkCore;

namespace FootballCards.Api.Endpoints;

public record LeagueInput(string Name, string Country, int Tier, bool IsPlayable);

public record TeamInput(string Name, string? ShortName, string City, string Country, string Stadium,
    string PrimaryColor, string SecondaryColor, int Attack, int Midfield, int Defense, int? LeagueId);

/// <summary>Лиги, отбори, карти и турнири — включително добавяне/редакция (за редактора).</summary>
public static partial class DataEndpoints
{
    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();

    public static void MapDataEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/leagues", async (AppDbContext db) =>
            await db.Leagues.OrderBy(l => l.Country).ThenBy(l => l.Tier).ThenBy(l => l.Name)
                .Select(l => new { l.Id, l.Name, l.Country, l.Tier, l.IsPlayable, teamCount = l.Teams.Count })
                .ToListAsync());

        api.MapPost("/leagues", async (LeagueInput input, AppDbContext db) =>
        {
            ValidateLeague(input);
            if (await db.Leagues.AnyAsync(l => l.Name == input.Name.Trim()))
                throw new GameRuleException("A league with this name already exists.");
            var league = new League { Name = input.Name.Trim(), Country = input.Country.Trim(), Tier = input.Tier, IsPlayable = input.IsPlayable };
            db.Leagues.Add(league);
            await db.SaveChangesAsync();
            return Results.Created($"/api/leagues/{league.Id}", new { league.Id });
        });

        api.MapPut("/leagues/{id:int}", async (int id, LeagueInput input, AppDbContext db) =>
        {
            ValidateLeague(input);
            var league = await db.Leagues.FindAsync(id);
            if (league is null) return Results.NotFound();
            league.Name = input.Name.Trim();
            league.Country = input.Country.Trim();
            league.Tier = input.Tier;
            league.IsPlayable = input.IsPlayable;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        api.MapDelete("/leagues/{id:int}", async (int id, AppDbContext db) =>
        {
            var league = await db.Leagues.Include(l => l.Teams).FirstOrDefaultAsync(l => l.Id == id);
            if (league is null) return Results.NotFound();
            if (league.Teams.Count > 0) throw new GameRuleException("Move or delete the league's teams first.");
            if (await db.Careers.AnyAsync(c => c.LeagueId == id)) throw new GameRuleException("The league is used by a saved career.");
            db.Leagues.Remove(league);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        api.MapGet("/teams", async (int? leagueId, string? country, AppDbContext db) =>
        {
            var q = db.Teams.Include(t => t.League).AsQueryable();
            if (leagueId.HasValue) q = q.Where(t => t.LeagueId == leagueId);
            if (!string.IsNullOrWhiteSpace(country)) q = q.Where(t => t.Country == country);
            var teams = await q.ToListAsync();
            return teams.OrderBy(t => t.League == null).ThenBy(t => t.League?.Name ?? "")
                .ThenByDescending(t => t.Overall).ThenBy(t => t.Name).Select(ToDto);
        });

        api.MapGet("/teams/{id:int}", async (int id, AppDbContext db) =>
            await db.Teams.Include(t => t.League).FirstOrDefaultAsync(t => t.Id == id) is { } t
                ? Results.Ok(ToDto(t)) : Results.NotFound());

        api.MapPost("/teams", async (TeamInput input, AppDbContext db) =>
        {
            await ValidateTeam(input, db, null);
            var team = new Team();
            Apply(team, input);
            db.Teams.Add(team);
            await db.SaveChangesAsync();
            return Results.Created($"/api/teams/{team.Id}", new { team.Id });
        });

        api.MapPut("/teams/{id:int}", async (int id, TeamInput input, AppDbContext db) =>
        {
            var team = await db.Teams.FindAsync(id);
            if (team is null) return Results.NotFound();
            await ValidateTeam(input, db, id);
            Apply(team, input);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        api.MapDelete("/teams/{id:int}", async (int id, AppDbContext db) =>
        {
            var team = await db.Teams.FindAsync(id);
            if (team is null) return Results.NotFound();
            var used = await db.Fixtures.AnyAsync(f => f.HomeTeamId == id || f.AwayTeamId == id)
                       || await db.Careers.AnyAsync(c => c.TeamId == id);
            if (used) throw new GameRuleException("The team is part of a saved career and cannot be deleted.");
            db.Matches.RemoveRange(db.Matches.Where(m => m.HomeTeamId == id || m.AwayTeamId == id));
            db.Teams.Remove(team);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        api.MapGet("/cards", async (AppDbContext db) =>
            await db.Cards.OrderBy(c => c.Id)
                .Select(c => new { c.Code, c.Name, c.Description, c.CopiesInDeck }).ToListAsync());

        api.MapGet("/competitions", async (AppDbContext db) =>
            await db.Competitions.OrderBy(c => c.Type).ThenBy(c => c.Tier)
                .Select(c => new { c.Id, c.Code, c.Name, type = c.Type.ToString(), c.Tier }).ToListAsync());
    }

    private static object ToDto(Team t) => new
    {
        t.Id, t.Name, t.ShortName, t.City, t.Country, t.Stadium, t.PrimaryColor, t.SecondaryColor,
        t.Attack, t.Midfield, t.Defense, t.Overall, t.LeagueId, leagueName = t.League?.Name
    };

    private static void ValidateLeague(LeagueInput i)
    {
        if (string.IsNullOrWhiteSpace(i.Name)) throw new GameRuleException("League name is required.");
        if (string.IsNullOrWhiteSpace(i.Country)) throw new GameRuleException("Country is required.");
        if (i.Tier is < 1 or > 10) throw new GameRuleException("Tier must be between 1 and 10.");
    }

    private static async Task ValidateTeam(TeamInput i, AppDbContext db, int? id)
    {
        if (string.IsNullOrWhiteSpace(i.Name)) throw new GameRuleException("Team name is required.");
        if (await db.Teams.AnyAsync(t => t.Name == i.Name.Trim() && t.Id != id))
            throw new GameRuleException("A team with this name already exists.");
        foreach (var (v, label) in new[] { (i.Attack, "Attack"), (i.Midfield, "Midfield"), (i.Defense, "Defense") })
            if (v is < 1 or > 99) throw new GameRuleException($"{label}: rating must be between 1 and 99.");
        if (!HexColor().IsMatch(i.PrimaryColor ?? "") || !HexColor().IsMatch(i.SecondaryColor ?? ""))
            throw new GameRuleException("Colors must be in #RRGGBB format.");
        if (i.LeagueId.HasValue && !await db.Leagues.AnyAsync(l => l.Id == i.LeagueId))
            throw new GameRuleException("League not found.");
    }

    private static void Apply(Team t, TeamInput i)
    {
        t.Name = i.Name.Trim();
        t.ShortName = string.IsNullOrWhiteSpace(i.ShortName) ? t.Name[..Math.Min(3, t.Name.Length)].ToUpper() : i.ShortName.Trim();
        t.City = i.City?.Trim() ?? "";
        t.Country = i.Country?.Trim() ?? "";
        t.Stadium = i.Stadium?.Trim() ?? "";
        t.PrimaryColor = i.PrimaryColor;
        t.SecondaryColor = i.SecondaryColor;
        t.Attack = i.Attack;
        t.Midfield = i.Midfield;
        t.Defense = i.Defense;
        t.LeagueId = i.LeagueId;
    }
}
