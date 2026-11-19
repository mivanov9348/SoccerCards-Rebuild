import { api, type CardInfo } from '../api'
import { Screen, useLoad } from '../components/common'
import { CardFace } from '../components/GameCard'

function Gallery({ cards, kind }: { cards: CardInfo[]; kind: string }) {
  return (
    <div className="card-gallery">
      {cards
        .filter((c) => c.kind === kind)
        .map((c) => (
          <div key={c.code} className="gallery-item">
            <CardFace code={c.code} name={c.name} description={c.description} size="small" />
            <span className="muted small">
              {kind === 'Neutral' ? (c.copiesInDeck > 0 ? `× ${c.copiesInDeck} per half` : 'special') : `${c.cost} pt · ${c.rarity}`}
            </span>
          </div>
        ))}
    </div>
  )
}

export default function HowToPlay() {
  const { data: cards } = useLoad(() => api.cards(), [])
  return (
    <Screen title="How to play">
      <div className="panel prose">
        <h2>The match</h2>
        <p>
          You play against the computer. Each half is a <b>5×5 grid of face-down cards</b>: 16 grey (neutral) cards, 4 cards with
          your club colours, 4 with the opponent&apos;s colours and one special card — <b>Half Time</b> in the first half,{' '}
          <b>Full Time</b> in the second. Full Time can never come before Half Time. At half time all cards go face down again and are
          reshuffled. There is no clock.
        </p>
        <ul>
          <li>
            The player on turn flips one card; it stays face up until the half ends. You keep flipping while you get{' '}
            <b>Pass / Corner / Free Kick</b> results; a dangerous card (Tackle, Offside, Save, Miss, cards), a goal or a penalty gives the
            turn to the opponent.
          </li>
          <li>
            <b>Hints:</b> the numbers next to each row and under each column show how many dangerous grey cards are still face down
            there. A 0 means every grey card in that row/column is safe. Goals, penalties, Half Time/Full Time and team cards are not
            counted.
          </li>
          <li>
            <b>Team cards:</b> flip your own coloured card and you get its effect. Flip the opponent&apos;s and it <b>burns</b> — it
            works like a Pass for you (but resets your pass streak) and they lose it. Beware the Offside Trap!
          </li>
          <li>
            <b>Three passes in a row</b> = a Big Chance shot. <b>Corner / Free kick</b>: you pick an option, the defender picks what to
            guard. <b>Penalties</b>: pick a side, then stop the timing bar in the green zone. You also choose the defence when the computer
            attacks (or switch on automatic mini-games in Settings).
          </li>
          <li>
            <b>Hand cards</b> are played before a flip, one per flip. Second yellow card = red; a red card costs you a hand card and your
            passes no longer give Big Chances.
          </li>
          <li>Knockout matches that end level go to a penalty shootout (5 each, then sudden death).</li>
        </ul>
        <h2>Before the match: your tactic</h2>
        <p>
          You have <b>tactic points</b> (10 + 0–3 by club rating + 1 at home). Spend them on <b>4 field cards</b> (Squad Player is
          free), <b>1–3 hand cards</b> (by rating) and up to <b>2 secret neutral modifiers</b> (e.g. +1 Goal turns a Pass into a Goal).
          Both teams&apos; modifiers are revealed together and added up. Conditions (home, rain, wind, derby) change the grey cards too.
          The rating never changes the odds of a card — it gives more points, more hand cards and a smarter computer.
        </p>
        <h2>Career</h2>
        <p>
          Manage a club in England, Spain, Germany, Italy, France or Bulgaria — each with two divisions, promotion and relegation, a
          national cup and three European cups of 32 teams. Every other match is played in the background with exactly the same
          rules. Win money and cards, buy packs, single cards and club upgrades in the Store, meet the board&apos;s target and get job
          offers from bigger clubs. Your card collection stays with you when you move.
        </p>
      </div>
      {cards && (
        <>
          <div className="panel">
            <h2>Grey (neutral) cards</h2>
            <Gallery cards={cards} kind="Neutral" />
          </div>
          <div className="panel">
            <h2>Field cards (your colours in the grid)</h2>
            <Gallery cards={cards} kind="Field" />
          </div>
          <div className="panel">
            <h2>Hand cards</h2>
            <Gallery cards={cards} kind="Hand" />
          </div>
          <div className="panel">
            <h2>Neutral modifiers (secret)</h2>
            <Gallery cards={cards} kind="Modifier" />
          </div>
        </>
      )}
    </Screen>
  )
}
