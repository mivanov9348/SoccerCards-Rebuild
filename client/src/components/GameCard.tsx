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
}

interface FaceProps {
  code: string
  name: string
  description?: string
  size?: 'small' | 'normal' | 'big'
}

/** A face-up card. */
export function CardFace({ code, name, description, size = 'normal' }: FaceProps) {
  const style = cardStyles[code] ?? { icon: '?', theme: 'grey' }
  return (
    <div className={`card-face theme-${style.theme} size-${size}`}>
      <span className="cf-icon">{style.icon}</span>
      <span className="cf-name">{name}</span>
      {description && <span className="cf-desc">{description}</span>}
    </div>
  )
}

/** A face-down card on the table. */
export function CardBack({ onClick, disabled, highlight }: { onClick?: () => void; disabled?: boolean; highlight?: boolean }) {
  return (
    <button className={`card-back-big ${highlight ? 'just-flipped' : ''}`} disabled={disabled} onClick={onClick} aria-label="Face-down card">
      <span>⚽</span>
    </button>
  )
}
