import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, difficultyNames } from '../api'
import { Badge, ErrorBox, RatingBar, Screen, useLoad } from '../components/common'
import { useSettings } from '../settings'

const expectationByRank = (rank: number) =>
  rank <= 1 ? 'Титла' : rank <= 4 ? 'Топ 4' : rank <= 9 ? 'Горна половина' : 'Оцеляване'

export default function CareerNew() {
  const navigate = useNavigate()
  const [settings, updateSettings] = useSettings()
  const { data: leagues, error: leaguesError } = useLoad(() => api.leagues(), [])
  const playable = useMemo(() => (leagues ?? []).filter((l) => l.isPlayable && l.teamCount >= 4), [leagues])
  const [leagueId, setLeagueId] = useState<number | null>(null)
  const activeLeague = leagueId ?? playable[0]?.id ?? null
  const { data: teams } = useLoad(() => (activeLeague ? api.teams(activeLeague) : Promise.resolve([])), [activeLeague])

  const [manager, setManager] = useState(settings.managerName)
  const [saveName, setSaveName] = useState('')
  const [difficulty, setDifficulty] = useState(settings.difficulty)
  const [teamId, setTeamId] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  // отборите са подредени по сила; показваме първо най-малките клубове
  const ranked = useMemo(() => [...(teams ?? [])].sort((a, b) => b.overall - a.overall || a.name.localeCompare(b.name)), [teams])
  const display = useMemo(() => [...ranked].reverse(), [ranked])
  const selected = ranked.find((t) => t.id === teamId)

  const start = async () => {
    if (!manager.trim()) return setError('Въведете име на треньора.')
    if (!teamId) return setError('Изберете отбор.')
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
    <Screen title="Нова кариера">
      <ErrorBox error={leaguesError} />
      <div className="panel form-grid">
        <label>
          Име на треньора
          <input value={manager} maxLength={40} onChange={(e) => setManager(e.target.value)} placeholder="напр. Иван Петров" />
        </label>
        <label>
          Име на записа (по желание)
          <input value={saveName} maxLength={60} onChange={(e) => setSaveName(e.target.value)} placeholder="Моята кариера" />
        </label>
        <label>
          Трудност
          <select value={difficulty} onChange={(e) => setDifficulty(Number(e.target.value))}>
            {difficultyNames.map((d, i) => (
              <option key={d} value={i}>
                {d}
              </option>
            ))}
          </select>
        </label>
        {playable.length > 1 && (
          <label>
            Лига
            <select value={activeLeague ?? ''} onChange={(e) => setLeagueId(Number(e.target.value))}>
              {playable.map((l) => (
                <option key={l.id} value={l.id}>
                  {l.name} ({l.country})
                </option>
              ))}
            </select>
          </label>
        )}
      </div>

      <div className="two-col wide-left">
        <div className="panel">
          <h2>Изберете клуб</h2>
          <p className="muted">Истинското предизвикателство е да започнете с малък клуб. Очакванията на борда зависят от силата на отбора.</p>
          <div className="team-grid">
            {display.map((t) => {
              const rank = ranked.indexOf(t)
              return (
                <button key={t.id} className={`team-tile ${t.id === teamId ? 'selected' : ''}`} onClick={() => setTeamId(t.id)}>
                  <Badge name={t.name} primary={t.primaryColor} secondary={t.secondaryColor} size={34} />
                  <span className="tile-name">{t.name}</span>
                  <span className="muted small">{t.city}</span>
                  <span className="tile-meta">
                    <b>{t.overall}</b> · {expectationByRank(rank)}
                  </span>
                </button>
              )
            })}
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
                    {selected.city} · стадион „{selected.stadium}“
                  </p>
                </div>
              </div>
              <RatingBar label="Атака" value={selected.attack} />
              <RatingBar label="Полузащита" value={selected.midfield} />
              <RatingBar label="Защита" value={selected.defense} />
              <p>
                Очакване на борда: <b>{expectationByRank(ranked.indexOf(selected))}</b>
              </p>
              <button className="btn btn-primary big" disabled={busy} onClick={start}>
                Подпиши договора ▶
              </button>
            </>
          ) : (
            <p className="muted">Изберете клуб от списъка.</p>
          )}
          <ErrorBox error={error} />
        </div>
      </div>
    </Screen>
  )
}
