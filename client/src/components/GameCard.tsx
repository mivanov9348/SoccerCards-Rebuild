import { useRef, useState, type MouseEvent } from 'react'
import { Icon } from './Icons'
import { useCardData, zoomCard } from '../cardArt'
import { loadSettings } from '../settings'
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

/**
 * A face-up card. With a picture in public/cards it shows the picture, the name underneath and the icon in a corner
 * badge; without one, the big icon. Rare and epic cards shine (holo), larger cards tilt in 3D under the mouse, and a
 * right-click opens the big view.
 */
export function CardFace({ code, name, description, size = 'normal' }: FaceProps) {
  const { art, info } = useCardData()
  const style = cardStyles[code] ?? { theme: 'grey' }
  const picture = art[code]
  const rarity = info[code]?.rarity
  const holo = rarity === 'Rare' || rarity === 'Epic' || rarity === 'Legendary' ? `holo holo-${rarity.toLowerCase()}` : ''
  const ref = useRef<HTMLDivElement>(null)
  const tilt = size !== 'mini' && !loadSettings().reduceMotion

  const move = (e: MouseEvent) => {
    const el = ref.current
    if (!el || !tilt) return
    const r = el.getBoundingClientRect()
    const x = (e.clientX - r.left) / r.width
    const y = (e.clientY - r.top) / r.height
    el.style.setProperty('--ry', `${(x - 0.5) * 18}deg`)
    el.style.setProperty('--rx', `${(0.5 - y) * 18}deg`)
    el.style.setProperty('--mx', `${x * 100}%`)
    el.style.setProperty('--my', `${y * 100}%`)
  }
  const leave = () => {
    const el = ref.current
    if (!el) return
    el.style.setProperty('--rx', '0deg')
    el.style.setProperty('--ry', '0deg')
  }
  const zoom = (e: MouseEvent) => {
    e.preventDefault()
    zoomCard(code)
  }

  return (
    <div
      ref={ref}
      className={`card-face theme-${style.theme} size-${size} ${picture ? 'has-art' : ''} ${holo} ${tilt ? 'tilt' : ''}`}
      onMouseMove={move}
      onMouseLeave={leave}
      onContextMenu={zoom}
      title={size === 'mini' ? `${name} (right-click to zoom)` : undefined}
    >
      {picture ? (
        <>
          <img className="cf-art" src={picture} alt="" draggable={false} loading="lazy" />
          <span className="cf-badge">{iconFor(code)}</span>
          <span className="cf-name">{name}</span>
          {description && size === 'big' && <span className="cf-desc">{description}</span>}
        </>
      ) : (
        <>
          <span className="cf-icon">{iconFor(code)}</span>
          <span className="cf-name">{name}</span>
          {description && <span className="cf-desc">{description}</span>}
        </>
      )}
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
  const { art } = useCardData()
  const back = art.BACK
  return (
    <button
      style={back ? { backgroundImage: `url(${back})` } : undefined}
      className={`card-back-big grey-back ${back ? 'art-back' : ''} ${selected ? 'selected' : ''} ${peeked ? 'peeked' : ''} ${mark?.dim ? 'own-used' : ''} ${aligned ? 'aligned' : ''}`}
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
      ) : back ? null : (
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
              <PackBack />
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

function PackBack() {
  const { art } = useCardData()
  return art.BACK ? <div className="pack-back art-back" style={{ backgroundImage: `url(${art.BACK})` }} /> : <div className="pack-back"><Icon code="GOAL" /></div>
}
