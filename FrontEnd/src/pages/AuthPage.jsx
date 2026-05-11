import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { auth } from '../api'
import './AuthPage.css'

export default function AuthPage() {
  const [mode, setMode] = useState('login')
  const [username, setUsername] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [loading, setLoading] = useState(false)
  const navigate = useNavigate()

  async function handleSubmit(e) {
    e.preventDefault()
    setError('')
    setSuccess('')
    setLoading(true)
    try {
      const res = mode === 'login'
        ? await auth.login(email, password)
        : await auth.register(username, email, password)
      if (res?.accessToken) {
        localStorage.setItem('token', res.accessToken)
        navigate('/')
      } else if (mode === 'register') {
        setSuccess('Account created! Please log in.')
        setMode('login')
        setUsername('')
        setPassword('')
      }
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="auth-page">
      <div className="auth-card">
        <h2>{mode === 'login' ? 'Login' : 'Register'}</h2>
        <form className="auth-form" onSubmit={handleSubmit}>
          {mode === 'register' && (
            <div className="form-group">
              <label>Username</label>
              <input value={username} onChange={e => setUsername(e.target.value)} required />
            </div>
          )}
          <div className="form-group">
            <label>Email</label>
            <input type="email" value={email} onChange={e => setEmail(e.target.value)} required />
          </div>
          <div className="form-group">
            <label>Password</label>
            <input type="password" value={password} onChange={e => setPassword(e.target.value)} required />
          </div>
          {error && <p className="auth-error">{error}</p>}
          {success && <p className="auth-success">{success}</p>}
          <button type="submit" className="btn-submit" disabled={loading}>
            {loading
              ? <span className="auth-spinner" />
              : mode === 'login' ? 'Login' : 'Register'
            }
          </button>
        </form>
        <div className="auth-switch">
          {mode === 'login'
            ? <button type="button" onClick={() => { setMode('register'); setError('') }}>Create an account</button>
            : <button type="button" onClick={() => { setMode('login'); setError('') }}>Back to Login</button>
          }
        </div>
      </div>
    </div>
  )
}
