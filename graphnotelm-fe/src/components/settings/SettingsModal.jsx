import { useEffect, useMemo, useState } from 'react'
import { getAvailableSections } from './registry'
import './SettingsModal.css'

/**
 * Settings shell. Knows nothing about any individual topic — it renders whatever
 * the registry exposes, so adding a section never touches this file.
 */
export default function SettingsModal({ open, onClose, initialSectionId }) {
  const sections = useMemo(() => (open ? getAvailableSections() : []), [open])
  const [activeId, setActiveId] = useState(initialSectionId ?? null)

  // Reset to the requested (or first) section each time the modal opens.
  useEffect(() => {
    if (open) setActiveId(initialSectionId ?? getAvailableSections()[0]?.id ?? null)
  }, [open, initialSectionId])

  useEffect(() => {
    if (!open) return
    function onKeyDown(e) { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [open, onClose])

  if (!open) return null

  const active = sections.find(s => s.id === activeId) ?? sections[0]

  return (
    <div
      className="set-backdrop"
      onMouseDown={e => { if (e.target === e.currentTarget) onClose() }}
    >
      <div className="set-modal" role="dialog" aria-modal="true" aria-label="Settings">
        <nav className="set-nav">
          <div className="set-nav-title">Settings</div>
          <ul className="set-nav-list">
            {sections.map(section => (
              <li key={section.id}>
                <button
                  type="button"
                  className={`set-nav-item${section.id === active?.id ? ' active' : ''}`}
                  onClick={() => setActiveId(section.id)}
                >
                  <span className="set-nav-icon">{section.icon}</span>
                  {section.label}
                </button>
              </li>
            ))}
          </ul>
        </nav>

        <div className="set-pane">
          {/* A <div>, not a <header> — the global `header` rule is the app's 44px top bar. */}
          <div className="set-pane-header">
            <div>
              <h3 className="set-pane-title">{active?.label}</h3>
              {active?.description && <p className="set-pane-desc">{active.description}</p>}
            </div>
            <button className="btn-ghost set-close" onClick={onClose} aria-label="Close settings">
              <span className="app-icon icon-close" />
            </button>
          </div>

          <div className="set-pane-body">
            {active ? <active.Component /> : null}
          </div>
        </div>
      </div>
    </div>
  )
}
