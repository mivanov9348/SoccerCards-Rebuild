import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, type MatchTeam, type MatchView } from '../api'
import { Badge, ErrorBox, useLoad } from '../components/common'
import { CardBack, CardFace } from '../components/GameCard'
import { useSettings } from '../settings'

function ScoreTeam({ t, align, turn }: { t: MatchTeam; align: 'left' | 'right'; turn: boolean }) {
  return (
    <div className={`score-team ${align} ${turn ? 'has-turn' : ''}`}>
      <Badge name={t.name} primary={t.primaryColor} secondary={t.secondaryColor} size={46} />
      <div>
        <div className="score-name">
          {t.name} {t.isHuman ? <span className="you-tag">YOU</span> : <span className="cpu-tag">CPU</span>}
        </div>
        <div className="muted small">
          {t.stats.yellowCards > 0 && <span>🟨×{t.stats.yellowCards} </span>}
          {t.stats.redCards > 0 && <span>🟥×{t.stats.redCards}</span>}
          {turn && <span className="turn-tag">● to flip</span>}
        </div>
      </div>
    </div>
  )
}

function StatsTable({ m }: { m: MatchView }) {
  const rows: [string, number, number][] = [
    ['Cards flipped', m.home.stats.flips, m.away.stats.flips],
    ['Passes', m.home.stats.passes, m.away.stats.passes],
    ['Tackles won', m.home.stats.tackles, m.away.stats.tackles],
    ['Corners', m.home.stats.corners, m.away.stats.corners],
    ['Free kicks', m.home.stats.freeKicks, m.away.stats.freeKicks],
    ['Offsides', m.home.stats.offsides, m.away.stats.offsides],
    ['Saves', m.home.stats.saves, m.away.stats.saves],
    ['Misses', m.home.stats.misses, m.away.stats.misses],
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

function PenaltyPanel({ m, busy, onPick }: { m: MatchView; busy: boolean; onPick: (d: number) => void }) {
  const shooter = m.penaltyShooter === 'Home' ? m.home : m.away
  const shooting = m.humanRole === 'shooter'
  const shootout = m.phase === 'Shootout'
  const homeScore = m.shootout.filter((k) => k.side === 'Home' && k.scored).length
  const awayScore = m.shootout.filter((k) => k.side === 'Away' && k.scored).length
  return (
    <div className="penalty-box">
      <h2>{shootout ? `Penalty shootout — ${homeScore} : ${awayScore}` : `Penalty to ${shooter.name}!`}</h2>
      {shootout && (
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
      <p>{shooting ? `${shooter.name} shoot — where do you aim?` : `${shooter.name} shoot — where does your keeper dive?`}</p>
      <div className="penalty-goal">
        {['Left', 'Center', 'Right'].map((label, i) => (
          <button key={label} className="btn btn-primary" disabled={busy} onClick={() => onPick(i)}>
            {shooting ? '⚽ ' : '🧤 '}
            {label}
          </button>
        ))}
      </div>
    </div>
  )
}

export default function MatchScreen() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [settings] = useSettings()
  const { data: m, error: loadError, setData } = useLoad(() => api.match(Number(id)), [id])
  const { data: cards } = useLoad(() => api.cards(), [])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const aiInFlight = useRef(false)

  const descriptions = Object.fromEntries((cards ?? []).map((c) => [c.code, c.description]))

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

  // The computer's turn: flip one card after a short pause, so the player can follow it.
  const computerTurn = m != null && m.phase === 'Play' && m.humanRole === 'wait'
  useEffect(() => {
    if (!m || !computerTurn || aiInFlight.current) return
    const timer = setTimeout(async () => {
      aiInFlight.current = true
      try {
        setData(await api.aiFlip(m.id))
      } catch (e) {
        setError((e as Error).message)
      } finally {
        aiInFlight.current = false
      }
    }, settings.aiDelay)
    return () => clearTimeout(timer)
  }, [m, computerTurn, settings.aiDelay, setData])

  const simulate = () => {
    if (!m || !confirm('Let the computer play out the rest of the match?')) return
    void run(() => api.simulateMatch(m.id))
  }

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

  const human = m.humanSide ?? 'Home'
  const me = human === 'Home' ? m.home : m.away
  const finished = m.phase === 'Finished'
  const myGoals = me.goals
  const theirGoals = human === 'Home' ? m.away.goals : m.home.goals
  const myPens = human === 'Home' ? m.homePenalties : m.awayPenalties
  const theirPens = human === 'Home' ? m.awayPenalties : m.homePenalties
  const won = myGoals > theirGoals || (myGoals === theirGoals && myPens != null && myPens > (theirPens ?? 0))
  const lost = myGoals < theirGoals || (myGoals === theirGoals && myPens != null && myPens < (theirPens ?? 0))
  const myTurn = m.phase === 'Play' && m.humanRole === 'flip'
  const last = m.lastFlip
  const lastBy = last ? (last.side === 'Home' ? m.home : m.away) : null
  const events = [...m.events].reverse()

  return (
    <div className="screen match-screen">
      <div className="scoreboard">
        <ScoreTeam t={m.home} align="left" turn={m.phase === 'Play' && m.turn === 'Home'} />
        <div className="score-center">
          <div className="score">
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

      <div className="match-grid">
        <div className="match-main">
          <div className={`turn-banner ${myTurn ? 'mine' : ''}`}>
            {finished
              ? 'The match is over'
              : m.phase === 'Play'
                ? myTurn
                  ? '👉 Your turn — pick a face-down card'
                  : `⏳ ${m.turn === 'Home' ? m.home.name : m.away.name} (computer) is flipping…`
                : m.phase === 'Shootout'
                  ? 'Penalty shootout!'
                  : 'Penalty!'}
          </div>

          <div className="table-area">
            <div className="flip-grid">
              {m.table.map((c) =>
                c.faceUp && c.card ? (
                  <div
                    key={c.index}
                    className={`grid-face ${last?.cell === c.index ? 'just-flipped' : ''}`}
                    style={{ borderColor: (c.flippedBy === 'Home' ? m.home : m.away).primaryColor }}
                    title={`${c.name} — flipped by ${(c.flippedBy === 'Home' ? m.home : m.away).name}`}
                  >
                    <CardFace code={c.card} name={c.name ?? ''} size="mini" />
                    <span className="grid-owner" style={{ background: (c.flippedBy === 'Home' ? m.home : m.away).primaryColor }}>
                      {(c.flippedBy === 'Home' ? m.home : m.away).shortName}
                    </span>
                  </div>
                ) : c.card ? (
                  <div key={c.index} className="grid-face unflipped" title="Not flipped">
                    <CardFace code={c.card} name={c.name ?? ''} size="mini" />
                  </div>
                ) : (
                  <CardBack
                    key={c.index}
                    disabled={!myTurn || busy}
                    onClick={() => run(() => api.flip(m.id, c.index))}
                  />
                ),
              )}
            </div>
            <div className="reveal">
              <span className="muted small">Last card</span>
              {last && lastBy ? (
                <>
                  <CardFace key={`${m.events.length}`} code={last.card} name={last.name} description={descriptions[last.card]} size="big" />
                  <span className="reveal-by">flipped by {lastBy.isHuman ? 'you' : lastBy.name}</span>
                </>
              ) : (
                <div className="reveal-empty">No card flipped yet</div>
              )}
              <span className="muted small">{m.cardsLeft} cards still face down</span>
            </div>
          </div>

          <ErrorBox error={error} />

          {(m.phase === 'Penalty' || m.phase === 'Shootout') && (
            <PenaltyPanel m={m} busy={busy} onPick={(d) => run(() => api.penalty(m.id, d))} />
          )}

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
    </div>
  )
}
