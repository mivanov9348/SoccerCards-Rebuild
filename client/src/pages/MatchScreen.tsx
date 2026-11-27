import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, type Decision, type MatchTeam, type MatchView, type Side, type Tactic } from '../api'
import { Badge, ErrorBox, useLoad } from '../components/common'
import { CardBack, CardFace, iconFor, TierBadge } from '../components/GameCard'
import { Icon } from '../components/Icons'
import { ReviewButton } from '../components/ReviewButton'
import { play, playEvent } from '../sound'
import { TacticBuilder } from '../components/TacticBuilder'
import { PACE_TIMES, PACES, useSettings } from '../settings'
import { ActiveEffects, PlayedCards } from '../components/MatchEffects'

type Targeting = { card: string; free?: boolean; first?: number } | null

function ScoreTeam({ t, align, turn }: { t: MatchTeam; align: 'left' | 'right'; turn: boolean }) {
  const chips: string[] = []
  if (t.passStreak > 0) chips.push(`➜ passes ${t.passStreak}/3`)
  // the effects themselves are in the "Active effects" panel; the scoreboard only counts them
  const effects = [t.captain, t.wall, t.calm, t.press, t.counterFlips > 0, t.goalkeeper, t.targetMan, t.sweeper, t.twelfthMan, t.var, t.penaltyTaker, t.marking].filter(Boolean).length
  if (effects > 0) chips.push(`✦ ${effects} effect${effects === 1 ? '' : 's'}`)
  return (
    <div className={`score-team ${align} ${turn ? 'has-turn' : ''}`}>
      <Badge name={t.name} primary={t.primaryColor} secondary={t.secondaryColor} size={46} />
      <div>
        <div className="score-name">
          {t.name} {t.isHuman ? <span className="you-tag">YOU</span> : <span className="cpu-tag">CPU · {t.aiLevel}</span>}
        </div>
        <div className="muted small">
          {t.stats.yellowCards > 0 && <span>🟨×{t.stats.yellowCards} </span>}
          {t.stats.redCards > 0 && <span>🟥×{t.stats.redCards} </span>}
          {turn && <span className="turn-tag">● to flip</span>}
        </div>
        {chips.length > 0 && (
          <div className="effect-chips">
            {chips.map((c) => (
              <span key={c}>{c}</span>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}

/** A slider that runs back and forth; stop it in the green zone for a better shot (or save). */
function TimingBar({ onStop, disabled }: { onStop: (t: number) => void; disabled: boolean }) {
  const [pos, setPos] = useState(0)
  const posRef = useRef(0)
  useEffect(() => {
    let raf = 0
    const start = performance.now()
    const tick = (now: number) => {
      const p = ((now - start) / 900) % 2
      const v = p < 1 ? p : 2 - p
      posRef.current = v
      setPos(v)
      raf = requestAnimationFrame(tick)
    }
    raf = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(raf)
  }, [])
  return (
    <div className="timing">
      <div className="timing-bar">
        <div className="tz red left" />
        <div className="tz green" />
        <div className="tz red right" />
        <div className="timing-marker" style={{ left: `${pos * 100}%` }} />
      </div>
      <button className="btn btn-primary" disabled={disabled} onClick={() => onStop(posRef.current)}>
        NOW!
      </button>
    </div>
  )
}

function DecisionPanel({ m, d, busy, onDecide }: { m: MatchView; d: Decision; busy: boolean; onDecide: (choice: number | null, timing: number | null, auto?: boolean) => void }) {
  const [choice, setChoice] = useState<number | null>(null)
  const titles: Record<string, string> = {
    Corner: 'Corner kick',
    FreeKick: 'Free kick',
    BigChance: 'BIG CHANCE',
    Penalty: 'Penalty',
    Shootout: 'Penalty shootout',
    Discard: 'Red card — take away a hand card',
  }
  const attacking = d.role === 'attack'
  const homeScore = m.shootout.filter((k) => k.side === 'Home' && k.scored).length
  const awayScore = m.shootout.filter((k) => k.side === 'Away' && k.scored).length
  return (
    <div className="decision-box">
      <h2>
        {titles[d.kind]} {d.kind === 'Shootout' && `— ${homeScore} : ${awayScore}`}
      </h2>
      <p className="muted">
        {d.kind === 'Discard'
          ? `${d.attackerName} are down to ten men. Their hand is revealed — choose which card they lose.`
          : attacking
            ? `Your ${d.source.toLowerCase()} — choose your option.`
            : `${d.attackerName}: ${d.source.toLowerCase()} — choose how to defend.`}
      </p>
      {d.kind === 'Shootout' && (
        <div className="shootout-dots">
          {(['Home', 'Away'] as const).map((side) => (
            <div key={side}>
              <span className="muted small">{side === 'Home' ? m.home.shortName : m.away.shortName}</span>
              {m.shootout
                .filter((k) => k.side === side)
                .map((k, i) => (
                  <span key={i} className={`dot ${k.scored ? 'in' : 'out'}`} />
                ))}
            </div>
          ))}
        </div>
      )}
      {d.kind === 'Discard' && d.cards ? (
        <div className="discard-cards">
          {d.cards.map((code, i) => (
            <button key={code + i} className="discard-pick" disabled={busy} onClick={() => onDecide(i, null)} title={'Take away ' + d.options[i]}>
              <CardFace code={code} name={d.options[i]} size="normal" tier={d.tiers?.[i]} />
              <span className="btn btn-primary small">Take it</span>
            </button>
          ))}
        </div>
      ) : (
      <div className="decision-options">
        {d.options.map((o, i) => (
          <button
            key={o + i}
            className={`btn ${choice === i ? 'btn-primary' : ''}`}
            disabled={busy}
            onClick={() => (d.needsTiming ? setChoice(i) : onDecide(i, null))}
          >
            {o}
          </button>
        ))}
      </div>
      )}
      {d.needsTiming && choice !== null && (
        <>
          <p className="small">{attacking ? 'Time your shot — stop the marker in the green zone!' : 'Time your dive — stop the marker in the green zone!'}</p>
          <TimingBar disabled={busy} onStop={(t) => onDecide(choice, t)} />
        </>
      )}
      {d.kind !== 'Discard' && (
        <button className="btn btn-ghost small" disabled={busy} onClick={() => onDecide(null, null, true)}>
          Decide automatically
        </button>
      )}
    </div>
  )
}

interface Overlay {
  kind: 'goal' | 'against' | 'whistle' | 'result'
  title: string
  sub: string
  colors: string[]
  key: number
}

const DANGEROUS = ['TACKLE', 'OFFSIDE', 'SAVE', 'MISS', 'YELLOW_CARD', 'RED_CARD']

/** Big "GOAL!" / "HALF TIME" banner over the grid, with confetti for your own goals. */
function MatchOverlay({ o, calm }: { o: Overlay; calm: boolean }) {
  const pieces = o.kind === 'goal' && !calm ? Array.from({ length: 46 }, (_, i) => i) : []
  return (
    <div className={`match-overlay ov-${o.kind}`} key={o.key} aria-live="polite">
      {pieces.map((i) => (
        <i
          key={i}
          className="confetti"
          style={{
            left: `${(i * 37) % 100}%`,
            background: o.colors[i % 2] ?? '#f2c14e',
            animationDelay: `${(i % 9) * 0.07}s`,
            animationDuration: `${1.4 + (i % 5) * 0.18}s`,
          }}
        />
      ))}
      <div className="ov-title">{o.title}</div>
      <div className="ov-sub">{o.sub}</div>
    </div>
  )
}

/** Cards that get a build-up before they turn over ("theatre"). */
const BIG_CARDS = ['GOAL', 'PENALTY', 'RED_CARD']

const TARGET_HINT: Record<string, string> = {
  SCOUT: 'Scout: pick a card to look under',
  MARKING: 'Marking: pick one of your own face-down cards (with your flag) to protect',
  SUBSTITUTION: 'Substitution: pick one of your own team cards that is already face up',
  ANALYSIS: 'Analysis: click any card in the row you want to analyse',
}

function StatsTable({ m }: { m: MatchView }) {
  const rows: [string, number, number][] = [
    ['Cards flipped', m.home.stats.flips, m.away.stats.flips],
    ['Passes', m.home.stats.passes, m.away.stats.passes],
    ['Big chances', m.home.stats.bigChances, m.away.stats.bigChances],
    ['Corners', m.home.stats.corners, m.away.stats.corners],
    ['Free kicks', m.home.stats.freeKicks, m.away.stats.freeKicks],
    ['Penalties', m.home.stats.penalties, m.away.stats.penalties],
    ['Saves', m.home.stats.saves, m.away.stats.saves],
    ['Cards stolen', m.home.stats.cardsStolen, m.away.stats.cardsStolen],
    ['Cards burned', m.home.stats.cardsBurned, m.away.stats.cardsBurned],
    ['Yellow cards', m.home.stats.yellowCards, m.away.stats.yellowCards],
    ['Red cards', m.home.stats.redCards, m.away.stats.redCards],
  ]
  return (
    <table className="table stats">
      <tbody>
        {rows.map(([label, h, a]) => (
          <tr key={label}>
            <td>{h}</td>
            <td className="muted">{label}</td>
            <td>{a}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function PreMatchReveal({ m, onGo }: { m: MatchView; onGo: () => void }) {
  return (
    <div className="reveal-box panel">
      <h2>The grids are dealt</h2>
      <div className="two-col">
        <div>
          <h3>Grey cards in each half</h3>
          <ul className="composition">
            {m.composition.map((c) => (
              <li key={c.card}>
                {iconFor(c.card)} {c.name} × {c.count}
              </li>
            ))}
            <li>⏸ Half Time (1st half) / ⏹ Full Time (2nd half)</li>
          </ul>
        </div>
        <div>
          <h3>Secret modifiers revealed</h3>
          <p>
            <b>{m.home.name}:</b> {m.home.modifiers?.length ? m.home.modifiers.join(', ') : 'none'}
          </p>
          <p>
            <b>{m.away.name}:</b> {m.away.modifiers?.length ? m.away.modifiers.join(', ') : 'none'}
          </p>
          <h3>Team cards</h3>
          <p className="muted">
            Each team has {m.home.fieldCount} cards hidden in the grid — all backs are grey. Only you see where yours are (small corner
            flag). Whoever flips a team card gets its effect, so play yours before the opponent steals them — one per turn. Yours:{' '}
            {(m.humanSide === 'Away' ? m.away : m.home).fieldCards?.join(', ')}
          </p>
          <ul className="conditions">
            {m.conditions.map((c) => (
              <li key={c.code}>{c.label}</li>
            ))}
          </ul>
        </div>
      </div>
      <button className="btn btn-primary" onClick={onGo}>
        Let&apos;s play ▶
      </button>
    </div>
  )
}

export default function MatchScreen() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [settings, updateSettings] = useSettings()
  const { data: server, error: loadError, setData } = useLoad(() => api.match(Number(id)), [id])
  // What is on screen. Usually the latest match from the server; during a card's build-up ("lift") the flipped card is
  // still shown face down and raised, and the score, commentary and sounds wait until it turns over.
  const [m, setView] = useState<MatchView | null>(null)
  const [lift, setLift] = useState<{ cell: number; big: boolean; mine: boolean; side: Side } | null>(null)
  const viewRef = useRef<MatchView | null>(null)
  const pace = settings.reduceMotion ? 'Fast' : settings.pace
  const times = PACE_TIMES[pace] ?? PACE_TIMES.Normal
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [targeting, setTargeting] = useState<Targeting>(null)
  const [revealSeen, setRevealSeen] = useState(false)
  const aiInFlight = useRef(false)
  const seenEventId = useRef<number | null>(null)
  const seenScore = useRef<[number, number] | null>(null)
  const [toast, setToast] = useState<{ text: string; good: boolean } | null>(null)
  const [overlay, setOverlay] = useState<Overlay | null>(null)
  const [hover, setHover] = useState<number | null>(null)
  // half-time break: the finished first half stays on screen until the player starts the second half
  const [halfBreak, setHalfBreak] = useState(false)
  const [whistleKey, setWhistleKey] = useState(0)
  const [flying, setFlying] = useState<string | null>(null)

  useEffect(() => {
    if (!server) return
    const prev = viewRef.current
    viewRef.current = server
    const f = server.lastFlip
    const human = server.humanSide ?? 'Home'
    const newFlip =
      prev != null && f != null && prev.id === server.id && prev.half === server.half && server.phase !== 'Setup' &&
      (!prev.lastFlip || prev.lastFlip.cell !== f.cell || prev.lastFlip.card !== f.card) && prev.table[f.cell] != null && !prev.table[f.cell].faceUp
    if (!newFlip || !f || !prev) {
      setLift(null)
      setView(server)
      return
    }
    const mine = f.side === human
    const big = f.owner != null || BIG_CARDS.includes(f.card) || server.home.goals + server.away.goals > prev.home.goals + prev.away.goals ||
      server.home.stats.redCards + server.away.stats.redCards > prev.home.stats.redCards + prev.away.stats.redCards
    const wait = mine ? (big ? times.liftMineBig : 0) : big ? times.liftAiBig : times.liftAi
    if (wait <= 0) {
      setLift(null)
      setView(server)
      return
    }
    // the build-up: the new grid, except the flipped card is still face down; nothing else changes yet
    setView({
      ...server,
      table: server.table.map((c) => (c.index === f.cell ? prev.table[f.cell] : c)),
      lastFlip: prev.lastFlip, events: prev.events, rowHints: prev.rowHints, colHints: prev.colHints, cardsLeft: prev.cardsLeft,
      home: { ...server.home, goals: prev.home.goals }, away: { ...server.away, goals: prev.away.goals },
      phase: 'Play', role: null, decision: null,
    })
    setLift({ cell: f.cell, big, mine, side: f.side })
    if (big) play('lift')
    const t = window.setTimeout(() => {
      setLift(null)
      setView(server)
    }, wait)
    return () => window.clearTimeout(t)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [server])

  // New events since the last update: sounds, the goal / half-time overlays and the steal messages
  useEffect(() => {
    if (!m) return
    const lastId = m.events.length ? m.events[m.events.length - 1].id : 0
    const prev = seenEventId.current
    const prevScore = seenScore.current
    seenEventId.current = lastId
    seenScore.current = [m.home.goals, m.away.goals]
    if (prev == null || prevScore == null) return
    const fresh = m.events.filter((e) => e.id > prev)
    if (fresh.length === 0) return
    const human = m.humanSide ?? 'Home'
    const timers: number[] = []

    // a goal = the score went up (penalty-shootout kicks don't count)
    const goalSide: Side | null = m.home.goals > prevScore[0] ? 'Home' : m.away.goals > prevScore[1] ? 'Away' : null
    const goal = goalSide ? { side: goalSide, id: lastId } : null
    if (m.lastFlip && fresh.some((e) => e.card === m.lastFlip?.card)) play('flip')
    fresh
      .filter((e) => !(goal && e.type === 'goal'))
      .slice(-3)
      .forEach((e, i) => timers.push(window.setTimeout(() => playEvent(e.type, e.card, e.side === human), 140 + i * 260)))
    if (goal) timers.push(window.setTimeout(() => play(goal.side === human ? 'goal' : 'goalAgainst'), 150))
    const half = fresh.find((e) => e.type === 'whistle' && e.card === 'HALF_TIME')
    const full = fresh.find((e) => e.type === 'whistle' && e.card === 'FULL_TIME')
    const scorer = goal ? (goal.side === 'Home' ? m.home : m.away) : null
    if (goal && scorer) setOverlay({ kind: goal.side === human ? 'goal' : 'against', title: 'GOAL!', sub: scorer.name, colors: [scorer.primaryColor, scorer.secondaryColor], key: goal.id })
    else if (half && m.phase !== 'Finished') setHalfBreak(true)
    else if (full) setOverlay({ kind: 'whistle', title: 'FULL TIME', sub: `${m.home.name} ${m.home.goals} : ${m.away.goals} ${m.away.name}`, colors: [], key: full.id })
    else {
      // the outcome of a mini-game (corner, free kick, Big Chance, penalty): SAVED! / WIDE! / CLEARED! / KEPT IT!
      const pick = fresh.findIndex((e) => e.type === 'info' && / — /.test(e.text) && /: /.test(e.text))
      const after = pick >= 0 ? fresh.slice(pick + 1).find((e) => ['save', 'miss', 'lost', 'play'].includes(e.type)) : undefined
      const penaltyMiss = fresh.find((e) => e.type === 'miss' && e.text.includes('NO GOAL'))
      const outcome = penaltyMiss ? penaltyMiss : after
      if (outcome) {
        const title = outcome.type === 'save' ? 'SAVED!' : outcome.type === 'miss' ? (penaltyMiss ? 'NO GOAL!' : 'WIDE!') : outcome.type === 'lost' ? 'CLEARED!' : 'KEPT IT!'
        setOverlay({ kind: 'result', title, sub: '', colors: [], key: outcome.id })
      }
    }
    if (half || full) setWhistleKey((k) => k + 1)

    const steal = [...fresh].reverse().find((e) => e.type === 'steal')
    const takeAway = fresh.find((e) => e.type === 'info' && e.text.includes(' take away '))
    if (takeAway) setToast({ text: '🟥 ' + takeAway.text, good: takeAway.side !== human })
    if (steal) {
      const name = m.table.find((c) => c.card === steal.card && c.stolen)?.name ?? 'card'
      setToast(steal.side === human ? { text: `🕵 You stole the opponent's ${name}!`, good: true } : { text: `😱 The opponent stole your ${name}!`, good: false })
    }
    return () => timers.forEach((t) => window.clearTimeout(t))
  }, [m])

  // The banner and the steal message hide by themselves. These timers live on their own: a new update of the match
  // (e.g. you flip right after a goal) must not cancel them — a banner that never hides would stop the computer for good.
  useEffect(() => {
    if (!overlay) return
    const t = window.setTimeout(() => setOverlay(null), overlay.kind === 'result' ? 1100 : overlay.kind === 'whistle' ? 1900 : 2300)
    return () => window.clearTimeout(t)
  }, [overlay])
  useEffect(() => {
    if (!toast) return
    const t = window.setTimeout(() => setToast(null), 3500)
    return () => window.clearTimeout(t)
  }, [toast])

  const run = async (fn: () => Promise<MatchView>) => {
    setBusy(true)
    setError(null)
    try {
      setData(await fn())
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const started = m != null && m.phase !== 'Setup'
  const freshStart = m != null && started && m.events.length <= 1
  const showReveal = freshStart && !revealSeen

  // The computer acts after a short pause (its flips, and decisions only it has to make).
  // (it waits while a GOAL! / HALF TIME banner is on screen)
  const computerToAct = m != null && !lift && !showReveal && !overlay && !halfBreak && m.role === 'wait' && (m.phase === 'Play' || m.phase === 'Decision')
  useEffect(() => {
    if (!m || !computerToAct || aiInFlight.current) return
    const timer = setTimeout(async () => {
      aiInFlight.current = true
      try {
        setData(await api.aiStep(m.id))
      } catch (e) {
        setError((e as Error).message)
        // reload the match so the computer tries again instead of waiting forever
        window.setTimeout(() => void api.match(m.id).then(setData).catch(() => undefined), 1500)
      } finally {
        aiInFlight.current = false
      }
    }, times.think)
    return () => clearTimeout(timer)
  }, [m, computerToAct, times.think, setData])

  // Automatic mini-games (setting)
  const autoDecide = m != null && settings.autoMiniGames && m.phase === 'Decision' && (m.role === 'attack' || m.role === 'defend')
  useEffect(() => {
    if (!m || !autoDecide) return
    const timer = setTimeout(() => void run(() => api.decide(m.id, null, null, true)), 600)
    return () => clearTimeout(timer)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [m, autoDecide])

  if (loadError && !m)
    return (
      <div className="screen">
        <ErrorBox error={loadError} />
        <Link to="/" className="btn">
          Main menu
        </Link>
      </div>
    )
  if (!m) return <div className="screen loading">Loading match…</div>

  if (m.phase === 'Setup')
    return (
      <div className="screen">
        <TacticBuilder m={m} onStart={async (t: Tactic, saveAs?: string) => setData(await api.startMatch(m.id, t, saveAs))} />
      </div>
    )

  const human = m.humanSide ?? 'Home'
  const me = human === 'Home' ? m.home : m.away
  const finished = m.phase === 'Finished'
  const myGoals = me.goals
  const theirGoals = human === 'Home' ? m.away.goals : m.home.goals
  const myPens = human === 'Home' ? m.homePenalties : m.awayPenalties
  const theirPens = human === 'Home' ? m.awayPenalties : m.homePenalties
  const won = myGoals > theirGoals || (myGoals === theirGoals && myPens != null && myPens > (theirPens ?? 0))
  const lost = myGoals < theirGoals || (myGoals === theirGoals && myPens != null && myPens < (theirPens ?? 0))
  const myTurn = m.phase === 'Play' && m.role === 'flip' && !halfBreak
  const revealAll = finished || halfBreak
  const grid = halfBreak && m.firstHalfTable ? m.firstHalfTable : m.table
  const last = m.lastFlip
  const events = [...m.events].reverse()
  const teamOf = (s: string | null) => (s === 'Home' ? m.home : s === 'Away' ? m.away : null)

  const clickCell = (index: number) => {
    if (!myTurn || busy) return
    if (!targeting) return void run(() => api.flip(m.id, index))
    if (targeting.card === 'SWAP') {
      if (targeting.first == null) return setTargeting({ ...targeting, first: index })
      const first = targeting.first
      setTargeting(null)
      return void run(() => api.playHand(m.id, 'SWAP', first, index))
    }
    const card = targeting.card
    setTargeting(null)
    void run(() => api.playHand(m.id, card, index))
  }

  const playHand = (code: string) => {
    setError(null)
    if (['SCOUT', 'SWAP', 'MARKING', 'SUBSTITUTION', 'ANALYSIS'].includes(code)) setTargeting({ card: code })
    else void run(() => api.playHand(m.id, code))
  }

  const simulate = () => {
    if (!confirm('Let the computer play out the rest of the match?')) return
    void run(() => api.simulateMatch(m.id))
  }

  return (
    <div className="screen match-screen">
      <div className={`scoreboard ${whistleKey && !settings.reduceMotion ? 'whistle-shake' : ''}`} key={`sb-${whistleKey}`}>
        <ScoreTeam t={m.home} align="left" turn={m.phase === 'Play' && m.turn === 'Home'} />
        <div className="score-center">
          <div className="score score-pop" key={`${m.home.goals}-${m.away.goals}`}>
            {m.home.goals} : {m.away.goals}
          </div>
          {m.homePenalties != null && (
            <div className="muted small">
              pens {m.homePenalties}:{m.awayPenalties}
            </div>
          )}
          <div className="clock">
            {finished ? 'Full time' : m.half === 1 ? '1st half' : '2nd half'}
            {m.difficulty && m.difficulty !== 'Normal' ? ` · ${m.difficulty}` : ''}
          </div>
          <div className="muted small">
            {m.competition} · {m.stadium}
          </div>
        </div>
        <ScoreTeam t={m.away} align="right" turn={m.phase === 'Play' && m.turn === 'Away'} />
      </div>

      {showReveal ? (
        <PreMatchReveal m={m} onGo={() => setRevealSeen(true)} />
      ) : (
        <div className="match-grid">
          <div className="match-main">
            <div className={`turn-banner ${myTurn ? 'mine' : ''}`}>
              {finished
                ? 'The match is over'
                : halfBreak
                  ? 'Half time — press Start second half when you are ready'
                : lift
                  ? lift.mine ? '…' : '⏳ ' + (lift.side === 'Home' ? m.home.name : m.away.name) + ' turn over a card…'
                : targeting
                  ? targeting.card === 'SWAP'
                    ? targeting.first == null ? '⇄ Swap: pick the first face-down card' : '⇄ Swap: pick the second card'
                    : TARGET_HINT[targeting.card] ?? 'Pick a card'
                  : m.phase === 'Decision'
                    ? m.role === 'wait'
                      ? m.decision?.kind === 'Discard' ? '🟥 Red card! The opponent is choosing which of your hand cards to take…' : '⏳ The computer is deciding…'
                      : m.role === 'discard' ? '🟥 Red card for the opponent — take away one of their hand cards!' : '⚠ Your decision!'
                    : myTurn
                      ? '👉 Your turn — flip a card (or play a hand card first)'
                      : `⏳ ${m.turn === 'Home' ? m.home.name : m.away.name} (computer) is thinking…`}
            </div>

            {toast && <div className={`steal-toast ${toast.good ? 'good' : 'bad'}`}>{toast.text}</div>}
            {myTurn && m.ownCardUsed && <div className="own-used-note">You have played one of your own cards this turn — the others wait until your next turn.</div>}

            {m.phase === 'Decision' && m.decision && m.role !== 'wait' && (!settings.autoMiniGames || m.role === 'discard') && (
              <DecisionPanel m={m} d={m.decision} busy={busy} onDecide={(c, t, auto) => run(() => api.decide(m.id, c, t, auto))} />
            )}

            {halfBreak && (
              <div className="half-break panel">
                <h2>Half time — {m.home.name} {m.home.goals} : {m.away.goals} {m.away.name}</h2>
                <p>
                  Below is the whole first half, revealed — now you can see where every card was. In the second half all cards are reshuffled
                  face down, both teams get their team cards back, and <b>Full Time</b> is hidden somewhere in the new grid.{' '}
                  <b>{m.turn === 'Home' ? m.home.name : m.away.name}</b> kick off.
                </p>
                <button className="btn btn-primary" onClick={() => setHalfBreak(false)}>
                  Start second half ▶
                </button>
              </div>
            )}

            <div className={`table-area ${overlay ? 'has-overlay' : ''} ${halfBreak ? 'first-half-view' : ''}`}>
              {overlay && <MatchOverlay o={overlay} calm={settings.reduceMotion} />}
              <div className="grid-wrap">
                <div className={`flip-grid half-${halfBreak ? 1 : m.half} ${settings.reduceMotion ? '' : 'deal-in'}`} key={`half-${halfBreak ? 1 : m.half}`} onMouseLeave={() => setHover(null)}>
                  {grid.map((c) => {
                    const owner = teamOf(c.owner)
                    if (c.faceUp && c.card) {
                      const by = teamOf(c.flippedBy)!
                      return (
                        <div
                          key={c.index}
                          onClick={targeting?.card === 'SUBSTITUTION' && c.owner === human && !halfBreak ? () => clickCell(c.index) : undefined}
                          className={`grid-face ${targeting?.card === 'SUBSTITUTION' && c.owner === human && !halfBreak ? 'pickable' : ''} ${last?.cell === c.index ? `just-flipped ${settings.reduceMotion ? '' : 'anim'} ${DANGEROUS.includes(c.card) || (c.stolen && c.card === 'OFFSIDE_TRAP') ? 'danger' : ''} ${c.stolen && c.card !== 'OFFSIDE_TRAP' ? 'stolen-flash' : ''} ${c.card === 'GOAL' || c.card === 'PENALTY' ? 'golden fx-sparks' : ''} ${c.card === 'TACKLE' ? 'fx-slash' : ''} ${c.card === 'SAVE' || c.card === 'MISS' ? 'fx-bounce' : ''} ${c.card === 'YELLOW_CARD' || c.card === 'RED_CARD' ? 'fx-pop' : ''} ${c.stolen && c.card !== 'OFFSIDE_TRAP' ? `fx-fly-${c.flippedBy === 'Home' ? 'left' : 'right'}` : ''}` : ''}`}
                          style={{ borderColor: by.primaryColor }}
                          title={`${c.name} — flipped by ${by.name}${c.stolen ? ` (${owner?.name}'s card — ${c.card === 'OFFSIDE_TRAP' ? 'offside trap!' : 'stolen'})` : ''}`}
                        >
                          <CardFace code={c.card} name={c.name ?? ''} size="mini" tier={c.tier} />
                          <span className="grid-owner" style={{ background: by.primaryColor }}>
                            {c.stolen ? <Icon code={c.card === 'OFFSIDE_TRAP' ? 'OFFSIDE_TRAP' : 'SWAP'} /> : null}
                            {by.shortName}
                          </span>
                        </div>
                      )
                    }
                    if (c.burned)
                      return (
                        <div key={c.index} className="grid-face burned-slot" title={c.mine ? `Your ${c.name} — burned` : 'A burned opponent card'}>
                          {revealAll && c.card ? <CardFace code={c.card} name={c.name ?? ''} size="mini" /> : <span className="burn-mark"><Icon code="BURN" /></span>}
                        </div>
                      )
                    if (revealAll && c.card)
                      return (
                        <div key={c.index} className="grid-face unflipped" style={owner ? { borderColor: owner.primaryColor } : undefined}>
                          <CardFace code={c.card} name={c.name ?? ''} size="mini" />
                        </div>
                      )
                    const aligned = hover != null && hover !== c.index && (Math.floor(hover / 5) === Math.floor(c.index / 5) || hover % 5 === c.index % 5)
                    return (
                      <CardBack
                        key={c.index}
                        aligned={aligned}
                        lifting={lift?.cell === c.index ? (lift.big ? 'big' : 'small') : undefined}
                        marked={c.marked}
                        onHover={() => setHover(c.index)}
                        mark={c.mine ? { color: me.primaryColor, color2: me.secondaryColor, dim: m.ownCardUsed } : null}
                        peeked={c.peeked && c.card ? { code: c.card, name: c.name ?? '', opponent: c.owner != null && c.owner !== human } : null}
                        selected={targeting?.first === c.index}
                        disabled={!myTurn || busy}
                        title={c.mine ? `Your card — keep the mouse on it to see which${m.ownCardUsed ? ' (wait for your next turn)' : ''}` : 'Face-down card'}
                        ownName={c.mine ? c.name ?? undefined : undefined}
                        onClick={() => clickCell(c.index)}
                      />
                    )
                  })}
                </div>
                <div className="row-hints" style={halfBreak ? { visibility: 'hidden' } : undefined}>
                  {m.rowHints.map((h, i) => (
                    <span key={i} className={`${h === 0 ? 'safe' : ''} ${hover != null && Math.floor(hover / 5) === i ? 'hot' : ''}`} title="Dangerous grey cards still face down in this row">
                      {h}
                    </span>
                  ))}
                </div>
                <div className="col-hints" style={halfBreak ? { visibility: 'hidden' } : undefined}>
                  {m.colHints.map((h, i) => (
                    <span key={i} className={`${h === 0 ? 'safe' : ''} ${hover != null && hover % 5 === i ? 'hot' : ''}`} title="Dangerous grey cards still face down in this column">
                      {h}
                    </span>
                  ))}
                </div>
              </div>
              <div className="reveal">
                {hover != null && myTurn && !m.table[hover]?.faceUp && (
                  <span className="risk-note">
                    Row {Math.floor(hover / 5) + 1}: {m.rowHints[Math.floor(hover / 5)]} dangerous · Column {(hover % 5) + 1}: {m.colHints[hover % 5]} dangerous
                  </span>
                )}
                <span className="muted small">Last card</span>
                {last ? (
                  <>
                    <CardFace key={`${m.events.length}`} code={last.card} name={last.name} description={last.description} size="big" tier={last.tier} />
                    <span className="reveal-by">
                      flipped by {teamOf(last.side)?.isHuman ? 'you' : teamOf(last.side)?.name}
                      {last.owner && last.owner !== last.side ? (last.card === 'OFFSIDE_TRAP' ? ' — offside trap!' : ' — stolen!') : ''}
                    </span>
                  </>
                ) : (
                  <div className="reveal-empty">No card flipped yet</div>
                )}
                <span className="muted small">{m.cardsLeft} cards face down</span>
              </div>
            </div>

            {!finished && !halfBreak && (
              <div className="hand-panel">
                <span className="muted small">Your hand {m.handPlayed ? '(one card per flip — played)' : ''}</span>
                <div className="hand-cards">
                  {m.freeScouts > 0 && (
                    <button className="hand-card free" disabled={!myTurn || busy} onClick={() => setTargeting({ card: 'SCOUT', free: true })} title="Free scout from your Playmaker">
                      <Icon code="SCOUT" /> Free Scout ×{m.freeScouts}
                    </button>
                  )}
                  {m.hand.map((h, i) => (
                    <button
                      key={h.code + i}
                      className={`hand-card ${flying === h.code + i ? 'flying' : ''}`}
                      disabled={!myTurn || busy || m.handPlayed || (h.code === 'BURN' && m.opponentCardsLeft === 0)}
                      onClick={() => {
                        if (!settings.reduceMotion) {
                          setFlying(h.code + i)
                          window.setTimeout(() => setFlying(null), 450)
                        }
                        playHand(h.code)
                      }}
                      title={(h.description ?? '') + (h.bonus ? ` — ${h.bonus}` : '')}
                    >
                      {iconFor(h.code)} {h.name} <TierBadge tier={h.tier} />
                    </button>
                  ))}
                  {m.hand.length === 0 && m.freeScouts === 0 && <span className="muted small">No hand cards left.</span>}
                  {targeting && (
                    <button className="btn btn-ghost small" onClick={() => setTargeting(null)}>
                      Cancel
                    </button>
                  )}
                </div>
                <span className="muted small">Opponent holds {m.opponentHandCount} hand card(s)</span>
              </div>
            )}

            <ErrorBox error={error} />

            {finished && (
              <div className={`result-box ${won ? 'won' : lost ? 'lost' : ''}`}>
                <h2>{won ? '🏆 You win!' : lost ? 'You lose' : 'Draw'}</h2>
                <StatsTable m={m} />
                <div className="center">
                  <ReviewButton variant="link" />
                </div>
                <div className="row-buttons">
                  {m.careerId ? (
                    <button className="btn btn-primary" onClick={() => navigate(`/career/${m.careerId}`)}>
                      Back to career ▶
                    </button>
                  ) : (
                    <>
                      <button className="btn btn-primary" onClick={() => navigate('/friendly')}>
                        New friendly
                      </button>
                      <button className="btn" onClick={() => navigate('/')}>
                        Main menu
                      </button>
                    </>
                  )}
                </div>
              </div>
            )}

            {!finished && (
              <div className="row-buttons secondary">
                <span className="speed-control" title="Match pace: how long the build-up before each card is">
                  {PACES.map((p) => (
                    <button key={p} className={`btn btn-ghost small ${settings.pace === p ? 'active' : ''}`} onClick={() => updateSettings({ pace: p })}>
                      {p}
                    </button>
                  ))}
                </span>
                <button className="btn btn-ghost" disabled={busy} onClick={simulate}>
                  ⏩ Simulate to the end
                </button>
                <button className="btn btn-ghost" onClick={() => navigate(m.careerId ? `/career/${m.careerId}` : '/')}>
                  Pause (the match is saved)
                </button>
              </div>
            )}
          </div>

          <aside className="match-side">
            <ActiveEffects m={m} />
            <PlayedCards m={m} />
            <div className="commentary panel">
            <h2>Commentary</h2>
            <ol className="events">
              {events.map((e, i) => (
                <li key={e.id} className={`ev ev-${e.type} ${e.side === human ? 'ours' : e.side ? 'theirs' : ''} ${i === 0 ? 'fresh' : ''}`}>
                  <span className="ev-min">{e.half === 1 ? '1st' : '2nd'}</span>
                  <span className="ev-text">{e.text}</span>
                </li>
              ))}
            </ol>
            </div>
          </aside>
        </div>
      )}
    </div>
  )
}
