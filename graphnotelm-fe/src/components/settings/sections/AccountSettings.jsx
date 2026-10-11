import { useEffect, useState } from 'react'
import { accountApi } from '../../../api'
import {
  decodeToken,
  formatExpiry,
  getSessionExpiry,
  isNonExpiringSession,
  setSessionPreference,
} from '../../../utils/session'
import {
  SaveButton,
  SecretInput,
  SettingsField,
  SettingsGroup,
  SettingsMessage,
  SettingsToggle,
} from '../primitives'

export default function AccountSettings() {
  const [account, setAccount] = useState(null)
  const [loadError, setLoadError] = useState('')

  // ── Profile ────────────────────────────────────────────────────────────────
  const [username, setUsername] = useState('')
  const [email, setEmail] = useState('')
  const [profileStatus, setProfileStatus] = useState('idle')
  const [profileError, setProfileError] = useState('')

  // ── Password ───────────────────────────────────────────────────────────────
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [passwordStatus, setPasswordStatus] = useState('idle')
  const [passwordError, setPasswordError] = useState('')
  const [passwordSuccess, setPasswordSuccess] = useState('')

  // ── Session ────────────────────────────────────────────────────────────────
  const [neverExpire, setNeverExpire] = useState(() => isNonExpiringSession())
  const [expiry, setExpiry] = useState(() => getSessionExpiry())
  const [sessionBusy, setSessionBusy] = useState(false)
  const [sessionError, setSessionError] = useState('')

  useEffect(() => {
    // Show the JWT claims immediately, then reconcile with the server.
    const claims = decodeToken()
    if (claims) {
      setUsername(claims.username ?? '')
      setEmail(claims.email ?? '')
    }

    accountApi.get()
      .then(data => {
        setAccount(data)
        setUsername(data?.username ?? '')
        setEmail(data?.email ?? '')
      })
      .catch(err => setLoadError(err.message))
  }, [])

  // Compare against the loaded account when there is one, but never let a failed
  // load leave Save permanently disabled with nothing explaining why.
  const profileDirty = account
    ? username.trim() !== account.username || email.trim() !== account.email
    : true

  async function handleProfileSave() {
    setProfileError('')
    setProfileStatus('saving')
    try {
      const result = await accountApi.update({ username: username.trim(), email: email.trim() })
      if (result?.session?.accessToken) {
        // Claims changed, so the old token is stale — swap it in place.
        localStorage.setItem('token', result.session.accessToken)
        setExpiry(getSessionExpiry())
        setNeverExpire(isNonExpiringSession())
      }
      if (result?.account) setAccount(result.account)
      setProfileStatus('saved')
      setTimeout(() => setProfileStatus('idle'), 2000)
    } catch (err) {
      setProfileError(err.message)
      setProfileStatus('idle')
    }
  }

  async function handlePasswordSave() {
    setPasswordError('')
    setPasswordSuccess('')
    if (newPassword !== confirmPassword) {
      setPasswordError('New passwords do not match.')
      return
    }
    setPasswordStatus('saving')
    try {
      await accountApi.changePassword({ currentPassword, newPassword })
      setCurrentPassword('')
      setNewPassword('')
      setConfirmPassword('')
      setPasswordStatus('saved')
      setPasswordSuccess('Password updated.')
      setTimeout(() => { setPasswordStatus('idle'); setPasswordSuccess('') }, 3000)
    } catch (err) {
      setPasswordError(err.message)
      setPasswordStatus('idle')
    }
  }

  async function handleSessionToggle(value) {
    setSessionError('')
    setSessionBusy(true)
    try {
      const session = await accountApi.setSessionPreference(value)
      if (session?.accessToken) localStorage.setItem('token', session.accessToken)
      setSessionPreference(value)   // re-applied automatically on the next login
      setNeverExpire(value)
      setExpiry(getSessionExpiry())
    } catch (err) {
      setSessionError(err.message)
    } finally {
      setSessionBusy(false)
    }
  }

  return (
    <>
      <SettingsMessage error={loadError} />

      <SettingsGroup title="Profile" description="Shown on this device and used to sign in.">
        <SettingsField label="Username">
          <input
            className="set-input"
            value={username}
            onChange={e => setUsername(e.target.value)}
            autoComplete="username"
          />
        </SettingsField>

        <SettingsField label="Email">
          <input
            className="set-input"
            type="email"
            value={email}
            onChange={e => setEmail(e.target.value)}
            autoComplete="email"
          />
        </SettingsField>

        <SettingsMessage error={profileError} />
        <SaveButton
          status={profileStatus}
          onClick={handleProfileSave}
          disabled={!profileDirty || !username.trim() || !email.trim()}
        />
      </SettingsGroup>

      <SettingsGroup title="Password">
        <SettingsField label="Current password">
          <SecretInput
            value={currentPassword}
            onChange={setCurrentPassword}
            placeholder="Current password"
            autoComplete="current-password"
          />
        </SettingsField>

        <SettingsField label="New password" hint="At least 6 characters.">
          <SecretInput
            value={newPassword}
            onChange={setNewPassword}
            placeholder="New password"
            autoComplete="new-password"
          />
        </SettingsField>

        <SettingsField label="Confirm new password">
          <SecretInput
            value={confirmPassword}
            onChange={setConfirmPassword}
            placeholder="Repeat new password"
            autoComplete="new-password"
          />
        </SettingsField>

        <SettingsMessage error={passwordError} success={passwordSuccess} />
        <SaveButton
          status={passwordStatus}
          onClick={handlePasswordSave}
          disabled={!currentPassword || newPassword.length < 6 || !confirmPassword}
        >
          Change password
        </SaveButton>
      </SettingsGroup>

      <SettingsGroup title="Session">
        <SettingsToggle
          label="Never expire this session"
          description={
            neverExpire
              ? 'You will stay signed in on this device until you log out.'
              : `Current session expires ${formatExpiry(expiry)}.`
          }
          checked={neverExpire}
          onChange={handleSessionToggle}
          disabled={sessionBusy}
        />
        <p className="set-hint">
          Applies to this device only. Anyone with access to it stays signed in to your account.
        </p>
        <SettingsMessage error={sessionError} />
      </SettingsGroup>

      {account && (
        <SettingsGroup title="Details">
          <dl className="set-facts">
            <dt>Account created</dt>
            <dd>{new Date(account.createdAt).toLocaleDateString()}</dd>
            <dt>Last sign-in</dt>
            <dd>{account.lastLoginAt ? new Date(account.lastLoginAt).toLocaleString() : '—'}</dd>
          </dl>
        </SettingsGroup>
      )}
    </>
  )
}
