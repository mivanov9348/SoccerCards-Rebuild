import { useRef, useState, type MouseEvent } from 'react'
import { createPortal } from 'react-dom'
import { Icon } from './Icons'
import { useCardData, zoomCard } from '../cardArt'
import { loadSettings } from '../settings'
import { play } from '../sound'
import { TIER_NAMES, TIER_SHORT, type TieredCard } from '../api'
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
  WINGER: { theme: 'blue' },
  GOALKEEPER: { theme: 'purple' },
  TARGET_MAN: { theme: 'blue' },
  SWEEPER: { theme: 'purple' },
  SUPER_SUB: { theme: 'gold' },
  DECOY: { theme: 'grey' },
  TWELFTH_MAN: { theme: 'green' },
  // hand cards
  SCOUT: { theme: 'blue' },
  SWAP: { theme: 'blue' },
  CALM: { theme: 'green' },
  PRESS: { theme: 'red' },
  BURN: { theme: 'red' },
  VAR: { theme: 'black' },
  MARKING: { theme: 'purple' },
  SUBSTITUTION: { theme: 'green' },
  ANALYSIS: { theme: 'blue' },
  PENALTY_TAKER: { theme: 'gold' },
}

export const iconFor = (code: string) => <Icon code={code} />

interface FaceProps {
  code: string
  name: string
  description?: string | null
  size?: 'mini' | 'small' | 'normal' | 'big'
  /** tier of this copy (1.2): 0 Normal, 1 Rare (silver), 2 Ultra Rare (gold, holo), 3 Legendary (animated, holo) */
  tier?: number
}

/** A small tier label: R / UR / L (nothing for Normal). */
export function TierBadge({ tier, long }: { tier?: number; long?: boolean }) {
  if (!tier) return null
  return (
    <span className={`tier-badge tier-${tier}`} title={TIER_NAMES[tier]}>
      {long ? TIER_NAMES[tier] : TIER_SHORT[tier]}
    </span>
  )
}

/**
 * A face-up card. With a picture in public/cards it shows the picture, the name underneath and the icon in a corner
 * badge; without one, the big icon. The tier of the copy gives the frame (silver / gold / legendary) and Ultra Rare and
 * Legendary copies shine (holo). Larger cards tilt in 3D under the mouse, and a right-click opens the big view.
 */
export function CardFace({ code, name, description, size = 'normal', tier = 0 }: FaceProps) {
  const { art } = useCardData()
  const style = cardStyles[code] ?? { theme: 'grey' }
  const picture = art[code]
  const holo = tier >= 2 ? `holo holo-${tier === 3 ? 'legendary' : 'epic'}` : ''
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
      className={`card-face theme-${style.theme} size-${size} ${picture ? 'has-art' : ''} ${holo} ${tier ? `tier-frame tier-${tier}` : ''} ${tilt ? 'tilt' : ''}`}
      onMouseMove={move}
      onMouseLeave={leave}
      onContextMenu={zoom}
      title={size === 'mini' ? `${name} (right-click to zoom)` : undefined}
    >
      {tier > 0 && <TierBadge tier={tier} long={size === 'big'} />}
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
  marked,
  lifting,
}: {
  marked?: boolean
  /** the card is raised before it turns over (big = an important card: longer, with a glow) */
  lifting?: 'big' | 'small'
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
      className={`card-back-big grey-back ${back ? 'art-back' : ''} ${selected ? 'selected' : ''} ${peeked ? 'peeked' : ''} ${mark?.dim ? 'own-used' : ''} ${aligned ? 'aligned' : ''} ${lifting ? 'lifting lift-' + lifting : ''}`}
      onMouseEnter={onHover}
      disabled={disabled}
      onClick={onClick}
      title={title}
      aria-label={title ?? 'Face-down card'}
    >
      {marked && (
        <span className="marked-badge" title="Marked: cannot be stolen">
          <Icon code="MARKING" />
        </span>
      )}
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

export type PackCard = TieredCard

/**
 * Pack opening (1.2): the sealed pack shakes until you tear it open; the cards come out face down and their backs glow
 * silver, gold or rainbow for Rare, Ultra Rare and Legendary copies before you flip them. Click a card to flip it, or
 * "Reveal all". Packs of 10 are laid out in two rows.
 */
export function PackOpening({ cards, onClose, title = 'Your pack' }: { cards: PackCard[]; onClose: () => void; title?: string }) {
  const [stage, setStage] = useState<'sealed' | 'tearing' | 'open'>('sealed')
  const [open, setOpen] = useState<boolean[]>(cards.map(() => false))
  const calm = loadSettings().reduceMotion
  const tear = () => {
    if (stage !== 'sealed') return
    play('pack')
    setStage('tearing')
    window.setTimeout(() => setStage('open'), calm ? 0 : 650)
  }
  const reveal = (i: number) => {
    if (stage !== 'open' || open[i]) return
    setOpen((o) => o.map((v, j) => v || j === i))
    const t = cards[i].tier
    if (t >= 2) play('packEpic')
    else play(t === 1 ? 'keep' : 'flip')
  }
  const all = open.every(Boolean)
  const best = Math.max(...cards.map((c) => c.tier))
  // rendered on the page itself: a panel with a backdrop filter would otherwise trap the full-screen overlay
  return createPortal(
    <div className="pack-modal" role="dialog" aria-label="Pack opening">
      <h2>🎁 {title}</h2>
      {stage !== 'open' ? (
        <button className={`sealed-pack ${stage === 'tearing' ? 'tearing' : ''} glow-${best}`} onClick={tear} title="Tear it open">
          <span className="sealed-top" />
          <span className="sealed-body">
            <Icon code="GOAL" />
            <b>DUGOUT DECK</b>
            <small>{cards.length} cards</small>
          </span>
          <span className="sealed-hint">click to tear open</span>
        </button>
      ) : (
        <div className={`pack-cards size-${cards.length}`}>
          {cards.map((c, i) => (
            <button key={i} className={`pack-slot tier-hint-${c.tier} ${open[i] ? 'open' : ''}`} style={{ animationDelay: `${i * 70}ms` }} onClick={() => reveal(i)}>
              <div className="pack-inner">
                <PackBack />
                <div className="pack-front">
                  <CardFace code={c.code} name={c.name} description={c.description} size="normal" tier={c.tier} />
                </div>
              </div>
              <div className={`pack-rarity tier-text-${c.tier}`}>
                {open[i] ? (
                  <>
                    {c.tierName}
                    {c.bonus && <small> · {c.bonus}</small>}
                  </>
                ) : (
                  'click to flip'
                )}
              </div>
            </button>
          ))}
        </div>
      )}
      {stage === 'open' &&
        (all ? (
          <button className="btn btn-primary" onClick={onClose}>
            Great!
          </button>
        ) : (
          <button className="btn" onClick={() => cards.forEach((_, i) => window.setTimeout(() => reveal(i), i * 260))}>
            Reveal all
          </button>
        ))}
    </div>,
    document.body,
  )
}

function PackBack() {
  const { art } = useCardData()
  return art.BACK ? <div className="pack-back art-back" style={{ backgroundImage: `url(${art.BACK})` }} /> : <div className="pack-back"><Icon code="GOAL" /></div>
}

/** A tiny picture of the card (its art, or the icon) for tactic and collection tiles. */
export function CardThumb({ code }: { code: string }) {
  const { art } = useCardData()
  return art[code] ? <img className="card-thumb" src={art[code]} alt="" draggable={false} /> : iconFor(code)
}
