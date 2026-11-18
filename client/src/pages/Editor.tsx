import { useState } from 'react'
import { api, type League, type Team, type TeamInput } from '../api'
import { Badge, ErrorBox, Screen, useLoad } from '../components/common'

const emptyTeam: TeamInput = {
  name: '',
  shortName: '',
  city: '',
  country: 'Bulgaria',
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
          <h3>{t.name || 'New team'}</h3>
          <span className="muted">Overall rating: {overall}</span>
        </div>
      </div>
      <div className="form-grid">
        <label>
          Name*
          <input required maxLength={60} value={t.name} onChange={(e) => set('name', e.target.value)} />
        </label>
        <label>
          Short name
          <input maxLength={5} value={t.shortName} onChange={(e) => set('shortName', e.target.value)} />
        </label>
        <label>
          City
          <input maxLength={60} value={t.city} onChange={(e) => set('city', e.target.value)} />
        </label>
        <label>
          Country
          <input maxLength={60} value={t.country} onChange={(e) => set('country', e.target.value)} />
        </label>
        <label>
          Stadium
          <input maxLength={80} value={t.stadium} onChange={(e) => set('stadium', e.target.value)} />
        </label>
        <label>
          League
          <select value={t.leagueId ?? ''} onChange={(e) => set('leagueId', e.target.value ? Number(e.target.value) : null)}>
            <option value="">— no league (European club) —</option>
            {leagues.map((l) => (
              <option key={l.id} value={l.id}>
                {l.name} ({l.country})
              </option>
            ))}
          </select>
        </label>
        <label>
          Primary color
          <input type="color" value={t.primaryColor} onChange={(e) => set('primaryColor', e.target.value)} />
        </label>
        <label>
          Secondary color
          <input type="color" value={t.secondaryColor} onChange={(e) => set('secondaryColor', e.target.value)} />
        </label>
        {(['attack', 'midfield', 'defense'] as const).map((k) => (
          <label key={k}>
            {k === 'attack' ? 'Attack' : k === 'midfield' ? 'Midfield' : 'Defense'}: <b>{t[k]}</b>
            <input type="range" min={30} max={99} value={t[k]} onChange={(e) => set(k, Number(e.target.value))} />
          </label>
        ))}
      </div>
      <div className="row-buttons">
        <button className="btn btn-primary" disabled={busy} type="submit">
          Save
        </button>
        <button className="btn" type="button" onClick={onCancel}>
          Cancel
        </button>
      </div>
    </form>
  )
}

function LeagueForm({ onSave }: { onSave: (l: Omit<League, 'id' | 'teamCount'>) => Promise<void> }) {
  const [name, setName] = useState('')
  const [country, setCountry] = useState('Bulgaria')
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
        League name
        <input required value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Second League" />
      </label>
      <label>
        Country
        <input required value={country} onChange={(e) => setCountry(e.target.value)} />
      </label>
      <label>
        Tier
        <input type="number" min={1} max={10} value={tier} onChange={(e) => setTier(Number(e.target.value))} />
      </label>
      <label className="checkbox">
        <input type="checkbox" checked={isPlayable} onChange={(e) => setPlayable(e.target.checked)} />
        Playable in career mode (needs at least 4 teams, an even number is best)
      </label>
      <button className="btn btn-primary" type="submit">
        Add league
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
    <Screen title="Team & league editor">
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
              editing === 'new' ? `Team "${t.name}" added.` : 'Changes saved.',
            )
            if (ok) setEditing(null)
          }}
        />
      )}

      <div className="two-col wide-left">
        <div className="panel">
          <div className="panel-head">
            <h2>Teams ({shown.length})</h2>
            <select value={filter} onChange={(e) => setFilter(e.target.value)}>
              <option value="all">All</option>
              {leagues?.map((l) => (
                <option key={l.id} value={l.id}>
                  {l.name}
                </option>
              ))}
              <option value="none">European clubs (no league)</option>
            </select>
            <button className="btn btn-primary" onClick={() => setEditing('new')}>
              + New team
            </button>
          </div>
          <table className="table">
            <thead>
              <tr>
                <th className="left">Team</th>
                <th className="left">League / country</th>
                <th>ATT</th>
                <th>MID</th>
                <th>DEF</th>
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
                      Edit
                    </button>
                    <button
                      className="btn btn-small btn-danger"
                      onClick={() => confirm(`Delete "${t.name}"?`) && guard(() => api.deleteTeam(t.id), 'Team deleted.')}
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
          <h2>Leagues</h2>
          <ul className="league-list">
            {leagues?.map((l) => (
              <li key={l.id}>
                <b>{l.name}</b> <span className="muted">
                  {l.country} · tier {l.tier} · {l.teamCount} teams {l.isPlayable ? '· playable' : ''}
                </span>
                {l.teamCount === 0 && (
                  <button className="btn btn-small btn-danger" onClick={() => guard(() => api.deleteLeague(l.id), 'League deleted.')}>
                    ✕
                  </button>
                )}
              </li>
            ))}
          </ul>
          <h3>New league</h3>
          <LeagueForm onSave={async (l) => void (await guard(() => api.createLeague(l), `League "${l.name}" added.`))} />
          <p className="muted small">
            Tip: you can also add teams directly in <code>server/FootballCards.Api/Data/Seed/seed.json</code> — missing teams
            are added automatically the next time the server starts.
          </p>
        </div>
      </div>
    </Screen>
  )
}
