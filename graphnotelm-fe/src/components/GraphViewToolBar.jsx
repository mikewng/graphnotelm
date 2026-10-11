import { useState, useEffect, useRef, forwardRef } from 'react'

function SubmenuItem({ label, items, onSelect, onClose }) {
  const [flyoutStyle, setFlyoutStyle] = useState(null)
  const wrapRef = useRef(null)
  const closeTimer = useRef(null)

  function openFlyout() {
    clearTimeout(closeTimer.current)
    const rect = wrapRef.current.getBoundingClientRect()
    setFlyoutStyle({ position: 'fixed', left: rect.right + 4, top: rect.top })
  }

  function scheduleFlyoutClose() {
    closeTimer.current = setTimeout(() => setFlyoutStyle(null), 120)
  }

  function cancelClose() {
    clearTimeout(closeTimer.current)
  }

  return (
    <div
      className="gv-ctx-submenu-wrap"
      ref={wrapRef}
      onMouseEnter={openFlyout}
      onMouseLeave={scheduleFlyoutClose}
    >
      <div className={`gv-ctx-item gv-ctx-item--arrow${flyoutStyle ? ' hover' : ''}`}>
        <span>{label}</span>
        <svg viewBox="0 0 8 8" fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round">
          <path d="M2 2l3 2-3 2"/>
        </svg>
      </div>
      {flyoutStyle && (
        <div
          className="gv-ctx-flyout"
          style={flyoutStyle}
          onMouseEnter={cancelClose}
          onMouseLeave={scheduleFlyoutClose}
          onMouseDown={e => e.stopPropagation()}
        >
          {items.map(item => (
            <button
              key={item.id}
              className="gv-ctx-item"
              onClick={() => { onSelect(item.id); onClose() }}
            >
              {item.color && <span className="gv-ctx-dot" style={{ background: item.color }} />}
              {item.name}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

const GraphViewContextMenu = forwardRef(function GraphViewContextMenu({
  x, y, nodeId, nodeData, edgeData,
  relTypesList, tagsList,
  onAddRelationship, onAddTag, onDeleteNode, onDeleteEdge, onChangeRelType,
  onQuickEdit, onCreateNote,
  onClose,
}, ref) {
  const [createNoteOpen, setCreateNoteOpen] = useState(false)
  const [newNoteTitle, setNewNoteTitle] = useState('')

  function handleCreateNote() {
    if (!newNoteTitle.trim()) return
    onCreateNote?.(newNoteTitle.trim())
    setNewNoteTitle('')
    setCreateNoteOpen(false)
    onClose()
  }
  const [quickEditOpen, setQuickEditOpen] = useState(false)
  const [editTitle, setEditTitle] = useState(nodeData?.title || '')
  const [editConfidence, setEditConfidence] = useState(
    nodeData?.metadata?.userConfidenceRate != null
      ? nodeData.metadata.userConfidenceRate
      : ''
  )

  useEffect(() => {
    function onKey(e) { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [onClose])

  function handleQuickSave() {
    onQuickEdit(nodeId, {
      title: editTitle,
      confidenceRate: editConfidence !== '' ? Number(editConfidence) : null,
    })
    onClose()
  }

  return (
    <div
      ref={ref}
      className="gv-ctx-menu"
      style={{ position: 'fixed', left: x, top: y }}
    >
      {/* Add Relationship */}
      {/* Create Note — only shown when right-clicking empty canvas (no node, no edge) */}
      {!nodeId && !edgeData && (
        <>
          <button
            className={`gv-ctx-item gv-ctx-item--arrow${createNoteOpen ? ' hover' : ''}`}
            onClick={() => { setCreateNoteOpen(v => !v); setNewNoteTitle('') }}
          >
            <span>Create Note</span>
            <svg viewBox="0 0 8 8" fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round">
              <path d={createNoteOpen ? 'M2 6l3-2-3-2' : 'M2 2l3 2-3 2'} />
            </svg>
          </button>
          {createNoteOpen && (
            <div className="gv-ctx-quick-edit">
              <div className="gv-ctx-quick-field">
                <label>Note title</label>
                <input
                  value={newNoteTitle}
                  onChange={e => setNewNoteTitle(e.target.value)}
                  onKeyDown={e => e.key === 'Enter' && handleCreateNote()}
                  placeholder="Enter title..."
                  autoFocus
                />
              </div>
              <button className="gv-ctx-quick-save" onClick={handleCreateNote} disabled={!newNoteTitle.trim()}>
                Create
              </button>
            </div>
          )}
        </>
      )}
      {/* Edge-specific actions — only when right-clicking a relationship edge */}
      {edgeData && (
        <>
          <SubmenuItem
            label="Change Type"
            items={relTypesList.filter(r => r.id !== edgeData.relationshipId)}
            onSelect={newRelId => {
              onChangeRelType?.(edgeData.sourceId, edgeData.targetId, edgeData.relationshipId, newRelId)
              onClose()
            }}
            onClose={onClose}
          />
          <div className="gv-ctx-divider" />
          <button
            className="gv-ctx-item gv-ctx-item--danger"
            onClick={() => { onDeleteEdge?.(edgeData.sourceId, edgeData.targetId, edgeData.relationshipId); onClose() }}
          >
            Delete edge
          </button>
        </>
      )}

      {/* Node-specific actions — only when right-clicking on a node */}
      {nodeId && (
        <>
          {relTypesList.length > 0 && (
            <SubmenuItem label="Add Relationship" items={relTypesList}
              onSelect={relTypeId => onAddRelationship(nodeId, relTypeId)} onClose={onClose} />
          )}

          {tagsList.length > 0 && (
            <SubmenuItem label="Add Tag" items={tagsList}
              onSelect={tagId => onAddTag(nodeId, tagId)} onClose={onClose} />
          )}

          <div className="gv-ctx-divider" />
          <button
            className={`gv-ctx-item gv-ctx-item--arrow${quickEditOpen ? ' hover' : ''}`}
            onClick={() => setQuickEditOpen(v => !v)}
          >
            <span>Quick Edit</span>
            <svg viewBox="0 0 8 8" fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round">
              <path d={quickEditOpen ? 'M2 6l3-2-3-2' : 'M2 2l3 2-3 2'} />
            </svg>
          </button>

          {quickEditOpen && (
            <div className="gv-ctx-quick-edit">
              <div className="gv-ctx-quick-field">
                <label>Title</label>
                <input value={editTitle} onChange={e => setEditTitle(e.target.value)}
                  onKeyDown={e => e.key === 'Enter' && handleQuickSave()} autoFocus />
              </div>
              <div className="gv-ctx-quick-field">
                <label>Self-rating (0–10)</label>
                <input type="number" min="0" max="10" step="0.1"
                  value={editConfidence} onChange={e => setEditConfidence(e.target.value)}
                  onKeyDown={e => e.key === 'Enter' && handleQuickSave()} />
              </div>
              <button className="gv-ctx-quick-save" onClick={handleQuickSave}>Save</button>
            </div>
          )}

          <div className="gv-ctx-divider" />
          <button className="gv-ctx-item gv-ctx-item--danger"
            onClick={() => { onDeleteNode(nodeId); onClose() }}>
            Delete note
          </button>
        </>
      )}
    </div>
  )
})

export default GraphViewContextMenu
