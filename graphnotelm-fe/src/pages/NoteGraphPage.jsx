import { useState, useEffect, useRef, useMemo, useCallback } from 'react'
import { useParams, useNavigate, useLocation } from 'react-router-dom'
import { noteGraphApi, noteNodeApi, tagApi, relationshipApi, folderApi, reviewApi } from '../api'
import { NEW_PER_SESSION, displayConfidence } from '../utils/review'
import GraphView, { StudyPathIcon } from '../components/GraphView'
import NodeSidebar from '../components/NodeSidebar'
import NodeEditor from '../components/NodeEditor'
import MetadataSidebar from '../components/MetadataSidebar'
import ChatSideBar from '../components/ChatSideBar'
import StudyPathPanel from '../components/StudyPathPanel'
import ReviewSession from '../components/ReviewSession'
import FlashcardsView from '../components/FlashcardsView'
import NodeSearchModal from '../components/NodeSearchModal'
import ManageTypesOverlay from '../components/ManageTypesOverlay'
import NodeTableView from '../components/NodeTableView'
import Modal from '../components/Modal'
import './NoteGraphPage.css'

const DRAFT_ID = '__draft__'

// Used by the Extract Note button and its entry in the header's ⋯ menu.
function ExtractIcon() {
  return (
    <svg viewBox="0 0 14 14" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" style={{ width: 13, height: 13, flexShrink: 0 }}>
      <path d="M7 1l1.5 3 3.5.5-2.5 2.5.5 3.5L7 9l-3 1.5.5-3.5L2 4.5 5.5 4z"/>
    </svg>
  )
}

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
  const errorTimerRef = useRef(null)

  function showError(msg) {
    clearTimeout(errorTimerRef.current)
    setError(msg)
    errorTimerRef.current = setTimeout(() => setError(''), 5000)
  }

  // ── UI state ───────────────────────────────────────────────────────────────
  const [search, setSearch] = useState('')
  const [tagFilter, setTagFilter] = useState(null)
  const [showGraph, setShowGraph] = useState(false)
  const [showManageTypes, setShowManageTypes] = useState(false)
  const [showMetadataSidebar, setShowMetadataSidebar] = useState(false)
  const [showChat, setShowChat] = useState(false)
  const [showStudyPath, setShowStudyPath] = useState(false)
  // Which screen fills the main area: the note editor, or one of the full-width views.
  const [mainView, setMainView] = useState('editor') // 'editor' | 'table' | 'flashcards' | 'review'
  // The open review: `key` remounts the screen per session, `returnTo` is where leaving it goes.
  const [reviewSession, setReviewSession] = useState(null) // null | { single: { id, title } | null, key, returnTo }
  const [reviewSummary, setReviewSummary] = useState(null) // { dueCount, newCount }
  const [studyHighlight, setStudyHighlight] = useState(null)
  const [showNodeSearch, setShowNodeSearch] = useState(false)
  const graphViewRef = useRef(null)

  // The header's ⋯ menu holds the less-used actions once the window is too narrow for them.
  const [showHeaderMenu, setShowHeaderMenu] = useState(false)
  const headerMenuRef = useRef(null)
  useEffect(() => {
    if (!showHeaderMenu) return
    function onDown(e) {
      if (headerMenuRef.current && !headerMenuRef.current.contains(e.target)) setShowHeaderMenu(false)
    }
    function onKey(e) { if (e.key === 'Escape') setShowHeaderMenu(false) }
    window.addEventListener('mousedown', onDown)
    window.addEventListener('keydown', onKey)
    return () => {
      window.removeEventListener('mousedown', onDown)
      window.removeEventListener('keydown', onKey)
    }
  }, [showHeaderMenu])

  // ── Pinned nodes ───────────────────────────────────────────────────────────
  const [pinnedNodeIds, setPinnedNodeIds] = useState(new Set())
  const [modal, setModal] = useState(null)
  const [showExtract, setShowExtract] = useState(false)
  const [extractContent, setExtractContent] = useState('')
  const [extractLoading, setExtractLoading] = useState(false)

  // ── Node selection + history ───────────────────────────────────────────────
  const [selectedNodeId, setSelectedNodeId] = useState(null)
  const [nodeHistory, setNodeHistory] = useState([])
  const [historyIdx, setHistoryIdx] = useState(-1)

  // ── Node editor form ───────────────────────────────────────────────────────
  const [nodeTitle, setNodeTitle] = useState('')
  const nodeTitleRef = useRef('')        // ref — always has the latest title for saves
  const nodeNoteRef = useRef('')         // ref — no re-render on every keystroke
  const [localTags, setLocalTags] = useState([])
  const [localRelDict, setLocalRelDict] = useState({})
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

  // ── Draft incoming connections (stored locally before node is created) ─────
  const [draftIncoming, setDraftIncoming] = useState([])
  const draftFolderIdRef = useRef(null)
  const [draftFolderId, setDraftFolderId] = useState(null)

  // Keep selectedNodeId accessible in async callbacks without stale closure
  const selectedNodeIdRef = useRef(selectedNodeId)
  useEffect(() => { selectedNodeIdRef.current = selectedNodeId }, [selectedNodeId])

  function handleTitleChange(v) {
    nodeTitleRef.current = v
    setNodeTitle(v)
    if (selectedNodeId !== DRAFT_ID) {
      isUserEdit.current = true
      scheduleSave()
    }
  }

  // Creates the draft node from the current title/note/tags/relationships.
  // Returns the new node's id, or null if there was nothing to save / creation failed.
  async function confirmDraftNode() {
    const title = nodeTitleRef.current.trim()
    if (!title) return null
    try {
      const created = await noteNodeApi.create(id, { title, note: nodeNoteRef.current })
      if (!created?.id) return null
      const newId = created.id
      for (const tagId of localTags)
        await noteNodeApi.addTag(id, newId, tagId)
      for (const [targetNodeId, relationshipId] of Object.entries(localRelDict))
        await noteNodeApi.addRelationship(id, newId, targetNodeId, relationshipId)
      for (const { sourceNodeId, relTypeId } of draftIncoming)
        await noteNodeApi.addRelationship(id, sourceNodeId, newId, relTypeId)
      if (draftFolderIdRef.current)
        await folderApi.moveNode(id, newId, draftFolderIdRef.current)
      draftFolderIdRef.current = null
      setDraftFolderId(null)
      await loadGraph()
      setDraftIncoming([])
      return newId
    } catch (err) {
      showError(err.message)
      return null
    }
  }

  async function handleConfirmDraft() {
    const newId = await confirmDraftNode()
    if (newId) {
      setNodeHistory(prev => prev.map(nid => nid === DRAFT_ID ? newId : nid))
      setSelectedNodeId(newId)
    }
  }

  // Whether the current draft has unsaved title/note content worth prompting about.
  function hasDraftContent() {
    if (selectedNodeId !== DRAFT_ID) return false
    const noteText = (nodeNoteRef.current || '').replace(/<[^>]*>/g, '').trim()
    return !!(nodeTitleRef.current.trim() || noteText)
  }

  function handleRemoveDraftIncoming(sourceNodeId) {
    setDraftIncoming(prev => prev.filter(r => r.sourceNodeId !== sourceNodeId))
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
      isUserEdit.current = false
      setSaveStatus('saving')
      try {
        const title = nodeTitleRef.current
        const note  = nodeNoteRef.current
        await noteNodeApi.saveContent(id, nodeId, { title, note })
        mergeNodeIntoGraph(nodeId, { title, note })
        setSaveStatus('saved')
        setTimeout(() => setSaveStatus('idle'), 2000)
      } catch (err) {
        isUserEdit.current = true
        showError(err.message)
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
      showError(err.message)
    }
  }

  // The header's review count; refreshed after sessions and card edits.
  function loadReviewSummary() {
    reviewApi.summary(id, NEW_PER_SESSION).then(setReviewSummary).catch(() => {})
  }

  useEffect(() => {
    loadGraph()
    loadReviewSummary()
    noteNodeApi.getPinned(id)
      .then(nodes => setPinnedNodeIds(new Set(nodes.map(n => n.id))))
      .catch(() => {})
  }, [id]) // eslint-disable-line react-hooks/exhaustive-deps

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
    nodeTitleRef.current = node.title || ''
    nodeNoteRef.current = ''
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
                confidence: fullNode.confidence,
              },
            },
          }
        })
        setNodeTitle(fullNode.title || '')
        nodeTitleRef.current = fullNode.title || ''
        nodeNoteRef.current = fullNode.note || ''
        setNoteLoadVersion(v => v + 1)   // signal NodeEditor to re-sync contenteditable
      })
      .catch(err => showError(err.message))
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

  // Flush any pending save before switching nodes, then reset state
  useEffect(() => {
    return () => {
      if (isUserEdit.current) {
        const nodeId = selectedNodeIdRef.current
        if (nodeId && nodeId !== DRAFT_ID) {
          clearTimeout(autoSaveTimer.current)
          noteNodeApi.saveContent(id, nodeId, {
            title: nodeTitleRef.current,
            note: nodeNoteRef.current,
          }).catch(() => {})
          isUserEdit.current = false
        }
      }
    }
  }, [selectedNodeId]) // eslint-disable-line

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

    function proceed(resolvedDraftId) {
      if (selectedNodeId === DRAFT_ID) clearTimeout(autoSaveTimer.current)
      let base = nodeHistory.slice(0, historyIdx + 1)
      if (selectedNodeId === DRAFT_ID) {
        base = resolvedDraftId
          ? base.map(nid => nid === DRAFT_ID ? resolvedDraftId : nid)
          : base.filter(nid => nid !== DRAFT_ID)
      }
      const newHistory = [...base, nodeId]
      setNodeHistory(newHistory)
      setHistoryIdx(newHistory.length - 1)
      setSelectedNodeId(nodeId)
    }

    if (selectedNodeId === DRAFT_ID && hasDraftContent()) {
      setModal({
        type: 'confirm',
        title: 'Save new node?',
        message: 'Would you like to save this node first?',
        confirmLabel: 'Yes',
        declineLabel: 'No',
        onConfirm: async () => proceed(await confirmDraftNode()),
        onDecline: () => proceed(null),
      })
      return
    }

    proceed(null)
  }, [selectedNodeId, nodeHistory, historyIdx]) // eslint-disable-line react-hooks/exhaustive-deps

  // The assistant and the study path panel share the right edge, so opening one closes the other.
  function toggleChat() {
    setShowStudyPath(false)
    setShowChat(v => !v)
  }

  // Opens the review screen — from the split editor in graph view too, so leave the graph.
  function startReview(single = null) {
    setShowGraph(false)
    setReviewSession({ single, key: Date.now(), returnTo: mainView === 'review' ? 'editor' : mainView })
    setMainView('review')
  }

  function closeReview() {
    setMainView(reviewSession?.returnTo ?? 'editor')
    setReviewSession(null)
    loadReviewSummary()
  }

  // Header toggles: open a full-width view, or go back to the editor if it's already showing.
  function toggleMainView(view) {
    if (mainView === 'review') {
      setReviewSession(null)
      loadReviewSummary()
    }
    setMainView(v => v === view ? 'editor' : view)
  }

  function openNoteFromList(nodeId) {
    setMainView('editor')
    navigateTo(nodeId)
  }

  // Keep the graph, editor and study path in step with each answer as it's given.
  const handleReviewed = useCallback((nodeId, result) => {
    setGraph(prev => {
      const node = prev?.nodes?.[nodeId]
      if (!node) return prev
      return {
        ...prev,
        nodes: {
          ...prev.nodes,
          [nodeId]: { ...node, confidence: result.confidence, metadata: { ...node.metadata, memory: result.memory } },
        },
      }
    })
  }, [])

  function toggleStudyPath() {
    setShowChat(false)
    setShowStudyPath(v => !v)
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

  const nodesList = useMemo(() => {
    const list = dictToArray(graph?.nodes)
    if (selectedNodeId === DRAFT_ID)
      return [{ id: DRAFT_ID, title: '', tags: [], relationships: [] }, ...list]
    return list
  }, [graph?.nodes, selectedNodeId])
  const tagsList = useMemo(() => dictToArray(graph?.tags), [graph?.tags])
  const relTypesList = useMemo(() => dictToArray(graph?.relationships), [graph?.relationships])
  const foldersList = useMemo(() => dictToArray(graph?.folders), [graph?.folders])

  if (!graph) return <div style={{ padding: 16 }}>{error || 'Loading...'}</div>

  // ── NoteNode CRUD ──────────────────────────────────────────────────────────
  function handleCreateNode() {
    if (selectedNodeId === DRAFT_ID) return  // already drafting
    draftFolderIdRef.current = null
    setDraftFolderId(null)
    clearTimeout(autoSaveTimer.current)
    isUserEdit.current = false
    setNodeTitle('')
    nodeTitleRef.current = ''
    nodeNoteRef.current = ''
    setNoteLoadVersion(v => v + 1)
    setLocalTags([])
    setLocalRelDict({})
    setDraftIncoming([])
    const newHistory = [...nodeHistory.slice(0, historyIdx + 1), DRAFT_ID]
    setNodeHistory(newHistory)
    setHistoryIdx(newHistory.length - 1)
    setSelectedNodeId(DRAFT_ID)
  }

  async function handleExtractNote() {
    if (!extractContent.trim()) return
    setExtractLoading(true)
    try {
      const created = await noteNodeApi.paste(id, extractContent)
      await loadGraph()
      setShowExtract(false)
      setExtractContent('')
      if (created?.id) navigateTo(created.id)
    } catch (err) {
      showError(err.message)
    } finally {
      setExtractLoading(false)
    }
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
    } catch (err) { showError(err.message) }
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

  // Functional variant — updater sees the freshest node, avoiding races with
  // other in-flight merges (single-node mutations no longer refetch the graph).
  function updateNodeInGraph(nodeId, updater) {
    setGraph(prev => {
      const node = prev?.nodes?.[nodeId]
      if (!node) return prev
      return {
        ...prev,
        nodes: { ...prev.nodes, [nodeId]: { ...node, ...updater(node) } },
      }
    })
  }

  // Local mirror of the server-side delete: drop the node and strip any
  // relationships that pointed at it.
  function removeNodeFromGraph(nodeId) {
    setGraph(prev => {
      if (!prev) return prev
      const newNodes = {}
      for (const [nid, node] of Object.entries(prev.nodes || {})) {
        if (nid === nodeId) continue
        newNodes[nid] = (node.relationships || []).some(r => r.targetNodeId === nodeId)
          ? { ...node, relationships: node.relationships.filter(r => r.targetNodeId !== nodeId) }
          : node
      }
      return { ...prev, nodes: newNodes }
    })
  }

  async function handleSaveNode() {
    if (!selectedNodeId) return
    clearTimeout(autoSaveTimer.current)
    setSaveStatus('saving')
    try {
      await noteNodeApi.saveContent(id, selectedNodeId, { title: nodeTitleRef.current, note: nodeNoteRef.current })
      mergeNodeIntoGraph(selectedNodeId, { title: nodeTitleRef.current, note: nodeNoteRef.current })
      setSaveStatus('saved')
      setTimeout(() => setSaveStatus('idle'), 2000)
    } catch (err) {
      showError(err.message)
      setSaveStatus('idle')
    }
  }

  async function handleSaveMetadata(data) {
    if (!selectedNodeId) return
    try {
      const result = await noteNodeApi.updateMetadata(id, selectedNodeId, data)
      // A new self-rating changes confidence for notes that haven't been reviewed yet.
      mergeNodeIntoGraph(selectedNodeId, { metadata: result.metadata, confidence: result.confidence })
    } catch (err) { showError(err.message) }
  }

  async function handleGenerateLlmMetadata() {
    if (!selectedNodeId) return
    try {
      const result = await noteNodeApi.generateLlmMetadata(id, selectedNodeId)
      mergeNodeIntoGraph(selectedNodeId, { metadata: result.metadata })
    } catch (err) { showError(err.message) }
  }

  function handleDeleteNode() {
    if (!selectedNodeId) return
    const node = graph.nodes?.[selectedNodeId]
    setModal({
      type: 'confirm',
      title: 'Delete note',
      message: `Delete "${node?.title}"? This cannot be undone.`,
      confirmLabel: 'Delete',
      danger: true,
      onConfirm: async () => {
        try {
          await noteNodeApi.delete(id, selectedNodeId)
          setSelectedNodeId(null)
          setNodeHistory([])
          setHistoryIdx(-1)
          removeNodeFromGraph(selectedNodeId)
        } catch (err) { showError(err.message) }
      },
    })
  }

  async function handleTogglePin(nodeId) {
    const isPinned = pinnedNodeIds.has(nodeId)
    try {
      await noteNodeApi.setPin(id, nodeId, !isPinned)
      setPinnedNodeIds(prev => {
        const next = new Set(prev)
        if (isPinned) next.delete(nodeId)
        else next.add(nodeId)
        return next
      })
    } catch (err) { showError(err.message) }
  }

  // ── Bulk node operations (table view) ──────────────────────────────────────
  async function handleAddTagToNodes(tagId, nodeIds) {
    try {
      await noteNodeApi.addTagToNodes(id, tagId, nodeIds)
      await loadGraph()
    } catch (err) { showError(err.message) }
  }

  async function handleRemoveTagFromNodes(tagId, nodeIds) {
    try {
      await noteNodeApi.removeTagFromNodes(id, tagId, nodeIds)
      await loadGraph()
    } catch (err) { showError(err.message) }
  }

  async function handleSetPinnedMany(nodeIds, isPinned) {
    try {
      const result = isPinned
        ? await noteNodeApi.setPinnedMany(id, nodeIds)
        : await noteNodeApi.setUnpinnedMany(id, nodeIds)
      const updatedIds = result?.updatedNodeIds ?? nodeIds
      setPinnedNodeIds(prev => {
        const next = new Set(prev)
        for (const nodeId of updatedIds) {
          if (isPinned) next.add(nodeId)
          else next.delete(nodeId)
        }
        return next
      })
    } catch (err) { showError(err.message) }
  }

  // ── Tag assignment ─────────────────────────────────────────────────────────
  async function handleAddTag(tagId) {
    if (!tagId || localTags.includes(tagId)) return
    if (selectedNodeId === DRAFT_ID) {
      setLocalTags(prev => [...prev, tagId])
      return
    }
    try {
      await noteNodeApi.addTag(id, selectedNodeId, tagId)
      updateNodeInGraph(selectedNodeId, node => ({ tags: [...(node.tags || []), tagId] }))
    } catch (err) { showError(err.message) }
  }

  async function handleRemoveTag(tagId) {
    if (selectedNodeId === DRAFT_ID) {
      setLocalTags(prev => prev.filter(t => t !== tagId))
      return
    }
    try {
      await noteNodeApi.removeTag(id, selectedNodeId, tagId)
      updateNodeInGraph(selectedNodeId, node => ({ tags: (node.tags || []).filter(t => t !== tagId) }))
    } catch (err) { showError(err.message) }
  }

  // ── Relationship assignment ────────────────────────────────────────────────
  async function handleAddConnection() {
    if (!connTarget || !connRelType || connTarget === selectedNodeId) return
    if (selectedNodeId === DRAFT_ID) {
      setLocalRelDict(prev => ({ ...prev, [connTarget]: connRelType }))
      setConnTarget('')
      setConnRelType('')
      return
    }
    try {
      const targetNodeId = connTarget, relationshipId = connRelType
      await noteNodeApi.addRelationship(id, selectedNodeId, targetNodeId, relationshipId)
      setConnTarget('')
      setConnRelType('')
      updateNodeInGraph(selectedNodeId, node => ({
        relationships: [...(node.relationships || []), { targetNodeId, relationshipId }],
      }))
    } catch (err) { showError(err.message) }
  }

  async function handleAddInverseConnection() {
    if (!connInverseTarget || !connInverseRelType || connInverseTarget === selectedNodeId) return
    if (selectedNodeId === DRAFT_ID) {
      setDraftIncoming(prev => [...prev, { sourceNodeId: connInverseTarget, relTypeId: connInverseRelType }])
      setConnInverseTarget('')
      setConnInverseRelType('')
      return
    }
    try {
      const sourceNodeId = connInverseTarget, relationshipId = connInverseRelType
      await noteNodeApi.addRelationship(id, sourceNodeId, selectedNodeId, relationshipId)
      setConnInverseTarget('')
      setConnInverseRelType('')
      updateNodeInGraph(sourceNodeId, node => ({
        relationships: [...(node.relationships || []), { targetNodeId: selectedNodeId, relationshipId }],
      }))
    } catch (err) { showError(err.message) }
  }

  async function handleRemoveConnection(targetNodeId) {
    if (selectedNodeId === DRAFT_ID) {
      setLocalRelDict(prev => {
        const next = { ...prev }
        delete next[targetNodeId]
        return next
      })
      return
    }
    const relationshipId = localRelDict[targetNodeId]
    if (!relationshipId) return
    try {
      await noteNodeApi.removeRelationship(id, selectedNodeId, targetNodeId, relationshipId)
      updateNodeInGraph(selectedNodeId, node => ({
        relationships: (node.relationships || []).filter(r => r.targetNodeId !== targetNodeId),
      }))
    } catch (err) { showError(err.message) }
  }

  // ── Folder CRUD + node assignment ─────────────────────────────────────────
  function handleCreateFolder() {
    setModal({
      type: 'input',
      title: 'New folder',
      confirmLabel: 'Create',
      fields: [
        { key: 'folderName', label: 'Name', placeholder: 'Folder name...' },
        { key: 'folderColor', label: 'Color', fieldType: 'color', defaultValue: '#888888', required: false },
      ],
      onConfirm: async ({ folderName, folderColor }) => {
        try {
          const result = await folderApi.create(id, { folderName, folderColor })
          setGraph(prev => ({
            ...prev,
            folders: { ...prev.folders, [result.id]: { name: folderName, color: folderColor } },
          }))
        } catch (err) { showError(err.message) }
      },
    })
  }

  function handleEditFolder(folderId) {
    const existing = graph.folders?.[folderId]
    setModal({
      type: 'input',
      title: 'Edit folder',
      confirmLabel: 'Save',
      fields: [
        { key: 'folderName', label: 'Name', placeholder: 'Folder name...', defaultValue: existing?.name || '' },
        { key: 'folderColor', label: 'Color', fieldType: 'color', defaultValue: existing?.color || '#888888', required: false },
      ],
      onConfirm: async ({ folderName, folderColor }) => {
        try {
          await folderApi.edit(id, folderId, { id: folderId, folderName, folderColor })
          setGraph(prev => ({
            ...prev,
            folders: { ...prev.folders, [folderId]: { name: folderName, color: folderColor } },
          }))
        } catch (err) { showError(err.message) }
      },
    })
  }

  function handleDeleteFolder(folderId) {
    const folder = graph.folders?.[folderId]
    const nodeCount = Object.values(graph.nodes || {}).filter(n => n.folderId === folderId).length
    setModal({
      type: 'confirm',
      title: 'Delete folder',
      message: `Delete "${folder?.name}"? ${nodeCount > 0 ? `${nodeCount} note${nodeCount === 1 ? '' : 's'} will be moved to Unfiled.` : 'The folder is empty.'}`,
      confirmLabel: 'Delete',
      danger: true,
      onConfirm: async () => {
        try {
          await folderApi.delete(id, folderId)
          setGraph(prev => {
            const newFolders = { ...prev.folders }
            delete newFolders[folderId]
            const newNodes = {}
            for (const [nid, node] of Object.entries(prev.nodes || {})) {
              newNodes[nid] = node.folderId === folderId ? { ...node, folderId: null } : node
            }
            return { ...prev, folders: newFolders, nodes: newNodes }
          })
        } catch (err) { showError(err.message) }
      },
    })
  }

  async function handleMoveNodeToFolder(nodeId, folderId) {
    try {
      await folderApi.moveNode(id, nodeId, folderId)
      setGraph(prev => ({
        ...prev,
        nodes: { ...prev.nodes, [nodeId]: { ...prev.nodes?.[nodeId], folderId } },
      }))
    } catch (err) { showError(err.message) }
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
        } catch (err) { showError(err.message) }
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
        } catch (err) { showError(err.message) }
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
        } catch (err) { showError(err.message) }
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
        } catch (err) { showError(err.message) }
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
        } catch (err) { showError(err.message) }
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
        } catch (err) { showError(err.message) }
      },
    })
  }

  // ── Render ─────────────────────────────────────────────────────────────────
  return (
    <div className="page">
      <Modal modal={modal} onCancel={() => setModal(null)} />

      {showExtract && (
        <div className="extract-backdrop" onClick={e => { if (e.target === e.currentTarget && !extractLoading) { setShowExtract(false); setExtractContent('') } }}>
          <div className="extract-modal">
            <div className="extract-header">
              <span>Extract Note</span>
              <button className="btn-ghost btn-icon-sm" onClick={() => { setShowExtract(false); setExtractContent('') }} disabled={extractLoading}>
                <span className="app-icon icon-close" />
              </button>
            </div>
            <p className="extract-hint">Paste any content below — the AI will structure it into a new note node.</p>
            <textarea
              className="extract-textarea"
              value={extractContent}
              onChange={e => setExtractContent(e.target.value)}
              placeholder="Paste your content here..."
              disabled={extractLoading}
              autoFocus
            />
            <div className="extract-footer">
              <button className="btn-ghost" onClick={() => { setShowExtract(false); setExtractContent('') }} disabled={extractLoading}>Cancel</button>
              <button
                className="extract-submit"
                onClick={handleExtractNote}
                disabled={!extractContent.trim() || extractLoading}
              >
                {extractLoading
                  ? <><span className="save-spinner" style={{ width: 13, height: 13 }} /> Extracting…</>
                  : '✦ Extract'
                }
              </button>
            </div>
          </div>
        </div>
      )}

      {showGraph && (
        <GraphView
          ref={graphViewRef}
          nodes={nodesList}
          tagDefs={graph.tags || {}}
          relDefs={graph.relationships || {}}
          selectedNodeId={selectedNodeId}
          onClose={() => setShowGraph(false)}
          onSelectNode={node => { navigateTo(node.id); setShowGraph(false) }}
          onHighlightNode={node => navigateTo(node.id)}
          showChat={showChat}
          onToggleChat={toggleChat}
          showStudyPath={showStudyPath}
          onToggleStudyPath={toggleStudyPath}
          studyHighlight={showStudyPath ? studyHighlight : null}
          tagsList={tagsList}
          relTypesList={relTypesList}
          onEditAddRelationship={async (srcId, tgtId, relId) => {
            try {
              await noteNodeApi.addRelationship(id, srcId, tgtId, relId)
              updateNodeInGraph(srcId, node => ({
                relationships: [...(node.relationships || []), { targetNodeId: tgtId, relationshipId: relId }],
              }))
            }
            catch (err) { showError(err.message) }
          }}
          onEditAddTag={async (nodeId, tagId) => {
            try {
              await noteNodeApi.addTag(id, nodeId, tagId)
              updateNodeInGraph(nodeId, node => ({ tags: [...(node.tags || []), tagId] }))
            }
            catch (err) { showError(err.message) }
          }}
          onEditQuickEdit={async (nodeId, { title, confidenceRate }) => {
            try {
              const node = graph.nodes?.[nodeId]
              await noteNodeApi.saveContent(id, nodeId, { title, note: node?.note || '' })
              const updated = confidenceRate != null
                ? await noteNodeApi.updateMetadata(id, nodeId, {
                    userConfidenceRate: confidenceRate,
                    llmMetadata: node?.metadata?.llmMetadata ?? null,
                  })
                : null
              mergeNodeIntoGraph(nodeId, {
                title,
                ...(updated && {
                  metadata: { ...node?.metadata, userConfidenceRate: confidenceRate },
                  confidence: updated.confidence,
                }),
              })
            } catch (err) { showError(err.message) }
          }}
          onEditDeleteNode={(nodeId) => {
            const node = graph.nodes?.[nodeId]
            setModal({
              type: 'confirm',
              title: 'Delete note',
              message: `Delete "${node?.title}"? This cannot be undone.`,
              confirmLabel: 'Delete',
              danger: true,
              onConfirm: async () => {
                try {
                  await noteNodeApi.delete(id, nodeId)
                  if (selectedNodeId === nodeId) {
                    setSelectedNodeId(null)
                    setNodeHistory([])
                    setHistoryIdx(-1)
                  }
                  removeNodeFromGraph(nodeId)
                } catch (err) { showError(err.message) }
              },
            })
          }}
          onEditDeleteEdge={async (srcId, tgtId, relId) => {
            try {
              await noteNodeApi.removeRelationship(id, srcId, tgtId, relId)
              updateNodeInGraph(srcId, node => ({
                relationships: (node.relationships || []).filter(r => !(r.targetNodeId === tgtId && r.relationshipId === relId)),
              }))
            }
            catch (err) { showError(err.message) }
          }}
          onEditChangeRelType={async (srcId, tgtId, oldRelId, newRelId) => {
            try {
              await noteNodeApi.removeRelationship(id, srcId, tgtId, oldRelId)
              await noteNodeApi.addRelationship(id, srcId, tgtId, newRelId)
              updateNodeInGraph(srcId, node => ({
                relationships: (node.relationships || []).map(r =>
                  (r.targetNodeId === tgtId && r.relationshipId === oldRelId)
                    ? { ...r, relationshipId: newRelId }
                    : r),
              }))
            } catch (err) { showError(err.message) }
          }}
          onCreateNote={async (title, graphX, graphY) => {
            try {
              const created = await noteNodeApi.create(id, { title, note: '' })
              if (created?.id) {
                if (graphX != null && graphY != null)
                  graphViewRef.current?.seedPosition(created.id, graphX, graphY)
                await loadGraph()
                navigateTo(created.id)
              } else {
                await loadGraph()
              }
            } catch (err) { showError(err.message) }
          }}
          pinnedNodeIds={pinnedNodeIds}
          onSelectNodeSplit={node => navigateTo(node.id)}
          renderSplitPanel={() => (
            selectedNodeId ? (
              <NodeEditor
                graph={graph}
                graphId={id}
                onError={showError}
                selectedNodeId={selectedNodeId}
                nodesList={nodesList}
                tagsList={tagsList}
                relTypesList={relTypesList}
                nodeTitle={nodeTitle} setNodeTitle={handleTitleChange}
                setNodeNote={handleNoteChange}
                noteLoadVersion={noteLoadVersion}
                nodeLoading={nodeLoading}
                confidenceRate={selectedNodeId ? displayConfidence(graph.nodes?.[selectedNodeId]) : null}
                selfRating={selectedNodeId ? graph.nodes?.[selectedNodeId]?.metadata?.userConfidenceRate ?? null : null}
                memory={selectedNodeId ? graph.nodes?.[selectedNodeId]?.metadata?.memory ?? null : null}
                onReviewNote={() => startReview({ id: selectedNodeId, title: graph.nodes?.[selectedNodeId]?.title || nodeTitle })}
                onCardsChanged={loadReviewSummary}
                saveStatus={saveStatus}
                onSave={handleSaveNode}
                onDelete={handleDeleteNode}
                onToggleMetadata={() => setShowMetadataSidebar(v => !v)}
                onConfirmDraft={handleConfirmDraft}
                draftFolderId={draftFolderId}
                onSetDraftFolder={fid => { draftFolderIdRef.current = fid; setDraftFolderId(fid) }}
                foldersList={foldersList}
                draftIncoming={draftIncoming}
                onRemoveDraftIncoming={handleRemoveDraftIncoming}
                localTags={localTags}
                localRelDict={localRelDict}
                connTarget={connTarget} setConnTarget={setConnTarget}
                connRelType={connRelType} setConnRelType={setConnRelType}
                connInverseTarget={connInverseTarget} setConnInverseTarget={setConnInverseTarget}
                connInverseRelType={connInverseRelType} setConnInverseRelType={setConnInverseRelType}
                onAddTag={handleAddTag}
                onRemoveTag={handleRemoveTag}
                onAddConnection={handleAddConnection}
                onAddInverseConnection={handleAddInverseConnection}
                onRemoveConnection={handleRemoveConnection}
                onNavigate={navigateTo}
                isPinned={selectedNodeId !== '__draft__' && pinnedNodeIds.has(selectedNodeId)}
                onTogglePin={() => handleTogglePin(selectedNodeId)}
              />
            ) : (
              <p className="graph-split-empty">Click a node to edit it</p>
            )
          )}
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

      <header className="graph-page-header">
        <div className="header-actions header-left">
          <button className="btn-ghost header-back" onClick={() => navigate('/')} title="Back to notegraphs">
            ←<span className="btn-label"> Back</span>
          </button>
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
          <h2 className="header-title" title={graphName || 'Notegraph'}>
            {graphName || 'Notegraph'}
          </h2>
        </div>
        {/* Labels collapse as the window narrows (see .graph-page-header in the CSS): the
            header-secondary actions move into the ⋯ menu, then only icons remain. */}
        <div className="header-actions header-right">
          <button className="btn-icon-header" onClick={() => setShowNodeSearch(true)} title="Search note content">
            <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" style={{ width: 13, height: 13, flexShrink: 0 }}>
              <circle cx="6.5" cy="6.5" r="4.5" /><line x1="10.5" y1="10.5" x2="14" y2="14" />
            </svg>
            <span className="btn-label">Search<span className="btn-label-extra"> Note Content</span></span>
          </button>
          <button className="btn-icon-header header-secondary" onClick={() => setShowExtract(true)} title="Extract note">
            <ExtractIcon />
            <span className="btn-label">Extract Note</span>
          </button>
          <button className="btn-icon-header header-secondary" onClick={handleExport} title="Export JSON">
            <span className="app-icon icon-export" /><span className="btn-label">Export JSON</span>
          </button>
          <button
            className={`btn-icon-header${mainView === 'table' ? ' btn-icon-header--active' : ''}`}
            onClick={() => toggleMainView('table')}
            title="Table view"
          >
            <svg viewBox="0 0 14 14" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" style={{ width: 13, height: 13, flexShrink: 0 }}>
              <rect x="1" y="1" width="12" height="12" rx="1" />
              <line x1="1" y1="5" x2="13" y2="5" /><line x1="1" y1="9" x2="13" y2="9" /><line x1="5.5" y1="1" x2="5.5" y2="13" />
            </svg>
            <span className="btn-label">Table View</span>
          </button>
          <button className="btn-icon-header header-secondary" onClick={() => setShowManageTypes(true)} title="Manage types">
            <span className="app-icon icon-gear" /><span className="btn-label">Manage Types</span>
          </button>
          <button className="btn-icon-header" onClick={() => setShowGraph(true)} title="Graph view">
            <span className="app-icon icon-graph" /><span className="btn-label">Graph View</span>
          </button>
          <button
            className={`btn-icon-header${mainView === 'flashcards' ? ' btn-icon-header--active' : ''}`}
            onClick={() => toggleMainView('flashcards')}
            title="Flashcards"
          >
            <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" style={{ width: 13, height: 13, flexShrink: 0 }}>
              <rect x="1.5" y="2.5" width="13" height="11" rx="1.5" /><line x1="4.5" y1="6" x2="11.5" y2="6" /><line x1="4.5" y1="9.5" x2="9" y2="9.5" />
            </svg>
            <span className="btn-label">Flashcards</span>
          </button>
          <button
            className={`btn-icon-header${mainView === 'review' ? ' btn-icon-header--active' : ''}`}
            onClick={() => mainView === 'review' ? closeReview() : startReview()}
            title={reviewSummary
              ? `${reviewSummary.dueCount} due · ${reviewSummary.newCount} new`
              : 'Review notes with flashcards'}
          >
            <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" style={{ width: 13, height: 13, flexShrink: 0 }}>
              <rect x="2" y="4" width="10" height="9" rx="1.5" /><path d="M5 2h8.5A1.5 1.5 0 0115 3.5V11" />
            </svg>
            <span className="btn-label">Review</span>
            {reviewSummary && reviewSummary.dueCount + reviewSummary.newCount > 0 && (
              <span className="review-count-badge">{reviewSummary.dueCount + reviewSummary.newCount}</span>
            )}
          </button>
          <button
            className={`btn-icon-header${showStudyPath ? ' btn-icon-header--active' : ''}`}
            onClick={toggleStudyPath}
            title="Study path"
          >
            <StudyPathIcon />
            <span className="btn-label">Study Path</span>
          </button>
          <button
            className={`btn-icon-header${showChat ? ' btn-icon-header--active' : ''}`}
            onClick={toggleChat}
            title="Assistant (Ctrl+/)"
          >
            <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" style={{ width: 13, height: 13, flexShrink: 0 }}>
              <path d="M2.5 4L13.5 4A1 1 0 0114.5 5L14.5 11A1 1 0 0113.5 12L6 12L3.5 14L3.5 12A1 1 0 012.5 11Z" />
              <line x1="5.5" y1="7" x2="5.5" y2="7.01" /><line x1="8" y1="7" x2="8" y2="7.01" /><line x1="10.5" y1="7" x2="10.5" y2="7.01" />
            </svg>
            <span className="btn-label">Assistant</span>
          </button>
          <div className="header-more" ref={headerMenuRef}>
            <button
              className={`btn-icon-header${showHeaderMenu ? ' btn-icon-header--active' : ''}`}
              onClick={() => setShowHeaderMenu(v => !v)}
              title="More actions"
              aria-haspopup="menu"
              aria-expanded={showHeaderMenu}
            >
              <svg viewBox="0 0 16 16" fill="currentColor" style={{ width: 13, height: 13, flexShrink: 0 }}>
                <circle cx="3" cy="8" r="1.4" /><circle cx="8" cy="8" r="1.4" /><circle cx="13" cy="8" r="1.4" />
              </svg>
            </button>
            {showHeaderMenu && (
              <div className="header-more-menu" role="menu">
                <button role="menuitem" onClick={() => { setShowHeaderMenu(false); setShowExtract(true) }}>
                  <ExtractIcon />Extract Note
                </button>
                <button role="menuitem" onClick={() => { setShowHeaderMenu(false); handleExport() }}>
                  <span className="app-icon icon-export" />Export JSON
                </button>
                <button role="menuitem" onClick={() => { setShowHeaderMenu(false); setShowManageTypes(true) }}>
                  <span className="app-icon icon-gear" />Manage Types
                </button>
              </div>
            )}
          </div>
        </div>
      </header>

      {error && (
        <div className="page-alert">
          <span>{error}</span>
          <button className="page-alert-close" onClick={() => { clearTimeout(errorTimerRef.current); setError('') }}>
            <span className="app-icon icon-close" />
          </button>
        </div>
      )}

      <div className="layout">
        <NodeSidebar
          nodesList={nodesList}
          tagsList={tagsList}
          tagDefs={graph.tags || {}}
          foldersList={foldersList}
          graphId={id}
          search={search} setSearch={setSearch}
          tagFilter={tagFilter} setTagFilter={setTagFilter}
          selectedNodeId={selectedNodeId}
          onSelectNode={navigateTo}
          onCreateNode={handleCreateNode}
          onCreateFolder={handleCreateFolder}
          onEditFolder={handleEditFolder}
          onDeleteFolder={handleDeleteFolder}
          onMoveNodeToFolder={handleMoveNodeToFolder}
          pinnedNodeIds={pinnedNodeIds}
        />
        <div className="main-content">
          {mainView === 'review' && reviewSession ? (
            <ReviewSession
              key={reviewSession.key}
              graphId={id}
              single={reviewSession.single}
              onClose={closeReview}
              onReviewed={handleReviewed}
              onError={showError}
            />
          ) : mainView === 'flashcards' ? (
            <FlashcardsView
              graphId={id}
              selectedNodeId={selectedNodeId}
              onOpenNote={openNoteFromList}
              onReviewNote={single => startReview(single)}
              onReviewDue={() => startReview()}
              onCardsChanged={loadReviewSummary}
              onError={showError}
            />
          ) : mainView === 'table' ? (
            <NodeTableView
              nodesList={nodesList}
              tagsList={tagsList}
              tagDefs={graph.tags || {}}
              pinnedNodeIds={pinnedNodeIds}
              onAddTagToNodes={handleAddTagToNodes}
              onRemoveTagFromNodes={handleRemoveTagFromNodes}
              onSetPinnedMany={handleSetPinnedMany}
              onRequestConfirm={cfg => setModal({ type: 'confirm', ...cfg })}
            />
          ) : (
            <NodeEditor
              graph={graph}
              graphId={id}
              onError={showError}
              selectedNodeId={selectedNodeId}
              nodesList={nodesList}
              tagsList={tagsList}
              relTypesList={relTypesList}
              nodeTitle={nodeTitle} setNodeTitle={handleTitleChange}
              setNodeNote={handleNoteChange}
              noteLoadVersion={noteLoadVersion}
              nodeLoading={nodeLoading}
              confidenceRate={selectedNodeId ? displayConfidence(graph.nodes?.[selectedNodeId]) : null}
              selfRating={selectedNodeId ? graph.nodes?.[selectedNodeId]?.metadata?.userConfidenceRate ?? null : null}
              memory={selectedNodeId ? graph.nodes?.[selectedNodeId]?.metadata?.memory ?? null : null}
              onReviewNote={() => startReview({ id: selectedNodeId, title: graph.nodes?.[selectedNodeId]?.title || nodeTitle })}
              onCardsChanged={loadReviewSummary}
              saveStatus={saveStatus}
              localTags={localTags}
              localRelDict={localRelDict}
              connTarget={connTarget} setConnTarget={setConnTarget}
              connRelType={connRelType} setConnRelType={setConnRelType}
              connInverseTarget={connInverseTarget} setConnInverseTarget={setConnInverseTarget}
              connInverseRelType={connInverseRelType} setConnInverseRelType={setConnInverseRelType}
              onSave={handleSaveNode}
              onDelete={handleDeleteNode}
              onToggleMetadata={() => setShowMetadataSidebar(v => !v)}
              onConfirmDraft={handleConfirmDraft}
              draftFolderId={draftFolderId}
              onSetDraftFolder={fid => { draftFolderIdRef.current = fid; setDraftFolderId(fid) }}
              foldersList={foldersList}
              draftIncoming={draftIncoming}
              onRemoveDraftIncoming={handleRemoveDraftIncoming}
              onAddTag={handleAddTag}
              onRemoveTag={handleRemoveTag}
              onAddConnection={handleAddConnection}
              onAddInverseConnection={handleAddInverseConnection}
              onRemoveConnection={handleRemoveConnection}
              onNavigate={navigateTo}
              isPinned={selectedNodeId !== '__draft__' && pinnedNodeIds.has(selectedNodeId)}
              onTogglePin={() => handleTogglePin(selectedNodeId)}
            />
          )}
        </div>
      </div>

      <NodeSearchModal
        isOpen={showNodeSearch}
        onClose={() => setShowNodeSearch(false)}
        graphId={id}
        onSelectNode={nodeId => navigateTo(nodeId)}
      />

      <ChatSideBar
        isOpen={showChat}
        onClose={() => setShowChat(false)}
        graphId={id}
        graphName={graphName}
        selectedNodeTitle={selectedNodeId ? graph.nodes?.[selectedNodeId]?.title : null}
      />
      <StudyPathPanel
        isOpen={showStudyPath}
        onClose={() => setShowStudyPath(false)}
        graphId={id}
        nodesList={nodesList}
        relTypesList={relTypesList}
        selectedNodeId={selectedNodeId}
        onNavigate={navigateTo}
        onHighlightChange={setStudyHighlight}
        showGraph={showGraph}
        onShowGraph={() => setShowGraph(true)}
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
