import { useState, useRef, useEffect } from 'react'

export default function SearchSelect({ value, onChange, options, placeholder }) {
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const containerRef = useRef(null)

  const selected = options.find(o => o.id === value)

  const filtered = options.filter(o =>
    !query || o.label.toLowerCase().includes(query.toLowerCase())
  )

  function handleSelect(id) {
    onChange(id)
    setQuery('')
    setOpen(false)
  }

  function handleInputChange(e) {
    setQuery(e.target.value)
    if (!open) setOpen(true)
    if (!e.target.value) onChange('')
  }

  function handleFocus() {
    setOpen(true)
  }

  // Close when clicking outside
  useEffect(() => {
    function onMouseDown(e) {
      if (containerRef.current && !containerRef.current.contains(e.target)) {
        setOpen(false)
        setQuery('')
      }
    }
    document.addEventListener('mousedown', onMouseDown)
    return () => document.removeEventListener('mousedown', onMouseDown)
  }, [])

  const displayValue = open ? query : (selected?.label ?? '')

  return (
    <div className="search-select" ref={containerRef}>
      <input
        className="search-select-input"
        value={displayValue}
        onChange={handleInputChange}
        onFocus={handleFocus}
        placeholder={placeholder}
      />
      {open && (
        <div className="search-select-dropdown">
          {filtered.length === 0 ? (
            <div className="search-select-empty">No matches</div>
          ) : (
            filtered.map(o => (
              <div
                key={o.id}
                className={`search-select-option${o.id === value ? ' selected' : ''}`}
                onMouseDown={() => handleSelect(o.id)}
              >
                {o.label}
              </div>
            ))
          )}
        </div>
      )}
    </div>
  )
}
