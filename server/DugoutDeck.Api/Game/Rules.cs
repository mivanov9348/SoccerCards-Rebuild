using DugoutDeck.Api.Data.Entities;

namespace DugoutDeck.Api.Game;

/// <summary>Card codes. Their effects are coded in <see cref="MatchEngine"/>; costs/prices are in the database.</summary>
public static class Cards
{
    // neutral grid cards
    public const string Pass = "PASS";
    public const string Corner = "CORNER";
    public const string FreeKick = "FREE_KICK";
    public const string Tackle = "TACKLE";
    public const string Offside = "OFFSIDE";
    public const string Save = "SAVE";
    public const string Miss = "MISS";
    public const string YellowCard = "YELLOW_CARD";
    public const string RedCard = "RED_CARD";
    public const string Goal = "GOAL";
    public const string Penalty = "PENALTY";
    public const string HalfTime = "HALF_TIME";
    public const string FullTime = "FULL_TIME";

    // team (field) cards
    public const string Striker = "STRIKER";
    public const string BigChanceCard = "BIG_CHANCE_CARD";
    public const string LongShot = "LONG_SHOT";
    public const string Playmaker = "PLAYMAKER";
    public const string Captain = "CAPTAIN";
    public const string Wall = "WALL";
    public const string OffsideTrap = "OFFSIDE_TRAP";
    public const string CounterAttack = "COUNTER_ATTACK";
    public const string SquadPlayer = "SQUAD_PLAYER";

    // hand cards
    public const string Scout = "SCOUT";
    public const string Swap = "SWAP";
    public const string Calm = "CALM";
    public const string Press = "PRESS";
    public const string Burn = "BURN";

    public static readonly HashSet<string> Dangerous = [Tackle, Offside, Save, Miss, YellowCard, RedCard];

    public static readonly string[] FieldCodes = [Striker, BigChanceCard, LongShot, Playmaker, Captain, Wall, OffsideTrap, CounterAttack, SquadPlayer];
    public static readonly string[] HandCodes = [Scout, Swap, Calm, Press, Burn];

    public static readonly Dictionary<string, string> Names = new()
    {
        [Pass] = "Pass", [Corner] = "Corner", [FreeKick] = "Free Kick", [Tackle] = "Tackle", [Offside] = "Offside",
        [Save] = "Save", [Miss] = "Miss", [YellowCard] = "Yellow Card", [RedCard] = "Red Card", [Goal] = "Goal!",
        [Penalty] = "Penalty", [HalfTime] = "Half Time", [FullTime] = "Full Time",
        [Striker] = "Striker", [BigChanceCard] = "Big Chance", [LongShot] = "Long Shot", [Playmaker] = "Playmaker",
        [Captain] = "Captain", [Wall] = "Wall", [OffsideTrap] = "Offside Trap", [CounterAttack] = "Counter Attack",
        [SquadPlayer] = "Squad Player",
        [Scout] = "Scout", [Swap] = "Swap", [Calm] = "Calm", [Press] = "Press", [Burn] = "Burn",
    };

    public static string Name(string code) => Names.GetValueOrDefault(code, code);
}

/// <summary>Match conditions shown before kick-off.</summary>
public static class Conditions
{
    public const string Home = "HOME";
    public const string Rain = "RAIN";
    public const string Wind = "WIND";
    public const string Derby = "DERBY";
    public const string Fine = "FINE";

    public static readonly Dictionary<string, string> Labels = new()
    {
        [Home] = "Home advantage: +1 tactic point for the home team",
        [Rain] = "Rain: 1 Pass becomes a Miss",
        [Wind] = "Wind: 1 Pass becomes a Corner",
        [Derby] = "Derby: 1 Tackle becomes a Yellow Card",
        [Fine] = "Fine weather: no changes",
    };
}

/// <summary>Fixed numbers of the game. Also described in docs/rules.md.</summary>
public static class Rules
{
    public const int GridSize = 5;
    public const int FieldCardsPerTeam = 4;
    public const int MaxModifiers = 2;
    public const int BaseTacticPoints = 10;
    public const int PassesForBigChance = 3;
    /// <summary>Rain, wind and derby conditions are switched off for now (only the home bonus stays).</summary>
    public static readonly bool WeatherAndDerby = false;

    // mini games (percent)
    public const int CornerGoal = 30;
    public const int FreeKickDirectGoal = 25;
    public const int FreeKickDirectIntoWallGoal = 10;
    public const int FreeKickPressLoseBall = 50;
    public const int BigChanceGoalDifferentSide = 35;
    public const int BigChanceGoalSameSide = 15;
    public const int LongShotGoal = 12;
    public const int StrikerGoal = 35;
    public const int PenaltyGoalChance = 88;
    public const int PenaltySaveSameSide = 75;
    public const int PenaltySaveCenter = 70;
    public const int ShootoutKicks = 5;

    /// <summary>Limits for neutral card counts after conditions and modifiers.</summary>
    public static readonly Dictionary<string, (int Min, int Max)> NeutralLimits = new()
    {
        [Cards.Pass] = (1, 16), [Cards.Goal] = (0, 5), [Cards.Corner] = (0, 4),
        [Cards.Tackle] = (0, 6), [Cards.Penalty] = (0, 3), [Cards.Miss] = (0, 4), [Cards.YellowCard] = (0, 3),
    };

    /// <summary>Extra tactic points from the team rating (0–3).</summary>
    public static int RatingBonus(int rating) => rating >= 80 ? 3 : rating >= 72 ? 2 : rating >= 64 ? 1 : 0;

    public static int TacticPoints(int rating, bool home) => BaseTacticPoints + RatingBonus(rating) + (home ? 1 : 0);

    /// <summary>Number of hand cards a team may take (1–3).</summary>
    /// <summary>Difficulty: extra tactic points for the human (Easy +2, Hard −1).</summary>
    public static int DifficultyPoints(Difficulty d) => d switch { Difficulty.Easy => 2, Difficulty.Hard => -1, _ => 0 };
    /// <summary>Difficulty: the computer opponent plays one level weaker (Easy) or stronger (Hard), with 1 point less / more.</summary>
    public static int DifficultyAiShift(Difficulty d) => d switch { Difficulty.Easy => -1, Difficulty.Hard => 1, _ => 0 };

    public static int HandSize(int rating) => rating >= 78 ? 3 : rating >= 66 ? 2 : 1;

    /// <summary>Computer skill: 0 weak, 1 average, 2 strong.</summary>
    public static int AiLevel(int rating) => rating >= 78 ? 2 : rating >= 66 ? 1 : 0;

    public static readonly string[] AiLevelNames = ["Weak", "Average", "Strong"];
}
