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
}: {
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
      className={`card-back-big grey-back ${selected ? 'selected' : ''} ${peeked ? 'peeked' : ''} ${mark?.dim ? 'own-used' : ''}`}
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
