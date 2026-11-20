/**
 * Dugout Deck sound: every sound effect and the menu music are generated in code with the Web Audio API — no audio files,
 * so there is nothing to license. Volumes come from the settings (master, effects, crowd, music).
 * Later, any effect can be replaced by a CC0 recording (see docs) without changing the callers.
 */
import { loadSettings } from './settings'

export type Sfx =
  | 'flip' | 'pass' | 'keep' | 'tackle' | 'whistle' | 'whistleLong' | 'goal' | 'goalAgainst' | 'miss' | 'save'
  | 'yellow' | 'red' | 'steal' | 'stolen' | 'trap' | 'chance' | 'heartbeat' | 'click' | 'pack' | 'packEpic' | 'burn' | 'error'

let ctx: AudioContext | null = null
let master: GainNode, sfxBus: GainNode, crowdBus: GainNode, musicBus: GainNode
let noise: AudioBuffer | null = null

function audio(): AudioContext | null {
  if (typeof window === 'undefined' || !('AudioContext' in window)) return null
  if (!ctx) {
    ctx = new AudioContext()
    master = ctx.createGain()
    master.connect(ctx.destination)
    sfxBus = ctx.createGain()
    crowdBus = ctx.createGain()
    musicBus = ctx.createGain()
    for (const b of [sfxBus, crowdBus, musicBus]) b.connect(master)
    applyVolumes()
  }
  if (ctx.state === 'suspended') void ctx.resume()
  return ctx
}

/** Reads the volumes from the settings (call after the settings change). */
export function applyVolumes() {
  if (!ctx) return
  const s = loadSettings()
  const t = ctx.currentTime
  master.gain.setTargetAtTime(s.muted ? 0 : s.volume, t, 0.05)
  sfxBus.gain.setTargetAtTime(s.sfxVolume, t, 0.05)
  crowdBus.gain.setTargetAtTime(s.crowdVolume * 0.6, t, 0.05)
  musicBus.gain.setTargetAtTime(s.musicOn ? s.musicVolume * 0.35 : 0, t, 0.05)
}

function noiseBuffer(c: AudioContext) {
  if (!noise) {
    noise = c.createBuffer(1, c.sampleRate * 2, c.sampleRate)
    const d = noise.getChannelData(0)
    let last = 0
    for (let i = 0; i < d.length; i++) {
      // slightly "pink" noise: softer than white noise
      last = 0.97 * last + 0.03 * (Math.random() * 2 - 1)
      d[i] = (Math.random() * 2 - 1) * 0.5 + last * 3
    }
  }
  return noise
}

interface ToneOpts { type?: OscillatorType; gain?: number; attack?: number; at?: number; to?: number; bus?: GainNode; vibrato?: number }

function tone(freq: number, dur: number, o: ToneOpts = {}) {
  const c = audio()
  if (!c) return
  const t = c.currentTime + (o.at ?? 0)
  const osc = c.createOscillator()
  const g = c.createGain()
  osc.type = o.type ?? 'sine'
  osc.frequency.setValueAtTime(freq, t)
  if (o.to) osc.frequency.exponentialRampToValueAtTime(o.to, t + dur)
  if (o.vibrato) {
    const lfo = c.createOscillator()
    const lg = c.createGain()
    lfo.frequency.value = 28
    lg.gain.value = o.vibrato
    lfo.connect(lg).connect(osc.frequency)
    lfo.start(t)
    lfo.stop(t + dur)
  }
  const peak = o.gain ?? 0.3
  g.gain.setValueAtTime(0.0001, t)
  g.gain.exponentialRampToValueAtTime(peak, t + (o.attack ?? 0.005))
  g.gain.exponentialRampToValueAtTime(0.0001, t + dur)
  osc.connect(g).connect(o.bus ?? sfxBus)
  osc.start(t)
  osc.stop(t + dur + 0.05)
}

interface NoiseOpts { filter?: BiquadFilterType; freq?: number; to?: number; q?: number; gain?: number; attack?: number; at?: number; bus?: GainNode }

function hiss(dur: number, o: NoiseOpts = {}) {
  const c = audio()
  if (!c) return
  const t = c.currentTime + (o.at ?? 0)
  const src = c.createBufferSource()
  src.buffer = noiseBuffer(c)
  src.loop = true
  const f = c.createBiquadFilter()
  f.type = o.filter ?? 'bandpass'
  f.frequency.setValueAtTime(o.freq ?? 1000, t)
  if (o.to) f.frequency.exponentialRampToValueAtTime(o.to, t + dur)
  f.Q.value = o.q ?? 1
  const g = c.createGain()
  const peak = o.gain ?? 0.3
  const attack = o.attack ?? 0.005
  g.gain.setValueAtTime(0.0001, t)
  g.gain.exponentialRampToValueAtTime(peak, t + attack)
  g.gain.exponentialRampToValueAtTime(0.0001, t + dur)
  src.connect(f).connect(g).connect(o.bus ?? sfxBus)
  src.start(t, Math.random())
  src.stop(t + dur + 0.05)
}

const note = (n: number) => 440 * Math.pow(2, (n - 69) / 12)

export function play(s: Sfx) {
  if (!audio()) return
  switch (s) {
    case 'flip':
      hiss(0.07, { freq: 2800, q: 0.8, gain: 0.25 })
      tone(700, 0.06, { to: 450, gain: 0.08, type: 'triangle' })
      break
    case 'pass':
      hiss(0.22, { freq: 500, to: 1800, q: 1.2, gain: 0.12, attack: 0.05 })
      break
    case 'keep':
      tone(note(76), 0.12, { type: 'triangle', gain: 0.18 })
      tone(note(81), 0.18, { type: 'triangle', gain: 0.18, at: 0.08 })
      break
    case 'tackle':
      tone(140, 0.2, { to: 45, gain: 0.5 })
      hiss(0.12, { filter: 'lowpass', freq: 500, gain: 0.3 })
      break
    case 'whistle':
      tone(2900, 0.32, { gain: 0.12, vibrato: 140, attack: 0.02 })
      break
    case 'whistleLong':
      for (const at of [0, 0.4, 0.8]) tone(2900, at === 0.8 ? 0.7 : 0.3, { gain: 0.12, vibrato: 140, attack: 0.02, at })
      break
    case 'goal':
      hiss(3.2, { filter: 'lowpass', freq: 900, to: 1500, gain: 0.55, attack: 0.25, bus: crowdBus })
      hiss(2.6, { freq: 2500, q: 0.6, gain: 0.12, attack: 0.3, bus: crowdBus })
      ;[60, 64, 67, 72].forEach((n, i) => tone(note(n), 0.5, { type: 'sawtooth', gain: 0.07, at: 0.1 + i * 0.12, attack: 0.02 }))
      tone(note(72), 1.0, { type: 'square', gain: 0.05, at: 0.6 })
      break
    case 'goalAgainst':
      hiss(1.4, { filter: 'lowpass', freq: 500, to: 250, gain: 0.35, attack: 0.15, bus: crowdBus })
      tone(note(55), 0.5, { type: 'triangle', gain: 0.12, to: note(50) })
      break
    case 'miss':
      // the crowd's "ooh"
      hiss(1.0, { freq: 520, to: 300, q: 4, gain: 0.4, attack: 0.15, bus: crowdBus })
      hiss(1.0, { freq: 900, to: 600, q: 5, gain: 0.15, attack: 0.15, bus: crowdBus })
      break
    case 'save':
      tone(180, 0.15, { to: 80, gain: 0.4 })
      hiss(0.1, { freq: 1500, gain: 0.2 })
      hiss(0.8, { freq: 600, to: 400, q: 3, gain: 0.25, attack: 0.1, at: 0.1, bus: crowdBus })
      break
    case 'yellow':
      tone(2900, 0.25, { gain: 0.1, vibrato: 140 })
      tone(1200, 0.12, { type: 'square', gain: 0.06, at: 0.3 })
      break
    case 'red':
      tone(2900, 0.25, { gain: 0.1, vibrato: 140 })
      tone(600, 0.15, { type: 'square', gain: 0.08, at: 0.3 })
      tone(450, 0.25, { type: 'square', gain: 0.08, at: 0.48 })
      break
    case 'steal':
      ;[76, 79, 83, 88].forEach((n, i) => tone(note(n), 0.09, { type: 'triangle', gain: 0.16, at: i * 0.07 }))
      break
    case 'stolen':
      ;[76, 75, 74, 73].forEach((n, i) => tone(note(n), 0.12, { type: 'triangle', gain: 0.16, at: i * 0.1 }))
      break
    case 'trap':
      hiss(0.05, { filter: 'highpass', freq: 3000, gain: 0.4 })
      tone(320, 0.25, { to: 90, gain: 0.3, type: 'square', at: 0.03 })
      tone(2900, 0.2, { gain: 0.1, vibrato: 140, at: 0.2 })
      break
    case 'chance':
      tone(note(60), 0.6, { type: 'sawtooth', gain: 0.05, to: note(72), attack: 0.1 })
      hiss(0.8, { filter: 'lowpass', freq: 600, to: 1400, gain: 0.25, attack: 0.4, bus: crowdBus })
      break
    case 'heartbeat':
      for (const at of [0, 0.25, 0.8, 1.05]) tone(65, 0.14, { gain: 0.5, at })
      break
    case 'click':
      tone(1800, 0.03, { type: 'triangle', gain: 0.06 })
      break
    case 'pack':
      ;[72, 76, 79, 84].forEach((n, i) => tone(note(n), 0.25, { type: 'triangle', gain: 0.12, at: i * 0.06 }))
      break
    case 'packEpic':
      ;[72, 76, 79, 84, 88, 91].forEach((n, i) => tone(note(n), 0.4, { type: 'triangle', gain: 0.12, at: i * 0.07 }))
      hiss(1.0, { filter: 'highpass', freq: 6000, gain: 0.08, attack: 0.3 })
      break
    case 'burn':
      hiss(0.6, { filter: 'lowpass', freq: 3000, to: 400, gain: 0.3, attack: 0.05 })
      break
    case 'error':
      tone(200, 0.15, { type: 'square', gain: 0.06 })
      break
  }
}

/** Sound for one match event (type + card from the server's commentary). */
export function playEvent(type: string, card: string | null, mine: boolean) {
  switch (type) {
    case 'goal': return play(mine ? 'goal' : 'goalAgainst')
    case 'steal': return play(mine ? 'steal' : 'stolen')
    case 'save': return play('save')
    case 'miss': return play('miss')
    case 'yellow': return play('yellow')
    case 'red': return play('red')
    case 'penalty': return play('heartbeat')
    case 'whistle': return play(card === 'HALF_TIME' || card === 'FULL_TIME' ? 'whistleLong' : 'whistle')
    case 'lost':
      if (card === 'OFFSIDE_TRAP') return play('trap')
      if (card === 'OFFSIDE') return play('whistle')
      return play('tackle')
    case 'chance': return play(card === 'CORNER' || card === 'FREE_KICK' ? 'keep' : 'chance')
    case 'hand': return play(card === 'BURN' ? 'burn' : 'click')
    case 'play': return play('pass')
  }
}

// ------------------------------------------------------------------ crowd ambience (during matches)

let crowd: { src: AudioBufferSourceNode; g: GainNode; lfo: OscillatorNode } | null = null

export function startCrowd() {
  const c = audio()
  if (!c || crowd) return
  const src = c.createBufferSource()
  src.buffer = noiseBuffer(c)
  src.loop = true
  const f = c.createBiquadFilter()
  f.type = 'bandpass'
  f.frequency.value = 650
  f.Q.value = 0.7
  const g = c.createGain()
  g.gain.value = 0.12
  const lfo = c.createOscillator()
  const lg = c.createGain()
  lfo.frequency.value = 0.13
  lg.gain.value = 0.04
  lfo.connect(lg).connect(g.gain)
  src.connect(f).connect(g).connect(crowdBus)
  src.start()
  lfo.start()
  crowd = { src, g, lfo }
}

export function stopCrowd() {
  if (!crowd || !ctx) return
  const { src, g, lfo } = crowd
  g.gain.setTargetAtTime(0, ctx.currentTime, 0.3)
  src.stop(ctx.currentTime + 1.5)
  lfo.stop(ctx.currentTime + 1.5)
  crowd = null
}

// ------------------------------------------------------------------ menu music (generated, looping)

let musicTimer: number | null = null
let nextBar = 0
let bar = 0
const BPM = 92
const BEAT = 60 / BPM
// I – vi – IV – V in C, then a lighter turn: C – Em – F – G
const CHORDS = [
  [48, 52, 55], [45, 48, 52], [41, 45, 48], [43, 47, 50],
  [48, 52, 55], [40, 43, 47], [41, 45, 48], [43, 47, 50],
]

function scheduleBar(c: AudioContext, t: number, chord: number[]) {
  const len = BEAT * 4
  // pad
  for (const n of chord) {
    for (const detune of [-6, 6]) {
      const o = c.createOscillator()
      const g = c.createGain()
      const f = c.createBiquadFilter()
      o.type = 'sawtooth'
      o.frequency.value = note(n + 12)
      o.detune.value = detune
      f.type = 'lowpass'
      f.frequency.value = 900
      g.gain.setValueAtTime(0.0001, t)
      g.gain.exponentialRampToValueAtTime(0.025, t + 0.6)
      g.gain.exponentialRampToValueAtTime(0.0001, t + len + 0.3)
      o.connect(f).connect(g).connect(musicBus)
      o.start(t)
      o.stop(t + len + 0.4)
    }
  }
  // bass on beats 1 and 3
  for (const b of [0, 2]) {
    const o = c.createOscillator()
    const g = c.createGain()
    o.type = 'sine'
    o.frequency.value = note(chord[0] - 12)
    const s = t + b * BEAT
    g.gain.setValueAtTime(0.0001, s)
    g.gain.exponentialRampToValueAtTime(0.22, s + 0.02)
    g.gain.exponentialRampToValueAtTime(0.0001, s + BEAT * 1.6)
    o.connect(g).connect(musicBus)
    o.start(s)
    o.stop(s + BEAT * 1.7)
  }
  // arpeggio in eighths
  const arp = [...chord, chord[1] + 12, chord[2] + 12, chord[1] + 12, chord[2], chord[1]].map((n) => n + 24)
  arp.forEach((n, i) => {
    const o = c.createOscillator()
    const g = c.createGain()
    o.type = 'triangle'
    o.frequency.value = note(n)
    const s = t + i * (BEAT / 2)
    g.gain.setValueAtTime(0.0001, s)
    g.gain.exponentialRampToValueAtTime(0.05, s + 0.01)
    g.gain.exponentialRampToValueAtTime(0.0001, s + BEAT * 0.45)
    o.connect(g).connect(musicBus)
    o.start(s)
    o.stop(s + BEAT * 0.5)
  })
  // soft hi-hat on off-beats
  for (let i = 0; i < 4; i++) hiss(0.05, { filter: 'highpass', freq: 7000, gain: 0.03, at: t - c.currentTime + i * BEAT + BEAT / 2, bus: musicBus })
}

export function startMusic() {
  const c = audio()
  if (!c || musicTimer != null) return
  nextBar = c.currentTime + 0.2
  musicTimer = window.setInterval(() => {
    if (!ctx) return
    while (nextBar < ctx.currentTime + 1.5) {
      scheduleBar(ctx, nextBar, CHORDS[bar % CHORDS.length])
      nextBar += BEAT * 4
      bar++
    }
  }, 300)
}

export function stopMusic() {
  if (musicTimer != null) window.clearInterval(musicTimer)
  musicTimer = null
}

/** Browsers only allow sound after the first click: unlock the audio then. */
export function unlockOnFirstGesture(onUnlock: () => void) {
  const handler = () => {
    audio()
    onUnlock()
    window.removeEventListener('pointerdown', handler)
    window.removeEventListener('keydown', handler)
  }
  window.addEventListener('pointerdown', handler)
  window.addEventListener('keydown', handler)
}
