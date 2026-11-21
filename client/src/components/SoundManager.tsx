import { useEffect, useRef } from 'react'
import { useLocation } from 'react-router-dom'
import { applyVolumes, play, startCrowd, startMusic, stopCrowd, stopMusic, unlockOnFirstGesture } from '../sound'
import { loadSettings, saveSettings } from '../settings'
import { isDesktop, setFullscreen } from '../display'

/** Menu music outside matches, crowd ambience during matches, button clicks and the M (mute) key. */
export default function SoundManager() {
  const { pathname } = useLocation()
  const unlocked = useRef(false)
  const inMatch = pathname.startsWith('/match') || pathname.startsWith('/tutorial')

  const sync = () => {
    if (!unlocked.current) return
    applyVolumes()
    if (inMatch) {
      stopMusic()
      startCrowd()
    } else {
      stopCrowd()
      if (loadSettings().musicOn) startMusic()
      else stopMusic()
    }
  }
  const syncRef = useRef(sync)
  syncRef.current = sync

  useEffect(() => {
    // the desktop window remembers fullscreen; a browser only allows it after a click
    if (loadSettings().fullscreen && isDesktop()) setFullscreen(true)
    unlockOnFirstGesture(() => {
      unlocked.current = true
      syncRef.current()
      if (loadSettings().fullscreen && !isDesktop()) setFullscreen(true)
    })
    const onSettings = () => syncRef.current()
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'F11') {
        e.preventDefault()
        const on = !loadSettings().fullscreen
        saveSettings({ fullscreen: on })
        setFullscreen(on)
        return
      }
      if ((e.key === 'm' || e.key === 'M') && !(e.target instanceof HTMLInputElement) && !(e.target instanceof HTMLTextAreaElement)) {
        saveSettings({ muted: !loadSettings().muted })
      }
    }
    const onClick = (e: PointerEvent) => {
      const b = (e.target as HTMLElement | null)?.closest('button')
      if (b && !b.disabled && !b.classList.contains('card-back-big') && !b.closest('.decision-box')) play('click')
    }
    window.addEventListener('dugout-settings', onSettings)
    window.addEventListener('keydown', onKey)
    window.addEventListener('pointerdown', onClick)
    return () => {
      window.removeEventListener('dugout-settings', onSettings)
      window.removeEventListener('keydown', onKey)
      window.removeEventListener('pointerdown', onClick)
    }
  }, [])

  useEffect(() => {
    syncRef.current()
  }, [inMatch])

  return null
}
