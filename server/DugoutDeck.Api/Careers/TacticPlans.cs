using System.Text.Json;
using DugoutDeck.Api.Data.Entities;
using DugoutDeck.Api.Game;

namespace DugoutDeck.Api.Careers;

/// <summary>One of the three tactic plans of a career (Plan A / B / C), built in the career's Tactics tab.</summary>
public class TacticPlan
{
    public string Name { get; set; } = "";
    public TacticSpec? Tactic { get; set; }
}

/// <summary>The three plans of a career and which one is used by default before each match (stored as JSON on the career).</summary>
public class TacticPlanSet
{
    public const int Slots = 3;
    public static readonly string[] DefaultNames = ["Plan A", "Plan B", "Plan C"];

    public int Active { get; set; }
    public List<TacticPlan> Plans { get; set; } = new();

    public static TacticPlanSet Load(Career career)
    {
        TacticPlanSet? set = null;
        if (!string.IsNullOrEmpty(career.TacticPlansJson))
        {
            try { set = JsonSerializer.Deserialize<TacticPlanSet>(career.TacticPlansJson); }
            catch (JsonException) { set = null; }
        }
        set ??= new TacticPlanSet();
        while (set.Plans.Count < Slots) set.Plans.Add(new TacticPlan { Name = DefaultNames[set.Plans.Count] });
        if (set.Plans.Count > Slots) set.Plans = set.Plans.Take(Slots).ToList();
        for (var i = 0; i < Slots; i++)
            if (string.IsNullOrWhiteSpace(set.Plans[i].Name)) set.Plans[i].Name = DefaultNames[i];
        set.Active = Math.Clamp(set.Active, 0, Slots - 1);
        return set;
    }

    public void Save(Career career) => career.TacticPlansJson = JsonSerializer.Serialize(this);

    /// <summary>The tactic shown first before a match: the active plan if it has one, otherwise the last tactic played.</summary>
    public static TacticSpec? Preferred(Career career)
    {
        var plan = Load(career);
        if (plan.Plans[plan.Active].Tactic is { } t) return t;
        return career.LastTacticJson != null ? JsonSerializer.Deserialize<TacticSpec>(career.LastTacticJson) : null;
    }
}
