using DugoutDeck.Api.Data.Entities;

namespace DugoutDeck.Api.Game;

/// <summary>
/// Card tiers (version 1.2): every copy of a card is Normal, Rare, Ultra Rare or Legendary. Cards with a chance get
/// +4 / +8 / +12 percentage points; cards without a chance get an extra use (or a quick play) on Ultra Rare and Legendary.
/// The bonus goes with the card: a thief who steals a Legendary Striker shoots with the Legendary chance.
/// The full table is in docs/rules.md (section "Card tiers").
/// </summary>
public static class Tiers
{
    public const int Normal = 0, Rare = 1, UltraRare = 2, Legendary = 3;
    public static readonly string[] Names = ["Normal", "Rare", "Ultra Rare", "Legendary"];
    public static string Name(int tier) => Names[Math.Clamp(tier, 0, 3)];

    /// <summary>Percentage points added to a chance card per tier.</summary>
    public const int ChanceStep = 4;
    public static int ChanceBonus(int tier) => ChanceStep * Math.Clamp(tier, 0, 3);

    /// <summary>Extra uses for shield-like cards (Captain, Wall, Sweeper, 12th Man, Calm, Press): Ultra +1, Legendary +2.</summary>
    public static int ExtraUses(int tier) => Math.Max(0, Math.Clamp(tier, 0, 3) - 1);

    /// <summary>Ultra Rare and Legendary "quick" hand cards don't use up the one hand card per flip.</summary>
    public static bool QuickPlay(int tier) => tier >= UltraRare;

    /// <summary>Cards that have tiers (everything collectable except the free Squad Player).</summary>
    public static bool HasTiers(string code) => code != Cards.SquadPlayer;

    /// <summary>Which cards get a percentage bonus.</summary>
    public static readonly HashSet<string> ChanceCards =
    [
        Cards.Striker, Cards.LongShot, Cards.SuperSub, Cards.BigChanceCard, Cards.Winger, Cards.TargetMan,
        Cards.Goalkeeper, Cards.Var, Cards.PenaltyTaker
    ];

    /// <summary>Sell price of one copy (the market is always open; selling is cheap on purpose).</summary>
    public static long SellPrice(int tier) => Math.Clamp(tier, 0, 3) switch { 0 => 15_000, 1 => 60_000, 2 => 250_000, _ => 1_000_000 };

    /// <summary>Tier of a card from a pack or the draft: Normal 80%, Rare 16%, Ultra Rare 3.2%, Legendary 0.8% (per mille rolls).</summary>
    public static int RollPack(Random rng, int atLeast = Normal)
    {
        var roll = rng.Next(1000);
        var tier = roll < 8 ? Legendary : roll < 40 ? UltraRare : roll < 200 ? Rare : Normal;
        if (tier < atLeast)
        {
            // a guaranteed Rare+ card: Rare 80%, Ultra Rare 16%, Legendary 4%
            var r2 = rng.Next(100);
            tier = Math.Max(atLeast, r2 < 4 ? Legendary : r2 < 20 ? UltraRare : Rare);
        }
        return tier;
    }

    /// <summary>Tiers of the computer's cards (and of a club's squad cards) by rating — stronger clubs have better copies.</summary>
    public static int RollForRating(int rating, Random rng)
    {
        var roll = rng.Next(100);
        if (rating >= 78) return roll < 5 ? Legendary : roll < 22 ? UltraRare : roll < 60 ? Rare : Normal;
        if (rating >= 66) return roll < 1 ? Legendary : roll < 8 ? UltraRare : roll < 35 ? Rare : Normal;
        return roll < 2 ? UltraRare : roll < 15 ? Rare : Normal;
    }

    public static int At(List<int>? tiers, int index) => tiers != null && index >= 0 && index < tiers.Count ? Math.Clamp(tiers[index], 0, 3) : Normal;

    /// <summary>A short description of what a tier adds to a card (shown in the collection, the store and the tactic screen).</summary>
    public static string Bonus(string code, int tier)
    {
        if (tier <= Normal || !HasTiers(code)) return "";
        if (ChanceCards.Contains(code))
            return code switch
            {
                Cards.Goalkeeper => $"−{ChanceBonus(tier)}% more for the opponent's shot",
                Cards.Var => $"+{ChanceBonus(tier)}% to disallow the goal",
                _ => $"+{ChanceBonus(tier)}% chance"
            };
        if (tier < UltraRare) return "";
        var n = ExtraUses(tier);
        return code switch
        {
            Cards.Captain => $"ignores {1 + n} Tackles",
            Cards.Wall => $"stops {1 + n} goals",
            Cards.Sweeper => $"ignores {1 + n} Offside/Save cards",
            Cards.TwelfthMan => $"ignores {1 + n} dangerous cards",
            Cards.Calm => $"lasts {1 + n} cards",
            Cards.Press => $"works {1 + n} times",
            Cards.CounterAttack => $"{2 + n} extra cards",
            Cards.Playmaker or Cards.Scout => $"+{n} free Scout{(n == 1 ? "" : "s")}",
            Cards.OffsideTrap or Cards.Decoy => $"a free Scout{(n == 2 ? " ×2" : "")} when it springs",
            Cards.Burn => tier == Legendary ? "burns 2 cards, quick play" : "quick play",
            Cards.Swap or Cards.Analysis or Cards.Substitution or Cards.Marking => "quick play (does not use your hand card for the flip)",
            _ => ""
        };
    }
}
