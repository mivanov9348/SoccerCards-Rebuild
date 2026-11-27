using System.Text.Json;
using DugoutDeck.Api.Data.Entities;
using DugoutDeck.Api.Game;
using Microsoft.EntityFrameworkCore;

namespace DugoutDeck.Api.Careers;

/// <summary>An achievement: a small reward (money or a pack) the first time it is reached.</summary>
public record Achievement(string Code, string Name, string Description, long Money = 0, CardRarity? Pack = null);

/// <summary>
/// Version 1.3 "career with memory": the trophy cabinet and the history of every season (from the season summaries),
/// statistics of the matches the manager played, records from all results, and ~25 achievements with small rewards.
/// </summary>
public partial class CareerService
{
    public static readonly Achievement[] Achievements =
    [
        // a single match (played by the manager)
        new("first_win", "First win", "Win a match.", 100_000),
        new("clean_sheet", "Clean sheet", "Win or draw without conceding.", 100_000),
        new("untouchable", "Untouchable", "A match in which the opponent steals none of your cards.", 150_000),
        new("thief", "Pickpocket", "Steal 3 of the opponent's cards in one match.", 200_000),
        new("stolen_striker", "Thanks for the striker", "Score with the opponent's stolen Striker.", 200_000),
        new("five_goals", "Goal fest", "Score 5 goals in a match.", 200_000),
        new("big_win", "Thrashing", "Win by 4 goals or more.", 200_000),
        new("ten_men", "Ten men, no problem", "Win after a red card.", 200_000),
        new("penalty_save", "The keeper guessed right", "Stop an opponent's penalty.", 100_000),
        new("shootout", "Nerves of steel", "Win a penalty shootout.", 200_000),
        new("legendary_play", "Legend on the pitch", "A Legendary card of yours takes effect in a match.", 150_000),
        new("trap_master", "Trap master", "Your Offside Traps spring twice in one match.", 150_000),
        new("var", "VAR saves you", "VAR disallows an opponent's goal.", 100_000),
        // the club's cards
        new("collector", "Collector", "The club owns 50 cards.", 200_000),
        new("legendary_owner", "Something special", "The club owns a Legendary card.", 200_000),
        new("full_field", "Full squad", "The club owns every field card at least once.", 0, CardRarity.Rare),
        new("merge", "Upgrade!", "Merge cards into a higher tier.", 100_000),
        // a season
        new("title", "Champions!", "Win the league.", 0, CardRarity.Epic),
        new("cup", "Cup winners", "Win the national cup.", 0, CardRarity.Rare),
        new("europe", "Kings of Europe", "Win a European cup.", 1_000_000, CardRarity.Epic),
        new("unbeaten", "Invincibles", "A league season without a defeat.", 1_000_000),
        new("underdog", "Fairy tale", "Win the league with a club rated below 70.", 1_000_000),
        new("promotion", "Going up", "Win promotion.", 300_000),
        new("delighted", "Board's favourite", "The board is delighted at the end of a season.", 300_000),
        new("loyal", "One-club manager", "Three seasons at the same club.", 300_000),
    ];

    private static Dictionary<string, string> Unlocked(Career career) =>
        string.IsNullOrEmpty(career.AchievementsJson) ? new() : JsonSerializer.Deserialize<Dictionary<string, string>>(career.AchievementsJson) ?? new();

    /// <summary>Unlocks an achievement once and pays its reward.</summary>
    private async Task UnlockAsync(Career career, CareerSeason season, string code)
    {
        var done = Unlocked(career);
        if (done.ContainsKey(code)) return;
        var a = Achievements.FirstOrDefault(x => x.Code == code);
        if (a == null) return;
        done[code] = $"{season.Year}/{season.CurrentWeek}";
        career.AchievementsJson = JsonSerializer.Serialize(done);
        var reward = new List<string>();
        if (a.Money > 0)
        {
            Earn(career, season, a.Money);
            reward.Add($"{a.Money / 1000:N0}K");
        }
        if (a.Pack is { } rarity) reward.Add("a pack: " + string.Join(", ", await OpenPackAsync(career.Id, rarity)));
        AddMessage(career, season, $"🏅 Achievement: {a.Name}", $"{a.Description} Reward: {string.Join(" and ", reward)}.");
    }

    /// <summary>Achievements of a match the manager played (called when it is finished).</summary>
    private async Task MatchAchievementsAsync(Career career, CareerSeason season, MatchState s)
    {
        if (s.HumanSide is not { } side) return;
        var me = s.Team(side);
        var opp = s.Team(side.Other());
        var myPens = side == Side.Home ? s.HomePenalties : s.AwayPenalties;
        var oppPens = side == Side.Home ? s.AwayPenalties : s.HomePenalties;
        var won = me.Goals > opp.Goals || (me.Goals == opp.Goals && myPens > oppPens);
        var list = new List<string>();
        if (won) list.Add("first_win");
        if (opp.Goals == 0 && me.Goals >= opp.Goals) list.Add("clean_sheet");
        if (opp.Stats.CardsStolen == 0) list.Add("untouchable");
        if (me.Stats.CardsStolen >= 3) list.Add("thief");
        if (me.Played.Any(p => p.Code == Cards.Striker && p.How == "stolen") && me.Stats.GoalSources.GetValueOrDefault("Striker") > 0) list.Add("stolen_striker");
        if (me.Goals >= 5) list.Add("five_goals");
        if (me.Goals - opp.Goals >= 4) list.Add("big_win");
        if (won && me.Stats.RedCards > 0) list.Add("ten_men");
        if (opp.Stats.Penalties > opp.Stats.GoalSources.GetValueOrDefault("Penalty")) list.Add("penalty_save");
        if (myPens != null && myPens > oppPens) list.Add("shootout");
        if (me.Played.Any(p => p.Tier == Tiers.Legendary)) list.Add("legendary_play");
        if (me.Played.Count(p => p.How == "trap") >= 2) list.Add("trap_master");
        if (opp.Stats.GoalsDisallowed > 0) list.Add("var");
        foreach (var code in list) await UnlockAsync(career, season, code);
        await CollectionAchievementsAsync(career, season);
    }

    /// <summary>Achievements about the club's cards.</summary>
    private async Task CollectionAchievementsAsync(Career career, CareerSeason season)
    {
        var rows = await db.CareerCards.Where(c => c.CareerId == career.Id && c.Count > 0).ToListAsync();
        rows.AddRange(db.CareerCards.Local.Where(c => c.CareerId == career.Id && c.Count > 0 && c.Id == 0));
        if (rows.Sum(r => r.Count) >= 50) await UnlockAsync(career, season, "collector");
        if (rows.Any(r => r.Tier == Tiers.Legendary)) await UnlockAsync(career, season, "legendary_owner");
        var fieldCodes = (await matches.CatalogAsync()).OfKind(CardKind.Field).Where(c => c.Rarity != CardRarity.None).Select(c => c.Code).ToList();
        if (fieldCodes.All(code => rows.Any(r => r.Code == code))) await UnlockAsync(career, season, "full_field");
    }

    private async Task SeasonAchievementsAsync(Career career, CareerSeason season, SeasonSummary sum, List<StandingRow> table, int rating, int score)
    {
        var league = await db.Leagues.FindAsync(season.LeagueId);
        var mine = table.First(r => r.TeamId == season.TeamId);
        if (sum.Position == 1) await UnlockAsync(career, season, "title");
        if (sum.CupWon) await UnlockAsync(career, season, "cup");
        if (sum.EuropeWon) await UnlockAsync(career, season, "europe");
        if (mine.Lost == 0 && mine.Played > 0) await UnlockAsync(career, season, "unbeaten");
        if (sum.Position == 1 && rating < 70) await UnlockAsync(career, season, "underdog");
        if (league?.Tier == 2 && sum.Promoted.Contains(sum.Team)) await UnlockAsync(career, season, "promotion");
        if (score >= 3) await UnlockAsync(career, season, "delighted");
        var seasonsHere = await db.CareerSeasons.CountAsync(x => x.CareerId == career.Id && x.TeamId == season.TeamId);
        if (seasonsHere >= 3) await UnlockAsync(career, season, "loyal");
    }

    // ================================================================== the history tab

    public async Task<object> HistoryAsync(Career career)
    {
        var seasons = await db.CareerSeasons.Where(s => s.CareerId == career.Id).OrderBy(s => s.Year).ToListAsync();
        var teamNames = await db.Teams.ToDictionaryAsync(t => t.Id, t => t.Name);
        var leagueNames = await db.Leagues.ToDictionaryAsync(l => l.Id, l => l.Name);
        var cupNames = await db.Competitions.Where(c => c.Type == CompetitionType.Cup).ToDictionaryAsync(c => c.Country ?? "", c => c.Name);
        var leagueCountry = await db.Leagues.ToDictionaryAsync(l => l.Id, l => l.Country);

        // the seasons, newest first
        var seasonRows = seasons.Select(s =>
        {
            var sum = Summary(s);
            return new
            {
                year = s.Year, team = sum?.Team ?? teamNames.GetValueOrDefault(s.TeamId, "?"), league = sum?.League ?? leagueNames.GetValueOrDefault(s.LeagueId, "?"),
                finished = s.Finished, position = sum?.Position, points = sum?.Points, goalsFor = sum?.GoalsFor, goalsAgainst = sum?.GoalsAgainst,
                cup = sum?.CupWon == true, europe = sum?.EuropeResult, verdict = sum?.Verdict, fired = sum?.Fired == true,
                income = s.Income, expenses = s.Expenses
            };
        }).Reverse().ToList();

        // the trophy cabinet
        var trophies = new List<object>();
        foreach (var s in seasons)
        {
            if (Summary(s) is not { } sum) continue;
            var team = sum.Team.Length > 0 ? sum.Team : teamNames.GetValueOrDefault(s.TeamId, "?");
            if (sum.Position == 1) trophies.Add(new { kind = "league", name = sum.League, year = s.Year, team });
            if (sum.CupWon) trophies.Add(new { kind = "cup", name = cupNames.GetValueOrDefault(leagueCountry.GetValueOrDefault(s.LeagueId, ""), "National cup"), year = s.Year, team });
            if (sum.EuropeWon) trophies.Add(new { kind = "europe", name = (sum.EuropeResult ?? "").Split(':')[0], year = s.Year, team });
        }

        // records from every result of the manager's teams (simulated matches too)
        var seasonTeam = seasons.ToDictionary(s => s.Id, s => s.TeamId);
        var seasonYear = seasons.ToDictionary(s => s.Id, s => s.Year);
        var ids = seasonTeam.Keys.ToList();
        var fixtures = (await db.Fixtures.Include(f => f.HomeTeam).Include(f => f.AwayTeam)
                .Where(f => ids.Contains(f.CareerSeasonId) && f.Played).ToListAsync())
            .Where(f => f.HomeTeamId == seasonTeam[f.CareerSeasonId] || f.AwayTeamId == seasonTeam[f.CareerSeasonId])
            .OrderBy(f => seasonYear[f.CareerSeasonId]).ThenBy(f => f.Week).ToList();
        int w = 0, d = 0, l = 0, gf = 0, ga = 0, unbeaten = 0, bestUnbeaten = 0, wins = 0, bestWins = 0;
        (int Diff, string Text)? bestWin = null, worstLoss = null;
        foreach (var f in fixtures)
        {
            var home = f.HomeTeamId == seasonTeam[f.CareerSeasonId];
            var mineG = (home ? f.HomeGoals : f.AwayGoals) ?? 0;
            var theirG = (home ? f.AwayGoals : f.HomeGoals) ?? 0;
            gf += mineG; ga += theirG;
            var winner = f.WinnerTeamId();
            var me = seasonTeam[f.CareerSeasonId];
            var text = $"{f.HomeTeam!.Name} {f.HomeGoals}:{f.AwayGoals} {f.AwayTeam!.Name} ({seasonYear[f.CareerSeasonId]})";
            if (winner == me) { w++; unbeaten++; wins++; }
            else if (winner == null) { d++; unbeaten++; wins = 0; }
            else { l++; unbeaten = 0; wins = 0; }
            bestUnbeaten = Math.Max(bestUnbeaten, unbeaten);
            bestWins = Math.Max(bestWins, wins);
            var diff = mineG - theirG;
            if (diff > 0 && (bestWin == null || diff > bestWin.Value.Diff)) bestWin = (diff, text);
            if (diff < 0 && (worstLoss == null || diff < worstLoss.Value.Diff)) worstLoss = (diff, text);
        }

        // statistics of the matches the manager played card by card
        var states = (await db.Matches.Where(m => m.CareerId == career.Id && m.Status == MatchStatus.Finished).ToListAsync())
            .Select(m => { try { return MatchService.Load(m); } catch (JsonException) { return null; } })
            .Where(s => s?.HumanSide != null).Select(s => s!).ToList();
        var sources = new Dictionary<string, int>();
        int pensAwarded = 0, pensScored = 0, stolenByMe = 0, stolenFromMe = 0, played = states.Count, legendaryPlays = 0;
        var stolenCards = new Dictionary<string, int>();
        var myCards = new Dictionary<string, int>();
        foreach (var s in states)
        {
            var me = s.Team(s.HumanSide!.Value);
            var opp = s.Team(s.HumanSide!.Value.Other());
            foreach (var (k, v) in me.Stats.GoalSources) sources[k] = sources.GetValueOrDefault(k) + v;
            pensAwarded += me.Stats.Penalties;
            pensScored += me.Stats.GoalSources.GetValueOrDefault("Penalty");
            stolenByMe += me.Stats.CardsStolen;
            stolenFromMe += opp.Stats.CardsStolen;
            legendaryPlays += me.Played.Count(p => p.Tier == Tiers.Legendary);
            foreach (var p in opp.Played.Where(p => p.How == "stolen")) stolenCards[p.Code] = stolenCards.GetValueOrDefault(p.Code) + 1;
            foreach (var p in me.Played.Where(p => p.How is "own" or "hand")) myCards[p.Code] = myCards.GetValueOrDefault(p.Code) + 1;
        }
        object? Top(Dictionary<string, int> d) => d.Count == 0 ? null : d.OrderByDescending(kv => kv.Value).Select(kv => new { code = kv.Key, name = Cards.Name(kv.Key), count = kv.Value }).First();

        var done = Unlocked(career);
        return new
        {
            seasons = seasonRows,
            trophies,
            record = new { played = w + d + l, won = w, drawn = d, lost = l, goalsFor = gf, goalsAgainst = ga },
            records = new
            {
                biggestWin = bestWin?.Text, worstDefeat = worstLoss?.Text, longestUnbeaten = bestUnbeaten, longestWinningRun = bestWins
            },
            stats = new
            {
                matchesPlayed = played,
                goalSources = sources.OrderByDescending(kv => kv.Value).Select(kv => new { source = kv.Key, goals = kv.Value }),
                penaltiesAwarded = pensAwarded, penaltiesScored = pensScored, stolenByMe, stolenFromMe, legendaryPlays,
                mostStolen = Top(stolenCards), favourite = Top(myCards)
            },
            achievements = Achievements.Select(a => new
            {
                a.Code, a.Name, a.Description, unlocked = done.ContainsKey(a.Code), when = done.GetValueOrDefault(a.Code),
                reward = string.Join(" + ", new[] { a.Money > 0 ? $"€{a.Money / 1000:N0}K" : null, a.Pack != null ? (a.Pack == CardRarity.Epic ? "epic pack" : "pack") : null }.Where(x => x != null))
            })
        };
    }
}
