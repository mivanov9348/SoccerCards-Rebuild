import { Link } from 'react-router-dom'
import { api, seasonLabel } from '../api'
import { useLoad } from '../components/common'

export default function MainMenu() {
  const { data: careers, error } = useLoad(() => api.careers(), [])
  const latest = careers?.[0]

  return (
    <div className="main-menu">
      <div className="logo">
        <div className="logo-cards">
          <span className="logo-card c1">⚽</span>
          <span className="logo-card c2">⚡</span>
          <span className="logo-card c3">🛡</span>
        </div>
        <h1>Soccer Cards</h1>
        <p className="tagline">The football card game · Bulgarian First League and European cups</p>
      </div>

      <nav className="menu">
        {latest && (
          <Link className="menu-item primary" to={`/career/${latest.id}`}>
            Continue career
            <small>
              {latest.team} · {latest.year ? seasonLabel(latest.year) : ''} · week {latest.week}/{latest.totalWeeks}
            </small>
          </Link>
        )}
        <Link className="menu-item" to="/friendly">
          Friendly match
          <small>Pick two teams and play the computer</small>
        </Link>
        <Link className="menu-item" to="/career/new">
          New career
          <small>Take over a small club and lead it to glory</small>
        </Link>
        <Link className="menu-item" to="/load">
          Load game
          <small>Saved careers</small>
        </Link>
        <Link className="menu-item" to="/tables">
          Tables & teams
        </Link>
        <Link className="menu-item" to="/editor">
          Team & league editor
        </Link>
        <Link className="menu-item" to="/how-to-play">
          How to play
        </Link>
        <Link className="menu-item" to="/settings">
          Settings
        </Link>
      </nav>
      {error && <p className="error-box">{error}</p>}
      <footer className="footer">Original game · Generated badges · All data is editable</footer>
    </div>
  )
}
