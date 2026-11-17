import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, tacticNames, type MatchEvent, type MatchTeam, type MatchView } from '../api'
import { Badge, ErrorBox, useLoad } from '../components/common'
import { GameCard } from '../components/GameCard'
import { useSettings } from '../settings'

const basicDescriptions: Record<string, string> = {
  HOLD: 'Запазвате топката без да напредвате. Безопасно, но губи време.',
  POSITION: 'Защита без изразходване на карта. По-слаба от картите.',
}

const zoneLabels = ['Нашето наказателно поле', 'Наша половина', 'Център', 'Атака', 'Наказателно поле на съперника']

function ScoreTeam({ t, align }: { t: MatchTeam; align: 'left' | 'right' }) {
  return (
    <div className={`score-team ${align}`}>
      <Badge name={t.name} primary={t.primaryColor} secondary={t.secondaryColor} size={46} />
      <div>
        <div className="score-name">
          {t.name} {t.isHuman && <span className="you-tag">ВИЕ</span>}
        </div>
        <div className="muted small">
          {tacticNames[t.tactic]} · А{t.attack} П{t.midfield} З{t.defense}
          {t.stats.red > 0 && <span className="red-card-tag"> 🟥×{t.stats.red}</span>}
        </div>
      </div>
    </div>
  )
}

function Pitch({ m }: { m: MatchView }) {
  const human = m.humanSide ?? 'Home'
  const ours = m.possession === human
  const index = ours ? m.zone + 1 : 3 - m.zone
  return (
    <div className="pitch-wrap">
      <div className="pitch">
        {zoneLabels.map((label, i) => (
          <div key={i} className={`pitch-zone z${i} ${i === index ? 'active' : ''}`}>
            <span className="zone-label">{label}</span>
            {i === index && <span className={`ball ${ours ? 'ours' : 'theirs'}`}>⚽</span>}
          </div>
        ))}
      </div>
      <div className="pitch-caption">
        {m.phase === 'Finished'
          ? 'Мачът приключи'
          : ours
            ? `Топката е у вас → атакувате (${m.zoneName})`
            : `Съперникът атакува ← (${m.zoneName} за него)`}
        {m.counterAvailable && m.phase === 'Play' && <span className="counter-tag">{ours ? ' · Можете да контраатакувате!' : ' · Внимание: възможна контраатака!'}</span>}
      </div>
    </div>
  )
}

function LastDuel({ m, ev }: { m: MatchView; ev: MatchEvent | undefined }) {
  if (!ev || !ev.attackCard) return <div className="duel empty">Изберете карта, за да започне двубоят.</div>
  const human = m.humanSide ?? 'Home'
  const weAttacked = ev.side === human
  const ours = weAttacked ? ev.attackCard : ev.defenseCard
  const theirs = weAttacked ? ev.defenseCard : ev.attackCard
  return (
    <div className="duel">
      <div className="duel-side">
        <span className="muted small">Вие</span>
        <span className={`duel-card ${weAttacked ? 'attack' : 'defense'}`}>{ours}</span>
      </div>
      <span className="vs">срещу</span>
      <div className="duel-side">
        <span className="muted small">Съперникът</span>
        <span className={`duel-card ${weAttacked ? 'defense' : 'attack'}`}>{theirs}</span>
      </div>
    </div>
  )
}

function StatsTable({ m }: { m: MatchView }) {
  const total = m.home.stats.possessionTurns + m.away.stats.possessionTurns || 1
  const rows: [string, number | string, number | string][] = [
    ['Владение', `${Math.round((m.home.stats.possessionTurns / total) * 100)}%`, `${Math.round((m.away.stats.possessionTurns / total) * 100)}%`],
    ['Удари', m.home.stats.shots, m.away.stats.shots],
    ['Точни удари', m.home.stats.onTarget, m.away.stats.onTarget],
    ['Нарушения', m.home.stats.fouls, m.away.stats.fouls],
    ['Жълти картони', m.home.stats.yellow, m.away.stats.yellow],
    ['Червени картони', m.home.stats.red, m.away.stats.red],
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

export default function MatchScreen() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [settings] = useSettings()
  const { data: m, error: loadError, setData } = useLoad(() => api.match(Number(id)), [id])
  const { data: cards } = useLoad(() => api.cards(), [])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [seenId, setSeenId] = useState(0)
  const lastSeen = useRef(0)

  const descriptions = useMemo(() => Object.fromEntries((cards ?? []).map((c) => [c.code, c.description])), [cards])

  useEffect(() => {
    if (m && lastSeen.current === 0) lastSeen.current = m.events.at(-1)?.id ?? 0
  }, [m])

  const act = async (action: { cardIndex?: number; swapIndex?: number; penaltyDirection?: number }) => {
    if (!m || busy) return
    setBusy(true)
    setError(null)
    setSeenId(lastSeen.current)
    try {
      const next = await api.play(m.id, action)
      lastSeen.current = next.events.at(-1)?.id ?? 0
      setData(next)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const simulate = async () => {
    if (!m || !confirm('Да симулирам ли остатъка от мача автоматично?')) return
    setBusy(true)
    try {
      setSeenId(lastSeen.current)
      const next = await api.simulateMatch(m.id)
      lastSeen.current = next.events.at(-1)?.id ?? 0
      setData(next)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  if (loadError) return <div className="screen"><ErrorBox error={loadError} /><Link to="/" className="btn">Главно меню</Link></div>
  if (!m) return <div className="screen loading">Зареждане на мача…</div>

  const human = m.humanSide ?? 'Home'
  const lastDuelEvent = [...m.events].reverse().find((e) => e.attackCard)
  const events = [...m.events].reverse()
  const finished = m.phase === 'Finished'
  const won = finished && (human === 'Home' ? m.home.goals > m.away.goals : m.away.goals > m.home.goals)
  const lost = finished && (human === 'Home' ? m.home.goals < m.away.goals : m.away.goals < m.home.goals)
  const penWin = m.homePenalties != null && (human === 'Home' ? m.homePenalties > (m.awayPenalties ?? 0) : (m.awayPenalties ?? 0) > m.homePenalties)

  return (
    <div className="screen match-screen">
      <div className="scoreboard">
        <ScoreTeam t={m.home} align="left" />
        <div className="score-center">
          <div className="score">
            {m.home.goals} : {m.away.goals}
          </div>
          {m.homePenalties != null && (
            <div className="muted small">
              дузпи {m.homePenalties}:{m.awayPenalties}
            </div>
          )}
          <div className="clock">{finished ? 'Край' : `${m.minuteLabel} · ${m.half === 1 ? '1-во' : '2-ро'} полувреме`}</div>
          <div className="muted small">
            {m.competition} · {m.stadium}
          </div>
        </div>
        <ScoreTeam t={m.away} align="right" />
      </div>

      <div className="match-grid">
        <div className="match-main">
          <Pitch m={m} />
          <LastDuel m={m} ev={lastDuelEvent} />
          <ErrorBox error={error} />

          {m.phase === 'Play' && m.hand && (
            <div className="hand-area">
              <div className="hand-prompt">
                {m.humanRole === 'attack' ? '⚔ Вие атакувате — изберете атакуваща карта' : '🛡 Съперникът атакува — изберете защитна карта'}
                {m.opponentHand && (
                  <span className="opp-hand" title="Карти в ръката на съперника">
                    Съперникът държи {m.humanRole === 'attack' ? m.opponentHand.defense : m.opponentHand.attack} карти
                    {Array.from({ length: m.humanRole === 'attack' ? m.opponentHand.defense : m.opponentHand.attack }).map((_, i) => (
                      <span key={i} className="card-back" />
                    ))}
                  </span>
                )}
              </div>
              <div className="hand">
                {m.hand.map((c) => (
                  <GameCard
                    key={`${c.index}-${c.code}`}
                    code={c.code}
                    name={c.name}
                    description={descriptions[c.code]}
                    hint={c.hint}
                    hintLabel={c.hintLabel}
                    showHint={settings.showHints}
                    disabled={!c.legal || busy}
                    onPlay={() => act({ cardIndex: c.index })}
                    onSwap={busy ? undefined : () => act({ swapIndex: c.index })}
                  />
                ))}
                {m.basic && (
                  <GameCard
                    basic
                    code={m.basic.code}
                    name={m.basic.name}
                    description={basicDescriptions[m.basic.code]}
                    hint={m.basic.hint}
                    hintLabel={m.basic.hintLabel}
                    showHint={settings.showHints}
                    disabled={busy}
                    onPlay={() => act({})}
                  />
                )}
              </div>
            </div>
          )}

          {m.phase === 'Penalty' && (
            <div className="penalty-box">
              <h2>{m.humanRole === 'shooter' ? '⚽ Дузпа за вас! Накъде стреляте?' : '🧤 Дузпа срещу вас! Накъде да се хвърли вратарят?'}</h2>
              <div className="penalty-goal">
                {['Ляво', 'Център', 'Дясно'].map((label, i) => (
                  <button key={label} className="btn btn-primary" disabled={busy} onClick={() => act({ penaltyDirection: i })}>
                    {label}
                  </button>
                ))}
              </div>
            </div>
          )}

          {finished && (
            <div className={`result-box ${won || penWin ? 'won' : lost ? 'lost' : ''}`}>
              <h2>{won || penWin ? '🏆 Победа!' : lost || (m.homePenalties != null && !penWin) ? 'Загуба' : 'Равенство'}</h2>
              <StatsTable m={m} />
              <div className="row-buttons">
                {m.careerId ? (
                  <button className="btn btn-primary" onClick={() => navigate(`/career/${m.careerId}`)}>
                    Към кариерата ▶
                  </button>
                ) : (
                  <>
                    <button className="btn btn-primary" onClick={() => navigate('/friendly')}>
                      Нов приятелски мач
                    </button>
                    <button className="btn" onClick={() => navigate('/')}>
                      Главно меню
                    </button>
                  </>
                )}
              </div>
            </div>
          )}

          {!finished && (
            <div className="row-buttons secondary">
              <button className="btn btn-ghost" disabled={busy} onClick={simulate}>
                ⏩ Симулирай до края
              </button>
              <button className="btn btn-ghost" onClick={() => navigate(m.careerId ? `/career/${m.careerId}` : '/')}>
                Пауза (мачът се запазва)
              </button>
            </div>
          )}
        </div>

        <aside className="commentary panel">
          <h2>Коментар</h2>
          {!finished && <StatsTable m={m} />}
          <ol className="events">
            {events.map((e) => (
              <li key={e.id} className={`ev ev-${e.type} ${e.side === human ? 'ours' : e.side ? 'theirs' : ''} ${e.id > seenId && seenId > 0 ? 'fresh' : ''}`}>
                <span className="ev-min">{e.minuteLabel}</span>
                <span className="ev-text">
                  {e.attackCard && (
                    <span className="ev-cards">
                      {e.attackCard} / {e.defenseCard}
                    </span>
                  )}
                  {e.text}
                </span>
              </li>
            ))}
          </ol>
        </aside>
      </div>
    </div>
  )
}
