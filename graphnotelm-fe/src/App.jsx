import { useEffect, lazy, Suspense } from 'react'
import { HashRouter, Routes, Route, Navigate } from 'react-router-dom'
import AuthPage from './pages/AuthPage'
import HomePage from './pages/HomePage'
import TitleBar from './components/TitleBar'

// The graph page brings in the editor, chat and graph libraries, about 80% of the bundle.
// Loading it on demand keeps all of that out of startup.
const loadNoteGraphPage = () => import('./pages/NoteGraphPage')
const NoteGraphPage = lazy(loadNoteGraphPage)

function isAuthenticated() {
  return !!localStorage.getItem('token')
}

function PrivateRoute({ children }) {
  return isAuthenticated() ? children : <Navigate to="/auth" replace />
}

function PublicOnlyRoute({ children }) {
  return isAuthenticated() ? <Navigate to="/" replace /> : children
}

const isElectron = navigator.userAgent.includes('Electron')

function LandingRedirect() {
  useEffect(() => {
    if (!isElectron) window.location.replace('/landing.html')
  }, [])
  if (isElectron) return <Navigate to={isAuthenticated() ? '/' : '/auth'} replace />
  return null
}

export default function App() {
  // Fetch the graph page once the first screen is idle, so opening a graph doesn't wait on it.
  // Browsers without requestIdleCallback load it when it's first opened instead.
  useEffect(() => {
    if (!window.requestIdleCallback) return
    const id = requestIdleCallback(() => loadNoteGraphPage().catch(() => {}))
    return () => cancelIdleCallback(id)
  }, [])

  return (
    <HashRouter>
      <TitleBar />
      <Routes>
        <Route path="/landing" element={<LandingRedirect />} />
        <Route path="/auth" element={<PublicOnlyRoute><AuthPage /></PublicOnlyRoute>} />
        <Route path="/" element={<PrivateRoute><HomePage /></PrivateRoute>} />
        <Route path="/notegraph/:id" element={<PrivateRoute><Suspense fallback={null}><NoteGraphPage /></Suspense></PrivateRoute>} />
        <Route path="*" element={<Navigate to={isAuthenticated() ? '/' : '/auth'} replace />} />
      </Routes>
    </HashRouter>
  )
}
