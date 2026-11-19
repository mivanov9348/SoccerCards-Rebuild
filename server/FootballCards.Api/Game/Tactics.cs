using FootballCards.Api.Data.Entities;

namespace FootballCards.Api.Game;

/// <summary>Card definitions by code (loaded from the database).</summary>
public class CardCatalog(IEnumerable<CardDefinition> cards)
{
    public Dictionary<string, CardDefinition> ByCode { get; } = cards.ToDictionary(c => c.Code);

    public CardDefinition? Get(string code) => ByCode.GetValueOrDefault(code);
    public int Cost(string code) => Get(code)?.Cost ?? 0;
    public CardKind? Kind(string code) => Get(code)?.Kind;
    public IEnumerable<CardDefinition> OfKind(CardKind kind) => ByCode.Values.Where(c => c.Kind == kind);
}

/// <summary>Tactic rules: costs, validation, the grid composition and the computer's tactic.</summary>
public static class TacticRules
{
    /// <summary>What each neutral modifier changes: (card, delta).</summary>
    public static readonly Dictionary<string, (string Card, int Delta)> ModifierEffects = new()
    {
        ["MOD_GOAL_UP1"] = (Cards.Goal, 1), ["MOD_GOAL_UP2"] = (Cards.Goal, 2),
        ["MOD_GOAL_DOWN1"] = (Cards.Goal, -1), ["MOD_GOAL_DOWN2"] = (Cards.Goal, -2),
        ["MOD_CORNER_UP1"] = (Cards.Corner, 1), ["MOD_CORNER_UP2"] = (Cards.Corner, 2),
        ["MOD_CORNER_DOWN1"] = (Cards.Corner, -1), ["MOD_CORNER_DOWN2"] = (Cards.Corner, -2),
        ["MOD_TACKLE_UP1"] = (Cards.Tackle, 1), ["MOD_PENALTY_UP1"] = (Cards.Penalty, 1),
    };

    public static int Cost(TacticSpec t, CardCatalog catalog) =>
        t.Field.Sum(catalog.Cost) + t.Hand.Sum(catalog.Cost) + t.Modifiers.Sum(catalog.Cost);

    /// <summary>Validates a tactic. <paramref name="owned"/> returns how many copies of a card the player owns (null = unlimited).</summary>
    public static List<string> Validate(TacticSpec t, CardCatalog catalog, int budget, int handSize, Func<string, int>? owned)
    {
        var errors = new List<string>();
        if (t.Field.Count != Rules.FieldCardsPerTeam) errors.Add($"Pick exactly {Rules.FieldCardsPerTeam} field cards.");
        if (t.Hand.Count > handSize) errors.Add($"You can take at most {handSize} hand card(s).");
        if (t.Modifiers.Count > Rules.MaxModifiers) errors.Add($"You can take at most {Rules.MaxModifiers} modifiers.");
        if (t.Field.Any(c => catalog.Kind(c) != CardKind.Field)) errors.Add("Unknown field card.");
        if (t.Hand.Any(c => catalog.Kind(c) != CardKind.Hand)) errors.Add("Unknown hand card.");
        if (t.Modifiers.Any(c => catalog.Kind(c) != CardKind.Modifier)) errors.Add("Unknown modifier.");
        var cost = Cost(t, catalog);
        if (cost > budget) errors.Add($"The tactic costs {cost} points but you only have {budget}.");
        if (owned != null)
        {
            foreach (var g in t.Field.Concat(t.Hand).Where(c => c != Cards.SquadPlayer).GroupBy(c => c))
                if (g.Count() > owned(g.Key))
                    errors.Add($"You only own {owned(g.Key)} × {Cards.Name(g.Key)}.");
        }
        return errors;
    }

    public static Dictionary<string, int> BaseComposition(CardCatalog catalog) =>
        catalog.OfKind(CardKind.Neutral).Where(c => c.CopiesInDeck > 0).ToDictionary(c => c.Code, c => c.CopiesInDeck);

    /// <summary>Neutral cards for each half: base set → conditions → both teams' modifiers (summed), within limits.</summary>
    public static Dictionary<string, int> Composition(CardCatalog catalog, IEnumerable<string> conditions, TacticSpec home, TacticSpec away)
    {
        var c = BaseComposition(catalog);
        foreach (var cond in conditions)
        {
            switch (cond)
            {
                case Conditions.Rain: Convert(c, Cards.Pass, Cards.Miss, 1); break;
                case Conditions.Wind: Convert(c, Cards.Pass, Cards.Corner, 1); break;
                case Conditions.Derby: Convert(c, Cards.Tackle, Cards.YellowCard, 1); break;
            }
        }

        var deltas = new Dictionary<string, int>();
        foreach (var m in home.Modifiers.Concat(away.Modifiers))
            if (ModifierEffects.TryGetValue(m, out var e))
                deltas[e.Card] = deltas.GetValueOrDefault(e.Card) + e.Delta;
        // removals first (they free Pass cards), then additions in a fixed order — the result never depends on who chose what
        string[] order = [Cards.Goal, Cards.Penalty, Cards.Corner, Cards.Tackle];
        foreach (var card in order.Where(k => deltas.GetValueOrDefault(k) < 0)) Convert(c, card, Cards.Pass, -deltas[card]);
        foreach (var card in order.Where(k => deltas.GetValueOrDefault(k) > 0)) Convert(c, Cards.Pass, card, deltas[card]);
        return c.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    /// <summary>Turns up to <paramref name="n"/> cards of one kind into another, respecting the limits.</summary>
    private static void Convert(Dictionary<string, int> c, string from, string to, int n)
    {
        for (var i = 0; i < n; i++)
        {
            var fromCount = c.GetValueOrDefault(from);
            var toCount = c.GetValueOrDefault(to);
            var fromMin = Rules.NeutralLimits.TryGetValue(from, out var fl) ? fl.Min : 0;
            var toMax = Rules.NeutralLimits.TryGetValue(to, out var tl) ? tl.Max : 6;
            if (fromCount <= fromMin || toCount >= toMax) return;
            c[from] = fromCount - 1;
            c[to] = toCount + 1;
        }
    }

    public static List<string> RollConditions(string homeCity, string awayCity, Random rng)
    {
        var list = new List<string> { Conditions.Home };
        var w = rng.Next(100);
        list.Add(w < 20 ? Conditions.Rain : w < 40 ? Conditions.Wind : Conditions.Fine);
        if (!string.IsNullOrWhiteSpace(homeCity) && homeCity == awayCity) list.Add(Conditions.Derby);
        return list;
    }

    /// <summary>Card rarities a computer team can use, by rating.</summary>
    public static bool AiCanUse(CardDefinition c, int rating) => c.Rarity switch
    {
        CardRarity.Epic => rating >= 76,
        CardRarity.Rare => rating >= 66,
        _ => true
    };

    /// <summary>The computer builds a tactic from its rating, the opponent and the conditions.</summary>
    public static TacticSpec BuildAi(CardCatalog catalog, int rating, int opponentRating, bool home, Random rng)
    {
        var budget = Rules.TacticPoints(rating, home);
        var handSize = Rules.HandSize(rating);
        var style = rating - opponentRating >= 4 || (home && rating >= opponentRating) ? "attack"
            : opponentRating - rating >= 4 ? "defend" : "balanced";

        string[] fieldPriority = style switch
        {
            "attack" => [Cards.Striker, Cards.BigChanceCard, Cards.CounterAttack, Cards.LongShot, Cards.Playmaker, Cards.Captain, Cards.LongShot],
            "defend" => [Cards.Wall, Cards.OffsideTrap, Cards.Captain, Cards.Striker, Cards.LongShot, Cards.LongShot],
            _ => [Cards.Striker, Cards.BigChanceCard, Cards.Captain, Cards.LongShot, Cards.Playmaker, Cards.Wall, Cards.LongShot]
        };
        string[] handPriority = [Cards.Scout, Cards.Calm, Cards.Burn, Cards.Press, Cards.Swap];
        string[] modPriority = style switch
        {
            "attack" => ["MOD_GOAL_UP1", "MOD_CORNER_UP1"],
            "defend" => ["MOD_GOAL_DOWN1", "MOD_CORNER_DOWN1"],
            _ => ["MOD_CORNER_UP1", "MOD_GOAL_UP1"]
        };

        // a little variety between matches
        var fieldList = fieldPriority.ToList();
        for (var i = 0; i < fieldList.Count - 1; i++)
            if (rng.Next(3) == 0) (fieldList[i], fieldList[i + 1]) = (fieldList[i + 1], fieldList[i]);

        bool Usable(string code) => catalog.Get(code) is { } d && AiCanUse(d, rating);

        var t = new TacticSpec();
        var left = budget - handSize; // keep at least 1 point per hand card
        foreach (var code in fieldList)
        {
            if (t.Field.Count == Rules.FieldCardsPerTeam) break;
            if (!Usable(code) || catalog.Cost(code) > left - (Rules.FieldCardsPerTeam - t.Field.Count - 1)) continue;
            t.Field.Add(code);
            left -= catalog.Cost(code);
        }
        while (t.Field.Count < Rules.FieldCardsPerTeam)
        {
            var code = left >= catalog.Cost(Cards.LongShot) + (Rules.FieldCardsPerTeam - t.Field.Count - 1) ? Cards.LongShot : Cards.SquadPlayer;
            t.Field.Add(code);
            left -= catalog.Cost(code);
        }

        left += handSize;
        foreach (var code in handPriority)
        {
            if (t.Hand.Count == handSize) break;
            if (!Usable(code) || catalog.Cost(code) > left) continue;
            t.Hand.Add(code);
            left -= catalog.Cost(code);
        }
        foreach (var code in modPriority)
        {
            if (t.Modifiers.Count == Rules.MaxModifiers) break;
            if (catalog.Get(code) == null || catalog.Cost(code) > left) continue;
            t.Modifiers.Add(code);
            left -= catalog.Cost(code);
        }
        return t;
    }

    /// <summary>A simple default tactic (used e.g. for friendly matches before the player saves their own).</summary>
    public static TacticSpec Default() => new()
    {
        Field = [Cards.Striker, Cards.BigChanceCard, Cards.LongShot, Cards.Captain],
        Hand = [Cards.Scout],
        Modifiers = []
    };
}
