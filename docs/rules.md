# Dugout Deck — rules

Dugout Deck is a single-player football card game against the computer. Matches are played by flipping face-down cards;
before each match both teams build a tactic from their card collection. Luck leads, but reading the hints, a good tactic and
well-timed hand cards win more often.

All numbers below are in `server/DugoutDeck.Api/Game/Rules.cs` and the card catalog in `server/DugoutDeck.Api/Data/Seed/seed.json`.
They were tuned with 4,000 simulated matches (see the balance test): about **5 goals per match**, 0:0 in under 1% of matches,
a strong team (85) beats a weak one (58) about 55% of the time and loses about 23%. About 1.7 team cards are stolen per match.

---

## 1. The grid

Each half has its own 5×5 grid of 25 face-down cards. **All backs are the same grey.**

| Part | Count |
|---|---|
| Neutral cards | 16 |
| Home team cards (from its tactic) | 4 |
| Away team cards | 4 |
| Special card | 1 — **Half Time** in the 1st half, **Full Time** in the 2nd |

**Hidden ownership:** the player sees where their own 4 team cards are (a small flag in the club colours in the corner of the
card, only on their screen). Nobody sees where the opponent's cards are — the computer only knows its own cards, and the server
never sends the positions of the computer's cards to the browser. At half time the cards are reshuffled and the flags move.

Full Time can never come before Half Time. When Half Time is flipped the match pauses: the finished first half is shown revealed
and the player presses **Start second half**. Then every card goes face down again and is reshuffled; the team cards come
back (a card burned in the first half returns in the second). There is no clock — a half lasts 1–25 flips (about 14).

### Neutral base set (per half)

| Card | Count | Type | Effect | Next to flip |
|---|---|---|---|---|
| Pass | 4 | keep | You keep the ball; counts towards the pass streak | same team |
| Corner | 1 | keep | Corner mini-game | depends on outcome |
| Free Kick | 1 | keep | Free-kick mini-game | depends on outcome |
| Tackle | 3 | dangerous | Ball lost | other team |
| Offside | 1 | dangerous | Ball lost | other team |
| Save | 1 | dangerous | The opponent's keeper saves | other team |
| Miss | 1 | dangerous | Wide | other team |
| Yellow Card | 1 | dangerous | Booking (second yellow = red) | other team |
| Red Card | 1 | dangerous | Sent off (see 5) | other team |
| Goal! | 1 | goal | Goal for the team that flipped it | other team (kick-off) |
| Penalty | 1 | goal | Penalty mini-game | other team |

*Balance change vs. the design draft: 4 Pass / 1 Goal instead of 3 Pass / 2 Goal — with team cards in both halves the draft
produced ~10 goals per match.*

**Always at least one of each (1.1):** whatever the modifiers, every grid keeps at least one of each grey card — at least one
Goal, one Penalty, one Corner, one Free Kick, one Tackle, one Save, one Miss, one Offside, one Yellow and one Red card. A goal can
always come "out of nowhere"; the "−" modifiers only cancel extra copies.

### Hints

Next to every row and under every column is the number of **dangerous neutral cards** (Tackle, Offside, Save, Miss, Yellow,
Red) still face down there. They update after every flip. Goals, penalties, Half Time/Full Time and team cards are not counted —
so **a 0 does not mean "safe"**: an opponent's Offside Trap (or Half Time) can still hide in that row or column.

### Match conditions (shown before the match)

For now the only condition is **Home: +1 tactic point** for the home team, so the neutral set is always the same (apart from the
tactic modifiers). Rain, wind and derby are switched off and will come back later as a separate module
(`Rules.WeatherAndDerby` in the code).

## 2. Turn order

- A coin toss decides who flips first; the other team starts the second half.
- The team on turn flips one face-down card.
- **One own card per turn:** while on turn (until it loses the ball), a team may flip at most one of its own team cards. Flipping
  the opponent's cards is not limited (you don't know where they are). Exceptions: during a Counter Attack, and when only your own
  cards are left face down. In the game, your flags fade once you have played an own card this turn.
- You keep the turn while you get "keep" results. A dangerous card, a goal or a penalty passes the turn.
- Before a flip you may play **one** hand card.

## 3. Goals and mini-games

| Source | How it works |
|---|---|
| Goal! | Direct goal |
| Penalty | Shooter and keeper each pick left/centre/right, then stop a **timing bar**. Different sides → 88% goal; same side → keeper saves 75% (70% centre–centre). Green timing (±0.12): shooter +6% / keeper +10% save; red timing (beyond ±0.38): +15% risk of missing for the shooter, −15% save chance for the keeper. |
| Corner | Attacker: near post / far post / short corner. Defender: guard near / guard far / press the short corner. Short corner keeps the ball (lost if the defender pressed it). Same post → cleared, ball lost. Different → 30% goal, otherwise save/miss. |
| Free kick | Attacker: shoot / cross / short pass. Defender: wall / mark the box / press. Shot: 25% goal (10% into the wall). Cross: cleared if the box is marked, otherwise 30% goal. Short pass keeps the ball (50% lost if pressed). |
| Big Chance | After **3 passes in a row** by one team (or the Big Chance team card): shot left/centre/right vs keeper — 35% goal if the keeper guesses wrong, 15% if right. |
| Team cards | Striker, Big Chance, Long Shot (see 4) |

The human always chooses — when attacking and when defending. "Decide automatically" (or the Settings option) picks for you; an
automatic timing lands around the middle but is mostly random. The computer chooses at random; strong computers time penalties better.

**Knockouts:** a draw at Full Time goes to a shootout — 5 kicks each, then sudden death, each kick being the penalty mini-game.

## 4. Team cards and hand cards

### Field cards (in the grid, with your colours)

**Whoever flips a team card gets its effect.** Your own card works for you. If the opponent flips it, they **steal** it — the
effect is theirs. The only exception is the Offside Trap, which cannot be stolen. Because of "one own card per turn" you have to
choose when to play each card — the longer it waits, the bigger the risk that it gets stolen.

| Card | Rarity | Points | If the owner flips it | If the opponent flips it (steal) |
|---|---|---|---|---|
| Striker | common | 4 | Clean through: 35% goal | the same, for the thief |
| Big Chance | common | 3 | Big Chance mini-game | the same, for the thief |
| Long Shot | common | 1 | 12% goal; if it misses you keep the ball | the same, for the thief |
| Playmaker | rare | 2 | A free Scout, keep the ball | the same, for the thief |
| Captain | rare | 2 | Your next Tackle is ignored | protects the thief |
| Wall | rare | 2 | The opponent's next goal this half becomes a Save | protects the thief |
| Offside Trap | epic | 3 | Works like a Pass | **cannot be stolen** — caught offside, they lose the ball |
| Counter Attack | epic | 3 | Flip 2 more cards; dangerous ones don't count | the same, for the thief |
| Squad Player | — | 0 | Works like a Pass (free filler) | a Pass for the thief |
| Winger *(1.1)* | common | 2 | A corner (mini-game); the team keeps the ball even if it is not a goal | the same, for the thief |
| Goalkeeper *(1.1)* | rare | 2 | The next shot or penalty against you: −20% goal chance | protects the thief |
| Target Man *(1.1)* | common | 1 | Your next cross (corner or free kick): +20% goal chance | the same, for the thief |
| Sweeper *(1.1)* | rare | 1 | Your next Offside or Save is ignored — you keep the ball | protects the thief |
| Super Sub *(1.1)* | epic | 3 | 2nd half: 60% goal; 1st half: works like a Pass | the same, for the thief |
| Decoy *(1.1)* | common | 1 | Works like a Pass | **cannot be stolen** — they gain nothing and lose their pass streak |
| 12th Man *(1.1)* | rare | 2 | Home team: the next dangerous card is ignored; away team: a Pass | the same, for the thief |

*Balance changes vs. the draft: Striker is a 35% chance (not a sure goal) and costs 4; Big Chance costs 3; Long Shot 12%.*

### Hand cards (played before a flip, one per flip)

| Card | Points | Effect |
|---|---|---|
| Scout | 1 | Look under one face-down card (only you see it — including whose team card it is) |
| Swap | 1 | Swap the places of two face-down cards |
| Calm | 2 | If your next card is a Tackle, you keep the ball |
| Press | 2 | The next time you lose the ball, flip one more card straight away |
| VAR *(1.1)* | 2 | The opponent's next goal is checked: 40% chance it is disallowed |
| Marking *(1.1)* | 2 | Pick one of your face-down cards: if the opponent flips it, they can't steal it and lose the ball |
| Substitution *(1.1)* | 2 | Pick one of your team cards that is already face up: it goes back face down to a random place |
| Analysis *(1.1)* | 1 | Pick a row: you learn how many opponent cards are hidden in it |
| Penalty Taker *(1.1)* | 1 | Your next penalty: +10% goal chance |
| Burn | 2 | **Blind:** burns a random face-down team card of the opponent. A 🔥 is left in its place; you don't see which card it was (cost to be reviewed after balancing) |

## 5. Discipline

- Yellow card: recorded. A **second yellow** for the same team in a match is a red.
- Red card *(changed in 1.1)*: the **opponent** chooses which hand card the sent-off team loses. When the computer is sent off,
  its hand is revealed and you take away the card you want; when you are sent off, the computer takes your most valuable hand
  card (the highest tactic cost). The sent-off team's pass streak no longer gives Big Chances.

## 6. Tactics

- A tactic = **4 field cards** + **1–3 hand cards** (by rating) + up to **2 neutral modifiers**.
- Budget: **10 tactic points** + 0–3 by rating (64+ → 1, 72+ → 2, 80+ → 3) + 1 for the home team.
- Hand cards: 1 (rating < 66), 2 (66+), 3 (78+).
- Tactics can be saved; in a career the last tactic is used automatically ("Play (last tactic)").
- **Tactics tab (1.1):** a career has three plans (Plan A / B / C, renameable) that you prepare calmly between matches. One plan is
  **★ active**: it is loaded before every match and used by "Play (plan name)". On the pre-match screen you can still change it or
  switch to another plan with one click. A plan is checked against the home budget; if it costs one point too many for an away
  match, the Tactics tab says so and the pre-match screen asks you to drop a card.
- Every career starts with an **empty tactic**: the first match asks you to build one. The first time ever, a coach panel explains
  each step (field cards → hand cards → modifiers) while you pick.
- The computer builds its tactic from its rating and the opponent (attacking / balanced / defensive). Rare cards from rating 66,
  epic cards from 76. Background matches do the same.

### Neutral modifiers — the secret choice

Both teams secretly pick up to 2 modifiers; they are revealed together and **added up** (e.g. +2 Goal and −1 Goal = +1 Goal).
Removals are applied first, then additions in a fixed order (Goal, Penalty, Corner, Tackle), so the result does not depend on who
chose what. A "−" modifier never removes the last card of its kind (see 1).

| Modifier | Points | Effect |
|---|---|---|
| +1 / +2 Goal | 2 / 4 | Pass cards become Goals |
| −1 / −2 Goal | 1 / 2 | Extra Goals become Pass cards (at least 1 Goal stays) |
| +1 / +2 Corner | 1 / 2 | Pass cards become Corners |
| −1 / −2 Corner | 1 / 1 | Extra Corners become Pass cards (at least 1 Corner stays) |
| +1 Tackle | 1 | A Pass becomes a Tackle |
| +1 Penalty | 3 | A Pass becomes a Penalty |
| +1 Free Kick *(1.1)* | 1 | A Pass becomes a Free Kick |
| −1 Tackle *(1.1)* | 1 | A Tackle becomes a Pass (at least 1 Tackle stays) |
| +1 Save *(1.1)* | 1 | A Pass becomes a Save |
| Wet pitch *(1.1)* | 1 | A Pass becomes a Miss |

Limits: Pass ≥ 1, Goal 1–5, Corner 1–4, Tackle 1–6, Penalty 1–3, Miss 1–4, Yellow 1–3, Free Kick 1–3, Save 1–3. Modifiers apply to both halves.

## 7. Ratings, difficulty and the computer

The rating **never changes the odds of a card**. It gives tactic points, hand cards and the computer's skill:

| Level | Rating | How it plays |
|---|---|---|
| Weak | < 66 | Flips at random half of the time; plays hand cards at random moments |
| Average | 66–77 | Prefers safe cards by the hints but makes mistakes (20% random) |
| Strong | 78+ | Almost always the best card by the hints; plays its own cards at the right moment (one per turn, keeps its Offside Trap in the grid), uses the blind Burn early |

Difficulty (chosen for a career when it starts; for friendlies in Settings):

| Difficulty | You | The computer opponent |
|---|---|---|
| Easy | +2 tactic points | plays one level weaker, 1 tactic point less |
| Normal | — | — |
| Hard | −1 tactic point | plays one level stronger, 1 tactic point more |

Background matches are not affected. The computer never knows where your team cards are — it can only steal them by chance, like you. Background matches use the same rules.

## 8. Career

- **Leagues:** 35 leagues from 25 countries (all clubs, leagues and cups are fictional: a club is named after its main colour and city, e.g. "Red Sofia"; see the README for how to rename them). Two divisions with promotion/relegation and a national
  cup: England, Spain, Germany, Italy, France, Netherlands, Portugal, Turkey, Scotland, Bulgaria. Top division only: Belgium,
  Greece, Austria, Switzerland, Denmark, Czechia, Croatia, Serbia, Romania, Poland, Ukraine, Cyprus, Hungary, Norway, Sweden.
  Leagues of 12 teams or fewer play each other three times.
- **Fixtures (1.1):** a balanced schedule — no team plays more than **2 home or 2 away matches in a row**. The second half of the
  season is the first half with home and away swapped. New careers get it at once; a career started before 1.1 gets it from its
  next season.
- **Table colours (1.1):** every league table is coloured by that league's own rules, with a legend underneath: gold = champion,
  blue = Champions Cup, orange = Continental Cup, teal = Challenge Cup, green = promotion (second divisions), red = relegation
  (as many teams as really go down: 3, 2 or 1 in Scotland). Countries with only one division have no relegation.
- **Calendar:** one weekly calendar for everything (the longest league has 46 rounds; leagues with fewer rounds have rest weeks)
  plus 5 cup weeks and 5 European weeks — 56 weeks. After your match every other match of the week is played in the background
  with the same rules. "Continue" skips weeks in which your team has no match.
- **National cups:** 32 teams (top division + the best of the second), knockout from the Round of 32 (a country with fewer teams starts later).
- **European cups:** Champions Cup, Continental Cup, Challenge Cup — 32 teams each, knockout from the Round of 32 (group stage later).
  Places: top-five countries 1st–4th / 5th + cup winner / 6th; Bulgaria 1st / 2nd + cup winner / 3rd. If the cup winner has
  already qualified, the place goes to the next team in the table. The remaining places are filled with the strongest clubs that
  have no league, then with the strongest top-division clubs that did not qualify. If a cup has more than 32 qualifiers, the
  weakest drop to the next cup (Champions → Continental → Challenge). In the first season the places go by rating.
- **Promotion/relegation:** England, Spain, Italy and Turkey 3; Germany, France, Bulgaria, the Netherlands and Portugal 2;
  Scotland 1.
- **Board target** by rating rank in the league: title (top 2) / top 4 (3rd–5th) / top half (6th–10th) / avoid the drop.
  Review: target − actual position, +2 for the cup, +1 per European knockout win. ≤ −4 or last place = sacked.
- **Reputation** 0–100. **Offers:** a good season brings offers from stronger clubs in any league; a sacked manager gets offers
  from weaker clubs.
- **The cards belong to the club (1.2):** the manager has no personal collection. Taking a job at another club means leaving the
  cards behind; the new club's own squad cards (6 field + 4 hand cards that the club can use, with tiers by its rating) become
  yours. Tactic plans stay and are checked against the new cards before the next match.
- **Draft (1.2):** a new career starts with a "pick 1 of 3" draft instead of a starter collection: 10 rounds, field cards in
  rounds 1–6 and hand cards in rounds 7–10. Each card comes with a tier (pack odds). Matches can be played once the draft is done.

## 9. Money, rewards and the Store

- **Income:** per match (win / draw / loss), cup and European rounds, season TV money (by league and rating) and a prize for the
  final position. Top-five leagues pay ×2; Netherlands, Portugal, Turkey, Belgium ×1.5; Scotland, Greece, Austria, Switzerland,
  Denmark, Czechia, Norway, Sweden ×1.2; the rest ×1. Second divisions ×0.4.
- **Card rewards:** a win gives a card 30% of the time (European knockout wins always); meeting the board target gives a pack;
  every trophy and a league title give a pack with an epic card; **every new season starts with a free pack from the board (1.2)**.
  Every card from a reward comes with a tier.
- **Store** (in-game money only, no pay-to-win) *(changed in 1.2)*:

  | Pack | Price | Guarantee |
  |---|---|---|
  | 3 cards | €500K | — |
  | 5 cards | €800K | at least one Rare or better |
  | 10 cards | €1.5M | at least one Rare or better |

  Opening: the sealed pack shakes until you tear it open; the cards come out face down and their backs glow silver / gold /
  rainbow for Rare / Ultra Rare / Legendary before you flip them (click one by one, or "Reveal all").
  Single cards are Normal copies at the card's store price. Club upgrades: +2 rating per level, up to 10 levels.
- **Market, always open (1.2):** any copy can be sold at any time, cheaply — Normal €15K, Rare €60K, Ultra Rare €250K,
  Legendary €1M. "Sell all duplicates" sells every Normal copy beyond the first of each card.

### The economy (1.3)

Money now flows out as well as in:

- **Weekly expenses:** every week the club pays wages and stadium costs: €55K × league factor × (rating / 70)², rounded to
  €1K, plus **€6K upkeep per club upgrade level**. Examples: a 75-rated Bulgarian club ~€63K a week (~€3.5M a season),
  a 92-rated English club ~€190K a week (~€10.6M a season), a 50-rated club ~€28K a week.
- **Pack prices follow the league:** ×(0.5 + league factor / 2), between ×0.7 (second divisions) and ×1.5 (top five).
  The 3-card pack costs €350K in a second division, €500K in Bulgaria and €750K in England.
- **Club upgrades** cost €4M × (level + 1) (was €3M) and add upkeep.
- **Merging (a money and card sink):** 5 Normal → 1 Rare (€50K), 4 Rare → 1 Ultra Rare (€200K), 3 Ultra Rare → 1 Legendary
  (€600K), in the Collection tab.
- **Income and expenses** of each season are counted (buying and selling cards are not) and shown in Overview → Club
  finances, at the end of the season and in the club history.
- **Debt:** the club can go below zero (the game warns you once). At the end of the season any debt costs 2 points in the board's
  review; a debt bigger than €2M × league factor gets the manager sacked.

Economy test (9 careers × 3 seasons, money at the end of each season; "saver" buys nothing):

| Club | Start | 1.2: after 3 seasons | 1.3: after 1 / 2 / 3 seasons |
|---|---|---|---|
| Strong (92, England), saver | €4.0M | €41.6M | €4.8M / €8.6M / €11.1M |
| Average (75, Bulgaria), saver | €2.0M | €22.7M | €13.2M / €16.0M / €17.3M (won a European cup in season 1) |
| Weak (50), saver | €0.8M | €13.4M | €2.2M / €4.3M / €5.8M |

A club that keeps buying packs ends each season with ~€1M and 60–100 cards after three seasons. The one remaining jackpot is
European prize money (not scaled by league): a small club that goes deep in the Champions Cup earns a lot.

## 9b. Club history and achievements (1.3)

A **Club history** tab in the career:

- **Trophy cabinet:** every league title, national cup and European cup, by season and club.
- **Record:** played / won / drawn / lost and goals of all results (simulated matches included); biggest win, worst defeat,
  longest unbeaten run and longest winning run.
- **Your matches, card by card** (only matches you played yourself): goals by source, penalties scored of awarded, cards you
  stole and cards stolen from you, your most stolen card, your favourite card, Legendary cards played.
- **Seasons:** club, league, position, points, goals, cups, income, expenses and the board's verdict.
- **25 achievements**, each paid once (money is counted as income):

  | Achievement | How | Reward |
  |---|---|---|
  | First win | win a match | €100K |
  | Clean sheet | no goal against | €100K |
  | Untouchable | the opponent steals none of your cards | €150K |
  | Pickpocket | steal 3 cards in one match | €200K |
  | Thanks for the striker | score with a stolen Striker | €200K |
  | Goal fest | score 5 goals | €200K |
  | Thrashing | win by 4+ goals | €200K |
  | Ten men, no problem | win after a red card | €200K |
  | The keeper guessed right | stop an opponent's penalty | €100K |
  | Nerves of steel | win a penalty shootout | €200K |
  | Legend on the pitch | a Legendary card of yours takes effect | €150K |
  | Trap master | your Offside Traps spring twice in a match | €150K |
  | VAR saves you | VAR disallows an opponent's goal | €100K |
  | Collector | the club owns 50 cards | €200K |
  | Something special | the club owns a Legendary card | €200K |
  | Full squad | every field card at least once | a pack |
  | Upgrade! | merge cards | €100K |
  | Champions! | win the league | an epic pack |
  | Cup winners | win the national cup | a pack |
  | Kings of Europe | win a European cup | €1M + an epic pack |
  | Invincibles | a league season without a defeat | €1M |
  | Fairy tale | win the league with a club rated below 70 | €1M |
  | Going up | win promotion | €300K |
  | Board's favourite | the board is delighted | €300K |
  | One-club manager | three seasons at the same club | €300K |

Match achievements count only matches you play yourself.

## 9c. Name packs (1.3)

All names are fictional. A **name pack** is a JSON file that renames clubs (name, short name, city, stadium, colours), leagues
and cups by their code, in every save. Settings → **Name packs**: import a file, switch packs on and off, delete them, export the
current names as a template, reset to the original names. Packs are kept in `%LOCALAPPDATA%\DugoutDeck\name-packs`
(`.json` = on, `.json.off` = off) and survive updates. The game ships only `name-pack-template.json` and
`docs/name-packs.md`; packs with real names are made and shared by players. Clubs changed in the in-game editor keep their
names; ratings cannot be changed by a pack; badges are always drawn from the colours.

## 9a. Card tiers (1.2)

Every copy of a field or hand card is **Normal**, **Rare**, **Ultra Rare** or **Legendary** (the free Squad Player has no
tiers). Pack odds per card: Normal 80%, Rare 16%, Ultra Rare 3.2%, Legendary 0.8%. The card's old rarity (common / rare / epic)
now only means how often the card itself turns up in packs ("common / uncommon / scarce" in the gallery). The tactic cost is the
same for every tier. **The bonus travels with the card: a thief who steals a Legendary Striker shoots with the Legendary chance.**

**Cards with a chance:** +4 / +8 / +12 percentage points for Rare / Ultra Rare / Legendary.

| Card | Normal | Rare | Ultra Rare | Legendary |
|---|---|---|---|---|
| Striker | 35% | 39% | 43% | 47% |
| Long Shot | 12% | 16% | 20% | 24% |
| Super Sub (2nd half) | 60% | 64% | 68% | 72% |
| Big Chance card | +0 | +4 | +8 | +12 to the Big Chance shot |
| Winger | +0 | +4 | +8 | +12 to the corner shot |
| Target Man | +20 | +24 | +28 | +32 to the next cross |
| Goalkeeper | −20 | −24 | −28 | −32 for the next shot against you |
| VAR | 40% | 44% | 48% | 52% to disallow the goal |
| Penalty Taker | +10 | +14 | +18 | +22 to the next penalty |

**Cards without a chance:** Rare and better copies add **+1 to the pass streak** of the team that gets the effect (never
straight to a Big Chance — the streak stops at 2) *(1.3)*; Ultra Rare and Legendary add an extra:

| Card | Ultra Rare | Legendary |
|---|---|---|
| Captain | ignores 2 Tackles | ignores 3 Tackles |
| Wall | stops 2 goals | stops 3 goals |
| Sweeper | ignores 2 Offside/Save cards | ignores 3 |
| 12th Man | ignores 2 dangerous cards | ignores 3 |
| Calm | lasts 2 cards | lasts 3 cards |
| Press | works 2 times | works 3 times |
| Counter Attack | 3 extra cards | 4 extra cards |
| Playmaker | +1 free Scout (2 in all) | +2 free Scouts (3 in all) |
| Scout | +1 free Scout | +2 free Scouts |
| Offside Trap, Decoy | a free Scout for the owner when it springs | two free Scouts |
| Swap, Analysis, Substitution, Marking | quick play: it does not use your one hand card for the flip | the same |
| Burn | quick play | quick play, burns 2 cards |

**The computer** has tiers too in careers (not in friendlies): weak clubs mostly Normal, average clubs some Rare, strong clubs
often Rare, sometimes Ultra Rare and now and then Legendary. Background matches use the same tiers.

Balance with tiers (2,000 career-style matches): 5.2 goals per match (5.1 without tiers); strong (85) vs weak (58) 63% / 18%.

## 10. Presentation

- **Hold to peek (1.3):** your own face-down cards show only the corner flag; keep the mouse on one for a moment to see which
  card it is (also while the opponent is on turn).
- **Tutorial:** a scripted 14-step match (main menu → Tutorial) that shows every rule once.
- **Card pictures:** images in `client/public/cards/` (file name = card code) replace the icons on the cards; the name is written under the picture and the icon sits in a corner badge. Larger cards tilt in 3D, rare and epic cards shine (holo), right-click zooms any card.
- **Card gallery:** every card with its picture, effect, type, cost, how often it turns up in packs, store price and what each
  tier adds; the tier buttons under the big card show its frame (main menu → Card gallery).
- **Rate & review:** opens the game's itch.io page in the browser (main menu, About, after a match).
- **Fullscreen (F11):** in the desktop app the window covers the whole monitor, taskbar included.
- **Match pace (1.1):** Settings → *Match pace* and the buttons under the grid: **Fast** (no pauses), **Normal**, **Dramatic**.
  The computer "thinks", raises its card and turns it over. Important cards — team cards, Goal, Penalty, Red Card, steals and
  goals — get a build-up: the card is raised and glows, the crowd holds its breath, then it turns over with its sound and effect.
  Your own grey cards always turn over at once (nobody likes waiting for themself). *Reduce animations* works like Fast.
- **Active effects (1.1):** a panel next to the grid lists everything running for both teams in words (e.g. "Captain: the next
  Tackle is ignored"). When an effect is used up it lights up for a moment and is crossed out. Below it, **Cards played** lists the
  cards each team has played (hand, own, stolen, trap, decoy).
- **Animations:** 3D card flips, red shake for dangerous cards, purple glow for steals, gold for goals and penalties, a GOAL! / HALF
  TIME / FULL TIME banner (confetti for your own goals; the computer waits while it is shown), the second-half grid is dealt card by
  card, row/column highlight with the danger count on hover, pace buttons in the match, animated pack opening. Settings →
  *Reduce animations* turns off confetti and shaking.
- **Sound:** every effect and the menu music are generated in code (`client/src/sound.ts`, Web Audio API) — no recordings, nothing
  copyrighted. Crowd ambience during matches, music in the menus, volume sliders in Settings, **M** mutes everywhere.

## 11. Saves and versions

- The game version is in `server/Directory.Build.props` and is shown in the main menu and on the About screen.
- Save files are upgraded with EF Core migrations (`server/DugoutDeck.Api/Data/Migrations`). Before an upgrade the game writes
  a backup copy next to the save (`dugout-deck.db.backup-<date>`). Saves from before migrations existed are recognised and upgraded.
- Every team, league and competition has a stable code in `seed.json`; names, stadiums, colours and ratings are synced from
  seed.json by that code on every start (teams changed in the in-game editor are left alone).
