using DugoutDeck.Api.Careers;
using DugoutDeck.Api.Data.Entities;
using DugoutDeck.Api.Game;

namespace DugoutDeck.Tests;

/// <summary>Version 1.1 rules: balanced fixtures, red card chosen by the opponent, a minimum of grey cards, table zones,
/// tactic plans and the list of played cards.</summary>
public class Release11Tests
{
    [Theory]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(14)]
    [InlineData(16)]
    [InlineData(18)]
    [InlineData(20)]
    [InlineData(22)]
    [InlineData(24)]
    [InlineData(9)]
    public void Fixtures_NoTeamPlaysMoreThanTwoHomeOrAwayInARow(int count)
    {
        var teams = Enumerable.Range(100, count).ToList();
        var rounds = CareerService.RoundRobin(teams);
        var perRound = count % 2 == 0 ? count / 2 : (count - 1) / 2;
        Assert.All(rounds, r => Assert.Equal(perRound, r.Count));
        foreach (var t in teams)
        {
            var venues = rounds.Select(r => r.FirstOrDefault(p => p.Home == t || p.Away == t))
                .Where(p => p.Home != 0).Select(p => p.Home == t ? 'H' : 'A').ToList();
            var streak = 1;
            for (var i = 1; i < venues.Count; i++)
            {
                streak = venues[i] == venues[i - 1] ? streak + 1 : 1;
                Assert.True(streak <= 2, $"{count} teams: team {t} has 3 in a row ({new string(venues.ToArray())})");
            }
            // everyone plays everyone home and away in the first two halves
            var pairs = rounds.Take((count % 2 == 0 ? count - 1 : count) * 2).SelectMany(r => r).ToList();
            foreach (var o in teams.Where(o => o != t))
            {
                Assert.Contains(pairs, p => p.Home == t && p.Away == o);
                Assert.Contains(pairs, p => p.Home == o && p.Away == t);
            }
        }
    }

    private static (MatchEngine Engine, MatchState S) New(int seed, Side human, TacticSpec home, TacticSpec away)
    {
        var engine = new MatchEngine(new Random(seed), MatchEngineTestsCatalog.Get());
        var s = engine.Create(new MatchSetup(new Team { Id = 1, Name = "A", City = "X" }, new Team { Id = 2, Name = "B", City = "Y" }, 70, 70, human, false, "T"));
        engine.Start(s, human == Side.Home ? home : null, human == Side.Away ? away : null);
        // the computer's tactic is built by itself; give it known hand cards for the test
        s.Team(human.Other()).Hand = away.Hand.ToList();
        return (engine, s);
    }

    [Fact]
    public void RedCardForTheComputer_TheHumanChoosesWhichHandCardItLoses()
    {
        var home = new TacticSpec { Field = [Cards.LongShot, Cards.LongShot, Cards.LongShot, Cards.LongShot] };
        var away = new TacticSpec { Hand = [Cards.Scout, Cards.Calm, Cards.Burn] };
        var (e, s) = New(3, Side.Home, home, away);
        // only the red card is left face down: the computer has to flip it
        for (var i = 1; i < s.Table.Count; i++) s.Table[i] = new TableCell { Card = Cards.Pass, FlippedBy = Side.Home };
        s.Table[0] = new TableCell { Card = Cards.RedCard };
        s.Table[1] = new TableCell { Card = Cards.HalfTime };
        s.Away.HandPlayedThisTurn = true; // no hand card first
        s.Turn = Side.Away;
        s.Table[1].FlippedBy = Side.Home;
        e.AiStep(s);
        Assert.Equal(MatchPhase.Decision, s.Phase);
        Assert.Equal(DecisionKind.Discard, s.Decision!.Kind);
        Assert.Equal(Side.Away, s.Decision.Attacker); // the team that was sent off
        Assert.Throws<GameRuleException>(() => e.AiStep(s)); // the human chooses, not the computer
        var hand = s.Away.Hand.ToList();
        e.DecideHuman(s, hand.IndexOf(Cards.Calm), null, false);
        Assert.DoesNotContain(Cards.Calm, s.Away.Hand);
        Assert.Equal(hand.Count - 1, s.Away.Hand.Count);
        Assert.Contains(s.Events, ev => ev.Text.Contains("take away"));
    }

    [Fact]
    public void Composition_AlwaysKeepsAtLeastOneOfEachGreyCard()
    {
        var catalog = MatchEngineTestsCatalog.Get();
        var minus = new TacticSpec { Modifiers = ["MOD_GOAL_DOWN2", "MOD_CORNER_DOWN2", "MOD_TACKLE_DOWN1"] };
        var c = TacticRules.Composition(catalog, [], minus, minus);
        Assert.Equal(16, c.Values.Sum());
        foreach (var card in new[] { Cards.Goal, Cards.Penalty, Cards.Corner, Cards.FreeKick, Cards.Tackle, Cards.Save, Cards.Miss, Cards.Offside, Cards.YellowCard, Cards.RedCard, Cards.Pass })
            Assert.True(c.GetValueOrDefault(card) >= 1, $"{card} must stay in the grid");
    }

    [Fact]
    public void TableZones_FollowEachLeague()
    {
        var bg1 = new League { Id = 1, Country = "Bulgaria", Tier = 1, Relegation = 2, UclSpots = 1, UelSpots = 1, UeclSpots = 1 };
        var bg2 = new League { Id = 2, Country = "Bulgaria", Tier = 2, Relegation = 0 };
        var sco1 = new League { Id = 3, Country = "Scotland", Tier = 1, Relegation = 1, UclSpots = 1, UelSpots = 1, UeclSpots = 1 };
        var sco2 = new League { Id = 4, Country = "Scotland", Tier = 2 };
        var bel = new League { Id = 5, Country = "Belgium", Tier = 1, UclSpots = 1, UelSpots = 1, UeclSpots = 1 };
        var all = new[] { bg1, bg2, sco1, sco2, bel };

        var z = TableZones.For(bg1, all, 16);
        Assert.Equal(new TableZone(15, 16, "relegation", "Relegation"), z.Single(x => x.Kind == "relegation"));
        Assert.DoesNotContain(z, x => x.Kind == "ucl"); // one Champions Cup place: the champion
        Assert.Equal("Champion (Champions Cup)", z.Single(x => x.Kind == "champion").Label);
        Assert.Equal((2, 3), z.Where(x => x.Kind == "uel").Select(x => (x.From, x.To)).Single());
        Assert.Equal((4, 4), z.Where(x => x.Kind == "uecl").Select(x => (x.From, x.To)).Single());

        Assert.Equal((1, 2), TableZones.For(bg2, all, 16).Where(x => x.Kind == "promotion").Select(x => (x.From, x.To)).Single());
        Assert.Equal((12, 12), TableZones.For(sco1, all, 12).Where(x => x.Kind == "relegation").Select(x => (x.From, x.To)).Single());
        Assert.Equal((1, 1), TableZones.For(sco2, all, 10).Where(x => x.Kind == "promotion").Select(x => (x.From, x.To)).Single());
        Assert.DoesNotContain(TableZones.For(bel, all, 16), x => x.Kind == "relegation"); // one division: nobody goes down
    }

    [Fact]
    public void TacticPlans_ThreeNamedSlots_ActivePlanIsPreferred()
    {
        var career = new Career();
        var set = TacticPlanSet.Load(career);
        Assert.Equal(["Plan A", "Plan B", "Plan C"], set.Plans.Select(p => p.Name));
        Assert.Null(TacticPlanSet.Preferred(career));

        career.LastTacticJson = System.Text.Json.JsonSerializer.Serialize(new TacticSpec { Field = [Cards.Striker] });
        Assert.Equal([Cards.Striker], TacticPlanSet.Preferred(career)!.Field);

        set.Plans[1].Tactic = new TacticSpec { Field = [Cards.Wall] };
        set.Active = 1;
        set.Save(career);
        Assert.Equal([Cards.Wall], TacticPlanSet.Preferred(career)!.Field);
        Assert.Equal(1, TacticPlanSet.Load(career).Active);
    }

    [Fact]
    public void PlayedCards_AreRecorded()
    {
        var home = new TacticSpec { Field = [Cards.Captain, Cards.LongShot, Cards.LongShot, Cards.LongShot], Hand = [Cards.Calm] };
        var (e, s) = New(5, Side.Home, home, new TacticSpec());
        s.Turn = Side.Home;
        for (var i = 0; i < s.Table.Count; i++) s.Table[i] = new TableCell { Card = Cards.Pass };
        s.Table[0] = new TableCell { Card = Cards.Captain, Owner = Side.Home };
        s.Table[1] = new TableCell { Card = Cards.Wall, Owner = Side.Away };
        s.Home.Hand = [Cards.Calm];
        e.PlayHandHuman(s, Cards.Calm, null, null);
        e.FlipHuman(s, 0);
        e.FlipHuman(s, 1);
        Assert.Equal(["hand", "own", "stolen"], s.Home.Played.Select(p => p.How));
        Assert.Equal([Cards.Calm, Cards.Captain, Cards.Wall], s.Home.Played.Select(p => p.Code));
    }
}
