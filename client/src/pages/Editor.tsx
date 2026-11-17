import { useState } from 'react'
import { api, type League, type Team, type TeamInput } from '../api'
import { Badge, ErrorBox, Screen, useLoad } from '../components/common'

const emptyTeam: TeamInput = {
  name: '',
  shortName: '',
  city: '',
  country: 'България',
  stadium: '',
  primaryColor: '#1f6f3f',
  secondaryColor: '#ffffff',
  attack: 60,
  midfield: 60,
  defense: 60,
  leagueId: null,
}

function TeamForm({ initial, leagues, onSave, onCancel }: { initial: TeamInput; leagues: League[]; onSave: (t: TeamInput) => Promise<void>; onCancel: () => void }) {
  const [t, setT] = useState<TeamInput>(initial)
  const [busy, setBusy] = useState(false)
  const set = <K extends keyof TeamInput>(k: K, v: TeamInput[K]) => setT((p) => ({ ...p, [k]: v }))
  const overall = Math.round((t.attack + t.midfield + t.defense) / 3)

  return (
    <form
      className="panel team-form"
      onSubmit={async (e) => {
        e.preventDefault()
        setBusy(true)
        await onSave(t)
        setBusy(false)
      }}
    >
      <div className="form-preview">
        <Badge name={t.name || '?'} primary={t.primaryColor} secondary={t.secondaryColor} size={56} />
        <div>
          <h3>{t.name || 'Нов отбор'}</h3>
          <span className="muted">Обща сила: {overall}</span>
        </div>
      </div>
      <div className="form-grid">
        <label>
          Име*
          <input required maxLength={60} value={t.name} onChange={(e) => set('name', e.target.value)} />
        </label>
        <label>
          Съкращение
          <input maxLength={5} value={t.shortName} onChange={(e) => set('shortName', e.target.value)} />
        </label>
        <label>
          Град
          <input maxLength={60} value={t.city} onChange={(e) => set('city', e.target.value)} />
        </label>
        <label>
          Държава
          <input maxLength={60} value={t.country} onChange={(e) => set('country', e.target.value)} />
        </label>
        <label>
          Стадион
          <input maxLength={80} value={t.stadium} onChange={(e) => set('stadium', e.target.value)} />
        </label>
        <label>
          Лига
          <select value={t.leagueId ?? ''} onChange={(e) => set('leagueId', e.target.value ? Number(e.target.value) : null)}>
            <option value="">— без лига (европейски клуб) —</option>
            {leagues.map((l) => (
              <option key={l.id} value={l.id}>
                {l.name} ({l.country})
              </option>
            ))}
          </select>
        </label>
        <label>
          Основен цвят
          <input type="color" value={t.primaryColor} onChange={(e) => set('primaryColor', e.target.value)} />
        </label>
        <label>
          Втори цвят
          <input type="color" value={t.secondaryColor} onChange={(e) => set('secondaryColor', e.target.value)} />
        </label>
        {(['attack', 'midfield', 'defense'] as const).map((k) => (
          <label key={k}>
            {k === 'attack' ? 'Атака' : k === 'midfield' ? 'Полузащита' : 'Защита'}: <b>{t[k]}</b>
            <input type="range" min={30} max={99} value={t[k]} onChange={(e) => set(k, Number(e.target.value))} />
          </label>
        ))}
      </div>
      <div className="row-buttons">
        <button className="btn btn-primary" disabled={busy} type="submit">
          Запази
        </button>
        <button className="btn" type="button" onClick={onCancel}>
          Отказ
        </button>
      </div>
    </form>
  )
}

function LeagueForm({ onSave }: { onSave: (l: Omit<League, 'id' | 'teamCount'>) => Promise<void> }) {
  const [name, setName] = useState('')
  const [country, setCountry] = useState('България')
  const [tier, setTier] = useState(2)
  const [isPlayable, setPlayable] = useState(true)
  return (
    <form
      className="form-grid"
      onSubmit={async (e) => {
        e.preventDefault()
        await onSave({ name, country, tier, isPlayable })
        setName('')
      }}
    >
      <label>
        Име на лигата
        <input required value={name} onChange={(e) => setName(e.target.value)} placeholder="напр. Втора лига" />
      </label>
      <label>
        Държава
        <input required value={country} onChange={(e) => setCountry(e.target.value)} />
      </label>
      <label>
        Ниво
        <input type="number" min={1} max={10} value={tier} onChange={(e) => setTier(Number(e.target.value))} />
      </label>
      <label className="checkbox">
        <input type="checkbox" checked={isPlayable} onChange={(e) => setPlayable(e.target.checked)} />
        Може да се играе в кариера (нужни са поне 4 отбора, препоръчително четен брой)
      </label>
      <button className="btn btn-primary" type="submit">
        Добави лига
      </button>
    </form>
  )
}

export default function Editor() {
  const { data: leagues, reload: reloadLeagues } = useLoad(() => api.leagues(), [])
  const { data: teams, error: loadError, reload: reloadTeams } = useLoad(() => api.teams(), [])
  const [filter, setFilter] = useState<string>('all')
  const [editing, setEditing] = useState<Team | 'new' | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const reloadAll = () => Promise.all([reloadLeagues(), reloadTeams()])

  const guard = async (fn: () => Promise<unknown>, ok: string) => {
    setError(null)
    setNotice(null)
    try {
      await fn()
      setNotice(ok)
      await reloadAll()
      return true
    } catch (e) {
      setError((e as Error).message)
      return false
    }
  }

  const shown = (teams ?? []).filter((t) => (filter === 'all' ? true : filter === 'none' ? t.leagueId === null : t.leagueId === Number(filter)))

  return (
    <Screen title="Редактор на отбори и лиги">
      <ErrorBox error={loadError ?? error} />
      {notice && <div className="notice">{notice}</div>}

      {editing && (
        <TeamForm
          key={editing === 'new' ? 'new' : editing.id}
          initial={editing === 'new' ? { ...emptyTeam, leagueId: leagues?.[0]?.id ?? null } : { ...editing }}
          leagues={leagues ?? []}
          onCancel={() => setEditing(null)}
          onSave={async (t) => {
            const ok = await guard(
              () => (editing === 'new' ? api.createTeam(t) : api.updateTeam(editing.id, t)),
              editing === 'new' ? `Отборът „${t.name}“ е добавен.` : 'Промените са запазени.',
            )
            if (ok) setEditing(null)
          }}
        />
      )}

      <div className="two-col wide-left">
        <div className="panel">
          <div className="panel-head">
            <h2>Отбори ({shown.length})</h2>
            <select value={filter} onChange={(e) => setFilter(e.target.value)}>
              <option value="all">Всички</option>
              {leagues?.map((l) => (
                <option key={l.id} value={l.id}>
                  {l.name}
                </option>
              ))}
              <option value="none">Европейски клубове (без лига)</option>
            </select>
            <button className="btn btn-primary" onClick={() => setEditing('new')}>
              + Нов отбор
            </button>
          </div>
          <table className="table">
            <thead>
              <tr>
                <th className="left">Отбор</th>
                <th className="left">Лига / държава</th>
                <th>А</th>
                <th>П</th>
                <th>З</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {shown.map((t) => (
                <tr key={t.id}>
                  <td className="left team-cell">
                    <Badge name={t.name} primary={t.primaryColor} secondary={t.secondaryColor} size={18} />
                    {t.name}
                  </td>
                  <td className="left muted">{t.leagueName ?? t.country}</td>
                  <td>{t.attack}</td>
                  <td>{t.midfield}</td>
                  <td>{t.defense}</td>
                  <td className="actions">
                    <button className="btn btn-small" onClick={() => setEditing(t)}>
                      Редактирай
                    </button>
                    <button
                      className="btn btn-small btn-danger"
                      onClick={() => confirm(`Да изтрия ли „${t.name}“?`) && guard(() => api.deleteTeam(t.id), 'Отборът е изтрит.')}
                    >
                      ✕
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="panel">
          <h2>Лиги</h2>
          <ul className="league-list">
            {leagues?.map((l) => (
              <li key={l.id}>
                <b>{l.name}</b> <span className="muted">
                  {l.country} · ниво {l.tier} · {l.teamCount} отбора {l.isPlayable ? '· играема' : ''}
                </span>
                {l.teamCount === 0 && (
                  <button className="btn btn-small btn-danger" onClick={() => guard(() => api.deleteLeague(l.id), 'Лигата е изтрита.')}>
                    ✕
                  </button>
                )}
              </li>
            ))}
          </ul>
          <h3>Нова лига</h3>
          <LeagueForm onSave={async (l) => void (await guard(() => api.createLeague(l), `Лигата „${l.name}“ е добавена.`))} />
          <p className="muted small">
            Съвет: можете да добавяте отбори и директно във файла <code>server/FootballCards.Api/Data/Seed/seed.json</code> — при
            следващо стартиране на сървъра липсващите отбори се добавят автоматично.
          </p>
        </div>
      </div>
    </Screen>
  )
}
