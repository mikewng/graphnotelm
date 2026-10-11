import { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { noteGraphApi } from '../api'
import Modal from '../components/Modal'
import SettingsModal from '../components/settings/SettingsModal'
import './HomePage.css'

export default function HomePage() {
  const [graphs, setGraphs] = useState([])
  const [graphsLoaded, setGraphsLoaded] = useState(false)
  const [archivedGraphs, setArchivedGraphs] = useState([])
  const [view, setView] = useState('active') // 'active' | 'archived'
  const [search, setSearch] = useState('')
  const [error, setError] = useState('')
  const [modal, setModal] = useState(null)
  const [settingsOpen, setSettingsOpen] = useState(false)
  const navigate = useNavigate()
  const importInputRef = useRef(null)

  // ── Generate from extract ──────────────────────────────────────────────────
  const [showExtract, setShowExtract] = useState(false)
  const [extractName, setExtractName] = useState('')
  const [extractContent, setExtractContent] = useState('')
  const [extractLoading, setExtractLoading] = useState(false)

  useEffect(() => {
    noteGraphApi.list()
      .then(setGraphs)
      .catch(err => setError(err.message))
      .finally(() => setGraphsLoaded(true))
  }, [])

  function loadArchived() {
    noteGraphApi.listArchived().then(setArchivedGraphs).catch(err => setError(err.message))
  }

  function handleViewToggle(v) {
    setView(v)
    setSearch('')
    if (v === 'archived') loadArchived()
  }

  // ── Active graph actions ───────────────────────────────────────────────────
  function handleEdit(g) {
    setModal({
      type: 'input',
      title: 'Edit notegraph',
      confirmLabel: 'Save',
      fields: [
        { key: 'name', label: 'Name', placeholder: 'Collection name...', defaultValue: g.name },
        { key: 'description', label: 'Description', placeholder: 'Optional description...', defaultValue: g.description || '', required: false },
      ],
      onConfirm: async ({ name, description }) => {
        try {
          await noteGraphApi.editMetadata(g.id, { name, description: description || null })
          setGraphs(prev => prev.map(x => x.id === g.id ? { ...x, name, description } : x))
        } catch (err) { setError(err.message) }
      },
    })
  }

  function handleDelete(g) {
    setModal({
      type: 'confirm',
      title: 'Archive notegraph',
      message: `Archive "${g.name}"? You can restore it from the Archive view.`,
      confirmLabel: 'Archive',
      danger: true,
      onConfirm: async () => {
        try {
          await noteGraphApi.delete(g.id)
          setGraphs(prev => prev.filter(x => x.id !== g.id))
        } catch (err) { setError(err.message) }
      },
    })
  }

  // ── Archived graph actions ─────────────────────────────────────────────────
  function handleUnarchive(g) {
    setModal({
      type: 'confirm',
      title: 'Restore notegraph',
      message: `Restore "${g.name}" to your active graphs?`,
      confirmLabel: 'Restore',
      onConfirm: async () => {
        try {
          await noteGraphApi.unarchive(g.id)
          setArchivedGraphs(prev => prev.filter(x => x.id !== g.id))
          noteGraphApi.list().then(setGraphs).catch(err => setError(err.message))
        } catch (err) { setError(err.message) }
      },
    })
  }

  function handleHardDelete(g) {
    setModal({
      type: 'confirm',
      title: 'Permanently delete',
      message: `Permanently delete "${g.name}"? This cannot be undone and all data will be lost forever.`,
      confirmLabel: 'Delete Forever',
      danger: true,
      onConfirm: async () => {
        try {
          await noteGraphApi.hardDelete(g.id)
          setArchivedGraphs(prev => prev.filter(x => x.id !== g.id))
        } catch (err) { setError(err.message) }
      },
    })
  }

  async function handleImport(e) {
    const file = e.target.files?.[0]
    if (!file) return
    e.target.value = ''
    try {
      const text = await file.text()
      const data = JSON.parse(text)
      const result = await noteGraphApi.import(data)
      if (result?.id) navigate(`/notegraph/${result.id}`, { state: { name: result.name } })
    } catch (err) { setError(err.message || 'Invalid JSON file.') }
  }

  function handleCreate() {
    setModal({
      type: 'input',
      title: 'New notegraph',
      confirmLabel: 'Create',
      fields: [
        { key: 'name', label: 'Name', placeholder: 'Collection name...' },
        { key: 'description', label: 'Description', placeholder: 'Optional description...', required: false },
      ],
      onConfirm: async ({ name, description }) => {
        try {
          const created = await noteGraphApi.create({ name, ...(description ? { description } : {}) })
          navigate(`/notegraph/${created.id}`, { state: { name } })
        } catch (err) { setError(err.message) }
      },
    })
  }

  async function handleExtractGenerate() {
    if (!extractContent.trim()) return
    setExtractLoading(true)
    try {
      const created = await noteGraphApi.createFromExtract({
        name: extractName.trim() || null,
        content: extractContent,
      })
      setShowExtract(false)
      setExtractName('')
      setExtractContent('')
      if (created?.id) navigate(`/notegraph/${created.id}`, { state: { name: created.name } })
    } catch (err) { setError(err.message) }
    finally { setExtractLoading(false) }
  }

  function handleLogout() {
    localStorage.removeItem('token')
    navigate('/auth')
  }

  const activeList = graphs.filter(g => g.name.toLowerCase().includes(search.toLowerCase()))
  const archivedList = archivedGraphs.filter(g => g.name.toLowerCase().includes(search.toLowerCase()))
  const displayList = view === 'active' ? activeList : archivedList

  return (
    <div className="page">
      <Modal modal={modal} onCancel={() => setModal(null)} />
      <SettingsModal open={settingsOpen} onClose={() => setSettingsOpen(false)} />

      {showExtract && (
        <div className="extract-backdrop" onClick={e => { if (e.target === e.currentTarget && !extractLoading) { setShowExtract(false); setExtractName(''); setExtractContent('') } }}>
          <div className="extract-modal">
            <div className="extract-header">
              <span>Generate Notegraph with AI</span>
              <button className="btn-ghost btn-icon-sm" onClick={() => { setShowExtract(false); setExtractName(''); setExtractContent('') }} disabled={extractLoading}>
                <span className="app-icon icon-close" />
              </button>
            </div>
            <p className="extract-hint">Paste any text content below — the AI will extract concepts, build nodes, and create a full knowledge graph for you.</p>
            <div className="extract-name-row">
              <input
                className="extract-name-input"
                value={extractName}
                onChange={e => setExtractName(e.target.value)}
                placeholder="Graph name (optional)"
                disabled={extractLoading}
              />
            </div>
            <textarea
              className="extract-textarea"
              value={extractContent}
              onChange={e => setExtractContent(e.target.value)}
              placeholder="Paste your content here..."
              disabled={extractLoading}
              autoFocus
            />
            {extractLoading && (
              <p className="extract-progress">
                <span className="generate-spinner" /> Building your knowledge graph — this may take a moment…
              </p>
            )}
            <div className="extract-footer">
              <button className="btn-ghost" onClick={() => { setShowExtract(false); setExtractName(''); setExtractContent('') }} disabled={extractLoading}>Cancel</button>
              <button
                className="extract-submit"
                onClick={handleExtractGenerate}
                disabled={!extractContent.trim() || extractLoading}
              >
                {extractLoading ? 'Generating…' : '✦ Generate'}
              </button>
            </div>
          </div>
        </div>
      )}
      <header>
        <h2>GraphNoteLM</h2>
        <div className="header-actions">
          {view === 'active' && (
            <>
              <button className="btn-ghost" onClick={handleCreate}>Create Notegraph</button>
              <button className="btn-ghost" onClick={() => importInputRef.current.click()}>Import Notegraph</button>
              <button className="btn-ghost" onClick={() => setShowExtract(true)}>Generate with AI</button>
              <input ref={importInputRef} type="file" accept=".json,application/json" style={{ display: 'none' }} onChange={handleImport} />
            </>
          )}
          <button className="btn-ghost" onClick={handleLogout}>Logout</button>
        </div>
      </header>

      <div className="home-scroll">
        <div className="home-body">
          <div className="home-toolbar">
            <input
              className="home-search"
              type="text"
              placeholder={view === 'active' ? 'Search notegraphs...' : 'Search archive...'}
              value={search}
              onChange={e => setSearch(e.target.value)}
            />
            <div className="home-leftmost">
              <span className="home-count">
                {displayList.length} {displayList.length === 1 ? 'graph' : 'graphs'}
              </span>
              <div className="home-view-tabs">
                <button
                  className={`home-tab${view === 'active' ? ' active' : ''}`}
                  onClick={() => handleViewToggle('active')}
                >Active</button>
                <button
                  className={`home-tab${view === 'archived' ? ' active' : ''}`}
                  onClick={() => handleViewToggle('archived')}
                >Archive</button>
              </div>
            </div>
          </div>

          {error && <p className="home-error">{error}</p>}

          {/* Nothing until the first list arrives, so a slow backend start doesn't flash "No notegraphs yet." */}
          {view === 'active' && !graphsLoaded ? null : displayList.length === 0 ? (
            <div className="graph-empty">
              <p>{search
                ? `No results for "${search}"`
                : view === 'archived' ? 'No archived notegraphs.' : 'No notegraphs yet.'
              }</p>
              {!search && view === 'active' && <p className="hint">Use "Create Notegraph" above to get started.</p>}
            </div>
          ) : view === 'active' ? (
            <ul className="graph-list">
              {activeList.map(g => (
                <li key={g.id} className="graph-list-item">
                  <button className="graph-name-btn" onClick={() => navigate(`/notegraph/${g.id}`, { state: { name: g.name } })}>
                    <span className="graph-name">{g.name}</span>
                    <span className="graph-description">{g.description || 'No description.'}</span>
                  </button>
                  <button className="graph-list-edit-btn" onClick={() => handleEdit(g)}>Edit</button>
                  <button className="btn-danger" onClick={() => handleDelete(g)}>Archive</button>
                </li>
              ))}
            </ul>
          ) : (
            <ul className="graph-list">
              {archivedList.map(g => (
                <li key={g.id} className="graph-list-item">
                  <div className="graph-name-btn" style={{ cursor: 'default' }}>
                    <span className="graph-name">{g.name}</span>
                    <span className="graph-description">{g.description || 'No description.'}</span>
                  </div>
                  <div style={{ display: 'flex', gap: 6 }}>
                    <button className="btn-ghost btn-always-visible" onClick={() => handleUnarchive(g)}>Restore</button>
                    <button className="btn-danger btn-always-visible" onClick={() => handleHardDelete(g)}>Delete Forever</button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>

      <button
        className="home-settings-btn"
        onClick={() => setSettingsOpen(true)}
        title="Settings"
        aria-label="Settings"
      >
        <span className="app-icon icon-gear" />
        <span className="home-settings-btn-label">Settings</span>
      </button>
    </div>
  )
}
