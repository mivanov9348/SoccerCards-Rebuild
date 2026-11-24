import { useState } from 'react'
import { api, money, TIER_NAMES, type CardInfo } from '../api'
import { Screen, useLoad } from '../components/common'
import { CardFace } from '../components/GameCard'

type Filter = 'All' | 'Neutral' | 'Field' | 'Hand' | 'Modifier'

const FILTERS: [Filter, string, string][] = [
  ['All', 'All cards', ''],
  ['Neutral', 'Grey cards', 'The 16 neutral cards in every half, plus Half Time / Full Time. Hints count only the dangerous ones.'],
  ['Field', 'Field cards', 'Your 4 team cards, hidden in the grid. Whoever flips one gets its effect — the opponent can steal them (except the Offside Trap). One own card per turn.'],
  ['Hand', 'Hand cards', 'Played before a flip, one per flip. You take 1–3 into a match, depending on your club rating.'],
  ['Modifier', 'Modifiers', 'Up to 2 secret choices that change the grey cards. Both teams’ modifiers are revealed and added up at kick-off.'],
]

const KIND_LABEL: Record<string, string> = { Neutral: 'Grey card', Field: 'Field card', Hand: 'Hand card', Modifier: 'Modifier' }
const DANGEROUS = ['TACKLE', 'OFFSIDE', 'SAVE', 'MISS', 'YELLOW_CARD', 'RED_CARD']

const SCARCITY: Record<string, string> = { Common: 'common', Rare: 'uncommon', Epic: 'scarce' }

function Details({ c }: { c: CardInfo }) {
  const [tier, setTier] = useState(0)
  const facts: [string, string][] = [['Type', KIND_LABEL[c.kind] ?? c.kind]]
  if (c.kind === 'Neutral') {
    facts.push(['In the grid', c.copiesInDeck > 0 ? `${c.copiesInDeck} per half (before modifiers)` : 'one per half (Half Time in the 1st, Full Time in the 2nd)'])
    facts.push(['Hints', DANGEROUS.includes(c.code) ? 'counted — it is a dangerous card' : 'not counted'])
  } else {
    facts.push(['Tactic points', String(c.cost)])
    if (c.kind !== 'Modifier') facts.push(['In packs', c.rarity === 'None' ? 'free filler (not in packs)' : SCARCITY[c.rarity] ?? c.rarity])
    if (c.price > 0) facts.push(['Store price', money(c.price)])
  }
  return (
    <div className="gallery-details panel">
      <div>
        <CardFace key={c.code + tier} code={c.code} name={c.name} description={c.description} size="big" tier={tier} />
        {c.bonuses && (
          <div className="tier-preview">
            {TIER_NAMES.map((n, t) => (
              <button key={t} className={`tier-pill tier-${t} ${tier === t ? 'active' : ''}`} onClick={() => setTier(t)}>
                {n}
              </button>
            ))}
          </div>
        )}
      </div>
      <div>
        <h2>{c.name}</h2>
        <p>{c.description}</p>
        <table className="table">
          <tbody>
            {facts.map(([k, v]) => (
              <tr key={k}>
                <td className="left muted">{k}</td>
                <td className="left">{v}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {c.bonuses && (
          <>
            <h3>Tiers</h3>
            <table className="table">
              <tbody>
                {TIER_NAMES.map((n, t) => (
                  <tr key={t}>
                    <td className={`left tier-text-${t}`}>{n}</td>
                    <td className="left">{c.bonuses?.[t] || (t === 0 ? 'the card as described' : 'same effect — a better-looking copy, worth more when sold')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </>
        )}
      </div>
    </div>
  )
}

/** Every card of the game, to browse and read about at any time. */
export default function CardGallery() {
  const { data: cards } = useLoad(() => api.cards(), [])
  const [filter, setFilter] = useState<Filter>('All')
  const [selected, setSelected] = useState<string | null>(null)
  const shown = (cards ?? []).filter((c) => filter === 'All' || c.kind === filter)
  const current = cards?.find((c) => c.code === selected) ?? shown[0]
  const intro = FILTERS.find((f) => f[0] === filter)?.[2]

  return (
    <Screen title="Card gallery">
      <div className="tabs">
        {FILTERS.map(([f, label]) => (
          <button key={f} className={filter === f ? 'active' : ''} onClick={() => setFilter(f)}>
            {label}
          </button>
        ))}
      </div>
      {intro && <p className="muted">{intro}</p>}
      <div className="gallery-layout">
        <div className="card-gallery">
          {shown.map((c) => (
            <button key={c.code} className={`gallery-pick ${current?.code === c.code ? 'selected' : ''}`} onClick={() => setSelected(c.code)} title={c.description}>
              <CardFace code={c.code} name={c.name} size="small" />
              <span className="muted small">{c.kind === 'Neutral' ? (c.copiesInDeck > 0 ? `× ${c.copiesInDeck}` : 'special') : `${c.cost} pt`}</span>
            </button>
          ))}
        </div>
        {current && <Details c={current} />}
      </div>
    </Screen>
  )
}
