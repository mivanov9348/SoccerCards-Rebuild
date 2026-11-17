using FootballCards.Api.Careers;
using FootballCards.Api.Data;
using FootballCards.Api.Game;

namespace FootballCards.Api.Endpoints;

public record CreateFriendlyRequest(int HomeTeamId, int AwayTeamId, Side HumanSide, Tactic Tactic, int Difficulty);

public record PlayRequest(int? CardIndex, int? SwapIndex, int? PenaltyDirection, int Since = 0);

public static class MatchEndpoints
{
    public static void MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/matches");

        api.MapPost("/", async (CreateFriendlyRequest req, AppDbContext db, MatchService matches) =>
        {
            if (req.HomeTeamId == req.AwayTeamId) throw new GameRuleException("Изберете два различни отбора.");
            var home = await db.Teams.FindAsync(req.HomeTeamId) ?? throw new GameRuleException("Няма такъв домакин.");
            var away = await db.Teams.FindAsync(req.AwayTeamId) ?? throw new GameRuleException("Няма такъв гост.");
            var match = await matches.CreateAsync(home, away, req.HumanSide, req.Tactic, Math.Clamp(req.Difficulty, 0, 2),
                false, "Приятелски мач");
            return Results.Ok(MatchService.View(match, MatchService.Load(match)));
        });

        api.MapGet("/{id:int}", async (int id, int? since, AppDbContext db) =>
        {
            var match = await db.Matches.FindAsync(id);
            return match is null ? Results.NotFound() : Results.Ok(MatchService.View(match, MatchService.Load(match), since ?? 0));
        });

        api.MapPost("/{id:int}/play", async (int id, PlayRequest req, AppDbContext db, MatchService matches, CareerService careers) =>
        {
            var match = await db.Matches.FindAsync(id);
            if (match is null) return Results.NotFound();
            var state = MatchService.Load(match);
            matches.Play(state, new PlayerAction(req.CardIndex, req.SwapIndex, req.PenaltyDirection));
            return Results.Ok(await Persist(match, state, req.Since, db, careers));
        });

        api.MapPost("/{id:int}/simulate", async (int id, int? since, AppDbContext db, MatchService matches, CareerService careers) =>
        {
            var match = await db.Matches.FindAsync(id);
            if (match is null) return Results.NotFound();
            var state = MatchService.Load(match);
            if (state.Phase != MatchPhase.Finished) matches.SimulateRest(state);
            return Results.Ok(await Persist(match, state, since ?? 0, db, careers));
        });
    }

    private static async Task<object> Persist(Data.Entities.Match match, MatchState state, int since, AppDbContext db, CareerService careers)
    {
        MatchService.Save(match, state);
        await db.SaveChangesAsync();
        if (state.Phase == MatchPhase.Finished)
            await careers.OnUserMatchFinishedAsync(match, state);
        return MatchService.View(match, state, since);
    }
}
