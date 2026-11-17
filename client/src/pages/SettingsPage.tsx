import { difficultyNames, tacticNames, type Tactic } from '../api'
import { Screen } from '../components/common'
import { useSettings } from '../settings'

export default function SettingsPage() {
  const [s, update] = useSettings()
  return (
    <Screen title="Настройки">
      <div className="panel form-grid settings">
        <label>
          Трудност по подразбиране
          <select value={s.difficulty} onChange={(e) => update({ difficulty: Number(e.target.value) })}>
            {difficultyNames.map((d, i) => (
              <option key={d} value={i}>
                {d}
              </option>
            ))}
          </select>
          <small className="muted">
            На „Трудно“ компютърът играе по-умно и съперникът е с +4 към рейтингите; на „Лесно“ — с −4 и допуска повече грешки.
          </small>
        </label>
        <label>
          Тактика по подразбиране
          <select value={s.defaultTactic} onChange={(e) => update({ defaultTactic: e.target.value as Tactic })}>
            {Object.entries(tacticNames).map(([k, v]) => (
              <option key={k} value={k}>
                {v}
              </option>
            ))}
          </select>
        </label>
        <label>
          Име на треньора
          <input value={s.managerName} onChange={(e) => update({ managerName: e.target.value })} />
        </label>
        <label className="checkbox">
          <input type="checkbox" checked={s.showHints} onChange={(e) => update({ showHints: e.target.checked })} />
          Показвай шансовете върху картите (подсказки)
        </label>
        <p className="muted small">Настройките се пазят в браузъра.</p>
      </div>
    </Screen>
  )
}
