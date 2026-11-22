using DugoutDeck.Api.Data.Entities;
using DugoutDeck.Api.Game;

namespace DugoutDeck.Tests;

/// <summary>Version 1.1 cards: Winger, Goalkeeper, Target Man, Sweeper, Super Sub, Decoy, 12th Man, VAR, Marking,
/// Substitution, Analysis, Penalty Taker and the new modifiers.</summary>
public class NewCardsTests
{
    private static (MatchEngine Engine, MatchState S) New(int seed, TacticSpec? home = null)
    {
        var engine = new MatchEngine(new Random(seed), MatchEngineTestsCatalog.Get());
        var s = engine.Create(new MatchSetup(new Team { Id = 1, Name = "A", City = "X" }, new Team { Id = 2, Name = "B", City = "Y" }, 70, 70, Side.Home, false, "T"));
        engine.Start(s, home ?? TacticRules.Default(), TacticRules.Default());
        s.Turn = Side.Home;
        return (engine, s);
    }

    private static void Put(MatchState s, int cell, string card, Side? owner = null) => s.Table[cell] = new TableCell { Card = card, Owner = owner };

    [Fact]
    public void Winger_GivesACorner()
    {
        var (e, s) = New(1);
        Put(s, 0, Cards.Winger, Side.Home);
        e.FlipHuman(s, 0);
        Assert.Equal(DecisionKind.Corner, s.Decision!.Kind);
        Assert.Equal(Side.Home, s.Decision.Attacker);
    }

    [Fact]
    public void Sweeper_IgnoresOffside_And12thMan_IgnoresADangerousCardAtHomeOnly()
    {
        var (e, s) = New(2);
        Put(s, 0, Cards.Sweeper, Side.Home); Put(s, 1, Cards.Offside); Put(s, 2, Cards.Pass); Put(s, 3, Cards.TwelfthMan, Side.Away);
        e.FlipHuman(s, 0);
        e.FlipHuman(s, 1);
        Assert.Equal(Side.Home, s.Turn); // the sweeper saved the move
        e.FlipHuman(s, 2);
        e.FlipHuman(s, 3); // home steals the away team's 12th Man: home team → it works
        Assert.True(s.Home.TwelfthManActive);
        Put(s, 4, Cards.Tackle);
        e.FlipHuman(s, 4);
        Assert.Equal(Side.Home, s.Turn);
    }

    [Fact]
    public void Decoy_CannotBeStolen_AndMarking_ProtectsACard()
    {
        var (e, s) = New(3);
        Put(s, 0, Cards.Pass); Put(s, 1, Cards.Decoy, Side.Away);
        e.FlipHuman(s, 0);
        e.FlipHuman(s, 1);
        Assert.Equal(0, s.Home.PassStreak);
        Assert.Equal(0, s.Home.Stats.CardsStolen);
        Assert.Equal(Side.Home, s.Turn);

        s.Away.MarkedCell = 2;
        Put(s, 2, Cards.Striker, Side.Away);
        e.FlipHuman(s, 2);
        Assert.Equal(Side.Away, s.Turn); // marked: no steal, ball lost
        Assert.Equal(0, s.Home.Stats.CardsStolen);
    }

    [Fact]
    public void SuperSub_IsAPassInTheFirstHalf()
    {
        var (e, s) = New(4);
        Put(s, 0, Cards.SuperSub, Side.Home);
        e.FlipHuman(s, 0);
        Assert.Equal(1, s.Home.PassStreak);
        Assert.Equal(Side.Home, s.Turn);
    }

    [Fact]
    public void Var_CanDisallowAGoal_AndIsUsedOnce()
    {
        var disallowed = 0;
        for (var seed = 0; seed < 200; seed++)
        {
            var (e, s) = New(seed);
            s.Away.VarActive = true;
            Put(s, 0, Cards.Goal);
            e.FlipHuman(s, 0);
            Assert.False(s.Away.VarActive);
            if (s.Home.Goals == 0) disallowed++;
        }
        Assert.InRange(disallowed, 50, 110); // about 40%
    }

    [Fact]
    public void Marking_Substitution_Analysis_HandCards()
    {
        var tactic = new TacticSpec { Field = [Cards.Striker, Cards.LongShot, Cards.LongShot, Cards.Captain], Hand = [Cards.Marking, Cards.Substitution, Cards.Analysis] };
        var (e, s) = New(5, tactic);
        Put(s, 0, Cards.Striker, Side.Home); Put(s, 1, Cards.Pass); Put(s, 2, Cards.Pass); Put(s, 3, Cards.Pass);
        e.PlayHandHuman(s, Cards.Marking, 0, null);
        Assert.Equal(0, s.Home.MarkedCell);

        Put(s, 5, Cards.LongShot, Side.Home);
        s.Table[5].FlippedBy = Side.Home;
        e.FlipHuman(s, 1); // allows the next hand card
        e.PlayHandHuman(s, Cards.Substitution, 5, null);
        Assert.Contains(s.Table, c => c.Card == Cards.LongShot && c.Owner == Side.Home && c.Available);

        e.FlipHuman(s, 2);
        e.PlayHandHuman(s, Cards.Analysis, 10, null);
        Assert.Contains("hidden in row 3", s.Events[^1].Text);
    }

    [Fact]
    public void NewModifiers_ChangeTheGreyCards()
    {
        var c = TacticRules.Composition(MatchEngineTestsCatalog.Get(), [], new TacticSpec { Modifiers = ["MOD_FREEKICK_UP1", "MOD_TACKLE_DOWN1"] },
            new TacticSpec { Modifiers = ["MOD_SAVE_UP1", "MOD_WET_PITCH"] });
        Assert.Equal(16, c.Values.Sum());
        Assert.Equal(2, c[Cards.FreeKick]);
        Assert.Equal(2, c[Cards.Tackle]);
        Assert.Equal(2, c[Cards.Save]);
        Assert.Equal(2, c[Cards.Miss]);
    }
}
