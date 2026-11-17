import { useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { api, seasonLabel, tacticNames, type CareerView, type Fixture, type Tactic } from '../api'
import { Badge, ErrorBox, FixtureRow, RatingBar, Screen, StandingsTable, useLoad } from '../components/common'
import { useSettings } from '../settings'

type Tab = 'overview' | 'table' | 'fixtures' | 'cup' | 'europe' | 'news'

function NextMatch({ c, busy, onPlay, onSimulate }: { c: CareerView; busy: boolean; onPlay: (t: Tactic) => void; onSimulate: () => void }) {
  const [settings] = useSettings()
  const [tactic, setTactic] = useState<Tactic>(settings.defaultTactic)
  const f = c.thisWeekFixture
  if (c.season.finished) return null

  if (!f) {
    return (
      <div className="panel next-match">
        <h2>Седмица {c.season.currentWeek}</h2>
        <p>
          Тази седмица се играе <b>{c.season.weekCompetition ?? 'друг турнир'}</b>, но вашият отбор не участва.
        </p>
        <button className="btn btn-primary" disabled={busy} onClick={onSimulate}>
          Продължи към следващата седмица ▶
        </button>
      </div>
    )
  }

  const home = f.home.id === c.team.id
  const opp = home ? f.away : f.home
  return (
    <div className="panel next-match">
      <h2>
        {f.competition} · {f.stage}
      </h2>
      <div className="versus">
        <div className="vs-team">
          <Badge name={f.home.name} primary={f.home.primaryColor} secondary={f.home.secondaryColor} size={54} />
          <b>{f.home.name}</b>
        </div>
        <span className="vs-text">срещу</span>
        <div className="vs-team">
          <Badge name={f.away.name} primary={f.away.primaryColor} secondary={f.away.secondaryColor} size={54} />
          <b>{f.away.name}</b>
        </div>
      </div>
      <p className="muted center">
        {home ? 'Домакин' : 'Гост'} · съперник: {opp.name}
        {f.isKnockout && ' · елиминация (при равенство — дузпи)'}
      </p>
      {c.activeMatchId ? (
        <div className="row-buttons">
          <button className="btn btn-primary" onClick={() => onPlay(tactic)}>
            Продължи започнатия мач ▶
          </button>
          <button className="btn" disabled={busy} onClick={onSimulate}>
            Симулирай остатъка
          </button>
        </div>
      ) : (
        <div className="row-buttons">
          <label className="inline">
            Тактика
            <select value={tactic} onChange={(e) => setTactic(e.target.value as Tactic)}>
              {Object.entries(tacticNames).map(([k, v]) => (
                <option key={k} value={k}>
                  {v}
                </option>
              ))}
            </select>
          </label>
          <button className="btn btn-primary" disabled={busy} onClick={() => onPlay(tactic)}>
            Играй мача ▶
          </button>
          <button className="btn" disabled={busy} onClick={onSimulate}>
            Симулирай
          </button>
        </div>
      )}
    </div>
  )
}

function SeasonEnd({ c, busy, onNext }: { c: CareerView; busy: boolean; onNext: (teamId?: number) => void }) {
  const s = c.season.summary
  if (!c.season.finished || !s) return null
  return (
    <div className={`panel season-end ${s.fired ? 'fired' : ''}`}>
      <h2>Край на сезон {seasonLabel(c.season.year)}</h2>
      <div className="summary-grid">
        <div>
          <span className="muted">Крайно място</span>
          <b>
            {s.position}-о ({s.points} т.)
          </b>
        </div>
        <div>
          <span className="muted">Шампион</span>
          <b>{s.champion}</b>
        </div>
        <div>
          <span className="muted">Купа на България</span>
          <b>{s.cupWinner ?? '—'}</b>
        </div>
        {s.europeResult && (
          <div>
            <span className="muted">Европа</span>
            <b>{s.europeResult}</b>
          </div>
        )}
        <div>
          <span className="muted">Цел на борда</span>
          <b>{s.expectation}</b>
        </div>
        <div>
          <span className="muted">Репутация</span>
          <b>
            {c.reputation} ({s.reputationChange >= 0 ? '+' : ''}
            {s.reputationChange})
          </b>
        </div>
      </div>
      <p className="verdict">{s.verdict}</p>

      {s.offers.length > 0 && (
        <>
          <h3>Оферти за работа</h3>
          <div className="offers">
            {s.offers.map((o) => (
              <div key={o.teamId} className="offer">
                <span>
                  {o.name} <span className="muted">(сила {o.overall})</span>
                </span>
                <button className="btn btn-primary" disabled={busy} onClick={() => onNext(o.teamId)}>
                  Приеми
                </button>
              </div>
            ))}
          </div>
        </>
      )}
      <div className="row-buttons">
        {!s.fired && (
          <button className="btn btn-primary" disabled={busy} onClick={() => onNext()}>
            Остани в {c.team.name} — нов сезон ▶
          </button>
        )}
        {s.fired && s.offers.length === 0 && <p className="error-box">Кариерата приключи — никой клуб не ви предложи работа.</p>}
      </div>
    </div>
  )
}

function KnockoutView({ fixtures, teamId, empty }: { fixtures: Fixture[]; teamId: number; empty: string }) {
  if (fixtures.length === 0) return <p className="muted">{empty}</p>
  const stages = [...new Set(fixtures.map((f) => f.stage))]
  return (
    <div className="bracket">
      {stages.map((stage) => (
        <div key={stage} className="bracket-round">
          <h3>{stage}</h3>
          {fixtures
            .filter((f) => f.stage === stage)
            .map((f) => (
              <FixtureRow key={f.id} f={f} highlight={teamId} />
            ))}
        </div>
      ))}
    </div>
  )
}

export default function CareerHub() {
  const { id } = useParams()
  const careerId = Number(id)
  const navigate = useNavigate()
  const [tab, setTab] = useState<Tab>('overview')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const { data: c, error: loadError, reload } = useLoad(() => api.career(careerId), [careerId])
  const { data: fixtures, reload: reloadFixtures } = useLoad(() => api.careerFixtures(careerId), [careerId])

  const run = async (fn: () => Promise<void>) => {
    setBusy(true)
    setError(null)
    try {
      await fn()
      await Promise.all([reload(), reloadFixtures()])
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const play = async (tactic: Tactic) => {
    setBusy(true)
    try {
      const { matchId } = await api.playCareer(careerId, tactic)
      navigate(`/match/${matchId}`)
    } catch (e) {
      setError((e as Error).message)
      setBusy(false)
    }
  }

  const all = fixtures ?? []
  const myFixtures = useMemo(() => all.filter((f) => c && (f.home.id === c.team.id || f.away.id === c.team.id)), [all, c])

  if (loadError) return <Screen title="Кариера"><ErrorBox error={loadError} /></Screen>
  if (!c) return <div className="screen loading">Зареждане…</div>

  const leagueByRound = all.filter((f) => f.competitionCode === 'BG1')
  const cup = all.filter((f) => f.competitionCode === 'BGCUP')
  const europe = all.filter((f) => ['UCL', 'UEL', 'UECL'].includes(f.competitionCode))

  return (
    <Screen
      title={c.saveName}
      actions={
        <span className="muted small">
          Автоматично записване · {c.managerName}
        </span>
      }
    >
      <div className="career-header panel">
        <Badge name={c.team.name} primary={c.team.primaryColor} secondary={c.team.secondaryColor} size={64} />
        <div className="career-title">
          <h2>{c.team.name}</h2>
          <span className="muted">
            Старши треньор: {c.managerName} · {c.team.city}, „{c.team.stadium}“
          </span>
          {c.status === 'Fired' && <span className="error-text">Освободен от поста</span>}
        </div>
        <div className="career-stats">
          <div>
            <span className="muted">Сезон</span>
            <b>{seasonLabel(c.season.year)}</b>
          </div>
          <div>
            <span className="muted">Седмица</span>
            <b>
              {c.season.currentWeek}/{c.season.totalWeeks}
            </b>
          </div>
          <div>
            <span className="muted">Място</span>
            <b>{c.position ?? '—'}</b>
          </div>
          <div>
            <span className="muted">Репутация</span>
            <b>{c.reputation}</b>
          </div>
        </div>
      </div>

      <div className="tabs">
        {(
          [
            ['overview', 'Преглед'],
            ['table', 'Класиране'],
            ['fixtures', 'Програма'],
            ['cup', 'Купа на България'],
            ['europe', c.season.european ?? 'Европа'],
            ['news', 'Новини'],
          ] as [Tab, string][]
        ).map(([k, label]) => (
          <button key={k} className={tab === k ? 'active' : ''} onClick={() => setTab(k)}>
            {label}
          </button>
        ))}
      </div>
      <ErrorBox error={error} />

      {tab === 'overview' && (
        <div className="two-col">
          <div>
            <SeasonEnd c={c} busy={busy} onNext={(teamId) => run(() => api.nextSeason(careerId, teamId))} />
            <NextMatch c={c} busy={busy} onPlay={play} onSimulate={() => run(() => api.simulateCareer(careerId))} />
            <div className="panel">
              <h2>Цели и отбор</h2>
              <p>
                Очакване на борда: <b>{c.season.expectation}</b>
              </p>
              {c.season.european && (
                <p>
                  Европейски турнир: <b>{c.season.european}</b>
                </p>
              )}
              <RatingBar label="Атака" value={c.team.attack} />
              <RatingBar label="Полузащита" value={c.team.midfield} />
              <RatingBar label="Защита" value={c.team.defense} />
            </div>
            <div className="panel">
              <h2>Последни резултати</h2>
              {c.recent.length === 0 && <p className="muted">Още няма изиграни мачове.</p>}
              {[...c.recent].reverse().map((f) => (
                <div key={f.id}>
                  <span className="muted small">
                    {f.competition} · {f.stage}
                  </span>
                  <FixtureRow f={f} highlight={c.team.id} />
                </div>
              ))}
            </div>
          </div>
          <div>
            <div className="panel">
              <h2>Първа лига</h2>
              <StandingsTable rows={c.table} highlight={c.team.id} />
            </div>
            <div className="panel">
              <h2>Новини</h2>
              {c.messages.slice(0, 5).map((m) => (
                <div key={m.id} className="message">
                  <b>{m.title}</b>
                  <p>{m.body}</p>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {tab === 'table' && (
        <div className="panel">
          <StandingsTable rows={c.table} highlight={c.team.id} />
          <p className="muted small legend">
            <span className="lg champion" /> Шампионска лига <span className="lg europe" /> Лига Европа / Лига на конференциите{' '}
            <span className="lg bottom" /> Последно място
          </p>
        </div>
      )}

      {tab === 'fixtures' && (
        <div className="two-col">
          <div className="panel">
            <h2>Вашите мачове</h2>
            {myFixtures.map((f) => (
              <div key={f.id} className={f.week === c.season.currentWeek ? 'current-week' : ''}>
                <span className="muted small">
                  Седмица {f.week} · {f.competition} · {f.stage}
                </span>
                <FixtureRow f={f} highlight={c.team.id} />
              </div>
            ))}
          </div>
          <div className="panel">
            <h2>Всички кръгове</h2>
            {[...new Set(leagueByRound.map((f) => f.round))].map((r) => (
              <details key={r} open={leagueByRound.some((f) => f.round === r && f.week === c.season.currentWeek)}>
                <summary>{r}-ви кръг</summary>
                {leagueByRound
                  .filter((f) => f.round === r)
                  .map((f) => (
                    <FixtureRow key={f.id} f={f} highlight={c.team.id} />
                  ))}
              </details>
            ))}
          </div>
        </div>
      )}

      {tab === 'cup' && (
        <div className="panel">
          <KnockoutView fixtures={cup} teamId={c.team.id} empty="Купата още не е започнала." />
        </div>
      )}

      {tab === 'europe' && (
        <div className="panel">
          <KnockoutView
            fixtures={europe}
            teamId={c.team.id}
            empty="Този сезон отборът ви не участва в европейски турнир. Класирайте се в топ 3 или спечелете купата!"
          />
        </div>
      )}

      {tab === 'news' && (
        <div className="panel">
          {c.messages.map((m) => (
            <div key={m.id} className="message">
              <span className="muted small">
                {seasonLabel(m.year)} · седмица {m.week}
              </span>
              <b>{m.title}</b>
              <p>{m.body}</p>
            </div>
          ))}
        </div>
      )}
    </Screen>
  )
}
