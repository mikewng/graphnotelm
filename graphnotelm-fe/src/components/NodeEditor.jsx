import { useEffect, useRef, useState, useMemo, memo, useCallback } from 'react'
import { useEditor, EditorContent } from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import Placeholder from '@tiptap/extension-placeholder'
import SearchSelect from './SearchSelect'
import NoteEditorToolbar from './NoteEditorToolbar'
import { NodeRef } from './NodeRefExtension'
import { NoteImage } from './NoteImageExtension'
import { getImageFiles, uploadAndInsertImages } from '../utils/imageUpload'
import { useNodePreview, NodePreviewCard } from './NodePreview'
import { formatDue } from '../utils/review'
import NodeFlashcards from './NodeFlashcards'

const CONNECTIONS_OPEN_KEY = 'graphnotelm:connectionsOpen'

// Group edges by relationship type so each type's label is shown once rather than on every chip.
function groupByRelType(edges) {
  const groups = new Map()
  for (const edge of edges) {
    const key = edge.relTypeId || ''
    if (!groups.has(key)) groups.set(key, [])
    groups.get(key).push(edge)
  }
  return [...groups].map(([relTypeId, groupEdges]) => ({ relTypeId, edges: groupEdges }))
}

const NodeEditor = memo(function NodeEditor({
  graph,
  graphId,
  selectedNodeId,
  nodesList,
  tagsList,
  relTypesList,
  nodeTitle, setNodeTitle,
  setNodeNote,
  localTags,
  localRelDict,
  connTarget, setConnTarget,
  connRelType, setConnRelType,
  connInverseTarget, setConnInverseTarget,
  connInverseRelType, setConnInverseRelType,
  noteLoadVersion,
  nodeLoading,
  confidenceRate,
  selfRating = null,
  memory = null,
  onReviewNote,
  onCardsChanged,
  saveStatus,
  isPinned,
  onTogglePin,
  onSave,
  onDelete,
  onToggleMetadata,
  onConfirmDraft,
  draftFolderId,
  onSetDraftFolder,
  foldersList = [],
  draftIncoming = [],
  onRemoveDraftIncoming,
  onAddTag,
  onRemoveTag,
  onAddConnection,
  onAddInverseConnection,
  onRemoveConnection,
  onNavigate,
  onError,
}) {
  // useEditor binds editorProps once, and this component stays mounted while the
  // selected node changes — so the paste/drop handlers read current values from refs.
  const editorRef = useRef(null)
  const imageContextRef = useRef(null)
  imageContextRef.current = { graphId, nodeId: selectedNodeId, onError }

  function insertImageFiles(files) {
    const editorInstance = editorRef.current
    const { graphId: currentGraphId, nodeId } = imageContextRef.current
    if (!editorInstance || files.length === 0) return
    if (!currentGraphId || !nodeId || nodeId === '__draft__') {
      imageContextRef.current.onError?.('Create the note before adding images.')
      return
    }
    uploadAndInsertImages(editorInstance, files, () => imageContextRef.current)
  }

  const editor = useEditor({
    extensions: [
      StarterKit.configure({
        link: {
          // Autolink only text that declares itself a URL. The default linkifies
          // any `word.tld`, so ordinary prose becomes a link — a missing space
          // after a period ("is.It") turns into a link to an Italian domain.
          // `shouldAutoLink` receives the matched text, not the normalised href.
          shouldAutoLink: url => /^(https?:\/\/|www\.)/i.test(url),
        },
      }),
      Placeholder.configure({ placeholder: 'start writing...' }),
      NodeRef,
      NoteImage,
    ],
    content: graph.nodes?.[selectedNodeId]?.note || '',
    editable: !nodeLoading,
    onUpdate: ({ editor }) => setNodeNote(editor.getHTML()),
    editorProps: {
      // Only take over when the clipboard carries image files; text and HTML paste as usual.
      handlePaste: (_view, event) => {
        const files = getImageFiles(event.clipboardData?.files)
        if (files.length === 0) return false
        event.preventDefault()
        insertImageFiles(files)
        return true
      },
      handleDrop: (view, event, _slice, moved) => {
        if (moved) return false // an existing node dragged within the editor
        const files = getImageFiles(event.dataTransfer?.files)
        if (files.length === 0) return false
        event.preventDefault()
        const target = view.posAtCoords({ left: event.clientX, top: event.clientY })
        if (target) editorRef.current?.commands.setTextSelection(target.pos)
        insertImageFiles(files)
        return true
      },
    },
  })
  editorRef.current = editor

  // Node-ref picker
  const [refPicker, setRefPicker] = useState(null) // { x, y, query } | null
  const refPickerRef = useRef(null)
  useEffect(() => {
    if (!refPicker) return
    function onDown(e) {
      if (refPickerRef.current && !refPickerRef.current.contains(e.target))
        setRefPicker(null)
    }
    window.addEventListener('mousedown', onDown)
    return () => window.removeEventListener('mousedown', onDown)
  }, [refPicker])

  function handleInsertNodeRef(rect) {
    if (!editor) return
    if (editor.isActive('nodeRef')) {
      editor.chain().focus().unsetNodeRef().run()
      return
    }
    setRefPicker({ x: rect.left, y: rect.bottom + 6, query: '' })
  }

  function handlePickNode(node) {
    if (!editor) return
    editor.chain().focus().setNodeRef({ nodeId: node.id, nodeTitle: node.title || '' }).run()
    setRefPicker(null)
  }

  // Mark stale node-refs (referenced node was deleted)
  useEffect(() => {
    if (!editor) return
    const editorEl = editor.view.dom
    const nodeIds = new Set(nodesList.map(n => n.id))
    editorEl.querySelectorAll('a[data-node-id]').forEach(el => {
      const nid = el.getAttribute('data-node-id')
      el.classList.toggle('node-ref--stale', !!nid && !nodeIds.has(nid))
    })
  }, [editor, nodesList])

  // Intercept clicks on node-ref links — capture phase so we fire before Electron's shell handler
  const editorContentRef = useRef(null)
  useEffect(() => {
    const el = editorContentRef.current
    if (!el) return
    function onCapture(e) {
      const link = e.target.closest('[data-node-id]')
      if (!link) return
      e.preventDefault()
      e.stopPropagation()
      const nodeId = link.getAttribute('data-node-id')
      if (nodeId && !link.classList.contains('node-ref--stale')) onNavigate(nodeId)
    }
    el.addEventListener('click', onCapture, true)
    return () => el.removeEventListener('click', onCapture, true)
  }, [onNavigate])

  // Folder dropdown for draft node
  const [folderDropdownOpen, setFolderDropdownOpen] = useState(false)
  const folderDropdownRef = useRef(null)
  useEffect(() => {
    if (!folderDropdownOpen) return
    function onDown(e) {
      if (folderDropdownRef.current && !folderDropdownRef.current.contains(e.target))
        setFolderDropdownOpen(false)
    }
    window.addEventListener('mousedown', onDown)
    return () => window.removeEventListener('mousedown', onDown)
  }, [folderDropdownOpen])

  // Connections section — collapsed by default; the choice carries across notes and sessions
  const [connectionsOpen, setConnectionsOpen] = useState(() => localStorage.getItem(CONNECTIONS_OPEN_KEY) === 'true')
  function toggleConnections() {
    const next = !connectionsOpen
    setConnectionsOpen(next)
    localStorage.setItem(CONNECTIONS_OPEN_KEY, String(next))
  }

  // 'out' adds this note → another; 'in' adds another note → this one
  const [connDirection, setConnDirection] = useState('out')

  // Hover preview for connection chips; a chip can unmount under the cursor when the note changes
  const nodePreview = useNodePreview(graph, graphId)
  useEffect(() => { nodePreview.hide() }, [selectedNodeId]) // eslint-disable-line react-hooks/exhaustive-deps

  // Sticky header — detect when header has scrolled to the top of the scroll container
  const headerRef = useRef(null)
  const [isSticky, setIsSticky] = useState(false)

  useEffect(() => {
    const header = headerRef.current
    if (!header) return
    let container = header.parentElement
    while (container && container !== document.body) {
      const oy = window.getComputedStyle(container).overflowY
      if (oy === 'auto' || oy === 'scroll') break
      container = container.parentElement
    }
    if (!container || container === document.body) return
    setIsSticky(false)
    const check = () => {
      if (!headerRef.current) return
      const hTop = headerRef.current.getBoundingClientRect().top
      const cTop = container.getBoundingClientRect().top
      setIsSticky(Math.round(hTop) <= Math.round(cTop))
    }
    container.addEventListener('scroll', check, { passive: true })
    return () => container.removeEventListener('scroll', check)
  }, [selectedNodeId])

  // Re-sync editor content when the loaded note changes for the current node
  // (selectedNodeId switches are handled by the `key` on editor-body remounting this component)
  useEffect(() => {
    if (!editor) return
    const note = graph.nodes?.[selectedNodeId]?.note || ''
    if (note !== editor.getHTML()) editor.commands.setContent(note, { emitUpdate: false })
  }, [editor, noteLoadVersion]) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    editor?.setEditable(!nodeLoading, false)
  }, [editor, nodeLoading])

  function getNodeTitle(nodeId) { return graph.nodes?.[nodeId]?.title || nodeId }
  function getTagName(tagId) { return graph.tags?.[tagId]?.name || tagId }
  function getRelTypeName(relTypeId) { return graph.relationships?.[relTypeId]?.name || relTypeId }
  function getRelTypeInverse(relTypeId) {
    const rel = graph.relationships?.[relTypeId]
    return rel?.inverse || null
  }

  const outgoing = useMemo(
    () => Object.entries(localRelDict).map(([targetNodeId, relTypeId]) => ({ targetNodeId, relTypeId })),
    [localRelDict]
  )
  const computedIncoming = useMemo(
    () => nodesList.flatMap(n => {
      if (n.id === selectedNodeId) return []
      return (n.relationships || [])
        .filter(r => r.targetNodeId === selectedNodeId)
        .map(r => ({ sourceNodeId: n.id, relTypeId: r.relationshipId }))
    }),
    [nodesList, selectedNodeId]
  )
  const isDraft = selectedNodeId === '__draft__'
  const incoming = isDraft ? draftIncoming : computedIncoming

  if (!selectedNodeId) {
    return <p className="empty-editor">Select a node or create a new one.</p>
  }

  return (
    <div className="editor-body" key={selectedNodeId}>

      <div ref={headerRef} className={`editor-sticky-header${isSticky ? ' is-pinned' : ''}`}>
        {/* Title + actions */}
        <div className="topic-row">
          <input
            className="topic-input"
            value={nodeTitle}
            onChange={e => setNodeTitle(e.target.value)}
            placeholder="node title..."
            disabled={nodeLoading}
          />
          <div className="form-actions">
            {isDraft ? (
              <div className="draft-create-group" ref={folderDropdownRef}>
                <button
                  onClick={onConfirmDraft}
                  disabled={!nodeTitle.trim()}
                  title="Create node"
                  className={`draft-create-main${foldersList.length > 0 ? ' draft-create-main--split' : ''}`}
                >
                  Create
                  {draftFolderId && (() => {
                    const f = foldersList.find(f => f.id === draftFolderId)
                    return f ? (
                      <span className="draft-folder-badge" style={f.color ? { background: f.color } : undefined} />
                    ) : null
                  })()}
                </button>
                {foldersList.length > 0 && (
                  <button
                    className="draft-create-chevron"
                    onClick={() => setFolderDropdownOpen(v => !v)}
                    title="Choose folder"
                    disabled={!nodeTitle.trim()}
                  >
                    <svg viewBox="0 0 8 5" fill="currentColor" width="8" height="5">
                      <path d="M0 0l4 5 4-5z"/>
                    </svg>
                  </button>
                )}
                {folderDropdownOpen && (
                  <div className="draft-folder-dropdown">
                    <button
                      className={`draft-folder-item${!draftFolderId ? ' selected' : ''}`}
                      onClick={() => { onSetDraftFolder(null); setFolderDropdownOpen(false) }}
                    >
                      <svg className="folder-icon folder-icon--unfiled" viewBox="0 0 16 16" fill="currentColor" width="12" height="12">
                        <path d="M1 3.5A1.5 1.5 0 012.5 2h3.764a.5.5 0 01.447.276L7.5 4H13.5A1.5 1.5 0 0115 5.5v7A1.5 1.5 0 0113.5 14h-11A1.5 1.5 0 011 12.5v-9z"/>
                      </svg>
                      Unfiled
                    </button>
                    {foldersList.map(f => (
                      <button
                        key={f.id}
                        className={`draft-folder-item${draftFolderId === f.id ? ' selected' : ''}`}
                        onClick={() => { onSetDraftFolder(f.id); setFolderDropdownOpen(false) }}
                      >
                        <svg className="folder-icon" viewBox="0 0 16 16" fill="currentColor" width="12" height="12" style={f.color ? { color: f.color } : undefined}>
                          <path d="M1 3.5A1.5 1.5 0 012.5 2h3.764a.5.5 0 01.447.276L7.5 4H13.5A1.5 1.5 0 0115 5.5v7A1.5 1.5 0 0113.5 14h-11A1.5 1.5 0 011 12.5v-9z"/>
                        </svg>
                        {f.name}
                      </button>
                    ))}
                  </div>
                )}
              </div>
            ) : (
              <>
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
                  className={`confidence-badge${memory ? ' confidence-badge--measured' : ''}`}
                  title={memory
                    ? `Measured from ${memory.reviews} review${memory.reviews === 1 ? '' : 's'} — the chance you'd still remember this a week from now. `
                      + `Next review ${formatDue(memory.dueAt)}.${selfRating ? ` Self-rated ${selfRating}.` : ''}`
                    : confidenceRate != null
                      ? `Self-rated ${confidenceRate}/10. Review this note to measure it.`
                      : 'No confidence yet. Rate it in Metadata, or review the note.'}
                >
                  {confidenceRate != null ? 'Confidence: ' + confidenceRate : 'Confidence: —'}
                </span>
                <button
                  className={`btn-meta-info${isPinned ? ' btn-pin-active' : ''}`}
                  onClick={onTogglePin}
                  title={isPinned ? 'Unpin note' : 'Pin note'}
                >
                  <svg viewBox="0 0 16 16" fill="currentColor" width="13" height="13">
                    <path d="M9.828.722a.5.5 0 0 1 .354.146l4.95 4.95a.5.5 0 0 1 0 .707c-.48.48-1.072.588-1.503.588-.177 0-.335-.018-.46-.039l-3.134 3.134a5.927 5.927 0 0 1 .16 1.013c.046.702-.032 1.687-.72 2.375a.5.5 0 0 1-.707 0l-2.829-2.828-3.182 3.182c-.195.195-1.219.902-1.414.707-.195-.195.512-1.22.707-1.414l3.182-3.182-2.828-2.829a.5.5 0 0 1 0-.707c.688-.688 1.673-.767 2.375-.72a5.922 5.922 0 0 1 1.013.16l3.134-3.133a2.772 2.772 0 0 1-.04-.461c0-.43.108-1.022.589-1.503a.5.5 0 0 1 .353-.146z"/>
                  </svg>
                </button>
                <button className="btn-meta-info" onClick={onToggleMetadata} title="Node metadata"><span className="app-icon icon-info" /></button>
              </>
            )}
          </div>
        </div>

        {/* Tags — compact row under the title; hidden while the header is pinned */}
        <div className="header-tags">
          {localTags.map(tagId => {
            const tagColor = graph.tags?.[tagId]?.color
            return (
              <span
                key={tagId}
                className="tag-pill"
                style={tagColor ? { color: tagColor, borderColor: tagColor, background: tagColor + '20' } : undefined}
              >
                {getTagName(tagId)}
                <button className="pill-remove" onClick={() => onRemoveTag(tagId)} title="Remove tag"><span className="app-icon icon-close" /></button>
              </span>
            )
          })}
          {tagsList.length === 0
            ? <span className="header-tags-hint">Create tags in Manage Types to label notes.</span>
            : tagsList.some(t => !localTags.includes(t.id)) && (
                <div className="header-tag-picker">
                  <SearchSelect
                    value=""
                    onChange={tagId => { if (tagId) onAddTag(tagId) }}
                    options={tagsList
                      .filter(t => !localTags.includes(t.id))
                      .map(t => ({ id: t.id, label: t.name }))}
                    placeholder="+ tag"
                  />
                </div>
              )}
        </div>

        <NoteEditorToolbar
          editor={editor}
          onInsertNodeRef={handleInsertNodeRef}
          onInsertImages={files => insertImageFiles(getImageFiles(files))}
          canInsertImages={!isDraft && !!graphId && !nodeLoading}
        />
      </div>

      {/* Note */}
      <div className={`field note-editor-frame${nodeLoading ? ' note-editor--loading' : ''}`}>
        <div ref={editorContentRef}>
          <EditorContent editor={editor} className="note-editor" />
        </div>
      </div>

      {/* Node-ref picker */}
      {refPicker && (
        <div
          ref={refPickerRef}
          className="node-ref-picker"
          style={{ top: refPicker.y, left: refPicker.x }}
        >
          <input
            className="node-ref-picker-search"
            autoFocus
            placeholder="Search notes…"
            value={refPicker.query}
            onChange={e => setRefPicker(p => ({ ...p, query: e.target.value }))}
          />
          <div className="node-ref-picker-list">
            {nodesList
              .filter(n => n.id !== selectedNodeId && n.id !== '__draft__')
              .filter(n => !refPicker.query || (n.title || '').toLowerCase().includes(refPicker.query.toLowerCase()))
              .slice(0, 30)
              .map(n => (
                <button
                  key={n.id}
                  className="node-ref-picker-item"
                  onMouseDown={e => { e.preventDefault(); handlePickNode(n) }}
                >
                  {n.title || '(untitled)'}
                </button>
              ))
            }
            {nodesList.filter(n => n.id !== selectedNodeId && n.id !== '__draft__' &&
              (!refPicker.query || (n.title || '').toLowerCase().includes(refPicker.query.toLowerCase()))
            ).length === 0 && (
              <div className="node-ref-picker-empty">No notes found</div>
            )}
          </div>
        </div>
      )}

      {/* Connections — collapsible; the header keeps edge counts visible while closed */}
      <div className="field">
        <button
          type="button"
          className="connections-toggle"
          onClick={toggleConnections}
          aria-expanded={connectionsOpen}
        >
          <span className={`folder-chevron${connectionsOpen ? ' open' : ''}`}>›</span>
          <span className="field-label">Connections</span>
          <span className="connections-summary" title={`${outgoing.length} outgoing, ${incoming.length} incoming`}>
            <span className="arrow-out">→</span> {outgoing.length}
            <span className="arrow-in">←</span> {incoming.length}
          </span>
        </button>

        {connectionsOpen && (
          <>
            <div className="relations-grid">

              <div className="relation-col">
                <div className="field-label">
                  <span className="arrow-glyph arrow-out">→</span> outgoing
                </div>
                {outgoing.length === 0
                  ? <span className="relation-empty">no outgoing connections</span>
                  : groupByRelType(outgoing).map(({ relTypeId, edges }) => {
                      const relColor = graph.relationships?.[relTypeId]?.color
                      return (
                        <div
                          key={relTypeId}
                          className="relation-group"
                          style={relColor ? { '--rel-color': relColor } : undefined}
                        >
                          <div className="relation-group-label">{relTypeId ? getRelTypeName(relTypeId) : 'untyped'}</div>
                          <div className="relation-list">
                            {edges.map(r => (
                              <div
                                key={r.targetNodeId}
                                className="relation-tag outgoing"
                                onClick={() => onNavigate(r.targetNodeId)}
                                onMouseEnter={e => nodePreview.show(r.targetNodeId, e.currentTarget)}
                                onMouseLeave={nodePreview.hide}
                              >
                                <span className="relation-tag-title">{getNodeTitle(r.targetNodeId)}</span>
                                <span className="x" onClick={e => { e.stopPropagation(); nodePreview.hide(); onRemoveConnection(r.targetNodeId) }}><span className="app-icon icon-close" /></span>
                              </div>
                            ))}
                          </div>
                        </div>
                      )
                    })
                }
              </div>

              <div className="relation-col">
                <div className="field-label">
                  <span className="arrow-glyph arrow-in">←</span> incoming
                </div>
                {incoming.length === 0
                  ? <span className="relation-empty">nothing points here yet</span>
                  : groupByRelType(incoming).map(({ relTypeId, edges }) => {
                      const relColor = graph.relationships?.[relTypeId]?.color
                      return (
                        <div
                          key={relTypeId}
                          className="relation-group"
                          style={relColor ? { '--rel-color': relColor } : undefined}
                        >
                          <div className="relation-group-label">
                            {relTypeId ? (getRelTypeInverse(relTypeId) || getRelTypeName(relTypeId)) : 'untyped'}
                          </div>
                          <div className="relation-list">
                            {edges.map(r => (
                              <div
                                key={r.sourceNodeId}
                                className="relation-tag incoming"
                                onClick={() => onNavigate(r.sourceNodeId)}
                                onMouseEnter={e => nodePreview.show(r.sourceNodeId, e.currentTarget)}
                                onMouseLeave={nodePreview.hide}
                              >
                                <span className="relation-tag-title">{getNodeTitle(r.sourceNodeId)}</span>
                                {isDraft && <span className="x" onClick={e => { e.stopPropagation(); nodePreview.hide(); onRemoveDraftIncoming(r.sourceNodeId) }}><span className="app-icon icon-close" /></span>}
                              </div>
                            ))}
                          </div>
                        </div>
                      )
                    })
                }
              </div>

            </div>

            <div className="connections-add">
              <div className="field-label">Add connection</div>
              <div className="link-row">
                <div className="direction-toggle" role="group" aria-label="Connection direction">
                  <button
                    type="button"
                    className={connDirection === 'out' ? 'active' : ''}
                    onClick={() => setConnDirection('out')}
                    title="From this note to another"
                  >
                    <span className="arrow-out">→</span>
                  </button>
                  <button
                    type="button"
                    className={connDirection === 'in' ? 'active' : ''}
                    onClick={() => setConnDirection('in')}
                    title="From another note to this one"
                  >
                    <span className="arrow-in">←</span>
                  </button>
                </div>
                {connDirection === 'out' ? (
                  <>
                    <span className="link-row-source">{nodeTitle || '(untitled)'}</span>
                    <SearchSelect
                      key="out-rel"
                      value={connRelType}
                      onChange={setConnRelType}
                      options={relTypesList.map(r => ({ id: r.id, label: r.name }))}
                      placeholder="connects to..."
                    />
                    <SearchSelect
                      key="out-target"
                      value={connTarget}
                      onChange={setConnTarget}
                      options={nodesList
                        .filter(n => n.id !== selectedNodeId && !localRelDict[n.id])
                        .map(n => ({ id: n.id, label: n.title || '(untitled)' }))}
                      placeholder="target node..."
                    />
                    <button className="btn-outline" onClick={onAddConnection}>Add</button>
                  </>
                ) : (
                  <>
                    <SearchSelect
                      key="in-source"
                      value={connInverseTarget}
                      onChange={setConnInverseTarget}
                      options={nodesList
                        .filter(n => n.id !== selectedNodeId && !incoming.some(r => r.sourceNodeId === n.id))
                        .map(n => ({ id: n.id, label: n.title || '(untitled)' }))}
                      placeholder="source node..."
                    />
                    <SearchSelect
                      key="in-rel"
                      value={connInverseRelType}
                      onChange={setConnInverseRelType}
                      options={relTypesList.map(r => ({ id: r.id, label: r.name }))}
                      placeholder="connects to..."
                    />
                    <span className="link-row-source">{nodeTitle || '(untitled)'}</span>
                    <button className="btn-outline" onClick={onAddInverseConnection}>Add</button>
                  </>
                )}
              </div>
              {relTypesList.length === 0 && <p className="hint">Create relationship types in Manage Types.</p>}
            </div>
          </>
        )}
      </div>

      {!isDraft && (
        <NodeFlashcards
          graphId={graphId}
          nodeId={selectedNodeId}
          onError={onError}
          onCardsChanged={onCardsChanged}
          onReview={onReviewNote}
        />
      )}

      {!isDraft && (
        <div className="editor-delete-row">
          <button className="btn-delete-node" onClick={onDelete}>Delete note</button>
        </div>
      )}

      <NodePreviewCard
        preview={nodePreview.preview}
        title={nodePreview.preview ? getNodeTitle(nodePreview.preview.nodeId) : ''}
      />

    </div>
  )
})

export default NodeEditor
