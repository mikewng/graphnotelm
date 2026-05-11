import { useEffect } from 'react'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import AuthPage from './pages/AuthPage'
import HomePage from './pages/HomePage'
import NoteGraphPage from './pages/NoteGraphPage'

function isAuthenticated() {
  return !!localStorage.getItem('token')
}

function PrivateRoute({ children }) {
  return isAuthenticated() ? children : <Navigate to="/auth" replace />
}

function PublicOnlyRoute({ children }) {
  return isAuthenticated() ? <Navigate to="/" replace /> : children
}

function LandingRedirect() {
  useEffect(() => { window.location.replace('/landing.html') }, [])
  return null
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/landing" element={<LandingRedirect />} />
        <Route path="/auth" element={<PublicOnlyRoute><AuthPage /></PublicOnlyRoute>} />
        <Route path="/" element={<PrivateRoute><HomePage /></PrivateRoute>} />
        <Route path="/notegraph/:id" element={<PrivateRoute><NoteGraphPage /></PrivateRoute>} />
        <Route path="*" element={<Navigate to={isAuthenticated() ? '/' : '/auth'} replace />} />
      </Routes>
    </BrowserRouter>
  )
}
