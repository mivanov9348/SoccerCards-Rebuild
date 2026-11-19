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

/** A face-down card on the table: grey back for neutral cards, club colours for team cards. */
export function CardBack({
  onClick,
  disabled,
  color,
  color2,
  peeked,
  selected,
  title,
}: {
  onClick?: () => void
  disabled?: boolean
  color?: string
  color2?: string
  peeked?: { code: string; name: string } | null
  selected?: boolean
  title?: string
}) {
  const style = color ? { background: `repeating-linear-gradient(45deg, ${color} 0 7px, ${color2 ?? color} 7px 10px)` } : undefined
  return (
    <button
      className={`card-back-big ${color ? 'team-back' : 'grey-back'} ${selected ? 'selected' : ''} ${peeked ? 'peeked' : ''}`}
      style={style}
      disabled={disabled}
      onClick={onClick}
      title={title}
      aria-label={title ?? 'Face-down card'}
    >
      {peeked ? (
        <span className="peek-label">
          👁 {iconFor(peeked.code)}
          <small>{peeked.name}</small>
        </span>
      ) : (
        <span className="back-ball">⚽</span>
      )}
    </button>
  )
}
