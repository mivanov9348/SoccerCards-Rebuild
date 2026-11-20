using System.Text.Json;
using DugoutDeck.Api.Data;
using DugoutDeck.Api.Data.Entities;
using DugoutDeck.Api.Game;
using Microsoft.EntityFrameworkCore;

namespace DugoutDeck.Api.Careers;

public record ScheduleSlot(int Week, string Kind, int Round, string Stage);

public record StandingRow(int Position, int TeamId, string Name, string ShortName, string PrimaryColor, string SecondaryColor,
    int Played, int Won, int Drawn, int Lost, int GoalsFor, int GoalsAgainst, int Points)
{
    public int GoalDifference => GoalsFor - GoalsAgainst;
}

public record OfferDto(int TeamId, string Name, string League, int Rating);

public class SeasonSummary
{
    public int Position { get; set; }
    public int Points { get; set; }
    public string League { get; set; } = "";
    public string Champion { get; set; } = "";
    public string? CupWinner { get; set; }
    public string? EuropeResult { get; set; }
    public string Expectation { get; set; } = "";
    public string Verdict { get; set; } = "";
    public bool Fired { get; set; }
    public int ReputationChange { get; set; }
    public long MoneyEarned { get; set; }
    public List<string> CardsWon { get; set; } = new();
    public List<string> Promoted { get; set; } = new();
    public List<string> Relegated { get; set; } = new();
    public List<OfferDto> Offers { get; set; } = new();
    /// <summary>League of every team next season (teamId → leagueId).</summary>
    public Dictionary<int, int> NextMembership { get; set; } = new();
    /// <summary>European entrants next season (competition code → team ids).</summary>
    public Dictionary<string, List<int>> NextEurope { get; set; } = new();
}

/// <summary>Career mode: all leagues, cups and European cups in one weekly calendar, board, money, cards and the store.</summary>
public class CareerService(AppDbContext db, MatchService matches)
{
    public static readonly string[] KnockoutStages = ["Round of 32", "Round of 16", "Quarter-final", "Semi-final", "Final"];
    public static readonly string[] EuropeCodes = ["UCL", "UEL", "UECL"];
    private static readonly double[] CupFractions = [0.12, 0.3, 0.5, 0.7, 0.88];
    private static readonly double[] EuroFractions = [0.2, 0.4, 0.6, 0.78, 0.95];
    private const int StartYear = 2026;
    private const int MaxUpgradeLevel = 10;
    public const long PackPrice = 600_000;

    public static string ExpectationText(BoardExpectation e) => e switch
    {
        BoardExpectation.Title => "Win the league",
        BoardExpectation.Europe => "Finish in the top 4",
        BoardExpectation.MidTable => "Finish in the top half (8th or better)",
        _ => "Avoid the drop (13th or better)"
    };

    private static int TargetPosition(BoardExpectation e) => e switch
    {
        BoardExpectation.Title => 1, BoardExpectation.Europe => 4, BoardExpectation.MidTable => 8, _ => 13
    };

    public static string Ordinal(int n) => n + (n % 100 is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });

    /// <summary>Money multiplier: top-five countries pay more, second divisions less.</summary>
    private static double LeagueFactor(League l) => (l.Tier == 1 ? 1.0 : 0.4) * (l.Country == "Bulgaria" ? 1.0 : 2.0);

    // ================================================================== ratings, collection

    /// <summary>Effective team ratings in this career (base rating + club upgrades bought in the store).</summary>
    public async Task<Dictionary<int, int>> RatingsAsync(int careerId)
    {
        var teams = await db.Teams.AsNoTracking().ToListAsync();
        var upgrades = await db.ClubUpgrades.Where(u => u.CareerId == careerId).ToDictionaryAsync(u => u.TeamId, u => u.Level);
        return teams.ToDictionary(t => t.Id, t => Math.Min(99, t.Overall + 2 * upgrades.GetValueOrDefault(t.Id)));
    }

    public async Task<Dictionary<string, int>> CollectionAsync(int careerId) =>
        await db.CareerCards.Where(c => c.CareerId == careerId).ToDictionaryAsync(c => c.Code, c => c.Count);

    private async Task AddCardAsync(int careerId, string code, int n = 1)
    {
        var card = db.CareerCards.Local.FirstOrDefault(c => c.CareerId == careerId && c.Code == code)
                   ?? await db.CareerCards.FirstOrDefaultAsync(c => c.CareerId == careerId && c.Code == code);
        if (card == null) db.CareerCards.Add(new CareerCard { CareerId = careerId, Code = code, Count = n });
        else card.Count += n;
    }

    /// <summary>A random collectable card: common 65%, rare 28%, epic 7%.</summary>
    private async Task<CardDefinition> RandomCardAsync(CardRarity? atLeast = null)
    {
        var pool = (await matches.CatalogAsync()).ByCode.Values
            .Where(c => c.Kind is CardKind.Field or CardKind.Hand && c.Rarity != CardRarity.None).ToList();
        var roll = Random.Shared.Next(100);
        var rarity = roll < 65 ? CardRarity.Common : roll < 93 ? CardRarity.Rare : CardRarity.Epic;
        if (atLeast is { } min && rarity < min) rarity = min;
        var options = pool.Where(c => c.Rarity == rarity).ToList();
        return options[Random.Shared.Next(options.Count)];
    }

    private async Task<List<string>> OpenPackAsync(int careerId, CardRarity guaranteed = CardRarity.Rare)
    {
        var names = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var c = await RandomCardAsync(i == 2 ? guaranteed : null);
            await AddCardAsync(careerId, c.Code);
            names.Add(c.Name);
        }
        return names;
    }

    // ================================================================== create

    public async Task<Career> CreateAsync(string saveName, string managerName, int teamId)
    {
        var team = await db.Teams.Include(t => t.League).FirstOrDefaultAsync(t => t.Id == teamId)
                   ?? throw new GameRuleException("Team not found.");
        if (team.League is not { IsPlayable: true })
            throw new GameRuleException("A career can only start with a team from a playable league.");

        var career = new Career
        {
            SaveName = string.IsNullOrWhiteSpace(saveName) ? $"{managerName} – {team.Name}" : saveName.Trim(),
            ManagerName = string.IsNullOrWhiteSpace(managerName) ? "Manager" : managerName.Trim(),
            TeamId = team.Id,
            LeagueId = team.League.Id,
            Reputation = 30,
            Money = (long)(2_000_000 * LeagueFactor(team.League)),
            LastTacticJson = JsonSerializer.Serialize(TacticRules.Default())
        };
        db.Careers.Add(career);
        await db.SaveChangesAsync();

        // starter collection
        foreach (var (code, n) in new[] { (Cards.Striker, 2), (Cards.BigChanceCard, 2), (Cards.LongShot, 3), (Cards.Captain, 1), (Cards.Scout, 2), (Cards.Calm, 1) })
            await AddCardAsync(career.Id, code, n);

        var leagues = await db.Leagues.Where(l => l.IsPlayable).ToListAsync();
        var leagueIds = leagues.Select(l => l.Id).ToHashSet();
        var membership = await db.Teams.Where(t => t.LeagueId != null && leagueIds.Contains(t.LeagueId.Value))
            .ToDictionaryAsync(t => t.Id, t => t.LeagueId!.Value);
        var ratings = await RatingsAsync(career.Id);

        // first season: European places go to the highest-rated clubs of each top division
        var europe = EuropeCodes.ToDictionary(c => c, _ => new List<int>());
        foreach (var l in leagues.Where(l => l.Tier == 1))
        {
            var ranked = membership.Where(m => m.Value == l.Id).Select(m => m.Key).OrderByDescending(id => ratings[id]).ToList();
            europe["UCL"].AddRange(ranked.Take(l.UclSpots));
            europe["UEL"].AddRange(ranked.Skip(l.UclSpots).Take(l.UelSpots + 1));
            europe["UECL"].AddRange(ranked.Skip(l.UclSpots + l.UelSpots + 1).Take(l.UeclSpots));
        }
        await FillEuropeAsync(europe, ratings);

        var season = await CreateSeasonAsync(career, StartYear, membership, europe, ratings);
        AddMessage(career, season, "Welcome!",
            $"The {team.Name} board welcomes you as the new manager. Season target: {ExpectationText(season.Expectation)}." +
            (season.EuropeanCode != null ? $" The club will play in the {await CompetitionName(season.EuropeanCode)}." : "") +
            $" Budget: {career.Money / 1000:N0}K. You start with a basic card collection — build your tactic before each match.");
        await db.SaveChangesAsync();
        return career;
    }

    /// <summary>Fills each European cup to 32 teams with the strongest clubs that have no league.</summary>
    private async Task FillEuropeAsync(Dictionary<string, List<int>> europe, Dictionary<int, int> ratings)
    {
        var pool = (await db.Teams.Where(t => t.LeagueId == null).Select(t => t.Id).ToListAsync())
            .OrderByDescending(id => ratings[id]).ToList();
        var used = europe.Values.SelectMany(v => v).ToHashSet();
        foreach (var code in EuropeCodes)
        {
            var list = europe[code];
            foreach (var id in pool)
            {
                if (list.Count >= 32) break;
                if (used.Add(id)) list.Add(id);
            }
            if (list.Count > 32) europe[code] = list.Take(32).ToList();
        }
    }

    private async Task<string> CompetitionName(string code) => (await db.Competitions.FirstAsync(c => c.Code == code)).Name;

    private async Task<CareerSeason> CreateSeasonAsync(Career career, int year, Dictionary<int, int> membership,
        Dictionary<string, List<int>> europe, Dictionary<int, int> ratings)
    {
        var rng = Random.Shared;
        var leagues = await db.Leagues.Where(l => l.IsPlayable).ToListAsync();
        var comps = await db.Competitions.ToListAsync();
        var leagueComp = comps.Where(c => c.Type == CompetitionType.League && c.LeagueId != null).ToDictionary(c => c.LeagueId!.Value);
        var cupComp = comps.Where(c => c.Type == CompetitionType.Cup && c.Country != null).ToDictionary(c => c.Country!);
        var euroComp = comps.Where(c => c.Type == CompetitionType.European).ToDictionary(c => c.Code);

        var teamLeague = membership[career.TeamId];
        var myLeagueRanked = membership.Where(m => m.Value == teamLeague).Select(m => m.Key).OrderByDescending(id => ratings[id]).ToList();
        var rank = myLeagueRanked.IndexOf(career.TeamId);
        var season = new CareerSeason
        {
            CareerId = career.Id, Year = year, TeamId = career.TeamId, LeagueId = teamLeague,
            Expectation = rank switch { <= 1 => BoardExpectation.Title, <= 4 => BoardExpectation.Europe, <= 9 => BoardExpectation.MidTable, _ => BoardExpectation.Survive },
            EuropeanCode = EuropeCodes.FirstOrDefault(c => europe.GetValueOrDefault(c)?.Contains(career.TeamId) == true)
        };
        foreach (var (teamId, leagueId) in membership)
            season.Teams.Add(new SeasonTeam { TeamId = teamId, LeagueId = leagueId });

        // ---- the calendar: league weeks, with cup and European weeks spread over the season
        var rounds = leagues.ToDictionary(l => l.Id, l =>
            RoundRobin(membership.Where(m => m.Value == l.Id).Select(m => m.Key).OrderBy(_ => rng.Next()).ToList()));
        var leagueWeeks = Math.Max(1, rounds.Values.Max(r => r.Count));
        var slots = new List<(double Pos, string Kind, int Round)>();
        for (var k = 1; k <= leagueWeeks; k++) slots.Add((k, "League", k));
        for (var i = 0; i < 5; i++) slots.Add((CupFractions[i] * leagueWeeks + 0.5, "Cup", i));
        for (var i = 0; i < 5; i++) slots.Add((EuroFractions[i] * leagueWeeks + 0.25, "Euro", i));
        var schedule = slots.OrderBy(s => s.Pos).Select((s, i) =>
            new ScheduleSlot(i + 1, s.Kind, s.Round, s.Kind == "League" ? $"Week {s.Round}" : KnockoutStages[s.Round])).ToList();
        var leagueSlotWeek = schedule.Where(s => s.Kind == "League").ToDictionary(s => s.Round, s => s.Week);
        season.TotalWeeks = schedule.Count;
        season.ScheduleJson = JsonSerializer.Serialize(schedule);

        // ---- league fixtures (a league with fewer rounds has rest weeks)
        foreach (var l in leagues)
        {
            var r = rounds[l.Id];
            for (var i = 0; i < r.Count; i++)
            {
                var slot = (int)Math.Ceiling((i + 1) * (double)leagueWeeks / r.Count);
                foreach (var (h, a) in r[i])
                    season.Fixtures.Add(new Fixture
                    {
                        CompetitionId = leagueComp[l.Id].Id, Week = leagueSlotWeek[slot], Round = i + 1, Stage = $"Round {i + 1}",
                        HomeTeamId = h, AwayTeamId = a
                    });
            }
        }

        // ---- national cups: 32 teams (top division + best of the second division)
        var cupWeek = schedule.First(s => s.Kind == "Cup" && s.Round == 0).Week;
        foreach (var country in leagues.Select(l => l.Country).Distinct())
        {
            if (!cupComp.TryGetValue(country, out var cup)) continue;
            var countryLeagues = leagues.Where(l => l.Country == country).OrderBy(l => l.Tier).Select(l => l.Id).ToList();
            var entrants = countryLeagues.SelectMany(lid => membership.Where(m => m.Value == lid).Select(m => m.Key)
                .OrderByDescending(id => ratings[id])).Take(32).OrderBy(_ => rng.Next()).ToList();
            if (entrants.Count < 2) continue;
            AddKnockout(season, cup.Id, cupWeek, 0, PowerOfTwo(entrants));
        }

        // ---- European cups (32 teams each)
        var euroWeek = schedule.First(s => s.Kind == "Euro" && s.Round == 0).Week;
        foreach (var code in EuropeCodes)
        {
            if (!europe.TryGetValue(code, out var entrants) || entrants.Count < 2) continue;
            AddKnockout(season, euroComp[code].Id, euroWeek, 0, PowerOfTwo(entrants.OrderBy(_ => rng.Next()).ToList()));
        }

        db.CareerSeasons.Add(season);
        career.LeagueId = teamLeague;
        await db.SaveChangesAsync();
        return season;
    }

    private static List<int> PowerOfTwo(List<int> teams)
    {
        var n = 1;
        while (n * 2 <= teams.Count && n < 32) n *= 2;
        return teams.Take(n).ToList();
    }

    private static void AddKnockout(CareerSeason season, int competitionId, int week, int round, List<int> teams)
    {
        var stage = KnockoutStages[Math.Clamp(KnockoutStages.Length - (int)Math.Log2(teams.Count), 0, KnockoutStages.Length - 1)];
        for (var i = 0; i + 1 < teams.Count; i += 2)
            season.Fixtures.Add(new Fixture
            {
                CompetitionId = competitionId, Week = week, Round = round, Stage = stage,
                HomeTeamId = teams[i], AwayTeamId = teams[i + 1], IsKnockout = true
            });
    }

    /// <summary>Double round robin ("circle" method).</summary>
    public static List<List<(int Home, int Away)>> RoundRobin(List<int> teams)
    {
        var list = new List<int>(teams);
        if (list.Count < 2) return new();
        if (list.Count % 2 == 1) list.Add(-1);
        var n = list.Count;
        var firstHalf = new List<List<(int, int)>>();
        for (var r = 0; r < n - 1; r++)
        {
            var round = new List<(int, int)>();
            for (var i = 0; i < n / 2; i++)
            {
                var a = list[i];
                var b = list[n - 1 - i];
                if (a == -1 || b == -1) continue;
                round.Add((r + i) % 2 == 0 ? (a, b) : (b, a));
            }
            firstHalf.Add(round);
            var last = list[n - 1];
            list.RemoveAt(n - 1);
            list.Insert(1, last);
        }
        var secondHalf = firstHalf.Select(r => r.Select(p => (p.Item2, p.Item1)).ToList()).ToList();
        return firstHalf.Concat(secondHalf).ToList();
    }

    // ================================================================== queries

    public async Task<Career?> LoadAsync(int id) => await db.Careers.Include(c => c.Team).FirstOrDefaultAsync(c => c.Id == id);

    public async Task<CareerSeason> CurrentSeasonAsync(int careerId) =>
        await db.CareerSeasons.Where(s => s.CareerId == careerId).OrderByDescending(s => s.Year).FirstAsync();

    public async Task<List<Fixture>> FixturesAsync(int seasonId, IEnumerable<int> competitionIds)
    {
        var ids = competitionIds.ToList();
        return await db.Fixtures.Include(f => f.HomeTeam).Include(f => f.AwayTeam).Include(f => f.Competition)
            .Where(f => f.CareerSeasonId == seasonId && ids.Contains(f.CompetitionId))
            .OrderBy(f => f.Week).ThenBy(f => f.Id).ToListAsync();
    }

    public async Task<List<StandingRow>> StandingsAsync(int seasonId, int leagueId)
    {
        var teamIds = await db.SeasonTeams.Where(t => t.CareerSeasonId == seasonId && t.LeagueId == leagueId).Select(t => t.TeamId).ToListAsync();
        var teams = await db.Teams.Where(t => teamIds.Contains(t.Id)).ToListAsync();
        var compId = await db.Competitions.Where(c => c.LeagueId == leagueId && c.Type == CompetitionType.League).Select(c => c.Id).FirstOrDefaultAsync();
        var fixtures = await db.Fixtures.Where(f => f.CareerSeasonId == seasonId && f.Played && f.CompetitionId == compId).ToListAsync();

        var rows = teams.Select(t =>
        {
            var home = fixtures.Where(f => f.HomeTeamId == t.Id).ToList();
            var away = fixtures.Where(f => f.AwayTeamId == t.Id).ToList();
            int w = home.Count(f => f.HomeGoals > f.AwayGoals) + away.Count(f => f.AwayGoals > f.HomeGoals);
            int d = home.Count(f => f.HomeGoals == f.AwayGoals) + away.Count(f => f.AwayGoals == f.HomeGoals);
            int l = home.Count + away.Count - w - d;
            int gf = home.Sum(f => f.HomeGoals ?? 0) + away.Sum(f => f.AwayGoals ?? 0);
            int ga = home.Sum(f => f.AwayGoals ?? 0) + away.Sum(f => f.HomeGoals ?? 0);
            return new StandingRow(0, t.Id, t.Name, t.ShortName, t.PrimaryColor, t.SecondaryColor, home.Count + away.Count, w, d, l, gf, ga, w * 3 + d);
        })
        .OrderByDescending(r => r.Points).ThenByDescending(r => r.GoalDifference).ThenByDescending(r => r.GoalsFor).ThenBy(r => r.Name).ToList();
        return rows.Select((r, i) => r with { Position = i + 1 }).ToList();
    }

    public async Task<Fixture?> UserFixtureThisWeekAsync(CareerSeason season) =>
        await db.Fixtures.Include(f => f.HomeTeam).Include(f => f.AwayTeam).Include(f => f.Competition)
            .FirstOrDefaultAsync(f => f.CareerSeasonId == season.Id && f.Week == season.CurrentWeek && !f.Played
                                      && (f.HomeTeamId == season.TeamId || f.AwayTeamId == season.TeamId));

    // ================================================================== playing

    /// <summary>Creates (or resumes) the match of this week. With <paramref name="useLastTactic"/> it kicks off at once.</summary>
    public async Task<Match> StartUserMatchAsync(Career career, bool useLastTactic)
    {
        var season = await CurrentSeasonAsync(career.Id);
        if (season.Finished) throw new GameRuleException("The season is over. Start the next season.");
        var fixture = await UserFixtureThisWeekAsync(season) ?? throw new GameRuleException("Your team does not play this week.");
        var existing = await db.Matches.FirstOrDefaultAsync(m => m.FixtureId == fixture.Id && m.Status == MatchStatus.InProgress);
        if (existing != null) return existing;

        var ratings = await RatingsAsync(career.Id);
        var humanSide = fixture.HomeTeamId == season.TeamId ? Side.Home : Side.Away;
        var match = await matches.CreateAsync(fixture.HomeTeam!, fixture.AwayTeam!, ratings[fixture.HomeTeamId], ratings[fixture.AwayTeamId],
            humanSide, fixture.IsKnockout, $"{fixture.Competition!.Name} – {fixture.Stage}", fixture.Id, career.Id);

        if (useLastTactic && career.LastTacticJson != null)
        {
            var tactic = JsonSerializer.Deserialize<TacticSpec>(career.LastTacticJson)!;
            var state = MatchService.Load(match);
            var (budget, handSize) = MatchService.HumanLimits(state);
            var collection = await CollectionAsync(career.Id);
            if (TacticRules.Validate(tactic, await matches.CatalogAsync(), budget, handSize, c => collection.GetValueOrDefault(c)).Count == 0)
            {
                (await matches.EngineAsync()).Start(state, humanSide == Side.Home ? tactic : null, humanSide == Side.Away ? tactic : null);
                MatchService.Save(match, state);
                await db.SaveChangesAsync();
            }
        }
        return match;
    }

    /// <summary>Simulates your match (or finishes a started one) and the rest of the week.</summary>
    public async Task SimulateUserWeekAsync(Career career)
    {
        var season = await CurrentSeasonAsync(career.Id);
        if (season.Finished) throw new GameRuleException("The season is over. Start the next season.");
        var fixture = await UserFixtureThisWeekAsync(season);
        if (fixture != null)
        {
            var existing = await db.Matches.FirstOrDefaultAsync(m => m.FixtureId == fixture.Id && m.Status == MatchStatus.InProgress);
            if (existing != null)
            {
                var state = MatchService.Load(existing);
                (await matches.EngineAsync()).SimulateToEnd(state);
                MatchService.Save(existing, state);
                ApplyResult(fixture, state);
            }
        }
        await CompleteWeekAsync(career, season);
    }

    /// <summary>Plays the weeks in which your team has no match, up to your next match (or the end of the season).</summary>
    public async Task ContinueAsync(Career career)
    {
        for (var i = 0; i < 80; i++)
        {
            var season = await CurrentSeasonAsync(career.Id);
            if (season.Finished) return;
            if (i > 0 && await UserFixtureThisWeekAsync(season) != null) return;
            await SimulateUserWeekAsync(career);
        }
    }

    public async Task OnUserMatchFinishedAsync(Match match, MatchState state)
    {
        if (match.FixtureId == null || match.CareerId == null) return;
        var fixture = await db.Fixtures.FindAsync(match.FixtureId.Value);
        var career = await LoadAsync(match.CareerId.Value);
        if (fixture == null || career == null || fixture.Played) return;
        ApplyResult(fixture, state);
        var season = await db.CareerSeasons.FindAsync(fixture.CareerSeasonId);
        if (season != null && season.CurrentWeek == fixture.Week) await CompleteWeekAsync(career, season);
    }

    private static void ApplyResult(Fixture f, MatchState s)
    {
        f.Played = true;
        f.HomeGoals = s.Home.Goals;
        f.AwayGoals = s.Away.Goals;
        f.HomePenalties = s.HomePenalties;
        f.AwayPenalties = s.AwayPenalties;
    }

    public async Task CompleteWeekAsync(Career career, CareerSeason season)
    {
        if (season.Finished) throw new GameRuleException("The season is over.");
        var week = season.CurrentWeek;
        var fixtures = await db.Fixtures.Include(f => f.HomeTeam).Include(f => f.AwayTeam).Include(f => f.Competition)
            .Where(f => f.CareerSeasonId == season.Id && f.Week == week).ToListAsync();
        var engine = await matches.EngineAsync();
        var ratings = await RatingsAsync(career.Id);

        foreach (var f in fixtures.Where(f => !f.Played))
            ApplyResult(f, MatchService.SimulateQuick(engine, f.HomeTeam!, f.AwayTeam!, ratings[f.HomeTeamId], ratings[f.AwayTeamId], f.IsKnockout));

        var mine = fixtures.FirstOrDefault(f => f.HomeTeamId == season.TeamId || f.AwayTeamId == season.TeamId);
        if (mine != null) await RewardMatchAsync(career, season, mine);

        await db.SaveChangesAsync();
        await CreateNextKnockoutRoundsAsync(season, week, fixtures);

        season.CurrentWeek++;
        career.UpdatedAt = DateTime.UtcNow;
        if (season.CurrentWeek > season.TotalWeeks) await EndSeasonAsync(career, season);
        await db.SaveChangesAsync();
    }

    /// <summary>Prize money and card rewards for the manager's match.</summary>
    private async Task RewardMatchAsync(Career career, CareerSeason season, Fixture mine)
    {
        var league = await db.Leagues.FindAsync(season.LeagueId);
        var factor = league == null ? 1 : LeagueFactor(league);
        var winner = mine.WinnerTeamId();
        var won = winner == season.TeamId;
        var draw = winner == null;
        var comp = mine.Competition!;
        var money = (long)((won ? 150_000 : draw ? 60_000 : 20_000) * factor);
        if (mine.IsKnockout && won)
            money += comp.Type == CompetitionType.European
                ? comp.Code switch { "UCL" => 1_500_000, "UEL" => 800_000, _ => 400_000 }
                : (long)(200_000 * factor);
        career.Money += money;

        var pens = mine.HomePenalties.HasValue ? $" (pens {mine.HomePenalties}:{mine.AwayPenalties})" : "";
        var body = $"{mine.HomeTeam!.Name} {mine.HomeGoals}:{mine.AwayGoals} {mine.AwayTeam!.Name}{pens} — " +
                   (won ? "a win!" : draw ? "a draw." : "a defeat.") + $" Income: {money / 1000:N0}K.";

        if (won && (Random.Shared.Next(100) < 30 || (mine.IsKnockout && comp.Type == CompetitionType.European)))
        {
            var card = await RandomCardAsync();
            await AddCardAsync(career.Id, card.Code);
            body += $" New card: {card.Name} ({card.Rarity})!";
        }
        AddMessage(career, season, $"{comp.Name}, {mine.Stage}", body);

        if (mine.IsKnockout && !won)
            AddMessage(career, season, "Knocked out", $"Your team is out of the {comp.Name} ({mine.Stage}).");
        if (mine.IsKnockout && won && mine.Stage == KnockoutStages[^1])
        {
            var cards = await OpenPackAsync(career.Id, CardRarity.Epic);
            career.Money += (long)(1_000_000 * factor);
            career.Reputation = Math.Min(100, career.Reputation + 5);
            AddMessage(career, season, "TROPHY!", $"You win the {comp.Name}! Bonus cards: {string.Join(", ", cards)}.");
        }
    }

    private async Task CreateNextKnockoutRoundsAsync(CareerSeason season, int week, List<Fixture> weekFixtures)
    {
        var slots = JsonSerializer.Deserialize<List<ScheduleSlot>>(season.ScheduleJson)!;
        var current = slots.FirstOrDefault(s => s.Week == week);
        if (current == null || current.Kind == "League") return;
        var next = slots.FirstOrDefault(s => s.Kind == current.Kind && s.Round == current.Round + 1);
        if (next == null) return;

        foreach (var group in weekFixtures.Where(f => f.IsKnockout).GroupBy(f => f.CompetitionId))
        {
            var winners = group.OrderBy(f => f.Id).Select(f => f.WinnerTeamId() ?? f.HomeTeamId).ToList();
            if (winners.Count >= 2) AddKnockout(season, group.Key, next.Week, next.Round, winners);
        }
        await db.SaveChangesAsync();
    }

    // ================================================================== season end

    private async Task EndSeasonAsync(Career career, CareerSeason season)
    {
        season.Finished = true;
        season.CurrentWeek = season.TotalWeeks;
        var leagues = await db.Leagues.Where(l => l.IsPlayable).ToListAsync();
        var comps = await db.Competitions.ToListAsync();
        var ratings = await RatingsAsync(career.Id);
        var teamNames = await db.Teams.ToDictionaryAsync(t => t.Id, t => t.Name);
        var myLeague = leagues.First(l => l.Id == season.LeagueId);

        var tables = new Dictionary<int, List<StandingRow>>();
        foreach (var l in leagues) tables[l.Id] = await StandingsAsync(season.Id, l.Id);

        // cup winners per country
        var finals = await db.Fixtures.Where(f => f.CareerSeasonId == season.Id && f.IsKnockout && f.Stage == "Final").ToListAsync();
        var cupWinners = new Dictionary<string, int>();
        foreach (var f in finals)
        {
            var comp = comps.First(c => c.Id == f.CompetitionId);
            if (comp.Type == CompetitionType.Cup && comp.Country != null && f.WinnerTeamId() is { } w) cupWinners[comp.Country] = w;
        }

        var table = tables[myLeague.Id];
        var pos = table.FindIndex(r => r.TeamId == season.TeamId) + 1;
        var factor = LeagueFactor(myLeague);
        var cupWinnerId = cupWinners.GetValueOrDefault(myLeague.Country);

        string? euroResult = null;
        var euroWins = 0;
        if (season.EuropeanCode != null)
        {
            var euroComp = comps.First(c => c.Code == season.EuropeanCode);
            var mine = await db.Fixtures.Where(f => f.CareerSeasonId == season.Id && f.CompetitionId == euroComp.Id
                                                     && (f.HomeTeamId == season.TeamId || f.AwayTeamId == season.TeamId)).OrderBy(f => f.Week).ToListAsync();
            euroWins = mine.Count(f => f.WinnerTeamId() == season.TeamId);
            var last = mine.LastOrDefault();
            euroResult = last == null ? null
                : last.WinnerTeamId() == season.TeamId && last.Stage == "Final" ? $"{euroComp.Name}: WINNERS!"
                : $"{euroComp.Name}: out in the {last.Stage}";
        }

        var score = TargetPosition(season.Expectation) - pos + (cupWinnerId == season.TeamId ? 2 : 0) + euroWins;
        var fired = score <= -4 || pos == table.Count;
        var verdict = fired ? "The board is unhappy with the results and has sacked you."
            : score >= 3 ? "The board is delighted! You smashed every target."
            : score >= 0 ? "The board is satisfied — you met the season target."
            : "The board is disappointed, but gives you one more chance.";
        var repChange = Math.Clamp(score * 3, -15, 15) + (cupWinnerId == season.TeamId ? 5 : 0) + euroWins * 2 + (pos == 1 ? 5 : 0);
        career.Reputation = Math.Clamp(career.Reputation + repChange, 0, 100);

        // money and cards
        var earned = (long)(1_500_000 * factor * ratings[season.TeamId] / 70.0) + (long)((table.Count - pos + 1) * 150_000 * factor);
        career.Money += earned;
        var cardsWon = new List<string>();
        if (score >= 0) cardsWon.AddRange(await OpenPackAsync(career.Id));
        if (pos == 1) cardsWon.AddRange(await OpenPackAsync(career.Id, CardRarity.Epic));

        // promotion / relegation per country
        var nextMembership = (await db.SeasonTeams.Where(t => t.CareerSeasonId == season.Id).ToListAsync()).ToDictionary(t => t.TeamId, t => t.LeagueId);
        var promoted = new List<string>();
        var relegated = new List<string>();
        foreach (var top in leagues.Where(l => l.Tier == 1))
        {
            var second = leagues.FirstOrDefault(l => l.Country == top.Country && l.Tier == 2);
            if (second == null) continue;
            var n = top.Relegation;
            var down = tables[top.Id].TakeLast(n).Select(r => r.TeamId).ToList();
            var up = tables[second.Id].Take(n).Select(r => r.TeamId).ToList();
            foreach (var id in down) nextMembership[id] = second.Id;
            foreach (var id in up) nextMembership[id] = top.Id;
            if (top.Country == myLeague.Country)
            {
                relegated.AddRange(down.Select(id => teamNames[id]));
                promoted.AddRange(up.Select(id => teamNames[id]));
            }
        }

        // European places for next season
        var europe = EuropeCodes.ToDictionary(c => c, _ => new List<int>());
        foreach (var top in leagues.Where(l => l.Tier == 1))
        {
            var ranked = tables[top.Id].Select(r => r.TeamId).ToList();
            var taken = new HashSet<int>();
            void Take(string code, int count)
            {
                foreach (var id in ranked.Where(id => !taken.Contains(id)).Take(count).ToList())
                {
                    europe[code].Add(id);
                    taken.Add(id);
                }
            }
            Take("UCL", top.UclSpots);
            Take("UEL", top.UelSpots);
            if (cupWinners.TryGetValue(top.Country, out var cw) && !taken.Contains(cw)) { europe["UEL"].Add(cw); taken.Add(cw); }
            else Take("UEL", 1);
            Take("UECL", top.UeclSpots);
        }
        await FillEuropeAsync(europe, ratings);

        // job offers (from every league)
        var myRating = ratings[season.TeamId];
        var rng = Random.Shared;
        var clubs = nextMembership.Keys.Where(id => id != season.TeamId).ToList();
        List<int> offers;
        if (fired)
            offers = clubs.Where(id => ratings[id] < myRating && ratings[id] >= myRating - 12).OrderBy(_ => rng.Next()).Take(2).ToList();
        else if (score >= 1 || career.Reputation >= 70)
        {
            var reach = 3 + score * 2 + career.Reputation / 20;
            offers = clubs.Where(id => ratings[id] > myRating && ratings[id] <= myRating + reach).OrderBy(_ => rng.Next()).Take(score >= 3 ? 3 : 2).ToList();
        }
        else offers = new();
        var leagueNames = leagues.ToDictionary(l => l.Id, l => l.Name);

        var summary = new SeasonSummary
        {
            Position = pos, Points = table[pos - 1].Points, League = myLeague.Name, Champion = table[0].Name,
            CupWinner = cupWinnerId == 0 ? null : teamNames[cupWinnerId], EuropeResult = euroResult,
            Expectation = ExpectationText(season.Expectation), Verdict = verdict, Fired = fired, ReputationChange = repChange,
            MoneyEarned = earned, CardsWon = cardsWon, Promoted = promoted, Relegated = relegated,
            Offers = offers.Select(id => new OfferDto(id, teamNames[id], leagueNames[nextMembership[id]], ratings[id])).ToList(),
            NextMembership = nextMembership, NextEurope = europe
        };
        season.SummaryJson = JsonSerializer.Serialize(summary);
        if (fired) career.Status = CareerStatus.Fired;

        AddMessage(career, season, "End of season",
            $"You finished {Ordinal(pos)} in the {myLeague.Name}. Champions: {table[0].Name}. {verdict} Season income: {earned / 1000:N0}K." +
            (cardsWon.Count > 0 ? $" Reward cards: {string.Join(", ", cardsWon)}." : ""));
        if (promoted.Count > 0)
            AddMessage(career, season, "Promotion & relegation", $"Promoted: {string.Join(", ", promoted)}. Relegated: {string.Join(", ", relegated)}.");
        foreach (var o in summary.Offers)
            AddMessage(career, season, "Job offer", $"{o.Name} ({o.League}) want you as their new manager.");
    }

    public static SeasonSummary? Summary(CareerSeason s) => s.SummaryJson == null ? null : JsonSerializer.Deserialize<SeasonSummary>(s.SummaryJson);

    public async Task NextSeasonAsync(Career career, int? acceptTeamId)
    {
        var season = await CurrentSeasonAsync(career.Id);
        if (!season.Finished) throw new GameRuleException("The current season is not over yet.");
        var summary = Summary(season)!;

        var teamId = season.TeamId;
        if (acceptTeamId is { } accepted && accepted != season.TeamId)
        {
            if (summary.Offers.All(o => o.TeamId != accepted)) throw new GameRuleException("You have no offer from this club.");
            teamId = accepted;
        }
        else if (career.Status == CareerStatus.Fired)
            throw new GameRuleException(summary.Offers.Count == 0
                ? "You were sacked and have no offers — your career is over."
                : "You were sacked — you must accept one of the offers.");

        career.TeamId = teamId;
        career.Status = CareerStatus.Active;
        var ratings = await RatingsAsync(career.Id);
        var newSeason = await CreateSeasonAsync(career, season.Year + 1, summary.NextMembership, summary.NextEurope, ratings);
        var team = await db.Teams.FindAsync(teamId);
        var league = await db.Leagues.FindAsync(newSeason.LeagueId);
        AddMessage(career, newSeason, $"Season {newSeason.Year}/{(newSeason.Year + 1) % 100:00}",
            $"A new season in charge of {team!.Name} ({league!.Name}). Board target: {ExpectationText(newSeason.Expectation)}." +
            (newSeason.EuropeanCode != null ? $" You will play in the {await CompetitionName(newSeason.EuropeanCode)}." : ""));
        career.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // ================================================================== store

    public static long UpgradeCost(int level) => 3_000_000L * (level + 1);

    public async Task<List<string>> BuyPackAsync(Career career)
    {
        if (career.Money < PackPrice) throw new GameRuleException("Not enough money for a pack.");
        career.Money -= PackPrice;
        var cards = await OpenPackAsync(career.Id);
        await db.SaveChangesAsync();
        return cards;
    }

    public async Task BuyCardAsync(Career career, string code)
    {
        var card = (await matches.CatalogAsync()).Get(code);
        if (card == null || card.Price <= 0) throw new GameRuleException("This card is not sold.");
        if (career.Money < card.Price) throw new GameRuleException("Not enough money.");
        career.Money -= card.Price;
        await AddCardAsync(career.Id, code);
        await db.SaveChangesAsync();
    }

    public async Task SellCardAsync(Career career, string code)
    {
        var owned = await db.CareerCards.FirstOrDefaultAsync(c => c.CareerId == career.Id && c.Code == code);
        if (owned == null || owned.Count < 2) throw new GameRuleException("You can only sell duplicates.");
        var card = (await matches.CatalogAsync()).Get(code)!;
        owned.Count--;
        career.Money += card.Price / 3;
        await db.SaveChangesAsync();
    }

    public async Task<int> UpgradeLevelAsync(int careerId, int teamId) =>
        (await db.ClubUpgrades.FirstOrDefaultAsync(u => u.CareerId == careerId && u.TeamId == teamId))?.Level ?? 0;

    public async Task<int> UpgradeClubAsync(Career career)
    {
        var up = await db.ClubUpgrades.FirstOrDefaultAsync(u => u.CareerId == career.Id && u.TeamId == career.TeamId);
        var level = up?.Level ?? 0;
        if (level >= MaxUpgradeLevel) throw new GameRuleException("The club is fully upgraded.");
        var cost = UpgradeCost(level);
        if (career.Money < cost) throw new GameRuleException("Not enough money for the upgrade.");
        career.Money -= cost;
        if (up == null) db.ClubUpgrades.Add(up = new CareerClubUpgrade { CareerId = career.Id, TeamId = career.TeamId });
        up.Level++;
        await db.SaveChangesAsync();
        return up.Level;
    }

    private void AddMessage(Career career, CareerSeason season, string title, string body) =>
        db.CareerMessages.Add(new CareerMessage { CareerId = career.Id, Year = season.Year, Week = season.CurrentWeek, Title = title, Body = body });
}
