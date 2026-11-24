using DugoutDeck.Api.Data.Entities;
using DugoutDeck.Api.Game;

namespace DugoutDeck.Tests;

/// <summary>Version 1.2: card tiers (Normal / Rare / Ultra Rare / Legendary) in the match, the tactic and the packs.</summary>
public class Release12Tests
{
    private static (MatchEngine Engine, MatchState S) New(int seed, TacticSpec? home = null)
    {
        var engine = new MatchEngine(new Random(seed), MatchEngineTestsCatalog.Get());
        var s = engine.Create(new MatchSetup(new Team { Id = 1, Name = "A", City = "X" }, new Team { Id = 2, Name = "B", City = "Y" }, 70, 70, Side.Home, false, "T"));
        engine.Start(s, home ?? TacticRules.Default(), null);
        s.Turn = Side.Home;
        for (var i = 0; i < s.Table.Count; i++) s.Table[i] = new TableCell { Card = Cards.Pass };
        return (engine, s);
    }

    private static void Put(MatchState s, int cell, string card, Side? owner = null, int tier = 0) =>
        s.Table[cell] = new TableCell { Card = card, Owner = owner, Tier = tier };

    [Fact]
    public void UltraRareCaptain_IgnoresTwoTackles_AndATheftKeepsTheTier()
    {
        var (e, s) = New(1);
        Put(s, 0, Cards.Captain, Side.Away, Tiers.UltraRare); // the opponent's Ultra Rare Captain…
        Put(s, 1, Cards.Tackle); Put(s, 2, Cards.Tackle); Put(s, 3, Cards.Tackle);
        e.FlipHuman(s, 0); // …stolen: the thief gets both uses
        Assert.True(s.Home.CaptainShield);
        Assert.Equal(1, s.Home.CaptainExtra);
        e.FlipHuman(s, 1);
        e.FlipHuman(s, 2);
        Assert.Equal(Side.Home, s.Turn); // two Tackles ignored
        e.FlipHuman(s, 3);
        Assert.Equal(Side.Away, s.Turn); // the third one is not
        Assert.Contains(s.Home.Played, p => p.Code == Cards.Captain && p.How == "stolen" && p.Tier == Tiers.UltraRare);
    }

    [Fact]
    public void LegendaryCounterAttack_GivesFourFreeCards()
    {
        var (e, s) = New(2);
        Put(s, 0, Cards.CounterAttack, Side.Home, Tiers.Legendary);
        e.FlipHuman(s, 0);
        Assert.Equal(4, s.Home.CounterFlips);
    }

    [Fact]
    public void QuickHandCards_UltraRareSwapLetsAnotherHandCardFollow_LegendaryBurnBurnsTwo()
    {
        var home = new TacticSpec
        {
            Field = [Cards.LongShot, Cards.LongShot, Cards.LongShot, Cards.LongShot],
            Hand = [Cards.Swap, Cards.Burn], HandTiers = [Tiers.UltraRare, Tiers.Legendary]
        };
        var (e, s) = New(3, home);
        Put(s, 5, Cards.Striker, Side.Away); Put(s, 6, Cards.Wall, Side.Away); Put(s, 7, Cards.Captain, Side.Away);
        e.PlayHandHuman(s, Cards.Swap, 0, 1);
        Assert.False(s.Home.HandPlayedThisTurn); // quick play
        e.PlayHandHuman(s, Cards.Burn, null, null);
        Assert.Equal(2, s.Table.Count(c => c.Burned));
        Assert.Equal(2, s.Home.Stats.CardsBurned);
        Assert.Empty(s.Home.Hand);
        Assert.Empty(s.Home.HandTiers);
    }

    [Fact]
    public void LegendaryStriker_ScoresMoreOftenThanANormalOne()
    {
        double Rate(int tier)
        {
            var goals = 0;
            const int n = 3000;
            for (var i = 0; i < n; i++)
            {
                var (e, s) = New(1000 + i);
                Put(s, 0, Cards.Striker, Side.Home, tier);
                e.FlipHuman(s, 0);
                goals += s.Home.Goals;
            }
            return goals * 100.0 / n;
        }
        var normal = Rate(Tiers.Normal);
        var legendary = Rate(Tiers.Legendary);
        Assert.InRange(normal, 31, 39);        // 35%
        Assert.InRange(legendary, 43, 51);     // 35 + 12 = 47%
    }

    [Fact]
    public void Validation_CountsCopiesPerTier()
    {
        var catalog = MatchEngineTestsCatalog.Get();
        var owned = new Dictionary<(string, int), int> { [(Cards.Striker, Tiers.Rare)] = 1, [(Cards.LongShot, 0)] = 3 };
        int Owned(string c, int t) => owned.GetValueOrDefault((c, t));
        var ok = new TacticSpec { Field = [Cards.Striker, Cards.LongShot, Cards.LongShot, Cards.LongShot], FieldTiers = [Tiers.Rare, 0, 0, 0] };
        Assert.Empty(TacticRules.Validate(ok, catalog, 14, 2, Owned));
        var wrongTier = new TacticSpec { Field = [Cards.Striker, Cards.LongShot, Cards.LongShot, Cards.LongShot] }; // asks for a Normal Striker
        Assert.Contains(TacticRules.Validate(wrongTier, catalog, 14, 2, Owned), e => e.Contains("Normal Striker"));
        // friendlies: no tiers at all
        Assert.Contains(TacticRules.Validate(ok, catalog, 14, 2, null), e => e.Contains("only used in a career"));
    }

    [Fact]
    public void PackTiers_FollowTheOdds_AndGuaranteesWork()
    {
        var rng = new Random(7);
        var counts = new int[4];
        const int n = 200_000;
        for (var i = 0; i < n; i++) counts[Tiers.RollPack(rng)]++;
        Assert.InRange(counts[0] * 100.0 / n, 79, 81);
        Assert.InRange(counts[1] * 100.0 / n, 15, 17);
        Assert.InRange(counts[2] * 100.0 / n, 2.8, 3.6);
        Assert.InRange(counts[3] * 100.0 / n, 0.6, 1.0);
        for (var i = 0; i < 1000; i++) Assert.True(Tiers.RollPack(rng, Tiers.Rare) >= Tiers.Rare);
    }

    [Fact]
    public void ComputerTactics_HaveTiersOnlyInACareer_StrongClubsBetterCopies()
    {
        var catalog = MatchEngineTestsCatalog.Get();
        var rng = new Random(9);
        var friendly = TacticRules.BuildAi(catalog, 85, 70, true, rng);
        Assert.Empty(friendly.FieldTiers);
        double Avg(int rating) => Enumerable.Range(0, 400).Select(_ => TacticRules.BuildAi(catalog, rating, 70, true, rng, 0, true))
            .SelectMany(t => t.FieldTiers.Concat(t.HandTiers)).Average();
        var weak = Avg(58);
        var strong = Avg(85);
        Assert.True(strong > weak + 0.3, $"strong {strong:F2} vs weak {weak:F2}");
        var t = TacticRules.BuildAi(catalog, 80, 70, true, rng, 0, true);
        Assert.Equal(t.Field.Count, t.FieldTiers.Count);
        Assert.Equal(t.Hand.Count, t.HandTiers.Count);
    }

    /// <summary>Career matches with tiers on both sides (as in background matches): goals must stay reasonable.</summary>
    [Fact]
    public void Balance_WithTiers_GoalsStayAroundFive()
    {
        var output = new List<string>();
        double Goals(int homeRating, int awayRating, bool tiers, out double strongWin, out double weakWin)
        {
            double goals = 0; int sw = 0, ww = 0;
            const int n = 2000;
            for (var i = 0; i < n; i++)
            {
                var engine = new MatchEngine(new Random(5000 + i), MatchEngineTestsCatalog.Get());
                var s = engine.Create(new MatchSetup(new Team { Id = 1, Name = "A" }, new Team { Id = 2, Name = "B" }, homeRating, awayRating, null, false, "T", UseTiers: tiers));
                engine.SimulateToEnd(s);
                goals += s.Home.Goals + s.Away.Goals;
                if (s.Home.Goals > s.Away.Goals) sw++;
                if (s.Away.Goals > s.Home.Goals) ww++;
            }
            strongWin = sw * 100.0 / n; weakWin = ww * 100.0 / n;
            return goals / n;
        }
        var plain = Goals(75, 75, false, out _, out _);
        var tiered = Goals(75, 75, true, out _, out _);
        var strong = Goals(85, 58, true, out var sWin, out var wWin);
        Console.WriteLine($"[tiers] 75 vs 75: {plain:F2} goals without tiers, {tiered:F2} with tiers; 85 vs 58 with tiers: {strong:F2} goals, strong wins {sWin:F1}%, weak {wWin:F1}%");
        Assert.InRange(tiered, 3.5, 6.0);
        Assert.True(sWin > wWin);
    }

    [Fact]
    public void BonusTexts_ChanceCardsGetPercent_OthersGetExtrasOnUltraAndLegendary()
    {
        Assert.Equal("+8% chance", Tiers.Bonus(Cards.Striker, Tiers.UltraRare));
        Assert.Equal("", Tiers.Bonus(Cards.Captain, Tiers.Rare));
        Assert.Equal("ignores 3 Tackles", Tiers.Bonus(Cards.Captain, Tiers.Legendary));
        Assert.Equal("", Tiers.Bonus(Cards.SquadPlayer, Tiers.Legendary));
    }
}
