using DugoutDeck.Api.Data.Entities;
using DugoutDeck.Api.Game;
using Xunit.Abstractions;
namespace DugoutDeck.Tests;
/// <summary>A human who plays at random (any card, any hand card, any choice) must never break or stall the computer.</summary>
public class RandomHumanTests(ITestOutputHelper output)
{
    [Fact]
    public void RandomHuman_NeverBreaksTheComputer()
    {
        var catalogField = typeof(MatchEngineTests).GetField("Catalog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var catalog = (CardCatalog)catalogField.GetValue(null)!;
        var hands = Cards.HandCodes;
        var fields = Cards.FieldCodes;
        int errors = 0, stuck = 0;
        for (var i = 0; i < 800; i++)
        {
            var rng = new Random(i);
            var engine = new MatchEngine(new Random(i * 7 + 1), catalog);
            var human = i % 2 == 0 ? Side.Home : Side.Away;
            var s = engine.Create(new MatchSetup(new Team { Id = 1, Name = "A", City = "X" }, new Team { Id = 2, Name = "B", City = "Y" }, 55 + i % 40, 55 + (i * 3) % 40, human, i % 3 == 0, "T"));
            var tactic = new TacticSpec { Field = Enumerable.Range(0, 4).Select(_ => fields[rng.Next(fields.Length)]).ToList(), Hand = Enumerable.Range(0, 3).Select(_ => hands[rng.Next(hands.Length)]).ToList() };
            engine.Start(s, human == Side.Home ? tactic : null, human == Side.Away ? tactic : null);
            var guard = 0;
            try
            {
                while (s.Phase != MatchPhase.Finished && guard++ < 1500)
                {
                    var role = s.Phase == MatchPhase.Decision && s.Decision != null
                        ? (s.Decision.Kind == DecisionKind.Discard ? (s.Decision.Attacker == human ? "human" : "ai") : "human")
                        : s.Turn == human ? "human" : "ai";
                    if (role == "ai") { engine.AiStep(s); continue; }
                    if (s.Phase == MatchPhase.Decision) { engine.DecideHuman(s, rng.Next(3), rng.NextDouble(), false); continue; }
                    var me = s.Team(human);
                    if (!me.HandPlayedThisTurn && me.Hand.Count > 0 && rng.Next(4) == 0)
                    {
                        var card = me.Hand[rng.Next(me.Hand.Count)];
                        var avail = Enumerable.Range(0, s.Table.Count).Where(k => s.Table[k].Available).ToList();
                        try { engine.PlayHandHuman(s, card, avail[rng.Next(avail.Count)], avail[rng.Next(avail.Count)]); } catch (GameRuleException) { }
                        continue;
                    }
                    var cells = Enumerable.Range(0, s.Table.Count).Where(k => s.Table[k].Available).ToList();
                    try { engine.FlipHuman(s, cells[rng.Next(cells.Count)]); } catch (GameRuleException) { }
                }
                if (s.Phase != MatchPhase.Finished) { stuck++; output.WriteLine($"stuck {i}: phase {s.Phase} turn {s.Turn} left {s.Table.Count(c => c.Available)}"); }
            }
            catch (Exception e) { errors++; if (errors < 5) output.WriteLine($"match {i}: {e.GetType().Name} {e.Message}\n{e.StackTrace}"); }
        }
        output.WriteLine($"errors {errors}, stuck {stuck}");
        Assert.Equal(0, errors);
        Assert.Equal(0, stuck);
    }
}
