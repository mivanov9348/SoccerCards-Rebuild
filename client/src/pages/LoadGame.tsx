import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, seasonLabel } from '../api'
import { Badge, ErrorBox, Screen, useLoad } from '../components/common'

export default function LoadGame() {
  const navigate = useNavigate()
  const { data: careers, error: loadError, reload } = useLoad(() => api.careers(), [])
  const [error, setError] = useState<string | null>(null)
  const { data: info } = useLoad(() => api.info(), [])

  const rename = async (id: number, current: string) => {
    const name = prompt('New save name:', current)
    if (!name) return
    try {
      await api.renameCareer(id, name)
      await reload()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const remove = async (id: number, name: string) => {
    if (!confirm(`Delete the save "${name}"? This cannot be undone.`)) return
    try {
      await api.deleteCareer(id)
      await reload()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  return (
    <Screen title="Load game">
      <ErrorBox error={loadError ?? error} />
      <p className="muted">
        Careers are saved automatically after every card and every match — close the game any time and load it again here.
        {info && (
          <>
            <br />
            Save file: <code>{info.saveFile}</code>
          </>
        )}
      </p>
      {careers && careers.length === 0 && (
        <div className="panel">
          <p>No saved careers yet.</p>
          <button className="btn btn-primary" onClick={() => navigate('/career/new')}>
            Start a new career
          </button>
        </div>
      )}
      <div className="save-list">
        {careers?.map((c) => (
          <div key={c.id} className="panel save-slot">
            <Badge name={c.team} primary={c.teamColor} secondary={c.teamColor2} size={44} />
            <div className="save-info">
              <b>{c.saveName}</b>
              <span className="muted">
                {c.managerName} · {c.team} · {c.year ? seasonLabel(c.year) : ''} · week {c.week}/{c.totalWeeks}
                {c.status === 'Fired' && ' · sacked'}
              </span>
              <span className="muted small">Last played: {new Date(c.updatedAt).toLocaleString('en-GB')}</span>
            </div>
            <div className="row-buttons">
              <button className="btn btn-primary" onClick={() => navigate(`/career/${c.id}`)}>
                Load
              </button>
              <button className="btn" onClick={() => rename(c.id, c.saveName)}>
                Rename
              </button>
              <button className="btn btn-danger" onClick={() => remove(c.id, c.saveName)}>
                Delete
              </button>
            </div>
          </div>
        ))}
      </div>
    </Screen>
  )
}
