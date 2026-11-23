import { useState } from 'react'
import { api, seasonLabel } from '../api'
import { Badge, byCountry, ErrorBox, Screen, StandingsTable, useLoad } from '../components/common'

/** League tables of a saved career (any league) and the ratings of every club (any league or the clubs without one). */
export default function Tables() {
  const { data: careers, error } = useLoad(() => api.careers(), [])
  const { data: teams } = useLoad(() => api.teams(), [])
  const { data: leagues } = useLoad(() => api.leagues(), [])
  const [careerId, setCareerId] = useState<number | null>(null)
  const active = careerId ?? careers?.[0]?.id ?? null
  const { data: career } = useLoad(() => (active ? api.career(active) : Promise.resolve(null)), [active])
  const { data: careerLeagues } = useLoad(() => (active ? api.careerLeagues(active) : Promise.resolve([])), [active])
  const [tableLeague, setTableLeague] = useState<number | null>(null)
  const shownTableLeague = tableLeague ?? career?.league.id ?? null
  const { data: rows } = useLoad(
    () => (active && shownTableLeague ? api.careerStandings(active, shownTableLeague) : Promise.resolve([])),
    [active, shownTableLeague],
  )

  // team ratings: a league id, or 0 = clubs without a league
  const [ratingsLeague, setRatingsLeague] = useState<number | null>(null)
  const firstLeague = byCountry(leagues ?? [], (l) => l.country)[0]?.[1][0]?.id ?? null
  const shownLeague = ratingsLeague ?? firstLeague
  const shown = (teams ?? [])
    .filter((t) => (shownLeague === 0 ? t.leagueId === null : t.leagueId === shownLeague))
    .sort((a, b) => b.overall - a.overall || a.name.localeCompare(b.name))

  return (
    <Screen title="Tables & teams">
      <ErrorBox error={error} />
      <div className="two-col">
        <div className="panel">
          <h2>Career league tables</h2>
          {careers && careers.length === 0 && <p className="muted">Start a career to see league tables.</p>}
          {careers && careers.length > 0 && (
            <div className="form-row">
              <label className="inline">
                Save
                <select
                  value={active ?? ''}
                  onChange={(e) => {
                    setCareerId(Number(e.target.value))
                    setTableLeague(null)
                  }}
                >
                  {careers.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.saveName} ({c.team}, {c.year ? seasonLabel(c.year) : ''})
                    </option>
                  ))}
                </select>
              </label>
              {careerLeagues && careerLeagues.length > 0 && (
                <label className="inline">
                  League
                  <select value={shownTableLeague ?? ''} onChange={(e) => setTableLeague(Number(e.target.value))}>
                    {byCountry(careerLeagues, (l) => l.country).map(([country, ls]) => (
                      <optgroup key={country} label={country}>
                        {ls.map((l) => (
                          <option key={l.id} value={l.id}>
                            {l.name}
                          </option>
                        ))}
                      </optgroup>
                    ))}
                  </select>
                </label>
              )}
            </div>
          )}
          {career && rows && <StandingsTable rows={rows} highlight={career.team.id} leagueId={shownTableLeague ?? undefined} />}
        </div>
        <div className="panel">
          <h2>Team ratings</h2>
          <label className="inline">
            League
            <select value={shownLeague ?? ''} onChange={(e) => setRatingsLeague(Number(e.target.value))}>
              {byCountry(leagues ?? [], (l) => l.country).map(([country, ls]) => (
                <optgroup key={country} label={country}>
                  {ls.map((l) => (
                    <option key={l.id} value={l.id}>
                      {l.name}
                    </option>
                  ))}
                </optgroup>
              ))}
              <option value={0}>Clubs without a league (continental cups only)</option>
            </select>
          </label>
          <table className="table">
            <thead>
              <tr>
                <th className="left">Team</th>
                <th className="left">City</th>
                <th>ATT</th>
                <th>MID</th>
                <th>DEF</th>
                <th>OVR</th>
              </tr>
            </thead>
            <tbody>
              {shown.map((t) => (
                <tr key={t.id}>
                  <td className="left team-cell">
                    <Badge name={t.name} primary={t.primaryColor} secondary={t.secondaryColor} size={18} />
                    {t.name}
                  </td>
                  <td className="left muted">
                    {t.city}
                    {shownLeague === 0 && `, ${t.country}`}
                  </td>
                  <td>{t.attack}</td>
                  <td>{t.midfield}</td>
                  <td>{t.defense}</td>
                  <td className="points">{t.overall}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </Screen>
  )
}
