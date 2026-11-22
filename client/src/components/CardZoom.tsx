import { useEffect, useState } from 'react'
import { cardInfo, useCardData } from '../cardArt'
import { CardFace } from './GameCard'

const KIND: Record<string, string> = { Neutral: 'Grey card', Field: 'Field card', Hand: 'Hand card', Modifier: 'Modifier' }

/** Big view of a card (right-click any card). Click anywhere or press Escape to close. */
export default function CardZoom() {
  useCardData()
  const [code, setCode] = useState<string | null>(null)
  useEffect(() => {
    const open = (e: Event) => setCode((e as CustomEvent<string>).detail)
    const key = (e: KeyboardEvent) => e.key === 'Escape' && setCode(null)
    window.addEventListener('card-zoom', open)
    window.addEventListener('keydown', key)
    return () => {
      window.removeEventListener('card-zoom', open)
      window.removeEventListener('keydown', key)
    }
  }, [])
  if (!code) return null
  const c = cardInfo(code)
  return (
    <div className="card-zoom" onClick={() => setCode(null)} onContextMenu={(e) => { e.preventDefault(); setCode(null) }} role="dialog" aria-label="Card">
      <div className="card-zoom-inner" onClick={(e) => e.stopPropagation()}>
        <CardFace code={code} name={c?.name ?? code} size="big" />
        <div className="card-zoom-text">
          <h2>{c?.name ?? code}</h2>
          <p className="muted small">
            {KIND[c?.kind ?? ''] ?? ''}
            {c && c.rarity !== 'None' && c.kind !== 'Neutral' ? ` · ${c.rarity}` : ''}
          </p>
          <p>{c?.description}</p>
          <button className="btn" onClick={() => setCode(null)}>
            Close
          </button>
        </div>
      </div>
    </div>
  )
}
