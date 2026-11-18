using System.Text.Json;
using FootballCards.Api.Data;
using FootballCards.Api.Data.Entities;
using FootballCards.Api.Game;
using Microsoft.EntityFrameworkCore;

namespace FootballCards.Api.Careers;

public record ScheduleSlot(int Week, string Competition, int Round, string Stage);

public record StandingRow(int Position, int TeamId, string Name, string ShortName, string PrimaryColor, string SecondaryColor,
    int Played, int Won, int Drawn, int Lost, int GoalsFor, int GoalsAgainst, int Points)
{
    public int GoalDifference => GoalsFor - GoalsAgainst;
}

public record OfferDto(int TeamId, string Name, int Overall);

public class SeasonSummary
{
    public int Position { get; set; }
    public int Points { get; set; }
    public string Champion { get; set; } = "";
    public string? CupWinner { get; set; }
    public string? EuropeResult { get; set; }
    public string Expectation { get; set; } = "";
    public string Verdict { get; set; } = "";
    public bool Fired { get; set; }
    public int ReputationChange { get; set; }
    public List<OfferDto> Offers { get; set; } = new();
    public int? CupWinnerId { get; set; }
    public List<int> FinalTable { get; set; } = new();
}

/// <summary>Логиката на режим Кариера: сезони, график, симулация на кръговете, оценка на борда и оферти.</summary>
public class CareerService(AppDbContext db, MatchService matches)
{
    public const string LeagueCode = "BG1";
    public const string CupCode = "BGCUP";
    public static readonly string[] KnockoutStages = ["Round of 16", "Quarter-final", "Semi-final", "Final"];
    private static readonly Dictionary<int, int> CupAfterRound = new() { [5] = 0, [12] = 1, [20] = 2, [28] = 3 };
    private static readonly Dictionary<int, int> EuropeAfterRound = new() { [3] = 0, [9] = 1, [16] = 2, [24] = 3 };
    private const int StartYear = 2026;

    public static string ExpectationText(BoardExpectation e) => e switch
    {
        BoardExpectation.Title => "Win the title",
        BoardExpectation.Europe => "Finish in the top 4",
        BoardExpectation.MidTable => "Finish in the top half (8th or better)",
        _ => "Avoid the drop (13th or better)"
    };

    private static int TargetPosition(BoardExpectation e) => e switch
    {
        BoardExpectation.Title => 1, BoardExpectation.Europe => 4, BoardExpectation.MidTable => 8, _ => 13
    };

    // ------------------------------------------------------------------ create

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
            Reputation = 30
        };
        db.Careers.Add(career);
        await db.SaveChangesAsync();

        var ranking = await RankingAsync(career.LeagueId);
        var rank = ranking.FindIndex(t => t.Id == team.Id);
        string? euro = rank switch { 0 => "UCL", 1 => "UEL", 2 or 3 => "UECL", _ => null };
        var season = await CreateSeasonAsync(career, team.Id, StartYear, euro);

        AddMessage(career, season, "Welcome!",
            $"The {team.Name} board welcomes you as the new manager. Season target: {ExpectationText(season.Expectation)}." +
            (euro != null ? $" The club will play in the {await CompetitionName(euro)}." : ""));
        await db.SaveChangesAsync();
        return career;
    }

    private async Task<List<Team>> RankingAsync(int leagueId) =>
        (await db.Teams.Where(t => t.LeagueId == leagueId).ToListAsync())
        .OrderByDescending(t => t.Overall).ThenBy(t => t.Name).ToList();

    private async Task<string> CompetitionName(string code) =>
        (await db.Competitions.FirstAsync(c => c.Code == code)).Name;

    private async Task<CareerSeason> CreateSeasonAsync(Career career, int teamId, int year, string? euroCode)
    {
        var ranking = await RankingAsync(career.LeagueId);
        var rank = ranking.FindIndex(t => t.Id == teamId);
        var expectation = rank switch
        {
            <= 1 => BoardExpectation.Title,
            <= 4 => BoardExpectation.Europe,
            <= 9 => BoardExpectation.MidTable,
            _ => BoardExpectation.Survive
        };

        var season = new CareerSeason
        {
            CareerId = career.Id, Year = year, TeamId = teamId, Expectation = expectation, EuropeanCode = euroCode
        };
        var comps = await db.Competitions.ToDictionaryAsync(c => c.Code);
        var rng = Random.Shared;

        // --- график
        var slots = new List<ScheduleSlot>();
        var week = 1;
        var teamIds = ranking.Select(t => t.Id).OrderBy(_ => rng.Next()).ToList();
        var rounds = RoundRobin(teamIds);
        for (var r = 1; r <= rounds.Count; r++)
        {
            slots.Add(new ScheduleSlot(week, LeagueCode, r, $"Round {r}"));
            foreach (var (h, a) in rounds[r - 1])
                season.Fixtures.Add(new Fixture
                {
                    CompetitionId = comps[LeagueCode].Id, Week = week, Round = r, Stage = $"Round {r}",
                    HomeTeamId = h, AwayTeamId = a
                });
            week++;
            if (CupAfterRound.TryGetValue(r, out var cupRound) && teamIds.Count >= 16)
                slots.Add(new ScheduleSlot(week++, CupCode, cupRound, KnockoutStages[cupRound]));
            if (euroCode != null && EuropeAfterRound.TryGetValue(r, out var euroRound))
                slots.Add(new ScheduleSlot(week++, euroCode, euroRound, KnockoutStages[euroRound]));
        }
        season.TotalWeeks = week - 1;
        season.ScheduleJson = JsonSerializer.Serialize(slots);

        // --- първи кръг на купата: 16 отбора от лигата
        var cupSlot = slots.FirstOrDefault(s => s.Competition == CupCode);
        if (cupSlot != null)
            AddKnockoutFixtures(season, comps[CupCode].Id, cupSlot, teamIds.Take(16).OrderBy(_ => rng.Next()).ToList());

        // --- европейски турнир: нашият отбор + 15 клуба от Европа според нивото на турнира
        var euroSlot = slots.FirstOrDefault(s => s.Competition == euroCode);
        if (euroCode != null && euroSlot != null)
        {
            var leagueCountry = (await db.Leagues.FindAsync(career.LeagueId))!.Country;
            var pool = (await db.Teams.Where(t => t.Country != leagueCountry).ToListAsync())
                .OrderByDescending(t => t.Overall).ToList();
            var (from, to) = euroCode switch { "UCL" => (0, 24), "UEL" => (12, 46), _ => (28, pool.Count) };
            var band = pool.Skip(Math.Min(from, Math.Max(0, pool.Count - 15))).Take(Math.Max(15, to - from)).ToList();
            var participants = band.OrderBy(_ => rng.Next()).Take(15).Select(t => t.Id).ToList();
            participants.Add(teamId);
            AddKnockoutFixtures(season, comps[euroCode].Id, euroSlot, participants.OrderBy(_ => rng.Next()).ToList());
        }

        db.CareerSeasons.Add(season);
        await db.SaveChangesAsync();
        return season;
    }

    private static void AddKnockoutFixtures(CareerSeason season, int competitionId, ScheduleSlot slot, List<int> teams)
    {
        for (var i = 0; i + 1 < teams.Count; i += 2)
            season.Fixtures.Add(new Fixture
            {
                CompetitionId = competitionId, Week = slot.Week, Round = slot.Round, Stage = slot.Stage,
                HomeTeamId = teams[i], AwayTeamId = teams[i + 1], IsKnockout = true
            });
    }

    /// <summary>Двоен кръгов турнир по метода на "въртенето".</summary>
    public static List<List<(int Home, int Away)>> RoundRobin(List<int> teams)
    {
        var list = new List<int>(teams);
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
            // въртене: първият остава, последният отива на второ място
            var last = list[n - 1];
            list.RemoveAt(n - 1);
            list.Insert(1, last);
        }
        var secondHalf = firstHalf.Select(r => r.Select(p => (p.Item2, p.Item1)).ToList()).ToList();
        return firstHalf.Concat(secondHalf).ToList();
    }

    // ------------------------------------------------------------------ queries

    public async Task<Career?> LoadAsync(int id) =>
        await db.Careers.Include(c => c.Team).FirstOrDefaultAsync(c => c.Id == id);

    public async Task<CareerSeason> CurrentSeasonAsync(int careerId) =>
        await db.CareerSeasons.Where(s => s.CareerId == careerId).OrderByDescending(s => s.Year).FirstAsync();

    public async Task<List<Fixture>> FixturesAsync(int seasonId) =>
        await db.Fixtures.Include(f => f.HomeTeam).Include(f => f.AwayTeam).Include(f => f.Competition)
            .Where(f => f.CareerSeasonId == seasonId).OrderBy(f => f.Week).ThenBy(f => f.Id).ToListAsync();

    public async Task<List<StandingRow>> StandingsAsync(int seasonId, int leagueId)
    {
        var teams = await db.Teams.Where(t => t.LeagueId == leagueId).ToListAsync();
        var fixtures = await db.Fixtures.Where(f => f.CareerSeasonId == seasonId && f.Played && f.Competition!.Code == LeagueCode)
            .ToListAsync();
        // включваме и отбори, които вече не са в лигата, но имат мачове този сезон
        var ids = fixtures.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).Distinct().ToList();
        var extra = ids.Except(teams.Select(t => t.Id)).ToList();
        if (extra.Count > 0) teams.AddRange(await db.Teams.Where(t => extra.Contains(t.Id)).ToListAsync());
        if (fixtures.Count == 0)
        {
            var allIds = await db.Fixtures.Where(f => f.CareerSeasonId == seasonId && f.Competition!.Code == LeagueCode)
                .Select(f => f.HomeTeamId).Distinct().ToListAsync();
            if (allIds.Count > 0) teams = teams.Where(t => allIds.Contains(t.Id)).ToList();
        }

        var rows = teams.Select(t =>
        {
            var home = fixtures.Where(f => f.HomeTeamId == t.Id).ToList();
            var away = fixtures.Where(f => f.AwayTeamId == t.Id).ToList();
            int w = home.Count(f => f.HomeGoals > f.AwayGoals) + away.Count(f => f.AwayGoals > f.HomeGoals);
            int d = home.Count(f => f.HomeGoals == f.AwayGoals) + away.Count(f => f.AwayGoals == f.HomeGoals);
            int l = home.Count + away.Count - w - d;
            int gf = home.Sum(f => f.HomeGoals ?? 0) + away.Sum(f => f.AwayGoals ?? 0);
            int ga = home.Sum(f => f.AwayGoals ?? 0) + away.Sum(f => f.HomeGoals ?? 0);
            return new StandingRow(0, t.Id, t.Name, t.ShortName, t.PrimaryColor, t.SecondaryColor,
                home.Count + away.Count, w, d, l, gf, ga, w * 3 + d);
        })
        .OrderByDescending(r => r.Points).ThenByDescending(r => r.GoalDifference).ThenByDescending(r => r.GoalsFor)
        .ThenBy(r => r.Name).ToList();
        return rows.Select((r, i) => r with { Position = i + 1 }).ToList();
    }

    public async Task<Fixture?> UserFixtureThisWeekAsync(CareerSeason season) =>
        await db.Fixtures.Include(f => f.HomeTeam).Include(f => f.AwayTeam).Include(f => f.Competition)
            .FirstOrDefaultAsync(f => f.CareerSeasonId == season.Id && f.Week == season.CurrentWeek && !f.Played
                                      && (f.HomeTeamId == season.TeamId || f.AwayTeamId == season.TeamId));

    // ------------------------------------------------------------------ playing

    /// <summary>Стартира (или продължава) интерактивния мач от текущата седмица.</summary>
    public async Task<Match> StartUserMatchAsync(Career career)
    {
        var season = await CurrentSeasonAsync(career.Id);
        EnsurePlayable(career, season);
        var fixture = await UserFixtureThisWeekAsync(season) ?? throw new GameRuleException("Your team does not play this week.");
        var existing = await db.Matches.FirstOrDefaultAsync(m => m.FixtureId == fixture.Id && m.Status == MatchStatus.InProgress);
        if (existing != null) return existing;

        var humanSide = fixture.HomeTeamId == season.TeamId ? Side.Home : Side.Away;
        return await matches.CreateAsync(fixture.HomeTeam!, fixture.AwayTeam!, humanSide,
            fixture.IsKnockout, $"{fixture.Competition!.Name} – {fixture.Stage}", fixture.Id, career.Id);
    }

    /// <summary>Симулира мача на отбора ви (или довършва започнат мач) и цялата седмица.</summary>
    public async Task SimulateUserWeekAsync(Career career)
    {
        var season = await CurrentSeasonAsync(career.Id);
        EnsurePlayable(career, season);
        var fixture = await UserFixtureThisWeekAsync(season);
        if (fixture != null)
        {
            var existing = await db.Matches.FirstOrDefaultAsync(m => m.FixtureId == fixture.Id && m.Status == MatchStatus.InProgress);
            if (existing != null)
            {
                var state = MatchService.Load(existing);
                matches.SimulateRest(state);
                MatchService.Save(existing, state);
                ApplyResult(fixture, state);
            }
        }
        await CompleteWeekAsync(career, season);
    }

    /// <summary>Записва резултата от изиграния мач и придвижва кариерата напред.</summary>
    public async Task OnUserMatchFinishedAsync(Match match, MatchState state)
    {
        if (match.FixtureId == null || match.CareerId == null) return;
        var fixture = await db.Fixtures.FindAsync(match.FixtureId.Value);
        var career = await LoadAsync(match.CareerId.Value);
        if (fixture == null || career == null || fixture.Played) return;
        ApplyResult(fixture, state);
        var season = await db.CareerSeasons.FindAsync(fixture.CareerSeasonId);
        if (season != null && season.CurrentWeek == fixture.Week)
            await CompleteWeekAsync(career, season);
    }

    private static void ApplyResult(Fixture f, MatchState s)
    {
        f.Played = true;
        f.HomeGoals = s.Home.Goals;
        f.AwayGoals = s.Away.Goals;
        f.HomePenalties = s.HomePenalties;
        f.AwayPenalties = s.AwayPenalties;
    }

    private static void EnsurePlayable(Career career, CareerSeason season)
    {
        if (season.Finished) throw new GameRuleException("The season is over. Start the next season.");
    }

    public async Task CompleteWeekAsync(Career career, CareerSeason season)
    {
        if (season.Finished) throw new GameRuleException("The season is over.");
        var week = season.CurrentWeek;
        var fixtures = await db.Fixtures.Include(f => f.HomeTeam).Include(f => f.AwayTeam).Include(f => f.Competition)
            .Where(f => f.CareerSeasonId == season.Id && f.Week == week).ToListAsync();
        var deck = await matches.DeckAsync();

        foreach (var f in fixtures.Where(f => !f.Played))
            ApplyResult(f, matches.SimulateQuick(f.HomeTeam!, f.AwayTeam!, f.IsKnockout, deck));

        var mine = fixtures.FirstOrDefault(f => f.HomeTeamId == season.TeamId || f.AwayTeamId == season.TeamId);
        if (mine != null)
        {
            var won = mine.WinnerTeamId() == season.TeamId;
            var draw = mine.WinnerTeamId() == null;
            var pens = mine.HomePenalties.HasValue ? $" (pens {mine.HomePenalties}:{mine.AwayPenalties})" : "";
            AddMessage(career, season, $"{mine.Competition!.Name}, {mine.Stage}",
                $"{mine.HomeTeam!.Name} {mine.HomeGoals}:{mine.AwayGoals} {mine.AwayTeam!.Name}{pens} — " +
                (won ? "a win!" : draw ? "a draw." : "a defeat."));
            if (mine.IsKnockout && !won)
                AddMessage(career, season, "Knocked out", $"Your team is out of the {mine.Competition.Name} ({mine.Stage}).");
            if (mine.IsKnockout && won && mine.Stage == KnockoutStages[^1])
            {
                AddMessage(career, season, "TROPHY!", $"You win the {mine.Competition.Name}! The fans are celebrating in the streets.");
                career.Reputation = Math.Min(100, career.Reputation + 5);
            }
        }

        await db.SaveChangesAsync();
        await CreateNextKnockoutRoundsAsync(season, week);

        season.CurrentWeek++;
        career.UpdatedAt = DateTime.UtcNow;
        if (season.CurrentWeek > season.TotalWeeks)
            await EndSeasonAsync(career, season);
        await db.SaveChangesAsync();
    }

    private async Task CreateNextKnockoutRoundsAsync(CareerSeason season, int week)
    {
        var slots = JsonSerializer.Deserialize<List<ScheduleSlot>>(season.ScheduleJson)!;
        var current = slots.FirstOrDefault(s => s.Week == week);
        if (current == null || current.Competition == LeagueCode) return;
        var next = slots.FirstOrDefault(s => s.Competition == current.Competition && s.Round == current.Round + 1);
        if (next == null) return;

        var played = await db.Fixtures.Where(f => f.CareerSeasonId == season.Id && f.Week == week)
            .OrderBy(f => f.Id).ToListAsync();
        var winners = played.Select(f => f.WinnerTeamId() ?? f.HomeTeamId).ToList();
        var compId = played.First().CompetitionId;
        AddKnockoutFixtures(season, compId, next, winners);
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ season end

    private async Task EndSeasonAsync(Career career, CareerSeason season)
    {
        season.Finished = true;
        season.CurrentWeek = season.TotalWeeks;
        var table = await StandingsAsync(season.Id, career.LeagueId);
        var pos = table.FindIndex(r => r.TeamId == season.TeamId) + 1;
        var fixtures = await FixturesAsync(season.Id);

        var cupFinal = fixtures.LastOrDefault(f => f.Competition!.Code == CupCode && f.Stage == KnockoutStages[^1]);
        var cupWinnerId = cupFinal?.WinnerTeamId();
        var cupWinner = cupWinnerId == null ? null : cupFinal!.HomeTeamId == cupWinnerId ? cupFinal.HomeTeam!.Name : cupFinal.AwayTeam!.Name;

        string? euroResult = null;
        var euroWins = 0;
        if (season.EuropeanCode != null)
        {
            var mine = fixtures.Where(f => f.Competition!.Code == season.EuropeanCode
                                           && (f.HomeTeamId == season.TeamId || f.AwayTeamId == season.TeamId)).ToList();
            euroWins = mine.Count(f => f.WinnerTeamId() == season.TeamId);
            var last = mine.LastOrDefault();
            var compName = await CompetitionName(season.EuropeanCode);
            euroResult = last == null ? null
                : last.WinnerTeamId() == season.TeamId && last.Stage == KnockoutStages[^1] ? $"{compName}: WINNERS!"
                : $"{compName}: out in the {last.Stage}";
        }

        var score = TargetPosition(season.Expectation) - pos + (cupWinnerId == season.TeamId ? 2 : 0) + euroWins;
        var fired = score <= -4 || pos == table.Count;
        var verdict = fired ? "The board is unhappy with the results and has sacked you."
            : score >= 3 ? "The board is delighted! You smashed every target."
            : score >= 0 ? "The board is satisfied — you met the season target."
            : "The board is disappointed, but gives you one more chance.";

        var repChange = Math.Clamp(score * 3, -15, 15) + (cupWinnerId == season.TeamId ? 5 : 0) + euroWins * 2;
        career.Reputation = Math.Clamp(career.Reputation + repChange, 0, 100);

        // оферти
        var ranking = await RankingAsync(career.LeagueId);
        var myRank = ranking.FindIndex(t => t.Id == season.TeamId);
        var rng = Random.Shared;
        List<Team> offers;
        if (fired)
            // уволненият треньор получава предложения от по-скромни клубове (долната част на класацията по сила)
            offers = ranking.Where(t => t.Id != season.TeamId).Skip(Math.Max(0, ranking.Count - 7))
                .OrderBy(_ => rng.Next()).Take(2).ToList();
        else if (score >= 1 || career.Reputation >= 70)
        {
            var reach = Math.Max(2, score + 2 + career.Reputation / 25);
            offers = ranking.Take(myRank).Where((t, i) => i >= myRank - reach)
                .OrderBy(_ => rng.Next()).Take(score >= 3 ? 3 : 2).ToList();
            if (score < 2) offers = offers.Where(_ => rng.Next(100) < 65).ToList();
        }
        else offers = new();

        var summary = new SeasonSummary
        {
            Position = pos,
            Points = table[pos - 1].Points,
            Champion = table[0].Name,
            CupWinner = cupWinner,
            CupWinnerId = cupWinnerId,
            EuropeResult = euroResult,
            Expectation = ExpectationText(season.Expectation),
            Verdict = verdict,
            Fired = fired,
            ReputationChange = repChange,
            Offers = offers.Select(t => new OfferDto(t.Id, t.Name, t.Overall)).ToList(),
            FinalTable = table.Select(r => r.TeamId).ToList()
        };
        season.SummaryJson = JsonSerializer.Serialize(summary);
        if (fired) career.Status = CareerStatus.Fired;

        AddMessage(career, season, "End of season",
            $"You finished {Ordinal(pos)}. Champions: {table[0].Name}. {verdict}");
        foreach (var o in offers)
            AddMessage(career, season, "Job offer", $"{o.Name} want you as their new manager.");
    }

    public static SeasonSummary? Summary(CareerSeason s) =>
        s.SummaryJson == null ? null : JsonSerializer.Deserialize<SeasonSummary>(s.SummaryJson);

    public async Task NextSeasonAsync(Career career, int? acceptTeamId)
    {
        var season = await CurrentSeasonAsync(career.Id);
        if (!season.Finished) throw new GameRuleException("The current season is not over yet.");
        var summary = Summary(season)!;

        var teamId = season.TeamId;
        if (acceptTeamId is { } accepted && accepted != season.TeamId)
        {
            if (summary.Offers.All(o => o.TeamId != accepted))
                throw new GameRuleException("You have no offer from this club.");
            teamId = accepted;
        }
        else if (career.Status == CareerStatus.Fired)
        {
            throw new GameRuleException(summary.Offers.Count == 0
                ? "You were sacked and have no offers — your career is over."
                : "You were sacked — you must accept one of the offers.");
        }

        career.TeamId = teamId;
        career.Status = CareerStatus.Active;

        // европейска квота според крайното класиране и купата
        var pos = summary.FinalTable.IndexOf(teamId) + 1;
        string? euro = pos switch { 1 => "UCL", 2 => "UEL", 3 => "UECL", _ => null };
        if (summary.CupWinnerId == teamId && pos > 2) euro = "UEL";

        var newSeason = await CreateSeasonAsync(career, teamId, season.Year + 1, euro);
        var team = await db.Teams.FindAsync(teamId);
        AddMessage(career, newSeason, $"Season {newSeason.Year}/{(newSeason.Year + 1) % 100:00}",
            $"A new season in charge of {team!.Name}. Board target: {ExpectationText(newSeason.Expectation)}." +
            (euro != null ? $" You will play in the {await CompetitionName(euro)}." : ""));
        career.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public static string Ordinal(int n) => n + (n % 100 is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });

    private void AddMessage(Career career, CareerSeason season, string title, string body) =>
        db.CareerMessages.Add(new CareerMessage
        {
            CareerId = career.Id, Year = season.Year, Week = season.CurrentWeek, Title = title, Body = body
        });
}
