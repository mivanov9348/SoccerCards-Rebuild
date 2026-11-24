namespace DugoutDeck.Api.Game;

public enum Side { Home = 0, Away = 1 }

public enum MatchPhase { Setup = 0, Play = 1, Decision = 2, Finished = 3 }

public enum DecisionKind { Corner = 0, FreeKick = 1, BigChance = 2, Penalty = 3, Shootout = 4, Discard = 5 }

public static class SideExtensions
{
    public static Side Other(this Side s) => s == Side.Home ? Side.Away : Side.Home;
}

/// <summary>A tactic: 4 field cards, hand cards and up to 2 neutral modifiers.</summary>
public class TacticSpec
{
    public List<string> Field { get; set; } = new();
    public List<string> Hand { get; set; } = new();
    public List<string> Modifiers { get; set; } = new();
    /// <summary>Tier of each field / hand card (Tiers.Normal … Tiers.Legendary), in the same order; missing = Normal.</summary>
    public List<int> FieldTiers { get; set; } = new();
    public List<int> HandTiers { get; set; } = new();
}

public class TeamStats
{
    public int Flips { get; set; }
    public int Passes { get; set; }
    public int Corners { get; set; }
    public int FreeKicks { get; set; }
    public int BallsLost { get; set; }
    public int Saves { get; set; }
    public int Misses { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public int Penalties { get; set; }
    public int BigChances { get; set; }
    public int CardsBurned { get; set; }
    public int CardsStolen { get; set; }
    public int GoalsDisallowed { get; set; }
    public Dictionary<string, int> GoalSources { get; set; } = new();
}

public class TeamMatchState
{
    public int TeamId { get; set; }
    public string Name { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string PrimaryColor { get; set; } = "";
    public string SecondaryColor { get; set; } = "";
    public string City { get; set; } = "";
    public bool IsHuman { get; set; }
    public int Rating { get; set; }
    public int AiLevel { get; set; }
    public int Goals { get; set; }

    public TacticSpec Tactic { get; set; } = new();
    public List<string> Hand { get; set; } = new();
    /// <summary>Tier of each hand card, in the same order as Hand.</summary>
    public List<int> HandTiers { get; set; } = new();

    // running effects
    public int PassStreak { get; set; }
    public bool CaptainShield { get; set; }
    public bool WallActive { get; set; }
    public bool CalmActive { get; set; }
    public bool PressActive { get; set; }
    public int CounterFlips { get; set; }
    public int FreeScouts { get; set; }
    public bool HandPlayedThisTurn { get; set; }
    /// <summary>A team may flip only one of its own team cards per turn (a turn = until it loses the ball).</summary>
    public bool OwnCardUsedThisTurn { get; set; }
    // version 1.1 effects
    public bool GoalkeeperActive { get; set; }
    public bool TargetManActive { get; set; }
    public bool SweeperActive { get; set; }
    public bool TwelfthManActive { get; set; }
    public bool VarActive { get; set; }
    public bool PenaltyTakerActive { get; set; }
    /// <summary>Marking: the cell of an own face-down card that cannot be stolen (current half), or -1.</summary>
    public int MarkedCell { get; set; } = -1;
    // version 1.2 tiers: extra uses of shield-like effects and the bonus of chance effects
    public int CaptainExtra { get; set; }
    public int WallExtra { get; set; }
    public int SweeperExtra { get; set; }
    public int TwelfthManExtra { get; set; }
    public int CalmExtra { get; set; }
    public int PressExtra { get; set; }
    public int GoalkeeperBonus { get; set; }
    public int TargetManBonus { get; set; }
    public int VarBonus { get; set; }
    public int PenaltyTakerBonus { get; set; }
    /// <summary>Cells this team has looked under (current half).</summary>
    public List<int> Peeks { get; set; } = new();
    /// <summary>Cards this team has played in the match (hand cards and team cards that took effect), for the match screen.</summary>
    public List<PlayedCard> Played { get; set; } = new();

    public TeamStats Stats { get; set; } = new();
}

/// <summary>A card a team played: How = hand, own, stolen (taken from the opponent), trap or decoy (sprung against the opponent).</summary>
public class PlayedCard
{
    public string Code { get; set; } = "";
    public int Half { get; set; } = 1;
    public string How { get; set; } = "own";
    public int Tier { get; set; }
}

/// <summary>One spot of the grid.</summary>
public class TableCell
{
    public string Card { get; set; } = "";
    /// <summary>null = neutral; otherwise the team whose tactic the card comes from. All backs are grey — only the owner knows.</summary>
    public Side? Owner { get; set; }
    /// <summary>Tier of a team card (Normal … Legendary).</summary>
    public int Tier { get; set; }
    /// <summary>Who flipped it (null = still face down).</summary>
    public Side? FlippedBy { get; set; }
    /// <summary>Removed by a Burn hand card without being flipped.</summary>
    public bool Burned { get; set; }
    public bool FaceUp => FlippedBy != null;
    public bool Available => FlippedBy == null && !Burned;
}

public class MatchEvent
{
    public int Id { get; set; }
    public int Half { get; set; } = 1;
    public string Type { get; set; } = "info";
    public Side? Side { get; set; }
    public string Text { get; set; } = "";
    public string? Card { get; set; }
}

public class Flip
{
    public int Cell { get; set; }
    public string Card { get; set; } = "";
    public int Tier { get; set; }
    public Side Side { get; set; }
    public Side? Owner { get; set; }
}

public class PendingDecision
{
    public DecisionKind Kind { get; set; }
    /// <summary>The team that attacks (or, for Discard, the team that must give up a hand card).</summary>
    public Side Attacker { get; set; }
    public string Source { get; set; } = "";
    /// <summary>Extra goal chance of a tiered card that started this mini-game (Big Chance card, Winger).</summary>
    public int Bonus { get; set; }
}

public class ShootoutKick
{
    public Side Side { get; set; }
    public bool Scored { get; set; }
}

/// <summary>The full match state, stored as JSON in the Matches table.</summary>
public class MatchState
{
    public const int CurrentVersion = 5;
    public int Version { get; set; } = CurrentVersion;

    public TeamMatchState Home { get; set; } = new();
    public TeamMatchState Away { get; set; } = new();
    public string CompetitionName { get; set; } = "Friendly";
    public DugoutDeck.Api.Data.Entities.Difficulty Difficulty { get; set; }
    /// <summary>Career match: the computer's cards have tiers (version 1.2).</summary>
    public bool UseTiers { get; set; }
    public string Stadium { get; set; } = "";
    public bool IsKnockout { get; set; }

    public List<string> Conditions { get; set; } = new();
    /// <summary>Final neutral card counts for each half (after conditions and modifiers).</summary>
    public Dictionary<string, int> Composition { get; set; } = new();

    public int Half { get; set; } = 1;
    public MatchPhase Phase { get; set; }
    public Side Turn { get; set; }
    public Side FirstKickOff { get; set; }

    public List<TableCell> Table { get; set; } = new();
    public List<TableCell> SecondHalfTable { get; set; } = new();
    /// <summary>The finished first-half grid (shown revealed during the half-time break).</summary>
    public List<TableCell> FirstHalfTable { get; set; } = new();
    public Flip? LastFlip { get; set; }

    public PendingDecision? Decision { get; set; }
    public List<ShootoutKick> Shootout { get; set; } = new();
    public int? HomePenalties { get; set; }
    public int? AwayPenalties { get; set; }

    public int NextEventId { get; set; } = 1;
    public List<MatchEvent> Events { get; set; } = new();

    public TeamMatchState Team(Side s) => s == Side.Home ? Home : Away;
    public Side? HumanSide => Home.IsHuman ? Side.Home : Away.IsHuman ? Side.Away : null;
    public Side NextShootoutShooter => Shootout.Count % 2 == 0 ? Side.Home : Side.Away;

    public int? WinnerTeamId()
    {
        if (Phase != MatchPhase.Finished) return null;
        if (Home.Goals != Away.Goals) return Home.Goals > Away.Goals ? Home.TeamId : Away.TeamId;
        if (HomePenalties.HasValue && AwayPenalties.HasValue)
            return HomePenalties > AwayPenalties ? Home.TeamId : Away.TeamId;
        return null;
    }
}
