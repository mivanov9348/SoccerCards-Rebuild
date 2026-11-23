import { useMemo, useState, type ReactNode } from 'react'
import { api, type MatchView, type SetupCard, type Tactic, type TacticEditorSetup } from '../api'
import { ErrorBox, useLoad } from './common'
import { CardThumb, iconFor } from './GameCard'
import { TacticCoach } from './TacticCoach'
import { saveSettings, useSettings } from '../settings'

const rarityClass = (r: string) => `rarity-${r.toLowerCase()}`

const copy = (t: Tactic, handSize: number): Tactic => ({ field: [...t.field], hand: t.hand.slice(0, handSize), modifiers: [...t.modifiers] })

/** The state of a tactic being built: points, owned copies, adding and removing cards. */
export function useTacticEditor(setup: TacticEditorSetup, initial: Tactic) {
  const [tactic, setTactic] = useState<Tactic>(() => copy(initial, setup.handSize))
  const [error, setError] = useState<string | null>(null)
  const byCode = useMemo(() => Object.fromEntries(setup.cards.map((c) => [c.code, c])), [setup.cards])
  const cost = [...tactic.field, ...tactic.hand, ...tactic.modifiers].reduce((s, c) => s + (byCode[c]?.cost ?? 0), 0)
  const left = setup.budget - cost
  const used = (code: string) => [...tactic.field, ...tactic.hand].filter((c) => c === code).length
  const available = (c: SetupCard) => c.owned == null || used(c.code) < c.owned

  const add = (c: SetupCard) => {
    setError(null)
    if (c.cost > left) return setError(`Not enough tactic points for ${c.name}.`)
    if (!available(c)) return setError(`You don't own more ${c.name} cards.`)
    setTactic((t) => {
      if (c.kind === 'Field') return t.field.length >= setup.fieldCards ? t : { ...t, field: [...t.field, c.code] }
      if (c.kind === 'Hand') return t.hand.length >= setup.handSize ? t : { ...t, hand: [...t.hand, c.code] }
      if (t.modifiers.length >= setup.maxModifiers || t.modifiers.includes(c.code)) return t
      return { ...t, modifiers: [...t.modifiers, c.code] }
    })
  }
  const remove = (list: keyof Tactic, i: number) => setTactic((t) => ({ ...t, [list]: t[list].filter((_, j) => j !== i) }))
  const load = (t: Tactic) => {
    setError(null)
    setTactic(copy(t, setup.handSize))
  }
  return { setup, tactic, load, byCode, cost, left, used, available, add, remove, error, setError }
}

type Editor = ReturnType<typeof useTacticEditor>

/** Points left (with an optional note, e.g. the away budget). */
export function TacticBudget({ ed, note }: { ed: Editor; note?: ReactNode }) {
  return (
    <div className={`tb-budget ${ed.left < 0 ? 'over' : ''}`}>
      <span className="muted small">Tactic points left</span>
      <b>
        {ed.left} / {ed.setup.budget}
      </b>
      <span className="muted small">Hand cards: up to {ed.setup.handSize}</span>
      {note}
    </div>
  )
}

/** The chosen cards (click to remove). */
export function TacticChosen({ ed }: { ed: Editor }) {
  const { tactic, setup, byCode, remove } = ed
  return (
    <div className="panel tb-chosen">
      <div>
        <h3>
          Field cards ({tactic.field.length}/{setup.fieldCards})
        </h3>
        <div className="chips">
          {tactic.field.map((c, i) => (
            <button key={i} className="chip" onClick={() => remove('field', i)} title="Remove">
              {iconFor(c)} {byCode[c]?.name ?? c} ✕
            </button>
          ))}
        </div>
      </div>
      <div>
        <h3>
          Hand ({tactic.hand.length}/{setup.handSize})
        </h3>
        <div className="chips">
          {tactic.hand.map((c, i) => (
            <button key={i} className="chip" onClick={() => remove('hand', i)} title="Remove">
              {iconFor(c)} {byCode[c]?.name ?? c} ✕
            </button>
          ))}
        </div>
      </div>
      <div>
        <h3>
          Modifiers ({tactic.modifiers.length}/{setup.maxModifiers}) — secret
        </h3>
        <div className="chips">
          {tactic.modifiers.map((c, i) => (
            <button key={i} className="chip" onClick={() => remove('modifiers', i)} title="Remove">
              {byCode[c]?.name ?? c} ✕
            </button>
          ))}
        </div>
      </div>
    </div>
  )
}

/** All cards to choose from, by kind. */
export function TacticCards({ ed, busy, focus }: { ed: Editor; busy?: boolean; focus?: 'Field' | 'Hand' | 'Modifier' | null }) {
  const section = (kind: 'Field' | 'Hand' | 'Modifier', title: string, hint: string) => (
    <div className={`tb-section ${focus === kind ? 'coach-focus' : ''}`}>
      <h3>
        {title} <span className="muted small">{hint}</span>
      </h3>
      <div className="tb-cards">
        {ed.setup.cards
          .filter((c) => c.kind === kind)
          .map((c) => (
            <button key={c.code} className={`tb-card ${rarityClass(c.rarity)}`} disabled={busy || !ed.available(c)} onClick={() => ed.add(c)} title={c.description}>
              <span className="tb-icon">
                <CardThumb code={c.code} />
              </span>
              <span className="tb-name">{c.name}</span>
              <span className="tb-cost">{c.cost} pt</span>
              {c.owned != null && <span className="tb-owned">×{c.owned - ed.used(c.code)}</span>}
              <span className="tb-desc">{c.description}</span>
            </button>
          ))}
      </div>
    </div>
  )
  return (
    <div className="panel">
      {section('Field', 'Field cards', '— hidden in the grid (only you know where); if the opponent flips one, they steal it — except the Offside Trap')}
      {section('Hand', 'Hand cards', '— played before a flip, one per flip')}
      {section('Modifier', 'Neutral modifiers', '— secretly change the grey cards; both teams’ choices are added up')}
    </div>
  )
}

/** Pre-match screen: conditions, tactic points, field cards, hand cards and secret neutral modifiers. */
export function TacticBuilder({ m, onStart }: { m: MatchView; onStart: (t: Tactic, saveAs?: string) => Promise<void> }) {
  const setup = m.setup!
  const human = m.humanSide ?? 'Home'
  const me = human === 'Home' ? m.home : m.away
  const opp = human === 'Home' ? m.away : m.home
  const ed = useTacticEditor(setup, setup.lastTactic)
  const { tactic, left, setError } = ed
  const [saveAs, setSaveAs] = useState('')
  const [settings, updateSettings] = useSettings()
  const coaching = !settings.tacticTutorialDone
  const [busy, setBusy] = useState(false)
  const { data: saved } = useLoad(() => api.tactics(m.careerId), [m.careerId])
  const [plan, setPlan] = useState<number | null>(setup.plans?.list[setup.plans.active]?.tactic ? setup.plans.active : null)

  const start = async () => {
    if (tactic.field.length !== setup.fieldCards) return setError(`Pick ${setup.fieldCards} field cards (Squad Player is free).`)
    if (left < 0) return setError('Your tactic costs too many points.')
    setBusy(true)
    try {
      await onStart(tactic, saveAs.trim() || undefined)
      // saved directly: after kick-off this screen is already gone
      if (coaching) saveSettings({ tacticTutorialDone: true })
    } catch (e) {
      setError((e as Error).message)
      setBusy(false)
    }
  }

  const coachStep = tactic.field.length < setup.fieldCards ? 'Field' : tactic.hand.length === 0 ? 'Hand' : 'Modifier'

  return (
    <div className="tactic-builder">
      <div className="panel tb-top">
        <div>
          <h2>Pre-match: build your tactic</h2>
          <p className="muted">
            {me.name} (rating {me.rating}) vs {opp.name} (rating {opp.rating}, plays like a <b>{opp.aiLevel.toLowerCase()}</b> computer)
          </p>
          <ul className="conditions">
            {m.conditions.map((c) => (
              <li key={c.code}>{c.label}</li>
            ))}
          </ul>
        </div>
        <TacticBudget ed={ed} />
      </div>

      {setup.plans && setup.plans.list.some((p) => p.tactic) && (
        <div className="panel plan-picker">
          <span className="muted small">Your plans (Tactics tab):</span>
          {setup.plans.list.map((p, i) => (
            <button
              key={i}
              className={`btn small ${plan === i ? 'btn-primary' : ''}`}
              disabled={!p.tactic || busy}
              onClick={() => {
                if (!p.tactic) return
                ed.load(p.tactic)
                setPlan(i)
              }}
              title={p.tactic ? `Load ${p.name}` : 'This plan is empty — build it in the Tactics tab'}
            >
              {p.name}
              {i === setup.plans!.active ? ' ★' : ''}
            </button>
          ))}
        </div>
      )}

      {coaching && (
        <TacticCoach
          tactic={tactic}
          fieldCards={setup.fieldCards}
          handSize={setup.handSize}
          budget={setup.budget}
          left={left}
          onSkip={() => updateSettings({ tacticTutorialDone: true })}
        />
      )}

      <TacticChosen ed={ed} />

      <ErrorBox error={ed.error} />

      <div className="panel tb-actions">
        {saved && saved.length > 0 && (
          <label className="inline">
            Load saved tactic
            <select
              defaultValue=""
              onChange={(e) => {
                const t = saved.find((x) => String(x.id) === e.target.value)
                if (t) {
                  ed.load(t.tactic)
                  setPlan(null)
                }
              }}
            >
              <option value="">—</option>
              {saved.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name}
                </option>
              ))}
            </select>
          </label>
        )}
        <label className="inline">
          Save as
          <input value={saveAs} placeholder="(optional name)" maxLength={40} onChange={(e) => setSaveAs(e.target.value)} />
        </label>
        <button className="btn btn-primary big-inline" disabled={busy} onClick={start}>
          Kick off ▶
        </button>
      </div>

      <TacticCards ed={ed} busy={busy} focus={coaching ? coachStep : null} />
    </div>
  )
}
