using FootballCards.Api.Data.Entities;
using FootballCards.Api.Game;
using Xunit.Abstractions;

namespace FootballCards.Tests;

public class MatchEngineTests(ITestOutputHelper output)
{
    private static readonly List<CardDefinition> Deck =
    [
        new() { Code = "PASS", Kind = CardKind.Attack, CopiesInDeck = 9 },
        new() { Code = "LONG_BALL", Kind = CardKind.Attack, CopiesInDeck = 4 },
        new() { Code = "DRIBBLE", Kind = CardKind.Attack, CopiesInDeck = 5 },
        new() { Code = "CROSS", Kind = CardKind.Attack, CopiesInDeck = 4 },
        new() { Code = "SHOT", Kind = CardKind.Attack, CopiesInDeck = 6 },
        new() { Code = "COUNTER", Kind = CardKind.Attack, CopiesInDeck = 3 },
        new() { Code = "PRESS", Kind = CardKind.Defense, CopiesInDeck = 5 },
        new() { Code = "TACKLE", Kind = CardKind.Defense, CopiesInDeck = 5 },
        new() { Code = "INTERCEPT", Kind = CardKind.Defense, CopiesInDeck = 4 },
        new() { Code = "BLOCK", Kind = CardKind.Defense, CopiesInDeck = 4 },
        new() { Code = "FOUL", Kind = CardKind.Defense, CopiesInDeck = 2 },
    ];

    private static Team T(int id, int r) => new() { Id = id, Name = $"Отбор {id}", Attack = r, Midfield = r, Defense = r };

    private static (double goals, double homeWin, double draw, double steps) Run(int home, int away, int n, int seed)
    {
        var rng = new Random(seed);
        var engine = new MatchEngine(rng);
        double goals = 0, hw = 0, dr = 0, steps = 0;
        for (var i = 0; i < n; i++)
        {
            var s = engine.Create(new MatchSetup(T(1, home), T(2, away), null, Tactic.Balanced, Tactic.Balanced, 1, false, "Тест"), Deck);
            engine.SimulateToEnd(s);
            Assert.Equal(MatchPhase.Finished, s.Phase);
            goals += s.Home.Goals + s.Away.Goals;
            if (s.Home.Goals > s.Away.Goals) hw++;
            if (s.Home.Goals == s.Away.Goals) dr++;
            steps += s.Home.PossessionTurns + s.Away.PossessionTurns;
        }
        return (goals / n, hw / n, dr / n, steps / n);
    }

    [Fact]
    public void EqualTeams_ProduceRealisticScores()
    {
        var (goals, hw, dr, steps) = Run(65, 65, 2000, 1);
        output.WriteLine($"goals={goals:F2} homeWin={hw:P0} draw={dr:P0} steps={steps:F1}");
        Assert.InRange(goals, 1.8, 3.8);
        Assert.InRange(dr, 0.12, 0.40);
        Assert.InRange(steps, 22, 45);
    }

    [Fact]
    public void StrongerTeam_WinsMoreOften_ButNotAlways()
    {
        var (goals, hw, dr, _) = Run(82, 58, 2000, 2);
        output.WriteLine($"goals={goals:F2} strongHomeWin={hw:P0} draw={dr:P0}");
        Assert.InRange(hw, 0.55, 0.92);
    }

    [Fact]
    public void KnockoutMatch_AlwaysHasWinner()
    {
        var rng = new Random(3);
        var engine = new MatchEngine(rng);
        for (var i = 0; i < 300; i++)
        {
            var s = engine.Create(new MatchSetup(T(1, 60), T(2, 60), null, Tactic.Balanced, Tactic.Balanced, 1, true, "Купа"), Deck);
            engine.SimulateToEnd(s);
            Assert.NotNull(s.WinnerTeamId());
        }
    }

    [Fact]
    public void HumanCanPlayFullMatchWithBasicActions()
    {
        var rng = new Random(4);
        var engine = new MatchEngine(rng);
        var s = engine.Create(new MatchSetup(T(1, 60), T(2, 60), Side.Home, Tactic.Balanced, Tactic.Balanced, 1, false, "Тест"), Deck);
        var guard = 0;
        while (s.Phase != MatchPhase.Finished && guard++ < 500)
            engine.PlayHuman(s, new PlayerAction(null, 0, 1));
        Assert.Equal(MatchPhase.Finished, s.Phase);
    }
}
