import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../api'
import { Badge, byCountry, ErrorBox, Screen, useLoad } from '../components/common'
import { useSettings } from '../settings'
import { DIFFICULTIES, type Difficulty } from '../display'

// Same rule as the server: the board target depends on the club's rank by rating.
const expectationByRank = (rank: number) => (rank <= 1 ? 'Title' : rank <= 4 ? 'Top 4' : rank <= 9 ? 'Top half' : 'Avoid the drop')

export default function CareerNew() {
  const navigate = useNavigate()
  const [settings, updateSettings] = useSettings()
  const { data: leagues, error: leaguesError } = useLoad(() => api.leagues(), [])
  const playable = useMemo(() => (leagues ?? []).filter((l) => l.isPlayable && l.teamCount >= 4), [leagues])
  const [leagueId, setLeagueId] = useState<number | null>(null)
  // default: the first league in the country order (Bulgaria first), like the other menus
  const activeLeague = leagueId ?? byCountry(playable, (l) => l.country)[0]?.[1][0]?.id ?? null
  const { data: teams } = useLoad(() => (activeLeague ? api.teams(activeLeague) : Promise.resolve([])), [activeLeague])

  const [manager, setManager] = useState(settings.managerName)
  const [saveName, setSaveName] = useState('')
  const [difficulty, setDifficulty] = useState<Difficulty>('Normal')
  const [teamId, setTeamId] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const ranked = useMemo(() => [...(teams ?? [])].sort((a, b) => b.overall - a.overall || a.name.localeCompare(b.name)), [teams])
  const display = useMemo(() => [...ranked].reverse(), [ranked]) // smallest clubs first
  const selected = ranked.find((t) => t.id === teamId)

  const start = async () => {
    if (!manager.trim()) return setError('Enter your manager name.')
    if (!teamId) return setError('Pick a club.')
    setBusy(true)
    try {
      updateSettings({ managerName: manager.trim() })
      const { id } = await api.createCareer(saveName, manager, teamId, difficulty)
      navigate(`/career/${id}`)
    } catch (e) {
      setError((e as Error).message)
      setBusy(false)
    }
  }

  return (
    <Screen title="New career">
      <ErrorBox error={leaguesError} />
      <div className="panel form-grid">
        <label>
          Manager name
          <input value={manager} maxLength={40} onChange={(e) => setManager(e.target.value)} placeholder="e.g. Ivan Petrov" />
        </label>
        <label>
          Save name (optional)
          <input value={saveName} maxLength={60} onChange={(e) => setSaveName(e.target.value)} placeholder="My career" />
        </label>
        <label>
          Difficulty
          <select value={difficulty} onChange={(e) => setDifficulty(e.target.value as Difficulty)}>
            {DIFFICULTIES.map(([d, label]) => (
              <option key={d} value={d}>
                {label}
              </option>
            ))}
          </select>
          <small className="muted">Fixed for the whole career.</small>
        </label>
        {playable.length > 1 && (
          <label>
            League
            <select value={activeLeague ?? ''} onChange={(e) => setLeagueId(Number(e.target.value))}>
              {byCountry(playable, (l) => l.country).map(([country, ls]) => (
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

      <div className="two-col wide-left">
        <div className="panel">
          <h2>Choose your club</h2>
          <p className="muted">The real challenge is to start at a small club. The board's target depends on how big the club is.</p>
          <div className="team-grid">
            {display.map((t) => (
              <button key={t.id} className={`team-tile ${t.id === teamId ? 'selected' : ''}`} onClick={() => setTeamId(t.id)}>
                <Badge name={t.name} primary={t.primaryColor} secondary={t.secondaryColor} size={34} />
                <span className="tile-name">{t.name}</span>
                <span className="muted small">{t.city}</span>
                <span className="tile-meta">Target: {expectationByRank(ranked.indexOf(t))}</span>
              </button>
            ))}
          </div>
        </div>
        <div className="panel">
          {selected ? (
            <>
              <div className="team-detail">
                <Badge name={selected.name} primary={selected.primaryColor} secondary={selected.secondaryColor} size={60} />
                <div>
                  <h2>{selected.name}</h2>
                  <p className="muted">
                    {selected.city} · {selected.stadium}
                  </p>
                </div>
              </div>
              <p>
                Board target: <b>{expectationByRank(ranked.indexOf(selected))}</b>
              </p>
              <button className="btn btn-primary big" disabled={busy} onClick={start}>
                Sign the contract ▶
              </button>
            </>
          ) : (
            <p className="muted">Pick a club from the list.</p>
          )}
          <ErrorBox error={error} />
        </div>
      </div>
    </Screen>
  )
}
