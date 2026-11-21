import { useState } from 'react'
import { Icon } from './Icons'
/** Colour theme of every card face (the icons are in Icons.tsx). */
export const cardStyles: Record<string, { theme: string }> = {
  PASS: { theme: 'green' },
  TACKLE: { theme: 'red' },
  CORNER: { theme: 'blue' },
  FREE_KICK: { theme: 'blue' },
  OFFSIDE: { theme: 'grey' },
  SAVE: { theme: 'purple' },
  MISS: { theme: 'grey' },
  GOAL: { theme: 'gold' },
  PENALTY: { theme: 'gold' },
  YELLOW_CARD: { theme: 'yellow' },
  RED_CARD: { theme: 'red' },
  HALF_TIME: { theme: 'black' },
  FULL_TIME: { theme: 'black' },
  // team (field) cards
  STRIKER: { theme: 'gold' },
  BIG_CHANCE_CARD: { theme: 'gold' },
  LONG_SHOT: { theme: 'blue' },
  PLAYMAKER: { theme: 'green' },
  CAPTAIN: { theme: 'green' },
  WALL: { theme: 'purple' },
  OFFSIDE_TRAP: { theme: 'purple' },
  COUNTER_ATTACK: { theme: 'red' },
  SQUAD_PLAYER: { theme: 'grey' },
  // hand cards
  SCOUT: { theme: 'blue' },
  SWAP: { theme: 'blue' },
  CALM: { theme: 'green' },
  PRESS: { theme: 'red' },
  BURN: { theme: 'red' },
}

export const iconFor = (code: string) => <Icon code={code} />

interface FaceProps {
  code: string
  name: string
  description?: string | null
  size?: 'mini' | 'small' | 'normal' | 'big'
}

/** A face-up card. */
export function CardFace({ code, name, description, size = 'normal' }: FaceProps) {
  const style = cardStyles[code] ?? { theme: 'grey' }
  return (
    <div className={`card-face theme-${style.theme} size-${size}`}>
      <span className="cf-icon">{iconFor(code)}</span>
      <span className="cf-name">{name}</span>
      {description && <span className="cf-desc">{description}</span>}
    </div>
  )
}

/** A face-down card on the table. All backs are grey; my own team cards get a small corner flag in my colours. */
export function CardBack({
  onClick,
  disabled,
  mark,
  peeked,
  selected,
  title,
  aligned,
  onHover,
}: {
  aligned?: boolean
  onHover?: () => void
  onClick?: () => void
  disabled?: boolean
  /** my own card: club colours for the corner flag; dim = already played an own card this turn */
  mark?: { color: string; color2: string; dim: boolean } | null
  peeked?: { code: string; name: string; opponent?: boolean } | null
  selected?: boolean
  title?: string
}) {
  return (
    <button
      className={`card-back-big grey-back ${selected ? 'selected' : ''} ${peeked ? 'peeked' : ''} ${mark?.dim ? 'own-used' : ''} ${aligned ? 'aligned' : ''}`}
      onMouseEnter={onHover}
      disabled={disabled}
      onClick={onClick}
      title={title}
      aria-label={title ?? 'Face-down card'}
    >
      {mark && (
        <i className="own-mark" style={{ background: `linear-gradient(225deg, ${mark.color} 0 30%, ${mark.color2} 30% 42%, ${mark.color} 42% 50%, transparent 50%)` }} />
      )}
      {peeked ? (
        <span className="peek-label">
          <span className="peek-icons"><Icon code="SCOUT" /> {iconFor(peeked.code)}</span>
          <small>{peeked.opponent ? 'Opp: ' : ''}{peeked.name}</small>
        </span>
      ) : (
        <span className="back-ball"><Icon code="GOAL" /></span>
      )}
    </button>
  )
}

export interface PackCard {
  code: string
  name: string
  rarity: string
  description?: string | null
}

/** Pack opening: three face-down cards that flip one by one (click, or all at once). */
export function PackOpening({ cards, onClose, onReveal }: { cards: PackCard[]; onClose: () => void; onReveal?: (c: PackCard) => void }) {
  const [open, setOpen] = useState<boolean[]>(cards.map(() => false))
  const reveal = (i: number) => {
    if (open[i]) return
    setOpen((o) => o.map((v, j) => v || j === i))
    onReveal?.(cards[i])
  }
  const all = open.every(Boolean)
  return (
    <div className="pack-modal" role="dialog" aria-label="Pack opening">
      <h2>🎁 Your pack</h2>
      <div className="pack-cards">
        {cards.map((c, i) => (
          <button key={i} className={`pack-slot rarity-${c.rarity} ${open[i] ? 'open' : ''}`} onClick={() => reveal(i)}>
            <div className="pack-inner">
              <div className="pack-back"><Icon code="GOAL" /></div>
              <div className="pack-front">
                <CardFace code={c.code} name={c.name} description={c.description} size="normal" />
              </div>
            </div>
            <div className="pack-rarity">{open[i] ? c.rarity : 'click to flip'}</div>
          </button>
        ))}
      </div>
      {all ? (
        <button className="btn btn-primary" onClick={onClose}>
          Great!
        </button>
      ) : (
        <button className="btn" onClick={() => cards.forEach((_, i) => setTimeout(() => reveal(i), i * 350))}>
          Flip all
        </button>
      )}
    </div>
  )
}
