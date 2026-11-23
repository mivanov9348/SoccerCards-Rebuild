import { useState } from 'react'
import { api, type CareerTactics } from '../api'
import { ErrorBox, useLoad } from './common'
import { iconFor } from './GameCard'
import { TacticBudget, TacticCards, TacticChosen, useTacticEditor } from './TacticBuilder'

/**
 * The career's Tactics tab: three plans (Plan A / B / C) to prepare calmly between matches. The active plan (★) is loaded
 * before every match and used by "Play"; the pre-match screen can still change it or switch to another plan.
 */
export function TacticsTab({ careerId, onChanged }: { careerId: number; onChanged: () => void }) {
  const { data, error, setData } = useLoad(() => api.careerTactics(careerId), [careerId])
  const [slot, setSlot] = useState<number | null>(null)
  if (error) return <ErrorBox error={error} />
  if (!data) return <div className="panel loading">Loading tactics…</div>
  const shown = slot ?? data.active
  return (
    <div className="tactics-tab">
      <div className="panel plan-slots">
        {data.plans.map((p, i) => (
          <button key={i} className={`plan-slot ${shown === i ? 'selected' : ''}`} onClick={() => setSlot(i)}>
            <span className="plan-name">
              {p.name} {i === data.active && <span className="plan-star" title="Used before every match">★ active</span>}
            </span>
            {p.tactic ? (
              <span className="plan-cards">
                {p.tactic.field.map((c, j) => (
                  <span key={j}>{iconFor(c)}</span>
                ))}
                <small className="muted">
                  {' '}
                  · {p.cost} pt{p.tactic.hand.length > 0 ? ` · ${p.tactic.hand.length} hand` : ''}
                  {p.tactic.modifiers.length > 0 ? ` · ${p.tactic.modifiers.length} mod.` : ''}
                </small>
              </span>
            ) : (
              <span className="muted small">empty</span>
            )}
            {p.awayErrors.length > 0 && <span className="plan-warn small">away: 1 point too many</span>}
          </button>
        ))}
      </div>
      <PlanEditor
        key={shown}
        careerId={careerId}
        slot={shown}
        data={data}
        onSaved={(d) => {
          setData(d)
          onChanged()
        }}
      />
    </div>
  )
}

function PlanEditor({ careerId, slot, data, onSaved }: { careerId: number; slot: number; data: CareerTactics; onSaved: (d: CareerTactics) => void }) {
  const plan = data.plans[slot]
  const ed = useTacticEditor(data.setup, plan.tactic ?? { field: [], hand: [], modifiers: [] })
  const [name, setName] = useState(plan.name)
  const [busy, setBusy] = useState(false)
  const [saved, setSaved] = useState(false)
  const { tactic, left, setError } = ed

  const run = async (fn: () => Promise<CareerTactics>) => {
    setBusy(true)
    setError(null)
    try {
      onSaved(await fn())
      setSaved(true)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }
  const save = () => {
    if (tactic.field.length !== data.setup.fieldCards) return setError(`Pick ${data.setup.fieldCards} field cards (Squad Player is free).`)
    if (left < 0) return setError('This plan costs too many points.')
    void run(() => api.saveCareerPlan(careerId, slot, { name, tactic }))
  }
  const awayOver = ed.cost > data.setup.awayBudget

  return (
    <div className="plan-editor">
      <div className="panel tb-top">
        <div>
          <h2>Build {plan.name}</h2>
          <p className="muted">
            Prepare up to three plans between matches. The <b>★ active</b> plan is loaded before every match (and used by “Play”); on the
            pre-match screen you can still change it or switch to another plan.
          </p>
          <div className="form-row">
            <label className="inline">
              Name
              <input value={name} maxLength={24} onChange={(e) => setName(e.target.value)} />
            </label>
            <button className="btn btn-primary" disabled={busy} onClick={save}>
              Save plan
            </button>
            {slot !== data.active && (
              <button className="btn" disabled={busy || !plan.tactic} onClick={() => void run(() => api.setActivePlan(careerId, slot))} title={plan.tactic ? '' : 'Save the plan first'}>
                ★ Make active
              </button>
            )}
            {plan.tactic && (
              <button
                className="btn btn-ghost"
                disabled={busy}
                onClick={() => {
                  if (!confirm(`Clear ${plan.name}?`)) return
                  ed.load({ field: [], hand: [], modifiers: [] })
                  void run(() => api.saveCareerPlan(careerId, slot, { tactic: { field: [], hand: [], modifiers: [] } }))
                }}
              >
                Clear
              </button>
            )}
            {saved && !busy && <span className="good small">✓ Saved</span>}
          </div>
        </div>
        <TacticBudget
          ed={ed}
          note={
            <span className={`small ${awayOver ? 'bad' : 'muted'}`}>
              Home {data.setup.budget} pt · away {data.setup.awayBudget} pt{awayOver ? ' — away you will have to drop a card' : ''}
            </span>
          }
        />
      </div>
      <TacticChosen ed={ed} />
      <ErrorBox error={ed.error} />
      <TacticCards ed={ed} busy={busy} />
    </div>
  )
}
