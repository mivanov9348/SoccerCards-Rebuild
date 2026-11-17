export const cardIcons: Record<string, string> = {
  PASS: '➜',
  LONG_BALL: '⤴',
  DRIBBLE: '↯',
  CROSS: '⤵',
  SHOT: '⚽',
  COUNTER: '⚡',
  HOLD: '⟲',
  PRESS: '⇶',
  TACKLE: '✂',
  INTERCEPT: '✋',
  BLOCK: '🛡',
  FOUL: '⚠',
  POSITION: '▣',
}

const attackCodes = new Set(['PASS', 'LONG_BALL', 'DRIBBLE', 'CROSS', 'SHOT', 'COUNTER', 'HOLD'])

interface Props {
  code: string
  name: string
  description?: string
  hint?: number
  hintLabel?: string
  showHint?: boolean
  disabled?: boolean
  basic?: boolean
  small?: boolean
  onPlay?: () => void
  onSwap?: () => void
}

/** Една ситуационна карта. Атакуващите са червени, защитните — сини. */
export function GameCard({ code, name, description, hint, hintLabel, showHint, disabled, basic, small, onPlay, onSwap }: Props) {
  const kind = attackCodes.has(code) ? 'attack' : 'defense'
  return (
    <div className={`game-card ${kind} ${disabled ? 'disabled' : ''} ${basic ? 'basic' : ''} ${small ? 'small' : ''}`}>
      <button className="card-face" disabled={disabled || !onPlay} onClick={onPlay} title={description}>
        <span className="card-kind">{basic ? 'Основно действие' : kind === 'attack' ? 'Атака' : 'Защита'}</span>
        <span className="card-icon">{cardIcons[code] ?? '?'}</span>
        <span className="card-name">{name}</span>
        {description && <span className="card-desc">{description}</span>}
        {showHint && hint !== undefined && !disabled && (
          <span className="card-hint">
            {hint}% {hintLabel}
          </span>
        )}
        {disabled && !basic && <span className="card-hint muted">не е възможно сега</span>}
      </button>
      {onSwap && (
        <button className="card-swap" onClick={onSwap} title="Изиграйте основното действие и сменете тази карта с нова">
          ↻ смени
        </button>
      )}
    </div>
  )
}
