namespace FootballCards.Api.Game;

/// <summary>Кодовете на картите. Основните действия HOLD и POSITION не са карти — винаги са достъпни.</summary>
public static class Cards
{
    public const string Pass = "PASS";
    public const string LongBall = "LONG_BALL";
    public const string Dribble = "DRIBBLE";
    public const string Cross = "CROSS";
    public const string Shot = "SHOT";
    public const string Counter = "COUNTER";
    public const string Hold = "HOLD";

    public const string Press = "PRESS";
    public const string Tackle = "TACKLE";
    public const string Intercept = "INTERCEPT";
    public const string Block = "BLOCK";
    public const string Foul = "FOUL";
    public const string Position = "POSITION";

    public static readonly string[] AttackCodes = [Pass, LongBall, Dribble, Cross, Shot, Counter];
    public static readonly string[] DefenseCodes = [Press, Tackle, Intercept, Block, Foul];

    public static bool IsAttack(string code) => AttackCodes.Contains(code) || code == Hold;

    public static int Minutes(string code) => code switch
    {
        Pass => 2,
        Hold => 3,
        _ => 2
    };

    public static readonly Dictionary<string, string> Names = new()
    {
        [Pass] = "Пас", [LongBall] = "Дълъг пас", [Dribble] = "Дрибъл", [Cross] = "Центриране",
        [Shot] = "Удар", [Counter] = "Контраатака", [Hold] = "Задържане",
        [Press] = "Пресинг", [Tackle] = "Шпагат", [Intercept] = "Прихващане", [Block] = "Блокиране",
        [Foul] = "Тактическо нарушение", [Position] = "Позиционна защита"
    };
}

/// <summary>
/// Таблиците с вероятности. Всички числа са в проценти. Описани са и в docs/rules.md.
/// </summary>
public static class Rules
{
    public const int AttackHandSize = 4;
    public const int DefenseHandSize = 3;
    public const double StrengthFactor = 0.6;
    public const int HomeAdvantage = 2;

    // Базов шанс атакуващото действие да успее срещу дадена защита.
    private static readonly Dictionary<string, Dictionary<string, int>> Matchups = new()
    {
        [Cards.Pass] = new() { [Cards.Press] = 45, [Cards.Tackle] = 72, [Cards.Intercept] = 58, [Cards.Block] = 78, [Cards.Position] = 76 },
        [Cards.LongBall] = new() { [Cards.Press] = 68, [Cards.Tackle] = 72, [Cards.Intercept] = 30, [Cards.Block] = 55, [Cards.Position] = 56 },
        [Cards.Dribble] = new() { [Cards.Press] = 68, [Cards.Tackle] = 35, [Cards.Intercept] = 70, [Cards.Block] = 52, [Cards.Position] = 62 },
        [Cards.Cross] = new() { [Cards.Press] = 70, [Cards.Tackle] = 75, [Cards.Intercept] = 52, [Cards.Block] = 35, [Cards.Position] = 60 },
        [Cards.Counter] = new() { [Cards.Press] = 62, [Cards.Tackle] = 68, [Cards.Intercept] = 35, [Cards.Block] = 55, [Cards.Position] = 64 },
        [Cards.Hold] = new() { [Cards.Press] = 70, [Cards.Tackle] = 90, [Cards.Intercept] = 94, [Cards.Block] = 96, [Cards.Position] = 95 },
        // При удар: шанс ударът да НЕ бъде блокиран.
        [Cards.Shot] = new() { [Cards.Press] = 85, [Cards.Tackle] = 82, [Cards.Intercept] = 90, [Cards.Block] = 55, [Cards.Position] = 86 },
    };

    /// <summary>Шанс за гол при чист удар (неблокиран) от зона 1/2/3.</summary>
    public static int ShotGoalBase(int zone) => zone switch { 3 => 58, 2 => 34, _ => 11 };
    public const int HeaderGoalBase = 40;
    public const int CloseRangeGoalBase = 56;
    public const int FreeKickGoalBase = 20;
    public const int TackleFoulChance = 18;
    public const int FoulYellowChance = 40;
    public const int TackleYellowChance = 25;
    public const int CornerChance = 35;

    public static bool IsLegal(string card, int zone, bool counterAvailable) => card switch
    {
        Cards.Pass => zone <= 2,
        Cards.LongBall => zone <= 1,
        Cards.Dribble => true,
        Cards.Cross => zone == 2,
        Cards.Shot => zone >= 1,
        Cards.Counter => counterAvailable && zone <= 1,
        Cards.Hold => true,
        _ => Cards.DefenseCodes.Contains(card) || card == Cards.Position
    };

    /// <summary>Разлика в сила за дадено действие (атакуващ минус защитаващ рейтинг), в проценти.</summary>
    public static double StrengthDelta(string attackCard, TeamMatchState att, TeamMatchState def, bool attIsHome)
    {
        var (a, d) = attackCard switch
        {
            Cards.Pass or Cards.Hold => (att.Midfield, def.Midfield),
            Cards.LongBall => (att.Midfield, def.Defense),
            Cards.Counter => (att.Attack, def.Midfield),
            _ => (att.Attack, def.Defense)
        };
        return (a - d) * StrengthFactor + (attIsHome ? HomeAdvantage : -HomeAdvantage);
    }

    /// <summary>Шанс (0–100) атакуващото действие да успее. За Удар — шанс да не бъде блокиран.</summary>
    public static double SuccessChance(string attackCard, string defenseCard, int zone,
        TeamMatchState att, TeamMatchState def, bool attIsHome)
    {
        if (defenseCard == Cards.Foul) return 0; // нарушението винаги спира действието
        var p = (double)Matchups[attackCard][defenseCard];
        if (attackCard == Cards.Shot)
            return Clamp(p + StrengthDelta(attackCard, att, def, attIsHome) * 0.3);

        p += StrengthDelta(attackCard, att, def, attIsHome);
        // Колкото по-близо до вратата, толкова по-трудно е да се напредва.
        if (attackCard is Cards.Pass or Cards.Dribble && zone == 2) p -= 10;
        if (attackCard == Cards.LongBall && zone == 1) p -= 6;
        if (zone == 0 && attackCard != Cards.Hold) p += 5;
        return Clamp(p);
    }

    /// <summary>Шанс за гол при неблокиран удар.</summary>
    public static double GoalChance(int basePercent, TeamMatchState att, TeamMatchState def, bool attIsHome)
    {
        var p = basePercent + (att.Attack - def.Defense) * 0.5 + (attIsHome ? 1 : -1);
        return Math.Clamp(p, 3, 80);
    }

    public static double Clamp(double p) => Math.Clamp(p, 5, 95);

    /// <summary>Къде отборът, спечелил топката, я получава (от неговата гледна точка).</summary>
    public static int TurnoverZone(int attackerZone) => attackerZone switch
    {
        0 => 2,
        1 => 1,
        _ => 0
    };

    public static string ZoneName(int zone) => zone switch
    {
        0 => "Защита", 1 => "Център", 2 => "Атака", _ => "Наказателно поле"
    };
}
