using System.Text.Json;
using System.Text.Json.Serialization;
using FootballCards.Api.Data;
using FootballCards.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FootballCards.Api.Game;

/// <summary>Creates, loads and saves matches. Builds the view the player sees (face-down cards stay hidden).</summary>
public class MatchService(AppDbContext db)
{
    public static readonly JsonSerializerOptions StateJson = new() { Converters = { new JsonStringEnumConverter() } };

    private readonly MatchEngine _engine = new(Random.Shared);

    public async Task<List<CardDefinition>> DeckAsync() => await db.Cards.AsNoTracking().ToListAsync();

    public async Task<Match> CreateAsync(Team home, Team away, Side? humanSide, bool knockout, string competition,
        int? fixtureId = null, int? careerId = null)
    {
        var state = _engine.Create(new MatchSetup(home, away, humanSide, knockout, competition), await DeckAsync());
        var match = new Match
        {
            HomeTeamId = home.Id, AwayTeamId = away.Id, FixtureId = fixtureId, CareerId = careerId,
            StateJson = JsonSerializer.Serialize(state, StateJson)
        };
        db.Matches.Add(match);
        await db.SaveChangesAsync();
        return match;
    }

    /// <summary>Quick computer-vs-computer match (career fixtures you do not play).</summary>
    public MatchState SimulateQuick(Team home, Team away, bool knockout, List<CardDefinition> deck)
    {
        var state = _engine.Create(new MatchSetup(home, away, null, knockout, ""), deck);
        _engine.SimulateToEnd(state);
        return state;
    }

    public static MatchState Load(Match m) => JsonSerializer.Deserialize<MatchState>(m.StateJson, StateJson)!;

    public static void Save(Match m, MatchState s)
    {
        m.StateJson = JsonSerializer.Serialize(s, StateJson);
        m.Status = s.Phase == MatchPhase.Finished ? MatchStatus.Finished : MatchStatus.InProgress;
        m.UpdatedAt = DateTime.UtcNow;
    }

    public void Flip(MatchState s, int cell) => _engine.FlipHuman(s, cell);
    public void AiFlip(MatchState s) => _engine.AiFlip(s);
    public void Penalty(MatchState s, int direction) => _engine.PenaltyHuman(s, direction);
    public void SimulateRest(MatchState s) => _engine.SimulateToEnd(s);

    // ------------------------------------------------------------- view model

    public static object View(Match m, MatchState s)
    {
        var human = s.HumanSide;
        string? humanRole = null;
        if (human is { } h)
        {
            humanRole = s.Phase switch
            {
                MatchPhase.Play => s.Turn == h ? "flip" : "wait",
                MatchPhase.Penalty => s.PenaltyShooter == h ? "shooter" : "keeper",
                MatchPhase.Shootout => s.NextShootoutShooter == h ? "shooter" : "keeper",
                _ => null
            };
        }

        return new
        {
            id = m.Id,
            fixtureId = m.FixtureId,
            careerId = m.CareerId,
            competition = s.CompetitionName,
            stadium = s.Stadium,
            isKnockout = s.IsKnockout,
            home = TeamView(s.Home),
            away = TeamView(s.Away),
            half = s.Half,
            phase = s.Phase.ToString(),
            turn = s.Turn.ToString(),
            humanSide = human?.ToString(),
            humanRole,
            penaltyShooter = s.Phase == MatchPhase.Penalty ? s.PenaltyShooter?.ToString()
                : s.Phase == MatchPhase.Shootout ? s.NextShootoutShooter.ToString() : null,
            table = s.Table.Select((c, i) => new
            {
                index = i,
                faceUp = c.FaceUp,
                // face-down cards stay secret until the match is over
                card = c.FaceUp || s.Phase == MatchPhase.Finished ? c.Card : null,
                name = c.FaceUp || s.Phase == MatchPhase.Finished ? Cards.Name(c.Card) : null,
                flippedBy = c.FlippedBy?.ToString()
            }),
            cardsLeft = s.Table.Count(c => !c.FaceUp),
            lastFlip = s.LastFlip == null ? null : new
            {
                cell = s.LastFlip.Cell, card = s.LastFlip.Card, name = Cards.Name(s.LastFlip.Card), side = s.LastFlip.Side.ToString()
            },
            shootout = s.Shootout.Select(k => new { side = k.Side.ToString(), scored = k.Scored }),
            homePenalties = s.HomePenalties,
            awayPenalties = s.AwayPenalties,
            events = s.Events.Select(e => new
            {
                e.Id, e.Half, e.Type, side = e.Side?.ToString(), e.Text, e.Card
            }),
        };
    }

    private static object TeamView(TeamMatchState t) => new
    {
        id = t.TeamId, name = t.Name, shortName = t.ShortName, primaryColor = t.PrimaryColor,
        secondaryColor = t.SecondaryColor, goals = t.Goals, isHuman = t.IsHuman, stats = t.Stats
    };
}
