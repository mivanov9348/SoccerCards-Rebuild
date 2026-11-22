import { openExternal, REVIEW_URL } from '../display'
import { Icon } from './Icons'

/** "Rate & review" — opens the game's itch.io page in the browser. */
export function ReviewButton({ variant = 'button' }: { variant?: 'button' | 'menu' | 'link' }) {
  const open = () => openExternal(REVIEW_URL)
  if (variant === 'menu')
    return (
      <button className="menu-item review-item" onClick={open}>
        <span>
          <Icon code="BIG_CHANCE_CARD" /> Rate & review
        </span>
        <small>Enjoying Dugout Deck? Leave a rating or a comment on itch.io</small>
      </button>
    )
  if (variant === 'link')
    return (
      <button className="btn btn-ghost small" onClick={open}>
        <Icon code="BIG_CHANCE_CARD" /> Enjoying the game? Rate it on itch.io
      </button>
    )
  return (
    <button className="btn" onClick={open}>
      <Icon code="BIG_CHANCE_CARD" /> Rate & review on itch.io
    </button>
  )
}
