using FootballCards.Api.Careers;
using FootballCards.Api.Data;
using FootballCards.Api.Game;

namespace FootballCards.Api.Endpoints;

public record CreateFriendlyRequest(int HomeTeamId, int AwayTeamId, Side HumanSide);
public record FlipRequest(int Cell);
public record PenaltyRequest(int Direction);

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
            var match = await matches.CreateAsync(home, away, req.HumanSide, false, "Friendly");
            return Results.Ok(MatchService.View(match, MatchService.Load(match)));
        });

        api.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var match = await db.Matches.FindAsync(id);
            return match is null ? Results.NotFound() : Results.Ok(MatchService.View(match, MatchService.Load(match)));
        });

        // The human flips a face-down card.
        api.MapPost("/{id:int}/flip", (int id, FlipRequest req, AppDbContext db, MatchService m, CareerService c) =>
            Act(id, db, c, s => m.Flip(s, req.Cell)));

        // The computer flips one card (the client calls this with a short delay to animate the computer's turn).
        api.MapPost("/{id:int}/ai-flip", (int id, AppDbContext db, MatchService m, CareerService c) =>
            Act(id, db, c, m.AiFlip));

        // The human picks a side for a penalty (as shooter or keeper).
        api.MapPost("/{id:int}/penalty", (int id, PenaltyRequest req, AppDbContext db, MatchService m, CareerService c) =>
            Act(id, db, c, s => m.Penalty(s, req.Direction)));

        api.MapPost("/{id:int}/simulate", (int id, AppDbContext db, MatchService m, CareerService c) =>
            Act(id, db, c, s => { if (s.Phase != MatchPhase.Finished) m.SimulateRest(s); }));
    }

    private static async Task<IResult> Act(int id, AppDbContext db, CareerService careers, Action<MatchState> action)
    {
        var match = await db.Matches.FindAsync(id);
        if (match is null) return Results.NotFound();
        var state = MatchService.Load(match);
        action(state);
        MatchService.Save(match, state);
        await db.SaveChangesAsync();
        if (state.Phase == MatchPhase.Finished)
            await careers.OnUserMatchFinishedAsync(match, state);
        return Results.Ok(MatchService.View(match, state));
    }
}
