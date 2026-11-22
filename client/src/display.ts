/** Fullscreen: in the desktop app the window itself goes fullscreen (message to the host); in a browser, the page. */
interface PhotinoExternal {
  sendMessage?: (message: string) => void
}

function host(): PhotinoExternal | null {
  const ext = (window as unknown as { external?: PhotinoExternal }).external
  return ext && typeof ext.sendMessage === 'function' ? ext : null
}

export const isDesktop = () => host() != null

export function setFullscreen(on: boolean | 'toggle') {
  const h = host()
  if (h) {
    h.sendMessage!(on === 'toggle' ? 'fullscreen:toggle' : on ? 'fullscreen:on' : 'fullscreen:off')
    return
  }
  const active = document.fullscreenElement != null
  const want = on === 'toggle' ? !active : on
  if (want && !active) void document.documentElement.requestFullscreen?.().catch(() => undefined)
  if (!want && active) void document.exitFullscreen?.().catch(() => undefined)
}

export type Difficulty = 'Easy' | 'Normal' | 'Hard'
export const DIFFICULTIES: [Difficulty, string][] = [
  ['Easy', 'Easy — +2 tactic points, the computer plays one level weaker'],
  ['Normal', 'Normal — the standard game'],
  ['Hard', 'Hard — −1 tactic point, the computer plays one level stronger'],
]

/** The game's page on itch.io (ratings and comments). Change here if the page moves (e.g. to Steam later). */
export const REVIEW_URL = 'https://mivanov93.itch.io/dugout-deck'

/** Opens a web page in the player's own browser (the desktop window would otherwise navigate away from the game). */
export function openExternal(url: string) {
  const h = host()
  if (h) h.sendMessage!(`open:${url}`)
  else window.open(url, '_blank', 'noopener')
}
