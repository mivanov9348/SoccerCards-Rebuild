namespace DugoutDeck.Api.Data.Entities;

/// <summary>A national division (e.g. First League). Only playable leagues can be chosen for a career.</summary>
public class League
{
    public int Id { get; set; }
    /// <summary>Stable id from seed.json (e.g. "BG1"), so the league can be renamed safely.</summary>
    public string? Code { get; set; }
    public string Name { get; set; } = "";
    public string Country { get; set; } = "";
    /// <summary>1 = top division, 2 = second division.</summary>
    public int Tier { get; set; } = 1;
    public bool IsPlayable { get; set; }
    /// <summary>Teams relegated to the tier below (= teams promoted from it).</summary>
    public int Relegation { get; set; } = 2;
    /// <summary>European places for a top division (Champions Cup / Continental Cup / Challenge Cup).</summary>
    public int UclSpots { get; set; }
    public int UelSpots { get; set; }
    public int UeclSpots { get; set; }
    public List<Team> Teams { get; set; } = new();
}

public class Team
{
    public int Id { get; set; }
    /// <summary>Stable id from seed.json (e.g. "BG1-01"). Names, colours and stadiums are synced from seed.json by this code.</summary>
    public string? Code { get; set; }
    /// <summary>Changed in the in-game editor: seed.json no longer overwrites this team.</summary>
    public bool EditedInGame { get; set; }
    public string Name { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
    public string Stadium { get; set; } = "";
    public string PrimaryColor { get; set; } = "#336699";
    public string SecondaryColor { get; set; } = "#ffffff";
    /// <summary>Ratings 1–99.</summary>
    public int Attack { get; set; } = 60;
    public int Midfield { get; set; } = 60;
    public int Defense { get; set; } = 60;
    /// <summary>The team's starting league (in a career the league can change through promotion/relegation).</summary>
    public int? LeagueId { get; set; }
    public League? League { get; set; }

    public int Overall => (int)Math.Round((Attack + Midfield + Defense) / 3.0);
}

public enum CompetitionType { League = 0, Cup = 1, European = 2, Friendly = 3 }

/// <summary>A league competition, a national cup or a European cup.</summary>
public class Competition
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public CompetitionType Type { get; set; }
    /// <summary>For European cups: 1 = strongest.</summary>
    public int Tier { get; set; } = 1;
    /// <summary>For league competitions.</summary>
    public int? LeagueId { get; set; }
    public League? League { get; set; }
    /// <summary>For national cups.</summary>
    public string? Country { get; set; }
}

public enum CardKind { Neutral = 0, Field = 1, Hand = 2, Modifier = 3 }
public enum CardRarity { None = 0, Common = 1, Rare = 2, Epic = 3 }

/// <summary>
/// Every card of the game: neutral grid cards, team (field) cards, hand cards and neutral modifiers.
/// What a card does is coded in the match engine; prices and costs live here so they are easy to tune.
/// </summary>
public class CardDefinition
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public CardKind Kind { get; set; }
    public CardRarity Rarity { get; set; }
    /// <summary>Neutral cards: copies in the base grid of each half.</summary>
    public int CopiesInDeck { get; set; }
    /// <summary>Tactic points the card costs in a tactic.</summary>
    public int Cost { get; set; }
    /// <summary>Store price (in-game money); 0 = not sold.</summary>
    public long Price { get; set; }
}

public enum CareerStatus { Active = 0, Fired = 1 }

/// <summary>A save slot (career).</summary>
/// <summary>Difficulty of a career or friendly. Normal = 0 so older saves are Normal.</summary>
public enum Difficulty { Normal = 0, Easy = 1, Hard = 2 }

public class Career
{
    public int Id { get; set; }
    public Difficulty Difficulty { get; set; } = Difficulty.Normal;
    public string SaveName { get; set; } = "";
    public string ManagerName { get; set; } = "";
    public int TeamId { get; set; }
    public Team? Team { get; set; }
    /// <summary>The league the manager's team plays in this season.</summary>
    public int LeagueId { get; set; }
    public int Reputation { get; set; } = 30;
    public long Money { get; set; }
    /// <summary>The last tactic used (JSON) — loaded automatically for the next match.</summary>
    public string? LastTacticJson { get; set; }
    /// <summary>The three tactic plans of the Tactics tab and the active one (JSON, see Careers/TacticPlans.cs).</summary>
    public string? TacticPlansJson { get; set; }
    /// <summary>The "pick 1 of 3" draft at the start of a career while it is running (JSON, see CareerService.Cards.cs).</summary>
    public string? DraftJson { get; set; }
    /// <summary>Unlocked achievements (JSON: code → "season/week"), version 1.3.</summary>
    public string? AchievementsJson { get; set; }
    public CareerStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<CareerSeason> Seasons { get; set; } = new();
    public List<CareerMessage> Messages { get; set; } = new();
    public List<CareerCard> Cards { get; set; } = new();
}

public enum BoardExpectation { Survive = 0, MidTable = 1, Europe = 2, Title = 3 }

public class CareerSeason
{
    public int Id { get; set; }
    public int CareerId { get; set; }
    public Career? Career { get; set; }
    public int Year { get; set; }
    public int TeamId { get; set; }
    public int LeagueId { get; set; }
    public BoardExpectation Expectation { get; set; }
    /// <summary>European cup of the manager's team this season (or null).</summary>
    public string? EuropeanCode { get; set; }
    public int CurrentWeek { get; set; } = 1;
    public int TotalWeeks { get; set; }
    public bool Finished { get; set; }
    public string? SummaryJson { get; set; }
    /// <summary>Income and expenses of this season (1.3; buying and selling cards are not counted).</summary>
    public long Income { get; set; }
    public long Expenses { get; set; }
    /// <summary>JSON: which competition/round is played in which week.</summary>
    public string ScheduleJson { get; set; } = "[]";
    public List<Fixture> Fixtures { get; set; } = new();
    public List<SeasonTeam> Teams { get; set; } = new();
}

/// <summary>Which league a team plays in during one career season (changes with promotion/relegation).</summary>
public class SeasonTeam
{
    public int Id { get; set; }
    public int CareerSeasonId { get; set; }
    public int TeamId { get; set; }
    public int LeagueId { get; set; }
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

/// <summary>A card in the manager's collection (stays with the manager when changing clubs).</summary>
public class CareerCard
{
    public int Id { get; set; }
    public int CareerId { get; set; }
    public string Code { get; set; } = "";
    /// <summary>Tier of these copies: 0 Normal, 1 Rare, 2 Ultra Rare, 3 Legendary (version 1.2).</summary>
    public int Tier { get; set; }
    public int Count { get; set; }
}

/// <summary>Club upgrades bought in the store (per career, per club).</summary>
public class CareerClubUpgrade
{
    public int Id { get; set; }
    public int CareerId { get; set; }
    public int TeamId { get; set; }
    public int Level { get; set; }
}

/// <summary>A saved tactic. CareerId null = friendly-match tactic.</summary>
public class SavedTactic
{
    public int Id { get; set; }
    public int? CareerId { get; set; }
    public string Name { get; set; } = "";
    public string Json { get; set; } = "{}";
}

public enum MatchStatus { InProgress = 0, Finished = 1 }

/// <summary>A match being played (friendly or career). The full state is stored as JSON.</summary>
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
