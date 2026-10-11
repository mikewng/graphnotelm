import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { SettingsField, SettingsGroup, SettingsMessage } from '../primitives'

const PREFERENCE_PREFIX = 'graphnotelm:'
const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000'

export default function MiscSettings() {
  const navigate = useNavigate()
  const [confirmingReset, setConfirmingReset] = useState(false)
  const [resetMessage, setResetMessage] = useState('')

  const preferenceKeys = Object.keys(localStorage).filter(k => k.startsWith(PREFERENCE_PREFIX))

  function handleResetPreferences() {
    preferenceKeys.forEach(k => localStorage.removeItem(k))
    setConfirmingReset(false)
    setResetMessage(`Cleared ${preferenceKeys.length} local preference${preferenceKeys.length === 1 ? '' : 's'}.`)
    setTimeout(() => setResetMessage(''), 3000)
  }

  function handleSignOut() {
    localStorage.removeItem('token')
    navigate('/auth')
  }

  return (
    <>
      <SettingsGroup title="Environment">
        <dl className="set-facts">
          <dt>Running in</dt>
          <dd>{window.electronAPI ? 'Desktop app (Electron)' : 'Browser'}</dd>
          <dt>Backend</dt>
          <dd>{API_URL}</dd>
        </dl>
      </SettingsGroup>

      <SettingsGroup
        title="Local data"
        description="Preferences this device stores outside your account — cached provider keys, session choices and UI state."
      >
        <SettingsField hint={`${preferenceKeys.length} preference${preferenceKeys.length === 1 ? '' : 's'} stored on this device. Your notegraphs are not affected.`}>
          {!confirmingReset ? (
            <button
              className="btn-ghost set-self-start"
              onClick={() => setConfirmingReset(true)}
              disabled={preferenceKeys.length === 0}
            >
              Clear local preferences
            </button>
          ) : (
            <div className="set-confirm">
              <p className="set-error">Clear all local preferences on this device?</p>
              <div className="set-row">
                <button className="btn-danger" onClick={handleResetPreferences}>Yes, clear</button>
                <button className="btn-ghost" onClick={() => setConfirmingReset(false)}>Cancel</button>
              </div>
            </div>
          )}
        </SettingsField>
        <SettingsMessage success={resetMessage} />
      </SettingsGroup>

      <SettingsGroup title="Session">
        <SettingsField hint="Removes the stored token and returns you to the login screen.">
          <button className="btn-ghost set-self-start" onClick={handleSignOut}>Sign out</button>
        </SettingsField>
      </SettingsGroup>
    </>
  )
}
