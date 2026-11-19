import { Screen } from '../components/common'
import { useSettings } from '../settings'

export default function SettingsPage() {
  const [s, update] = useSettings()
  return (
    <Screen title="Settings">
      <div className="panel form-grid settings">
        <label>
          Computer flip speed
          <select value={s.aiDelay} onChange={(e) => update({ aiDelay: Number(e.target.value) })}>
            <option value={1600}>Slow</option>
            <option value={900}>Normal</option>
            <option value={400}>Fast</option>
            <option value={100}>Very fast</option>
          </select>
          <small className="muted">How long the computer waits before flipping each card.</small>
        </label>
        <label className="checkbox">
          <input type="checkbox" checked={s.autoMiniGames} onChange={(e) => update({ autoMiniGames: e.target.checked })} />
          Decide mini-games automatically (corners, free kicks, Big Chances, penalties)
        </label>
        <label>
          Manager name
          <input value={s.managerName} onChange={(e) => update({ managerName: e.target.value })} />
        </label>
        <p className="muted small">Settings are stored in your browser.</p>
      </div>
    </Screen>
  )
}
