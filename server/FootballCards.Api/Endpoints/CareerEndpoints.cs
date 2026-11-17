using FootballCards.Api.Careers;
using FootballCards.Api.Data;
using FootballCards.Api.Data.Entities;
using FootballCards.Api.Game;
using Microsoft.EntityFrameworkCore;

namespace FootballCards.Api.Endpoints;

public record CreateCareerRequest(string SaveName, string ManagerName, int TeamId, int Difficulty);
public record RenameCareerRequest(string SaveName);
public record PlayCareerRequest(Tactic Tactic);
public record NextSeasonRequest(int? AcceptTeamId);

public static class CareerEndpoints
{
    public static void MapCareerEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/careers");

        // Списък със записи (слотове)
        api.MapGet("/", async (AppDbContext db) =>
        {
            var careers = await db.Careers.Include(c => c.Team).OrderByDescending(c => c.UpdatedAt).ToListAsync();
            var seasons = await db.CareerSeasons.GroupBy(s => s.CareerId)
                .Select(g => g.OrderByDescending(s => s.Year).First()).ToListAsync();
            return careers.Select(c =>
            {
                var s = seasons.FirstOrDefault(x => x.CareerId == c.Id);
                return new
                {
                    c.Id, c.SaveName, c.ManagerName, team = c.Team!.Name, teamColor = c.Team.PrimaryColor,
                    teamColor2 = c.Team.SecondaryColor, status = c.Status.ToString(), c.Reputation, c.UpdatedAt,
                    year = s?.Year, week = s?.CurrentWeek, totalWeeks = s?.TotalWeeks, seasonFinished = s?.Finished
                };
            });
        });

        api.MapPost("/", async (CreateCareerRequest req, CareerService careers) =>
        {
            var c = await careers.CreateAsync(req.SaveName, req.ManagerName, req.TeamId, req.Difficulty);
            return Results.Ok(new { c.Id });
        });

        api.MapGet("/{id:int}", async (int id, AppDbContext db, CareerService careers) =>
        {
            var career = await careers.LoadAsync(id);
            if (career is null) return Results.NotFound();
            var season = await careers.CurrentSeasonAsync(id);
            var seasonTeam = await db.Teams.FindAsync(season.TeamId);
            var table = await careers.StandingsAsync(season.Id, career.LeagueId);
            var fixtures = await careers.FixturesAsync(season.Id);
            var mine = fixtures.Where(f => f.HomeTeamId == season.TeamId || f.AwayTeamId == season.TeamId).ToList();
            var thisWeek = season.Finished ? null : mine.FirstOrDefault(f => f.Week == season.CurrentWeek && !f.Played);
            var next = mine.FirstOrDefault(f => !f.Played);
            var activeMatch = thisWeek == null ? null
                : await db.Matches.Where(m => m.FixtureId == thisWeek.Id && m.Status == MatchStatus.InProgress)
                    .Select(m => (int?)m.Id).FirstOrDefaultAsync();
            var messages = await db.CareerMessages.Where(m => m.CareerId == id)
                .OrderByDescending(m => m.Id).Take(12).ToListAsync();
            var euroName = season.EuropeanCode == null ? null
                : (await db.Competitions.FirstAsync(c => c.Code == season.EuropeanCode)).Name;
            var weekComp = fixtures.FirstOrDefault(f => f.Week == season.CurrentWeek)?.Competition?.Name;

            return Results.Ok(new
            {
                career.Id, career.SaveName, career.ManagerName, career.Reputation, career.Difficulty,
                status = career.Status.ToString(),
                team = new
                {
                    seasonTeam!.Id, seasonTeam.Name, seasonTeam.City, seasonTeam.Stadium, seasonTeam.PrimaryColor,
                    seasonTeam.SecondaryColor, seasonTeam.Attack, seasonTeam.Midfield, seasonTeam.Defense, seasonTeam.Overall
                },
                season = new
                {
                    season.Id, season.Year, season.CurrentWeek, season.TotalWeeks, season.Finished,
                    expectation = CareerService.ExpectationText(season.Expectation),
                    european = euroName,
                    weekCompetition = weekComp,
                    summary = CareerService.Summary(season)
                },
                thisWeekFixture = thisWeek == null ? null : FixtureDto(thisWeek),
                nextFixture = next == null ? null : FixtureDto(next),
                activeMatchId = activeMatch,
                position = table.FirstOrDefault(r => r.TeamId == season.TeamId && r.Played > 0)?.Position,
                table,
                recent = mine.Where(f => f.Played).TakeLast(5).Select(FixtureDto),
                messages = messages.Select(m => new { m.Id, m.Year, m.Week, m.Title, m.Body })
            });
        });

        api.MapPut("/{id:int}", async (int id, RenameCareerRequest req, AppDbContext db) =>
        {
            var career = await db.Careers.FindAsync(id);
            if (career is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(req.SaveName)) throw new GameRuleException("Въведете име на записа.");
            career.SaveName = req.SaveName.Trim();
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        api.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var career = await db.Careers.FindAsync(id);
            if (career is null) return Results.NotFound();
            db.Matches.RemoveRange(db.Matches.Where(m => m.CareerId == id));
            db.Careers.Remove(career);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        api.MapGet("/{id:int}/fixtures", async (int id, CareerService careers) =>
        {
            var season = await careers.CurrentSeasonAsync(id);
            var fixtures = await careers.FixturesAsync(season.Id);
            return fixtures.Select(FixtureDto);
        });

        api.MapPost("/{id:int}/play", async (int id, PlayCareerRequest req, CareerService careers) =>
        {
            var career = await careers.LoadAsync(id) ?? throw new GameRuleException("Няма такава кариера.");
            var match = await careers.StartUserMatchAsync(career, req.Tactic);
            return Results.Ok(new { matchId = match.Id });
        });

        api.MapPost("/{id:int}/simulate", async (int id, CareerService careers) =>
        {
            var career = await careers.LoadAsync(id) ?? throw new GameRuleException("Няма такава кариера.");
            await careers.SimulateUserWeekAsync(career);
            return Results.NoContent();
        });

        api.MapPost("/{id:int}/next-season", async (int id, NextSeasonRequest req, CareerService careers) =>
        {
            var career = await careers.LoadAsync(id) ?? throw new GameRuleException("Няма такава кариера.");
            await careers.NextSeasonAsync(career, req.AcceptTeamId);
            return Results.NoContent();
        });
    }

    private static object FixtureDto(Fixture f) => new
    {
        f.Id, f.Week, f.Round, f.Stage, f.Played, f.HomeGoals, f.AwayGoals, f.HomePenalties, f.AwayPenalties, f.IsKnockout,
        competition = f.Competition?.Name, competitionCode = f.Competition?.Code,
        home = new { id = f.HomeTeamId, name = f.HomeTeam?.Name, primaryColor = f.HomeTeam?.PrimaryColor, secondaryColor = f.HomeTeam?.SecondaryColor },
        away = new { id = f.AwayTeamId, name = f.AwayTeam?.Name, primaryColor = f.AwayTeam?.PrimaryColor, secondaryColor = f.AwayTeam?.SecondaryColor },
        winnerId = f.WinnerTeamId()
    };
}
