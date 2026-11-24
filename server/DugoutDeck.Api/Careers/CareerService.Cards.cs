using System.Text.Json;
using DugoutDeck.Api.Data.Entities;
using DugoutDeck.Api.Game;
using Microsoft.EntityFrameworkCore;

namespace DugoutDeck.Api.Careers;

/// <summary>A card from a pack, the draft or a reward: which card and which tier.</summary>
public record PackCard(CardDefinition Card, int Tier);

/// <summary>A pack in the store: number of cards, price and whether one card is guaranteed Rare or better.</summary>
public record PackOffer(int Size, long Price, bool GuaranteedRarePlus);

/// <summary>The draft at the start of a career: round n of Total, three cards to choose from.</summary>
public class DraftState
{
    public int Round { get; set; } = 1;
    public int Total { get; set; } = 10;
    public List<DraftOption> Options { get; set; } = new();
    public List<DraftOption> Picked { get; set; } = new();
}

public class DraftOption
{
    public string Code { get; set; } = "";
    public int Tier { get; set; }
}

/// <summary>
/// Version 1.2 "Collection & market": the club's cards with tiers, packs of 3 / 5 / 10, a market that is always open
/// (cheap selling), the "pick 1 of 3" draft at the start of a career, a free season pack and new squad cards when the
/// manager moves to another club. The cards belong to the club, not to the manager.
/// </summary>
public partial class CareerService
{
    public static readonly PackOffer[] Packs = [new(3, 500_000, false), new(5, 800_000, true), new(10, 1_500_000, true)];

    /// <summary>Draft: the first rounds offer field cards, the last ones hand cards.</summary>
    public const int DraftRounds = 10, DraftFieldRounds = 6;

    public static string CardLabel(string name, int tier) => tier > Tiers.Normal ? $"{name} ({Tiers.Name(tier)})" : name;

    // ------------------------------------------------------------------ collection

    public async Task<Dictionary<(string Code, int Tier), int>> CollectionAsync(int careerId) =>
        (await db.CareerCards.Where(c => c.CareerId == careerId && c.Count > 0).ToListAsync())
        .ToDictionary(c => (c.Code, c.Tier), c => c.Count);

    /// <summary>How many copies of a card in a tier the club owns (for tactic validation).</summary>
    public static Func<string, int, int> Owned(Dictionary<(string Code, int Tier), int> collection) =>
        (code, tier) => collection.GetValueOrDefault((code, tier));

    private async Task AddCardAsync(int careerId, string code, int tier, int n = 1)
    {
        var card = db.CareerCards.Local.FirstOrDefault(c => c.CareerId == careerId && c.Code == code && c.Tier == tier)
                   ?? await db.CareerCards.FirstOrDefaultAsync(c => c.CareerId == careerId && c.Code == code && c.Tier == tier);
        if (card == null) db.CareerCards.Add(new CareerCard { CareerId = careerId, Code = code, Tier = tier, Count = n });
        else card.Count += n;
    }

    /// <summary>A random collectable card type: common 65%, rare 28%, epic 7% (the card's own scarcity, not the tier).</summary>
    private async Task<CardDefinition> RandomCardAsync(CardRarity? atLeast = null, CardKind? kind = null)
    {
        var pool = (await matches.CatalogAsync()).ByCode.Values
            .Where(c => c.Kind is CardKind.Field or CardKind.Hand && c.Rarity != CardRarity.None && (kind == null || c.Kind == kind)).ToList();
        var roll = Random.Shared.Next(100);
        var rarity = roll < 65 ? CardRarity.Common : roll < 93 ? CardRarity.Rare : CardRarity.Epic;
        if (atLeast is { } min && rarity < min) rarity = min;
        var options = pool.Where(c => c.Rarity == rarity).ToList();
        if (options.Count == 0) options = pool;
        return options[Random.Shared.Next(options.Count)];
    }

    /// <summary>Opens a pack into the collection. The last card is at least of <paramref name="guaranteed"/> scarcity and,
    /// for the bigger packs, at least Rare tier.</summary>
    private async Task<List<PackCard>> OpenPackCardsAsync(int careerId, int size = 3, CardRarity guaranteed = CardRarity.Rare, bool rarePlus = false)
    {
        var cards = new List<PackCard>();
        for (var i = 0; i < size; i++)
        {
            var last = i == size - 1;
            var c = await RandomCardAsync(last ? guaranteed : null);
            var tier = Tiers.RollPack(Random.Shared, last && rarePlus && cards.All(x => x.Tier == Tiers.Normal) ? Tiers.Rare : Tiers.Normal);
            await AddCardAsync(careerId, c.Code, tier);
            cards.Add(new PackCard(c, tier));
        }
        return cards;
    }

    /// <summary>A free pack (rewards, season pack): card names with their tier.</summary>
    private async Task<List<string>> OpenPackAsync(int careerId, CardRarity guaranteed = CardRarity.Rare) =>
        (await OpenPackCardsAsync(careerId, 3, guaranteed)).Select(c => CardLabel(c.Card.Name, c.Tier)).ToList();

    /// <summary>A new club's own cards: 6 field and 4 hand cards that the club can use, tiers by rating.</summary>
    private async Task<List<string>> GiveClubCardsAsync(Career career, int rating)
    {
        db.CareerCards.RemoveRange(db.CareerCards.Where(c => c.CareerId == career.Id));
        await db.SaveChangesAsync();
        var names = new List<string>();
        foreach (var (kind, n) in new[] { (CardKind.Field, 6), (CardKind.Hand, 4) })
            for (var i = 0; i < n; i++)
            {
                CardDefinition c;
                var guard = 0;
                do c = await RandomCardAsync(kind: kind);
                while (!TacticRules.AiCanUse(c, rating) && guard++ < 20);
                var tier = Tiers.RollForRating(rating, Random.Shared);
                await AddCardAsync(career.Id, c.Code, tier);
                names.Add(CardLabel(c.Name, tier));
            }
        // the old plans stay: they are checked against the new cards before the next match
        return names;
    }

    // ------------------------------------------------------------------ store & market (always open)

    public async Task<List<PackCard>> BuyPackAsync(Career career, int size)
    {
        var offer = Packs.FirstOrDefault(p => p.Size == size) ?? throw new GameRuleException("Unknown pack.");
        if (career.Money < offer.Price) throw new GameRuleException("Not enough money for this pack.");
        career.Money -= offer.Price;
        var cards = await OpenPackCardsAsync(career.Id, offer.Size, CardRarity.Rare, offer.GuaranteedRarePlus);
        await db.SaveChangesAsync();
        return cards;
    }

    /// <summary>Single cards are sold as Normal copies at the card's store price.</summary>
    public async Task BuyCardAsync(Career career, string code)
    {
        var card = (await matches.CatalogAsync()).Get(code);
        if (card == null || card.Price <= 0) throw new GameRuleException("This card is not sold.");
        if (career.Money < card.Price) throw new GameRuleException("Not enough money.");
        career.Money -= card.Price;
        await AddCardAsync(career.Id, code, Tiers.Normal);
        await db.SaveChangesAsync();
    }

    /// <summary>Sells one copy (any card, any tier) for its tier's price.</summary>
    public async Task<long> SellCardAsync(Career career, string code, int tier)
    {
        var owned = await db.CareerCards.FirstOrDefaultAsync(c => c.CareerId == career.Id && c.Code == code && c.Tier == tier);
        if (owned == null || owned.Count < 1) throw new GameRuleException("You don't own that card.");
        owned.Count--;
        var price = Tiers.SellPrice(tier);
        career.Money += price;
        await db.SaveChangesAsync();
        return price;
    }

    /// <summary>Sells every Normal copy beyond the first of each card.</summary>
    public async Task<(int Cards, long Money)> SellDuplicatesAsync(Career career)
    {
        var rows = await db.CareerCards.Where(c => c.CareerId == career.Id && c.Tier == Tiers.Normal && c.Count > 1).ToListAsync();
        var n = rows.Sum(r => r.Count - 1);
        foreach (var r in rows) r.Count = 1;
        var money = n * Tiers.SellPrice(Tiers.Normal);
        career.Money += money;
        await db.SaveChangesAsync();
        return (n, money);
    }

    // ------------------------------------------------------------------ the draft

    public static DraftState? Draft(Career career) =>
        string.IsNullOrEmpty(career.DraftJson) ? null : JsonSerializer.Deserialize<DraftState>(career.DraftJson);

    private static void EnsureDrafted(Career career)
    {
        if (Draft(career) != null) throw new GameRuleException("Finish the draft first: pick your club's cards.");
    }

    private async Task StartDraftAsync(Career career)
    {
        var draft = new DraftState { Total = DraftRounds };
        draft.Options = await DraftOptionsAsync(1);
        career.DraftJson = JsonSerializer.Serialize(draft);
        await db.SaveChangesAsync();
    }

    /// <summary>Three different cards (field cards in the first rounds, hand cards in the last), tiers like a pack.</summary>
    private async Task<List<DraftOption>> DraftOptionsAsync(int round)
    {
        var kind = round <= DraftFieldRounds ? CardKind.Field : CardKind.Hand;
        var options = new List<DraftOption>();
        var guard = 0;
        while (options.Count < 3 && guard++ < 50)
        {
            var c = await RandomCardAsync(kind: kind);
            if (options.Any(o => o.Code == c.Code)) continue;
            options.Add(new DraftOption { Code = c.Code, Tier = Tiers.RollPack(Random.Shared) });
        }
        return options;
    }

    /// <summary>Takes one of the three cards; after the last round the draft is over and the season can start.</summary>
    public async Task<DraftState?> DraftPickAsync(Career career, int pick)
    {
        var draft = Draft(career) ?? throw new GameRuleException("There is no draft.");
        if (pick < 0 || pick >= draft.Options.Count) throw new GameRuleException("Pick one of the three cards.");
        var chosen = draft.Options[pick];
        await AddCardAsync(career.Id, chosen.Code, chosen.Tier);
        draft.Picked.Add(chosen);
        if (draft.Round >= draft.Total)
        {
            career.DraftJson = null;
            var catalog = await matches.CatalogAsync();
            var season = await CurrentSeasonAsync(career.Id);
            AddMessage(career, season, "Draft complete",
                $"Your club's cards: {string.Join(", ", draft.Picked.Select(p => CardLabel(catalog.Get(p.Code)?.Name ?? p.Code, p.Tier)))}. Build your first tactic in the Tactics tab or before the first match.");
            await db.SaveChangesAsync();
            return null;
        }
        draft.Round++;
        draft.Options = await DraftOptionsAsync(draft.Round);
        career.DraftJson = JsonSerializer.Serialize(draft);
        await db.SaveChangesAsync();
        return draft;
    }
}
