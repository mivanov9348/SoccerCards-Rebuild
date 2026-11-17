namespace FootballCards.Api.Data.Entities;

/// <summary>Национално първенство (напр. Първа лига). Само лиги с IsPlayable могат да се играят в кариера.</summary>
public class League
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Country { get; set; } = "";
    public int Tier { get; set; } = 1;
    /// <summary>Ако е true, лигата има пълен списък отбори и може да се избира за кариера.</summary>
    public bool IsPlayable { get; set; }
    public List<Team> Teams { get; set; } = new();
}

public class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
    public string Stadium { get; set; } = "";
    public string PrimaryColor { get; set; } = "#336699";
    public string SecondaryColor { get; set; } = "#ffffff";
    /// <summary>Рейтинги 1–99.</summary>
    public int Attack { get; set; } = 60;
    public int Midfield { get; set; } = 60;
    public int Defense { get; set; } = 60;
    public int? LeagueId { get; set; }
    public League? League { get; set; }

    public int Overall => (int)Math.Round((Attack + Midfield + Defense) / 3.0);
}

public enum CompetitionType { League = 0, Cup = 1, European = 2, Friendly = 3 }

/// <summary>Турнир: шампионат, национална купа или европейски турнир.</summary>
public class Competition
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public CompetitionType Type { get; set; }
    /// <summary>За европейските турнири: 1 = най-силният.</summary>
    public int Tier { get; set; } = 1;
    public int? LeagueId { get; set; }
    public League? League { get; set; }
}

public enum CardKind { Attack = 0, Defense = 1 }

/// <summary>Дефиниция на ситуационна карта и колко копия има в тестето.</summary>
public class CardDefinition
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public CardKind Kind { get; set; }
    public int CopiesInDeck { get; set; }
    public int Minutes { get; set; }
}

public enum CareerStatus { Active = 0, Fired = 1 }

/// <summary>Слот за запис (кариера).</summary>
public class Career
{
    public int Id { get; set; }
    public string SaveName { get; set; } = "";
    public string ManagerName { get; set; } = "";
    public int TeamId { get; set; }
    public Team? Team { get; set; }
    public int LeagueId { get; set; }
    public int Reputation { get; set; } = 30;
    public int Difficulty { get; set; } = 1;
    public CareerStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<CareerSeason> Seasons { get; set; } = new();
    public List<CareerMessage> Messages { get; set; } = new();
}

public enum BoardExpectation { Survive = 0, MidTable = 1, Europe = 2, Title = 3 }

public class CareerSeason
{
    public int Id { get; set; }
    public int CareerId { get; set; }
    public Career? Career { get; set; }
    public int Year { get; set; }
    public int TeamId { get; set; }
    public BoardExpectation Expectation { get; set; }
    /// <summary>Код на европейския турнир, в който участва отборът този сезон (или null).</summary>
    public string? EuropeanCode { get; set; }
    public int CurrentWeek { get; set; } = 1;
    public int TotalWeeks { get; set; }
    public bool Finished { get; set; }
    /// <summary>JSON с обобщение на края на сезона (позиция, оценка, оферти).</summary>
    public string? SummaryJson { get; set; }
    /// <summary>JSON: списък със седмици и какъв кръг/турнир се играе във всяка.</summary>
    public string ScheduleJson { get; set; } = "[]";
    public List<Fixture> Fixtures { get; set; } = new();
}

public class Fixture
{
    public int Id { get; set; }
    public int CareerSeasonId { get; set; }
    public CareerSeason? CareerSeason { get; set; }
    public int CompetitionId { get; set; }
    public Competition? Competition { get; set; }
    public int Week { get; set; }
    public int Round { get; set; }
    public string Stage { get; set; } = "";
    public int HomeTeamId { get; set; }
    public Team? HomeTeam { get; set; }
    public int AwayTeamId { get; set; }
    public Team? AwayTeam { get; set; }
    public bool Played { get; set; }
    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }
    public int? HomePenalties { get; set; }
    public int? AwayPenalties { get; set; }
    public bool IsKnockout { get; set; }

    public int? WinnerTeamId()
    {
        if (!Played) return null;
        if (HomeGoals > AwayGoals) return HomeTeamId;
        if (AwayGoals > HomeGoals) return AwayTeamId;
        if (HomePenalties.HasValue && AwayPenalties.HasValue)
            return HomePenalties > AwayPenalties ? HomeTeamId : AwayTeamId;
        return null;
    }
}

public class CareerMessage
{
    public int Id { get; set; }
    public int CareerId { get; set; }
    public int Year { get; set; }
    public int Week { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum MatchStatus { InProgress = 0, Finished = 1 }

/// <summary>Мач в ход (приятелски или от кариера). Пълното състояние се пази като JSON.</summary>
public class Match
{
    public int Id { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public int? FixtureId { get; set; }
    public int? CareerId { get; set; }
    public MatchStatus Status { get; set; }
    public string StateJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
