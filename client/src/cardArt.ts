/**
 * Card pictures and card facts used by every card on screen:
 *  - pictures from public/cards (listed in /cards/manifest.json, see vite.config.ts),
 *  - rarity and description from the card catalog (for the holo effect and the zoom view).
 * Loaded once when the game starts.
 */
import { useSyncExternalStore } from 'react'
import { api } from './api'

interface CardInfoLite {
  name: string
  description: string
  rarity: string
  kind: string
}

let art: Record<string, string> = {}
let info: Record<string, CardInfoLite> = {}
let version = 0
const listeners = new Set<() => void>()
const changed = () => {
  version++
  listeners.forEach((l) => l())
}

export function loadCardData() {
  fetch('/cards/manifest.json')
    .then((r) => (r.ok ? r.json() : {}))
    .then((m: Record<string, string>) => {
      art = m
      changed()
    })
    .catch(() => undefined)
  api
    .cards()
    .then((cards) => {
      info = Object.fromEntries(cards.map((c) => [c.code, { name: c.name, description: c.description, rarity: c.rarity, kind: c.kind }]))
      changed()
    })
    .catch(() => undefined)
}

const subscribe = (l: () => void) => {
  listeners.add(l)
  return () => listeners.delete(l)
}

/** Re-renders the caller when the pictures or the catalog arrive. */
export function useCardData() {
  useSyncExternalStore(subscribe, () => version)
  return { art, info }
}

export const cardArt = (code: string): string | undefined => art[code]
export const cardInfo = (code: string): CardInfoLite | undefined => info[code]

/** Right-click on any card opens the big view (CardZoom listens for this). */
export function zoomCard(code: string) {
  window.dispatchEvent(new CustomEvent('card-zoom', { detail: code }))
}
