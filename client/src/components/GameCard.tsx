import { useState } from 'react'
/** Look of every card face: icon and color theme. */
export const cardStyles: Record<string, { icon: string; theme: string }> = {
  PASS: { icon: '➜', theme: 'green' },
  TACKLE: { icon: '✂', theme: 'red' },
  CORNER: { icon: '⚑', theme: 'blue' },
  FREE_KICK: { icon: '↷', theme: 'blue' },
  OFFSIDE: { icon: '⚐', theme: 'grey' },
  SAVE: { icon: '🧤', theme: 'purple' },
  MISS: { icon: '✗', theme: 'grey' },
  GOAL: { icon: '⚽', theme: 'gold' },
  PENALTY: { icon: '◎', theme: 'gold' },
  YELLOW_CARD: { icon: '▮', theme: 'yellow' },
  RED_CARD: { icon: '▮', theme: 'red' },
  HALF_TIME: { icon: '⏸', theme: 'black' },
  FULL_TIME: { icon: '⏹', theme: 'black' },
  // team (field) cards
  STRIKER: { icon: '🎯', theme: 'gold' },
  BIG_CHANCE_CARD: { icon: '💥', theme: 'gold' },
  LONG_SHOT: { icon: '🚀', theme: 'blue' },
  PLAYMAKER: { icon: '🧠', theme: 'green' },
  CAPTAIN: { icon: '©', theme: 'green' },
  WALL: { icon: '🧱', theme: 'purple' },
  OFFSIDE_TRAP: { icon: '🪤', theme: 'purple' },
  COUNTER_ATTACK: { icon: '⚡', theme: 'red' },
  SQUAD_PLAYER: { icon: '👟', theme: 'grey' },
  // hand cards
  SCOUT: { icon: '👁', theme: 'blue' },
  SWAP: { icon: '⇄', theme: 'blue' },
  CALM: { icon: '☯', theme: 'green' },
  PRESS: { icon: '⏩', theme: 'red' },
  BURN: { icon: '🔥', theme: 'red' },
}

export const iconFor = (code: string) => cardStyles[code]?.icon ?? (code.startsWith('MOD_') ? '±' : '?')

interface FaceProps {
  code: string
  name: string
  description?: string | null
  size?: 'mini' | 'small' | 'normal' | 'big'
}

/** A face-up card. */
export function CardFace({ code, name, description, size = 'normal' }: FaceProps) {
  const style = cardStyles[code] ?? { icon: iconFor(code), theme: 'grey' }
  return (
    <div className={`card-face theme-${style.theme} size-${size}`}>
      <span className="cf-icon">{style.icon}</span>
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
          👁 {iconFor(peeked.code)}
          <small>{peeked.opponent ? 'Opp: ' : ''}{peeked.name}</small>
        </span>
      ) : (
        <span className="back-ball">⚽</span>
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
              <div className="pack-back">⚽</div>
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
