import { useState } from 'react'
import { api, seasonLabel } from '../api'
import { Badge, ErrorBox, Screen, StandingsTable, useLoad } from '../components/common'

export default function Tables() {
  const { data: careers, error } = useLoad(() => api.careers(), [])
  const { data: teams } = useLoad(() => api.teams(), [])
  const [careerId, setCareerId] = useState<number | null>(null)
  const active = careerId ?? careers?.[0]?.id ?? null
  const { data: career } = useLoad(() => (active ? api.career(active) : Promise.resolve(null)), [active])
  const [group, setGroup] = useState<'BG' | 'EU'>('BG')

  const shown = (teams ?? []).filter((t) => (group === 'BG' ? t.leagueId !== null : t.leagueId === null))

  return (
    <Screen title="Tables & teams">
      <ErrorBox error={error} />
      <div className="two-col">
        <div className="panel">
          <h2>Career league table</h2>
          {careers && careers.length === 0 && <p className="muted">Start a career to see a league table.</p>}
          {careers && careers.length > 0 && (
            <label className="inline">
              Save
              <select value={active ?? ''} onChange={(e) => setCareerId(Number(e.target.value))}>
                {careers.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.saveName} ({c.team}, {c.year ? seasonLabel(c.year) : ''})
                  </option>
                ))}
              </select>
            </label>
          )}
          {career && <StandingsTable rows={career.table} highlight={career.team.id} />}
        </div>
        <div className="panel">
          <h2>Team ratings</h2>
          <div className="tabs small">
            <button className={group === 'BG' ? 'active' : ''} onClick={() => setGroup('BG')}>
              First League
            </button>
            <button className={group === 'EU' ? 'active' : ''} onClick={() => setGroup('EU')}>
              European clubs
            </button>
          </div>
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
                    {group === 'EU' && `, ${t.country}`}
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
