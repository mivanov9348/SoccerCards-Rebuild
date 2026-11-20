import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { CardBack, CardFace } from '../components/GameCard'
import { play } from '../sound'
import { useSettings } from '../settings'

/**
 * A scripted tutorial match: a fixed grid and step-by-step instructions. It runs entirely in the browser (no server),
 * so the cards are always the same and every rule shows up once: hints, passes, Big Chance, stealing, one own card per
 * turn, Scout, the Captain and the Offside Trap.
 */

type Owner = 'me' | 'cpu' | null
interface Cell {
  card: string
  name: string
  owner: Owner
  flippedBy: 'me' | 'cpu' | null
  peeked?: boolean
}

const N = (card: string, name: string, owner: Owner = null): Cell => ({ card, name, owner, flippedBy: null })
const GRID: Cell[] = [
  N('PASS', 'Pass'), N('PASS', 'Pass'), N('PASS', 'Pass'), N('CAPTAIN', 'Captain', 'me'), N('HALF_TIME', 'Half Time'),
  N('TACKLE', 'Tackle'), N('SQUAD_PLAYER', 'Squad Player', 'cpu'), N('STRIKER', 'Striker', 'me'), N('SAVE', 'Save'), N('PASS', 'Pass'),
  N('OFFSIDE_TRAP', 'Offside Trap', 'cpu'), N('MISS', 'Miss'), N('TACKLE', 'Tackle'), N('GOAL', 'Goal!'), N('LONG_SHOT', 'Long Shot', 'me'),
  N('YELLOW_CARD', 'Yellow Card'), N('PASS', 'Pass'), N('STRIKER', 'Striker', 'cpu'), N('TACKLE', 'Tackle'), N('CORNER', 'Corner'),
  N('BIG_CHANCE_CARD', 'Big Chance', 'me'), N('OFFSIDE', 'Offside'), N('FREE_KICK', 'Free Kick'), N('LONG_SHOT', 'Long Shot', 'cpu'), N('RED_CARD', 'Red Card'),
]
const DANGEROUS = ['TACKLE', 'OFFSIDE', 'SAVE', 'MISS', 'YELLOW_CARD', 'RED_CARD']

type Action =
  | { kind: 'next' }
  | { kind: 'flip'; cell: number }
  | { kind: 'scout'; cell: number }
  | { kind: 'shot' }
  | { kind: 'cpu'; cells: number[] }
  | { kind: 'end' }

interface Step {
  title: string
  text: string
  action: Action
  /** what happens after the action (the result line) */
  result?: string
  goals?: [number, number]
  passes?: number
  captain?: boolean
  ownUsed?: boolean
}

const STEPS: Step[] = [
  {
    title: 'Welcome to Dugout Deck!',
    text: 'You are Dugout FC (green) against the computer. Each half is a 5×5 grid of face-down cards — all with the same grey back. The small green flag in a corner marks YOUR four team cards. You never see where the computer’s cards are.',
    action: { kind: 'next' },
  },
  {
    title: 'Read the hints',
    text: 'The numbers on the right and at the bottom count the dangerous grey cards (Tackle, Offside, Save, Miss, cards) still face down in that row or column. The top row shows 0 — a good place to start. Flip the first card of the top row.',
    action: { kind: 'flip', cell: 0 },
    result: 'Pass! You keep the ball and flip again.',
    passes: 1,
  },
  {
    title: 'Keep the ball',
    text: 'Pass, Corner and Free Kick keep the ball. A dangerous card, a goal or a penalty gives it to the opponent. Flip the second card of the top row.',
    action: { kind: 'flip', cell: 1 },
    result: 'Another Pass — 2 in a row.',
    passes: 2,
  },
  {
    title: 'Three passes = Big Chance',
    text: 'Three passes in a row give you a Big Chance: a shot against the keeper. Flip the third card of the top row.',
    action: { kind: 'flip', cell: 2 },
    result: 'BIG CHANCE! Pick where to shoot.',
    passes: 0,
  },
  {
    title: 'Big Chance',
    text: 'Choose a side. If the keeper guesses wrong, the goal chance is much higher. (You also choose the dive when the computer shoots.)',
    action: { kind: 'shot' },
    result: 'The keeper dives the other way — GOAL! After a goal the opponent kicks off.',
    goals: [1, 0],
  },
  {
    title: 'The computer’s turn',
    text: 'Watch the computer play. It doesn’t know where your cards are either…',
    action: { kind: 'cpu', cells: [9, 7] },
    result: 'It flipped a Pass, then YOUR Striker — and stole it! Whoever flips a team card gets its effect. Luckily your keeper saved the shot, and the ball is yours.',
  },
  {
    title: 'Play your cards in time',
    text: 'That is why you should not wait too long with your own cards. Flip your Captain now (top row, with the flag): your next Tackle will be ignored.',
    action: { kind: 'flip', cell: 3 },
    result: 'Captain! Your next Tackle is ignored.',
    captain: true,
    ownUsed: true,
  },
  {
    title: 'One own card per turn',
    text: 'You may flip only ONE of your own cards per turn — see how your other flags faded? They light up again in your next turn. Now a hand card: click “Scout” below, then the card in row 4, column 3.',
    action: { kind: 'scout', cell: 17 },
    result: 'Scouted: it is the opponent’s Striker (only you can see it).',
  },
  {
    title: 'Steal it!',
    text: 'Flip the scouted card. You steal the opponent’s Striker — the shot is yours.',
    action: { kind: 'flip', cell: 17 },
    result: 'Stolen! Clean through… GOAL! 2:0.',
    goals: [2, 0],
  },
  {
    title: 'The computer again',
    text: 'After your goal the computer kicks off.',
    action: { kind: 'cpu', cells: [11] },
    result: 'It flipped a Miss — a dangerous card. The ball is yours again.',
  },
  {
    title: 'Risky rows',
    text: 'Row 2 shows 2 dangerous cards. Normally you would avoid it — but your Captain protects you from one Tackle. Flip the first card of row 2.',
    action: { kind: 'flip', cell: 5 },
    result: 'Tackle! But the Captain ignores it — you keep the ball.',
    captain: false,
  },
  {
    title: 'Beware the Offside Trap',
    text: 'Hints count only grey neutral cards — the opponent’s team cards look grey too and are not counted. Flip the first card of row 3.',
    action: { kind: 'flip', cell: 10 },
    result: 'The opponent’s Offside Trap! It is the one card that cannot be stolen: you lose the ball.',
  },
  {
    title: 'Half time',
    text: 'The computer is on the ball…',
    action: { kind: 'cpu', cells: [4] },
    result: 'HALF TIME. In the second half everything is reshuffled face down, the team cards come back, and Full Time is hidden somewhere in the new grid.',
  },
  {
    title: 'You’re ready!',
    text: 'Before each match you build a tactic with tactic points: 4 field cards, 1–3 hand cards (Scout, Swap, Calm, Press, a blind Burn) and up to 2 secret modifiers. Have fun!',
    action: { kind: 'end' },
  },
]

export default function Tutorial() {
  const navigate = useNavigate()
  const [, updateSettings] = useSettings()
  const [grid, setGrid] = useState<Cell[]>(() => GRID.map((c) => ({ ...c })))
  const [stepIndex, setStepIndex] = useState(0)
  const [done, setDone] = useState(false) // the current step's action happened
  const [state, setState] = useState({ goals: [0, 0] as [number, number], passes: 0, captain: false, ownUsed: false })
  const [last, setLast] = useState<number | null>(null)
  const [scouting, setScouting] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const step = STEPS[stepIndex]

  const hints = useMemo(() => {
    const rows = [0, 0, 0, 0, 0]
    const cols = [0, 0, 0, 0, 0]
    grid.forEach((c, i) => {
      if (!c.flippedBy && !c.owner && DANGEROUS.includes(c.card)) {
        rows[Math.floor(i / 5)]++
        cols[i % 5]++
      }
    })
    return { rows, cols }
  }, [grid])

  const finishStep = () => {
    setDone(true)
    setState((s) => ({
      goals: step.goals ?? s.goals,
      passes: step.passes ?? s.passes,
      captain: step.captain ?? s.captain,
      ownUsed: step.ownUsed ?? s.ownUsed,
    }))
    if (step.goals) setTimeout(() => play('goal'), 300)
  }

  const flip = (i: number, by: 'me' | 'cpu') => {
    setGrid((g) => g.map((c, j) => (j === i ? { ...c, flippedBy: by } : c)))
    setLast(i)
    play('flip')
    const c = grid[i]
    setTimeout(() => {
      if (c.owner && c.owner !== by) play(c.card === 'OFFSIDE_TRAP' ? 'trap' : by === 'me' ? 'steal' : 'stolen')
      else if (c.card === 'PASS' || c.card === 'SQUAD_PLAYER') play('pass')
      else if (c.card === 'HALF_TIME') play('whistleLong')
      else if (DANGEROUS.includes(c.card)) play(c.card === 'MISS' ? 'miss' : 'tackle')
      else play('keep')
    }, 150)
  }

  // computer steps play by themselves
  useEffect(() => {
    if (step.action.kind !== 'cpu' || done) return
    const cells = step.action.cells
    const timers = cells.map((cell, k) => window.setTimeout(() => flip(cell, 'cpu'), 900 + k * 1100))
    timers.push(window.setTimeout(finishStep, 900 + cells.length * 1100))
    return () => timers.forEach((t) => window.clearTimeout(t))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [stepIndex])

  const clickCell = (i: number) => {
    setMessage(null)
    if (done) return
    const a = step.action
    if (a.kind === 'scout' && scouting) {
      if (i !== a.cell) return setMessage('Scout the highlighted card (row 4, column 3).')
      setGrid((g) => g.map((c, j) => (j === i ? { ...c, peeked: true } : c)))
      setScouting(false)
      play('click')
      return finishStep()
    }
    if (a.kind !== 'flip') return
    if (i !== a.cell) {
      if (grid[i].owner === 'me' && state.ownUsed) {
        play('error')
        return setMessage('You have already played one of your own cards this turn.')
      }
      return setMessage('In the tutorial, flip the highlighted card.')
    }
    flip(i, 'me')
    finishStep()
  }

  const next = () => {
    setMessage(null)
    setDone(false)
    if (step.title === 'Steal it!' || step.title === 'Big Chance') setState((s) => ({ ...s, ownUsed: false }))
    setStepIndex((i) => Math.min(STEPS.length - 1, i + 1))
  }

  const finish = (to: string) => {
    updateSettings({ tutorialDone: true })
    navigate(to)
  }

  const target = step.action.kind === 'flip' || step.action.kind === 'scout' ? step.action.cell : null
  const showNext = step.action.kind === 'next' || done

  return (
    <div className="screen match-screen tutorial">
      <div className="scoreboard">
        <div className="score-team">
          <div className="score-name">
            Dugout FC <span className="you-tag">YOU</span>
          </div>
          <div className="effect-chips">
            {state.passes > 0 && <span>➜ passes {state.passes}/3</span>}
            {state.captain && <span>© captain</span>}
          </div>
        </div>
        <div className="score-center">
          <div className="score score-pop" key={state.goals.join('-')}>
            {state.goals[0]} : {state.goals[1]}
          </div>
          <div className="clock">Tutorial · step {stepIndex + 1}/{STEPS.length}</div>
        </div>
        <div className="score-team right">
          <div className="score-name">
            Computer United <span className="cpu-tag">CPU</span>
          </div>
        </div>
      </div>

      <div className="tutorial-box panel">
        <h2>{step.title}</h2>
        <p>{step.text}</p>
        {done && step.result && <p className="tutorial-result">{step.result}</p>}
        {step.action.kind === 'shot' && !done && (
          <div className="decision-options">
            {['Shoot left', 'Shoot centre', 'Shoot right'].map((label) => (
              <button key={label} className="btn" onClick={finishStep}>
                {label}
              </button>
            ))}
          </div>
        )}
        {message && <p className="error-box">{message}</p>}
        <div className="row-buttons">
          {step.action.kind === 'end' ? (
            <>
              <button className="btn btn-primary" onClick={() => finish('/friendly')}>
                Play a friendly ▶
              </button>
              <button className="btn" onClick={() => finish('/')}>
                Main menu
              </button>
            </>
          ) : (
            showNext && (
              <button className="btn btn-primary" onClick={next}>
                Next ▶
              </button>
            )
          )}
          <Link className="btn btn-ghost" to="/" onClick={() => updateSettings({ tutorialDone: true })}>
            Skip tutorial
          </Link>
        </div>
      </div>

      <div className="table-area">
        <div className="grid-wrap">
          <div className="flip-grid">
            {grid.map((c, i) => {
              if (c.flippedBy) {
                const stolen = c.owner != null && c.owner !== c.flippedBy
                return (
                  <div
                    key={i}
                    className={`grid-face ${last === i ? `just-flipped anim ${DANGEROUS.includes(c.card) || (stolen && c.card === 'OFFSIDE_TRAP') ? 'danger' : ''} ${stolen && c.card !== 'OFFSIDE_TRAP' ? 'stolen-flash' : ''}` : ''}`}
                    style={{ borderColor: c.flippedBy === 'me' ? '#1d8a4a' : '#c0392b' }}
                  >
                    <CardFace code={c.card} name={c.name} size="mini" />
                    <span className="grid-owner" style={{ background: c.flippedBy === 'me' ? '#1d8a4a' : '#c0392b' }}>
                      {stolen ? (c.card === 'OFFSIDE_TRAP' ? '🪤' : '🕵') : ''}
                      {c.flippedBy === 'me' ? 'YOU' : 'CPU'}
                    </span>
                  </div>
                )
              }
              return (
                <div key={i} className={target === i && !done ? 'tutorial-target' : ''}>
                  <CardBack
                    mark={c.owner === 'me' ? { color: '#1d8a4a', color2: '#ffffff', dim: state.ownUsed } : null}
                    peeked={c.peeked ? { code: c.card, name: c.name, opponent: c.owner === 'cpu' } : null}
                    title={c.owner === 'me' ? `Your ${c.name}` : 'Face-down card'}
                    onClick={() => clickCell(i)}
                  />
                </div>
              )
            })}
          </div>
          <div className="row-hints">
            {hints.rows.map((h, i) => (
              <span key={i} className={h === 0 ? 'safe' : ''}>
                {h}
              </span>
            ))}
          </div>
          <div className="col-hints">
            {hints.cols.map((h, i) => (
              <span key={i} className={h === 0 ? 'safe' : ''}>
                {h}
              </span>
            ))}
          </div>
        </div>
        <div className="reveal">
          <span className="muted small">Last card</span>
          {last != null ? (
            <CardFace key={last} code={grid[last].card} name={grid[last].name} size="big" />
          ) : (
            <div className="reveal-empty">No card flipped yet</div>
          )}
        </div>
      </div>

      <div className="hand-panel">
        <span className="muted small">Your hand</span>
        <div className="hand-cards">
          <button
            className={`hand-card ${step.action.kind === 'scout' && !done ? 'tutorial-pulse' : ''}`}
            disabled={step.action.kind !== 'scout' || done}
            onClick={() => setScouting(true)}
          >
            👁 Scout
          </button>
          {scouting && <span className="muted small">Now click the highlighted card.</span>}
        </div>
      </div>
    </div>
  )
}
