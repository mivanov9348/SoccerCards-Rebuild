using System.Text.Json;
using System.Text.Json.Serialization;
using DugoutDeck.Api.Data;
using DugoutDeck.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DugoutDeck.Api.Game;

/// <summary>Creates, loads and saves matches and builds what the player sees (face-down cards stay secret).</summary>
public class MatchService(AppDbContext db)
{
    public static readonly JsonSerializerOptions StateJson = new() { Converters = { new JsonStringEnumConverter() } };

    private CardCatalog? _catalog;

    public async Task<CardCatalog> CatalogAsync() =>
        _catalog ??= new CardCatalog(await db.Cards.AsNoTracking().ToListAsync());

    public async Task<MatchEngine> EngineAsync() => new(Random.Shared, await CatalogAsync());

    public async Task<Match> CreateAsync(Team home, Team away, int homeRating, int awayRating, Side? humanSide, bool knockout,
        string competition, int? fixtureId = null, int? careerId = null, Difficulty difficulty = Difficulty.Normal)
    {
        var engine = await EngineAsync();
        var state = engine.Create(new MatchSetup(home, away, homeRating, awayRating, humanSide, knockout, competition, difficulty));
        var match = new Match
        {
            HomeTeamId = home.Id, AwayTeamId = away.Id, FixtureId = fixtureId, CareerId = careerId,
            StateJson = JsonSerializer.Serialize(state, StateJson)
        };
        db.Matches.Add(match);
        await db.SaveChangesAsync();
        return match;
    }

    /// <summary>Computer vs computer match (career fixtures you do not play) — same rules, tactics and AI.</summary>
    public static MatchState SimulateQuick(MatchEngine engine, Team home, Team away, int homeRating, int awayRating, bool knockout)
    {
        var state = engine.Create(new MatchSetup(home, away, homeRating, awayRating, null, knockout, ""));
        engine.SimulateToEnd(state);
        return state;
    }

    public static MatchState Load(Match m) => JsonSerializer.Deserialize<MatchState>(m.StateJson, StateJson)!;

    public static void Save(Match m, MatchState s)
    {
        m.StateJson = JsonSerializer.Serialize(s, StateJson);
        m.Status = s.Phase == MatchPhase.Finished ? MatchStatus.Finished : MatchStatus.InProgress;
        m.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Budget and hand size of the human team for this match.</summary>
    public static (int Budget, int HandSize) HumanLimits(MatchState s)
    {
        var human = s.HumanSide ?? Side.Home;
        var t = s.Team(human);
        return (Rules.TacticPoints(t.Rating, human == Side.Home) + Rules.DifficultyPoints(s.Difficulty), Rules.HandSize(t.Rating));
    }

    // ------------------------------------------------------------- view model

    public static object View(Match m, MatchState s, CardCatalog catalog, object? setup = null)
    {
        var human = s.HumanSide ?? Side.Home;
        var me = s.Team(human);
        var opp = s.Team(human.Other());
        var finished = s.Phase == MatchPhase.Finished;
        var (rows, cols) = MatchEngine.Hints(s);

        string? role = s.Phase switch
        {
            MatchPhase.Play => s.Turn == human ? "flip" : "wait",
            MatchPhase.Decision when s.Decision != null =>
                s.Decision.Kind == DecisionKind.Discard ? (s.Decision.Attacker == human ? "discard" : "wait")
                : s.Decision.Attacker == human ? "attack" : "defend",
            _ => null
        };

        object? decision = null;
        if (s.Phase == MatchPhase.Decision && s.Decision is { } d)
        {
            var (ao, dopts) = MatchEngine.Options(d.Kind);
            string[] options = d.Kind == DecisionKind.Discard
                ? s.Team(d.Attacker).Hand.Select(Cards.Name).ToArray()
                : d.Attacker == human ? ao : dopts;
            decision = new
            {
                kind = d.Kind.ToString(), source = d.Source, attacker = d.Attacker.ToString(),
                attackerName = s.Team(d.Attacker).Name, role, options, needsTiming = MatchEngine.NeedsTiming(d.Kind)
            };
        }

        return new
        {
            id = m.Id, fixtureId = m.FixtureId, careerId = m.CareerId,
            competition = s.CompetitionName, stadium = s.Stadium, isKnockout = s.IsKnockout,
            phase = s.Phase.ToString(), half = s.Half, turn = s.Turn.ToString(), difficulty = s.Difficulty.ToString(),
            humanSide = s.HumanSide?.ToString(), role,
            home = TeamView(s.Home, s, human, finished, catalog),
            away = TeamView(s.Away, s, human, finished, catalog),
            conditions = s.Conditions.Select(c => new { code = c, label = Conditions.Labels.GetValueOrDefault(c, c) }),
            composition = s.Composition.OrderByDescending(kv => kv.Value)
                .Select(kv => new { card = kv.Key, name = Cards.Name(kv.Key), count = kv.Value }),
            rowHints = rows, colHints = cols,
            table = s.Table.Select((c, i) =>
            {
                // all backs are grey: the player sees only where their own team cards are (never the computer's —
                // those positions are not even sent, so they cannot be found in the browser's developer tools)
                var mine = c.Owner == human;
                var show = c.FaceUp || finished;
                var peeked = !show && !c.Burned && me.Peeks.Contains(i);
                var know = show || peeked || mine;
                return new
                {
                    index = i, faceUp = c.FaceUp, burned = c.Burned,
                    owner = know ? c.Owner?.ToString() : null, flippedBy = c.FlippedBy?.ToString(),
                    card = know ? c.Card : null,
                    name = know ? Cards.Name(c.Card) : null,
                    peeked, mine, marked = mine && me.MarkedCell == i,
                    stolen = c.FaceUp && c.Owner != null && c.Owner != c.FlippedBy
                };
            }),
            cardsLeft = s.Table.Count(c => c.Available),
            // the first half, fully revealed, for the half-time break screen
            firstHalfTable = s.Half == 2 && s.FirstHalfTable.Count > 0
                ? s.FirstHalfTable.Select((c, i) => new
                {
                    index = i, faceUp = c.FaceUp, burned = c.Burned, owner = c.Owner?.ToString(), flippedBy = c.FlippedBy?.ToString(),
                    card = c.Card, name = Cards.Name(c.Card), peeked = false, mine = c.Owner == human,
                    stolen = c.FaceUp && c.Owner != null && c.Owner != c.FlippedBy
                })
                : null,
            lastFlip = s.LastFlip == null ? null : new
            {
                cell = s.LastFlip.Cell, card = s.LastFlip.Card, name = Cards.Name(s.LastFlip.Card),
                side = s.LastFlip.Side.ToString(), owner = s.LastFlip.Owner?.ToString(),
                description = catalog.Get(s.LastFlip.Card)?.Description
            },
            hand = me.Hand.Select(c => new { code = c, name = Cards.Name(c), description = catalog.Get(c)?.Description }),
            freeScouts = me.FreeScouts,
            handPlayed = me.HandPlayedThisTurn,
            ownCardUsed = me.OwnCardUsedThisTurn && me.CounterFlips == 0,
            opponentCardsLeft = s.Table.Count(c => c.Available && c.Owner == human.Other()),
            opponentHandCount = opp.Hand.Count,
            decision,
            shootout = s.Shootout.Select(k => new { side = k.Side.ToString(), scored = k.Scored }),
            homePenalties = s.HomePenalties, awayPenalties = s.AwayPenalties,
            events = s.Events.Select(e => new { e.Id, e.Half, e.Type, side = e.Side?.ToString(), e.Text, e.Card }),
            setup
        };
    }

    private static object TeamView(TeamMatchState t, MatchState s, Side human, bool finished, CardCatalog catalog)
    {
        var isMe = s.Team(human) == t;
        var started = s.Phase != MatchPhase.Setup;
        return new
        {
            id = t.TeamId, name = t.Name, shortName = t.ShortName, primaryColor = t.PrimaryColor, secondaryColor = t.SecondaryColor,
            goals = t.Goals, isHuman = t.IsHuman, rating = t.Rating, aiLevel = Rules.AiLevelNames[t.AiLevel],
            passStreak = t.PassStreak, captain = t.CaptainShield, wall = t.WallActive, calm = t.CalmActive,
            press = t.PressActive, counterFlips = t.CounterFlips,
            goalkeeper = t.GoalkeeperActive, targetMan = t.TargetManActive, sweeper = t.SweeperActive, twelfthMan = t.TwelfthManActive,
            var = t.VarActive, penaltyTaker = t.PenaltyTakerActive,
            // the opponent's field cards stay secret; modifiers are revealed when the match starts
            fieldCards = started && (isMe || finished) ? t.Tactic.Field.Select(Cards.Name) : null,
            fieldCount = started ? t.Tactic.Field.Count : 0,
            modifiers = started ? t.Tactic.Modifiers.Select(m => catalog.Get(m)?.Name ?? m) : null,
            stats = t.Stats
        };
    }
}
