// Session/token helpers shared by the auth flow and the Account settings section.

const SESSION_PREF_KEY = 'graphnotelm:session-never-expire'

// A "never expiring" session is really a token minted ~100 years out — JWTs must
// carry an exp. Anything this far ahead is treated as non-expiring in the UI.
const NEVER_EXPIRES_AFTER_YEARS = 50

export function decodeToken(token = localStorage.getItem('token')) {
  if (!token) return null
  try {
    const payload = token.split('.')[1]
    if (!payload) return null
    const binary = atob(payload.replace(/-/g, '+').replace(/_/g, '/'))
    const bytes = Uint8Array.from(binary, c => c.charCodeAt(0))
    return JSON.parse(new TextDecoder().decode(bytes))
  } catch {
    return null
  }
}

export function getSessionExpiry(token) {
  const exp = decodeToken(token)?.exp
  return typeof exp === 'number' ? new Date(exp * 1000) : null
}

export function isNonExpiringSession(token) {
  const expiry = getSessionExpiry(token)
  if (!expiry) return false
  return (expiry - Date.now()) / (365.25 * 24 * 60 * 60 * 1000) >= NEVER_EXPIRES_AFTER_YEARS
}

export function getSessionPreference() {
  return localStorage.getItem(SESSION_PREF_KEY) === 'true'
}

export function setSessionPreference(neverExpire) {
  if (neverExpire) localStorage.setItem(SESSION_PREF_KEY, 'true')
  else localStorage.removeItem(SESSION_PREF_KEY)
}

export function formatExpiry(expiry) {
  if (!expiry) return 'Unknown'
  const minutes = Math.round((expiry - Date.now()) / 60000)
  if (minutes <= 0) return 'Expired'
  if (minutes < 60) return `in ${minutes} min`
  const hours = Math.round(minutes / 60)
  if (hours < 24) return `in ${hours} ${hours === 1 ? 'hour' : 'hours'}`
  const days = Math.round(hours / 24)
  return `in ${days} ${days === 1 ? 'day' : 'days'}`
}
