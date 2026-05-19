import { useState, useEffect, useRef, useMemo, useCallback } from 'react'
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

const DRAFT_ID = '__draft__'

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
  const nodeNoteRef = useRef('')         // ref — no re-render on every keystroke
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

  // ── Node content lazy-load ─────────────────────────────────────────────────
  const [noteLoadVersion, setNoteLoadVersion] = useState(0)
  const [nodeLoading, setNodeLoading] = useState(false)

  // Keep selectedNodeId accessible in async callbacks without stale closure
  const selectedNodeIdRef = useRef(selectedNodeId)
  useEffect(() => { selectedNodeIdRef.current = selectedNodeId }, [selectedNodeId])

  function handleTitleChange(v) {
    setNodeTitle(v)
    if (selectedNodeId === DRAFT_ID) {
      clearTimeout(autoSaveTimer.current)
      if (v.trim()) {
        autoSaveTimer.current = setTimeout(async () => {
          try {
            const created = await noteNodeApi.create(id, { title: v.trim(), note: nodeNoteRef.current })
            if (created?.id) {
              await loadGraph()
              setNodeHistory(prev => prev.map(nid => nid === DRAFT_ID ? created.id : nid))
              setSelectedNodeId(created.id)
            }
          } catch (err) { setError(err.message) }
        }, 1500)
      }
    } else {
      isUserEdit.current = true
      scheduleSave()
    }
  }

  function handleNoteChange(v) {
    isUserEdit.current = true
    nodeNoteRef.current = v   // ref update — zero re-renders
    scheduleSave()
  }

  function scheduleSave() {
    clearTimeout(autoSaveTimer.current)
    autoSaveTimer.current = setTimeout(async () => {
      const nodeId = selectedNodeIdRef.current
      if (!nodeId || !isUserEdit.current) return
      setSaveStatus('saving')
      try {
        const title = nodeTitle  // captured at schedule time — fine for debounce
        const note  = nodeNoteRef.current
        await noteNodeApi.saveContent(id, nodeId, { title, note })
        mergeNodeIntoGraph(nodeId, { title, note })
        await loadGraph()
        setSaveStatus('saved')
        setTimeout(() => setSaveStatus('idle'), 2000)
      } catch (err) {
        setError(err.message)
        setSaveStatus('idle')
      }
    }, 1500)
  }


  // ── Data fetching ──────────────────────────────────────────────────────────
  async function loadGraph() {
    try {
      const data = await noteGraphApi.get(id)
      setGraph(prev => {
        if (!prev?.nodes) return data
        // Re-merge full node data (note, metadata) that was already fetched —
        // the skeleton endpoint omits these fields so we must preserve them
        const mergedNodes = {}
        for (const [nodeId, skeletonNode] of Object.entries(data.nodes || {})) {
          const cached = prev.nodes[nodeId]
          mergedNodes[nodeId] = {
            ...skeletonNode,
            ...(cached?.note      !== undefined && { note:     cached.note }),
            ...(cached?.metadata  !== undefined && { metadata: cached.metadata }),
          }
        }
        return { ...data, nodes: mergedNodes }
      })
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

  // Reset form fields when switching to a different node
  // Note: nodeNote is set by the full-node fetch below, not here
  useEffect(() => {
    if (!graph || !selectedNodeId || selectedNodeId === DRAFT_ID) return
    const node = graph.nodes?.[selectedNodeId]
    if (!node) return
    setNodeTitle(node.title || '')
    nodeNoteRef.current = ''
    setAddTagId('')
    setConnTarget('')
    setConnRelType('')
    setConnInverseTarget('')
    setConnInverseRelType('')
  }, [selectedNodeId]) // eslint-disable-line react-hooks/exhaustive-deps

  // Fetch full node content on selection (skeleton omits note + metadata)
  useEffect(() => {
    if (!selectedNodeId || selectedNodeId === DRAFT_ID) return
    setNodeLoading(true)
    noteNodeApi.get(id, selectedNodeId)
      .then(fullNode => {
        // Merge note + metadata into graph so NodeEditor and MetadataSidebar stay correct
        setGraph(prev => {
          if (!prev) return prev
          return {
            ...prev,
            nodes: {
              ...prev.nodes,
              [selectedNodeId]: {
                ...prev.nodes?.[selectedNodeId],
                note:     fullNode.note,
                metadata: fullNode.metadata,
                title:    fullNode.title,
              },
            },
          }
        })
        setNodeTitle(fullNode.title || '')
        nodeNoteRef.current = fullNode.note || ''
        setNoteLoadVersion(v => v + 1)   // signal NodeEditor to re-sync contenteditable
      })
      .catch(err => setError(err.message))
      .finally(() => setNodeLoading(false))
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
  const navigateTo = useCallback((nodeId) => {
    if (!nodeId || nodeId === selectedNodeId) return
    if (selectedNodeId === DRAFT_ID) clearTimeout(autoSaveTimer.current)
    const base = nodeHistory.slice(0, historyIdx + 1).filter(nid => nid !== DRAFT_ID)
    const newHistory = [...base, nodeId]
    setNodeHistory(newHistory)
    setHistoryIdx(newHistory.length - 1)
    setSelectedNodeId(nodeId)
  }, [selectedNodeId, nodeHistory, historyIdx])

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

  const nodesList = useMemo(() => {
    const list = dictToArray(graph?.nodes)
    if (selectedNodeId === DRAFT_ID)
      return [{ id: DRAFT_ID, title: '', tags: [], relationships: [] }, ...list]
    return list
  }, [graph?.nodes, selectedNodeId])
  const tagsList = useMemo(() => dictToArray(graph?.tags), [graph?.tags])
  const relTypesList = useMemo(() => dictToArray(graph?.relationships), [graph?.relationships])

  if (!graph) return <div style={{ padding: 16 }}>{error || 'Loading...'}</div>

  // ── NoteNode CRUD ──────────────────────────────────────────────────────────
  function handleCreateNode() {
    if (selectedNodeId === DRAFT_ID) return  // already drafting
    clearTimeout(autoSaveTimer.current)
    isUserEdit.current = false
    setNodeTitle('')
    nodeNoteRef.current = ''
    const newHistory = [...nodeHistory.slice(0, historyIdx + 1), DRAFT_ID]
    setNodeHistory(newHistory)
    setHistoryIdx(newHistory.length - 1)
    setSelectedNodeId(DRAFT_ID)
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

  function mergeNodeIntoGraph(nodeId, fields) {
    setGraph(prev => {
      if (!prev) return prev
      return {
        ...prev,
        nodes: {
          ...prev.nodes,
          [nodeId]: { ...prev.nodes?.[nodeId], ...fields },
        },
      }
    })
  }

  async function handleSaveNode() {
    if (!selectedNodeId) return
    clearTimeout(autoSaveTimer.current)
    setSaveStatus('saving')
    try {
      await noteNodeApi.saveContent(id, selectedNodeId, { title: nodeTitle, note: nodeNoteRef.current })
      mergeNodeIntoGraph(selectedNodeId, { title: nodeTitle, note: nodeNoteRef.current })
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
      const result = await noteNodeApi.updateMetadata(id, selectedNodeId, data)
      mergeNodeIntoGraph(selectedNodeId, { metadata: result.metadata })
      await loadGraph()
    } catch (err) { setError(err.message) }
  }

  async function handleGenerateLlmMetadata() {
    if (!selectedNodeId) return
    try {
      const result = await noteNodeApi.generateLlmMetadata(id, selectedNodeId)
      mergeNodeIntoGraph(selectedNodeId, { metadata: result.metadata })
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
          showChat={showChat}
          onToggleChat={() => setShowChat(v => !v)}
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
            setNodeNote={handleNoteChange}
            noteLoadVersion={noteLoadVersion}
            nodeLoading={nodeLoading}
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
