using System.Text.Json;
using System.Text.Json.Serialization;
using FootballCards.Api.Data;
using FootballCards.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FootballCards.Api.Game;

/// <summary>Създаване, зареждане и записване на мачове. Връща изглед от гледна точка на човека.</summary>
public class MatchService(AppDbContext db)
{
    public static readonly JsonSerializerOptions StateJson = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly MatchEngine _engine = new(Random.Shared);

    public async Task<List<CardDefinition>> DeckAsync() => await db.Cards.AsNoTracking().ToListAsync();

    public async Task<Match> CreateAsync(Team home, Team away, Side? humanSide, Tactic humanTactic, int difficulty,
        bool knockout, string competition, int? fixtureId = null, int? careerId = null)
    {
        var homeTactic = humanSide == Side.Home ? humanTactic : AiTactic(home, away);
        var awayTactic = humanSide == Side.Away ? humanTactic : AiTactic(away, home);
        var state = _engine.Create(new MatchSetup(home, away, humanSide, homeTactic, awayTactic, difficulty, knockout, competition),
            await DeckAsync());
        var match = new Match
        {
            HomeTeamId = home.Id, AwayTeamId = away.Id, FixtureId = fixtureId, CareerId = careerId,
            StateJson = JsonSerializer.Serialize(state, StateJson)
        };
        db.Matches.Add(match);
        await db.SaveChangesAsync();
        return match;
    }

    /// <summary>Бърза симулация AI срещу AI (за мачовете от кариерата, които не играете).</summary>
    public MatchState SimulateQuick(Team home, Team away, bool knockout, List<CardDefinition> deck)
    {
        var state = _engine.Create(new MatchSetup(home, away, null, AiTactic(home, away), AiTactic(away, home), 1, knockout, ""), deck);
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

    public void Play(MatchState s, PlayerAction action) => _engine.PlayHuman(s, action);

    public void SimulateRest(MatchState s) => _engine.SimulateToEnd(s);

    private static Tactic AiTactic(Team self, Team opp)
    {
        var diff = self.Overall - opp.Overall;
        return diff >= 8 ? Tactic.Attacking : diff <= -8 ? Tactic.Defensive : Tactic.Balanced;
    }

    // ------------------------------------------------------------- view model

    public static object View(Match m, MatchState s, int sinceEventId = 0)
    {
        var human = s.HumanSide;
        var me = human is { } h ? s.Team(h) : null;
        var opp = human is { } h2 ? s.Team(h2.Other()) : null;
        var attacking = human == s.Possession;

        object? hand = null;
        if (me != null && s.Phase == MatchPhase.Play)
        {
            var cards = attacking ? me.AttackHand : me.DefenseHand;
            hand = cards.Select((c, i) =>
            {
                var legal = Rules.IsLegal(c, s.Zone, s.CounterAvailable);
                var (pct, label) = legal ? AiPlayer.Hint(s, c, attacking) : (0, "");
                return new { index = i, code = c, name = Cards.Names[c], legal, hint = pct, hintLabel = label };
            }).ToList();
        }

        object? basic = null;
        if (me != null && s.Phase == MatchPhase.Play)
        {
            var code = attacking ? Cards.Hold : Cards.Position;
            var (pct, label) = AiPlayer.Hint(s, code, attacking);
            basic = new { code, name = Cards.Names[code], hint = pct, hintLabel = label };
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
            minute = s.Minute,
            minuteLabel = s.MinuteLabel,
            half = s.Half,
            phase = s.Phase.ToString(),
            possession = s.Possession.ToString(),
            zone = s.Zone,
            zoneName = Rules.ZoneName(s.Zone),
            counterAvailable = s.CounterAvailable,
            humanSide = human?.ToString(),
            humanRole = s.Phase == MatchPhase.Penalty && human != null
                ? (s.Penalty!.Shooter == human ? "shooter" : "keeper")
                : attacking ? "attack" : "defense",
            hand,
            basic,
            opponentHand = opp == null ? null : new { attack = opp.AttackHand.Count, defense = opp.DefenseHand.Count },
            homePenalties = s.HomePenalties,
            awayPenalties = s.AwayPenalties,
            events = s.Events.Where(e => e.Id > sinceEventId).Select(e => new
            {
                e.Id, e.Minute, e.MinuteLabel, e.Type, side = e.Side?.ToString(), e.Text,
                attackCard = e.AttackCard == null ? null : Cards.Names.GetValueOrDefault(e.AttackCard),
                defenseCard = e.DefenseCard == null ? null : Cards.Names.GetValueOrDefault(e.DefenseCard),
            }),
        };
    }

    private static object TeamView(TeamMatchState t) => new
    {
        id = t.TeamId, name = t.Name, shortName = t.ShortName, primaryColor = t.PrimaryColor, secondaryColor = t.SecondaryColor,
        goals = t.Goals, tactic = t.Tactic.ToString(), isHuman = t.IsHuman,
        attack = t.Attack, midfield = t.Midfield, defense = t.Defense,
        stats = new
        {
            shots = t.Shots, onTarget = t.ShotsOnTarget, fouls = t.Fouls, yellow = t.YellowCards, red = t.RedCards,
            possessionTurns = t.PossessionTurns
        }
    };
}
