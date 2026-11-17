import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, tacticNames, type Side, type Tactic, type Team } from '../api'
import { Badge, ErrorBox, RatingBar, Screen, useLoad } from '../components/common'
import { useSettings } from '../settings'

function TeamPicker({ label, teams, value, onChange }: { label: string; teams: Team[]; value: number | null; onChange: (id: number) => void }) {
  const [filter, setFilter] = useState<string>('BG')
  const shown = teams.filter((t) => (filter === 'BG' ? t.leagueId !== null : t.leagueId === null))
  const selected = teams.find((t) => t.id === value)
  return (
    <div className="panel picker">
      <h2>{label}</h2>
      <div className="tabs small">
        <button className={filter === 'BG' ? 'active' : ''} onClick={() => setFilter('BG')}>
          Първа лига
        </button>
        <button className={filter === 'EU' ? 'active' : ''} onClick={() => setFilter('EU')}>
          Европейски клубове
        </button>
      </div>
      <div className="team-list">
        {shown.map((t) => (
          <button key={t.id} className={`team-option ${t.id === value ? 'selected' : ''}`} onClick={() => onChange(t.id)}>
            <Badge name={t.name} primary={t.primaryColor} secondary={t.secondaryColor} size={22} />
            <span>{t.name}</span>
            <b>{t.overall}</b>
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
            <RatingBar label="Атака" value={selected.attack} />
            <RatingBar label="Полузащита" value={selected.midfield} />
            <RatingBar label="Защита" value={selected.defense} />
          </div>
        </div>
      )}
    </div>
  )
}

export default function FriendlySetup() {
  const navigate = useNavigate()
  const [settings] = useSettings()
  const { data: teams, error: loadError } = useLoad(() => api.teams(), [])
  const [home, setHome] = useState<number | null>(null)
  const [away, setAway] = useState<number | null>(null)
  const [side, setSide] = useState<Side>('Home')
  const [tactic, setTactic] = useState<Tactic>(settings.defaultTactic)
  const [difficulty, setDifficulty] = useState(settings.difficulty)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const sorted = useMemo(() => teams ?? [], [teams])

  const start = async () => {
    if (!home || !away) return setError('Изберете домакин и гост.')
    if (home === away) return setError('Изберете два различни отбора.')
    setBusy(true)
    try {
      const m = await api.createFriendly(home, away, side, tactic, difficulty)
      navigate(`/match/${m.id}`)
    } catch (e) {
      setError((e as Error).message)
      setBusy(false)
    }
  }

  return (
    <Screen title="Приятелски мач">
      <ErrorBox error={loadError} />
      <div className="two-col">
        <TeamPicker label="Домакин" teams={sorted} value={home} onChange={setHome} />
        <TeamPicker label="Гост" teams={sorted} value={away} onChange={setAway} />
      </div>
      <div className="panel setup-bar">
        <label>
          Вие управлявате
          <select value={side} onChange={(e) => setSide(e.target.value as Side)}>
            <option value="Home">Домакина</option>
            <option value="Away">Госта</option>
          </select>
        </label>
        <label>
          Тактика
          <select value={tactic} onChange={(e) => setTactic(e.target.value as Tactic)}>
            {Object.entries(tacticNames).map(([k, v]) => (
              <option key={k} value={k}>
                {v}
              </option>
            ))}
          </select>
        </label>
        <label>
          Трудност
          <select value={difficulty} onChange={(e) => setDifficulty(Number(e.target.value))}>
            <option value={0}>Лесно</option>
            <option value={1}>Нормално</option>
            <option value={2}>Трудно</option>
          </select>
        </label>
        <button className="btn btn-primary" disabled={busy} onClick={start}>
          Започни мача ▶
        </button>
      </div>
      <ErrorBox error={error} />
    </Screen>
  )
}
