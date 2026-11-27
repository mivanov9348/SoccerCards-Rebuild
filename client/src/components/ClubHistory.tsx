import { api, money, seasonLabel } from '../api'
import { ErrorBox, useLoad } from './common'
import { Icon } from './Icons'

const TROPHY_ICON: Record<string, string> = { league: '🏆', cup: '🥇', europe: '⭐' }

/**
 * The club history tab (1.3): the trophy cabinet, every season, the overall record, records, statistics of the matches you
 * played card by card, and the achievements with their rewards.
 */
export function ClubHistory({ careerId }: { careerId: number }) {
  const { data: h, error } = useLoad(() => api.careerHistory(careerId), [careerId])
  if (error) return <ErrorBox error={error} />
  if (!h) return <div className="panel loading">Loading history…</div>
  const unlocked = h.achievements.filter((a) => a.unlocked).length
  const s = h.stats
  return (
    <div className="club-history">
      <div className="panel">
        <h2>Trophy cabinet</h2>
        {h.trophies.length === 0 ? (
          <p className="muted">No trophies yet. Win the league, the cup or a European cup to fill the shelves.</p>
        ) : (
          <div className="trophy-shelf">
            {h.trophies.map((t, i) => (
              <div key={i} className={`trophy trophy-${t.kind}`} title={`${t.name} — ${t.team}`}>
                <span className="trophy-icon">{TROPHY_ICON[t.kind]}</span>
                <b>{t.name}</b>
                <span className="muted small">
                  {seasonLabel(t.year)} · {t.team}
                </span>
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="two-col">
        <div className="panel">
          <h2>Record</h2>
          <div className="record-grid">
            <div><b>{h.record.played}</b><span className="muted small">played</span></div>
            <div><b>{h.record.won}</b><span className="muted small">won</span></div>
            <div><b>{h.record.drawn}</b><span className="muted small">drawn</span></div>
            <div><b>{h.record.lost}</b><span className="muted small">lost</span></div>
            <div><b>{h.record.goalsFor}:{h.record.goalsAgainst}</b><span className="muted small">goals</span></div>
          </div>
          <table className="table">
            <tbody>
              <tr><td className="left muted">Biggest win</td><td className="left">{h.records.biggestWin ?? '—'}</td></tr>
              <tr><td className="left muted">Worst defeat</td><td className="left">{h.records.worstDefeat ?? '—'}</td></tr>
              <tr><td className="left muted">Longest unbeaten run</td><td className="left">{h.records.longestUnbeaten} matches</td></tr>
              <tr><td className="left muted">Longest winning run</td><td className="left">{h.records.longestWinningRun} matches</td></tr>
            </tbody>
          </table>
        </div>
        <div className="panel">
          <h2>Your matches, card by card</h2>
          <p className="muted small">From the {s.matchesPlayed} match{s.matchesPlayed === 1 ? '' : 'es'} you played yourself (simulated matches are not counted here).</p>
          <table className="table">
            <tbody>
              <tr>
                <td className="left muted">Goals by source</td>
                <td className="left">{s.goalSources.length ? s.goalSources.map((g) => `${g.source} ${g.goals}`).join(' · ') : '—'}</td>
              </tr>
              <tr>
                <td className="left muted">Penalties</td>
                <td className="left">
                  {s.penaltiesScored} scored of {s.penaltiesAwarded}
                  {s.penaltiesAwarded > 0 ? ` (${Math.round((s.penaltiesScored * 100) / s.penaltiesAwarded)}%)` : ''}
                </td>
              </tr>
              <tr><td className="left muted">Cards you stole</td><td className="left">{s.stolenByMe}</td></tr>
              <tr><td className="left muted">Cards stolen from you</td><td className="left">{s.stolenFromMe}</td></tr>
              <tr>
                <td className="left muted">Most stolen card of yours</td>
                <td className="left">{s.mostStolen ? <><Icon code={s.mostStolen.code} /> {s.mostStolen.name} × {s.mostStolen.count}</> : '—'}</td>
              </tr>
              <tr>
                <td className="left muted">Your favourite card</td>
                <td className="left">{s.favourite ? <><Icon code={s.favourite.code} /> {s.favourite.name} × {s.favourite.count}</> : '—'}</td>
              </tr>
              <tr><td className="left muted">Legendary cards played</td><td className="left">{s.legendaryPlays}</td></tr>
            </tbody>
          </table>
        </div>
      </div>

      <div className="panel">
        <h2>Seasons</h2>
        <div className="table-scroll">
          <table className="table">
            <thead>
              <tr>
                <th className="left">Season</th>
                <th className="left">Club</th>
                <th className="left">League</th>
                <th>Pos</th>
                <th>Pts</th>
                <th>Goals</th>
                <th className="left">Cups</th>
                <th>Income</th>
                <th>Expenses</th>
                <th className="left">Board</th>
              </tr>
            </thead>
            <tbody>
              {h.seasons.map((x) => (
                <tr key={x.year} className={x.position === 1 ? 'season-champion' : ''}>
                  <td className="left">{seasonLabel(x.year)}{x.finished ? '' : ' (now)'}</td>
                  <td className="left">{x.team}</td>
                  <td className="left">{x.league}</td>
                  <td>{x.position ?? '—'}</td>
                  <td>{x.points ?? '—'}</td>
                  <td>{x.goalsFor != null ? `${x.goalsFor}:${x.goalsAgainst}` : '—'}</td>
                  <td className="left small">{[x.cup ? '🥇 cup' : null, x.europe].filter(Boolean).join(' · ') || '—'}</td>
                  <td>{money(x.income)}</td>
                  <td>{money(x.expenses)}</td>
                  <td className="left small">{x.fired ? 'sacked' : x.verdict ? x.verdict.split(' — ')[0].replace(/^The board is /, '') : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <div className="panel">
        <h2>
          Achievements <span className="muted small">{unlocked} / {h.achievements.length}</span>
        </h2>
        <div className="achievements">
          {h.achievements.map((a) => (
            <div key={a.code} className={`achievement ${a.unlocked ? 'unlocked' : ''}`} title={a.description}>
              <span className="ach-medal">{a.unlocked ? '🏅' : '🔒'}</span>
              <div>
                <b>{a.name}</b>
                <span className="small">{a.description}</span>
                <span className="muted small">
                  {a.unlocked ? `unlocked ${a.when ? seasonLabel(Number(a.when.split('/')[0])) + ', week ' + a.when.split('/')[1] : ''}` : `reward: ${a.reward}`}
                </span>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
