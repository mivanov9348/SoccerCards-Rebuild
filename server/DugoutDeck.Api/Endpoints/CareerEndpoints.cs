using DugoutDeck.Api.Careers;
using DugoutDeck.Api.Data;
using DugoutDeck.Api.Data.Entities;
using DugoutDeck.Api.Game;
using Microsoft.EntityFrameworkCore;

namespace DugoutDeck.Api.Endpoints;

public record CreateCareerRequest(string SaveName, string ManagerName, int TeamId, Difficulty? Difficulty = null);
public record RenameCareerRequest(string SaveName);
public record PlayCareerRequest(bool UseLastTactic);
public record NextSeasonRequest(int? AcceptTeamId);
public record CardRequest(string Code, int Tier = 0);
public record PackRequest(int Size = 3);
public record DraftRequest(int Pick);
public record TacticPlanRequest(string? Name, TacticSpec? Tactic);
public record ActivePlanRequest(int Slot);

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
            var c = await careers.CreateAsync(req.SaveName, req.ManagerName, req.TeamId, req.Difficulty ?? Difficulty.Normal);
            return Results.Ok(new { c.Id });
        });

        api.MapGet("/{id:int}", async (int id, AppDbContext db, CareerService careers, MatchService matches) =>
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
                hasTactic = TacticPlanSet.Preferred(career) != null,
                draft = await DraftView(career, matches),
                activePlan = TacticPlanSet.Load(career) is var ps && ps.Plans[ps.Active].Tactic != null ? ps.Plans[ps.Active].Name : null,
                status = career.Status.ToString(),
                team = new
                {
                    team!.Id, team.Name, team.City, team.Stadium, team.PrimaryColor, team.SecondaryColor,
                    rating, upgradeLevel,
                    tacticPoints = Rules.TacticPoints(rating, false) + Rules.DifficultyPoints(career.Difficulty), difficulty = career.Difficulty.ToString(), handSize = Rules.HandSize(rating), aiLevel = Rules.AiLevelNames[Rules.AiLevel(rating)]
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

        // ---------------- the draft ("pick 1 of 3") at the start of a career
        api.MapPost("/{id:int}/draft", async (int id, DraftRequest req, CareerService careers, MatchService matches) =>
        {
            var career = await careers.LoadAsync(id) ?? throw new GameRuleException("Career not found.");
            await careers.DraftPickAsync(career, req.Pick);
            return Results.Ok(new { draft = await DraftView(career, matches) });
        });

        // ---------------- collection & market (the cards belong to the club; the market is always open)
        api.MapGet("/{id:int}/collection", async (int id, CareerService careers, MatchService matches) =>
        {
            var catalog = await matches.CatalogAsync();
            var owned = await careers.CollectionAsync(id);
            return catalog.ByCode.Values.Where(c => c.Kind is CardKind.Field or CardKind.Hand && c.Rarity != CardRarity.None)
                .OrderBy(c => c.Kind).ThenBy(c => c.Rarity).ThenBy(c => c.Cost)
                .Select(c => new
                {
                    c.Code, c.Name, c.Description, kind = c.Kind.ToString(), rarity = c.Rarity.ToString(), c.Cost, c.Price,
                    owned = Enumerable.Range(0, 4).Sum(t => owned.GetValueOrDefault((c.Code, t))),
                    tiers = Enumerable.Range(0, 4).Select(t => new
                    {
                        tier = t, name = Tiers.Name(t), count = owned.GetValueOrDefault((c.Code, t)),
                        sellPrice = Tiers.SellPrice(t), bonus = Tiers.Bonus(c.Code, t)
                    })
                });
        });

        api.MapGet("/{id:int}/store", async (int id, CareerService careers) =>
        {
            var career = await careers.LoadAsync(id) ?? throw new GameRuleException("Career not found.");
            var level = await careers.UpgradeLevelAsync(id, career.TeamId);
            return Results.Ok(new
            {
                money = career.Money,
                packs = CareerService.Packs.Select(p => new { size = p.Size, price = p.Price, rarePlus = p.GuaranteedRarePlus }),
                sellPrices = Enumerable.Range(0, 4).Select(t => new { tier = t, name = Tiers.Name(t), price = Tiers.SellPrice(t) }),
                upgradeLevel = level, upgradeCost = level >= 10 ? (long?)null : CareerService.UpgradeCost(level)
            });
        });

        api.MapPost("/{id:int}/store/pack", async (int id, PackRequest req, CareerService careers) =>
        {
            var career = await careers.LoadAsync(id) ?? throw new GameRuleException("Career not found.");
            var cards = await careers.BuyPackAsync(career, req.Size);
            return Results.Ok(new
            {
                cards = cards.Select(c => new
                {
                    c.Card.Code, c.Card.Name, rarity = c.Card.Rarity.ToString(), c.Card.Description,
                    tier = c.Tier, tierName = Tiers.Name(c.Tier), bonus = Tiers.Bonus(c.Card.Code, c.Tier)
                })
            });
        });

        api.MapPost("/{id:int}/store/sell-duplicates", async (int id, CareerService careers) =>
        {
            var career = await careers.LoadAsync(id) ?? throw new GameRuleException("Career not found.");
            var (n, money) = await careers.SellDuplicatesAsync(career);
            return Results.Ok(new { cards = n, money });
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
            var price = await careers.SellCardAsync(career, req.Code, req.Tier);
            return Results.Ok(new { money = price });
        });

        api.MapPost("/{id:int}/store/upgrade", async (int id, CareerService careers) =>
        {
            var career = await careers.LoadAsync(id) ?? throw new GameRuleException("Career not found.");
            return Results.Ok(new { level = await careers.UpgradeClubAsync(career) });
        });

        // ---- tactic plans (the Tactics tab): three plans, one of them is loaded before every match
        api.MapGet("/{id:int}/tactics", async (int id, AppDbContext db, CareerService careers, MatchService matches) =>
        {
            var career = await careers.LoadAsync(id);
            return career is null ? Results.NotFound() : Results.Ok(await PlansView(career, db, careers, matches));
        });

        api.MapPut("/{id:int}/tactics/{slot:int}", async (int id, int slot, TacticPlanRequest req, AppDbContext db, CareerService careers, MatchService matches) =>
        {
            var career = await careers.LoadAsync(id);
            if (career is null) return Results.NotFound();
            if (slot < 0 || slot >= TacticPlanSet.Slots) throw new GameRuleException("Unknown plan.");
            var set = TacticPlanSet.Load(career);
            var plan = set.Plans[slot];
            if (req.Name != null)
            {
                var name = req.Name.Trim();
                if (name.Length == 0) name = TacticPlanSet.DefaultNames[slot];
                plan.Name = name.Length > 24 ? name[..24] : name;
            }
            if (req.Tactic != null)
            {
                var season = await careers.CurrentSeasonAsync(id);
                var rating = (await careers.RatingsAsync(id))[season.TeamId];
                var catalog = await matches.CatalogAsync();
                var collection = await careers.CollectionAsync(id);
                // checked against the home budget (one point more); away the pre-match screen asks to drop a card if needed
                var budget = Rules.TacticPoints(rating, true) + Rules.DifficultyPoints(career.Difficulty);
                var empty = req.Tactic.Field.Count == 0 && req.Tactic.Hand.Count == 0 && req.Tactic.Modifiers.Count == 0;
                if (!empty)
                {
                    var errors = TacticRules.Validate(req.Tactic, catalog, budget, Rules.HandSize(rating), CareerService.Owned(collection));
                    if (errors.Count > 0) throw new GameRuleException(string.Join(" ", errors));
                }
                plan.Tactic = empty ? null : req.Tactic;
            }
            set.Save(career);
            await db.SaveChangesAsync();
            return Results.Ok(await PlansView(career, db, careers, matches));
        });

        api.MapPost("/{id:int}/tactics/active", async (int id, ActivePlanRequest req, AppDbContext db, CareerService careers, MatchService matches) =>
        {
            var career = await careers.LoadAsync(id);
            if (career is null) return Results.NotFound();
            var set = TacticPlanSet.Load(career);
            set.Active = Math.Clamp(req.Slot, 0, TacticPlanSet.Slots - 1);
            set.Save(career);
            await db.SaveChangesAsync();
            return Results.Ok(await PlansView(career, db, careers, matches));
        });
    }

    /// <summary>The Tactics tab: three plans, the active one, and everything the tactic editor needs.</summary>
    private static async Task<object> PlansView(Career career, AppDbContext db, CareerService careers, MatchService matches)
    {
        var season = await careers.CurrentSeasonAsync(career.Id);
        var rating = (await careers.RatingsAsync(career.Id))[season.TeamId];
        var catalog = await matches.CatalogAsync();
        var collection = await careers.CollectionAsync(career.Id);
        var extra = Rules.DifficultyPoints(career.Difficulty);
        var away = Rules.TacticPoints(rating, false) + extra;
        var handSize = Rules.HandSize(rating);
        var set = TacticPlanSet.Load(career);
        return new
        {
            active = set.Active,
            plans = set.Plans.Select(p => new
            {
                name = p.Name, tactic = p.Tactic,
                cost = p.Tactic == null ? 0 : TacticRules.Cost(p.Tactic, catalog),
                // a plan that is fine at home may cost one point too many away
                awayErrors = p.Tactic == null ? new List<string>() : TacticRules.Validate(p.Tactic, catalog, away, handSize, CareerService.Owned(collection))
            }),
            setup = new
            {
                budget = away + 1, awayBudget = away, handSize, maxModifiers = Rules.MaxModifiers, fieldCards = Rules.FieldCardsPerTeam,
                lastTactic = new TacticSpec(),
                cards = MatchEndpoints.SetupCards(catalog, collection)
            }
        };
    }

    /// <summary>The running draft (null when there is none): round, the three cards to choose from and the picks so far.</summary>
    private static async Task<object?> DraftView(Career career, MatchService matches)
    {
        var draft = CareerService.Draft(career);
        if (draft == null) return null;
        var catalog = await matches.CatalogAsync();
        object Card(DraftOption o) => new
        {
            code = o.Code, name = catalog.Get(o.Code)?.Name ?? o.Code, description = catalog.Get(o.Code)?.Description,
            kind = catalog.Get(o.Code)?.Kind.ToString(), cost = catalog.Cost(o.Code),
            tier = o.Tier, tierName = Tiers.Name(o.Tier), bonus = Tiers.Bonus(o.Code, o.Tier)
        };
        return new
        {
            round = draft.Round, total = draft.Total,
            kind = draft.Round <= CareerService.DraftFieldRounds ? "Field" : "Hand",
            options = draft.Options.Select(Card), picked = draft.Picked.Select(Card)
        };
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
