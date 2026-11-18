using FootballCards.Api.Data.Entities;
using FootballCards.Api.Game;
using Xunit.Abstractions;

namespace FootballCards.Tests;

public class MatchEngineTests(ITestOutputHelper output)
{
    // Same composition as seed.json: 24 cards per half + Half Time (1st half) / Full Time (2nd half) = 5x5 grid
    private static readonly List<CardDefinition> Deck =
    [
        new() { Code = Cards.Pass, CopiesInDeck = 7 },
        new() { Code = Cards.Tackle, CopiesInDeck = 5 },
        new() { Code = Cards.Corner, CopiesInDeck = 2 },
        new() { Code = Cards.FreeKick, CopiesInDeck = 1 },
        new() { Code = Cards.Offside, CopiesInDeck = 1 },
        new() { Code = Cards.Save, CopiesInDeck = 1 },
        new() { Code = Cards.Miss, CopiesInDeck = 2 },
        new() { Code = Cards.Goal, CopiesInDeck = 2 },
        new() { Code = Cards.Penalty, CopiesInDeck = 1 },
        new() { Code = Cards.YellowCard, CopiesInDeck = 1 },
        new() { Code = Cards.RedCard, CopiesInDeck = 1 },
        new() { Code = Cards.HalfTime, CopiesInDeck = 1 },
        new() { Code = Cards.FullTime, CopiesInDeck = 1 },
    ];

    private static Team T(int id) => new() { Id = id, Name = $"Team {id}", Stadium = "Stadium" };

    private static MatchState NewMatch(MatchEngine e, Side? human = null, bool knockout = false) =>
        e.Create(new MatchSetup(T(1), T(2), human, knockout, "Test"), Deck);

    [Fact]
    public void FirstHalfGrid_HasHalfTime_SecondHalfGrid_HasFullTime()
    {
        var s = NewMatch(new MatchEngine(new Random(1)));
        Assert.Equal(25, s.Table.Count);
        Assert.All(s.Table, c => Assert.False(c.FaceUp));
        Assert.Single(s.Table, c => c.Card == Cards.HalfTime);
        Assert.DoesNotContain(s.Table, c => c.Card == Cards.FullTime);
        Assert.Equal(25, s.SecondHalfTable.Count);
        Assert.Single(s.SecondHalfTable, c => c.Card == Cards.FullTime);
        Assert.DoesNotContain(s.SecondHalfTable, c => c.Card == Cards.HalfTime);
    }

    [Fact]
    public void FullTime_NeverComesBeforeHalfTime()
    {
        var engine = new MatchEngine(new Random(11));
        for (var i = 0; i < 500; i++)
        {
            var s = NewMatch(engine);
            engine.SimulateToEnd(s);
            var ht = s.Events.FindIndex(e => e.Card == Cards.HalfTime);
            var ft = s.Events.FindIndex(e => e.Card == Cards.FullTime);
            Assert.True(ht >= 0 && ft > ht);
        }
    }

    [Fact]
    public void FlippedCard_StaysFaceUp_AndCannotBeFlippedAgain()
    {
        var engine = new MatchEngine(new Random(10));
        var s = NewMatch(engine, Side.Home);
        s.Turn = Side.Home;
        s.Table[0].Card = Cards.Pass;
        engine.FlipHuman(s, 0);
        Assert.True(s.Table[0].FaceUp);
        Assert.Equal(Side.Home, s.Table[0].FlippedBy);
        Assert.Throws<GameRuleException>(() => engine.FlipHuman(s, 0));
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
        Assert.InRange(flips / n, 10, 50);
    }

    [Fact]
    public void Pass_KeepsTurn_Tackle_SwitchesTurn()
    {
        var engine = new MatchEngine(new Random(3));
        var s = NewMatch(engine, Side.Home);
        s.Turn = Side.Home;
        s.Table[0].Card = Cards.Pass;
        engine.FlipHuman(s, 0);
        Assert.Equal(Side.Home, s.Turn);

        s.Table[1].Card = Cards.Tackle;
        engine.FlipHuman(s, 1);
        Assert.Equal(Side.Away, s.Turn);
    }

    [Fact]
    public void FullTime_AsFirstCardOfSecondHalf_EndsTheMatch()
    {
        var engine = new MatchEngine(new Random(4));
        var s = NewMatch(engine, Side.Home);
        s.Turn = Side.Home;
        s.Table[0].Card = Cards.HalfTime;
        engine.FlipHuman(s, 0);
        s.Turn = Side.Home;
        s.Table[5].Card = Cards.FullTime;
        engine.FlipHuman(s, 5);
        Assert.Equal(MatchPhase.Finished, s.Phase);
    }

    [Fact]
    public void HalfTime_DealsFreshFaceDownGrid_AndOtherPlayerStarts()
    {
        var engine = new MatchEngine(new Random(5));
        var s = NewMatch(engine, Side.Home);
        s.Turn = Side.Home;
        s.Table[0].Card = Cards.Pass;
        engine.FlipHuman(s, 0);
        s.Table[2].Card = Cards.HalfTime;
        engine.FlipHuman(s, 2);
        Assert.Equal(2, s.Half);
        Assert.Equal(Side.Away, s.Turn);
        Assert.Equal(25, s.Table.Count);
        Assert.All(s.Table, c => Assert.False(c.FaceUp));
        Assert.Single(s.Table, c => c.Card == Cards.FullTime);
    }

    [Fact]
    public void Goal_Scores_AndGivesKickOffToOpponent()
    {
        var engine = new MatchEngine(new Random(6));
        var s = NewMatch(engine, Side.Away);
        s.Turn = Side.Away;
        s.Table[3].Card = Cards.Goal;
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
        s.Table[0].Card = Cards.Penalty;
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
        h.Table[0].Card = Cards.FullTime;
        engine.FlipHuman(h, 0);
        Assert.Equal(MatchPhase.Shootout, h.Phase);
        var guard = 0;
        while (h.Phase == MatchPhase.Shootout && guard++ < 100) engine.PenaltyHuman(h, guard % 3);
        Assert.Equal(MatchPhase.Finished, h.Phase);
        Assert.NotNull(h.WinnerTeamId());
    }
}
