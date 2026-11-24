using System.Text.Json;
using DugoutDeck.Api.Careers;
using DugoutDeck.Api.Data;
using DugoutDeck.Api.Data.Entities;
using DugoutDeck.Api.Game;
using Microsoft.EntityFrameworkCore;

namespace DugoutDeck.Api.Endpoints;

public record CreateFriendlyRequest(int HomeTeamId, int AwayTeamId, Side HumanSide, Difficulty? Difficulty = null);
public record StartRequest(TacticSpec Tactic, string? SaveAs);
public record FlipRequest(int Cell);
public record HandRequest(string Card, int? Cell, int? Cell2);
public record DecideRequest(int? Choice, double? Timing, bool Auto);
public record SaveTacticRequest(int? CareerId, string Name, TacticSpec Tactic);

public static class MatchEndpoints
{
    public static void MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/matches");

        api.MapPost("/", async (CreateFriendlyRequest req, AppDbContext db, MatchService matches) =>
        {
            if (req.HomeTeamId == req.AwayTeamId) throw new GameRuleException("Pick two different teams.");
            var home = await db.Teams.FindAsync(req.HomeTeamId) ?? throw new GameRuleException("Home team not found.");
            var away = await db.Teams.FindAsync(req.AwayTeamId) ?? throw new GameRuleException("Away team not found.");
            var match = await matches.CreateAsync(home, away, home.Overall, away.Overall, req.HumanSide, false, "Friendly",
                difficulty: req.Difficulty ?? Difficulty.Normal);
            return Results.Ok(await ViewAsync(match, MatchService.Load(match), db, matches));
        });

        api.MapGet("/{id:int}", async (int id, AppDbContext db, MatchService matches) =>
        {
            var match = await db.Matches.FindAsync(id);
            return match is null ? Results.NotFound() : Results.Ok(await ViewAsync(match, MatchService.Load(match), db, matches));
        });

        // Apply the player's tactic; the computer builds its own; the grids are dealt and the match kicks off.
        api.MapPost("/{id:int}/start", async (int id, StartRequest req, AppDbContext db, MatchService matches, CareerService careers) =>
        {
            var match = await db.Matches.FindAsync(id);
            if (match is null) return Results.NotFound();
            var state = MatchService.Load(match);
            await StartAsync(match, state, req.Tactic, db, matches, careers);
            if (!string.IsNullOrWhiteSpace(req.SaveAs))
                db.SavedTactics.Add(new SavedTactic { CareerId = match.CareerId, Name = req.SaveAs.Trim(), Json = JsonSerializer.Serialize(req.Tactic) });
            MatchService.Save(match, state);
            await db.SaveChangesAsync();
            return Results.Ok(await ViewAsync(match, state, db, matches));
        });

        api.MapPost("/{id:int}/flip", (int id, FlipRequest req, AppDbContext db, MatchService m, CareerService c) =>
            Act(id, db, m, c, (e, s) => e.FlipHuman(s, req.Cell)));

        api.MapPost("/{id:int}/hand", (int id, HandRequest req, AppDbContext db, MatchService m, CareerService c) =>
            Act(id, db, m, c, (e, s) => e.PlayHandHuman(s, req.Card, req.Cell, req.Cell2)));

        api.MapPost("/{id:int}/decide", (int id, DecideRequest req, AppDbContext db, MatchService m, CareerService c) =>
            Act(id, db, m, c, (e, s) => e.DecideHuman(s, req.Choice, req.Timing, req.Auto)));

        // The computer plays one step (the client calls this with a short delay to animate the computer's turn).
        api.MapPost("/{id:int}/ai-step", (int id, AppDbContext db, MatchService m, CareerService c) =>
            Act(id, db, m, c, (e, s) => e.AiStep(s)));

        api.MapPost("/{id:int}/simulate", (int id, AppDbContext db, MatchService m, CareerService c) =>
            Act(id, db, m, c, (e, s) => { if (s.Phase != MatchPhase.Finished) e.SimulateToEnd(s); }));

        // ---------------- saved tactics
        var tactics = app.MapGroup("/api/tactics");
        tactics.MapGet("/", async (int? careerId, AppDbContext db) =>
            (await db.SavedTactics.Where(t => t.CareerId == careerId).OrderBy(t => t.Name).ToListAsync())
            .Select(t => new { t.Id, t.Name, tactic = JsonSerializer.Deserialize<TacticSpec>(t.Json) }));
        tactics.MapPost("/", async (SaveTacticRequest req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name)) throw new GameRuleException("Enter a name for the tactic.");
            var t = new SavedTactic { CareerId = req.CareerId, Name = req.Name.Trim(), Json = JsonSerializer.Serialize(req.Tactic) };
            db.SavedTactics.Add(t);
            await db.SaveChangesAsync();
            return Results.Ok(new { t.Id });
        });
        tactics.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var t = await db.SavedTactics.FindAsync(id);
            if (t is null) return Results.NotFound();
            db.SavedTactics.Remove(t);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    /// <summary>Validates the human tactic (against the career collection, if any) and starts the match.</summary>
    public static async Task StartAsync(Match match, MatchState state, TacticSpec tactic, AppDbContext db, MatchService matches, CareerService careers)
    {
        var catalog = await matches.CatalogAsync();
        var (budget, handSize) = MatchService.HumanLimits(state);
        Func<string, int, int>? owned = null;
        if (match.CareerId is { } careerId) owned = CareerService.Owned(await careers.CollectionAsync(careerId));
        var errors = TacticRules.Validate(tactic, catalog, budget, handSize, owned);
        if (errors.Count > 0) throw new GameRuleException(string.Join(" ", errors));

        var engine = await matches.EngineAsync();
        var human = state.HumanSide ?? Side.Home;
        engine.Start(state, human == Side.Home ? tactic : null, human == Side.Away ? tactic : null);

        if (match.CareerId is { } cid && await db.Careers.FindAsync(cid) is { } career)
            career.LastTacticJson = JsonSerializer.Serialize(tactic);
    }

    private static async Task<IResult> Act(int id, AppDbContext db, MatchService matches, CareerService careers, Action<MatchEngine, MatchState> action)
    {
        var match = await db.Matches.FindAsync(id);
        if (match is null) return Results.NotFound();
        var state = MatchService.Load(match);
        if (state.Phase == MatchPhase.Setup) throw new GameRuleException("Choose your tactic first.");
        action(await matches.EngineAsync(), state);
        MatchService.Save(match, state);
        await db.SaveChangesAsync();
        if (state.Phase == MatchPhase.Finished)
            await careers.OnUserMatchFinishedAsync(match, state);
        return Results.Ok(await ViewAsync(match, state, db, matches));
    }

    /// <summary>The match view plus, during Setup, everything the tactic screen needs.</summary>
    public static async Task<object> ViewAsync(Match match, MatchState state, AppDbContext db, MatchService matches)
    {
        var catalog = await matches.CatalogAsync();
        object? setup = null;
        if (state.Phase == MatchPhase.Setup)
        {
            var (budget, handSize) = MatchService.HumanLimits(state);
            Dictionary<(string Code, int Tier), int>? collection = null;
            TacticSpec? last = null;
            object? plans = null;
            if (match.CareerId is { } careerId)
            {
                collection = (await db.CareerCards.Where(c => c.CareerId == careerId && c.Count > 0).ToListAsync()).ToDictionary(c => (c.Code, c.Tier), c => c.Count);
                if (await db.Careers.FindAsync(careerId) is { } career)
                {
                    // the active plan of the Tactics tab (or the last tactic played) is loaded; the other plans are one click away
                    last = TacticPlanSet.Preferred(career);
                    var set = TacticPlanSet.Load(career);
                    plans = new { active = set.Active, list = set.Plans.Select(p => new { name = p.Name, tactic = p.Tactic }) };
                }
            }
            setup = new
            {
                budget, handSize, maxModifiers = Rules.MaxModifiers, fieldCards = Rules.FieldCardsPerTeam,
                // a career's first match starts with an empty tactic; friendlies start with a sensible default
                lastTactic = last ?? (match.CareerId != null ? new TacticSpec() : TacticRules.Default()),
                plans,
                cards = SetupCards(catalog, collection)
            };
        }
        return MatchService.View(match, state, catalog, setup);
    }

    /// <summary>The cards of the tactic editor, with how many the career owns (null = unlimited).</summary>
    /// <summary>The cards of the tactic editor, with how many copies the career owns in each tier (null = unlimited, all Normal).</summary>
    public static object SetupCards(CardCatalog catalog, Dictionary<(string Code, int Tier), int>? collection) =>
        catalog.ByCode.Values.Where(c => c.Kind != CardKind.Neutral).OrderBy(c => c.Kind).ThenBy(c => c.Cost).Select(c =>
        {
            var unlimited = collection == null || c.Code == Cards.SquadPlayer || c.Kind == CardKind.Modifier;
            return new
            {
                c.Code, c.Name, c.Description, kind = c.Kind.ToString(), rarity = c.Rarity.ToString(), c.Cost,
                owned = unlimited ? (int?)null : Enumerable.Range(0, 4).Sum(t => collection!.GetValueOrDefault((c.Code, t))),
                // copies per tier: [Normal, Rare, Ultra Rare, Legendary]
                tiers = unlimited ? null : Enumerable.Range(0, 4).Select(t => collection!.GetValueOrDefault((c.Code, t))).ToArray(),
                bonuses = Enumerable.Range(0, 4).Select(t => Tiers.Bonus(c.Code, t)).ToArray()
            };
        }).ToList();
}
