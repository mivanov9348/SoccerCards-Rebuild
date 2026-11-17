namespace FootballCards.Api.Game;

/// <summary>Изборът на един участник в една ситуация.</summary>
/// <param name="Card">Кодът на изиграната карта или HOLD/POSITION за основно действие.</param>
/// <param name="HandIndex">Индекс на картата в ръката (null при основно действие).</param>
/// <param name="SwapIndex">При основно действие: индекс на карта, която да бъде сменена.</param>
public record Choice(string Card, int? HandIndex = null, int? SwapIndex = null);

/// <summary>
/// Изкуствен интелект. Оценява очакваната стойност на всяка комбинация от карти
/// с помощта на същите таблици, които използва двигателят.
/// </summary>
public static class AiPlayer
{
    // Приблизителна "заплаха" (шанс да се стигне до гол) от всяка зона.
    private static readonly double[] Threat = [0.03, 0.06, 0.13, 0.28];

    private static readonly (string Card, double W)[] DefenseGuess =
    [
        (Cards.Press, 0.2), (Cards.Tackle, 0.2), (Cards.Intercept, 0.15), (Cards.Block, 0.15), (Cards.Position, 0.3)
    ];

    public static Choice ChooseAttack(MatchState s, Random rng)
    {
        var att = s.Team(s.Possession);
        var options = new List<(Choice Choice, double Value)>();
        for (var i = 0; i < att.AttackHand.Count; i++)
        {
            var card = att.AttackHand[i];
            if (!Rules.IsLegal(card, s.Zone, s.CounterAvailable)) continue;
            options.Add((new Choice(card, i), AttackValue(s, card)));
        }

        var holdValue = AttackValue(s, Cards.Hold);
        var lead = att.Goals - s.Team(s.Possession.Other()).Goals;
        if (s.Half == 2 && s.Minute >= 75)
        {
            if (lead > 0) holdValue += 0.08;
            if (lead < 0) for (var i = 0; i < options.Count; i++)
                if (options[i].Choice.Card is Cards.Shot or Cards.Cross)
                    options[i] = (options[i].Choice, options[i].Value + 0.04);
        }
        options.Add((new Choice(Cards.Hold, null, PickSwap(att.AttackHand, s)), holdValue));

        return Pick(options, s.Difficulty, rng);
    }

    public static Choice ChooseDefense(MatchState s, Random rng)
    {
        var def = s.Team(s.Possession.Other());
        var guesses = AttackGuess(s);
        var options = new List<(Choice Choice, double Value)>();
        for (var i = 0; i < def.DefenseHand.Count; i++)
        {
            var card = def.DefenseHand[i];
            // по-ниска стойност за атакуващия = по-добре за нас; малка "цена" за изразходване на карта
            var v = -Expected(s, guesses, card) - 0.008;
            options.Add((new Choice(card, i), v));
        }
        options.Add((new Choice(Cards.Position, null, def.DefenseHand.Count > 0 && rng.Next(4) == 0 ? rng.Next(def.DefenseHand.Count) : null),
            -Expected(s, guesses, Cards.Position)));
        return Pick(options, s.Difficulty, rng);
    }

    public static int PenaltyDirection(bool shooter, Random rng)
    {
        var r = rng.Next(100);
        if (shooter) return r < 40 ? 0 : r < 60 ? 1 : 2;
        return r < 45 ? 0 : r < 55 ? 1 : 2;
    }

    /// <summary>Подсказка за човека: шанс в проценти за дадена карта и какво означава.</summary>
    public static (int Percent, string Label) Hint(MatchState s, string card, bool attacking)
    {
        var att = s.Team(s.Possession);
        var def = s.Team(s.Possession.Other());
        var home = s.Possession == Side.Home;
        if (attacking)
        {
            double total = 0;
            foreach (var (d, w) in DefenseGuess)
            {
                var p = Rules.SuccessChance(card, d, s.Zone, att, def, home) / 100;
                p *= card switch
                {
                    Cards.Shot => Rules.GoalChance(Rules.ShotGoalBase(s.Zone), att, def, home) / 100,
                    Cards.Cross => Rules.GoalChance(Rules.HeaderGoalBase, att, def, home) / 100,
                    Cards.Dribble when s.Zone == 3 => Rules.GoalChance(Rules.CloseRangeGoalBase, att, def, home) / 100,
                    _ => 1
                };
                total += p * w;
            }
            var goalCard = card is Cards.Shot or Cards.Cross || (card == Cards.Dribble && s.Zone == 3);
            return ((int)Math.Round(total * 100), goalCard ? "гол" : "успех");
        }
        else
        {
            if (card == Cards.Foul) return (100, "спира");
            var guesses = AttackGuess(s);
            double sum = 0, wsum = 0;
            foreach (var (a, w) in guesses)
            {
                sum += Rules.SuccessChance(a, card, s.Zone, att, def, home) * w;
                wsum += w;
            }
            return ((int)Math.Round(100 - sum / wsum), "спира");
        }
    }

    private static double AttackValue(MatchState s, string card)
    {
        double v = 0;
        foreach (var (d, w) in DefenseGuess) v += PairValue(s, card, d) * w;
        return v;
    }

    private static double Expected(MatchState s, List<(string Card, double W)> guesses, string defenseCard)
    {
        double v = 0, wsum = 0;
        foreach (var (a, w) in guesses)
        {
            v += PairValue(s, a, defenseCard) * w;
            wsum += w;
        }
        return v / wsum;
    }

    /// <summary>Очаквана стойност за атакуващия от двойка карти.</summary>
    public static double PairValue(MatchState s, string attCard, string defCard)
    {
        var att = s.Team(s.Possession);
        var def = s.Team(s.Possession.Other());
        var home = s.Possession == Side.Home;
        var zone = s.Zone;

        if (defCard == Cards.Foul) return FoulValue(s, att, def, home) + 0.03;

        var p = Rules.SuccessChance(attCard, defCard, zone, att, def, home) / 100;
        var loss = Threat[Rules.TurnoverZone(zone)];
        double value = attCard switch
        {
            Cards.Shot => p * Rules.GoalChance(Rules.ShotGoalBase(zone), att, def, home) / 100 - 0.03,
            Cards.Cross => p * Rules.GoalChance(Rules.HeaderGoalBase, att, def, home) / 100 - (1 - p) * Threat[2] * 0.5,
            Cards.Dribble when zone == 3 => p * Rules.GoalChance(Rules.CloseRangeGoalBase, att, def, home) / 100 - (1 - p) * Threat[2] * 0.5,
            _ => p * Threat[Math.Min(3, zone + MatchEngine.Advance(attCard))] - (1 - p) * loss
        };

        if (defCard == Cards.Tackle && attCard != Cards.Hold)
        {
            var f = Rules.TackleFoulChance / 100.0;
            value = f * FoulValue(s, att, def, home) + (1 - f) * value;
        }
        return value;
    }

    private static double FoulValue(MatchState s, TeamMatchState att, TeamMatchState def, bool home) => s.Zone switch
    {
        3 => 0.78,
        2 => Rules.GoalChance(Rules.FreeKickGoalBase, att, def, home) / 100,
        _ => Threat[s.Zone]
    };

    private static List<(string Card, double W)> AttackGuess(MatchState s)
    {
        var list = s.Zone switch
        {
            3 => new List<(string, double)> { (Cards.Shot, 3), (Cards.Dribble, 1), (Cards.Hold, 0.4) },
            2 => [(Cards.Shot, 1.5), (Cards.Cross, 1.5), (Cards.Pass, 1.5), (Cards.Dribble, 1.2), (Cards.Hold, 0.5)],
            1 => [(Cards.Pass, 2), (Cards.LongBall, 1), (Cards.Dribble, 1), (Cards.Shot, 0.4), (Cards.Hold, 0.5)],
            _ => [(Cards.Pass, 2), (Cards.LongBall, 1), (Cards.Dribble, 1), (Cards.Hold, 0.5)]
        };
        if (s.CounterAvailable && s.Zone <= 1) list.Add((Cards.Counter, 2));
        return list;
    }

    private static int? PickSwap(List<string> hand, MatchState s)
    {
        for (var i = 0; i < hand.Count; i++)
            if (hand[i] == Cards.Counter || (hand[i] == Cards.Cross && s.Zone == 0))
                return i;
        return null;
    }

    private static Choice Pick(List<(Choice Choice, double Value)> options, int difficulty, Random rng)
    {
        var randomChance = difficulty switch { 0 => 40, 1 => 15, _ => 3 };
        if (rng.Next(100) < randomChance) return options[rng.Next(options.Count)].Choice;
        // малко шум, за да не е напълно предвидим
        return options.OrderByDescending(o => o.Value + rng.NextDouble() * 0.01).First().Choice;
    }
}
