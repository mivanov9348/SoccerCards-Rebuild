import { useMemo, useState, type ReactNode } from 'react'
import { api, TIER_NAMES, TIER_SHORT, type MatchView, type SetupCard, type Tactic, type TacticEditorSetup } from '../api'
import { ErrorBox, useLoad } from './common'
import { CardThumb, iconFor, TierBadge } from './GameCard'
import { TacticCoach } from './TacticCoach'
import { saveSettings, useSettings } from '../settings'

const tierOf = (tiers: number[] | undefined, i: number) => tiers?.[i] ?? 0

/**
 * The state of a tactic being built: points, owned copies per tier, adding and removing cards. Each field and hand card
 * remembers the tier of the copy it uses (1.2); a click on a card takes the best copy still free.
 */
export function useTacticEditor(setup: TacticEditorSetup, initial: Tactic) {
  const byCode = useMemo(() => Object.fromEntries(setup.cards.map((c) => [c.code, c])), [setup.cards])

  /** Copies of a card in a tier that the club owns (Infinity for friendlies and free cards — they are all Normal). */
  const ownedIn = (code: string, tier: number) => {
    const c = byCode[code]
    if (!c || c.tiers == null) return tier === 0 ? Infinity : 0
    return c.tiers[tier] ?? 0
  }

  /** A loaded tactic (plan, saved tactic, last tactic) may ask for copies the club no longer has in that tier: each card
   * then takes the best copy that is free — so an old Normal Striker becomes your Rare Striker if that is what you own. */
  const normalise = (t: Tactic): Tactic => {
    const taken = new Map<string, number>()
    const pick = (code: string, wanted: number) => {
      const free = (tier: number) => ownedIn(code, tier) - (taken.get(`${code}|${tier}`) ?? 0) > 0
      let tier = free(wanted) ? wanted : [3, 2, 1, 0].find(free)
      if (tier == null) tier = wanted // not owned at all: validation explains it
      taken.set(`${code}|${tier}`, (taken.get(`${code}|${tier}`) ?? 0) + 1)
      return tier
    }
    const field = [...t.field]
    const hand = t.hand.slice(0, setup.handSize)
    return {
      field,
      hand,
      modifiers: [...t.modifiers],
      fieldTiers: field.map((c, i) => pick(c, tierOf(t.fieldTiers, i))),
      handTiers: hand.map((c, i) => pick(c, tierOf(t.handTiers, i))),
    }
  }

  const [tactic, setTactic] = useState<Tactic>(() => normalise(initial))
  const [error, setError] = useState<string | null>(null)
  const cost = [...tactic.field, ...tactic.hand, ...tactic.modifiers].reduce((s, c) => s + (byCode[c]?.cost ?? 0), 0)
  const left = setup.budget - cost
  const usedIn = (code: string, tier: number) =>
    tactic.field.filter((c, i) => c === code && tierOf(tactic.fieldTiers, i) === tier).length +
    tactic.hand.filter((c, i) => c === code && tierOf(tactic.handTiers, i) === tier).length
  const used = (code: string) => [...tactic.field, ...tactic.hand].filter((c) => c === code).length
  const freeIn = (code: string, tier: number) => ownedIn(code, tier) - usedIn(code, tier)
  const bestFree = (code: string) => [3, 2, 1, 0].find((t) => freeIn(code, t) > 0)
  const available = (c: SetupCard) => c.kind === 'Modifier' || bestFree(c.code) != null

  /** Adds a card: the given tier, or the best free copy. */
  const add = (c: SetupCard, tier?: number) => {
    setError(null)
    if (c.cost > left) return setError(`Not enough tactic points for ${c.name}.`)
    if (c.kind === 'Modifier') {
      return setTactic((t) => (t.modifiers.length >= setup.maxModifiers || t.modifiers.includes(c.code) ? t : { ...t, modifiers: [...t.modifiers, c.code] }))
    }
    const use = tier ?? bestFree(c.code)
    if (use == null || freeIn(c.code, use) <= 0) return setError(`You don't own more ${tier != null ? TIER_NAMES[tier] + ' ' : ''}${c.name} cards.`)
    setTactic((t) => {
      if (c.kind === 'Field')
        return t.field.length >= setup.fieldCards ? t : { ...t, field: [...t.field, c.code], fieldTiers: [...(t.fieldTiers ?? t.field.map(() => 0)), use] }
      return t.hand.length >= setup.handSize ? t : { ...t, hand: [...t.hand, c.code], handTiers: [...(t.handTiers ?? t.hand.map(() => 0)), use] }
    })
  }
  const remove = (list: 'field' | 'hand' | 'modifiers', i: number) =>
    setTactic((t) => {
      const next = { ...t, [list]: t[list].filter((_, j) => j !== i) }
      if (list === 'field') next.fieldTiers = (t.fieldTiers ?? []).filter((_, j) => j !== i)
      if (list === 'hand') next.handTiers = (t.handTiers ?? []).filter((_, j) => j !== i)
      return next
    })
  const load = (t: Tactic) => {
    setError(null)
    setTactic(normalise(t))
  }
  return { setup, tactic, load, byCode, cost, left, used, freeIn, available, add, remove, error, setError }
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

/** The chosen cards (click to remove), with the tier of each copy. */
export function TacticChosen({ ed }: { ed: Editor }) {
  const { tactic, setup, byCode, remove } = ed
  const chip = (list: 'field' | 'hand', c: string, i: number, tier: number) => (
    <button key={i} className={`chip tier-chip-${tier}`} onClick={() => remove(list, i)} title={`Remove${tier ? ` (${TIER_NAMES[tier]}: ${byCode[c]?.bonuses?.[tier] || 'better copy'})` : ''}`}>
      {iconFor(c)} {byCode[c]?.name ?? c} <TierBadge tier={tier} /> ✕
    </button>
  )
  return (
    <div className="panel tb-chosen">
      <div>
        <h3>
          Field cards ({tactic.field.length}/{setup.fieldCards})
        </h3>
        <div className="chips">{tactic.field.map((c, i) => chip('field', c, i, tierOf(tactic.fieldTiers, i)))}</div>
      </div>
      <div>
        <h3>
          Hand ({tactic.hand.length}/{setup.handSize})
        </h3>
        <div className="chips">{tactic.hand.map((c, i) => chip('hand', c, i, tierOf(tactic.handTiers, i)))}</div>
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

/** All cards to choose from, by kind. In a career each card shows its free copies per tier; click a tier to use that copy. */
export function TacticCards({ ed, busy, focus }: { ed: Editor; busy?: boolean; focus?: 'Field' | 'Hand' | 'Modifier' | null }) {
  const section = (kind: 'Field' | 'Hand' | 'Modifier', title: string, hint: string) => (
    <div className={`tb-section ${focus === kind ? 'coach-focus' : ''}`}>
      <h3>
        {title} <span className="muted small">{hint}</span>
      </h3>
      <div className="tb-cards">
        {ed.setup.cards
          .filter((c) => c.kind === kind)
          .map((c) => {
            const best = c.tiers ? [3, 2, 1, 0].find((t) => (c.tiers?.[t] ?? 0) > 0) ?? 0 : 0
            return (
              <div
                key={c.code}
                role="button"
                tabIndex={0}
                className={`tb-card tier-edge-${best} ${busy || !ed.available(c) ? 'disabled' : ''}`}
                onClick={() => !busy && ed.available(c) && ed.add(c)}
                onKeyDown={(e) => e.key === 'Enter' && !busy && ed.available(c) && ed.add(c)}
                title={c.description}
              >
                <span className="tb-icon">
                  <CardThumb code={c.code} />
                </span>
                <span className="tb-name">{c.name}</span>
                <span className="tb-cost">{c.cost} pt</span>
                {c.owned != null && <span className="tb-owned">×{c.owned - ed.used(c.code)}</span>}
                <span className="tb-desc">{c.description}</span>
                {c.tiers && c.tiers.some((n, t) => t > 0 && n > 0) && (
                  <span className="tb-tiers">
                    {c.tiers.map((n, t) =>
                      n > 0 ? (
                        <button
                          key={t}
                          className={`tier-pill tier-${t}`}
                          disabled={busy || ed.freeIn(c.code, t) <= 0}
                          title={`${TIER_NAMES[t]}${c.bonuses?.[t] ? ': ' + c.bonuses[t] : ''} — ${ed.freeIn(c.code, t)} free`}
                          onClick={(e) => {
                            e.stopPropagation()
                            ed.add(c, t)
                          }}
                        >
                          {TIER_SHORT[t]} ×{ed.freeIn(c.code, t)}
                        </button>
                      ) : null,
                    )}
                  </span>
                )}
              </div>
            )
          })}
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
