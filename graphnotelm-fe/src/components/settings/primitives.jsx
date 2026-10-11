import { useState } from 'react'

// Building blocks every settings section is composed from. Sections should reach
// for these rather than raw markup so a new topic inherits the same layout,
// spacing and status conventions for free.

export function SettingsGroup({ title, description, children }) {
  return (
    <section className="set-group">
      {title && <h4 className="set-group-title">{title}</h4>}
      {description && <p className="set-group-desc">{description}</p>}
      <div className="set-group-body">{children}</div>
    </section>
  )
}

export function SettingsField({ label, hint, children }) {
  return (
    <div className="set-field">
      {label && <label className="set-field-label">{label}</label>}
      {children}
      {hint && <p className="set-hint">{hint}</p>}
    </div>
  )
}

export function SettingsRow({ children }) {
  return <div className="set-row">{children}</div>
}

export function SettingsToggle({ label, description, checked, onChange, disabled }) {
  return (
    <div className="set-toggle">
      <button
        type="button"
        role="switch"
        aria-checked={!!checked}
        className={`modal-toggle${checked ? ' on' : ''}`}
        onClick={() => onChange(!checked)}
        disabled={disabled}
      >
        <span className="modal-toggle-thumb" />
      </button>
      <div className="set-toggle-text">
        <span className="set-toggle-label">{label}</span>
        {description && <span className="set-hint">{description}</span>}
      </div>
    </div>
  )
}

/** Password/API-key input with a show/hide affordance. */
export function SecretInput({ value, onChange, placeholder, autoComplete = 'off' }) {
  const [show, setShow] = useState(false)
  return (
    <div className="set-row">
      <input
        className="set-input"
        type={show ? 'text' : 'password'}
        value={value}
        onChange={e => onChange(e.target.value)}
        placeholder={placeholder}
        autoComplete={autoComplete}
      />
      <button type="button" className="btn-ghost" onClick={() => setShow(v => !v)}>
        {show ? 'Hide' : 'Show'}
      </button>
    </div>
  )
}

/**
 * Save button that reflects its own outcome. `status` is one of
 * 'idle' | 'saving' | 'saved'.
 */
export function SaveButton({ status = 'idle', onClick, disabled, children = 'Save' }) {
  return (
    <button
      type="button"
      className="set-save-btn"
      onClick={onClick}
      disabled={disabled || status === 'saving'}
    >
      {status === 'saving' ? 'Saving…' : status === 'saved' ? '✓ Saved' : children}
    </button>
  )
}

export function SettingsMessage({ error, success }) {
  if (!error && !success) return null
  return <p className={error ? 'set-error' : 'set-success'}>{error || success}</p>
}

export function SettingsEmpty({ children }) {
  return <p className="set-empty">{children}</p>
}
