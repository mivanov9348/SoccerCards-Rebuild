import { api } from '../api'
import { Screen, useLoad } from '../components/common'
import { CardFace } from '../components/GameCard'

export default function HowToPlay() {
  const { data: cards } = useLoad(() => api.cards(), [])
  const total = (cards ?? []).reduce((sum, c) => sum + c.copiesInDeck, 0)
  return (
    <Screen title="How to play">
      <div className="panel prose">
        <h2>The match</h2>
        <p>
          You play against the computer. {` `}
          <b>24 cards lie face down</b> on the table. The player whose turn it is flips one card — you click the card you want, the
          computer picks one at random. The card decides what happens and <b>who flips next</b>. A new card from the deck takes the
          place of the flipped one.
        </p>
        <ul>
          <li>
            <b>Pass, Corner, Free Kick</b> — you keep the ball: flip again.
          </li>
          <li>
            <b>Tackle, Offside, Save, Miss, Yellow Card, Red Card</b> — the other player flips next.
          </li>
          <li>
            <b>Goal!</b> — you score, then the other player kicks off.
          </li>
          <li>
            <b>Penalty</b> — a mini-game: the shooter picks left, center or right and the keeper picks where to dive. Same side and
            the keeper usually saves; different sides and it is almost always a goal. Then the other player flips.
          </li>
          <li>
            <b>Half Time</b> — the first half ends, the other player starts the second half.
          </li>
          <li>
            <b>Full Time</b> — the match is over, whenever it comes. Even if it is the very first card!
          </li>
        </ul>
        <p>
          In cup and European knockout matches a draw at full time goes to a <b>penalty shootout</b> (5 kicks each, then sudden death)
          — you take every kick for your team and pick the dive for your keeper on every kick of theirs.
        </p>
        <p>It is a game of pure luck — team ratings only matter for the board's expectations in career mode.</p>
        <h2>Career</h2>
        <p>
          Take over a club in the Bulgarian First League. The board sets a target based on how big the club is. A season has 30
          league rounds, the Bulgarian Cup and — if you qualified — a European cup. At the end of the season the board judges you:
          do well and bigger clubs will offer you a job; do badly and you may be sacked. Champions play in the Champions Cup, the
          runners-up and the cup winners in the Europa Cup, third place in the Conference Cup.
        </p>
      </div>
      <div className="panel">
        <h2>The cards ({total} in the deck)</h2>
        <div className="card-gallery">
          {cards?.map((c) => (
            <div key={c.code} className="gallery-item">
              <CardFace code={c.code} name={c.name} description={c.description} size="small" />
              <span className="muted small">× {c.copiesInDeck}</span>
            </div>
          ))}
        </div>
      </div>
    </Screen>
  )
}
