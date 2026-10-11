import { useState, useEffect, useRef } from 'react'
import { noteNodeApi } from '../api'

function highlightText(text, query) {
  if (!query || !text) return text
  const escaped = query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const parts = text.split(new RegExp(`(${escaped})`, 'gi'))
  return parts.map((part, i) =>
    part.toLowerCase() === query.toLowerCase()
      ? <mark key={i} className="search-highlight">{part}</mark>
      : part
  )
}

export default function NodeSearchModal({ isOpen, onClose, graphId, onSelectNode }) {
  const [query, setQuery] = useState('')
  const [results, setResults] = useState([])
  const [loading, setLoading] = useState(false)
  const [searched, setSearched] = useState(false)
  const inputRef = useRef(null)
  const debounceRef = useRef(null)

  useEffect(() => {
    if (!isOpen) return
    setQuery('')
    setResults([])
    setSearched(false)
    setTimeout(() => inputRef.current?.focus(), 50)
  }, [isOpen])

  useEffect(() => {
    clearTimeout(debounceRef.current)
    if (!query.trim()) {
      setResults([])
      setSearched(false)
      return
    }
    debounceRef.current = setTimeout(async () => {
      setLoading(true)
      try {
        const res = await noteNodeApi.searchNodes(graphId, query.trim())
        setResults(res ?? [])
        setSearched(true)
      } catch {
        setResults([])
        setSearched(true)
      } finally {
        setLoading(false)
      }
    }, 400)
    return () => clearTimeout(debounceRef.current)
  }, [query, graphId])

  function handleKeyDown(e) {
    if (e.key === 'Escape') onClose()
  }

  if (!isOpen) return null

  return (
    <div
      className="search-backdrop"
      onClick={e => { if (e.target === e.currentTarget) onClose() }}
      onKeyDown={handleKeyDown}
    >
      <div className="search-modal">
        <div className="search-input-row">
          <svg className="search-icon" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round">
            <circle cx="6.5" cy="6.5" r="4.5" /><line x1="10.5" y1="10.5" x2="14" y2="14" />
          </svg>
          <input
            ref={inputRef}
            className="search-input"
            value={query}
            onChange={e => setQuery(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Search note content…"
            autoComplete="off"
          />
          {loading && <span className="search-spinner" />}
          <button className="btn-ghost btn-icon-sm" onClick={onClose} title="Close">
            <span className="app-icon icon-close" />
          </button>
        </div>

        <div className="search-results">
          {!query.trim() && (
            <p className="search-empty">Type to search across all node titles and notes.</p>
          )}
          {query.trim() && !loading && searched && results.length === 0 && (
            <p className="search-empty">No results for "{query}"</p>
          )}
          {results.map(r => (
            <button
              key={r.id}
              className="search-result-item"
              onClick={() => { onSelectNode(r.id); onClose() }}
            >
              <span className="search-result-title">
                {highlightText(r.title ?? 'Untitled', query)}
              </span>
              {r.matchedNote && r.snippet && (
                <span className="search-result-snippet">
                  {highlightText(r.snippet, query)}
                </span>
              )}
              <span className="search-result-badges">
                {r.matchedTitle && <span className="search-badge search-badge--title">title</span>}
                {r.matchedNote && <span className="search-badge search-badge--note">note</span>}
              </span>
            </button>
          ))}
        </div>
      </div>
    </div>
  )
}
