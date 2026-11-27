import { Link } from 'react-router-dom'
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
        <p>
          Want to look at every card in detail? Open the <Link to="/cards">Card gallery</Link>.
        </p>
        <h2>The match</h2>
        <p>
          You play against the computer. Each half is a <b>5×5 grid of face-down cards</b>, all with the same grey back: 16 neutral
          cards, your 4 team cards, the opponent&apos;s 4 team cards and one special card — <b>Half Time</b> in the first half,{' '}
          <b>Full Time</b> in the second. Only you see where your own cards are (a small flag in your colours in the corner); you never
          know where the opponent&apos;s are — and the computer doesn&apos;t know where yours are. Full Time can never come before Half Time. At half time all cards go face down again and are
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
            there. Goals, penalties, Half Time/Full Time and team cards are not counted — so a 0 is <b>not</b> completely safe: an
            opponent&apos;s Offside Trap (or Half Time) can still hide there.
          </li>
          <li>
            <b>Team cards — whoever flips it gets it:</b> flip your own card and you get its effect. Flip one of the opponent&apos;s and
            you <b>steal</b> it — its effect is yours (a stolen Striker shoots for you). The exception is the <b>Offside Trap</b>: it can&apos;t
            be stolen — whoever flips the opponent&apos;s trap loses the ball. You may flip only <b>one of your own cards per turn</b>, so
            choose when to play each one — the longer it waits, the bigger the risk that it gets stolen.
          </li>
          <li>
            <b>Three passes in a row</b> = a Big Chance shot. <b>Corner / Free kick</b>: you pick an option, the defender picks what to
            guard. <b>Penalties</b>: pick a side, then stop the timing bar in the green zone. You also choose the defence when the computer
            attacks (or switch on automatic mini-games in Settings).
          </li>
          <li>
            <b>Hand cards</b> are played before a flip, one per flip. Second yellow card = red. After a red card the <b>opponent</b> chooses which of
            your hand cards you lose (when the computer is sent off, its hand is shown and you take one away), and your passes no
            longer give Big Chances.
          </li>
          <li>Knockout matches that end level go to a penalty shootout (5 each, then sudden death).</li>
        </ul>
        <h2>Before the match: your tactic</h2>
        <p>
          You have <b>tactic points</b> (10 + 0–3 by club rating + 1 at home). Spend them on <b>4 field cards</b> (Squad Player is
          free), <b>1–3 hand cards</b> (by rating) and up to <b>2 secret neutral modifiers</b> (e.g. +1 Goal turns a Pass into a Goal).
          Both teams&apos; modifiers are revealed together and added up. The home team gets +1 tactic point.
          The rating never changes the odds of a card — it gives more points, more hand cards and a smarter computer.
        </p>
        <p>
          In a career, prepare up to <b>three plans</b> in the <b>Tactics</b> tab between matches. The ★ active plan is loaded before
          every match; you can still change it or pick another plan on the pre-match screen.
        </p>
        <h2>Card tiers</h2>
        <p>
          Every copy of a card is <b>Normal</b>, <b>Rare</b>, <b>Ultra Rare</b> or <b>Legendary</b>. Cards with a chance get +4 / +8 /
          +12% (a Legendary Striker scores 47% instead of 35%); the others get an extra use or a quick play on Ultra Rare and
          Legendary (a Legendary Captain ignores 3 Tackles). The bonus goes with the card — steal a Legendary Striker and it shoots for
          you. Packs of 3, 5 or 10 cards are in the Store; selling is cheap and always possible.
        </p>
        <h2>Money, history and names</h2>
        <p>
          The club pays wages and stadium costs every week, so money no longer piles up: spend it on packs, merge spare copies into a
          higher tier (5 Normal → Rare, 4 Rare → Ultra Rare, 3 Ultra Rare → Legendary) and watch the debt — a big one at the end of
          the season gets you sacked. The <b>Club history</b> tab keeps your trophies, every season, records, statistics and 25
          achievements with small rewards. All names are fictional; <b>Settings → Name packs</b> can rename clubs, leagues and cups
          with a pack file.
        </p>
        <h2>During the match</h2>
        <p>
          The <b>Active effects</b> panel next to the grid says in words what is running for both teams (an effect lights up when it is
          used up), and <b>Cards played</b> lists every card played so far. The match <b>pace</b> (Fast / Normal / Dramatic, buttons
          under the grid or in Settings) sets how long the build-up before important cards is.
        </p>
        <h2>Career</h2>
        <p>
          Manage a club in one of 35 leagues from 25 countries: England, Spain, Germany, Italy, France, the Netherlands, Portugal,
          Turkey, Scotland and Bulgaria with two divisions each (promotion and relegation, a national cup), plus the top divisions of
          Belgium, Greece, Austria, Switzerland, Denmark, Czechia, Croatia, Serbia, Romania, Poland, Ukraine, Cyprus, Hungary, Norway and
          Sweden. Leagues of 12 teams or fewer play each other three times; nobody plays more than two home or two away matches in a
          row. The table colours follow each league: champion, European places, promotion and relegation. Three European cups of 32 teams. Every other match is played in the background with exactly the same
          rules. Win money and cards, buy packs, single cards and club upgrades in the Store, meet the board&apos;s target and get job
          offers from bigger clubs. The cards belong to the club: a new career starts with a draft (pick 1 of 3, ten times), and if you
          move to another club you leave the cards behind and get the new club&apos;s squad cards.
        </p>
      </div>
      {cards && (
        <>
          <div className="panel">
            <h2>Grey (neutral) cards</h2>
            <Gallery cards={cards} kind="Neutral" />
          </div>
          <div className="panel">
            <h2>Field cards (hidden in the grid — whoever flips one gets it)</h2>
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
