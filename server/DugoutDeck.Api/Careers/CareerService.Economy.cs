using DugoutDeck.Api.Data.Entities;
using DugoutDeck.Api.Game;
using Microsoft.EntityFrameworkCore;

namespace DugoutDeck.Api.Careers;

/// <summary>
/// Version 1.3 economy: money now also flows out. Every week the club pays wages and stadium costs (more in rich leagues and
/// for strong clubs) plus upkeep for its upgrades; packs cost more in rich leagues; spare copies can be merged into a higher
/// tier for a fee. Income and expenses are counted per season. A club deep in debt gets sacked.
/// </summary>
public partial class CareerService
{
    /// <summary>Base weekly wages + stadium at rating 70 in a ×1 league.</summary>
    public const long WeeklyBase = 55_000;
    /// <summary>Weekly upkeep per club upgrade level.</summary>
    public const long UpkeepPerLevel = 6_000;
    /// <summary>Merging: copies needed to make one copy of the next tier, and the fee. [Normal→Rare, Rare→Ultra, Ultra→Legendary]</summary>
    public static readonly int[] MergeNeed = [5, 4, 3];
    public static readonly long[] MergeFee = [50_000, 200_000, 600_000];

    /// <summary>Weekly expenses of a club: wages and stadium (league factor × (rating/70)²) plus upgrade upkeep.</summary>
    public static long WeeklyExpenses(double leagueFactor, int rating, int upgradeLevel) =>
        (long)Math.Round(WeeklyBase * leagueFactor * Math.Pow(rating / 70.0, 2) / 1000.0) * 1000 + upgradeLevel * UpkeepPerLevel;

    /// <summary>Pack prices follow the league: ×0.7 in second divisions … ×1.5 in the top five.</summary>
    public static double PackPriceFactor(double leagueFactor) => Math.Clamp(0.5 + leagueFactor / 2, 0.7, 1.5);

    public static long PackPrice(PackOffer offer, double leagueFactor) => (long)Math.Round(offer.Price * PackPriceFactor(leagueFactor) / 10_000.0) * 10_000;

    public async Task<double> CareerLeagueFactorAsync(Career career)
    {
        var season = await CurrentSeasonAsync(career.Id);
        var league = await db.Leagues.FindAsync(season.LeagueId);
        return league == null ? 1 : LeagueFactor(league);
    }

    /// <summary>Income (prize money, rewards): counted for the season.</summary>
    private static void Earn(Career career, CareerSeason season, long amount)
    {
        career.Money += amount;
        season.Income += amount;
    }

    /// <summary>The weekly bill. A warning when the club drops into the red.</summary>
    private async Task PayWeekAsync(Career career, CareerSeason season)
    {
        var league = await db.Leagues.FindAsync(season.LeagueId);
        var rating = (await RatingsAsync(career.Id))[season.TeamId];
        var level = await UpgradeLevelAsync(career.Id, season.TeamId);
        var cost = WeeklyExpenses(league == null ? 1 : LeagueFactor(league), rating, level);
        var before = career.Money;
        career.Money -= cost;
        season.Expenses += cost;
        if (before >= 0 && career.Money < 0)
            AddMessage(career, season, "In the red!",
                $"The club is {Math.Abs(career.Money) / 1000:N0}K in debt after this week's wages ({cost / 1000:N0}K). Sell cards or win money — the board will not accept a big debt at the end of the season.");
    }

    /// <summary>Debt limit at the end of the season: beyond it the manager is sacked.</summary>
    public static long DebtLimit(double leagueFactor) => (long)(2_000_000 * leagueFactor);

    /// <summary>Merges spare copies into one copy of the next tier (5 Normal → Rare, 4 Rare → Ultra Rare, 3 Ultra Rare → Legendary).</summary>
    public async Task<int> MergeAsync(Career career, string code, int fromTier)
    {
        if (fromTier < Tiers.Normal || fromTier > Tiers.UltraRare) throw new GameRuleException("Legendary is the highest tier.");
        if (!Tiers.HasTiers(code)) throw new GameRuleException("This card has no tiers.");
        var need = MergeNeed[fromTier];
        var fee = MergeFee[fromTier];
        var owned = await db.CareerCards.FirstOrDefaultAsync(c => c.CareerId == career.Id && c.Code == code && c.Tier == fromTier);
        if (owned == null || owned.Count < need) throw new GameRuleException($"You need {need} {Tiers.Name(fromTier)} copies to merge.");
        if (career.Money < fee) throw new GameRuleException("Not enough money for the merge fee.");
        owned.Count -= need;
        career.Money -= fee;
        await AddCardAsync(career.Id, code, fromTier + 1);
        var season = await CurrentSeasonAsync(career.Id);
        await UnlockAsync(career, season, "merge");
        await db.SaveChangesAsync();
        return fromTier + 1;
    }
}
