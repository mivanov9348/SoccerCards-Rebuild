namespace FootballCards.Api.Game;

/// <summary>
/// The computer player. It reads the row/column hints, its own scouting and the colours of the cards.
/// Its skill depends on the team rating: weak teams often flip at random, strong teams almost always pick the best card.
/// </summary>
public class MatchAi(Random rng)
{
    private static readonly double[] RandomFlipChance = [0.5, 0.2, 0.04];

    /// <summary>Value of a known card for the team that flips it.</summary>
    private static double KnownValue(TableCell c, Side side)
    {
        if (c.Owner == side) return 0.6;
        if (c.Owner == side.Other()) return c.Card == Cards.OffsideTrap ? -0.6 : 0.45;
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

    /// <summary>Estimated chance that a grey card is dangerous, from the row and column hints.</summary>
    private static double Danger(MatchState s, int index, int[] rows, int[] cols)
    {
        var n = Rules.GridSize;
        int r = index / n, c = index % n;
        int rowGrey = 0, colGrey = 0;
        for (var i = 0; i < s.Table.Count; i++)
        {
            var cell = s.Table[i];
            if (!cell.Available || cell.Owner != null) continue;
            if (i / n == r) rowGrey++;
            if (i % n == c) colGrey++;
        }
        var pr = rowGrey == 0 ? 0 : (double)rows[r] / rowGrey;
        var pc = colGrey == 0 ? 0 : (double)cols[c] / colGrey;
        if (rows[r] == 0 || cols[c] == 0) return 0; // a zero hint means this card is safe
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
            if (!cell.Available) continue;
            if (team.Peeks.Contains(i)) { list.Add((i, KnownValue(cell, side), Cards.Dangerous.Contains(cell.Card) && cell.Owner == null ? 1 : 0)); continue; }
            if (cell.Owner == side) { list.Add((i, 0.6, 0)); continue; }
            // burning an opponent card is safe (works like a Pass) and denies them a chance — small Offside Trap risk
            if (cell.Owner != null) { list.Add((i, 0.42, 0)); continue; }
            var p = Danger(s, i, rows, cols);
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
        var unknownGrey = options.Where(o => !team.Peeks.Contains(o.Index) && s.Table[o.Index].Owner == null).ToList();

        // a free scout from the Playmaker is always used
        if (team.FreeScouts > 0 && unknownGrey.Count > 0)
            return (Cards.Scout, unknownGrey.OrderByDescending(o => o.Value).First().Index, null);

        if (team.HandPlayedThisTurn || team.Hand.Count == 0) return (null, null, null);
        var level = team.AiLevel;

        // weak teams play their cards at random moments
        if (level == 0)
        {
            if (rng.Next(100) >= 20) return (null, null, null);
            var card = team.Hand[rng.Next(team.Hand.Count)];
            return Target(s, side, card, unknownGrey);
        }

        var careful = level == 2 ? 1.0 : 0.6;
        if (rng.NextDouble() > careful) return (null, null, null);

        if (team.Hand.Contains(Cards.Burn))
        {
            var oppCards = options.Where(o => s.Table[o.Index].Owner == side.Other()).ToList();
            if (oppCards.Count >= 2) return (Cards.Burn, oppCards[rng.Next(oppCards.Count)].Index, null);
        }
        if (best.Value >= 0.3) return (null, null, null); // a good card is available anyway
        if (team.Hand.Contains(Cards.Scout) && unknownGrey.Count > 0)
            return (Cards.Scout, unknownGrey.OrderByDescending(o => o.Value).First().Index, null);
        if (team.Hand.Contains(Cards.Calm) && best.Danger > 0.3) return (Cards.Calm, null, null);
        if (team.Hand.Contains(Cards.Press) && best.Danger > 0.4) return (Cards.Press, null, null);
        return (null, null, null);
    }

    private (string?, int?, int?) Target(MatchState s, Side side, string card, List<(int Index, double Value, double Danger)> grey)
    {
        var avail = Enumerable.Range(0, s.Table.Count).Where(i => s.Table[i].Available).ToList();
        switch (card)
        {
            case Cards.Scout:
                return grey.Count > 0 ? (card, grey[rng.Next(grey.Count)].Index, null) : (null, null, null);
            case Cards.Swap:
                if (avail.Count < 2) return (null, null, null);
                var a = avail[rng.Next(avail.Count)];
                var b = avail.Where(i => i != a).ElementAt(rng.Next(avail.Count - 1));
                return (card, a, b);
            case Cards.Burn:
                var opp = avail.Where(i => s.Table[i].Owner == side.Other()).ToList();
                return opp.Count > 0 ? (card, opp[rng.Next(opp.Count)], null) : (null, null, null);
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
