import { useRef, useState } from 'react'
import { api, type NamePack } from '../api'
import { ErrorBox, useLoad } from './common'

/**
 * Name packs (1.3): JSON files that rename clubs, leagues and cups in every save. The game ships fictional names; players can
 * make and share their own packs. Import a file, switch packs on and off, export the current names as a template.
 */
export function NamePacksPanel() {
  const { data, error: loadError, setData } = useLoad(() => api.namePacks(), [])
  const [error, setError] = useState<string | null>(null)
  const [note, setNote] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const input = useRef<HTMLInputElement>(null)

  const run = async (fn: () => Promise<{ packs: NamePack[] }>, done?: string) => {
    setBusy(true)
    setError(null)
    setNote(null)
    try {
      const r = await fn()
      setData({ folder: data?.folder ?? '', packs: r.packs })
      if (done) setNote(done)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const importFile = async (file: File) => {
    const text = await file.text()
    await run(() => api.importNamePack(file.name, text), `Imported ${file.name} — the names are applied to every save.`)
  }

  const exportNames = () =>
    run(async () => {
      const r = await api.exportNamePack()
      // also offer the file as a download (the copy in the name-packs folder stays switched off)
      try {
        const url = URL.createObjectURL(new Blob([r.content], { type: 'application/json' }))
        const a = document.createElement('a')
        a.href = url
        a.download = r.file.replace(/\.off$/, '')
        a.click()
        window.setTimeout(() => URL.revokeObjectURL(url), 2000)
      } catch {
        /* the copy in the folder is enough */
      }
      return r
    }, 'Exported the current names. A copy is in the name-packs folder (switched off) — edit it, then import it or switch it on.')

  return (
    <div className="name-packs">
      <p className="muted small">
        All clubs, leagues and cups in Dugout Deck are fictional. A <b>name pack</b> is a JSON file that renames them in every save
        (careers in progress too). Packs are kept in <code>{data?.folder ?? '…'}</code>, so game updates do not remove them. Clubs you
        changed in the Team &amp; league editor keep their editor names.
      </p>
      <ErrorBox error={loadError ?? error} />
      {note && <p className="good small">{note}</p>}
      <div className="row-buttons">
        <button className="btn btn-primary" disabled={busy} onClick={() => input.current?.click()}>
          Import a name pack…
        </button>
        <button className="btn" disabled={busy} onClick={exportNames}>
          Export current names (template)
        </button>
        <button
          className="btn btn-ghost"
          disabled={busy || !data?.packs.some((p) => p.enabled)}
          onClick={() => confirm('Switch off every name pack and go back to the fictional names?') && run(() => api.resetNamePacks(), 'Back to the original names.')}
        >
          Reset to original names
        </button>
        <input
          ref={input}
          type="file"
          accept=".json,application/json"
          hidden
          onChange={(e) => {
            const f = e.target.files?.[0]
            e.target.value = ''
            if (f) void importFile(f)
          }}
        />
      </div>
      {data && data.packs.length > 0 ? (
        <table className="table name-pack-list">
          <thead>
            <tr>
              <th className="left">Pack</th>
              <th>Clubs</th>
              <th>Leagues</th>
              <th>Cups</th>
              <th>On</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {data.packs.map((p) => (
              <tr key={p.file} className={p.error ? 'bad' : ''}>
                <td className="left">
                  <b>{p.name}</b>
                  {p.author && <span className="muted small"> by {p.author}</span>}
                  <div className="muted small">{p.error ?? p.file}</div>
                </td>
                <td>{p.teams}</td>
                <td>{p.leagues}</td>
                <td>{p.competitions}</td>
                <td>
                  <input type="checkbox" checked={p.enabled} disabled={busy || !!p.error} onChange={(e) => run(() => api.enableNamePack(p.file, e.target.checked))} />
                </td>
                <td>
                  <button className="btn btn-small btn-ghost" disabled={busy} onClick={() => confirm(`Delete ${p.name}?`) && run(() => api.deleteNamePack(p.file))}>
                    Delete
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : (
        <p className="muted small">No name packs yet.</p>
      )}
    </div>
  )
}
