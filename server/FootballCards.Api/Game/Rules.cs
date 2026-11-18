namespace FootballCards.Api.Game;

/// <summary>Card codes. What each card does is decided in <see cref="MatchEngine"/>.</summary>
public static class Cards
{
    public const string Pass = "PASS";
    public const string Tackle = "TACKLE";
    public const string Corner = "CORNER";
    public const string FreeKick = "FREE_KICK";
    public const string Offside = "OFFSIDE";
    public const string Save = "SAVE";
    public const string Miss = "MISS";
    public const string Goal = "GOAL";
    public const string Penalty = "PENALTY";
    public const string YellowCard = "YELLOW_CARD";
    public const string RedCard = "RED_CARD";
    public const string HalfTime = "HALF_TIME";
    public const string FullTime = "FULL_TIME";

    public static readonly Dictionary<string, string> Names = new()
    {
        [Pass] = "Pass", [Tackle] = "Tackle", [Corner] = "Corner", [FreeKick] = "Free Kick",
        [Offside] = "Offside", [Save] = "Save", [Miss] = "Miss", [Goal] = "Goal!",
        [Penalty] = "Penalty", [YellowCard] = "Yellow Card", [RedCard] = "Red Card",
        [HalfTime] = "Half Time", [FullTime] = "Full Time"
    };

    /// <summary>Cards after which the same player flips again.</summary>
    public static bool KeepsTurn(string code) => code is Pass or Corner or FreeKick;

    public static string Name(string code) => Names.GetValueOrDefault(code, code);
}

/// <summary>Fixed numbers of the game. They are also described in docs/rules.md.</summary>
public static class Rules
{
    /// <summary>Number of face-down cards on the table.</summary>
    public const int TableSize = 24;

    /// <summary>Game minutes that pass with each flipped card (random between min and max).</summary>
    public const int MinMinutesPerFlip = 1;
    public const int MaxMinutesPerFlip = 3;

    /// <summary>Penalty: shooter and keeper pick different sides → goal with this chance (otherwise wide).</summary>
    public const int PenaltyGoalChance = 88;
    /// <summary>Penalty: same side → keeper saves with this chance (center/center: lower).</summary>
    public const int PenaltySaveSameSide = 75;
    public const int PenaltySaveCenter = 70;

    public const int ShootoutKicks = 5;

    public static readonly string[] Directions = ["left", "center", "right"];
}
