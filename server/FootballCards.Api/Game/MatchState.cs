namespace FootballCards.Api.Game;

public enum Side { Home = 0, Away = 1 }

public enum Tactic { Balanced = 0, Attacking = 1, Defensive = 2 }

public enum MatchPhase { Play = 0, Penalty = 1, Finished = 2 }

public static class SideExtensions
{
    public static Side Other(this Side s) => s == Side.Home ? Side.Away : Side.Home;
}

/// <summary>Състоянието на един отбор в мача. Сериализира се в JSON заедно с мача.</summary>
public class TeamMatchState
{
    public int TeamId { get; set; }
    public string Name { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string PrimaryColor { get; set; } = "";
    public string SecondaryColor { get; set; } = "";
    public int BaseAttack { get; set; }
    public int BaseMidfield { get; set; }
    public int BaseDefense { get; set; }
    public Tactic Tactic { get; set; }
    public bool IsHuman { get; set; }
    /// <summary>Бонус/наказание от трудността (само за AI отбор срещу човек).</summary>
    public int DifficultyBonus { get; set; }
    public int Goals { get; set; }

    public List<string> AttackHand { get; set; } = new();
    public List<string> DefenseHand { get; set; } = new();
    public List<string> AttackDeck { get; set; } = new();
    public List<string> DefenseDeck { get; set; } = new();
    public List<string> AttackDiscard { get; set; } = new();
    public List<string> DefenseDiscard { get; set; } = new();

    public int Shots { get; set; }
    public int ShotsOnTarget { get; set; }
    public int Fouls { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public int PossessionTurns { get; set; }

    private int TacticAttack => Tactic switch { Tactic.Attacking => 4, Tactic.Defensive => -4, _ => 0 };
    private int TacticDefense => Tactic switch { Tactic.Attacking => -4, Tactic.Defensive => 4, _ => 0 };
    private int Common => DifficultyBonus - RedCards * 5;

    public int Attack => BaseAttack + TacticAttack + Common;
    public int Midfield => BaseMidfield + Common;
    public int Defense => BaseDefense + TacticDefense + Common;
}

public class MatchEvent
{
    public int Id { get; set; }
    public int Minute { get; set; }
    public string MinuteLabel { get; set; } = "";
    public string Type { get; set; } = "info";
    public Side? Side { get; set; }
    public string Text { get; set; } = "";
    public string? AttackCard { get; set; }
    public string? DefenseCard { get; set; }
}

public class PenaltyContext
{
    public Side Shooter { get; set; }
}

/// <summary>Пълното състояние на мача. Пази се като JSON в таблицата Matches.</summary>
public class MatchState
{
    public TeamMatchState Home { get; set; } = new();
    public TeamMatchState Away { get; set; } = new();
    public string CompetitionName { get; set; } = "Приятелски мач";
    public string Stadium { get; set; } = "";
    public bool IsKnockout { get; set; }
    public int Difficulty { get; set; } = 1;

    public int Minute { get; set; }
    public int Half { get; set; } = 1;
    public int AddedTime1 { get; set; }
    public int AddedTime2 { get; set; }
    public Side FirstKickOff { get; set; }

    public MatchPhase Phase { get; set; }
    public Side Possession { get; set; }
    /// <summary>Зона на топката от гледна точка на отбора с топката: 0 Защита, 1 Център, 2 Атака, 3 Наказателно поле.</summary>
    public int Zone { get; set; } = 1;
    /// <summary>Отборът току-що е отнел топката и може да изиграе Контраатака.</summary>
    public bool CounterAvailable { get; set; }
    public PenaltyContext? Penalty { get; set; }

    public int? HomePenalties { get; set; }
    public int? AwayPenalties { get; set; }

    public int NextEventId { get; set; } = 1;
    public List<MatchEvent> Events { get; set; } = new();

    public TeamMatchState Team(Side s) => s == Side.Home ? Home : Away;

    public Side? HumanSide => Home.IsHuman ? Side.Home : Away.IsHuman ? Side.Away : null;

    public string MinuteLabel
    {
        get
        {
            if (Half == 1 && Minute > 45) return $"45+{Minute - 45}'";
            if (Half == 2 && Minute > 90) return $"90+{Minute - 90}'";
            return $"{Math.Max(Minute, 1)}'";
        }
    }

    public int? WinnerTeamId()
    {
        if (Phase != MatchPhase.Finished) return null;
        if (Home.Goals != Away.Goals) return Home.Goals > Away.Goals ? Home.TeamId : Away.TeamId;
        if (HomePenalties.HasValue && AwayPenalties.HasValue)
            return HomePenalties > AwayPenalties ? Home.TeamId : Away.TeamId;
        return null;
    }
}
