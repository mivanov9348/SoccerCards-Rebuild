using DugoutDeck.Api.Careers;
using DugoutDeck.Api.Data;
using DugoutDeck.Api.Data.Entities;
using DugoutDeck.Api.Game;
using Microsoft.EntityFrameworkCore;

namespace DugoutDeck.Api.Endpoints;

public record CreateCareerRequest(string SaveName, string ManagerName, int TeamId);
public record RenameCareerRequest(string SaveName);
public record PlayCareerRequest(bool UseLastTactic);
public record NextSeasonRequest(int? AcceptTeamId);
public record CardRequest(string Code);

public static class CareerEndpoints
{
    public static void MapCareerEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/careers");

        api.MapGet("/", async (AppDbContext db) =>
        {
            var careers = await db.Careers.Include(c => c.Team).OrderByDescending(c => c.UpdatedAt).ToListAsync();
            var seasons = (await db.CareerSeasons.ToListAsync()).GroupBy(s => s.CareerId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.Year).First());
            return careers.Select(c =>
            {
                var s = seasons.GetValueOrDefault(c.Id);
                return new
                {
                    c.Id, c.SaveName, c.ManagerName, team = c.Team!.Name, teamColor = c.Team.PrimaryColor,
                    teamColor2 = c.Team.SecondaryColor, status = c.Status.ToString(), c.Reputation, c.Money, c.UpdatedAt,
                    year = s?.Year, week = s?.CurrentWeek, totalWeeks = s?.TotalWeeks, seasonFinished = s?.Finished
                };
            });
        });

        api.MapPost("/", async (CreateCareerRequest req, CareerService careers) =>
        {
            var c = await careers.CreateAsync(req.SaveName, req.ManagerName, req.TeamId);
            return Results.Ok(new { c.Id });
        });

        api.MapGet("/{id:int}", async (int id, AppDbContext db, CareerService careers) =>
        {
            var career = await careers.LoadAsync(id);
            if (career is null) return Results.NotFound();
            var season = await careers.CurrentSeasonAsync(id);
            var team = await db.Teams.FindAsync(season.TeamId);
            var league = await db.Leagues.FindAsync(season.LeagueId);
            var ratings = await careers.RatingsAsync(id);
            var table = await careers.StandingsAsync(season.Id, season.LeagueId);
            var mine = await db.Fixtures.Include(f => f.HomeTeam).Include(f => f.AwayTeam).Include(f => f.Competition)
                .Where(f => f.CareerSeasonId == season.Id && (f.HomeTeamId == season.TeamId || f.AwayTeamId == season.TeamId))
                .OrderBy(f => f.Week).ToListAsync();
            var thisWeek = season.Finished ? null : mine.FirstOrDefault(f => f.Week == season.CurrentWeek && !f.Played);
            var next = mine.FirstOrDefault(f => !f.Played);
            var activeMatch = thisWeek == null ? null
                : await db.Matches.Where(m => m.FixtureId == thisWeek.Id && m.Status == MatchStatus.InProgress).Select(m => (int?)m.Id).FirstOrDefaultAsync();
            var messages = await db.CareerMessages.Where(m => m.CareerId == id).OrderByDescending(m => m.Id).Take(25).ToListAsync();
            var euroName = season.EuropeanCode == null ? null : (await db.Competitions.FirstAsync(c => c.Code == season.EuropeanCode)).Name;
            var cup = await db.Competitions.FirstOrDefaultAsync(c => c.Type == CompetitionType.Cup && c.Country == league!.Country);
            var collectionCount = await db.CareerCards.Where(c => c.CareerId == id).SumAsync(c => c.Count);
            var upgradeLevel = await careers.UpgradeLevelAsync(id, season.TeamId);
            var rating = ratings[season.TeamId];

            return Results.Ok(new
            {
                career.Id, career.SaveName, career.ManagerName, career.Reputation, career.Money,
                status = career.Status.ToString(),
                team = new
                {
                    team!.Id, team.Name, team.City, team.Stadium, team.PrimaryColor, team.SecondaryColor,
                    rating, upgradeLevel,
                    tacticPoints = Rules.TacticPoints(rating, false), handSize = Rules.HandSize(rating), aiLevel = Rules.AiLevelNames[Rules.AiLevel(rating)]
                },
                league = new { league!.Id, league.Name, league.Country, league.Tier },
                cupName = cup?.Name,
                season = new
                {
                    season.Id, season.Year, season.CurrentWeek, season.TotalWeeks, season.Finished,
                    expectation = CareerService.ExpectationText(season.Expectation),
                    european = euroName,
                    summary = CareerService.Summary(season) is { } sum ? new
                    {
                        sum.Position, sum.Points, sum.League, sum.Champion, sum.CupWinner, sum.EuropeResult, sum.Expectation,
                        sum.Verdict, sum.Fired, sum.ReputationChange, sum.MoneyEarned, sum.CardsWon, sum.Promoted, sum.Relegated, sum.Offers
                    } : null
                },
                collectionCount,
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
            if (string.IsNullOrWhiteSpace(req.SaveName)) throw new GameRuleException("Enter a save name.");
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

        // Fixtures of the manager's league, cup and all European cups this season.
        api.MapGet("/{id:int}/fixtures", async (int id, AppDbContext db, CareerService careers) =>
        {
            var season = await careers.CurrentSeasonAsync(id);
            var league = await db.Leagues.FindAsync(season.LeagueId);
            var compIds = await db.Competitions.Where(c =>
                    (c.Type == CompetitionType.League && c.LeagueId == season.LeagueId) ||
                    (c.Type == CompetitionType.Cup && c.Country == league!.Country) ||
                    c.Type == CompetitionType.European)
                .Select(c => c.Id).ToListAsync();
            var fixtures = await careers.FixturesAsync(season.Id, compIds);
            return fixtures.Select(FixtureDto);
        });

        // All leagues of the career and the table of any of them.
        api.MapGet("/{id:int}/leagues", async (int id, AppDbContext db) =>
            await db.Leagues.Where(l => l.IsPlayable).OrderBy(l => l.Country).ThenBy(l => l.Tier)
                .Select(l => new { l.Id, l.Name, l.Country, l.Tier }).ToListAsync());

        api.MapGet("/{id:int}/standings/{leagueId:int}", async (int id, int leagueId, CareerService careers) =>
        {
            var season = await careers.CurrentSeasonAsync(id);
            return await careers.StandingsAsync(season.Id, leagueId);
        });

        api.MapPost("/{id:int}/play", async (int id, PlayCareerRequest req, CareerService careers) =>
        {
            var career = await careers.LoadAsync(id) ?? throw new GameRuleException("Career not found.");
            var match = await careers.StartUserMatchAsync(career, req.UseLastTactic);
            return Results.Ok(new { matchId = match.Id });
        });

        api.MapPost("/{id:int}/simulate", async (int id, CareerService careers) =>
        {
            var career = await careers.LoadAsync(id) ?? throw new GameRuleException("Career not found.");
            await careers.SimulateUserWeekAsync(career);
            return Results.NoContent();
        });

        // Weeks without a match for the manager's team are played straight through.
        api.MapPost("/{id:int}/continue", async (int id, CareerService careers) =>
        {
            var career = await careers.LoadAsync(id) ?? throw new GameRuleException("Career not found.");
            await careers.ContinueAsync(career);
            return Results.NoContent();
        });

        api.MapPost("/{id:int}/next-season", async (int id, NextSeasonRequest req, CareerService careers) =>
        {
            var career = await careers.LoadAsync(id) ?? throw new GameRuleException("Career not found.");
            await careers.NextSeasonAsync(career, req.AcceptTeamId);
            return Results.NoContent();
        });

        // ---------------- collection & store
        api.MapGet("/{id:int}/collection", async (int id, CareerService careers, MatchService matches) =>
        {
            var catalog = await matches.CatalogAsync();
            var owned = await careers.CollectionAsync(id);
            return catalog.ByCode.Values.Where(c => c.Kind is CardKind.Field or CardKind.Hand && c.Rarity != CardRarity.None)
                .OrderBy(c => c.Kind).ThenBy(c => c.Rarity).ThenBy(c => c.Cost)
                .Select(c => new
                {
                    c.Code, c.Name, c.Description, kind = c.Kind.ToString(), rarity = c.Rarity.ToString(), c.Cost, c.Price,
                    sellPrice = c.Price / 3, owned = owned.GetValueOrDefault(c.Code)
                });
        });

        api.MapGet("/{id:int}/store", async (int id, CareerService careers) =>
        {
            var career = await careers.LoadAsync(id) ?? throw new GameRuleException("Career not found.");
            var level = await careers.UpgradeLevelAsync(id, career.TeamId);
            return Results.Ok(new
            {
                money = career.Money, packPrice = CareerService.PackPrice,
                upgradeLevel = level, upgradeCost = level >= 10 ? (long?)null : CareerService.UpgradeCost(level)
            });
        });

        api.MapPost("/{id:int}/store/pack", async (int id, CareerService careers) =>
        {
            var career = await careers.LoadAsync(id) ?? throw new GameRuleException("Career not found.");
            return Results.Ok(new { cards = await careers.BuyPackAsync(career) });
        });

        api.MapPost("/{id:int}/store/buy", async (int id, CardRequest req, CareerService careers) =>
        {
            var career = await careers.LoadAsync(id) ?? throw new GameRuleException("Career not found.");
            await careers.BuyCardAsync(career, req.Code);
            return Results.NoContent();
        });

        api.MapPost("/{id:int}/store/sell", async (int id, CardRequest req, CareerService careers) =>
        {
            var career = await careers.LoadAsync(id) ?? throw new GameRuleException("Career not found.");
            await careers.SellCardAsync(career, req.Code);
            return Results.NoContent();
        });

        api.MapPost("/{id:int}/store/upgrade", async (int id, CareerService careers) =>
        {
            var career = await careers.LoadAsync(id) ?? throw new GameRuleException("Career not found.");
            return Results.Ok(new { level = await careers.UpgradeClubAsync(career) });
        });
    }

    private static object FixtureDto(Fixture f) => new
    {
        f.Id, f.Week, f.Round, f.Stage, f.Played, f.HomeGoals, f.AwayGoals, f.HomePenalties, f.AwayPenalties, f.IsKnockout,
        competition = f.Competition?.Name, competitionCode = f.Competition?.Code, competitionType = f.Competition?.Type.ToString(),
        home = new { id = f.HomeTeamId, name = f.HomeTeam?.Name, primaryColor = f.HomeTeam?.PrimaryColor, secondaryColor = f.HomeTeam?.SecondaryColor },
        away = new { id = f.AwayTeamId, name = f.AwayTeam?.Name, primaryColor = f.AwayTeam?.PrimaryColor, secondaryColor = f.AwayTeam?.SecondaryColor },
        winnerId = f.WinnerTeamId()
    };
}
