import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, type Side, type Team } from '../api'
import { Badge, byCountry, ErrorBox, Screen, useLoad } from '../components/common'
import { loadSettings } from '../settings'

function TeamPicker({ label, teams, value, onChange }: { label: string; teams: Team[]; value: number | null; onChange: (id: number) => void }) {
  const groups = [...new Set(teams.map((t) => t.leagueName ?? 'Other European clubs'))]
  const countryOf = (g: string) => (g === 'Other European clubs' ? 'Other' : (teams.find((t) => t.leagueName === g)?.country ?? 'Other'))
  const [filter, setFilter] = useState<string>('Bulgarian Premier Division')
  const active = groups.includes(filter) ? filter : groups[0]
  const shown = teams.filter((t) => (t.leagueName ?? 'Other European clubs') === active)
  const selected = teams.find((t) => t.id === value)
  return (
    <div className="panel picker">
      <h2>{label}</h2>
      <select value={active} onChange={(e) => setFilter(e.target.value)}>
        {byCountry(groups, countryOf).map(([country, gs]) => (
          <optgroup key={country} label={country}>
            {gs.map((g) => (
              <option key={g} value={g}>
                {g}
              </option>
            ))}
          </optgroup>
        ))}
      </select>
      <div className="team-list">
        {shown.map((t) => (
          <button key={t.id} className={`team-option ${t.id === value ? 'selected' : ''}`} onClick={() => onChange(t.id)}>
            <Badge name={t.name} primary={t.primaryColor} secondary={t.secondaryColor} size={22} />
            <span>{t.name}</span>
            <span className="muted small">{t.city}</span>
          </button>
        ))}
      </div>
      {selected && (
        <div className="team-detail">
          <Badge name={selected.name} primary={selected.primaryColor} secondary={selected.secondaryColor} size={48} />
          <div>
            <h3>{selected.name}</h3>
            <p className="muted">
              {selected.city}, {selected.country} · {selected.stadium}
            </p>
          </div>
        </div>
      )}
    </div>
  )
}

export default function FriendlySetup() {
  const navigate = useNavigate()
  const { data: teams, error: loadError } = useLoad(() => api.teams(), [])
  const [home, setHome] = useState<number | null>(null)
  const [away, setAway] = useState<number | null>(null)
  const [side, setSide] = useState<Side>('Home')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const start = async () => {
    if (!home || !away) return setError('Pick a home team and an away team.')
    if (home === away) return setError('Pick two different teams.')
    setBusy(true)
    try {
      const m = await api.createFriendly(home, away, side, loadSettings().difficulty)
      navigate(`/match/${m.id}`)
    } catch (e) {
      setError((e as Error).message)
      setBusy(false)
    }
  }

  return (
    <Screen title="Friendly match">
      <ErrorBox error={loadError} />
      <div className="two-col">
        <TeamPicker label="Home team" teams={teams ?? []} value={home} onChange={setHome} />
        <TeamPicker label="Away team" teams={teams ?? []} value={away} onChange={setAway} />
      </div>
      <div className="panel setup-bar">
        <label>
          You play as
          <select value={side} onChange={(e) => setSide(e.target.value as Side)}>
            <option value="Home">the home team</option>
            <option value="Away">the away team</option>
          </select>
        </label>
        <span className="muted small">The computer plays the other team.</span>
        <button className="btn btn-primary" disabled={busy} onClick={start}>
          Kick off ▶
        </button>
      </div>
      <ErrorBox error={error} />
    </Screen>
  )
}
