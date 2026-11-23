import { useEffect, useRef, useState } from 'react'
import type { MatchTeam, MatchView } from '../api'
import { Icon } from './Icons'

/** Every running effect a team can have, with the full text shown in the "Active effects" panel. */
const EFFECTS: { key: keyof MatchTeam; code: string; name: string; text: string }[] = [
  { key: 'captain', code: 'CAPTAIN', name: 'Captain', text: 'the next Tackle is ignored' },
  { key: 'wall', code: 'WALL', name: 'Wall', text: 'the next goal against them this half becomes a Save' },
  { key: 'calm', code: 'CALM', name: 'Calm', text: 'if the next card is a Tackle, they keep the ball' },
  { key: 'press', code: 'PRESS', name: 'Press', text: 'when they next lose the ball, they flip one more card' },
  { key: 'goalkeeper', code: 'GOALKEEPER', name: 'Goalkeeper', text: 'the next shot or penalty against them is 20% less likely to go in' },
  { key: 'targetMan', code: 'TARGET_MAN', name: 'Target Man', text: 'the next corner or free kick cross is 20% more likely to go in' },
  { key: 'sweeper', code: 'SWEEPER', name: 'Sweeper', text: 'the next Offside or Save is ignored' },
  { key: 'twelfthMan', code: 'TWELFTH_MAN', name: '12th Man', text: 'the next dangerous card is ignored' },
  { key: 'var', code: 'VAR', name: 'VAR', text: "the opponent's next goal is checked — 40% chance it is disallowed" },
  { key: 'penaltyTaker', code: 'PENALTY_TAKER', name: 'Penalty Taker', text: 'the next penalty is 10% more likely to go in' },
  { key: 'marking', code: 'MARKING', name: 'Marking', text: 'one of your cards is marked — if the opponent flips it, they lose the ball' },
]

interface Row {
  id: string
  side: 'Home' | 'Away'
  code: string
  name: string
  text: string
  spent?: boolean
}

function activeRows(t: MatchTeam, side: 'Home' | 'Away'): Row[] {
  const rows: Row[] = EFFECTS.filter((e) => Boolean(t[e.key])).map((e) => ({ id: `${side}-${e.code}`, side, code: e.code, name: e.name, text: e.text }))
  if (t.counterFlips > 0)
    rows.push({ id: `${side}-COUNTER`, side, code: 'COUNTER_ATTACK', name: 'Counter Attack', text: `${t.counterFlips} more card${t.counterFlips === 1 ? '' : 's'} — dangerous ones don't count` })
  if (t.passStreak > 0)
    rows.push({ id: `${side}-PASS`, side, code: 'PASS', name: `Passes ${t.passStreak}/3`, text: t.passStreak >= 2 ? 'one more Pass = Big Chance!' : 'three in a row = Big Chance' })
  return rows
}

/**
 * "Active effects": what is running for each team, in words. When an effect is used up (e.g. the Captain saves you from a
 * Tackle) it stays for a moment, lit up and crossed out, so you see that it worked.
 */
export function ActiveEffects({ m }: { m: MatchView }) {
  const now = [...activeRows(m.home, 'Home'), ...activeRows(m.away, 'Away')]
  const prev = useRef<Row[]>([])
  const prevHalf = useRef<number | null>(null)
  const [spent, setSpent] = useState<Row[]>([])
  const key = now.map((r) => r.id + r.name).join('|')

  useEffect(() => {
    const ids = new Set(now.map((r) => r.id))
    // pass streaks just reset — that is not an effect being used up
    const gone = prev.current.filter((r) => !ids.has(r.id) && !r.id.endsWith('-PASS') && m.half === (prevHalf.current ?? m.half))
    prev.current = now
    prevHalf.current = m.half
    if (gone.length === 0) return
    setSpent((s) => [...s.filter((x) => !gone.some((g) => g.id === x.id)), ...gone.map((g) => ({ ...g, spent: true }))])
    const t = window.setTimeout(() => setSpent((s) => s.filter((x) => !gone.some((g) => g.id === x.id))), 1800)
    return () => window.clearTimeout(t)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key])

  const rows = [...now, ...spent.filter((s) => !now.some((r) => r.id === s.id))]
  const teams = { Home: m.home, Away: m.away }
  return (
    <section className="panel effects-panel">
      <h2>Active effects</h2>
      {rows.length === 0 ? (
        <p className="muted small">Nothing running. Effects of team and hand cards show up here.</p>
      ) : (
        <ul className="effects-list">
          {rows.map((r) => {
            const t = teams[r.side]
            return (
              <li key={r.id} className={`effect-row ${r.spent ? 'spent' : ''}`} style={{ borderLeftColor: t.primaryColor }}>
                <span className="effect-icon">
                  <Icon code={r.code} />
                </span>
                <span>
                  <b>{t.isHuman ? 'You' : t.shortName}</b> · <b>{r.name}</b>
                  {r.spent ? ' — used up' : `: ${r.text}`}
                </span>
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}

const HOW: Record<string, string> = { hand: 'hand', own: 'own', stolen: 'stolen', trap: 'trap', decoy: 'decoy' }

/** The cards each team has played so far (hand cards and team cards that took effect). */
export function PlayedCards({ m }: { m: MatchView }) {
  const [open, setOpen] = useState(true)
  return (
    <section className="panel played-panel">
      <h2 className="clickable" onClick={() => setOpen((o) => !o)}>
        Cards played {open ? '▾' : '▸'}
      </h2>
      {open && (
        <div className="played-cols">
          {[m.home, m.away].map((t) => (
            <div key={t.id}>
              <div className="played-team" style={{ borderBottomColor: t.primaryColor }}>
                {t.isHuman ? 'You' : t.shortName}
              </div>
              {(t.played ?? []).length === 0 && <span className="muted small">—</span>}
              <ul>
                {(t.played ?? []).map((p, i) => (
                  <li key={i} className={`played-${p.how}`} title={`${p.half === 1 ? '1st' : '2nd'} half`}>
                    <Icon code={p.code} /> {p.name} <small className="muted">{HOW[p.how]}</small>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      )}
    </section>
  )
}
