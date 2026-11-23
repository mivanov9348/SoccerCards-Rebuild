using DugoutDeck.Api.Data.Entities;

namespace DugoutDeck.Api.Careers;

/// <summary>A coloured band of a league table: positions From..To (1-based) and what they mean.</summary>
public record TableZone(int From, int To, string Kind, string Label);

/// <summary>
/// The coloured places of each league table, taken from the same rules the end of the season uses
/// (CareerService.EndSeasonAsync): champion, European places, promotion and relegation.
/// </summary>
public static class TableZones
{
    public static List<TableZone> For(League league, IReadOnlyCollection<League> all, int teamCount)
    {
        var zones = new List<TableZone>();
        if (teamCount < 2) return zones;
        var top = all.FirstOrDefault(l => l.Country == league.Country && l.Tier == 1);
        var lower = all.FirstOrDefault(l => l.Country == league.Country && l.Tier == league.Tier + 1);

        if (league.Tier == 1)
        {
            // the European places in table order (as in EndSeasonAsync); the Continental Cup has one more place,
            // which goes to the cup winner or else to the next team in the table
            var slots = new List<(string Kind, string Label)>();
            for (var i = 0; i < league.UclSpots; i++) slots.Add(("ucl", "Champions Cup"));
            for (var i = 0; i < league.UelSpots + 1; i++) slots.Add(("uel", "Continental Cup (the last place: or the cup winner)"));
            for (var i = 0; i < league.UeclSpots; i++) slots.Add(("uecl", "Challenge Cup"));
            // the champion is shown on its own (gold) and takes the first of those places
            var first = slots.Count > 0 ? slots[0].Label.Split(" (")[0] : null;
            zones.Add(new TableZone(1, 1, "champion", first != null ? $"Champion ({first})" : "Champion"));
            var pos = 2;
            foreach (var g in slots.Skip(1).GroupBy(s => s.Kind))
            {
                var to = Math.Min(teamCount, pos + g.Count() - 1);
                if (to >= pos) zones.Add(new TableZone(pos, to, g.Key, g.First().Label));
                pos = to + 1;
            }
            if (lower != null && league.Relegation > 0)
                zones.Add(new TableZone(Math.Max(pos, teamCount - league.Relegation + 1), teamCount, "relegation", "Relegation"));
        }
        else if (top != null && top.Relegation > 0)
        {
            zones.Add(new TableZone(1, Math.Min(teamCount, top.Relegation), "promotion", "Promotion"));
        }
        return zones;
    }
}
