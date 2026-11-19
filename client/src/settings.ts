import { useCallback, useState } from 'react'

export interface Settings {
  /** Delay (ms) before each computer card flip, so you can follow the computer's turn. */
  aiDelay: number
  managerName: string
  /** Mini-games (corners, free kicks, Big Chances, penalties) are decided automatically. */
  autoMiniGames: boolean
}

const KEY = 'dugout-deck-settings'
const defaults: Settings = { aiDelay: 900, managerName: '', autoMiniGames: false }

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
  const update = useCallback((patch: Partial<Settings>) => {
    setSettings((prev) => {
      const next = { ...prev, ...patch }
      try {
        localStorage.setItem(KEY, JSON.stringify(next))
      } catch {
        /* storage unavailable */
      }
      return next
    })
  }, [])
  return [settings, update]
}
