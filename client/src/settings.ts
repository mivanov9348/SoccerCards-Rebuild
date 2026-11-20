import { useCallback, useEffect, useState } from 'react'

export interface Settings {
  /** Delay (ms) before each computer card flip, so you can follow the computer's turn. */
  aiDelay: number
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
}

const KEY = 'dugout-deck-settings'
const defaults: Settings = {
  aiDelay: 900, managerName: '', autoMiniGames: false,
  volume: 0.8, sfxVolume: 0.8, crowdVolume: 0.6, musicVolume: 0.5, musicOn: true, muted: false,
  reduceMotion: false, tutorialDone: false,
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
