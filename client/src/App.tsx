import { BrowserRouter, Route, Routes } from 'react-router-dom'
import MainMenu from './pages/MainMenu'
import FriendlySetup from './pages/FriendlySetup'
import MatchScreen from './pages/MatchScreen'
import CareerNew from './pages/CareerNew'
import CareerHub from './pages/CareerHub'
import LoadGame from './pages/LoadGame'
import Tables from './pages/Tables'
import Editor from './pages/Editor'
import SettingsPage from './pages/SettingsPage'
import HowToPlay from './pages/HowToPlay'
import SoundManager from './components/SoundManager'
import Tutorial from './pages/Tutorial'

export default function App() {
  return (
    <BrowserRouter>
      <SoundManager />
      <Routes>
        <Route path="/" element={<MainMenu />} />
        <Route path="/friendly" element={<FriendlySetup />} />
        <Route path="/match/:id" element={<MatchScreen />} />
        <Route path="/career/new" element={<CareerNew />} />
        <Route path="/career/:id" element={<CareerHub />} />
        <Route path="/load" element={<LoadGame />} />
        <Route path="/tables" element={<Tables />} />
        <Route path="/editor" element={<Editor />} />
        <Route path="/settings" element={<SettingsPage />} />
        <Route path="/how-to-play" element={<HowToPlay />} />
        <Route path="/tutorial" element={<Tutorial />} />
        <Route path="*" element={<MainMenu />} />
      </Routes>
    </BrowserRouter>
  )
}
