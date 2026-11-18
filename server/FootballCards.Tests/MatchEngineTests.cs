using FootballCards.Api.Data.Entities;
using FootballCards.Api.Game;
using Xunit.Abstractions;

namespace FootballCards.Tests;

public class MatchEngineTests(ITestOutputHelper output)
{
    // Same composition as seed.json
    private static readonly List<CardDefinition> Deck =
    [
        new() { Code = Cards.Pass, CopiesInDeck = 33 },
        new() { Code = Cards.Tackle, CopiesInDeck = 24 },
        new() { Code = Cards.Corner, CopiesInDeck = 6 },
        new() { Code = Cards.FreeKick, CopiesInDeck = 5 },
        new() { Code = Cards.Offside, CopiesInDeck = 6 },
        new() { Code = Cards.Save, CopiesInDeck = 6 },
        new() { Code = Cards.Miss, CopiesInDeck = 7 },
        new() { Code = Cards.Goal, CopiesInDeck = 4 },
        new() { Code = Cards.Penalty, CopiesInDeck = 2 },
        new() { Code = Cards.YellowCard, CopiesInDeck = 4 },
        new() { Code = Cards.RedCard, CopiesInDeck = 1 },
        new() { Code = Cards.HalfTime, CopiesInDeck = 1 },
        new() { Code = Cards.FullTime, CopiesInDeck = 1 },
    ];

    private static Team T(int id) => new() { Id = id, Name = $"Team {id}", Stadium = "Stadium" };

    private static MatchState NewMatch(MatchEngine e, Side? human = null, bool knockout = false) =>
        e.Create(new MatchSetup(T(1), T(2), human, knockout, "Test"), Deck);

    [Fact]
    public void Table_HasFaceDownCards_AndDeckHoldsTheRest()
    {
        var s = NewMatch(new MatchEngine(new Random(1)));
        Assert.Equal(Rules.TableSize, s.Table.Count);
        Assert.Equal(100 - Rules.TableSize, s.Deck.Count);
        Assert.Equal(MatchPhase.Play, s.Phase);
    }

    [Fact]
    public void SimulatedMatches_AlwaysEnd_WithReasonableScores()
    {
        var engine = new MatchEngine(new Random(2));
        double goals = 0, flips = 0;
        const int n = 3000;
        for (var i = 0; i < n; i++)
        {
            var s = NewMatch(engine);
            engine.SimulateToEnd(s);
            Assert.Equal(MatchPhase.Finished, s.Phase);
            goals += s.Home.Goals + s.Away.Goals;
            flips += s.Home.Stats.Flips + s.Away.Stats.Flips;
        }
        output.WriteLine($"avg goals {goals / n:F2}, avg flips {flips / n:F1}");
        Assert.InRange(goals / n, 1.0, 4.5);
    }

    [Fact]
    public void Pass_KeepsTurn_Tackle_SwitchesTurn()
    {
        var engine = new MatchEngine(new Random(3));
        var s = NewMatch(engine, Side.Home);
        s.Turn = Side.Home;
        s.Table[0] = Cards.Pass;
        engine.FlipHuman(s, 0);
        Assert.Equal(Side.Home, s.Turn);

        s.Table[1] = Cards.Tackle;
        engine.FlipHuman(s, 1);
        Assert.Equal(Side.Away, s.Turn);
    }

    [Fact]
    public void FullTime_AsFirstCard_EndsTheMatch()
    {
        var engine = new MatchEngine(new Random(4));
        var s = NewMatch(engine, Side.Home);
        s.Turn = Side.Home;
        s.Table[5] = Cards.FullTime;
        engine.FlipHuman(s, 5);
        Assert.Equal(MatchPhase.Finished, s.Phase);
    }

    [Fact]
    public void HalfTime_StartsSecondHalf_WithTheOtherPlayer()
    {
        var engine = new MatchEngine(new Random(5));
        var s = NewMatch(engine, Side.Home);
        s.Turn = Side.Home;
        s.Table[2] = Cards.HalfTime;
        engine.FlipHuman(s, 2);
        Assert.Equal(2, s.Half);
        Assert.Equal(Side.Away, s.Turn);
    }

    [Fact]
    public void Goal_Scores_AndGivesKickOffToOpponent()
    {
        var engine = new MatchEngine(new Random(6));
        var s = NewMatch(engine, Side.Away);
        s.Turn = Side.Away;
        s.Table[3] = Cards.Goal;
        engine.FlipHuman(s, 3);
        Assert.Equal(1, s.Away.Goals);
        Assert.Equal(Side.Home, s.Turn);
    }

    [Fact]
    public void Penalty_StartsMiniGame_ThenOtherPlayerFlips()
    {
        var engine = new MatchEngine(new Random(7));
        var s = NewMatch(engine, Side.Home);
        s.Turn = Side.Home;
        s.Table[0] = Cards.Penalty;
        engine.FlipHuman(s, 0);
        Assert.Equal(MatchPhase.Penalty, s.Phase);
        Assert.Throws<GameRuleException>(() => engine.FlipHuman(s, 1));
        engine.PenaltyHuman(s, 0);
        Assert.Equal(MatchPhase.Play, s.Phase);
        Assert.Equal(Side.Away, s.Turn);
    }

    [Fact]
    public void HumanCannotFlip_OnComputersTurn()
    {
        var engine = new MatchEngine(new Random(8));
        var s = NewMatch(engine, Side.Home);
        s.Turn = Side.Away;
        Assert.Throws<GameRuleException>(() => engine.FlipHuman(s, 0));
        engine.AiFlip(s); // works
    }

    [Fact]
    public void KnockoutDraw_GoesToShootout_AndAlwaysHasWinner()
    {
        var engine = new MatchEngine(new Random(9));
        for (var i = 0; i < 300; i++)
        {
            var s = NewMatch(engine, knockout: true);
            engine.SimulateToEnd(s);
            Assert.NotNull(s.WinnerTeamId());
        }

        var h = NewMatch(engine, Side.Home, knockout: true);
        h.Turn = Side.Home;
        h.Table[0] = Cards.FullTime;
        engine.FlipHuman(h, 0);
        Assert.Equal(MatchPhase.Shootout, h.Phase);
        var guard = 0;
        while (h.Phase == MatchPhase.Shootout && guard++ < 100) engine.PenaltyHuman(h, guard % 3);
        Assert.Equal(MatchPhase.Finished, h.Phase);
        Assert.NotNull(h.WinnerTeamId());
    }
}
