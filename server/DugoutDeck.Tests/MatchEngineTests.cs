using System.Text.Json;
using DugoutDeck.Api.Data.Entities;
using DugoutDeck.Api.Game;
using Xunit.Abstractions;

namespace DugoutDeck.Tests;

public class MatchEngineTests(ITestOutputHelper output)
{
    /// <summary>The real card catalog from seed.json, so the tests check the shipped numbers.</summary>
    private static readonly CardCatalog Catalog = LoadCatalog();

    private static CardCatalog LoadCatalog()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "Seed", "seed.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var cards = doc.RootElement.GetProperty("cards").EnumerateArray().Select(c => new CardDefinition
        {
            Code = c.GetProperty("code").GetString()!,
            Name = c.GetProperty("name").GetString()!,
            Kind = Enum.Parse<CardKind>(c.GetProperty("kind").GetString()!),
            Rarity = c.TryGetProperty("rarity", out var r) ? Enum.Parse<CardRarity>(r.GetString()!) : CardRarity.None,
            CopiesInDeck = c.TryGetProperty("copies", out var n) ? n.GetInt32() : 0,
            Cost = c.TryGetProperty("cost", out var cost) ? cost.GetInt32() : 0,
        });
        return new CardCatalog(cards);
    }

    private static Team T(int id, string city = "") => new() { Id = id, Name = $"Team {id}", ShortName = $"T{id}", City = city, Stadium = "Stadium" };

    private static (MatchEngine Engine, MatchState State) NewMatch(int seed, Side? human = null, bool knockout = false,
        int homeRating = 70, int awayRating = 70, TacticSpec? homeTactic = null, TacticSpec? awayTactic = null)
    {
        var engine = new MatchEngine(new Random(seed), Catalog);
        var s = engine.Create(new MatchSetup(T(1), T(2), homeRating, awayRating, human, knockout, "Test"));
        engine.Start(s, homeTactic ?? TacticRules.Default(), awayTactic ?? TacticRules.Default());
        return (engine, s);
    }

    /// <summary>Puts a specific card in the given cell (keeps the rest of the grid).</summary>
    private static void Put(MatchState s, int cell, string card, Side? owner = null) =>
        s.Table[cell] = new TableCell { Card = card, Owner = owner };

    [Fact]
    public void Grid_Has16Neutral_4PerTeam_AndHalfTime_SecondHalfHasFullTime()
    {
        var (_, s) = NewMatch(1);
        Assert.Equal(25, s.Table.Count);
        Assert.Equal(16, s.Table.Count(c => c.Owner == null && c.Card != Cards.HalfTime));
        Assert.Equal(4, s.Table.Count(c => c.Owner == Side.Home));
        Assert.Equal(4, s.Table.Count(c => c.Owner == Side.Away));
        Assert.Single(s.Table, c => c.Card == Cards.HalfTime);
        Assert.DoesNotContain(s.Table, c => c.Card == Cards.FullTime);
        Assert.Single(s.SecondHalfTable, c => c.Card == Cards.FullTime);
        Assert.DoesNotContain(s.SecondHalfTable, c => c.Card == Cards.HalfTime);
        Assert.Equal([Conditions.Home], s.Conditions); // weather and derby are switched off for now
    }

    [Fact]
    public void Composition_AppliesConditionsAndSummedModifiers_WithinLimits()
    {
        var home = new TacticSpec { Modifiers = ["MOD_GOAL_UP2"] };
        var away = new TacticSpec { Modifiers = ["MOD_GOAL_DOWN1", "MOD_CORNER_UP1"] };
        var c = TacticRules.Composition(Catalog, [Conditions.Home, Conditions.Rain], home, away);
        Assert.Equal(16, c.Values.Sum());
        Assert.Equal(2, c[Cards.Goal]);   // 1 + 2 - 1
        Assert.Equal(2, c[Cards.Miss]);   // rain
        Assert.Equal(1, c[Cards.Pass]);   // 4 - rain - goal - corner
        Assert.Equal(2, c[Cards.Corner]);

        var greedy = new TacticSpec { Modifiers = ["MOD_GOAL_UP2", "MOD_PENALTY_UP1"] };
        var c2 = TacticRules.Composition(Catalog, [], greedy, greedy);
        Assert.True(c2[Cards.Goal] <= 5);
        Assert.True(c2[Cards.Pass] >= 1);
    }

    [Fact]
    public void Hints_CountOnlyFaceDownDangerousNeutralCards()
    {
        var (_, s) = NewMatch(2);
        for (var i = 0; i < 25; i++) Put(s, i, Cards.Pass);
        Put(s, 0, Cards.Tackle);
        Put(s, 6, Cards.Miss);
        Put(s, 7, Cards.Tackle, Side.Away); // team card — not counted
        Put(s, 8, Cards.Goal);              // goals are not counted
        var (rows, cols) = MatchEngine.Hints(s);
        Assert.Equal([1, 1, 0, 0, 0], rows);
        Assert.Equal([1, 1, 0, 0, 0], cols);
    }

    [Fact]
    public void ThreePasses_GiveBigChance()
    {
        var (engine, s) = NewMatch(3, Side.Home);
        s.Turn = Side.Home;
        Put(s, 0, Cards.Pass); Put(s, 1, Cards.Pass); Put(s, 2, Cards.Pass);
        engine.FlipHuman(s, 0);
        engine.FlipHuman(s, 1);
        Assert.Equal(2, s.Home.PassStreak);
        engine.FlipHuman(s, 2);
        Assert.Equal(MatchPhase.Decision, s.Phase);
        Assert.Equal(DecisionKind.BigChance, s.Decision!.Kind);
    }

    [Fact]
    public void OpponentCard_IsStolen_TheEffectGoesToTheFlipper()
    {
        var (engine, s) = NewMatch(30, Side.Home);
        s.Turn = Side.Home;
        Put(s, 0, Cards.Captain, Side.Away); Put(s, 1, Cards.Tackle); Put(s, 2, Cards.BigChanceCard, Side.Away);
        engine.FlipHuman(s, 0); // stolen Captain protects the thief
        Assert.True(s.Home.CaptainShield);
        Assert.Equal(1, s.Home.Stats.CardsStolen);
        engine.FlipHuman(s, 1);
        Assert.Equal(Side.Home, s.Turn);
        engine.FlipHuman(s, 2); // stolen Big Chance: the thief shoots
        Assert.Equal(DecisionKind.BigChance, s.Decision!.Kind);
        Assert.Equal(Side.Home, s.Decision.Attacker);
    }

    [Fact]
    public void OnlyOneOwnCardPerTurn_UntilTheBallIsLost()
    {
        var (engine, s) = NewMatch(31, Side.Home);
        s.Turn = Side.Home;
        Put(s, 0, Cards.LongShot, Side.Home); Put(s, 1, Cards.Captain, Side.Home); Put(s, 2, Cards.Tackle); Put(s, 3, Cards.Tackle);
        s.Home.CaptainShield = false;
        engine.FlipHuman(s, 0);
        if (s.Turn != Side.Home) return; // the long shot went in — kick-off for the opponent
        Assert.True(s.Home.OwnCardUsedThisTurn);
        Assert.Throws<GameRuleException>(() => engine.FlipHuman(s, 1));
        engine.FlipHuman(s, 2); // Tackle: the ball is lost, the turn ends
        Assert.Equal(Side.Away, s.Turn);
        Assert.False(s.Home.OwnCardUsedThisTurn);
    }

    [Fact]
    public void View_NeverSendsTheComputersCardPositions()
    {
        var (engine, s) = NewMatch(32, Side.Home);
        var view = JsonSerializer.Serialize(MatchService.View(new Match { Id = 1 }, s, Catalog));
        using var doc = JsonDocument.Parse(view);
        var cells = doc.RootElement.GetProperty("table").EnumerateArray().ToList();
        Assert.Equal(4, cells.Count(c => c.GetProperty("owner").GetString() == "Home"));
        Assert.DoesNotContain(cells, c => c.GetProperty("owner").GetString() == "Away");
        Assert.Equal(4, cells.Count(c => c.GetProperty("mine").GetBoolean()));
        Assert.Equal(4, cells.Count(c => c.GetProperty("card").ValueKind == JsonValueKind.String)); // only the own cards
    }

    [Fact]
    public void OffsideTrap_FlippedByOpponent_LosesTheBall()
    {
        var (engine, s) = NewMatch(4, Side.Home);
        s.Turn = Side.Home;
        Put(s, 0, Cards.OffsideTrap, Side.Away);
        engine.FlipHuman(s, 0);
        Assert.Equal(Side.Away, s.Turn);
    }

    [Fact]
    public void Captain_IgnoresNextTackle_AndWall_TurnsGoalIntoSave()
    {
        var (engine, s) = NewMatch(5, Side.Home);
        s.Turn = Side.Home;
        Put(s, 0, Cards.Captain, Side.Home); Put(s, 1, Cards.Tackle);
        engine.FlipHuman(s, 0);
        engine.FlipHuman(s, 1);
        Assert.Equal(Side.Home, s.Turn);

        s.Away.WallActive = true;
        Put(s, 2, Cards.Goal);
        engine.FlipHuman(s, 2);
        Assert.Equal(0, s.Home.Goals);
        Assert.False(s.Away.WallActive);
        Assert.Equal(Side.Away, s.Turn);
    }

    [Fact]
    public void Save_PassesTheTurn()
    {
        var (engine, s) = NewMatch(6, Side.Home);
        s.Turn = Side.Home;
        Put(s, 0, Cards.Save);
        engine.FlipHuman(s, 0);
        Assert.Equal(Side.Away, s.Turn);
    }

    [Fact]
    public void HandCards_Scout_Burn_Calm_Work()
    {
        var tactic = new TacticSpec { Field = [Cards.Striker, Cards.LongShot, Cards.LongShot, Cards.LongShot], Hand = [Cards.Scout, Cards.Burn, Cards.Calm] };
        var (engine, s) = NewMatch(7, Side.Home, homeTactic: tactic);
        s.Turn = Side.Home;
        Put(s, 0, Cards.Tackle);
        engine.PlayHandHuman(s, Cards.Scout, 0, null);
        Assert.Contains(0, s.Home.Peeks);
        Assert.Throws<GameRuleException>(() => engine.PlayHandHuman(s, Cards.Burn, null, null)); // one per flip
        Put(s, 2, Cards.Pass);
        engine.FlipHuman(s, 2);
        var awayBefore = s.Table.Count(c => c.Available && c.Owner == Side.Away);
        engine.PlayHandHuman(s, Cards.Burn, null, null); // blind: a random card of the opponent
        Assert.Equal(1, s.Table.Count(c => c.Burned));
        Assert.Equal(Side.Away, s.Table.Single(c => c.Burned).Owner);
        Assert.Equal(awayBefore - 1, s.Table.Count(c => c.Available && c.Owner == Side.Away));
        Put(s, 3, Cards.Pass);
        engine.FlipHuman(s, 3);
        engine.PlayHandHuman(s, Cards.Calm, null, null);
        engine.FlipHuman(s, 0); // the scouted Tackle — Calm keeps the ball
        Assert.Equal(Side.Home, s.Turn);
    }

    [Fact]
    public void SecondYellow_IsRed_DiscardsAHandCard_AndStopsBigChances()
    {
        var tactic = new TacticSpec { Field = [Cards.LongShot, Cards.LongShot, Cards.LongShot, Cards.LongShot], Hand = [Cards.Scout, Cards.Calm] };
        var (engine, s) = NewMatch(8, Side.Home, homeTactic: tactic);
        s.Turn = Side.Home;
        Put(s, 0, Cards.YellowCard);
        engine.FlipHuman(s, 0);
        s.Turn = Side.Home;
        Put(s, 1, Cards.YellowCard);
        engine.FlipHuman(s, 1);
        Assert.Equal(DecisionKind.Discard, s.Decision!.Kind);
        engine.DecideHuman(s, 0, null, false);
        Assert.Single(s.Home.Hand);
        Assert.Equal(1, s.Home.Stats.RedCards);
        s.Turn = Side.Home;
        Put(s, 2, Cards.Pass); Put(s, 3, Cards.Pass); Put(s, 4, Cards.Pass);
        engine.FlipHuman(s, 2); engine.FlipHuman(s, 3); engine.FlipHuman(s, 4);
        Assert.Equal(MatchPhase.Play, s.Phase); // no Big Chance with ten men
    }

    [Fact]
    public void HalfTime_ReshufflesAFreshGrid_AndOtherTeamStarts()
    {
        var (engine, s) = NewMatch(9, Side.Home);
        s.Turn = s.FirstKickOff;
        var flipper = s.Turn;
        var idx = s.Table.FindIndex(c => c.Card == Cards.HalfTime);
        if (flipper == Side.Home) engine.FlipHuman(s, idx);
        else { s.Turn = Side.Home; engine.FlipHuman(s, idx); }
        Assert.Equal(2, s.Half);
        Assert.Equal(s.FirstKickOff.Other(), s.Turn);
        Assert.All(s.Table, c => Assert.True(c.Available));
    }

    [Fact]
    public void FullTime_NeverBeforeHalfTime_AndKnockoutsAlwaysHaveAWinner()
    {
        for (var i = 0; i < 400; i++)
        {
            var engine = new MatchEngine(new Random(100 + i), Catalog);
            var s = engine.Create(new MatchSetup(T(1), T(2), 60 + i % 30, 90 - i % 30, null, i % 2 == 0, "Test"));
            engine.SimulateToEnd(s);
            Assert.Equal(MatchPhase.Finished, s.Phase);
            var ht = s.Events.FindIndex(e => e.Card == Cards.HalfTime);
            var ft = s.Events.FindIndex(e => e.Card == Cards.FullTime);
            Assert.True(ht >= 0 && ft > ht);
            if (s.IsKnockout) Assert.NotNull(s.WinnerTeamId());
        }
    }

    [Fact]
    public void ComputerTactics_AreValid_ForAllRatings()
    {
        var rng = new Random(10);
        for (var rating = 45; rating <= 95; rating += 5)
        foreach (var home in new[] { true, false })
        {
            var t = TacticRules.BuildAi(Catalog, rating, 70, home, rng);
            var errors = TacticRules.Validate(t, Catalog, Rules.TacticPoints(rating, home), Rules.HandSize(rating), null);
            Assert.True(errors.Count == 0, $"rating {rating}: {string.Join(" ", errors)}");
        }
    }

    [Fact]
    public void HumanCanPlayAWholeMatch_WithAutoDecisions()
    {
        var (engine, s) = NewMatch(11, Side.Away, knockout: true);
        var guard = 0;
        while (s.Phase != MatchPhase.Finished && guard++ < 500)
        {
            if (s.Phase == MatchPhase.Decision)
            {
                if (s.Decision!.Kind == DecisionKind.Discard && s.Decision.Attacker != Side.Away) engine.AiStep(s);
                else engine.DecideHuman(s, null, null, true);
            }
            else if (s.Turn == Side.Away)
                engine.FlipHuman(s, Enumerable.Range(0, s.Table.Count).First(i => s.Table[i].Available && MatchEngine.CanFlipOwn(s, Side.Away, i)));
            else engine.AiStep(s);
        }
        Assert.Equal(MatchPhase.Finished, s.Phase);
        Assert.NotNull(s.WinnerTeamId());
    }

    /// <summary>Balance check: thousands of computer-vs-computer matches.</summary>
    [Fact]
    public void Balance_AroundFourGoals_StrongTeamWinsMoreOften()
    {
        double goals = 0, flips = 0, zeroZero = 0, stolen = 0, trapsSprung = 0;
        var sources = new Dictionary<string, int>();
        const int n = 4000;
        for (var i = 0; i < n; i++)
        {
            var engine = new MatchEngine(new Random(1000 + i), Catalog);
            var s = engine.Create(new MatchSetup(T(1), T(2), 70, 70, null, false, "Test"));
            engine.SimulateToEnd(s);
            goals += s.Home.Goals + s.Away.Goals;
            flips += s.Home.Stats.Flips + s.Away.Stats.Flips;
            if (s.Home.Goals + s.Away.Goals == 0) zeroZero++;
            stolen += s.Home.Stats.CardsStolen + s.Away.Stats.CardsStolen;
            trapsSprung += s.Events.Count(e => e.Type == "lost" && e.Card == Cards.OffsideTrap);
            foreach (var t in new[] { s.Home, s.Away })
                foreach (var (k, v) in t.Stats.GoalSources) sources[k] = sources.GetValueOrDefault(k) + v;
        }

        int strongWins = 0, weakWins = 0;
        for (var i = 0; i < 2000; i++)
        {
            var engine = new MatchEngine(new Random(9000 + i), Catalog);
            var strongHome = i % 2 == 0;
            var s = engine.Create(new MatchSetup(T(1), T(2), strongHome ? 85 : 58, strongHome ? 58 : 85, null, false, "Test"));
            engine.SimulateToEnd(s);
            var strong = strongHome ? s.Home.Goals : s.Away.Goals;
            var weak = strongHome ? s.Away.Goals : s.Home.Goals;
            if (strong > weak) strongWins++;
            if (weak > strong) weakWins++;
        }

        output.WriteLine($"avg goals {goals / n:F2}, avg flips {flips / n:F1}, 0:0 {zeroZero / n:P1}");
        output.WriteLine("goal sources: " + string.Join(", ", sources.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {(double)kv.Value / n:F2}")));
        output.WriteLine($"steals per match {stolen / n:F2}, offside traps sprung per match {trapsSprung / n:F2}");
        output.WriteLine($"strong (85) vs weak (58): strong wins {strongWins / 20.0:F1}%, weak wins {weakWins / 20.0:F1}%");
        Assert.InRange(goals / n, 3.0, 5.0);
        Assert.True(strongWins > weakWins);

        // how strong is the Offside Trap? the same team with a Long Shot vs with an Offside Trap in its place
        foreach (var trap in new[] { false, true })
        {
            int wins = 0, losses = 0, sprung = 0;
            for (var i = 0; i < 2000; i++)
            {
                var tactic = new TacticSpec { Field = [Cards.Striker, Cards.LongShot, trap ? Cards.OffsideTrap : Cards.LongShot, Cards.Captain], Hand = [Cards.Scout] };
                var engine = new MatchEngine(new Random(20000 + i), Catalog);
                var s = engine.Create(new MatchSetup(T(1), T(2), 75, 75, null, false, "Test"));
                engine.Start(s, tactic, TacticRules.Default());
                engine.SimulateToEnd(s);
                if (s.Home.Goals > s.Away.Goals) wins++;
                if (s.Home.Goals < s.Away.Goals) losses++;
                sprung += s.Events.Count(e => e.Type == "lost" && e.Card == Cards.OffsideTrap);
            }
            output.WriteLine($"{(trap ? "with Offside Trap" : "with Long Shot   ")}: wins {wins / 20.0:F1}%, losses {losses / 20.0:F1}%, traps sprung per match {sprung / 2000.0:F2}");
        }
    }
}
