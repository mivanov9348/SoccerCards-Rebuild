import { useCallback, useEffect, useState } from 'react'

export interface Settings {
  /** Delay (ms) before each computer card flip — old setting, replaced by pace. */
  aiDelay: number
  /** Match pace: Fast (no pauses), Normal, Dramatic (longer build-up before important cards). */
  pace: Pace
  managerName: string
  /** Mini-games (corners, free kicks, Big Chances, penalties) are decided automatically. */
  autoMiniGames: boolean
  /** Sound (all generated in code). Volumes 0–1. */
  volume: number
  sfxVolume: number
  crowdVolume: number
  musicVolume: number
  musicOn: boolean
  muted: boolean
  /** Fewer animations (no confetti, no shaking). */
  reduceMotion: boolean
  /** The tutorial match was offered / played. */
  tutorialDone: boolean
  /** The tactic coach (tips while building the first tactic) was shown. */
  tacticTutorialDone: boolean
  /** Fullscreen (F11). */
  fullscreen: boolean
  /** Difficulty for friendlies (a career keeps the difficulty it was started with). */
  difficulty: 'Easy' | 'Normal' | 'Hard'
}

export type Pace = 'Fast' | 'Normal' | 'Dramatic'
export const PACES: Pace[] = ['Fast', 'Normal', 'Dramatic']

/**
 * Timings for each pace (ms). think = the computer "thinks" before its move; lift = a face-down card is raised before it
 * turns over (computer: every card; you: only important cards — nobody likes waiting for themself).
 */
export const PACE_TIMES: Record<Pace, { think: number; liftAi: number; liftAiBig: number; liftMineBig: number }> = {
  Fast: { think: 250, liftAi: 0, liftAiBig: 0, liftMineBig: 0 },
  Normal: { think: 650, liftAi: 280, liftAiBig: 850, liftMineBig: 550 },
  Dramatic: { think: 1000, liftAi: 450, liftAiBig: 1400, liftMineBig: 1000 },
}

const KEY = 'dugout-deck-settings'
const defaults: Settings = {
  aiDelay: 900, pace: 'Normal', managerName: '', autoMiniGames: false,
  volume: 0.8, sfxVolume: 0.8, crowdVolume: 0.6, musicVolume: 0.5, musicOn: true, muted: false,
  reduceMotion: false, tutorialDone: false, tacticTutorialDone: false, fullscreen: false, difficulty: 'Normal',
}

/** Updates settings outside React (e.g. the M key). */
export function saveSettings(patch: Partial<Settings>) {
  try {
    localStorage.setItem(KEY, JSON.stringify({ ...loadSettings(), ...patch }))
  } catch {
    /* storage unavailable */
  }
  window.dispatchEvent(new Event('dugout-settings'))
}

export function loadSettings(): Settings {
  try {
    const raw = localStorage.getItem(KEY)
    return raw ? { ...defaults, ...JSON.parse(raw) } : defaults
  } catch {
    return defaults
  }
}

export function useSettings(): [Settings, (patch: Partial<Settings>) => void] {
  const [settings, setSettings] = useState<Settings>(loadSettings)
  useEffect(() => {
    const sync = () => setSettings(loadSettings())
    window.addEventListener('dugout-settings', sync)
    return () => window.removeEventListener('dugout-settings', sync)
  }, [])
  const update = useCallback((patch: Partial<Settings>) => {
    setSettings((prev) => {
      const next = { ...prev, ...patch }
      try {
        localStorage.setItem(KEY, JSON.stringify(next))
      } catch {
        /* storage unavailable */
      }
      queueMicrotask(() => window.dispatchEvent(new Event('dugout-settings')))
      return next
    })
  }, [])
  return [settings, update]
}
