# Dugout Deck — rules

Dugout Deck is a single-player football card game against the computer. Matches are played by flipping face-down cards;
before each match both teams build a tactic from their card collection. Luck leads, but reading the hints, a good tactic and
well-timed hand cards win more often.

All numbers below are in `server/DugoutDeck.Api/Game/Rules.cs` and the card catalog in `server/DugoutDeck.Api/Data/Seed/seed.json`.
They were tuned with 4,000 simulated matches (see the balance test): about **4.9 goals per match**, 0:0 in under 1% of matches,
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

*Balance changes vs. the draft: Striker is a 35% chance (not a sure goal) and costs 4; Big Chance costs 3; Long Shot 12%.*

### Hand cards (played before a flip, one per flip)

| Card | Points | Effect |
|---|---|---|
| Scout | 1 | Look under one face-down card (only you see it — including whose team card it is) |
| Swap | 1 | Swap the places of two face-down cards |
| Calm | 2 | If your next card is a Tackle, you keep the ball |
| Press | 2 | The next time you lose the ball, flip one more card straight away |
| Burn | 2 | **Blind:** burns a random face-down team card of the opponent. A 🔥 is left in its place; you don't see which card it was (cost to be reviewed after balancing) |

## 5. Discipline

- Yellow card: recorded. A **second yellow** for the same team in a match is a red.
- Red card: the team gives up one hand card (the human chooses) and its pass streak no longer gives Big Chances.

## 6. Tactics

- A tactic = **4 field cards** + **1–3 hand cards** (by rating) + up to **2 neutral modifiers**.
- Budget: **10 tactic points** + 0–3 by rating (64+ → 1, 72+ → 2, 80+ → 3) + 1 for the home team.
- Hand cards: 1 (rating < 66), 2 (66+), 3 (78+).
- Tactics can be saved; in a career the last tactic is used automatically ("Play (last tactic)").
- Every career starts with an **empty tactic**: the first match asks you to build one. The first time ever, a coach panel explains
  each step (field cards → hand cards → modifiers) while you pick.
- The computer builds its tactic from its rating and the opponent (attacking / balanced / defensive). Rare cards from rating 66,
  epic cards from 76. Background matches do the same.

### Neutral modifiers — the secret choice

Both teams secretly pick up to 2 modifiers; they are revealed together and **added up** (e.g. +2 Goal and −1 Goal = +1 Goal).
Removals are applied first, then additions in a fixed order (Goal, Penalty, Corner, Tackle), so the result does not depend on who
chose what.

| Modifier | Points | Effect |
|---|---|---|
| +1 / +2 Goal | 2 / 4 | Pass cards become Goals |
| −1 / −2 Goal | 2 / 4 | Goals become Pass cards |
| +1 / +2 Corner | 1 / 2 | Pass cards become Corners |
| −1 / −2 Corner | 1 / 2 | Corners become Pass cards |
| +1 Tackle | 1 | A Pass becomes a Tackle |
| +1 Penalty | 3 | A Pass becomes a Penalty |

Limits: Pass ≥ 1, Goal 0–5, Corner 0–4, Tackle 0–6, Penalty 0–3, Miss 0–4, Yellow 0–3. Modifiers apply to both halves.

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
  from weaker clubs. The card collection stays with the manager.

## 9. Money, rewards and the Store

- **Income:** per match (win / draw / loss), cup and European rounds, season TV money (by league and rating) and a prize for the
  final position. Top-five leagues pay ×2; Netherlands, Portugal, Turkey, Belgium ×1.5; Scotland, Greece, Austria, Switzerland,
  Denmark, Czechia, Norway, Sweden ×1.2; the rest ×1. Second divisions ×0.4.
- **Card rewards:** a win gives a card 30% of the time (European knockout wins always); meeting the board target gives a pack;
  every trophy and a league title give a pack with an epic card.
- **Store** (in-game money only, no pay-to-win): packs (3 cards, at least one rare), single cards, selling duplicates (⅓ of the
  price), club upgrades (+2 rating per level, up to 10 levels — more points, more hand cards, smarter play, more income).

## 10. Presentation

- **Tutorial:** a scripted 14-step match (main menu → Tutorial) that shows every rule once.
- **Card pictures:** images in `client/public/cards/` (file name = card code) replace the icons on the cards; the name is written under the picture and the icon sits in a corner badge. Larger cards tilt in 3D, rare and epic cards shine (holo), right-click zooms any card.
- **Card gallery:** every card with its picture, effect, type, cost, rarity and store price (main menu → Card gallery).
- **Rate & review:** opens the game's itch.io page in the browser (main menu, About, after a match).
- **Fullscreen (F11):** in the desktop app the window covers the whole monitor, taskbar included.
- **Animations:** 3D card flips, red shake for dangerous cards, purple glow for steals, gold for goals and penalties, a GOAL! / HALF
  TIME / FULL TIME banner (confetti for your own goals; the computer waits while it is shown), the second-half grid is dealt card by
  card, row/column highlight with the danger count on hover, speed buttons in the match, animated pack opening. Settings →
  *Reduce animations* turns off confetti and shaking.
- **Sound:** every effect and the menu music are generated in code (`client/src/sound.ts`, Web Audio API) — no recordings, nothing
  copyrighted. Crowd ambience during matches, music in the menus, volume sliders in Settings, **M** mutes everywhere.

## 11. Saves and versions

- The game version is in `server/Directory.Build.props` and is shown in the main menu and on the About screen.
- Save files are upgraded with EF Core migrations (`server/DugoutDeck.Api/Data/Migrations`). Before an upgrade the game writes
  a backup copy next to the save (`dugout-deck.db.backup-<date>`). Saves from before migrations existed are recognised and upgraded.
- Every team, league and competition has a stable code in `seed.json`; names, stadiums, colours and ratings are synced from
  seed.json by that code on every start (teams changed in the in-game editor are left alone).
