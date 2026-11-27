using DugoutDeck.Api.Data.Entities;

namespace DugoutDeck.Api.Game;

/// <param name="UseTiers">career matches: the computer's cards have tiers by rating (friendlies: all Normal)</param>
public record MatchSetup(Team Home, Team Away, int HomeRating, int AwayRating, Side? HumanSide, bool IsKnockout, string CompetitionName,
    Difficulty Difficulty = Difficulty.Normal, bool UseTiers = false);

public class GameRuleException(string message) : Exception(message);

/// <summary>
/// The Dugout Deck match engine. Each half is a 5×5 grid: neutral cards, 4 cards of each team and Half Time /
/// Full Time — all with the same grey back (only the owner knows where its team cards are). The player on turn
/// flips one card; the card decides what happens. Whoever flips a team card gets its effect (stealing).
/// See docs/rules.md for all rules.
/// </summary>
public class MatchEngine(Random rng, CardCatalog catalog)
{
    private readonly MatchAi _ai = new(rng);

    // ======================================================================= setup

    /// <summary>Creates a match in the Setup phase: teams and conditions are known, tactics are not chosen yet.</summary>
    public MatchState Create(MatchSetup setup) => new()
    {
        Home = CreateTeam(setup.Home, setup.HomeRating, setup.HumanSide == Side.Home, setup.HumanSide == Side.Away ? setup.Difficulty : Difficulty.Normal),
        Away = CreateTeam(setup.Away, setup.AwayRating, setup.HumanSide == Side.Away, setup.HumanSide == Side.Home ? setup.Difficulty : Difficulty.Normal),
        Difficulty = setup.HumanSide == null ? Difficulty.Normal : setup.Difficulty,
        CompetitionName = setup.CompetitionName,
        Stadium = setup.Home.Stadium,
        IsKnockout = setup.IsKnockout,
        UseTiers = setup.UseTiers,
        Conditions = TacticRules.RollConditions(setup.Home.City, setup.Away.City, rng),
        Phase = MatchPhase.Setup
    };

    /// <param name="vsHuman">difficulty of the human opponent (shifts this computer team's level)</param>
    private static TeamMatchState CreateTeam(Team t, int rating, bool human, Difficulty vsHuman) => new()
    {
        TeamId = t.Id, Name = t.Name, ShortName = t.ShortName, City = t.City,
        PrimaryColor = t.PrimaryColor, SecondaryColor = t.SecondaryColor,
        IsHuman = human, Rating = rating, AiLevel = Math.Clamp(Rules.AiLevel(rating) + Rules.DifficultyAiShift(vsHuman), 0, 2)
    };

    /// <summary>Applies both tactics (the computer builds its own if null), deals both grids and kicks off.</summary>
    public void Start(MatchState s, TacticSpec? homeTactic, TacticSpec? awayTactic)
    {
        if (s.Phase != MatchPhase.Setup) throw new GameRuleException("The match has already started.");
        var aiBudget = Rules.DifficultyAiShift(s.Difficulty);
        s.Home.Tactic = homeTactic ?? TacticRules.BuildAi(catalog, s.Home.Rating, s.Away.Rating, true, rng, s.HumanSide == Side.Away ? aiBudget : 0, s.UseTiers);
        s.Away.Tactic = awayTactic ?? TacticRules.BuildAi(catalog, s.Away.Rating, s.Home.Rating, false, rng, s.HumanSide == Side.Home ? aiBudget : 0, s.UseTiers);
        foreach (var t in new[] { s.Home, s.Away })
        {
            t.Hand = new List<string>(t.Tactic.Hand);
            t.HandTiers = t.Tactic.Hand.Select((_, i) => Tiers.At(t.Tactic.HandTiers, i)).ToList();
        }
        s.Composition = TacticRules.Composition(catalog, s.Conditions, s.Home.Tactic, s.Away.Tactic);
        s.Table = Deal(s, Cards.HalfTime);
        s.SecondHalfTable = Deal(s, Cards.FullTime);
        s.FirstKickOff = rng.Next(2) == 0 ? Side.Home : Side.Away;
        s.Turn = s.FirstKickOff;
        s.Phase = MatchPhase.Play;
        AddEvent(s, "whistle", s.Turn, Text(s, "kickoff", s.Turn));
    }

    private List<TableCell> Deal(MatchState s, string specialCard)
    {
        var cells = new List<TableCell>();
        foreach (var (code, count) in s.Composition)
            for (var i = 0; i < count; i++) cells.Add(new TableCell { Card = code });
        cells.AddRange(s.Home.Tactic.Field.Select((c, i) => new TableCell { Card = c, Owner = Side.Home, Tier = Tiers.At(s.Home.Tactic.FieldTiers, i) }));
        cells.AddRange(s.Away.Tactic.Field.Select((c, i) => new TableCell { Card = c, Owner = Side.Away, Tier = Tiers.At(s.Away.Tactic.FieldTiers, i) }));
        cells.Add(new TableCell { Card = specialCard });
        for (var i = cells.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (cells[i], cells[j]) = (cells[j], cells[i]);
        }
        return cells;
    }

    // ======================================================================= public actions

    public void FlipHuman(MatchState s, int cell)
    {
        var human = RequireHuman(s);
        if (s.Phase != MatchPhase.Play) throw new GameRuleException("You cannot flip a card right now.");
        if (s.Turn != human) throw new GameRuleException("It is not your turn.");
        if (cell < 0 || cell >= s.Table.Count || !s.Table[cell].Available) throw new GameRuleException("That card cannot be flipped.");
        if (!CanFlipOwn(s, human, cell)) throw new GameRuleException("You have already played one of your own cards this turn.");
        Flip(s, cell);
    }

    /// <summary>The human plays a hand card (or a free Scout from a Playmaker) before flipping.</summary>
    public void PlayHandHuman(MatchState s, string card, int? cell, int? cell2)
    {
        var human = RequireHuman(s);
        if (s.Phase != MatchPhase.Play || s.Turn != human) throw new GameRuleException("You can only play a hand card on your turn.");
        PlayHand(s, human, card, cell, cell2);
    }

    /// <summary>The human answers the pending mini-game (choice 0–2, or — after the computer's red card — the index of
    /// the computer's hand card the human takes away).</summary>
    public void DecideHuman(MatchState s, int? choice, double? timing, bool auto)
    {
        var human = RequireHuman(s);
        if (s.Phase != MatchPhase.Decision || s.Decision == null) throw new GameRuleException("There is no decision to make.");
        var d = s.Decision;
        var humanAttacks = d.Attacker == human;
        if (d.Kind == DecisionKind.Discard)
        {
            // the team that was sent off is d.Attacker; its opponent chooses which hand card it loses
            if (humanAttacks) throw new GameRuleException("The opponent is choosing which of your cards to take.");
            var pick = auto || choice == null ? _ai.RemoveChoice(s.Team(d.Attacker), catalog) : choice.Value;
            Resolve(s, pick, 0, 0.5, 0.5);
            return;
        }

        var c = auto || choice == null ? AutoChoice(s, human) : choice.Value;
        var t = auto || timing == null ? AutoTiming() : Math.Clamp(timing.Value, 0, 1);
        var aiSide = human.Other();
        var aiChoice = _ai.MiniChoice();
        var aiTiming = _ai.Timing(s.Team(aiSide).AiLevel);

        if (humanAttacks) Resolve(s, c, aiChoice, t, aiTiming);
        else Resolve(s, aiChoice, c, aiTiming, t);
    }

    /// <summary>One computer step: on the computer's turn it may play a hand card and then flips a card;
    /// a pending decision that only the computer must make is resolved.</summary>
    public void AiStep(MatchState s)
    {
        if (s.Phase == MatchPhase.Decision && s.Decision is { Kind: DecisionKind.Discard } dd && !s.Team(dd.Attacker.Other()).IsHuman)
        {
            Resolve(s, _ai.RemoveChoice(s.Team(dd.Attacker), catalog), 0, 0.5, 0.5);
            return;
        }
        if (s.Phase != MatchPhase.Play) throw new GameRuleException("The computer has nothing to do.");
        if (s.Team(s.Turn).IsHuman) throw new GameRuleException("It is your turn.");
        AiTurn(s, s.Turn);
    }

    /// <summary>The computer plays both sides until the end of the match.</summary>
    public void SimulateToEnd(MatchState s)
    {
        if (s.Phase == MatchPhase.Setup) Start(s, null, null);
        var guard = 0;
        while (s.Phase != MatchPhase.Finished && guard++ < 3000)
        {
            if (s.Phase == MatchPhase.Play) AiTurn(s, s.Turn);
            else if (s.Phase == MatchPhase.Decision && s.Decision != null)
            {
                var d = s.Decision;
                var a = s.Team(d.Attacker);
                var def = s.Team(d.Attacker.Other());
                if (d.Kind == DecisionKind.Discard) Resolve(s, _ai.RemoveChoice(a, catalog), 0, 0.5, 0.5);
                else Resolve(s, _ai.MiniChoice(), _ai.MiniChoice(), _ai.Timing(a.AiLevel), _ai.Timing(def.AiLevel));
            }
        }
    }

    private void AiTurn(MatchState s, Side side)
    {
        var (card, cell, cell2) = _ai.ChooseHandCard(s, side);
        if (card != null) PlayHand(s, side, card, cell, cell2);
        if (s.Phase != MatchPhase.Play || s.Turn != side) return;
        Flip(s, _ai.ChooseCell(s, side));
    }

    /// <summary>One own team card per turn — unless only own cards are left face down (or a Counter Attack is running).</summary>
    public static bool CanFlipOwn(MatchState s, Side side, int cell)
    {
        var team = s.Team(side);
        if (s.Table[cell].Owner != side || !team.OwnCardUsedThisTurn || team.CounterFlips > 0) return true;
        return s.Table.All(c => !c.Available || c.Owner == side);
    }

    /// <summary>Gives the turn to a side; a new turn clears the "one own card per turn" marks.</summary>
    private static void SetTurn(MatchState s, Side side)
    {
        if (s.Turn != side)
        {
            s.Home.OwnCardUsedThisTurn = false;
            s.Away.OwnCardUsedThisTurn = false;
        }
        s.Turn = side;
    }

    private static Side RequireHuman(MatchState s) =>
        s.HumanSide ?? throw new GameRuleException("There is no human player in this match.");

    // ======================================================================= hand cards

    private void PlayHand(MatchState s, Side side, string card, int? cell, int? cell2)
    {
        var team = s.Team(side);
        var opp = s.Team(side.Other());
        var freeScout = card == Cards.Scout && team.FreeScouts > 0;
        var handIndex = team.Hand.IndexOf(card);
        if (!freeScout)
        {
            if (handIndex < 0) throw new GameRuleException("You don't have that card in your hand.");
            if (team.HandPlayedThisTurn) throw new GameRuleException("Only one hand card per flip.");
        }
        var tier = freeScout ? Tiers.Normal : Tiers.At(team.HandTiers, handIndex);
        var extra = Tiers.ExtraUses(tier);
        var tierText = tier > Tiers.Normal ? $" ({Tiers.Name(tier)})" : "";
        if (Tiers.StreakBonus(card, tier)) team.PassStreak = Math.Min(team.PassStreak + 1, Rules.PassesForBigChance - 1);

        TableCell Target(int? i, string what)
        {
            if (i is not { } idx || idx < 0 || idx >= s.Table.Count) throw new GameRuleException($"Pick a card to {what}.");
            return s.Table[idx];
        }

        switch (card)
        {
            case Cards.Scout:
            {
                var c = Target(cell, "look under");
                if (!c.Available) throw new GameRuleException("Pick a face-down card.");
                if (!team.Peeks.Contains(cell!.Value)) team.Peeks.Add(cell.Value);
                team.FreeScouts += extra; // Ultra Rare / Legendary: free Scouts on top
                AddEvent(s, "hand", side, $"{team.Name} scout a card{tierText}.", card);
                break;
            }
            case Cards.Swap:
            {
                var a = Target(cell, "swap");
                var b = Target(cell2, "swap");
                if (!a.Available || !b.Available || cell == cell2) throw new GameRuleException("Pick two different face-down cards.");
                s.Table[cell!.Value] = b;
                s.Table[cell2!.Value] = a;
                foreach (var t in new[] { s.Home, s.Away })
                    t.Peeks = t.Peeks.Select(p => p == cell ? cell2.Value : p == cell2 ? cell.Value : p).ToList();
                AddEvent(s, "hand", side, $"{team.Name} swap two cards.", card);
                break;
            }
            case Cards.Burn:
            {
                // blind: a random face-down card of the opponent — the player does not choose and does not see which
                var targets = Enumerable.Range(0, s.Table.Count).Where(i => s.Table[i].Available && s.Table[i].Owner == side.Other()).ToList();
                if (targets.Count == 0) throw new GameRuleException("The opponent has no face-down cards left to burn.");
                var burns = tier == Tiers.Legendary ? 2 : 1;
                for (var b = 0; b < burns && targets.Count > 0; b++)
                {
                    var idx = targets[rng.Next(targets.Count)];
                    targets.Remove(idx);
                    s.Table[idx].Burned = true;
                    foreach (var t in new[] { s.Home, s.Away }) t.Peeks.Remove(idx);
                    team.Stats.CardsBurned++;
                }
                AddEvent(s, "hand", side, burns > 1 ? $"{team.Name} burn two of {opp.Name}'s cards!{tierText}" : $"{team.Name} burn one of {opp.Name}'s cards!{tierText}", card);
                break;
            }
            case Cards.Calm:
                team.CalmActive = true;
                team.CalmExtra = extra;
                AddEvent(s, "hand", side, $"{team.Name} play Calm{tierText}.", card);
                break;
            case Cards.Press:
                team.PressActive = true;
                team.PressExtra = extra;
                AddEvent(s, "hand", side, $"{team.Name} play Press{tierText}.", card);
                break;
            case Cards.Var:
                team.VarActive = true;
                team.VarBonus = Tiers.ChanceBonus(tier);
                AddEvent(s, "hand", side, $"{team.Name} ask for VAR{tierText} — the opponent's next goal will be checked.", card);
                break;
            case Cards.PenaltyTaker:
                team.PenaltyTakerActive = true;
                team.PenaltyTakerBonus = Tiers.ChanceBonus(tier);
                AddEvent(s, "hand", side, $"{team.Name}'s penalty taker is ready — their next penalty is more likely to go in.", card);
                break;
            case Cards.Marking:
            {
                var c = Target(cell, "mark");
                if (!c.Available || c.Owner != side) throw new GameRuleException("Pick one of your own face-down cards.");
                team.MarkedCell = cell!.Value;
                AddEvent(s, "hand", side, $"{team.Name} put a man-marker on one of their cards.", card);
                break;
            }
            case Cards.Substitution:
            {
                var c = Target(cell, "bring back");
                if (!c.FaceUp || c.Owner != side) throw new GameRuleException("Pick one of your own team cards that is already face up.");
                var free = Enumerable.Range(0, s.Table.Count).Where(i => s.Table[i].Available).ToList();
                if (free.Count == 0) throw new GameRuleException("There is no face-down card left to swap with.");
                var j = free[rng.Next(free.Count)];
                var i0 = cell!.Value;
                var hidden = s.Table[j];
                c.FlippedBy = null;
                s.Table[j] = c;
                s.Table[i0] = hidden;
                foreach (var t in new[] { s.Home, s.Away })
                {
                    t.Peeks = t.Peeks.Select(p => p == j ? i0 : p).ToList();
                    if (t.MarkedCell == j) t.MarkedCell = i0;
                }
                if (s.LastFlip?.Cell == i0) s.LastFlip.Cell = -1;
                AddEvent(s, "hand", side, $"Substitution! {team.Name} bring their {Cards.Name(c.Card)} back into the grid.", card);
                break;
            }
            case Cards.Analysis:
            {
                Target(cell, "analyse");
                var row = cell!.Value / Rules.GridSize;
                var n = Enumerable.Range(row * Rules.GridSize, Rules.GridSize).Count(i => s.Table[i].Available && s.Table[i].Owner == side.Other());
                AddEvent(s, "hand", side, $"Analysis: {opp.Name} have {n} card{(n == 1 ? "" : "s")} hidden in row {row + 1}.", card);
                break;
            }
            default:
                throw new GameRuleException("Unknown hand card.");
        }

        team.Played.Add(new PlayedCard { Code = card, Half = s.Half, How = "hand", Tier = tier });
        if (freeScout) team.FreeScouts--;
        else
        {
            team.Hand.RemoveAt(handIndex);
            if (handIndex < team.HandTiers.Count) team.HandTiers.RemoveAt(handIndex);
            // Ultra Rare / Legendary Swap, Analysis, Substitution, Marking and Burn are quick: another hand card may follow
            var quick = Tiers.QuickPlay(tier) && card is Cards.Swap or Cards.Analysis or Cards.Substitution or Cards.Marking or Cards.Burn;
            if (!quick) team.HandPlayedThisTurn = true;
        }
    }

    // ======================================================================= flipping

    private void Flip(MatchState s, int index)
    {
        var side = s.Turn;
        var team = s.Team(side);
        var cell = s.Table[index];
        cell.FlippedBy = side;
        s.LastFlip = new Flip { Cell = index, Card = cell.Card, Side = side, Owner = cell.Owner, Tier = cell.Owner != null ? cell.Tier : 0 };
        team.Stats.Flips++;
        team.HandPlayedThisTurn = false;
        var calm = team.CalmActive;
        if (team.CalmExtra > 0) team.CalmExtra--; // Ultra Rare / Legendary Calm lasts more cards
        else team.CalmActive = false;
        var counter = team.CounterFlips > 0;
        if (counter) team.CounterFlips--;

        // --- a card of the opponent: stolen — the effect is for the one who flipped it (the Offside Trap springs)
        if (cell.Owner == side.Other())
        {
            if (cell.Card == Cards.OffsideTrap)
            {
                AddEvent(s, "lost", side, Text(s, "trap", side), cell.Card);
                s.Team(side.Other()).Played.Add(new PlayedCard { Code = cell.Card, Half = s.Half, How = "trap", Tier = cell.Tier });
                s.Team(side.Other()).FreeScouts += Tiers.ExtraUses(cell.Tier);
                LoseBall(s, side);
                return;
            }
            var owner = s.Team(side.Other());
            if (owner.MarkedCell == index)
            {
                owner.MarkedCell = -1;
                AddEvent(s, "lost", side, $"Man-marked! {owner.Name}'s {Cards.Name(cell.Card)} was marked — {team.Name} cannot take it and lose the ball.", Cards.Marking);
                LoseBall(s, side);
                return;
            }
            if (cell.Card == Cards.Decoy)
            {
                team.PassStreak = 0;
                owner.Played.Add(new PlayedCard { Code = cell.Card, Half = s.Half, How = "decoy", Tier = cell.Tier });
                owner.FreeScouts += Tiers.ExtraUses(cell.Tier);
                AddEvent(s, "play", side, $"A decoy run by {owner.Name} — {team.Name} gain nothing and their passing move breaks down.", cell.Card);
                return;
            }
            team.Stats.CardsStolen++;
            team.Played.Add(new PlayedCard { Code = cell.Card, Half = s.Half, How = "stolen", Tier = cell.Tier });
            AddEvent(s, "steal", side, $"{team.Name} steal {s.Team(side.Other()).Name}'s {(cell.Tier > 0 ? Tiers.Name(cell.Tier) + " " : "")}{Cards.Name(cell.Card)}!", cell.Card);
            TeamCard(s, side, cell.Card, cell.Tier);
            return;
        }

        // --- an own team card (one per turn; free during a Counter Attack)
        if (cell.Owner == side)
        {
            if (!counter) team.OwnCardUsedThisTurn = true;
            if (team.MarkedCell == index) team.MarkedCell = -1;
            team.Played.Add(new PlayedCard { Code = cell.Card, Half = s.Half, How = "own", Tier = cell.Tier });
            TeamCard(s, side, cell.Card, cell.Tier);
            return;
        }

        // --- neutral card
        switch (cell.Card)
        {
            case Cards.Pass: Pass(s, side, Cards.Pass); break;
            case Cards.Corner:
                team.Stats.Corners++;
                AddEvent(s, "chance", side, Text(s, Cards.Corner, side), Cards.Corner);
                StartDecision(s, DecisionKind.Corner, side, "Corner");
                break;
            case Cards.FreeKick:
                team.Stats.FreeKicks++;
                AddEvent(s, "chance", side, Text(s, Cards.FreeKick, side), Cards.FreeKick);
                StartDecision(s, DecisionKind.FreeKick, side, "Free kick");
                break;
            case Cards.Goal: Goal(s, side, "Goal card", Cards.Goal); break;
            case Cards.Penalty:
                team.Stats.Penalties++;
                team.PassStreak = 0;
                AddEvent(s, "penalty", side, Text(s, Cards.Penalty, side), Cards.Penalty);
                StartDecision(s, DecisionKind.Penalty, side, "Penalty");
                break;
            case Cards.HalfTime: HalfTime(s, side); break;
            case Cards.FullTime: FullTime(s, side); break;
            default:
                Dangerous(s, side, cell.Card, calm, counter);
                break;
        }
    }

    /// <summary>The effect of a team card for the team that flipped it (its owner or a thief).</summary>
    private void TeamCard(MatchState s, Side side, string card, int tier = Tiers.Normal)
    {
        var team = s.Team(side);
        var bonus = Tiers.ChanceBonus(tier);
        var extra = Tiers.ExtraUses(tier);
        // Rare+ cards without a chance: +1 to the pass streak (never straight to a Big Chance)
        if (Tiers.StreakBonus(card, tier)) team.PassStreak = Math.Min(team.PassStreak + 1, Rules.PassesForBigChance - 1);
        switch (card)
        {
            case Cards.Striker:
                AddEvent(s, "chance", side, $"{team.Name}'s striker is clean through on goal!", card);
                Shot(s, side, Rules.StrikerGoal + bonus, "Striker");
                break;
            case Cards.BigChanceCard:
                AddEvent(s, "chance", side, $"{team.Name} play their Big Chance card!", card);
                StartDecision(s, DecisionKind.BigChance, side, "Big Chance card", bonus);
                break;
            case Cards.LongShot:
                if (rng.Next(100) < Rules.LongShotGoal + bonus) Goal(s, side, "Long Shot", card);
                else
                {
                    team.Stats.Misses++;
                    AddEvent(s, "play", side, $"Long shot from {team.Name}... just wide. They keep the ball.", card);
                }
                break;
            case Cards.Playmaker:
                team.FreeScouts += 1 + extra;
                AddEvent(s, "play", side, $"{team.Name}'s playmaker reads the game — a free Scout!", card);
                break;
            case Cards.Captain:
                if (team.CaptainShield) team.CaptainExtra += 1 + extra;
                else { team.CaptainShield = true; team.CaptainExtra = extra; }
                AddEvent(s, "play", side, $"{team.Name}'s captain rallies the team — the next Tackle will be ignored.", card);
                break;
            case Cards.Wall:
                if (team.WallActive) team.WallExtra += 1 + extra;
                else { team.WallActive = true; team.WallExtra = extra; }
                AddEvent(s, "play", side, $"{team.Name} build a wall — the next goal against them this half becomes a save.", card);
                break;
            case Cards.CounterAttack:
                team.CounterFlips = 2 + extra;
                AddEvent(s, "play", side, $"COUNTER ATTACK! {team.Name} flip two more cards — dangerous ones don't count.", card);
                break;
            case Cards.Winger:
                team.Stats.Corners++;
                AddEvent(s, "chance", side, $"{team.Name}'s winger beats his man and wins a corner!", card);
                StartDecision(s, DecisionKind.Corner, side, "Winger", bonus);
                break;
            case Cards.Goalkeeper:
                team.GoalkeeperBonus = team.GoalkeeperActive ? Math.Max(team.GoalkeeperBonus, bonus) : bonus;
                team.GoalkeeperActive = true;
                AddEvent(s, "play", side, $"{team.Name}'s goalkeeper is on fire — the next shot against them is harder to score.", card);
                break;
            case Cards.TargetMan:
                team.TargetManBonus = team.TargetManActive ? Math.Max(team.TargetManBonus, bonus) : bonus;
                team.TargetManActive = true;
                AddEvent(s, "play", side, $"{team.Name}'s target man takes up position — their next cross is more dangerous.", card);
                break;
            case Cards.Sweeper:
                if (team.SweeperActive) team.SweeperExtra += 1 + extra;
                else { team.SweeperActive = true; team.SweeperExtra = extra; }
                AddEvent(s, "play", side, $"{team.Name}'s sweeper reads the game — their next Offside or Save will be ignored.", card);
                break;
            case Cards.SuperSub when s.Half == 2:
                AddEvent(s, "chance", side, $"SUPER SUB! {team.Name}'s fresh legs burst through!", card);
                Shot(s, side, Rules.SuperSubGoal + bonus, "Super Sub");
                break;
            case Cards.TwelfthMan when side == Side.Home:
                if (team.TwelfthManActive) team.TwelfthManExtra += 1 + extra;
                else { team.TwelfthManActive = true; team.TwelfthManExtra = extra; }
                AddEvent(s, "play", side, $"The home crowd roars — {team.Name}'s next dangerous card will be ignored.", card);
                break;
            default: // Offside Trap, Squad Player, Decoy, Super Sub (1st half) and 12th Man (away) work like a Pass
                Pass(s, side, card);
                break;
        }
    }

    private void Pass(MatchState s, Side side, string card)
    {
        var team = s.Team(side);
        team.Stats.Passes++;
        team.PassStreak++;
        AddEvent(s, "play", side, card == Cards.Pass ? Text(s, Cards.Pass, side) : $"{Cards.Name(card)} — {team.Name} keep the ball.", card);
        if (team.PassStreak >= Rules.PassesForBigChance)
        {
            team.PassStreak = 0;
            if (team.Stats.RedCards > 0)
                AddEvent(s, "info", side, $"{team.Name} are down to ten men — no Big Chance from passes.");
            else
            {
                AddEvent(s, "chance", side, Text(s, "big_chance", side));
                StartDecision(s, DecisionKind.BigChance, side, "Three passes");
            }
        }
    }

    private void Dangerous(MatchState s, Side side, string card, bool calm, bool counter)
    {
        var team = s.Team(side);
        if (counter)
        {
            AddEvent(s, "play", side, $"{Cards.Name(card)} — {Text(s, "counter_ignore", side)}", card);
            return;
        }
        if (card == Cards.Tackle && team.CaptainShield)
        {
            if (team.CaptainExtra > 0) team.CaptainExtra--;
            else team.CaptainShield = false;
            AddEvent(s, "play", side, Text(s, "captain", side), card);
            return;
        }
        if (card == Cards.Tackle && calm)
        {
            AddEvent(s, "play", side, Text(s, "calm", side), card);
            return;
        }
        if ((card == Cards.Offside || card == Cards.Save) && team.SweeperActive)
        {
            if (team.SweeperExtra > 0) team.SweeperExtra--;
            else team.SweeperActive = false;
            AddEvent(s, "play", side, $"{Cards.Name(card)} — but {team.Name}'s sweeper saves the move. They keep the ball.", card);
            return;
        }
        if (team.TwelfthManActive)
        {
            if (team.TwelfthManExtra > 0) team.TwelfthManExtra--;
            else team.TwelfthManActive = false;
            AddEvent(s, "play", side, $"{Cards.Name(card)} — the home crowd carries {team.Name} through it. They keep the ball.", card);
            return;
        }

        switch (card)
        {
            case Cards.YellowCard:
                team.Stats.YellowCards++;
                if (team.Stats.YellowCards % 2 == 0)
                {
                    AddEvent(s, "red", side, Text(s, "second_yellow", side), card);
                    SendOff(s, side);
                }
                else AddEvent(s, "yellow", side, Text(s, card, side), card);
                break;
            case Cards.RedCard:
                AddEvent(s, "red", side, Text(s, card, side), card);
                SendOff(s, side);
                break;
            case Cards.Save:
                s.Team(side.Other()).Stats.Saves++;
                AddEvent(s, "save", side, Text(s, card, side), card);
                break;
            case Cards.Miss:
                team.Stats.Misses++;
                AddEvent(s, "miss", side, Text(s, card, side), card);
                break;
            default:
                AddEvent(s, "lost", side, Text(s, card, side), card);
                break;
        }
        team.Stats.BallsLost++;
        if (s.Phase == MatchPhase.Decision) return; // a discard is pending: the ball is lost after it
        LoseBall(s, side);
    }

    private void SendOff(MatchState s, Side side)
    {
        var team = s.Team(side);
        team.Stats.RedCards++;
        if (team.Hand.Count > 0) StartDecision(s, DecisionKind.Discard, side, "Red card");
    }

    private void LoseBall(MatchState s, Side side)
    {
        var team = s.Team(side);
        team.PassStreak = 0;
        if (team.PressActive)
        {
            if (team.PressExtra > 0) team.PressExtra--;
            else team.PressActive = false;
            AddEvent(s, "play", side, Text(s, "press_extra", side), Cards.Press);
            return;
        }
        SetTurn(s, side.Other());
    }

    private void Goal(MatchState s, Side side, string source, string? card)
    {
        var team = s.Team(side);
        var opp = s.Team(side.Other());
        team.PassStreak = 0;
        if (opp.WallActive)
        {
            if (opp.WallExtra > 0) opp.WallExtra--;
            else opp.WallActive = false;
            opp.Stats.Saves++;
            AddEvent(s, "save", side, Text(s, "wall", side), Cards.Wall);
            SetTurn(s, side.Other());
            return;
        }
        if (opp.VarActive)
        {
            opp.VarActive = false;
            var varChance = Rules.VarCancel + opp.VarBonus;
            opp.VarBonus = 0;
            if (rng.Next(100) < varChance)
            {
                team.Stats.GoalsDisallowed++;
                AddEvent(s, "save", side, $"VAR check… the goal is DISALLOWED! {team.Name} are furious.", Cards.Var);
                SetTurn(s, side.Other());
                return;
            }
            AddEvent(s, "info", side, "VAR check… the goal stands!", Cards.Var);
        }
        team.Goals++;
        team.Stats.GoalSources[source] = team.Stats.GoalSources.GetValueOrDefault(source) + 1;
        AddEvent(s, "goal", side, Text(s, Cards.Goal, side) + $" ({source})", card);
        SetTurn(s, side.Other());
    }

    private void HalfTime(MatchState s, Side side)
    {
        AddEvent(s, "whistle", side, Text(s, Cards.HalfTime, side), Cards.HalfTime);
        s.Half = 2;
        s.FirstHalfTable = s.Table;
        s.Table = s.SecondHalfTable;
        s.SecondHalfTable = new();
        s.LastFlip!.Cell = -1;
        foreach (var t in new[] { s.Home, s.Away })
        {
            t.Peeks.Clear();
            t.PassStreak = 0;
            t.WallActive = false;
            t.CaptainShield = false;
            t.CalmActive = false;
            t.PressActive = false;
            t.CounterFlips = 0;
            t.HandPlayedThisTurn = false;
            t.OwnCardUsedThisTurn = false;
            t.GoalkeeperActive = false;
            t.TargetManActive = false;
            t.SweeperActive = false;
            t.TwelfthManActive = false;
            t.MarkedCell = -1; // VAR and the penalty taker stay ready
            t.CaptainExtra = t.WallExtra = t.SweeperExtra = t.TwelfthManExtra = t.CalmExtra = t.PressExtra = 0;
            t.GoalkeeperBonus = t.TargetManBonus = 0;
        }
        s.Turn = s.FirstKickOff.Other();
        AddEvent(s, "whistle", s.Turn, Text(s, "kickoff2", s.Turn));
    }

    private void FullTime(MatchState s, Side side)
    {
        AddEvent(s, "whistle", side, Text(s, Cards.FullTime, side), Cards.FullTime);
        if (s.IsKnockout && s.Home.Goals == s.Away.Goals)
        {
            AddEvent(s, "penalty", null, "It's level — the winner will be decided by a penalty shootout!");
            StartDecision(s, DecisionKind.Shootout, Side.Home, "Shootout");
        }
        else
        {
            s.Phase = MatchPhase.Finished;
            s.Decision = null;
        }
    }

    // ======================================================================= mini games

    private static void StartDecision(MatchState s, DecisionKind kind, Side attacker, string source, int bonus = 0)
    {
        s.Phase = MatchPhase.Decision;
        s.Decision = new PendingDecision { Kind = kind, Attacker = attacker, Source = source, Bonus = bonus };
    }

    public static (string[] Attacker, string[] Defender) Options(DecisionKind kind) => kind switch
    {
        DecisionKind.Corner => (["Near post", "Far post", "Short corner"], ["Guard near post", "Guard far post", "Press the short corner"]),
        DecisionKind.FreeKick => (["Shoot", "Cross", "Short pass"], ["Build a wall", "Mark the box", "Press"]),
        DecisionKind.BigChance => (["Shoot left", "Shoot centre", "Shoot right"], ["Dive left", "Stay centre", "Dive right"]),
        DecisionKind.Penalty or DecisionKind.Shootout => (["Left", "Centre", "Right"], ["Dive left", "Stay centre", "Dive right"]),
        _ => ([], [])
    };

    public static bool NeedsTiming(DecisionKind kind) => kind is DecisionKind.Penalty or DecisionKind.Shootout;

    private int AutoChoice(MatchState s, Side human) =>
        s.Decision!.Kind == DecisionKind.Discard ? _ai.RemoveChoice(s.Team(s.Decision.Attacker), catalog) : rng.Next(3);

    /// <summary>Automatic timing for the human: around the middle, but mostly random.</summary>
    private double AutoTiming() => 0.1 + rng.NextDouble() * 0.8;

    private void Resolve(MatchState s, int attackChoice, int defenseChoice, double attackTiming, double defenseTiming)
    {
        var d = s.Decision!;
        var a = d.Attacker;
        var team = s.Team(a);
        var opp = s.Team(a.Other());
        s.Phase = MatchPhase.Play;
        s.Decision = null;

        string Pick(bool attacker, int i)
        {
            var (ao, dopts) = Options(d.Kind);
            var arr = attacker ? ao : dopts;
            return arr.Length > 0 ? arr[Math.Clamp(i, 0, arr.Length - 1)] : "";
        }

        switch (d.Kind)
        {
            case DecisionKind.Discard:
            {
                if (team.Hand.Count > 0)
                {
                    var idx = Math.Clamp(attackChoice, 0, team.Hand.Count - 1);
                    var lost = team.Hand[idx];
                    team.Hand.RemoveAt(idx);
                    if (idx < team.HandTiers.Count) team.HandTiers.RemoveAt(idx);
                    AddEvent(s, "info", a, $"{opp.Name} take away {team.Name}'s {Cards.Name(lost)} hand card.", lost);
                }
                LoseBall(s, a);
                break;
            }
            case DecisionKind.Corner:
            {
                var winger = d.Source == "Winger"; // the winger's team keeps the ball if the corner does not end in a goal
                AddEvent(s, "info", a, $"{team.Name}: {Pick(true, attackChoice)} — {opp.Name}: {Pick(false, defenseChoice)}.");
                if (attackChoice == 2)
                {
                    if (defenseChoice == 2 && !winger) { AddEvent(s, "lost", a, $"{opp.Name} read the short corner and win the ball."); LoseBall(s, a); }
                    else AddEvent(s, "play", a, $"Short corner — {team.Name} keep the ball.");
                }
                else if (attackChoice == defenseChoice)
                {
                    if (winger) AddEvent(s, "play", a, $"{opp.Name} clear the corner — but the winger wins it back.");
                    else { AddEvent(s, "lost", a, $"{opp.Name} clear the corner."); LoseBall(s, a); }
                }
                else Shot(s, a, Rules.CornerGoal + TargetMan(team) + d.Bonus, "Corner", keepBallOnMiss: winger);
                break;
            }
            case DecisionKind.FreeKick:
            {
                AddEvent(s, "info", a, $"{team.Name}: {Pick(true, attackChoice)} — {opp.Name}: {Pick(false, defenseChoice)}.");
                switch (attackChoice)
                {
                    case 0: Shot(s, a, defenseChoice == 0 ? Rules.FreeKickDirectIntoWallGoal : Rules.FreeKickDirectGoal, "Free kick"); break;
                    case 1:
                        if (defenseChoice == 1) { AddEvent(s, "lost", a, $"{opp.Name} head the cross clear."); LoseBall(s, a); }
                        else Shot(s, a, Rules.CornerGoal + TargetMan(team), "Free kick");
                        break;
                    default:
                        if (defenseChoice == 2 && rng.Next(100) < Rules.FreeKickPressLoseBall)
                        { AddEvent(s, "lost", a, $"{opp.Name} press the short free kick and win the ball."); LoseBall(s, a); }
                        else AddEvent(s, "play", a, $"Short free kick — {team.Name} keep the ball.");
                        break;
                }
                break;
            }
            case DecisionKind.BigChance:
            {
                team.Stats.BigChances++;
                AddEvent(s, "info", a, $"{team.Name}: {Pick(true, attackChoice)} — keeper: {Pick(false, defenseChoice)}.");
                Shot(s, a, (attackChoice != defenseChoice ? Rules.BigChanceGoalDifferentSide : Rules.BigChanceGoalSameSide) + d.Bonus, "Big Chance");
                break;
            }
            case DecisionKind.Penalty:
            {
                if (PenaltyKick(s, a, attackChoice, defenseChoice, attackTiming, defenseTiming)) Goal(s, a, "Penalty", Cards.Penalty);
                else SetTurn(s, a.Other());
                break;
            }
            case DecisionKind.Shootout:
            {
                var shooter = s.NextShootoutShooter;
                var scored = PenaltyKick(s, shooter, attackChoice, defenseChoice, attackTiming, defenseTiming);
                s.Shootout.Add(new ShootoutKick { Side = shooter, Scored = scored });
                var (h, aw) = ShootoutScore(s);
                AddEvent(s, "info", shooter, $"Shootout: {s.Home.ShortName} {h}:{aw} {s.Away.ShortName}");
                if (ShootoutDecided(s))
                {
                    s.HomePenalties = h;
                    s.AwayPenalties = aw;
                    s.Phase = MatchPhase.Finished;
                    AddEvent(s, "whistle", h > aw ? Side.Home : Side.Away, $"{(h > aw ? s.Home : s.Away).Name} win the shootout {h}:{aw}!");
                }
                else StartDecision(s, DecisionKind.Shootout, s.NextShootoutShooter, "Shootout");
                break;
            }
        }
    }

    /// <summary>Target Man bonus for this cross (used once).</summary>
    private static int TargetMan(TeamMatchState team)
    {
        if (!team.TargetManActive) return 0;
        team.TargetManActive = false;
        var bonus = Rules.TargetManBonus + team.TargetManBonus;
        team.TargetManBonus = 0;
        return bonus;
    }

    /// <summary>Goalkeeper card of the defending team: makes this shot harder (used once).</summary>
    private int Keeper(MatchState s, Side defending)
    {
        var d = s.Team(defending);
        if (!d.GoalkeeperActive) return 0;
        d.GoalkeeperActive = false;
        AddEvent(s, "info", defending, $"{d.Name}'s goalkeeper is ready for it…", Cards.Goalkeeper);
        var reduction = Rules.GoalkeeperReduction + d.GoalkeeperBonus;
        d.GoalkeeperBonus = 0;
        return reduction;
    }

    private void Shot(MatchState s, Side a, int goalChance, string source, bool keepBallOnMiss = false)
    {
        goalChance -= Keeper(s, a.Other());
        if (rng.Next(100) < goalChance) { Goal(s, a, source, null); return; }
        var opp = s.Team(a.Other());
        if (rng.Next(2) == 0)
        {
            opp.Stats.Saves++;
            AddEvent(s, "save", a, $"Saved by the {opp.Name} keeper!");
        }
        else
        {
            s.Team(a).Stats.Misses++;
            AddEvent(s, "miss", a, $"{s.Team(a).Name} put it wide!");
        }
        if (keepBallOnMiss) { AddEvent(s, "play", a, $"{s.Team(a).Name} keep the ball."); return; }
        LoseBall(s, a);
    }

    /// <summary>A penalty with direction and timing. Timing: 0.5 = perfect; green zone ±0.12, red zone beyond ±0.38.</summary>
    private bool PenaltyKick(MatchState s, Side shooter, int shot, int dive, double shotTiming, double diveTiming)
    {
        var shotOff = Math.Abs(shotTiming - 0.5);
        var diveOff = Math.Abs(diveTiming - 0.5);
        double goal;
        if (shot != dive)
            goal = Rules.PenaltyGoalChance + (shotOff <= 0.12 ? 6 : 0) - (diveOff <= 0.12 ? 5 : 0);
        else
        {
            double save = shot == 1 ? Rules.PenaltySaveCenter : Rules.PenaltySaveSameSide;
            save += diveOff <= 0.12 ? 10 : diveOff >= 0.38 ? -15 : 0;
            save -= shotOff <= 0.12 ? 10 : 0;
            goal = 100 - save;
        }
        if (shotOff >= 0.38) goal -= 15; // a badly timed shot can fly over the bar
        var taker = s.Team(shooter);
        if (taker.PenaltyTakerActive)
        {
            taker.PenaltyTakerActive = false;
            goal += Rules.PenaltyTakerBonus + taker.PenaltyTakerBonus;
            taker.PenaltyTakerBonus = 0;
        }
        goal -= Keeper(s, shooter.Other());
        var scored = rng.NextDouble() * 100 < goal;
        var dirs = new[] { "left", "centre", "right" };
        var team = s.Team(shooter);
        var keeper = s.Team(shooter.Other());
        var timingWord = shotOff <= 0.12 ? " (perfectly timed)" : shotOff >= 0.38 ? " (badly timed)" : "";
        AddEvent(s, scored ? "goal" : "miss", shooter,
            $"{team.Name} go {dirs[Math.Clamp(shot, 0, 2)]}{timingWord}, the {keeper.Name} keeper " +
            (dive == 1 ? "stays in the centre" : $"dives {dirs[Math.Clamp(dive, 0, 2)]}") + (scored ? " — SCORED!" : " — NO GOAL!"));
        return scored;
    }

    private static (int Home, int Away) ShootoutScore(MatchState s) =>
        (s.Shootout.Count(k => k.Side == Side.Home && k.Scored), s.Shootout.Count(k => k.Side == Side.Away && k.Scored));

    private static bool ShootoutDecided(MatchState s)
    {
        var (h, a) = ShootoutScore(s);
        var hk = s.Shootout.Count(k => k.Side == Side.Home);
        var ak = s.Shootout.Count(k => k.Side == Side.Away);
        var n = Rules.ShootoutKicks;
        if (hk <= n && ak <= n) return h > a + (n - ak) || a > h + (n - hk);
        return hk == ak && h != a;
    }

    // ======================================================================= hints & helpers

    /// <summary>Row and column hints: dangerous neutral cards still face down.</summary>
    public static (int[] Rows, int[] Cols) Hints(MatchState s)
    {
        var n = Rules.GridSize;
        var rows = new int[n];
        var cols = new int[n];
        for (var i = 0; i < s.Table.Count; i++)
        {
            var c = s.Table[i];
            if (!c.Available || c.Owner != null || !Cards.Dangerous.Contains(c.Card)) continue; // a 0 can still hide a team card
            rows[i / n]++;
            cols[i % n]++;
        }
        return (rows, cols);
    }

    private static string Score(MatchState s) => $"{s.Home.Name} {s.Home.Goals}:{s.Away.Goals} {s.Away.Name}";

    private string Text(MatchState s, string key, Side side) =>
        Commentary.Get(key, rng, s.Team(side).Name, s.Team(side.Other()).Name, Score(s));

    private static void AddEvent(MatchState s, string type, Side? side, string text, string? card = null) =>
        s.Events.Add(new MatchEvent { Id = s.NextEventId++, Half = s.Half, Type = type, Side = side, Text = text, Card = card });
}
