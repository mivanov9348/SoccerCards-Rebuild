namespace DugoutDeck.Api.Game;

/// <summary>
/// The computer player. All backs are grey, so it only knows: where its own team cards are, the cards it has scouted,
/// the face-up cards and the row/column hints. It does NOT know where the opponent's team cards are.
/// Its skill depends on the team rating: weak teams often flip at random, strong teams almost always pick the best card.
/// </summary>
public class MatchAi(Random rng)
{
    private static readonly double[] RandomFlipChance = [0.5, 0.2, 0.04];

    /// <summary>Value of flipping one of its own team cards now. The Offside Trap is worth more left in the grid.</summary>
    private static double OwnValue(TeamMatchState team, string card) => card switch
    {
        Cards.Striker => 0.75,
        Cards.BigChanceCard => 0.7,
        Cards.CounterAttack => 0.6,
        Cards.LongShot => 0.55,
        Cards.Playmaker => 0.5,
        Cards.Captain => team.CaptainShield ? 0.3 : 0.5,
        Cards.Wall => 0.45,
        Cards.SquadPlayer => 0.36,
        Cards.OffsideTrap => 0.05,
        _ => 0.36
    };

    /// <summary>Value of a card the computer has seen (scouted) for the team that flips it.</summary>
    private static double KnownValue(TeamMatchState team, TableCell c, Side side)
    {
        if (c.Owner == side) return OwnValue(team, c.Card);
        if (c.Owner == side.Other()) return c.Card == Cards.OffsideTrap ? -0.6 : 0.6; // a steal
        return c.Card switch
        {
            Cards.Goal => 1.0,
            Cards.Penalty => 0.7,
            Cards.Corner or Cards.FreeKick => 0.4,
            Cards.Pass => 0.3,
            Cards.HalfTime or Cards.FullTime => 0.0,
            _ => -0.6
        };
    }

    /// <summary>A cell the computer cannot tell apart from the others: grey back, not its own, not scouted.</summary>
    private static bool Unknown(MatchState s, Side side, int i) =>
        s.Table[i].Available && s.Table[i].Owner != side && !s.Team(side).Peeks.Contains(i);

    /// <summary>Estimated chance that an unknown card is dangerous, from the row and column hints.</summary>
    private static double Danger(MatchState s, Side side, int index, int[] rows, int[] cols)
    {
        var n = Rules.GridSize;
        int r = index / n, c = index % n;
        int rowUnknown = 0, colUnknown = 0;
        for (var i = 0; i < s.Table.Count; i++)
        {
            if (!Unknown(s, side, i)) continue;
            if (i / n == r) rowUnknown++;
            if (i % n == c) colUnknown++;
        }
        // a zero hint: no dangerous neutral card there — but an opponent's Offside Trap could still hide in it
        if (rows[r] == 0 || cols[c] == 0) return 0.03;
        var pr = rowUnknown == 0 ? 0 : (double)rows[r] / rowUnknown;
        var pc = colUnknown == 0 ? 0 : (double)cols[c] / colUnknown;
        return (pr + pc) / 2;
    }

    private List<(int Index, double Value, double Danger)> Evaluate(MatchState s, Side side)
    {
        var (rows, cols) = MatchEngine.Hints(s);
        var team = s.Team(side);
        var list = new List<(int, double, double)>();
        for (var i = 0; i < s.Table.Count; i++)
        {
            var cell = s.Table[i];
            if (!cell.Available || !MatchEngine.CanFlipOwn(s, side, i)) continue;
            if (cell.Owner == side) { list.Add((i, OwnValue(team, cell.Card), 0)); continue; }
            if (team.Peeks.Contains(i))
            {
                var bad = (cell.Owner == null && Cards.Dangerous.Contains(cell.Card)) || (cell.Owner != null && cell.Card == Cards.OffsideTrap);
                list.Add((i, KnownValue(team, cell, side), bad ? 1 : 0));
                continue;
            }
            var p = Danger(s, side, i, rows, cols);
            list.Add((i, 0.35 * (1 - p) - 0.6 * p, p));
        }
        return list;
    }

    public int ChooseCell(MatchState s, Side side)
    {
        var options = Evaluate(s, side);
        if (options.Count == 0) throw new InvalidOperationException("No face-down cards left.");
        var level = s.Team(side).AiLevel;
        if (rng.NextDouble() < RandomFlipChance[level]) return options[rng.Next(options.Count)].Index;
        var noise = level == 2 ? 0.02 : 0.15;
        return options.OrderByDescending(o => o.Value + rng.NextDouble() * noise).First().Index;
    }

    /// <summary>Decides whether to play a hand card (or a free Scout) before flipping.</summary>
    public (string? Card, int? Cell, int? Cell2) ChooseHandCard(MatchState s, Side side)
    {
        var team = s.Team(side);
        var options = Evaluate(s, side);
        if (options.Count == 0) return (null, null, null);
        var best = options.OrderByDescending(o => o.Value).First();
        var unknown = options.Where(o => Unknown(s, side, o.Index)).ToList();
        var oppLeft = s.Table.Count(c => c.Available && c.Owner == side.Other()); // public: 4 per team minus the flipped/burned ones

        // a free scout from the Playmaker is always used
        if (team.FreeScouts > 0 && unknown.Count > 0)
            return (Cards.Scout, unknown.OrderByDescending(o => o.Value).First().Index, null);

        if (team.HandPlayedThisTurn || team.Hand.Count == 0) return (null, null, null);
        var level = team.AiLevel;

        // weak teams play their cards at random moments
        if (level == 0)
        {
            if (rng.Next(100) >= 20) return (null, null, null);
            var card = team.Hand[rng.Next(team.Hand.Count)];
            return Target(s, side, card, unknown, oppLeft);
        }

        var careful = level == 2 ? 1.0 : 0.6;
        if (rng.NextDouble() > careful) return (null, null, null);

        // the blind Burn is best early, while the opponent still has most of its cards in the grid
        if (team.Hand.Contains(Cards.Burn) && oppLeft >= 3) return (Cards.Burn, null, null);
        if (best.Value >= 0.3) return (null, null, null); // a good card is available anyway
        if (team.Hand.Contains(Cards.Scout) && unknown.Count > 0)
            return (Cards.Scout, unknown.OrderByDescending(o => o.Value).First().Index, null);
        if (team.Hand.Contains(Cards.Calm) && best.Danger > 0.3) return (Cards.Calm, null, null);
        if (team.Hand.Contains(Cards.Press) && best.Danger > 0.4) return (Cards.Press, null, null);
        return (null, null, null);
    }

    private (string?, int?, int?) Target(MatchState s, Side side, string card, List<(int Index, double Value, double Danger)> unknown, int oppLeft)
    {
        var avail = Enumerable.Range(0, s.Table.Count).Where(i => s.Table[i].Available).ToList();
        switch (card)
        {
            case Cards.Scout:
                return unknown.Count > 0 ? (card, unknown[rng.Next(unknown.Count)].Index, null) : (null, null, null);
            case Cards.Swap:
                if (avail.Count < 2) return (null, null, null);
                var a = avail[rng.Next(avail.Count)];
                var b = avail.Where(i => i != a).ElementAt(rng.Next(avail.Count - 1));
                return (card, a, b);
            case Cards.Burn:
                return oppLeft > 0 ? (card, null, null) : (null, null, null);
            default:
                return (card, null, null);
        }
    }

    public int MiniChoice() => rng.Next(3);

    /// <summary>Timing for penalties: strong computers hit the green zone more often.</summary>
    public double Timing(int level)
    {
        var spread = level switch { 2 => 0.2, 1 => 0.35, _ => 0.5 };
        return 0.5 + (rng.NextDouble() * 2 - 1) * spread;
    }

    /// <summary>After a red card: give up the cheapest hand card.</summary>
    public int DiscardChoice(TeamMatchState team, CardCatalog catalog)
    {
        if (team.Hand.Count == 0) return 0;
        var cheapest = team.Hand.Select((c, i) => (i, catalog.Cost(c))).OrderBy(x => x.Item2).First();
        return cheapest.i;
    }
}
