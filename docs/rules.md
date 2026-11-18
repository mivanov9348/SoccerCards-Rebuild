# Soccer Cards — rules

Soccer Cards is a single-player football card game against the computer. Every match is played by flipping face-down cards. It is a game of luck; team ratings only matter for the board's targets in career mode.

All numbers below match the code in `server/FootballCards.Api/Game/Rules.cs` and the deck in `server/FootballCards.Api/Data/Seed/seed.json`.

---

## 1. The grid

- Each half is played on its own **5×5 grid of 25 face-down cards**: the same 24 match cards plus one special card —
  **Half Time** in the first-half grid, **Full Time** in the second-half grid. So Full Time can never come before Half Time.
- A random player (you or the computer) kicks off.
- The player whose turn it is flips **one** face-down card. You choose which card to click; the computer picks one at random.
- A flipped card **stays face up** (marked with the team that flipped it) until the half ends.
- When Half Time is flipped, all cards go **face down again and are reshuffled** for the second half.
- There is no clock — only the 1st and 2nd half. A half lasts between 1 and 25 flips (about 13 on average).
- When the match ends, the second-half cards nobody flipped are shown faded.

## 2. The cards

| Card | Copies per half | What happens | Who flips next |
|---|---|---|---|
| Pass | 7 | You keep the ball | **same player** |
| Corner | 2 | Corner kick | **same player** |
| Free Kick | 1 | Free kick | **same player** |
| Tackle | 5 | Ball lost | other player |
| Offside | 1 | Offside | other player |
| Save | 1 | The opposing keeper saves | other player |
| Miss | 2 | Shot goes wide | other player |
| Yellow Card | 1 | A player of the flipping team is booked | other player |
| Red Card | 1 | A player of the flipping team is sent off | other player |
| Goal! | 2 | The flipping team scores | other player (kick-off) |
| Penalty | 1 | Penalty mini-game (see below) | other player |
| Half Time | 1 (1st half only) | The first half ends, the grid is reshuffled | other player starts the 2nd half |
| Full Time | 1 (2nd half only) | **The match ends** | — |

Yellow and red cards are recorded in the match statistics; they do not change the odds (yet).

## 3. Penalty mini-game

When a Penalty card is flipped, the flipping team takes a penalty:

- The shooter picks **left, center or right**; the keeper picks where to dive. You make the pick for your side — as the shooter or as the keeper. The computer picks at random.
- Different sides → goal in **88%** of cases (otherwise the shot goes wide).
- Same side → the keeper saves in **75%** of cases (**70%** if both pick the center).
- After the penalty the other player flips.

## 4. Knockout matches

Cup and European matches cannot end in a draw. If the score is level when Full Time is flipped, there is a **penalty shootout**:

- Home team shoots first, then the teams alternate; 5 kicks each, then sudden death.
- Each kick is a penalty mini-game: you shoot for your team and dive for your keeper on every kick of the opponent.
- The shootout ends as soon as one team cannot be caught.

## 5. Career

- Manage a club in the Bulgarian **First League** (16 teams, home and away — 30 rounds).
- A season also includes the **Bulgarian Cup** (16 teams, knockout from the Round of 16) and, if you qualified, a **European cup** (16 teams, knockout).
- A season has 34–38 weeks; each week is either a league round or a cup/European round.
- **Board target**, based on the club's rating rank in the league:
  - top 2 clubs: Win the title
  - 3rd–5th: Finish in the top 4
  - 6th–10th: Finish in the top half (8th or better)
  - the rest: Avoid the drop (13th or better)
- **European places**: 1st → Champions Cup; 2nd and the cup winners → Europa Cup; 3rd → Conference Cup. In the first season the places go to the highest-rated clubs.
- **End-of-season review**: score = target position − actual position, +2 for winning the cup, +1 per European knockout win.
  - ≥ 3: the board is delighted; ≥ 0: satisfied; −1 to −3: one more chance;
  - ≤ −4, or last place: **sacked**.
- **Job offers**: a good season (or a high reputation) brings offers from bigger clubs; a sacked manager gets offers from smaller clubs. Accept one, or stay where you are.
- **Reputation** (0–100) rises with success.
- All matches you do not play yourself are played in the background by the computer with exactly the same cards and rules (two grids, Half Time, Full Time, penalties), so the league table is fair.

## 6. Adding teams and leagues

- In the game: **Team & league editor**.
- Or in `server/FootballCards.Api/Data/Seed/seed.json` — on start-up the server adds missing teams and leagues (by name) without overwriting changes made in the editor.
- A league marked as playable with at least 4 teams (an even number is best) can be chosen for a career. Teams without a league are used as European opponents.
- The cards per half are defined in `seed.json` (the `cards` section) and synced to the database on every start-up. The engine always adds exactly one Half Time card to the first-half grid and one Full Time card to the second-half grid.
