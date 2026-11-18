namespace FootballCards.Api.Game;

public enum Side { Home = 0, Away = 1 }

public enum MatchPhase { Play = 0, Penalty = 1, Shootout = 2, Finished = 3 }

public static class SideExtensions
{
    public static Side Other(this Side s) => s == Side.Home ? Side.Away : Side.Home;
}

public class TeamStats
{
    public int Passes { get; set; }
    public int Tackles { get; set; }
    public int Corners { get; set; }
    public int FreeKicks { get; set; }
    public int Offsides { get; set; }
    public int Saves { get; set; }
    public int Misses { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public int Penalties { get; set; }
    public int Flips { get; set; }
}

public class TeamMatchState
{
    public int TeamId { get; set; }
    public string Name { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string PrimaryColor { get; set; } = "";
    public string SecondaryColor { get; set; } = "";
    public bool IsHuman { get; set; }
    public int Goals { get; set; }
    public TeamStats Stats { get; set; } = new();
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

/// <summary>One spot of the grid: a card that is face down until someone flips it.</summary>
public class TableCell
{
    public string Card { get; set; } = "";
    /// <summary>Who flipped it (null = still face down).</summary>
    public Side? FlippedBy { get; set; }
    public bool FaceUp => FlippedBy != null;
}

public class Flip
{
    public int Cell { get; set; }
    public string Card { get; set; } = "";
    public Side Side { get; set; }
}

public class ShootoutKick
{
    public Side Side { get; set; }
    public bool Scored { get; set; }
}

/// <summary>The full match state. Stored as JSON in the Matches table.</summary>
public class MatchState
{
    public TeamMatchState Home { get; set; } = new();
    public TeamMatchState Away { get; set; } = new();
    public string CompetitionName { get; set; } = "Friendly";
    public string Stadium { get; set; } = "";
    public bool IsKnockout { get; set; }

    /// <summary>State format version; matches saved by older versions are discarded on start-up.</summary>
    public int Version { get; set; } = CurrentVersion;
    public const int CurrentVersion = 2;

    public int Half { get; set; } = 1;
    public MatchPhase Phase { get; set; }
    /// <summary>The player whose turn it is to flip a card.</summary>
    public Side Turn { get; set; }

    /// <summary>The grid. Every card of the match is on it from the start; flipped cards stay face up.</summary>
    public List<TableCell> Table { get; set; } = new();
    public Flip? LastFlip { get; set; }

    /// <summary>Who takes the penalty during the Penalty phase.</summary>
    public Side? PenaltyShooter { get; set; }
    public List<ShootoutKick> Shootout { get; set; } = new();
    public int? HomePenalties { get; set; }
    public int? AwayPenalties { get; set; }

    public int NextEventId { get; set; } = 1;
    public List<MatchEvent> Events { get; set; } = new();

    public TeamMatchState Team(Side s) => s == Side.Home ? Home : Away;

    public Side? HumanSide => Home.IsHuman ? Side.Home : Away.IsHuman ? Side.Away : null;

    /// <summary>Who shoots next in the shootout (home starts, then alternate).</summary>
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
