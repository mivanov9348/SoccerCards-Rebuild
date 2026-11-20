import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, type Decision, type MatchTeam, type MatchView, type Side, type Tactic } from '../api'
import { Badge, ErrorBox, useLoad } from '../components/common'
import { CardBack, CardFace, iconFor } from '../components/GameCard'
import { play, playEvent } from '../sound'
import { TacticBuilder } from '../components/TacticBuilder'
import { useSettings } from '../settings'

type Targeting = { card: string; free?: boolean; first?: number } | null

function ScoreTeam({ t, align, turn }: { t: MatchTeam; align: 'left' | 'right'; turn: boolean }) {
  const chips: string[] = []
  if (t.passStreak > 0) chips.push(`➜ passes ${t.passStreak}/3`)
  if (t.captain) chips.push('© captain')
  if (t.wall) chips.push('🧱 wall')
  if (t.calm) chips.push('☯ calm')
  if (t.press) chips.push('⏩ press')
  if (t.counterFlips > 0) chips.push(`⚡ counter ×${t.counterFlips}`)
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
    Discard: 'Red card — give up a hand card',
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
          ? 'You are down to ten men. Choose which hand card you lose.'
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
  kind: 'goal' | 'against' | 'whistle'
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

const SPEEDS: [number, string][] = [
  [1600, '🐢'],
  [900, '1×'],
  [400, '2×'],
  [100, '⚡'],
]

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
  const { data: m, error: loadError, setData } = useLoad(() => api.match(Number(id)), [id])
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
    else if (half) setOverlay({ kind: 'whistle', title: 'HALF TIME', sub: `${m.home.goals} : ${m.away.goals} — new grid, ${m.turn === 'Home' ? m.home.name : m.away.name} start`, colors: [], key: half.id })
    else if (full) setOverlay({ kind: 'whistle', title: 'FULL TIME', sub: `${m.home.name} ${m.home.goals} : ${m.away.goals} ${m.away.name}`, colors: [], key: full.id })
    if (goal || half || full) timers.push(window.setTimeout(() => setOverlay(null), goal ? 2300 : 1900))

    const steal = [...fresh].reverse().find((e) => e.type === 'steal')
    if (steal) {
      const name = m.table.find((c) => c.card === steal.card && c.stolen)?.name ?? 'card'
      setToast(steal.side === human ? { text: `🕵 You stole the opponent's ${name}!`, good: true } : { text: `😱 The opponent stole your ${name}!`, good: false })
      timers.push(window.setTimeout(() => setToast(null), 3500))
    }
    return () => timers.forEach((t) => window.clearTimeout(t))
  }, [m])

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
  const computerToAct = m != null && !showReveal && !overlay && m.role === 'wait' && (m.phase === 'Play' || m.phase === 'Decision')
  useEffect(() => {
    if (!m || !computerToAct || aiInFlight.current) return
    const timer = setTimeout(async () => {
      aiInFlight.current = true
      try {
        setData(await api.aiStep(m.id))
      } catch (e) {
        setError((e as Error).message)
      } finally {
        aiInFlight.current = false
      }
    }, settings.aiDelay)
    return () => clearTimeout(timer)
  }, [m, computerToAct, settings.aiDelay, setData])

  // Automatic mini-games (setting)
  const autoDecide = m != null && settings.autoMiniGames && m.phase === 'Decision' && (m.role === 'attack' || m.role === 'defend')
  useEffect(() => {
    if (!m || !autoDecide) return
    const timer = setTimeout(() => void run(() => api.decide(m.id, null, null, true)), 600)
    return () => clearTimeout(timer)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [m, autoDecide])

  if (loadError)
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
  const myTurn = m.phase === 'Play' && m.role === 'flip'
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
    if (code === 'SCOUT' || code === 'SWAP') setTargeting({ card: code })
    else void run(() => api.playHand(m.id, code))
  }

  const simulate = () => {
    if (!confirm('Let the computer play out the rest of the match?')) return
    void run(() => api.simulateMatch(m.id))
  }

  return (
    <div className="screen match-screen">
      <div className="scoreboard">
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
          <div className="clock">{finished ? 'Full time' : m.half === 1 ? '1st half' : '2nd half'}</div>
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
                : targeting
                  ? targeting.card === 'SWAP'
                    ? targeting.first == null ? '⇄ Swap: pick the first face-down card' : '⇄ Swap: pick the second card'
                    : '👁 Scout: pick a card to look under'
                  : m.phase === 'Decision'
                    ? m.role === 'wait' ? '⏳ The computer is deciding…' : '⚠ Your decision!'
                    : myTurn
                      ? '👉 Your turn — flip a card (or play a hand card first)'
                      : `⏳ ${m.turn === 'Home' ? m.home.name : m.away.name} (computer) is thinking…`}
            </div>

            {toast && <div className={`steal-toast ${toast.good ? 'good' : 'bad'}`}>{toast.text}</div>}
            {myTurn && m.ownCardUsed && <div className="own-used-note">You have played one of your own cards this turn — the others wait until your next turn.</div>}

            {m.phase === 'Decision' && m.decision && m.role !== 'wait' && !settings.autoMiniGames && (
              <DecisionPanel m={m} d={m.decision} busy={busy} onDecide={(c, t, auto) => run(() => api.decide(m.id, c, t, auto))} />
            )}

            <div className={`table-area ${overlay ? 'has-overlay' : ''}`}>
              {overlay && <MatchOverlay o={overlay} calm={settings.reduceMotion} />}
              <div className="grid-wrap">
                <div className={`flip-grid half-${m.half} ${settings.reduceMotion ? '' : 'deal-in'}`} key={`half-${m.half}`} onMouseLeave={() => setHover(null)}>
                  {m.table.map((c) => {
                    const owner = teamOf(c.owner)
                    if (c.faceUp && c.card) {
                      const by = teamOf(c.flippedBy)!
                      return (
                        <div
                          key={c.index}
                          className={`grid-face ${last?.cell === c.index ? `just-flipped ${settings.reduceMotion ? '' : 'anim'} ${DANGEROUS.includes(c.card) || (c.stolen && c.card === 'OFFSIDE_TRAP') ? 'danger' : ''} ${c.stolen && c.card !== 'OFFSIDE_TRAP' ? 'stolen-flash' : ''} ${c.card === 'GOAL' || c.card === 'PENALTY' ? 'golden' : ''}` : ''}`}
                          style={{ borderColor: by.primaryColor }}
                          title={`${c.name} — flipped by ${by.name}${c.stolen ? ` (${owner?.name}'s card — ${c.card === 'OFFSIDE_TRAP' ? 'offside trap!' : 'stolen'})` : ''}`}
                        >
                          <CardFace code={c.card} name={c.name ?? ''} size="mini" />
                          <span className="grid-owner" style={{ background: by.primaryColor }}>
                            {c.stolen ? (c.card === 'OFFSIDE_TRAP' ? '🪤' : '🕵') : ''}
                            {by.shortName}
                          </span>
                        </div>
                      )
                    }
                    if (c.burned)
                      return (
                        <div key={c.index} className="grid-face burned-slot" title={c.mine ? `Your ${c.name} — burned` : 'A burned opponent card'}>
                          {finished && c.card ? <CardFace code={c.card} name={c.name ?? ''} size="mini" /> : <span>🔥</span>}
                        </div>
                      )
                    if (finished && c.card)
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
                        onHover={() => setHover(c.index)}
                        mark={c.mine ? { color: me.primaryColor, color2: me.secondaryColor, dim: m.ownCardUsed } : null}
                        peeked={c.peeked && c.card ? { code: c.card, name: c.name ?? '', opponent: c.owner != null && c.owner !== human } : null}
                        selected={targeting?.first === c.index}
                        disabled={!myTurn || busy}
                        title={c.mine ? `Your ${c.name}${m.ownCardUsed ? ' — wait for your next turn' : ''}` : 'Face-down card'}
                        onClick={() => clickCell(c.index)}
                      />
                    )
                  })}
                </div>
                <div className="row-hints">
                  {m.rowHints.map((h, i) => (
                    <span key={i} className={`${h === 0 ? 'safe' : ''} ${hover != null && Math.floor(hover / 5) === i ? 'hot' : ''}`} title="Dangerous grey cards still face down in this row">
                      {h}
                    </span>
                  ))}
                </div>
                <div className="col-hints">
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
                    <CardFace key={`${m.events.length}`} code={last.card} name={last.name} description={last.description} size="big" />
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

            {!finished && (
              <div className="hand-panel">
                <span className="muted small">Your hand {m.handPlayed ? '(one card per flip — played)' : ''}</span>
                <div className="hand-cards">
                  {m.freeScouts > 0 && (
                    <button className="hand-card free" disabled={!myTurn || busy} onClick={() => setTargeting({ card: 'SCOUT', free: true })} title="Free scout from your Playmaker">
                      👁 Free Scout ×{m.freeScouts}
                    </button>
                  )}
                  {m.hand.map((h, i) => (
                    <button key={h.code + i} className="hand-card" disabled={!myTurn || busy || m.handPlayed || (h.code === 'BURN' && m.opponentCardsLeft === 0)} onClick={() => playHand(h.code)} title={h.description ?? ''}>
                      {iconFor(h.code)} {h.name}
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
                <span className="speed-control" title="Computer speed">
                  {SPEEDS.map(([v, label]) => (
                    <button key={v} className={`btn btn-ghost small ${settings.aiDelay === v ? 'active' : ''}`} onClick={() => updateSettings({ aiDelay: v })}>
                      {label}
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

          <aside className="commentary panel">
            <h2>Commentary</h2>
            <ol className="events">
              {events.map((e, i) => (
                <li key={e.id} className={`ev ev-${e.type} ${e.side === human ? 'ours' : e.side ? 'theirs' : ''} ${i === 0 ? 'fresh' : ''}`}>
                  <span className="ev-min">{e.half === 1 ? '1st' : '2nd'}</span>
                  <span className="ev-text">{e.text}</span>
                </li>
              ))}
            </ol>
          </aside>
        </div>
      )}
    </div>
  )
}
