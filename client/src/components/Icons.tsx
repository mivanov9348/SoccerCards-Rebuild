/**
 * The game's own icon set (no emoji, no icon fonts): simple 24×24 line drawings that take the colour of the text
 * around them (currentColor) and scale with the font size (1em).
 */
import type { ReactNode } from 'react'

const S = { fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' } as const
const F = { fill: 'currentColor', stroke: 'none' } as const

const ball = (
  <>
    <circle cx="12" cy="12" r="9" {...S} />
    <path d="M12 7.5l3.8 2.8-1.5 4.5H9.7l-1.5-4.5z" {...F} />
    <path d="M12 3v4.5M15.8 10.3l4.5-1.6M14.3 14.8l2.8 3.9M9.7 14.8l-2.8 3.9M8.2 10.3L3.7 8.7" {...S} strokeWidth={1.5} />
  </>
)

const PATHS: Record<string, ReactNode> = {
  // neutral cards
  PASS: <path d="M3 12h15M13 6l6 6-6 6" {...S} />,
  TACKLE: (
    <>
      <path d="M6 3h4v7l7 3c2 .9 3 2.2 3 4v1H6z" {...S} />
      <path d="M8 21v-1M12 21v-1M16 21v-1" {...S} />
    </>
  ),
  CORNER: (
    <>
      <path d="M6 21V3" {...S} />
      <path d="M6 4h11l-3 3.5 3 3.5H6z" {...F} />
      <path d="M3 21h8" {...S} />
    </>
  ),
  FREE_KICK: (
    <>
      <path d="M4 19c2.5-8 9-12 16-11" {...S} />
      <path d="M16.5 4.5L20 8l-3.5 3.5" {...S} />
      <circle cx="4" cy="19" r="2" {...F} />
    </>
  ),
  OFFSIDE: (
    <>
      <path d="M5 21L14 4" {...S} />
      <path d="M14 4l6 3-7.2 2.4z" {...F} />
    </>
  ),
  SAVE: <path d="M8 21v-5l-2.5-4 1.2-1.2L9 13V5.5a1.5 1.5 0 0 1 3 0V11V4.5a1.5 1.5 0 0 1 3 0V11V6.5a1.5 1.5 0 0 1 3 0V15c0 3.5-2 6-5.5 6z" {...S} />,
  MISS: (
    <>
      <path d="M3 20V8h13v12" {...S} />
      <path d="M17 3l4 4M21 3l-4 4" {...S} />
    </>
  ),
  GOAL: ball,
  PENALTY: (
    <>
      <circle cx="12" cy="12" r="9" {...S} />
      <circle cx="12" cy="12" r="4.5" {...S} />
      <circle cx="12" cy="12" r="1.6" {...F} />
    </>
  ),
  YELLOW_CARD: <rect x="7" y="3" width="10" height="17" rx="1.5" {...F} />,
  RED_CARD: <rect x="7" y="3" width="10" height="17" rx="1.5" {...F} />,
  HALF_TIME: (
    <>
      <circle cx="12" cy="12" r="9" {...S} />
      <path d="M12 3a9 9 0 0 1 0 18z" {...F} />
    </>
  ),
  FULL_TIME: (
    <>
      <circle cx="12" cy="12" r="9" {...F} />
      <path d="M8 12.5l3 3 5-6" stroke="#fff" strokeWidth={2.2} fill="none" strokeLinecap="round" strokeLinejoin="round" />
    </>
  ),
  // field cards
  STRIKER: (
    <>
      <circle cx="12" cy="12" r="8" {...S} />
      <path d="M12 1.5v5M12 17.5v5M1.5 12h5M17.5 12h5" {...S} />
      <circle cx="12" cy="12" r="2.2" {...F} />
    </>
  ),
  BIG_CHANCE_CARD: <path d="M12 2l2.6 6.3L21 9l-5 4.4L17.6 20 12 16.6 6.4 20 8 13.4 3 9l6.4-.7z" {...F} />,
  LONG_SHOT: (
    <>
      <path d="M3 20C6 9 13 5 19 7" {...S} strokeDasharray="2.5 3" />
      <circle cx="19.5" cy="7" r="2.5" {...F} />
    </>
  ),
  PLAYMAKER: (
    <>
      <path d="M5 18L12 6l7 9" {...S} strokeDasharray="2.5 2.5" />
      <circle cx="5" cy="18" r="2.4" {...F} />
      <circle cx="12" cy="6" r="2.4" {...F} />
      <circle cx="19" cy="15" r="2.4" {...F} />
    </>
  ),
  CAPTAIN: (
    <>
      <rect x="3" y="7" width="18" height="10" rx="2.5" {...S} />
      <path d="M14.5 10.2a2.6 2.6 0 1 0 0 3.6" {...S} />
    </>
  ),
  WALL: (
    <>
      <rect x="3" y="5" width="18" height="14" rx="1" {...S} />
      <path d="M3 9.7h18M3 14.3h18M9 5v4.7M15 5v4.7M6 9.7v4.6M12 9.7v4.6M18 9.7v4.6M9 14.3V19M15 14.3V19" {...S} strokeWidth={1.5} />
    </>
  ),
  OFFSIDE_TRAP: (
    <>
      <path d="M3 18h18" {...S} />
      <path d="M4 18l2-6 2 6 2-6 2 6 2-6 2 6 2-6 2 6" {...S} />
      <path d="M12 6V3M9 7L7.5 5M15 7l1.5-2" {...S} strokeWidth={1.5} />
    </>
  ),
  COUNTER_ATTACK: <path d="M13.5 2L4 14h7l-1.5 8L19 10h-7z" {...F} />,
  SQUAD_PLAYER: <path d="M8 3L3 6l2 4 2-1v12h10V9l2 1 2-4-5-3c-.8 1.8-2.3 3-4 3S8.8 4.8 8 3z" {...S} />,
  // hand cards
  SCOUT: (
    <>
      <path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12z" {...S} />
      <circle cx="12" cy="12" r="3" {...F} />
    </>
  ),
  SWAP: <path d="M4 8h15l-4-4M20 16H5l4 4" {...S} />,
  CALM: (
    <>
      <path d="M5 19C5 10.5 10.5 5 19 5c0 8.5-5.5 14-14 14z" {...S} />
      <path d="M5 19l8-8" {...S} />
    </>
  ),
  PRESS: <path d="M4 6l6 6-6 6M12 6l6 6-6 6" {...S} />,
  BURN: <path d="M12 2c1 4.2 6 6.2 6 12.2a6 6 0 0 1-12 0c0-3.2 1.9-5.2 3-6.2 0 2 1 3.3 2.2 3.3C11.2 8 10.4 5.2 12 2z" {...F} />,
  // version 1.1 field cards
  WINGER: (
    <>
      <path d="M3 20h18" {...S} />
      <path d="M4 16c4-1 8-6 9-11" {...S} />
      <path d="M10 5l3-1 1 3" {...S} />
      <circle cx="18" cy="15" r="2.5" {...F} />
    </>
  ),
  GOALKEEPER: (
    <>
      <path d="M3 21V6h18v15" {...S} />
      <path d="M8.5 19v-4l-1.5-2.5 1-1 1.8 1.6V8.5a1.1 1.1 0 0 1 2.2 0V12V7.5a1.1 1.1 0 0 1 2.2 0V12V9a1.1 1.1 0 0 1 2.2 0v6.5c0 2-1.4 3.5-3.4 3.5z" {...F} />
    </>
  ),
  TARGET_MAN: (
    <>
      <circle cx="12" cy="7" r="3" {...S} />
      <path d="M6 21c0-4 2.7-7 6-7s6 3 6 7" {...S} />
      <circle cx="18.5" cy="4.5" r="2" {...F} />
    </>
  ),
  SWEEPER: (
    <>
      <path d="M14 3l-5 11" {...S} />
      <path d="M6 13l6 3-2 5c-2 0-5-1.5-6-3.5z" {...F} />
      <path d="M17 14h4M18 18h3" {...S} strokeWidth={1.5} />
    </>
  ),
  SUPER_SUB: (
    <>
      <path d="M7 20V8M3.5 11.5L7 8l3.5 3.5" {...S} />
      <path d="M15 12l1.5 3 3.3.5-2.4 2.3.6 3.2-3-1.6-3 1.6.6-3.2L10.2 15.5l3.3-.5z" {...F} />
    </>
  ),
  DECOY: (
    <>
      <circle cx="12" cy="6.5" r="3" {...S} strokeDasharray="2 2" />
      <path d="M6 21c0-4 2.7-7 6-7s6 3 6 7" {...S} strokeDasharray="2.5 2.5" />
    </>
  ),
  TWELFTH_MAN: (
    <>
      <path d="M4 10v4l3 1 9 4V5L7 9z" {...F} />
      <path d="M19 9.5c1 .8 1 4.2 0 5" {...S} />
      <path d="M7 15l1 5h2.5l-1-4.5" {...S} strokeWidth={1.5} />
    </>
  ),
  // version 1.1 hand cards
  VAR: (
    <>
      <rect x="3" y="4" width="18" height="13" rx="2" {...S} />
      <path d="M9 21h6M12 17v4" {...S} />
      <path d="M8 9l2 4 2-4M14 13l1.5-4 1.5 4M14.5 11.5h2" {...S} strokeWidth={1.5} />
    </>
  ),
  MARKING: (
    <>
      <path d="M12 3l8 3v5c0 5-3.5 8.5-8 10-4.5-1.5-8-5-8-10V6z" {...S} />
      <path d="M8.5 12l2.5 2.5 4.5-5" {...S} />
    </>
  ),
  SUBSTITUTION: (
    <>
      <path d="M8 21V5M4.5 8.5L8 5l3.5 3.5" {...S} />
      <path d="M16 3v16M12.5 15.5L16 19l3.5-3.5" {...S} />
    </>
  ),
  ANALYSIS: (
    <>
      <circle cx="10.5" cy="10.5" r="6.5" {...S} />
      <path d="M15.5 15.5L21 21" {...S} />
      <path d="M8 12.5v-2M10.5 12.5V8M13 12.5v-3" {...S} strokeWidth={1.6} />
    </>
  ),
  PENALTY_TAKER: (
    <>
      <circle cx="16" cy="8" r="5" {...S} />
      <circle cx="16" cy="8" r="1.6" {...F} />
      <path d="M3 20l6-1.5 1.5-3.5-2-1-2 2.5-3 .5z" {...F} />
    </>
  ),
  // modifiers
  MOD: <path d="M12 3.5v9M7.5 8h9M7.5 19h9" {...S} />,
}

/** A card or game icon by card code (e.g. "STRIKER"). Unknown codes show a dot. */
export function Icon({ code, title }: { code: string; title?: string }) {
  const art = PATHS[code] ?? (code.startsWith('MOD_') ? PATHS.MOD : <circle cx="12" cy="12" r="4" {...F} />)
  return (
    <svg className="icon" viewBox="0 0 24 24" width="1em" height="1em" aria-hidden={title ? undefined : true} role={title ? 'img' : undefined}>
      {title && <title>{title}</title>}
      {art}
    </svg>
  )
}

/** The Dugout Deck logo: three fanned cards with a ball, a bolt and a wall. */
export function Logo({ size = 120 }: { size?: number }) {
  const card = (rot: number, x: number, color: string, art: ReactNode) => (
    <g transform={`rotate(${rot} 60 90) translate(${x} 0)`}>
      <rect x="38" y="20" width="44" height="62" rx="6" fill="#f6efdc" stroke={color} strokeWidth="4" />
      <g transform="translate(48 39) scale(1)" color={color}>
        <svg x="0" y="0" width="24" height="24" viewBox="0 0 24 24">
          {art}
        </svg>
      </g>
    </g>
  )
  return (
    <svg viewBox="0 0 120 100" width={size} height={(size * 100) / 120} role="img" aria-label="Dugout Deck">
      {card(-22, -16, '#1d8a4a', PATHS.GOAL)}
      {card(22, 16, '#7a4dd8', PATHS.WALL)}
      {card(0, 0, '#d39b1e', PATHS.COUNTER_ATTACK)}
    </svg>
  )
}
