import { useState, useEffect } from 'react'

export default function TitleBar() {
  const [maximized, setMaximized] = useState(false)

  useEffect(() => {
    if (!window.electronAPI) return
    document.documentElement.style.setProperty('--titlebar-h', '32px')
    window.electronAPI.isMaximized().then(setMaximized)
    window.electronAPI.onMaximizeChange(setMaximized)
  }, [])

  if (!window.electronAPI) return null

  return (
    <div className="titlebar">
      <span className="titlebar-name">GraphNoteLM</span>
      <div className="titlebar-controls">

        {/* Minimize */}
        <button className="titlebar-btn" onClick={() => window.electronAPI.minimize()} title="Minimize">
          <svg viewBox="0 0 12 12" fill="none">
            <line x1="2" y1="6" x2="10" y2="6" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round"/>
          </svg>
        </button>

        {/* Maximize / Restore */}
        <button className="titlebar-btn" onClick={() => window.electronAPI.maximize()} title={maximized ? 'Restore' : 'Maximize'}>
          {maximized ? (
            <svg viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="1.2">
              <rect x="3.5" y="1.5" width="7" height="7" rx="0.5"/>
              <path d="M1.5 4.5v6h6"/>
            </svg>
          ) : (
            <svg viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="1.2">
              <rect x="2" y="2" width="8" height="8" rx="0.5"/>
            </svg>
          )}
        </button>

        {/* Close */}
        <button className="titlebar-btn titlebar-btn--close" onClick={() => window.electronAPI.close()} title="Close">
          <svg viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round">
            <line x1="2.5" y1="2.5" x2="9.5" y2="9.5"/>
            <line x1="9.5" y1="2.5" x2="2.5" y2="9.5"/>
          </svg>
        </button>

      </div>
    </div>
  )
}
