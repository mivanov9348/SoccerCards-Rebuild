import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, seasonLabel } from '../api'
import { Badge, ErrorBox, Screen, useLoad } from '../components/common'

export default function LoadGame() {
  const navigate = useNavigate()
  const { data: careers, error: loadError, reload } = useLoad(() => api.careers(), [])
  const [error, setError] = useState<string | null>(null)

  const rename = async (id: number, current: string) => {
    const name = prompt('Ново име на записа:', current)
    if (!name) return
    try {
      await api.renameCareer(id, name)
      await reload()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const remove = async (id: number, name: string) => {
    if (!confirm(`Да изтрия ли записа „${name}“? Това не може да бъде отменено.`)) return
    try {
      await api.deleteCareer(id)
      await reload()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  return (
    <Screen title="Зареди игра">
      <ErrorBox error={loadError ?? error} />
      <p className="muted">Кариерите се записват автоматично в базата данни след всеки мач и всяко действие.</p>
      {careers && careers.length === 0 && (
        <div className="panel">
          <p>Няма запазени кариери.</p>
          <button className="btn btn-primary" onClick={() => navigate('/career/new')}>
            Започни нова кариера
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
                {c.managerName} · {c.team} · {c.year ? seasonLabel(c.year) : ''} · седмица {c.week}/{c.totalWeeks}
                {c.status === 'Fired' && ' · уволнен'}
              </span>
              <span className="muted small">Последна промяна: {new Date(c.updatedAt).toLocaleString('bg-BG')}</span>
            </div>
            <div className="row-buttons">
              <button className="btn btn-primary" onClick={() => navigate(`/career/${c.id}`)}>
                Зареди
              </button>
              <button className="btn" onClick={() => rename(c.id, c.saveName)}>
                Преименувай
              </button>
              <button className="btn btn-danger" onClick={() => remove(c.id, c.saveName)}>
                Изтрий
              </button>
            </div>
          </div>
        ))}
      </div>
    </Screen>
  )
}
