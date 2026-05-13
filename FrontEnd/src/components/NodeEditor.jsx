import { useEffect, useRef } from 'react'
import SearchSelect from './SearchSelect'

export default function NodeEditor({
  graph,
  selectedNodeId,
  nodesList,
  tagsList,
  relTypesList,
  nodeTitle, setNodeTitle,
  nodeNote, setNodeNote,
  localTags,
  localRelDict,
  addTagId, setAddTagId,
  connTarget, setConnTarget,
  connRelType, setConnRelType,
  connInverseTarget, setConnInverseTarget,
  connInverseRelType, setConnInverseRelType,
  confidenceRate,
  saveStatus,
  onSave,
  onDelete,
  onToggleMetadata,
  onAddTag,
  onRemoveTag,
  onAddConnection,
  onAddInverseConnection,
  onRemoveConnection,
  onNavigate,
}) {
  const noteRef = useRef(null)

  // Sync contenteditable when switching nodes — read directly from graph
  // so we always get the correct content regardless of when nodeNote state updates
  useEffect(() => {
    const el = noteRef.current
    if (!el) return
    el.innerHTML = graph.nodes?.[selectedNodeId]?.note || ''
  }, [selectedNodeId]) // eslint-disable-line react-hooks/exhaustive-deps

  function handleNoteInput(e) {
    setNodeNote(e.currentTarget.innerHTML)
  }

  function applyFormat(command) {
    noteRef.current?.focus()
    document.execCommand(command, false, null)
  }

  function handleNoteKeyDown(e) {
    if (e.ctrlKey || e.metaKey) {
      if (e.key === 'b') { e.preventDefault(); applyFormat('bold') }
      if (e.key === 'i') { e.preventDefault(); applyFormat('italic') }
      if (e.key === 'u') { e.preventDefault(); applyFormat('underline') }
    }
  }

  function getNodeTitle(nodeId) { return graph.nodes?.[nodeId]?.title || nodeId }
  function getTagName(tagId) { return graph.tags?.[tagId]?.name || tagId }
  function getRelTypeName(relTypeId) { return graph.relationships?.[relTypeId]?.name || relTypeId }
  function getRelTypeInverse(relTypeId) {
    const rel = graph.relationships?.[relTypeId]
    return rel?.inverse || null
  }

  const outgoing = Object.entries(localRelDict).map(([targetNodeId, relTypeId]) => ({ targetNodeId, relTypeId }))
  const incoming = nodesList.flatMap(n => {
    if (n.id === selectedNodeId) return []
    return (n.relationships || [])
      .filter(r => r.targetNodeId === selectedNodeId)
      .map(r => ({ sourceNodeId: n.id, relTypeId: r.relationshipId }))
  })

  if (!selectedNodeId) {
    return <p className="empty-editor">Select a node or create a new one.</p>
  }

  return (
    <div className="editor-body" key={selectedNodeId}>

      {/* Title + actions */}
      <div className="topic-row">
        <input
          className="topic-input"
          value={nodeTitle}
          onChange={e => setNodeTitle(e.target.value)}
          placeholder="node title..."
        />
        <div className="form-actions">
          {saveStatus === 'saving' && <span className="save-indicator save-spinner" title="Saving…" />}
          {saveStatus === 'saved' && (
            <span className="save-indicator save-check">
              <svg viewBox="0 0 14 14" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M2 7l4 4 6-6" />
              </svg>
              Saved
            </span>
          )}
          <span
            className="confidence-badge"
            title={confidenceRate != null ? `Confidence: ${confidenceRate}/10` : 'No confidence score set'}
          >
            {confidenceRate != null ? 'Confidence: ' + confidenceRate : 'Confidence: —'}
          </span>
          <button className="btn-meta-info" onClick={onToggleMetadata} title="Node metadata">i</button>
        </div>
      </div>

      {/* Note */}
      <div className="field">
        <div className="field-label">Note</div>
        <div
          ref={noteRef}
          className="note-editor"
          contentEditable
          suppressContentEditableWarning
          onInput={handleNoteInput}
          onKeyDown={handleNoteKeyDown}
          data-placeholder="start writing..."
        />
      </div>

      {/* Tags */}
      <div className="field">
        <div className="field-label">Tags</div>
        <div className="tags-row">
          {localTags.length === 0 && <span className="field-empty">No tags assigned.</span>}
          {localTags.map(tagId => {
            const tagColor = graph.tags?.[tagId]?.color
            return (
              <span
                key={tagId}
                className="tag-pill"
                style={tagColor ? { color: tagColor, borderColor: tagColor, background: tagColor + '20' } : undefined}
              >
                {getTagName(tagId)}
                <button className="pill-remove" onClick={() => onRemoveTag(tagId)}>×</button>
              </span>
            )
          })}
        </div>
        <div className="form-actions" style={{ marginTop: 8 }}>
          <select value={addTagId} onChange={e => setAddTagId(e.target.value)}>
            <option value="">tag...</option>
            {tagsList
              .filter(t => !localTags.includes(t.id))
              .map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
          </select>
          <button className="btn-outline" onClick={onAddTag}>Add</button>
        </div>
        {tagsList.length === 0 && <p className="hint">Create tags in Manage Types.</p>}
      </div>

      {/* Add connection */}
      <div className="field">
        <div className="field-label">Add Connection</div>
        <div className="link-row">
          <span className="link-row-source">{nodeTitle || '(untitled)'}</span>
          <SearchSelect
            value={connRelType}
            onChange={setConnRelType}
            options={relTypesList.map(r => ({ id: r.id, label: r.name }))}
            placeholder="connects to..."
          />
          <SearchSelect
            value={connTarget}
            onChange={setConnTarget}
            options={nodesList
              .filter(n => n.id !== selectedNodeId && !localRelDict[n.id])
              .map(n => ({ id: n.id, label: n.title || '(untitled)' }))}
            placeholder="target node..."
          />
          <button className="btn-outline" onClick={onAddConnection}>Add</button>
        </div>
        <div className="link-row">
          <SearchSelect
            value={connInverseTarget}
            onChange={setConnInverseTarget}
            options={nodesList
              .filter(n => n.id !== selectedNodeId && !incoming.some(r => r.sourceNodeId === n.id))
              .map(n => ({ id: n.id, label: n.title || '(untitled)' }))}
            placeholder="source node..."
          />
          <SearchSelect
            value={connInverseRelType}
            onChange={setConnInverseRelType}
            options={relTypesList.map(r => ({ id: r.id, label: r.name }))}
            placeholder="connects to..."
          />
          <span className="link-row-source">{nodeTitle || '(untitled)'}</span>
          <button className="btn-outline" onClick={onAddInverseConnection}>Add</button>
        </div>
        {relTypesList.length === 0 && <p className="hint">Create relationship types in Manage Types.</p>}
      </div>

      {/* Connections */}
      <div className="field">
        <div className="field-label">Connections</div>
        <div className="relations-grid">

          <div className="relation-col">
            <div className="field-label">
              <span className="arrow-glyph arrow-out">→</span> outgoing
            </div>
            <div className="relation-list">
              {outgoing.length === 0
                ? <span className="relation-empty">no outgoing connections</span>
                : outgoing.map(r => {
                    const relColor = graph.relationships?.[r.relTypeId]?.color
                    return (
                      <div
                        key={r.targetNodeId}
                        className="relation-tag outgoing"
                        style={relColor ? { '--rel-color': relColor } : undefined}
                        onClick={() => onNavigate(r.targetNodeId)}
                      >
                        <span>{r.relTypeId ? `${getRelTypeName(r.relTypeId)} · ` : ''}{getNodeTitle(r.targetNodeId)}</span>
                        <span className="x" onClick={e => { e.stopPropagation(); onRemoveConnection(r.targetNodeId) }}>×</span>
                      </div>
                    )
                  })
              }
            </div>
          </div>

          <div className="relation-col">
            <div className="field-label">
              <span className="arrow-glyph arrow-in">←</span> incoming
            </div>
            <div className="relation-list">
              {incoming.length === 0
                ? <span className="relation-empty">nothing points here yet</span>
                : incoming.map(r => {
                    const relColor = graph.relationships?.[r.relTypeId]?.color
                    return (
                      <div
                        key={r.sourceNodeId}
                        className="relation-tag incoming"
                        style={relColor ? { '--rel-color': relColor } : undefined}
                        onClick={() => onNavigate(r.sourceNodeId)}
                      >
                        <span>{r.relTypeId && getRelTypeInverse(r.relTypeId) ? `${getRelTypeInverse(r.relTypeId)} · ` : ''}{getNodeTitle(r.sourceNodeId)}</span>
                      </div>
                    )
                  })
              }
            </div>
          </div>

        </div>
      </div>

      <div className="editor-delete-row">
        <button className="btn-delete-node" onClick={onDelete}>Delete node</button>
      </div>

    </div>
  )
}
