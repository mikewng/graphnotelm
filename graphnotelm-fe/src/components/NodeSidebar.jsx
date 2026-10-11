import { useState, useRef, useEffect, memo } from 'react'

const NodeSidebar = memo(function NodeSidebar({
  nodesList,
  tagsList,
  tagDefs,
  foldersList = [],
  graphId,
  search,
  setSearch,
  tagFilter,
  setTagFilter,
  selectedNodeId,
  onSelectNode,
  onCreateNode,
  onCreateFolder,
  onEditFolder,
  onDeleteFolder,
  onMoveNodeToFolder,
  pinnedNodeIds = new Set(),
}) {
  const sessionKey = `sidebar-folders-${graphId}`
  const [sortAsc, setSortAsc] = useState(true)
  const [tagsExpanded, setTagsExpanded] = useState(false)
  const [collapsed, setCollapsed] = useState(false)
  const [expandedFolders, setExpandedFolders] = useState(() => {
    try {
      const stored = sessionStorage.getItem(sessionKey)
      return stored ? new Set(JSON.parse(stored)) : new Set()
    } catch { return new Set() }
  })
  const [dragOverTarget, setDragOverTarget] = useState(null) // folderId | '__unfiled__' | null
  const [contextMenu, setContextMenu] = useState(null) // { nodeId, x, y }
  const contextMenuRef = useRef(null)
  const dragNodeIdRef = useRef(null)
  const TAG_LIMIT = 5

  useEffect(() => {
    if (!contextMenu) return
    function onDown(e) {
      if (contextMenuRef.current && !contextMenuRef.current.contains(e.target))
        setContextMenu(null)
    }
    window.addEventListener('mousedown', onDown)
    return () => window.removeEventListener('mousedown', onDown)
  }, [contextMenu])

  const filteredNodes = nodesList
    .filter(n => !search || (n.title || '').toLowerCase().includes(search.toLowerCase()))
    .filter(n => !tagFilter || (n.tags || []).includes(tagFilter))
    .sort((a, b) => {
      const aPinned = pinnedNodeIds.has(a.id)
      const bPinned = pinnedNodeIds.has(b.id)
      if (aPinned !== bPinned) return aPinned ? -1 : 1
      const ta = (a.title || '').toLowerCase()
      const tb = (b.title || '').toLowerCase()
      return sortAsc ? ta.localeCompare(tb) : tb.localeCompare(ta)
    })

  const nodesByFolder = {}
  const unfiledNodes = []
  for (const n of filteredNodes) {
    if (n.folderId) {
      if (!nodesByFolder[n.folderId]) nodesByFolder[n.folderId] = []
      nodesByFolder[n.folderId].push(n)
    } else {
      unfiledNodes.push(n)
    }
  }

  function toggleFolder(folderId) {
    setExpandedFolders(prev => {
      const next = new Set(prev)
      if (next.has(folderId)) next.delete(folderId)
      else next.add(folderId)
      try { sessionStorage.setItem(sessionKey, JSON.stringify([...next])) } catch {}
      return next
    })
  }

  function handleDragStart(e, nodeId) {
    dragNodeIdRef.current = nodeId
    e.dataTransfer.effectAllowed = 'move'
    e.dataTransfer.setData('text/plain', nodeId)
  }

  function handleDragEnd() {
    dragNodeIdRef.current = null
    setDragOverTarget(null)
  }

  function handleDragOver(e, target) {
    e.preventDefault()
    e.dataTransfer.dropEffect = 'move'
    setDragOverTarget(target)
  }

  function handleDragLeave(e) {
    if (!e.currentTarget.contains(e.relatedTarget)) {
      setDragOverTarget(null)
    }
  }

  function handleDrop(e, target) {
    e.preventDefault()
    const nodeId = dragNodeIdRef.current
    if (nodeId) {
      onMoveNodeToFolder(nodeId, target === '__unfiled__' ? null : target)
    }
    dragNodeIdRef.current = null
    setDragOverTarget(null)
  }

  function handleRightClick(e, nodeId) {
    e.preventDefault()
    setContextMenu({ nodeId, x: e.clientX, y: e.clientY })
  }

  function moveToFolder(folderId) {
    if (contextMenu) onMoveNodeToFolder(contextMenu.nodeId, folderId)
    setContextMenu(null)
  }

  function renderNodeItem(n) {
    const primaryTagId = (n.tags || [])[0]
    const dotColor = primaryTagId ? tagDefs[primaryTagId]?.color : null
    const connectionCount =
      (n.relationships?.length || 0) +
      nodesList.filter(o => o.id !== n.id && (o.relationships || []).some(r => r.targetNodeId === n.id)).length
    return (
      <div
        key={n.id}
        className={`note-item${selectedNodeId === n.id ? ' active' : ''}`}
        onClick={() => onSelectNode(n.id)}
        onContextMenu={e => handleRightClick(e, n.id)}
        draggable
        onDragStart={e => handleDragStart(e, n.id)}
        onDragEnd={handleDragEnd}
      >
        <span className="note-dot" style={dotColor ? { background: dotColor } : undefined} />
        <span className="note-name">{n.title || '(untitled)'}</span>
        {pinnedNodeIds.has(n.id) && (
          <span className="pin-indicator" title="Pinned">
            <svg viewBox="0 0 16 16" fill="currentColor" width="11" height="11">
              <path d="M9.828.722a.5.5 0 0 1 .354.146l4.95 4.95a.5.5 0 0 1 0 .707c-.48.48-1.072.588-1.503.588-.177 0-.335-.018-.46-.039l-3.134 3.134a5.927 5.927 0 0 1 .16 1.013c.046.702-.032 1.687-.72 2.375a.5.5 0 0 1-.707 0l-2.829-2.828-3.182 3.182c-.195.195-1.219.902-1.414.707-.195-.195.512-1.22.707-1.414l3.182-3.182-2.828-2.829a.5.5 0 0 1 0-.707c.688-.688 1.673-.767 2.375-.72a5.922 5.922 0 0 1 1.013.16l3.134-3.133a2.772 2.772 0 0 1-.04-.461c0-.43.108-1.022.589-1.503a.5.5 0 0 1 .353-.146z"/>
            </svg>
          </span>
        )}
        {connectionCount > 0 && <span className="note-meta">{connectionCount}</span>}
      </div>
    )
  }

  if (collapsed) {
    return (
      <button
        className="sidebar-expand-btn"
        onClick={() => setCollapsed(false)}
        title="Expand sidebar"
      >›</button>
    )
  }

  return (
    <>
      <div className="sidebar">
        <div className="sidebar-header">
          <span>Notes</span>
          <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
            <button className="btn-ghost" onClick={onCreateFolder}>+ Folder</button>
            <button onClick={onCreateNode}>+ Note</button>
          </div>
        </div>
        <div className="sidebar-search">
          <input
            type="text"
            placeholder="Search nodes..."
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
          <button
            className="sidebar-sort-btn"
            onClick={() => setSortAsc(v => !v)}
            title={sortAsc ? 'Sorted A→Z' : 'Sorted Z→A'}
          >
            {sortAsc ? 'A↑' : 'Z↓'}
          </button>
        </div>
        <div className="sidebar-tag-filter">
          <button
            className={tagFilter === null ? 'active' : ''}
            onClick={() => setTagFilter(null)}
          >all</button>
          {(tagsExpanded ? tagsList : tagsList.slice(0, TAG_LIMIT)).map(t => (
            <button
              key={t.id}
              className={tagFilter === t.id ? 'active' : ''}
              style={t.color ? { color: t.color, borderColor: t.color, background: t.color + '20' } : undefined}
              onClick={() => setTagFilter(tagFilter === t.id ? null : t.id)}
            >{t.name}</button>
          ))}
          {tagsList.length > TAG_LIMIT && (
            <button
              className="tag-expand-btn"
              onClick={() => setTagsExpanded(v => !v)}
            >
              {tagsExpanded ? '↑ less' : `+${tagsList.length - TAG_LIMIT}`}
            </button>
          )}
        </div>

        <div className="node-list">
          {/* Named folder sections */}
          {foldersList.map(folder => {
            const nodes = nodesByFolder[folder.id] || []
            if (search && nodes.length === 0) return null
            const isCollapsed = !expandedFolders.has(folder.id)
            const isDragOver = dragOverTarget === folder.id
            return (
              <div
                key={folder.id}
                className={`folder-section${isDragOver ? ' drag-over' : ''}`}
                onDragOver={e => handleDragOver(e, folder.id)}
                onDragLeave={handleDragLeave}
                onDrop={e => handleDrop(e, folder.id)}
              >
                <div
                  className="folder-header"
                  onClick={() => toggleFolder(folder.id)}
                >
                  <svg
                    className="folder-icon"
                    viewBox="0 0 16 16"
                    fill="currentColor"
                    width="13" height="13"
                    style={folder.color ? { color: folder.color } : undefined}
                  >
                    <path d="M1 3.5A1.5 1.5 0 012.5 2h3.764a.5.5 0 01.447.276L7.5 4H13.5A1.5 1.5 0 0115 5.5v7A1.5 1.5 0 0113.5 14h-11A1.5 1.5 0 011 12.5v-9z"/>
                  </svg>
                  <span className="folder-name">{folder.name}</span>
                  <span className="folder-count">{nodes.length}</span>
                  <span className={`folder-chevron${isCollapsed ? '' : ' open'}`}>›</span>
                  <div className="folder-actions" onClick={e => e.stopPropagation()}>
                    <button
                      className="folder-action-btn"
                      title="Edit folder"
                      onClick={() => onEditFolder(folder.id)}
                    >
                      <svg viewBox="0 0 14 14" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" width="11" height="11">
                        <path d="M9.5 2L12 4.5 5 11.5H2.5V9L9.5 2z"/>
                      </svg>
                    </button>
                    <button
                      className="folder-action-btn folder-action-btn--danger"
                      title="Delete folder"
                      onClick={() => onDeleteFolder(folder.id)}
                    >
                      <svg viewBox="0 0 14 14" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" width="11" height="11">
                        <line x1="3" y1="3" x2="11" y2="11"/><line x1="11" y1="3" x2="3" y2="11"/>
                      </svg>
                    </button>
                  </div>
                </div>
                {!isCollapsed && (
                  <div className="folder-nodes">
                    {nodes.map(renderNodeItem)}
                  </div>
                )}
              </div>
            )
          })}

          {/* Unfiled section */}
          <div
            className={`folder-section${foldersList.length > 0 ? ' folder-section--unfiled' : ''}${dragOverTarget === '__unfiled__' ? ' drag-over' : ''}`}
            onDragOver={e => handleDragOver(e, '__unfiled__')}
            onDragLeave={handleDragLeave}
            onDrop={e => handleDrop(e, '__unfiled__')}
          >
            {foldersList.length > 0 && (
              <div
                className="folder-header"
                onClick={() => toggleFolder('__unfiled__')}
              >
                <svg
                  className="folder-icon folder-icon--unfiled"
                  viewBox="0 0 16 16"
                  fill="currentColor"
                  width="13" height="13"
                >
                  <path d="M1 3.5A1.5 1.5 0 012.5 2h3.764a.5.5 0 01.447.276L7.5 4H13.5A1.5 1.5 0 0115 5.5v7A1.5 1.5 0 0113.5 14h-11A1.5 1.5 0 011 12.5v-9z"/>
                </svg>
                <span className="folder-name">Unfiled</span>
                <span className="folder-count">{unfiledNodes.length}</span>
                <span className={`folder-chevron${expandedFolders.has('__unfiled__') ? ' open' : ''}`}>›</span>
              </div>
            )}
            {(foldersList.length === 0 || expandedFolders.has('__unfiled__')) && unfiledNodes.map(renderNodeItem)}
          </div>
        </div>

        <button
          className="sidebar-collapse-btn"
          onClick={() => setCollapsed(true)}
          title="Collapse sidebar"
        >‹</button>
      </div>

      {contextMenu && (
        <div
          ref={contextMenuRef}
          className="sidebar-context-menu"
          style={{ top: contextMenu.y, left: contextMenu.x }}
        >
          <div className="sidebar-context-menu-label">Move to folder</div>
          {foldersList.map(f => (
            <button key={f.id} className="sidebar-context-menu-item" onClick={() => moveToFolder(f.id)}>
              <svg viewBox="0 0 16 16" fill="currentColor" width="12" height="12" style={f.color ? { color: f.color } : undefined}>
                <path d="M1 3.5A1.5 1.5 0 012.5 2h3.764a.5.5 0 01.447.276L7.5 4H13.5A1.5 1.5 0 0115 5.5v7A1.5 1.5 0 0113.5 14h-11A1.5 1.5 0 011 12.5v-9z"/>
              </svg>
              {f.name}
            </button>
          ))}
          <button className="sidebar-context-menu-item" onClick={() => moveToFolder(null)}>
            <svg viewBox="0 0 16 16" fill="currentColor" width="12" height="12" style={{ color: 'var(--text-3)' }}>
              <path d="M1 3.5A1.5 1.5 0 012.5 2h3.764a.5.5 0 01.447.276L7.5 4H13.5A1.5 1.5 0 0115 5.5v7A1.5 1.5 0 0113.5 14h-11A1.5 1.5 0 011 12.5v-9z"/>
            </svg>
            Unfiled
          </button>
        </div>
      )}
    </>
  )
})

export default NodeSidebar
