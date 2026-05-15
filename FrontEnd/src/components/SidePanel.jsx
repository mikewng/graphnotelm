import { useResizable } from '../hooks/useResizable'

export default function SidePanel({ title, onClose, defaultWidth = 300, isOpen, headerActions, children }) {
  const { width, onMouseDown } = useResizable(defaultWidth)

  return (
    <div className={`side-panel${isOpen ? ' open' : ''}`} style={{ width }}>
      <div className="side-panel-resize-handle" onMouseDown={onMouseDown} />
      <div className="side-panel-header">
        <span className="side-panel-title">{title}</span>
        <div className="side-panel-header-actions">
          {headerActions}
          <button className="btn-ghost btn-icon-sm" onClick={onClose} title="Close">
            <span className="app-icon icon-close" />
          </button>
        </div>
      </div>
      <div className="side-panel-body">
        {children}
      </div>
    </div>
  )
}
