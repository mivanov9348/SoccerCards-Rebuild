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
    private static double OwnValue(MatchState s, Side side, TeamMatchState team, string card) => card switch
    {
        Cards.SuperSub => s.Half == 2 ? 0.75 : 0.2,
        Cards.Winger => 0.6,
        Cards.Goalkeeper => team.GoalkeeperActive ? 0.3 : 0.45,
        Cards.TargetMan => team.TargetManActive ? 0.3 : 0.42,
        Cards.Sweeper => team.SweeperActive ? 0.3 : 0.45,
        Cards.TwelfthMan => side == Side.Home && !team.TwelfthManActive ? 0.5 : 0.3,
        Cards.Decoy => 0.08,
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
    private static double KnownValue(MatchState s, TeamMatchState team, TableCell c, Side side)
    {
        if (c.Owner == side) return OwnValue(s, side, team, c.Card);
        if (c.Owner == side.Other()) return c.Card == Cards.OffsideTrap ? -0.6 : c.Card == Cards.Decoy ? 0.15 : 0.6; // a steal
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
            if (cell.Owner == side) { list.Add((i, OwnValue(s, side, team, cell.Card), 0)); continue; }
            if (team.Peeks.Contains(i))
            {
                var bad = (cell.Owner == null && Cards.Dangerous.Contains(cell.Card)) || (cell.Owner != null && cell.Card == Cards.OffsideTrap);
                list.Add((i, KnownValue(s, team, cell, side), bad ? 1 : 0));
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
        // Marking: protect the best own card that is still hidden
        if (team.Hand.Contains(Cards.Marking) && team.MarkedCell < 0)
        {
            var toMark = BestOwnHidden(s, side, team);
            if (toMark >= 0) return (Cards.Marking, toMark, null);
        }
        // Substitution: bring back a strong card that is already used
        if (team.Hand.Contains(Cards.Substitution) && unknown.Count > 2)
        {
            var used = Enumerable.Range(0, s.Table.Count).FirstOrDefault(i => s.Table[i].FaceUp && s.Table[i].Owner == side
                && s.Table[i].Card is Cards.Striker or Cards.BigChanceCard or Cards.SuperSub or Cards.Winger, -1);
            if (used >= 0) return (Cards.Substitution, used, null);
        }
        // VAR and the penalty taker are "armed" cards: play them early, they wait for their moment
        var myGoals = team.Goals;
        var theirGoals = s.Team(side.Other()).Goals;
        if (team.Hand.Contains(Cards.Var) && !team.VarActive && myGoals >= theirGoals) return (Cards.Var, null, null);
        if (team.Hand.Contains(Cards.PenaltyTaker) && !team.PenaltyTakerActive) return (Cards.PenaltyTaker, null, null);
        if (best.Value >= 0.3) return (null, null, null); // a good card is available anyway
        if (team.Hand.Contains(Cards.Scout) && unknown.Count > 0)
            return (Cards.Scout, unknown.OrderByDescending(o => o.Value).First().Index, null);
        if (team.Hand.Contains(Cards.Calm) && best.Danger > 0.3) return (Cards.Calm, null, null);
        if (team.Hand.Contains(Cards.Press) && best.Danger > 0.4) return (Cards.Press, null, null);
        return (null, null, null);
    }

    /// <summary>The most valuable own team card that is still face down (and not yet marked), or -1.</summary>
    private static int BestOwnHidden(MatchState s, Side side, TeamMatchState team) =>
        Enumerable.Range(0, s.Table.Count)
            .Where(i => s.Table[i].Available && s.Table[i].Owner == side && s.Table[i].Card != Cards.OffsideTrap && s.Table[i].Card != Cards.Decoy)
            .OrderByDescending(i => OwnValue(s, side, team, s.Table[i].Card))
            .DefaultIfEmpty(-1).First();

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
            case Cards.Marking:
                var mark = BestOwnHidden(s, side, s.Team(side));
                return mark >= 0 ? (card, mark, null) : (null, null, null);
            case Cards.Substitution:
                var up = Enumerable.Range(0, s.Table.Count).FirstOrDefault(i => s.Table[i].FaceUp && s.Table[i].Owner == side, -1);
                return up >= 0 && avail.Count > 0 ? (card, up, null) : (null, null, null);
            case Cards.Analysis:
                return avail.Count > 0 ? (card, avail[rng.Next(avail.Count)], null) : (null, null, null);
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

    /// <summary>After the opponent's red card: take away its most valuable hand card (ties: a random one of them).</summary>
    public int RemoveChoice(TeamMatchState victim, CardCatalog catalog)
    {
        if (victim.Hand.Count == 0) return 0;
        // value = tactic cost, then tier (a Legendary copy is worth more than a Normal one)
        int Value(int i) => catalog.Cost(victim.Hand[i]) * 10 + Tiers.At(victim.HandTiers, i);
        var best = Enumerable.Range(0, victim.Hand.Count).Max(Value);
        var top = Enumerable.Range(0, victim.Hand.Count).Where(i => Value(i) == best).Select(i => (i, victim.Hand[i])).ToList();
        return top[rng.Next(top.Count)].i;
    }
}
