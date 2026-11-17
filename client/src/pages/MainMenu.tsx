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
        <h1>Карти Футбол</h1>
        <p className="tagline">Футболен мениджър със ситуационни карти · Първа лига и европейските турнири</p>
      </div>

      <nav className="menu">
        {latest && (
          <Link className="menu-item primary" to={`/career/${latest.id}`}>
            Продължи кариерата
            <small>
              {latest.team} · {latest.year ? seasonLabel(latest.year) : ''} · седмица {latest.week}/{latest.totalWeeks}
            </small>
          </Link>
        )}
        <Link className="menu-item" to="/friendly">
          Приятелски мач
          <small>Изберете два отбора и играйте</small>
        </Link>
        <Link className="menu-item" to="/career/new">
          Нова кариера
          <small>Поемете малък клуб и го изведете до върха</small>
        </Link>
        <Link className="menu-item" to="/load">
          Зареди игра
          <small>Запазени кариери</small>
        </Link>
        <Link className="menu-item" to="/tables">
          Класирания и отбори
        </Link>
        <Link className="menu-item" to="/editor">
          Редактор на отбори и лиги
        </Link>
        <Link className="menu-item" to="/how-to-play">
          Как се играе
        </Link>
        <Link className="menu-item" to="/settings">
          Настройки
        </Link>
      </nav>
      {error && <p className="error-box">{error}</p>}
      <footer className="footer">Оригинална игра · Емблемите са генерирани · Данните могат да се редактират</footer>
    </div>
  )
}
