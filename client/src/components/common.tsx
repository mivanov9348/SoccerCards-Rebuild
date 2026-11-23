import { useCallback, useEffect, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { api, type Fixture, type StandingRow, type TableZone } from '../api'

/** Generated badge: a shield in the club colors with its initials (no real crests). */
export function Badge({ name, primary, secondary, size = 40 }: { name: string; primary: string; secondary: string; size?: number }) {
  const letters = name
    .split(/[\s-]+/)
    .filter(Boolean)
    .map((w) => w[0])
    .join('')
    .slice(0, 3)
    .toUpperCase()
  const id = `b${primary.slice(1)}${secondary.slice(1)}`
  return (
    <svg width={size} height={size * 1.15} viewBox="0 0 40 46" className="badge" aria-label={name} role="img">
      <defs>
        <clipPath id={id}>
          <path d="M20 2 L37 8 V22 C37 33 29 41 20 44 C11 41 3 33 3 22 V8 Z" />
        </clipPath>
      </defs>
      <g clipPath={`url(#${id})`}>
        <rect x="0" y="0" width="40" height="46" fill={primary} />
        <rect x="20" y="0" width="20" height="46" fill={secondary} opacity="0.9" />
        <rect x="0" y="16" width="40" height="13" fill="rgba(0,0,0,0.55)" />
      </g>
      <path d="M20 2 L37 8 V22 C37 33 29 41 20 44 C11 41 3 33 3 22 V8 Z" fill="none" stroke="#111" strokeWidth="1.5" />
      <text x="20" y="26.5" textAnchor="middle" fontSize={letters.length > 2 ? 9 : 11} fontWeight="700" fill="#fff">
        {letters}
      </text>
    </svg>
  )
}

export function Screen({ title, back = '/', children, actions }: { title: string; back?: string | null; children: ReactNode; actions?: ReactNode }) {
  return (
    <div className="screen">
      <header className="screen-header">
        {back !== null && (
          <Link className="btn btn-ghost" to={back}>
            ← Back
          </Link>
        )}
        <h1>{title}</h1>
        <div className="screen-actions">{actions}</div>
      </header>
      {children}
    </div>
  )
}

export function ErrorBox({ error }: { error: string | null }) {
  if (!error) return null
  return <div className="error-box">{error}</div>
}

/** Loads data asynchronously and keeps the error as text. */
export function useLoad<T>(loader: () => Promise<T>, deps: unknown[]) {
  const [data, setData] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const load = useCallback(loader, deps)
  const reload = useCallback(async () => {
    setLoading(true)
    try {
      setData(await load())
      setError(null)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }, [load])
  useEffect(() => {
    void reload()
  }, [reload])
  return { data, error, loading, reload, setData, setError }
}

/** Zones of a league table, by league id (cached: they only change with seed.json). */
const zoneCache = new Map<number, Promise<TableZone[]>>()
function useZones(leagueId?: number) {
  const [zones, setZones] = useState<TableZone[]>([])
  useEffect(() => {
    if (!leagueId) return setZones([])
    let alive = true
    if (!zoneCache.has(leagueId)) zoneCache.set(leagueId, api.leagueZones(leagueId).catch(() => []))
    void zoneCache.get(leagueId)!.then((z) => alive && setZones(z))
    return () => {
      alive = false
    }
  }, [leagueId])
  return zones
}

/**
 * A league table. With leagueId the places are coloured by that league's own rules — champion, European places,
 * promotion and relegation (as many as really go up or down) — with a legend underneath.
 */
export function StandingsTable({ rows, highlight, leagueId }: { rows: StandingRow[]; highlight?: number; leagueId?: number }) {
  const zones = useZones(leagueId)
  const zoneOf = (pos: number) => zones.find((z) => pos >= z.from && pos <= z.to)?.kind ?? ''
  const kinds = zones.filter((z, i) => zones.findIndex((x) => x.kind === z.kind) === i)
  return (
    <>
    <table className="table standings">
      <thead>
        <tr>
          <th>#</th>
          <th className="left">Team</th>
          <th>P</th>
          <th>W</th>
          <th>D</th>
          <th>L</th>
          <th>Goals</th>
          <th>GD</th>
          <th>Pts</th>
        </tr>
      </thead>
      <tbody>
        {rows.map((r) => (
          <tr
            key={r.teamId}
            className={[
              r.teamId === highlight ? 'highlight' : '',
              zoneOf(r.position) ? `zone-${zoneOf(r.position)}` : '',
            ].join(' ')}
          >
            <td>{r.position}</td>
            <td className="left team-cell">
              <Badge name={r.name} primary={r.primaryColor} secondary={r.secondaryColor} size={18} />
              {r.name}
            </td>
            <td>{r.played}</td>
            <td>{r.won}</td>
            <td>{r.drawn}</td>
            <td>{r.lost}</td>
            <td>
              {r.goalsFor}:{r.goalsAgainst}
            </td>
            <td>{r.goalDifference > 0 ? `+${r.goalDifference}` : r.goalDifference}</td>
            <td className="points">{r.points}</td>
          </tr>
        ))}
      </tbody>
    </table>
    {kinds.length > 0 && (
      <div className="legend small muted">
        {kinds.map((z) => (
          <span key={z.kind}>
            <i className={`lg ${z.kind}`} />
            {z.label}
          </span>
        ))}
        {zones.every((z) => z.kind !== 'relegation' && z.kind !== 'promotion') && <span> · no relegation (one division only)</span>}
      </div>
    )}
    </>
  )
}

export function FixtureRow({ f, highlight }: { f: Fixture; highlight?: number }) {
  const mine = f.home.id === highlight || f.away.id === highlight
  const result = f.played ? `${f.homeGoals} : ${f.awayGoals}` : '– : –'
  return (
    <div className={`fixture ${mine ? 'mine' : ''}`}>
      <span className={`fx-team home ${f.winnerId === f.home.id ? 'winner' : ''}`}>
        {f.home.name}
        <Badge name={f.home.name} primary={f.home.primaryColor} secondary={f.home.secondaryColor} size={18} />
      </span>
      <span className="fx-score">
        {result}
        {f.homePenalties != null && <small> (p. {f.homePenalties}:{f.awayPenalties})</small>}
      </span>
      <span className={`fx-team away ${f.winnerId === f.away.id ? 'winner' : ''}`}>
        <Badge name={f.away.name} primary={f.away.primaryColor} secondary={f.away.secondaryColor} size={18} />
        {f.away.name}
      </span>
    </div>
  )
}

export function RatingBar({ label, value }: { label: string; value: number }) {
  return (
    <div className="rating">
      <span>{label}</span>
      <div className="rating-bar">
        <div style={{ width: `${value}%` }} />
      </div>
      <b>{value}</b>
    </div>
  )
}

const COUNTRY_ORDER = ['Bulgaria', 'England', 'Spain', 'Germany', 'Italy', 'France']

/** Groups items by country for <optgroup> lists: Bulgaria and the top five first, then the rest alphabetically. */
export function byCountry<T>(items: T[], country: (t: T) => string): [string, T[]][] {
  const map = new Map<string, T[]>()
  for (const it of items) {
    const c = country(it) || 'Other'
    map.set(c, [...(map.get(c) ?? []), it])
  }
  const rank = (c: string) => {
    const i = COUNTRY_ORDER.indexOf(c)
    return i < 0 ? 100 : i
  }
  return [...map.entries()].sort(([a], [b]) => rank(a) - rank(b) || a.localeCompare(b))
}
