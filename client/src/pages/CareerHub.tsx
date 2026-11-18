import { useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { api, seasonLabel, type CareerView, type Fixture } from '../api'
import { Badge, ErrorBox, FixtureRow, Screen, StandingsTable, useLoad } from '../components/common'

type Tab = 'overview' | 'table' | 'fixtures' | 'cup' | 'europe' | 'news'

const ordinal = (n: number) => {
  const s = n % 100 >= 11 && n % 100 <= 13 ? 'th' : ({ 1: 'st', 2: 'nd', 3: 'rd' } as Record<number, string>)[n % 10] ?? 'th'
  return `${n}${s}`
}

function NextMatch({ c, busy, onPlay, onSimulate }: { c: CareerView; busy: boolean; onPlay: () => void; onSimulate: () => void }) {
  const f = c.thisWeekFixture
  if (c.season.finished) return null

  if (!f) {
    return (
      <div className="panel next-match">
        <h2>Week {c.season.currentWeek}</h2>
        <p>
          This week's games are in the <b>{c.season.weekCompetition ?? 'another competition'}</b>, but your team is not playing.
        </p>
        <button className="btn btn-primary" disabled={busy} onClick={onSimulate}>
          Continue to next week ▶
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
        <span className="vs-text">vs</span>
        <div className="vs-team">
          <Badge name={f.away.name} primary={f.away.primaryColor} secondary={f.away.secondaryColor} size={54} />
          <b>{f.away.name}</b>
        </div>
      </div>
      <p className="muted center">
        {home ? 'Home' : 'Away'} · opponent: {opp.name}
        {f.isKnockout && ' · knockout (penalties if level)'}
      </p>
      <div className="row-buttons">
        <button className="btn btn-primary" disabled={busy} onClick={onPlay}>
          {c.activeMatchId ? 'Resume match ▶' : 'Play match ▶'}
        </button>
        <button className="btn" disabled={busy} onClick={onSimulate}>
          {c.activeMatchId ? 'Simulate the rest' : 'Simulate'}
        </button>
      </div>
    </div>
  )
}

function SeasonEnd({ c, busy, onNext }: { c: CareerView; busy: boolean; onNext: (teamId?: number) => void }) {
  const s = c.season.summary
  if (!c.season.finished || !s) return null
  return (
    <div className={`panel season-end ${s.fired ? 'fired' : ''}`}>
      <h2>End of season {seasonLabel(c.season.year)}</h2>
      <div className="summary-grid">
        <div>
          <span className="muted">Final position</span>
          <b>
            {ordinal(s.position)} ({s.points} pts)
          </b>
        </div>
        <div>
          <span className="muted">Champions</span>
          <b>{s.champion}</b>
        </div>
        <div>
          <span className="muted">Bulgarian Cup</span>
          <b>{s.cupWinner ?? '—'}</b>
        </div>
        {s.europeResult && (
          <div>
            <span className="muted">Europe</span>
            <b>{s.europeResult}</b>
          </div>
        )}
        <div>
          <span className="muted">Board target</span>
          <b>{s.expectation}</b>
        </div>
        <div>
          <span className="muted">Reputation</span>
          <b>
            {c.reputation} ({s.reputationChange >= 0 ? '+' : ''}
            {s.reputationChange})
          </b>
        </div>
      </div>
      <p className="verdict">{s.verdict}</p>

      {s.offers.length > 0 && (
        <>
          <h3>Job offers</h3>
          <div className="offers">
            {s.offers.map((o) => (
              <div key={o.teamId} className="offer">
                <span>{o.name}</span>
                <button className="btn btn-primary" disabled={busy} onClick={() => onNext(o.teamId)}>
                  Accept
                </button>
              </div>
            ))}
          </div>
        </>
      )}
      <div className="row-buttons">
        {!s.fired && (
          <button className="btn btn-primary" disabled={busy} onClick={() => onNext()}>
            Stay at {c.team.name} — next season ▶
          </button>
        )}
        {s.fired && s.offers.length === 0 && <p className="error-box">Career over — no club offered you a job.</p>}
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

  const play = async () => {
    setBusy(true)
    try {
      const { matchId } = await api.playCareer(careerId)
      navigate(`/match/${matchId}`)
    } catch (e) {
      setError((e as Error).message)
      setBusy(false)
    }
  }

  const all = useMemo(() => fixtures ?? [], [fixtures])
  const myFixtures = useMemo(() => all.filter((f) => c && (f.home.id === c.team.id || f.away.id === c.team.id)), [all, c])

  if (loadError)
    return (
      <Screen title="Career">
        <ErrorBox error={loadError} />
      </Screen>
    )
  if (!c) return <div className="screen loading">Loading…</div>

  const league = all.filter((f) => f.competitionCode === 'BG1')
  const cup = all.filter((f) => f.competitionCode === 'BGCUP')
  const europe = all.filter((f) => ['UCL', 'UEL', 'UECL'].includes(f.competitionCode))

  return (
    <Screen title={c.saveName} actions={<span className="muted small">Auto-saved · {c.managerName}</span>}>
      <div className="career-header panel">
        <Badge name={c.team.name} primary={c.team.primaryColor} secondary={c.team.secondaryColor} size={64} />
        <div className="career-title">
          <h2>{c.team.name}</h2>
          <span className="muted">
            Manager: {c.managerName} · {c.team.city}, {c.team.stadium}
          </span>
          {c.status === 'Fired' && <span className="error-text">Sacked</span>}
        </div>
        <div className="career-stats">
          <div>
            <span className="muted">Season</span>
            <b>{seasonLabel(c.season.year)}</b>
          </div>
          <div>
            <span className="muted">Week</span>
            <b>
              {c.season.currentWeek}/{c.season.totalWeeks}
            </b>
          </div>
          <div>
            <span className="muted">Position</span>
            <b>{c.position ?? '—'}</b>
          </div>
          <div>
            <span className="muted">Reputation</span>
            <b>{c.reputation}</b>
          </div>
        </div>
      </div>

      <div className="tabs">
        {(
          [
            ['overview', 'Overview'],
            ['table', 'Table'],
            ['fixtures', 'Fixtures'],
            ['cup', 'Bulgarian Cup'],
            ['europe', c.season.european ?? 'Europe'],
            ['news', 'News'],
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
              <h2>Season targets</h2>
              <p>
                Board target: <b>{c.season.expectation}</b>
              </p>
              {c.season.european && (
                <p>
                  European cup: <b>{c.season.european}</b>
                </p>
              )}
            </div>
            <div className="panel">
              <h2>Recent results</h2>
              {c.recent.length === 0 && <p className="muted">No matches played yet.</p>}
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
              <h2>First League</h2>
              <StandingsTable rows={c.table} highlight={c.team.id} />
            </div>
            <div className="panel">
              <h2>News</h2>
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
            <span className="lg champion" /> Champions Cup <span className="lg europe" /> Europa Cup / Conference Cup{' '}
            <span className="lg bottom" /> Last place
          </p>
        </div>
      )}

      {tab === 'fixtures' && (
        <div className="two-col">
          <div className="panel">
            <h2>Your matches</h2>
            {myFixtures.map((f) => (
              <div key={f.id} className={f.week === c.season.currentWeek ? 'current-week' : ''}>
                <span className="muted small">
                  Week {f.week} · {f.competition} · {f.stage}
                </span>
                <FixtureRow f={f} highlight={c.team.id} />
              </div>
            ))}
          </div>
          <div className="panel">
            <h2>All rounds</h2>
            {[...new Set(league.map((f) => f.round))].map((r) => (
              <details key={r} open={league.some((f) => f.round === r && f.week === c.season.currentWeek)}>
                <summary>Round {r}</summary>
                {league
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
          <KnockoutView fixtures={cup} teamId={c.team.id} empty="The cup has not started yet." />
        </div>
      )}

      {tab === 'europe' && (
        <div className="panel">
          <KnockoutView
            fixtures={europe}
            teamId={c.team.id}
            empty="Your team is not in a European cup this season. Finish in the top 3 or win the cup!"
          />
        </div>
      )}

      {tab === 'news' && (
        <div className="panel">
          {c.messages.map((m) => (
            <div key={m.id} className="message">
              <span className="muted small">
                {seasonLabel(m.year)} · week {m.week}
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
