import { useCallback, useState } from 'react'
import type { Tactic } from './api'

export interface Settings {
  difficulty: number
  showHints: boolean
  defaultTactic: Tactic
  managerName: string
}

const KEY = 'karti-futbol-settings'
const defaults: Settings = { difficulty: 1, showHints: true, defaultTactic: 'Balanced', managerName: '' }

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
        /* localStorage недостъпен */
      }
      return next
    })
  }, [])
  return [settings, update]
}
