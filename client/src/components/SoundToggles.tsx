import { useSettings } from '../settings'

/** Quick on/off buttons for all sound and for the music (also in Settings; M toggles the sound). */
export default function SoundToggles() {
  const [s, update] = useSettings()
  return (
    <div className="sound-toggles" role="group" aria-label="Sound">
      <button className={`btn btn-ghost small ${s.muted ? '' : 'active'}`} onClick={() => update({ muted: !s.muted })} title="Sound on/off (M)">
        {s.muted ? 'Sound: off' : 'Sound: on'}
      </button>
      <button className={`btn btn-ghost small ${s.musicOn && !s.muted ? 'active' : ''}`} onClick={() => update({ musicOn: !s.musicOn })} title="Menu music on/off">
        {s.musicOn ? 'Music: on' : 'Music: off'}
      </button>
    </div>
  )
}
