using FootballCards.Api.Data.Entities;

namespace FootballCards.Api.Game;

public record MatchSetup(Team Home, Team Away, Side? HumanSide, bool IsKnockout, string CompetitionName);

public class GameRuleException(string message) : Exception(message);

/// <summary>
/// Flip-card match engine. Face-down cards lie on the table; the player whose turn it is flips one.
/// The card decides what happens and who flips next (see docs/rules.md). Pure luck — team strength does not matter.
/// </summary>
public class MatchEngine(Random rng)
{
    public MatchState Create(MatchSetup setup, IReadOnlyList<CardDefinition> cardDefs)
    {
        var deck = new List<string>();
        foreach (var d in cardDefs)
            for (var i = 0; i < d.CopiesInDeck; i++) deck.Add(d.Code);
        if (!deck.Contains(Cards.FullTime)) deck.Add(Cards.FullTime); // the match must be able to end
        Shuffle(deck);

        var s = new MatchState
        {
            Home = CreateTeam(setup.Home, setup.HumanSide == Side.Home),
            Away = CreateTeam(setup.Away, setup.HumanSide == Side.Away),
            CompetitionName = setup.CompetitionName,
            Stadium = setup.Home.Stadium,
            IsKnockout = setup.IsKnockout,
            Turn = rng.Next(2) == 0 ? Side.Home : Side.Away,
        };
        for (var i = 0; i < Rules.TableSize && deck.Count > 0; i++)
        {
            s.Table.Add(deck[^1]);
            deck.RemoveAt(deck.Count - 1);
        }
        s.Deck = deck;
        AddEvent(s, "whistle", s.Turn, Text(s, "kickoff", s.Turn));
        return s;
    }

    private static TeamMatchState CreateTeam(Team t, bool human) => new()
    {
        TeamId = t.Id, Name = t.Name, ShortName = t.ShortName,
        PrimaryColor = t.PrimaryColor, SecondaryColor = t.SecondaryColor, IsHuman = human
    };

    // ---------------------------------------------------------------- public API

    /// <summary>The human flips the card at the given table position.</summary>
    public void FlipHuman(MatchState s, int cell)
    {
        var human = s.HumanSide ?? throw new GameRuleException("There is no human player in this match.");
        if (s.Phase != MatchPhase.Play) throw new GameRuleException("You cannot flip a card right now.");
        if (s.Turn != human) throw new GameRuleException("It is not your turn.");
        if (cell < 0 || cell >= s.Table.Count || s.Table[cell] == null) throw new GameRuleException("There is no card there.");
        Flip(s, cell);
    }

    /// <summary>The computer flips one card (only when it is the computer's turn).</summary>
    public void AiFlip(MatchState s)
    {
        if (s.Phase != MatchPhase.Play) throw new GameRuleException("You cannot flip a card right now.");
        if (s.Team(s.Turn).IsHuman) throw new GameRuleException("It is your turn.");
        Flip(s, RandomCell(s));
    }

    /// <summary>The human picks a side during a penalty or a shootout (as shooter or as keeper).</summary>
    public void PenaltyHuman(MatchState s, int direction)
    {
        var human = s.HumanSide ?? throw new GameRuleException("There is no human player in this match.");
        if (direction is < 0 or > 2) throw new GameRuleException("Pick left, center or right.");
        var shooter = s.Phase switch
        {
            MatchPhase.Penalty => s.PenaltyShooter!.Value,
            MatchPhase.Shootout => s.NextShootoutShooter,
            _ => throw new GameRuleException("There is no penalty to take.")
        };
        var ai = rng.Next(3);
        var humanShoots = shooter == human;
        TakePenalty(s, shooter, humanShoots ? direction : ai, humanShoots ? ai : direction);
    }

    /// <summary>The computer plays for both teams until the end of the match.</summary>
    public void SimulateToEnd(MatchState s)
    {
        var guard = 0;
        while (s.Phase != MatchPhase.Finished && guard++ < 2000)
        {
            switch (s.Phase)
            {
                case MatchPhase.Play:
                    Flip(s, RandomCell(s));
                    break;
                case MatchPhase.Penalty:
                    TakePenalty(s, s.PenaltyShooter!.Value, rng.Next(3), rng.Next(3));
                    break;
                case MatchPhase.Shootout:
                    TakePenalty(s, s.NextShootoutShooter, rng.Next(3), rng.Next(3));
                    break;
            }
        }
    }

    // ---------------------------------------------------------------- core

    private void Flip(MatchState s, int cell)
    {
        var side = s.Turn;
        var team = s.Team(side);
        var card = s.Table[cell]!;
        s.Table[cell] = null;
        if (s.Deck.Count > 0)
        {
            s.Table[cell] = s.Deck[^1];
            s.Deck.RemoveAt(s.Deck.Count - 1);
        }
        s.LastFlip = new Flip { Cell = cell, Card = card, Side = side };
        team.Stats.Flips++;

        if (card is not (Cards.HalfTime or Cards.FullTime))
            s.Minute = Math.Min(s.Half == 1 ? 45 : 90, s.Minute + rng.Next(Rules.MinMinutesPerFlip, Rules.MaxMinutesPerFlip + 1));

        switch (card)
        {
            case Cards.Pass: team.Stats.Passes++; break;
            case Cards.Corner: team.Stats.Corners++; break;
            case Cards.FreeKick: team.Stats.FreeKicks++; break;
            case Cards.Tackle: s.Team(side.Other()).Stats.Tackles++; break;
            case Cards.Offside: team.Stats.Offsides++; break;
            case Cards.Save: s.Team(side.Other()).Stats.Saves++; break;
            case Cards.Miss: team.Stats.Misses++; break;
            case Cards.YellowCard: team.Stats.YellowCards++; break;
            case Cards.RedCard: team.Stats.RedCards++; break;
        }

        switch (card)
        {
            case Cards.Goal:
                team.Goals++;
                AddEvent(s, "goal", side, Text(s, card, side), card);
                s.Turn = side.Other();
                break;

            case Cards.Penalty:
                team.Stats.Penalties++;
                AddEvent(s, "penalty", side, Text(s, card, side), card);
                s.Phase = MatchPhase.Penalty;
                s.PenaltyShooter = side;
                break;

            case Cards.HalfTime when s.Half == 1:
                AddEvent(s, "whistle", side, Text(s, card, side), card);
                s.Half = 2;
                s.Minute = 45;
                s.Turn = side.Other();
                AddEvent(s, "whistle", s.Turn, Text(s, "kickoff2", s.Turn));
                break;

            case Cards.HalfTime: // a second Half Time card (only if the deck was edited) — just change turns
                AddEvent(s, "info", side, $"{team.Name} flip Half Time again — play on.", card);
                s.Turn = side.Other();
                break;

            case Cards.FullTime:
                EndOfMatch(s, side);
                break;

            default:
                AddEvent(s, card is Cards.YellowCard ? "yellow" : card is Cards.RedCard ? "red" : "play",
                    side, Text(s, card, side), card);
                if (!Cards.KeepsTurn(card)) s.Turn = side.Other();
                break;
        }
    }

    private void EndOfMatch(MatchState s, Side side)
    {
        var early = s.Half == 1 && s.Minute < 45;
        AddEvent(s, "whistle", side, Commentary.Get(early ? "full_time_early" : Cards.FullTime, rng,
            s.Team(side).Name, s.Team(side.Other()).Name, Score(s), s.MinuteLabel), Cards.FullTime);

        if (s.IsKnockout && s.Home.Goals == s.Away.Goals)
        {
            s.Phase = MatchPhase.Shootout;
            AddEvent(s, "penalty", null, "It's level — the winner will be decided by a penalty shootout!");
        }
        else s.Phase = MatchPhase.Finished;
    }

    private void TakePenalty(MatchState s, Side shooter, int shotDir, int keeperDir)
    {
        bool scored;
        string outcome;
        if (shotDir != keeperDir)
        {
            scored = rng.Next(100) < Rules.PenaltyGoalChance;
            outcome = scored ? "penalty_goal" : "penalty_miss";
        }
        else
        {
            scored = rng.Next(100) >= (shotDir == 1 ? Rules.PenaltySaveCenter : Rules.PenaltySaveSameSide);
            outcome = scored ? "penalty_goal" : "penalty_saved";
        }
        var how = $"The taker goes {Rules.Directions[shotDir]}, the keeper dives {Rules.Directions[keeperDir]}.";
        if (keeperDir == 1) how = $"The taker goes {Rules.Directions[shotDir]}, the keeper stays in the middle.";

        if (s.Phase == MatchPhase.Penalty)
        {
            AddEvent(s, "info", shooter, how);
            if (scored) s.Team(shooter).Goals++;
            AddEvent(s, scored ? "goal" : "miss", shooter, Text(s, outcome, shooter));
            s.Phase = MatchPhase.Play;
            s.PenaltyShooter = null;
            s.Turn = shooter.Other();
            return;
        }

        // shootout
        s.Shootout.Add(new ShootoutKick { Side = shooter, Scored = scored });
        var (h, a) = ShootoutScore(s);
        AddEvent(s, scored ? "goal" : "miss", shooter,
            $"{how} {(scored ? "Scored" : "Not scored")} — {s.Team(shooter).Name}. Shootout: {h}:{a}");
        if (ShootoutDecided(s))
        {
            s.HomePenalties = h;
            s.AwayPenalties = a;
            s.Phase = MatchPhase.Finished;
            var winner = h > a ? s.Home : s.Away;
            AddEvent(s, "whistle", h > a ? Side.Home : Side.Away, $"{winner.Name} win the shootout {h}:{a}!");
        }
    }

    private static (int Home, int Away) ShootoutScore(MatchState s) =>
        (s.Shootout.Count(k => k.Side == Side.Home && k.Scored), s.Shootout.Count(k => k.Side == Side.Away && k.Scored));

    private static bool ShootoutDecided(MatchState s)
    {
        var (h, a) = ShootoutScore(s);
        var hk = s.Shootout.Count(k => k.Side == Side.Home);
        var ak = s.Shootout.Count(k => k.Side == Side.Away);
        var n = Rules.ShootoutKicks;
        if (hk <= n && ak <= n)
            return h > a + (n - ak) || a > h + (n - hk);
        return hk == ak && h != a; // sudden death
    }

    // ---------------------------------------------------------------- helpers

    private int RandomCell(MatchState s)
    {
        var cells = Enumerable.Range(0, s.Table.Count).Where(i => s.Table[i] != null).ToList();
        if (cells.Count == 0) throw new InvalidOperationException("No cards left on the table.");
        return cells[rng.Next(cells.Count)];
    }

    private void Shuffle(List<string> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static string Score(MatchState s) => $"{s.Home.Name} {s.Home.Goals}:{s.Away.Goals} {s.Away.Name}";

    private string Text(MatchState s, string key, Side side) =>
        Commentary.Get(key, rng, s.Team(side).Name, s.Team(side.Other()).Name, Score(s), s.MinuteLabel);

    private static void AddEvent(MatchState s, string type, Side? side, string text, string? card = null) =>
        s.Events.Add(new MatchEvent
        {
            Id = s.NextEventId++, Minute = s.Minute, MinuteLabel = s.MinuteLabel, Type = type,
            Side = side, Text = text, Card = card
        });
}
