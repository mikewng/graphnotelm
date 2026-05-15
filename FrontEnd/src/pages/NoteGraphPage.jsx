import { useState, useEffect, useRef } from 'react'
import { useParams, useNavigate, useLocation } from 'react-router-dom'
import { noteGraphApi, noteNodeApi, tagApi, relationshipApi } from '../api'
import GraphView from '../components/GraphView'
import NodeSidebar from '../components/NodeSidebar'
import NodeEditor from '../components/NodeEditor'
import MetadataSidebar from '../components/MetadataSidebar'
import ChatSideBar from '../components/ChatSideBar'
import ManageTypesOverlay from '../components/ManageTypesOverlay'
import Modal from '../components/Modal'
import './NoteGraphPage.css'

function dictToArray(dict) {
  if (!dict) return []
  return Object.entries(dict).map(([id, obj]) => ({ ...obj, id }))
}

export default function NoteGraphPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const location = useLocation()

  // ── Graph data ─────────────────────────────────────────────────────────────
  const [graph, setGraph] = useState(null)
  const [graphName, setGraphName] = useState(location.state?.name || '')
  const [error, setError] = useState('')

  // ── UI state ───────────────────────────────────────────────────────────────
  const [search, setSearch] = useState('')
  const [tagFilter, setTagFilter] = useState(null)
  const [showGraph, setShowGraph] = useState(false)
  const [showManageTypes, setShowManageTypes] = useState(false)
  const [showMetadataSidebar, setShowMetadataSidebar] = useState(false)
  const [showChat, setShowChat] = useState(false)
  const [modal, setModal] = useState(null)

  // ── Node selection + history ───────────────────────────────────────────────
  const [selectedNodeId, setSelectedNodeId] = useState(null)
  const [nodeHistory, setNodeHistory] = useState([])
  const [historyIdx, setHistoryIdx] = useState(-1)

  // ── Node editor form ───────────────────────────────────────────────────────
  const [nodeTitle, setNodeTitle] = useState('')
  const [nodeNote, setNodeNote] = useState('')
  const [localTags, setLocalTags] = useState([])
  const [localRelDict, setLocalRelDict] = useState({})
  const [addTagId, setAddTagId] = useState('')
  const [connTarget, setConnTarget] = useState('')
  const [connRelType, setConnRelType] = useState('')
  const [connInverseTarget, setConnInverseTarget] = useState('')
  const [connInverseRelType, setConnInverseRelType] = useState('')

  // ── Auto-save ──────────────────────────────────────────────────────────────
  const [saveStatus, setSaveStatus] = useState('idle') // 'idle' | 'saving' | 'saved'
  const autoSaveTimer = useRef(null)
  const isUserEdit = useRef(false)

  function handleTitleChange(v) { isUserEdit.current = true; setNodeTitle(v) }
  function handleNoteChange(v) { isUserEdit.current = true; setNodeNote(v) }


  // ── Data fetching ──────────────────────────────────────────────────────────
  async function loadGraph() {
    try {
      const data = await noteGraphApi.get(id)
      setGraph(data)
    } catch (err) {
      setError(err.message)
    }
  }

  useEffect(() => { loadGraph() }, [id])

  useEffect(() => {
    if (graphName) return
    noteGraphApi.list().then(list => {
      const entry = list.find(g => g.id === id)
      if (entry?.name) setGraphName(entry.name)
    }).catch(() => {})
  }, [id]) // eslint-disable-line react-hooks/exhaustive-deps

  // Reset title + note only when switching to a different node
  useEffect(() => {
    if (!graph || !selectedNodeId) return
    const node = graph.nodes?.[selectedNodeId]
    if (!node) return
    setNodeTitle(node.title || '')
    setNodeNote(node.note || '')
    setAddTagId('')
    setConnTarget('')
    setConnRelType('')
    setConnInverseTarget('')
    setConnInverseRelType('')
  }, [selectedNodeId]) // eslint-disable-line react-hooks/exhaustive-deps

  // Keep tags + relationships in sync with every graph reload
  useEffect(() => {
    if (!graph || !selectedNodeId) return
    const node = graph.nodes?.[selectedNodeId]
    if (!node) return
    setLocalTags([...(node.tags || [])])
    const dict = {}
    for (const r of (node.relationships || [])) {
      dict[r.targetNodeId] = r.relationshipId
    }
    setLocalRelDict(dict)
  }, [graph, selectedNodeId])

  // Reset auto-save state when switching nodes
  useEffect(() => {
    isUserEdit.current = false
    setSaveStatus('idle')
    clearTimeout(autoSaveTimer.current)
  }, [selectedNodeId])

  // Debounced auto-save — fires 1.5s after user stops typing
  useEffect(() => {
    if (!selectedNodeId || !isUserEdit.current) return
    clearTimeout(autoSaveTimer.current)
    autoSaveTimer.current = setTimeout(async () => {
      setSaveStatus('saving')
      try {
        await noteNodeApi.saveContent(id, selectedNodeId, { title: nodeTitle, note: nodeNote })
        await loadGraph()
        setSaveStatus('saved')
        setTimeout(() => setSaveStatus('idle'), 2000)
      } catch (err) {
        setError(err.message)
        setSaveStatus('idle')
      }
    }, 1500)
    return () => clearTimeout(autoSaveTimer.current)
  }, [nodeTitle, nodeNote]) // eslint-disable-line react-hooks/exhaustive-deps

  // Keyboard shortcuts for back/forward navigation
  useEffect(() => {
    function onKey(e) {
      const inField = ['INPUT', 'TEXTAREA', 'SELECT'].includes(document.activeElement?.tagName)
      if (inField) return
      if ((e.metaKey || e.ctrlKey) && e.key === '[') { e.preventDefault(); navBack() }
      if ((e.metaKey || e.ctrlKey) && e.key === ']') { e.preventDefault(); navForward() }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [historyIdx, nodeHistory]) // eslint-disable-line react-hooks/exhaustive-deps

  // ── Navigation ─────────────────────────────────────────────────────────────
  function navigateTo(nodeId) {
    if (!nodeId || nodeId === selectedNodeId) return
    const newHistory = [...nodeHistory.slice(0, historyIdx + 1), nodeId]
    setNodeHistory(newHistory)
    setHistoryIdx(newHistory.length - 1)
    setSelectedNodeId(nodeId)
  }

  function navBack() {
    if (historyIdx <= 0) return
    const newIdx = historyIdx - 1
    setHistoryIdx(newIdx)
    setSelectedNodeId(nodeHistory[newIdx])
  }

  function navForward() {
    if (historyIdx >= nodeHistory.length - 1) return
    const newIdx = historyIdx + 1
    setHistoryIdx(newIdx)
    setSelectedNodeId(nodeHistory[newIdx])
  }

  if (!graph) return <div style={{ padding: 16 }}>{error || 'Loading...'}</div>

  const nodesList = dictToArray(graph.nodes)
  const tagsList = dictToArray(graph.tags)
  const relTypesList = dictToArray(graph.relationships)

  // ── NoteNode CRUD ──────────────────────────────────────────────────────────
  function handleCreateNode() {
    setModal({
      type: 'input',
      title: 'New node',
      placeholder: 'Node title...',
      confirmLabel: 'Create',
      onConfirm: async (title) => {
        try {
          const created = await noteNodeApi.create(id, { title, note: '' })
          await loadGraph()
          if (created?.id) navigateTo(created.id)
        } catch (err) { setError(err.message) }
      },
    })
  }

  async function handleExport() {
    try {
      const blob = await noteGraphApi.export(id)
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `${graph.name || 'notegraph'}.json`
      a.click()
      URL.revokeObjectURL(url)
    } catch (err) { setError(err.message) }
  }

  async function handleSaveNode() {
    if (!selectedNodeId) return
    clearTimeout(autoSaveTimer.current)
    setSaveStatus('saving')
    try {
      await noteNodeApi.saveContent(id, selectedNodeId, { title: nodeTitle, note: nodeNote })
      await loadGraph()
      setSaveStatus('saved')
      setTimeout(() => setSaveStatus('idle'), 2000)
    } catch (err) {
      setError(err.message)
      setSaveStatus('idle')
    }
  }

  async function handleSaveMetadata(data) {
    if (!selectedNodeId) return
    try {
      await noteNodeApi.updateMetadata(id, selectedNodeId, data)
      await loadGraph()
    } catch (err) { setError(err.message) }
  }

  async function handleGenerateLlmMetadata() {
    if (!selectedNodeId) return
    try {
      await noteNodeApi.generateLlmMetadata(id, selectedNodeId)
      await loadGraph()
    } catch (err) { setError(err.message) }
  }

  function handleDeleteNode() {
    if (!selectedNodeId) return
    const node = graph.nodes?.[selectedNodeId]
    setModal({
      type: 'confirm',
      title: 'Delete node',
      message: `Delete "${node?.title}"? This cannot be undone.`,
      confirmLabel: 'Delete',
      danger: true,
      onConfirm: async () => {
        try {
          await noteNodeApi.delete(id, selectedNodeId)
          setSelectedNodeId(null)
          setNodeHistory([])
          setHistoryIdx(-1)
          await loadGraph()
        } catch (err) { setError(err.message) }
      },
    })
  }

  // ── Tag assignment ─────────────────────────────────────────────────────────
  async function handleAddTag() {
    if (!addTagId || localTags.includes(addTagId)) return
    try {
      await noteNodeApi.addTag(id, selectedNodeId, addTagId)
      setAddTagId('')
      await loadGraph()
    } catch (err) { setError(err.message) }
  }

  async function handleRemoveTag(tagId) {
    try {
      await noteNodeApi.removeTag(id, selectedNodeId, tagId)
      await loadGraph()
    } catch (err) { setError(err.message) }
  }

  // ── Relationship assignment ────────────────────────────────────────────────
  async function handleAddConnection() {
    if (!connTarget || !connRelType || connTarget === selectedNodeId) return
    try {
      await noteNodeApi.addRelationship(id, selectedNodeId, connTarget, connRelType)
      setConnTarget('')
      setConnRelType('')
      await loadGraph()
    } catch (err) { setError(err.message) }
  }

  async function handleAddInverseConnection() {
    if (!connInverseTarget || !connInverseRelType || connInverseTarget === selectedNodeId) return
    try {
      await noteNodeApi.addRelationship(id, connInverseTarget, selectedNodeId, connInverseRelType)
      setConnInverseTarget('')
      setConnInverseRelType('')
      await loadGraph()
    } catch (err) { setError(err.message) }
  }

  async function handleRemoveConnection(targetNodeId) {
    const relationshipId = localRelDict[targetNodeId]
    if (!relationshipId) return
    try {
      await noteNodeApi.removeRelationship(id, selectedNodeId, targetNodeId, relationshipId)
      await loadGraph()
    } catch (err) { setError(err.message) }
  }

  // ── Tag definition CRUD ────────────────────────────────────────────────────
  function handleCreateTag() {
    setModal({
      type: 'input',
      title: 'New tag',
      confirmLabel: 'Create',
      fields: [
        { key: 'tagName', label: 'Name', placeholder: 'Tag name...' },
        { key: 'tagColor', label: 'Color', fieldType: 'color', defaultValue: '#888888', required: false },
      ],
      onConfirm: async ({ tagName, tagColor }) => {
        try {
          await tagApi.create(id, { tagName, tagColor })
          await loadGraph()
        } catch (err) { setError(err.message) }
      },
    })
  }

  function handleEditTag(tagId) {
    const existing = graph.tags?.[tagId]
    setModal({
      type: 'input',
      title: 'Edit tag',
      confirmLabel: 'Save',
      fields: [
        { key: 'tagName', label: 'Name', placeholder: 'Tag name...', defaultValue: existing?.name || '' },
        { key: 'tagColor', label: 'Color', fieldType: 'color', defaultValue: existing?.color || '#888888', required: false },
      ],
      onConfirm: async ({ tagName, tagColor }) => {
        try {
          await tagApi.edit(id, tagId, { id: tagId, tagName, tagColor })
          await loadGraph()
        } catch (err) { setError(err.message) }
      },
    })
  }

  function handleDeleteTag(tagId) {
    const tag = graph.tags?.[tagId]
    setModal({
      type: 'confirm',
      title: 'Delete tag',
      message: `Delete the tag "${tag?.name}"? It will be removed from all nodes.`,
      confirmLabel: 'Delete',
      danger: true,
      onConfirm: async () => {
        try {
          await tagApi.delete(id, tagId)
          if (tagFilter === tagId) setTagFilter(null)
          await loadGraph()
        } catch (err) { setError(err.message) }
      },
    })
  }

  // ── Relationship type CRUD ─────────────────────────────────────────────────
  function handleCreateRelType() {
    setModal({
      type: 'input',
      title: 'New relationship type',
      confirmLabel: 'Create',
      fields: [
        { key: 'type', label: 'Name', placeholder: 'e.g. prerequisite' },
        { key: 'hasInverse', label: 'Has inverse', fieldType: 'toggle', defaultValue: false, required: false },
        { key: 'inverse', label: 'Inverse name', placeholder: 'e.g. leads to', required: false,
          visibleWhen: (v) => v.hasInverse },
        { key: 'color', label: 'Color', fieldType: 'color', defaultValue: '#888888', required: false },
      ],
      onConfirm: async ({ type, hasInverse, inverse, color }) => {
        try {
          await relationshipApi.create(id, { type, inverse: (hasInverse && inverse) || '', color })
          await loadGraph()
        } catch (err) { setError(err.message) }
      },
    })
  }

  function handleEditRelType(relTypeId) {
    const existing = graph.relationships?.[relTypeId]
    setModal({
      type: 'input',
      title: 'Edit relationship type',
      confirmLabel: 'Save',
      fields: [
        { key: 'type', label: 'Name', placeholder: 'e.g. prerequisite', defaultValue: existing?.name || '' },
        { key: 'hasInverse', label: 'Has inverse', fieldType: 'toggle', defaultValue: !!existing?.inverse, required: false },
        { key: 'inverse', label: 'Inverse name', placeholder: 'e.g. leads to', defaultValue: existing?.inverse || '', required: false,
          visibleWhen: (v) => v.hasInverse },
        { key: 'color', label: 'Color', fieldType: 'color', defaultValue: existing?.color || '#888888', required: false },
      ],
      onConfirm: async ({ type, hasInverse, inverse, color }) => {
        try {
          await relationshipApi.edit(id, relTypeId, { id: relTypeId, type, inverse: (hasInverse && inverse) || '', color })
          await loadGraph()
        } catch (err) { setError(err.message) }
      },
    })
  }

  function handleDeleteRelType(relTypeId) {
    const rel = graph.relationships?.[relTypeId]
    setModal({
      type: 'confirm',
      title: 'Delete relationship type',
      message: `Delete the relationship type "${rel?.name}"? This will remove it from all connections.`,
      confirmLabel: 'Delete',
      danger: true,
      onConfirm: async () => {
        try {
          await relationshipApi.delete(id, relTypeId)
          await loadGraph()
        } catch (err) { setError(err.message) }
      },
    })
  }

  // ── Render ─────────────────────────────────────────────────────────────────
  return (
    <div className="page">
      <Modal modal={modal} onCancel={() => setModal(null)} />

      {showGraph && (
        <GraphView
          nodes={nodesList}
          tagDefs={graph.tags || {}}
          relDefs={graph.relationships || {}}
          selectedNodeId={selectedNodeId}
          onClose={() => setShowGraph(false)}
          onSelectNode={node => { navigateTo(node.id); setShowGraph(false) }}
        />
      )}

      <ManageTypesOverlay
        isOpen={showManageTypes}
        tagsList={tagsList}
        relTypesList={relTypesList}
        onAddTag={handleCreateTag}
        onEditTag={handleEditTag}
        onDeleteTag={handleDeleteTag}
        onAddRelType={handleCreateRelType}
        onEditRelType={handleEditRelType}
        onDeleteRelType={handleDeleteRelType}
        onClose={() => setShowManageTypes(false)}
      />

      <header>
        <div className="header-actions" style={{ marginRight: 'auto' }}>
          <button className="btn-ghost" onClick={() => navigate('/')}>← Back</button>
          <div className="nav-btns">
            <button
              className="nav-btn"
              disabled={historyIdx <= 0}
              onClick={navBack}
              title="Go back (Ctrl+[)"
            >
              <svg viewBox="0 0 14 14" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
                <path d="M9 11L5 7l4-4"/>
              </svg>
            </button>
            <button
              className="nav-btn"
              disabled={historyIdx >= nodeHistory.length - 1}
              onClick={navForward}
              title="Go forward (Ctrl+])"
            >
              <svg viewBox="0 0 14 14" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
                <path d="M5 11l4-4-4-4"/>
              </svg>
            </button>
          </div>
          <h2 style={{ color: 'var(--header-text)', marginLeft: 8, display: 'flex', alignItems: 'center' }}>
            {graphName || 'Notegraph'}
          </h2>
        </div>
        <div className="header-actions">
          <button className="btn-icon-header" onClick={handleExport}><span className="app-icon icon-export" />Export JSON</button>
          <button className="btn-icon-header" onClick={() => setShowGraph(true)}><span className="app-icon icon-graph" />Graph View</button>
          <button className="btn-icon-header" onClick={() => setShowManageTypes(true)}><span className="app-icon icon-gear" />Manage Types</button>
          <button
            className={`btn-icon-header${showChat ? ' btn-icon-header--active' : ''}`}
            onClick={() => setShowChat(v => !v)}
            title="Assistant (Ctrl+/)"
          >
            <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" style={{ width: 13, height: 13, flexShrink: 0 }}>
              <path d="M2.5 4L13.5 4A1 1 0 0114.5 5L14.5 11A1 1 0 0113.5 12L6 12L3.5 14L3.5 12A1 1 0 012.5 11Z" />
              <line x1="5.5" y1="7" x2="5.5" y2="7.01" /><line x1="8" y1="7" x2="8" y2="7.01" /><line x1="10.5" y1="7" x2="10.5" y2="7.01" />
            </svg>
            Assistant
          </button>
        </div>
      </header>

      {error && <p style={{ color: 'var(--danger)', padding: '6px 16px', fontSize: 13 }}>{error}</p>}

      <div className="layout">
        <NodeSidebar
          nodesList={nodesList}
          tagsList={tagsList}
          tagDefs={graph.tags || {}}
          search={search} setSearch={setSearch}
          tagFilter={tagFilter} setTagFilter={setTagFilter}
          selectedNodeId={selectedNodeId}
          onSelectNode={navigateTo}
          onCreateNode={handleCreateNode}
        />
        <div className="main-content">
          <NodeEditor
            graph={graph}
            selectedNodeId={selectedNodeId}
            nodesList={nodesList}
            tagsList={tagsList}
            relTypesList={relTypesList}
            nodeTitle={nodeTitle} setNodeTitle={handleTitleChange}
            nodeNote={nodeNote} setNodeNote={handleNoteChange}
            confidenceRate={selectedNodeId ? graph.nodes?.[selectedNodeId]?.metadata?.userConfidenceRate ?? null : null}
            saveStatus={saveStatus}
            localTags={localTags}
            localRelDict={localRelDict}
            addTagId={addTagId} setAddTagId={setAddTagId}
            connTarget={connTarget} setConnTarget={setConnTarget}
            connRelType={connRelType} setConnRelType={setConnRelType}
            connInverseTarget={connInverseTarget} setConnInverseTarget={setConnInverseTarget}
            connInverseRelType={connInverseRelType} setConnInverseRelType={setConnInverseRelType}
            onSave={handleSaveNode}
            onDelete={handleDeleteNode}
            onToggleMetadata={() => setShowMetadataSidebar(v => !v)}
            onAddTag={handleAddTag}
            onRemoveTag={handleRemoveTag}
            onAddConnection={handleAddConnection}
            onAddInverseConnection={handleAddInverseConnection}
            onRemoveConnection={handleRemoveConnection}
            onNavigate={navigateTo}
          />
        </div>
      </div>

      <ChatSideBar
        isOpen={showChat}
        onClose={() => setShowChat(false)}
        graphId={id}
        graphName={graphName}
        selectedNodeTitle={selectedNodeId ? graph.nodes?.[selectedNodeId]?.title : null}
      />
      <MetadataSidebar
        node={selectedNodeId ? graph.nodes?.[selectedNodeId] : null}
        isOpen={showMetadataSidebar}
        onToggle={() => setShowMetadataSidebar(v => !v)}
        onSave={handleSaveMetadata}
        onGenerate={handleGenerateLlmMetadata}
      />

    </div>
  )
}
