import type { Tactic } from '../api'

/**
 * Tips shown while the player builds a tactic for the first time. The step follows what has been picked so far,
 * so the player learns by doing: field cards → hand cards → modifiers → kick off.
 */
export function TacticCoach({ tactic, fieldCards, handSize, budget, left, onSkip }: {
  tactic: Tactic
  fieldCards: number
  handSize: number
  budget: number
  left: number
  onSkip: () => void
}) {
  const step = tactic.field.length < fieldCards ? 1 : tactic.hand.length === 0 ? 2 : 3
  return (
    <div className="panel coach" role="note">
      <div className="coach-head">
        <b>Coach — build your tactic</b>
        <span className="muted small">step {step} of 3</span>
        <button className="btn btn-ghost small" onClick={onSkip}>
          Hide tips
        </button>
      </div>
      <p className="muted small">
        You have <b>{budget} tactic points</b> for this match (10 + your club rating bonus + 1 at home + difficulty). Every card shows its
        cost in the corner. Points left now: <b>{left}</b>.
      </p>
      {step === 1 && (
        <p>
          <b>1. Pick {fieldCards} field cards</b> ({tactic.field.length}/{fieldCards}). They are hidden in the grid — only you see where (a small flag).
          Flip your own card and you get its effect; if the computer flips it, it <b>steals</b> it. Good start: <b>Striker</b> (35% goal),
          <b> Long Shot</b> (cheap), <b>Captain</b> (ignores a Tackle). <b>Squad Player</b> costs nothing if you run out of points.
        </p>
      )}
      {step === 2 && (
        <p>
          <b>2. Pick hand cards</b> (up to {handSize}). You play them before a flip, one per flip. <b>Scout</b> (1 point) lets you look under a
          card — great when every row looks risky. <b>Burn</b> removes a random opponent card. Click a chosen card at the top to remove it.
        </p>
      )}
      {step === 3 && (
        <p>
          <b>3. Optional: secret modifiers</b> change the grey cards (e.g. +1 Goal turns a Pass into a Goal). The computer picks its own and both
          are revealed at kick-off. When you are happy, press <b>Kick off ▶</b>. Your tactic is saved — next time one click on
          “Play (last tactic)” is enough.
        </p>
      )}
    </div>
  )
}
