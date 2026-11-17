using FootballCards.Api.Data.Entities;

namespace FootballCards.Api.Game;

public record MatchSetup(
    Team Home, Team Away,
    Side? HumanSide,
    Tactic HomeTactic, Tactic AwayTactic,
    int Difficulty,
    bool IsKnockout,
    string CompetitionName);

/// <summary>Действие на човека: изиграна карта, основно действие (със смяна на карта) или посока при дузпа.</summary>
public record PlayerAction(int? CardIndex, int? SwapIndex, int? PenaltyDirection);

public class GameRuleException(string message) : Exception(message);

/// <summary>
/// Двигателят на мача. Всяка "ситуация" е двубой между една атакуваща и една защитна карта,
/// избрани едновременно (AI не вижда картата на човека).
/// </summary>
public class MatchEngine(Random rng)
{
    public static int Advance(string attackCard) => attackCard switch
    {
        Cards.Pass or Cards.Dribble => 1,
        Cards.LongBall or Cards.Counter => 2,
        _ => 0
    };

    public MatchState Create(MatchSetup setup, IReadOnlyList<CardDefinition> cardDefs)
    {
        var s = new MatchState
        {
            Home = CreateTeam(setup.Home, setup.HomeTactic, setup.HumanSide == Side.Home, cardDefs),
            Away = CreateTeam(setup.Away, setup.AwayTactic, setup.HumanSide == Side.Away, cardDefs),
            CompetitionName = setup.CompetitionName,
            Stadium = setup.Home.Stadium,
            IsKnockout = setup.IsKnockout,
            Difficulty = setup.Difficulty,
            AddedTime1 = rng.Next(1, 4),
            AddedTime2 = rng.Next(2, 6),
            FirstKickOff = rng.Next(2) == 0 ? Side.Home : Side.Away,
        };

        if (setup.HumanSide is { } human)
        {
            var bonus = setup.Difficulty switch { 0 => -4, 2 => 4, _ => 0 };
            s.Team(human.Other()).DifficultyBonus = bonus;
        }

        s.Possession = s.FirstKickOff;
        s.Zone = 1;
        AddEvent(s, "whistle", s.Possession, Commentary.Get("kickoff", rng, Att(s).Name, Def(s).Name));
        return s;
    }

    private TeamMatchState CreateTeam(Team t, Tactic tactic, bool human, IReadOnlyList<CardDefinition> defs)
    {
        var ts = new TeamMatchState
        {
            TeamId = t.Id, Name = t.Name, ShortName = t.ShortName,
            PrimaryColor = t.PrimaryColor, SecondaryColor = t.SecondaryColor,
            BaseAttack = t.Attack, BaseMidfield = t.Midfield, BaseDefense = t.Defense,
            Tactic = tactic, IsHuman = human
        };
        foreach (var d in defs)
        {
            var target = d.Kind == CardKind.Attack ? ts.AttackDeck : ts.DefenseDeck;
            for (var i = 0; i < d.CopiesInDeck; i++) target.Add(d.Code);
        }
        Shuffle(ts.AttackDeck);
        Shuffle(ts.DefenseDeck);
        for (var i = 0; i < Rules.AttackHandSize; i++) Draw(ts, attack: true);
        for (var i = 0; i < Rules.DefenseHandSize; i++) Draw(ts, attack: false);
        return ts;
    }

    // ---------------------------------------------------------------- public API

    public void PlayHuman(MatchState s, PlayerAction action)
    {
        if (s.Phase == MatchPhase.Finished) throw new GameRuleException("Мачът е приключил.");
        var human = s.HumanSide ?? throw new GameRuleException("В този мач няма човешки играч.");

        if (s.Phase == MatchPhase.Penalty)
        {
            var dir = action.PenaltyDirection ?? throw new GameRuleException("Изберете посока за дузпата.");
            if (dir is < 0 or > 2) throw new GameRuleException("Невалидна посока.");
            var humanShoots = s.Penalty!.Shooter == human;
            var aiDir = AiPlayer.PenaltyDirection(!humanShoots, rng);
            ResolvePenalty(s, humanShoots ? dir : aiDir, humanShoots ? aiDir : dir);
            return;
        }

        var humanAttacks = s.Possession == human;
        var humanChoice = FromHuman(s, s.Team(human), action, humanAttacks);
        var attChoice = humanAttacks ? humanChoice : AiPlayer.ChooseAttack(s, rng);
        var defChoice = humanAttacks ? AiPlayer.ChooseDefense(s, rng) : humanChoice;
        Step(s, attChoice, defChoice);
    }

    /// <summary>AI играе и за двата отбора до края на мача.</summary>
    public void SimulateToEnd(MatchState s)
    {
        var guard = 0;
        while (s.Phase != MatchPhase.Finished && guard++ < 1000)
        {
            if (s.Phase == MatchPhase.Penalty)
                ResolvePenalty(s, AiPlayer.PenaltyDirection(true, rng), AiPlayer.PenaltyDirection(false, rng));
            else
                Step(s, AiPlayer.ChooseAttack(s, rng), AiPlayer.ChooseDefense(s, rng));
        }
    }

    private static Choice FromHuman(MatchState s, TeamMatchState team, PlayerAction a, bool attacking)
    {
        var hand = attacking ? team.AttackHand : team.DefenseHand;
        if (a.CardIndex is { } idx)
        {
            if (idx < 0 || idx >= hand.Count) throw new GameRuleException("Няма такава карта в ръката.");
            var card = hand[idx];
            if (!Rules.IsLegal(card, s.Zone, s.CounterAvailable))
                throw new GameRuleException($"Картата „{Cards.Names[card]}“ не може да се изиграе в тази ситуация.");
            return new Choice(card, idx);
        }
        if (a.SwapIndex is { } sw && (sw < 0 || sw >= hand.Count))
            throw new GameRuleException("Няма такава карта за смяна.");
        return new Choice(attacking ? Cards.Hold : Cards.Position, null, a.SwapIndex);
    }

    // ---------------------------------------------------------------- core resolution

    private void Step(MatchState s, Choice attChoice, Choice defChoice)
    {
        var att = Att(s);
        var def = Def(s);
        var home = s.Possession == Side.Home;
        var a = attChoice.Card;
        var d = defChoice.Card;

        Consume(att, attChoice, attack: true);
        Consume(def, defChoice, attack: false);
        att.PossessionTurns++;

        var minutes = Cards.Minutes(a) + rng.Next(0, 2);
        s.CounterAvailable = false;
        var zone = s.Zone;

        if (d == Cards.Foul)
        {
            minutes += 1;
            HandleFoul(s, a, d, fromTackle: false);
        }
        else if (d == Cards.Tackle && a != Cards.Hold && Roll(Rules.TackleFoulChance))
        {
            minutes += 1;
            HandleFoul(s, a, d, fromTackle: true);
        }
        else if (a == Cards.Shot)
        {
            var notBlocked = Rules.SuccessChance(a, d, zone, att, def, home);
            if (Roll(notBlocked))
                Shot(s, Rules.ShotGoalBase(zone), a, d, null);
            else
            {
                att.Shots++;
                AddEvent(s, "chance", s.Possession, Text("shot_blocked", s), a, d);
                if (Roll(Rules.CornerChance))
                {
                    s.Zone = 2;
                    AddEvent(s, "info", s.Possession, Text("corner", s));
                }
                else Turnover(s, toZone: 1, counter: true);
            }
        }
        else
        {
            var p = Rules.SuccessChance(a, d, zone, att, def, home);
            var ok = Roll(p);
            if (a == Cards.Cross)
            {
                if (ok) Shot(s, Rules.HeaderGoalBase, a, d, "cross_ok");
                else { AddEvent(s, "lost", s.Possession, LostText(s, d), a, d); Turnover(s, 0, counter: true); }
            }
            else if (a == Cards.Dribble && zone == 3)
            {
                if (ok) Shot(s, Rules.CloseRangeGoalBase, a, d, "dribble_box_ok");
                else { AddEvent(s, "lost", s.Possession, LostText(s, d), a, d); Turnover(s, 0, counter: true); }
            }
            else if (ok)
            {
                s.Zone = Math.Min(3, zone + Advance(a));
                var key = a switch
                {
                    Cards.Pass => "pass_ok", Cards.LongBall => "long_ok", Cards.Dribble => "dribble_ok",
                    Cards.Counter => "counter_ok", _ => "hold_ok"
                };
                AddEvent(s, s.Zone == 3 && zone < 3 ? "chance" : "play", s.Possession, Text(key, s), a, d);
            }
            else
            {
                AddEvent(s, "lost", s.Possession, LostText(s, d), a, d);
                Turnover(s, Rules.TurnoverZone(zone), counter: true);
            }
        }

        if (s.Phase == MatchPhase.Penalty) return; // минутите се добавят след дузпата
        s.Minute += minutes;
        CheckPeriodEnd(s);
    }

    private void Shot(MatchState s, int goalBase, string a, string d, string? introKey)
    {
        var att = Att(s);
        var def = Def(s);
        var home = s.Possession == Side.Home;
        att.Shots++;
        if (introKey != null) AddEvent(s, "chance", s.Possession, Text(introKey, s), a, d);

        var goal = Rules.GoalChance(goalBase, att, def, home);
        var roll = rng.NextDouble() * 100;
        if (roll < goal)
        {
            Goal(s, "goal", introKey == null ? a : null, introKey == null ? d : null);
        }
        else if (roll < goal + (100 - goal) * 0.55)
        {
            att.ShotsOnTarget++;
            AddEvent(s, "save", s.Possession, Text("save", s), introKey == null ? a : null, introKey == null ? d : null);
            Turnover(s, 0, counter: false);
        }
        else
        {
            AddEvent(s, "miss", s.Possession, Text("miss", s), introKey == null ? a : null, introKey == null ? d : null);
            Turnover(s, 0, counter: false);
        }
    }

    private void Goal(MatchState s, string key, string? a = null, string? d = null)
    {
        var att = Att(s);
        att.Goals++;
        att.ShotsOnTarget++;
        AddEvent(s, "goal", s.Possession, Text(key, s), a, d);
        s.Possession = s.Possession.Other();
        s.Zone = 1;
        s.CounterAvailable = false;
    }

    private void HandleFoul(MatchState s, string a, string d, bool fromTackle)
    {
        var def = Def(s);
        def.Fouls++;
        AddEvent(s, "foul", s.Possession, Text("foul", s), a, d);

        var yellow = fromTackle ? Rules.TackleYellowChance : Rules.FoulYellowChance;
        if (Roll(yellow))
        {
            if (def.YellowCards >= 2 && def.RedCards < 2 && Roll(25))
            {
                def.RedCards++;
                AddEvent(s, "red", s.Possession.Other(), Text("red", s));
            }
            else
            {
                def.YellowCards++;
                AddEvent(s, "yellow", s.Possession.Other(), Text("yellow", s));
            }
        }

        switch (s.Zone)
        {
            case 3:
                s.Phase = MatchPhase.Penalty;
                s.Penalty = new PenaltyContext { Shooter = s.Possession };
                AddEvent(s, "penalty", s.Possession, Text("penalty_awarded", s));
                break;
            case 2:
                AddEvent(s, "chance", s.Possession, Text("free_kick", s));
                var att = Att(s);
                att.Shots++;
                var g = Rules.GoalChance(Rules.FreeKickGoalBase, att, def, s.Possession == Side.Home);
                if (Roll(g)) Goal(s, "free_kick_goal");
                else { AddEvent(s, "miss", s.Possession, Text("free_kick_miss", s)); Turnover(s, 0, counter: false); }
                break;
            default:
                // атакуващият запазва топката, но не напредва
                break;
        }
    }

    private void ResolvePenalty(MatchState s, int shooterDir, int keeperDir)
    {
        var att = Att(s);
        var def = Def(s);
        att.Shots++;
        bool scored;
        string key;
        if (shooterDir != keeperDir)
        {
            scored = Roll(88 + (att.Attack - def.Defense) * 0.1);
            key = scored ? "penalty_goal" : "penalty_miss";
        }
        else
        {
            var saveChance = shooterDir == 1 ? 70 : 75;
            scored = !Roll(saveChance);
            key = scored ? "penalty_goal" : "penalty_saved";
        }

        var dirs = new[] { "наляво", "в средата", "надясно" };
        var info = $"Изпълнителят стреля {dirs[shooterDir]}, вратарят се хвърля {dirs[keeperDir]}.";
        AddEvent(s, "info", s.Possession, info);

        s.Phase = MatchPhase.Play;
        s.Penalty = null;
        if (scored) Goal(s, key);
        else
        {
            if (key == "penalty_saved") att.ShotsOnTarget++;
            AddEvent(s, key == "penalty_saved" ? "save" : "miss", s.Possession, Text(key, s));
            Turnover(s, 0, counter: false);
        }
        s.Minute += 2;
        CheckPeriodEnd(s);
    }

    private void Turnover(MatchState s, int toZone, bool counter)
    {
        s.Possession = s.Possession.Other();
        s.Zone = toZone;
        s.CounterAvailable = counter;
    }

    private void CheckPeriodEnd(MatchState s)
    {
        if (s.Phase != MatchPhase.Play) return;
        if (s.Half == 1 && s.Minute >= 45 + s.AddedTime1)
        {
            AddEvent(s, "whistle", null, Commentary.Get("halftime", rng, "", "", Score(s)));
            s.Half = 2;
            s.Minute = 45;
            s.Possession = s.FirstKickOff.Other();
            s.Zone = 1;
            s.CounterAvailable = false;
            AddEvent(s, "whistle", s.Possession, Commentary.Get("kickoff2", rng, Att(s).Name, Def(s).Name));
        }
        else if (s.Half == 2 && s.Minute >= 90 + s.AddedTime2)
        {
            if (s.IsKnockout && s.Home.Goals == s.Away.Goals) Shootout(s);
            s.Phase = MatchPhase.Finished;
            var score = Score(s) + (s.HomePenalties.HasValue ? $" (дузпи {s.HomePenalties}:{s.AwayPenalties})" : "");
            AddEvent(s, "whistle", null, Commentary.Get("fulltime", rng, "", "", score));
        }
    }

    /// <summary>Автоматична серия дузпи при равенство в елиминационен мач.</summary>
    private void Shootout(MatchState s)
    {
        AddEvent(s, "penalty", null, "Равенство! Победителят ще се определи с дузпи.");
        int h = 0, a = 0, hk = 0, ak = 0;
        bool Decided() => hk <= 5 && ak <= 5
            ? h > a + (5 - ak) || a > h + (5 - hk)
            : hk == ak && h != a;
        while (hk < 40)
        {
            hk++;
            if (Roll(76 + (s.Home.Attack - s.Away.Defense) * 0.1)) h++;
            if (Decided()) break;
            ak++;
            if (Roll(76 + (s.Away.Attack - s.Home.Defense) * 0.1)) a++;
            if (Decided()) break;
        }
        s.HomePenalties = h;
        s.AwayPenalties = a;
        var winner = h > a ? s.Home.Name : s.Away.Name;
        AddEvent(s, "penalty", h > a ? Side.Home : Side.Away, $"{winner} печели след дузпи — {h}:{a}!");
    }

    // ---------------------------------------------------------------- helpers

    private void Consume(TeamMatchState t, Choice c, bool attack)
    {
        var hand = attack ? t.AttackHand : t.DefenseHand;
        var discard = attack ? t.AttackDiscard : t.DefenseDiscard;
        var index = c.HandIndex ?? c.SwapIndex;
        if (index is { } i && i >= 0 && i < hand.Count)
        {
            discard.Add(hand[i]);
            hand.RemoveAt(i);
            Draw(t, attack);
        }
    }

    private void Draw(TeamMatchState t, bool attack)
    {
        var deck = attack ? t.AttackDeck : t.DefenseDeck;
        var discard = attack ? t.AttackDiscard : t.DefenseDiscard;
        var hand = attack ? t.AttackHand : t.DefenseHand;
        if (deck.Count == 0)
        {
            deck.AddRange(discard);
            discard.Clear();
            Shuffle(deck);
        }
        if (deck.Count == 0) return;
        hand.Add(deck[^1]);
        deck.RemoveAt(deck.Count - 1);
    }

    private void Shuffle(List<string> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private bool Roll(double percent) => rng.NextDouble() * 100 < percent;

    private static TeamMatchState Att(MatchState s) => s.Team(s.Possession);
    private static TeamMatchState Def(MatchState s) => s.Team(s.Possession.Other());
    private static string Score(MatchState s) => $"{s.Home.Name} {s.Home.Goals}:{s.Away.Goals} {s.Away.Name}";

    private string Text(string key, MatchState s) =>
        Commentary.Get(key, rng, Att(s).Name, Def(s).Name, Score(s), s.Zone);

    private string LostText(MatchState s, string defCard) => Text(defCard switch
    {
        Cards.Press => "lost_press",
        Cards.Tackle => "lost_tackle",
        Cards.Intercept => "lost_intercept",
        Cards.Block => "lost_block",
        _ => "lost_position"
    }, s);

    private static void AddEvent(MatchState s, string type, Side? side, string text, string? a = null, string? d = null)
    {
        s.Events.Add(new MatchEvent
        {
            Id = s.NextEventId++, Minute = s.Minute, MinuteLabel = s.MinuteLabel, Type = type,
            Side = side, Text = text, AttackCard = a, DefenseCard = d
        });
    }
}
