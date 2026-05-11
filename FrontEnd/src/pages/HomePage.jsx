import { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { noteGraphApi } from '../api'
import Modal from '../components/Modal'
import './HomePage.css'

export default function HomePage() {
  const [graphs, setGraphs] = useState([])
  const [search, setSearch] = useState('')
  const [error, setError] = useState('')
  const [modal, setModal] = useState(null)
  const navigate = useNavigate()
  const importInputRef = useRef(null)

  useEffect(() => {
    noteGraphApi.list().then(setGraphs).catch(err => setError(err.message))
  }, [])

  function handleDelete(g) {
    setModal({
      type: 'confirm',
      title: 'Delete notegraph',
      message: `Delete "${g.name}"? This cannot be undone.`,
      confirmLabel: 'Delete',
      danger: true,
      onConfirm: async () => {
        try {
          await noteGraphApi.delete(g.id)
          setGraphs(prev => prev.filter(x => x.id !== g.id))
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
    } catch (err) {
      setError(err.message || 'Invalid JSON file.')
    }
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

  function handleLogout() {
    localStorage.removeItem('token')
    navigate('/auth')
  }

  const filtered = graphs.filter(g =>
    g.name.toLowerCase().includes(search.toLowerCase())
  )

  return (
    <div className="page">
      <Modal modal={modal} onCancel={() => setModal(null)} />
      <header>
        <h2>GraphNoteLM</h2>
        <div className="header-actions">
          <button className="btn-ghost" onClick={handleCreate}>Create Notegraph</button>
          <button className="btn-ghost" onClick={() => importInputRef.current.click()}>Import Notegraph</button>
          <input
            ref={importInputRef}
            type="file"
            accept=".json,application/json"
            style={{ display: 'none' }}
            onChange={handleImport}
          />
          <button className="btn-ghost" onClick={handleLogout}>Logout</button>
        </div>
      </header>
      <div className="home-body">
        <div className="home-toolbar">
          <input
            className="home-search"
            type="text"
            placeholder="Search notegraphs..."
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
          <span className="home-count">
            {filtered.length} {filtered.length === 1 ? 'graph' : 'graphs'}
          </span>
        </div>
        {error && <p className="home-error">{error}</p>}
        {filtered.length === 0 ? (
          <div className="graph-empty">
            <p>{search ? `No results for "${search}"` : 'No notegraphs yet.'}</p>
            {!search && <p className="hint">Use "Create Notegraph" above to get started.</p>}
          </div>
        ) : (
          <ul className="graph-list">
            {filtered.map(g => (
              <li key={g.id} className="graph-list-item">
                <button className="graph-name-btn" onClick={() => navigate(`/notegraph/${g.id}`, { state: { name: g.name } })}>
                  <span className="graph-name">{g.name}</span>
                  <span className="graph-description">{g.description || 'No description.'}</span>
                </button>
                <button className="btn-danger" onClick={() => handleDelete(g)}>Delete</button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}
