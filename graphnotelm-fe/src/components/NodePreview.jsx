import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { noteNodeApi } from '../api'

const SHOW_DELAY_MS = 350
const SNIPPET_LENGTH = 180
const CARD_WIDTH = 280
const CARD_ROOM_BELOW = 160

// Plain-text opening of a note's HTML, for previews.
export function noteSnippet(html, maxLength = SNIPPET_LENGTH) {
  if (!html) return ''
  // Space out block elements so adjacent paragraphs don't run together once tags are dropped
  const spaced = html.replace(/<\/(p|h[1-6]|li|blockquote|pre)>|<br\s*\/?>/gi, '$& ')
  const text = new DOMParser().parseFromString(spaced, 'text/html').body.textContent
    .replace(/\s+/g, ' ')
    .trim()
  return text.length > maxLength ? `${text.slice(0, maxLength).trimEnd()}…` : text
}

// Hover preview for links to other notes. The graph skeleton omits note bodies, so a note
// that hasn't been opened yet is fetched on first hover and cached while the page is open.
export function useNodePreview(graph, graphId) {
  const [preview, setPreview] = useState(null) // { nodeId, rect, snippet } — snippet undefined while loading, null on error
  const timerRef = useRef(null)
  const cacheRef = useRef(new Map())
  const graphRef = useRef(graph)
  graphRef.current = graph

  function show(nodeId, anchorEl) {
    clearTimeout(timerRef.current)
    timerRef.current = setTimeout(() => {
      const rect = anchorEl.getBoundingClientRect()
      // A note already loaded into the graph is current; the cache only covers unopened notes
      const loadedNote = graphRef.current.nodes?.[nodeId]?.note
      const snippet = loadedNote !== undefined ? noteSnippet(loadedNote) : cacheRef.current.get(nodeId)
      setPreview({ nodeId, rect, snippet })
      if (snippet !== undefined) return

      noteNodeApi.get(graphId, nodeId)
        .then(node => noteSnippet(node?.note))
        .catch(() => null)
        .then(fetched => {
          if (fetched !== null) cacheRef.current.set(nodeId, fetched)
          setPreview(p => (p?.nodeId === nodeId ? { ...p, snippet: fetched } : p))
        })
    }, SHOW_DELAY_MS)
  }

  function hide() {
    clearTimeout(timerRef.current)
    setPreview(null)
  }

  // The card is fixed-position, so it would drift away from its chip on scroll
  useEffect(() => {
    if (!preview) return
    window.addEventListener('scroll', hide, true)
    return () => window.removeEventListener('scroll', hide, true)
  }, [preview]) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => () => clearTimeout(timerRef.current), [])

  return { preview, show, hide }
}

export function NodePreviewCard({ preview, title }) {
  if (!preview) return null
  const { rect, snippet } = preview

  const left = Math.max(8, Math.min(rect.left, window.innerWidth - CARD_WIDTH - 8))
  // Connections sit at the bottom of a note, so open upward when there's no room below
  const style = rect.bottom + CARD_ROOM_BELOW > window.innerHeight
    ? { left, bottom: window.innerHeight - rect.top + 6, width: CARD_WIDTH }
    : { left, top: rect.bottom + 6, width: CARD_WIDTH }

  let body = snippet
  if (snippet === undefined) body = 'Loading…'
  else if (snippet === null) body = 'Preview unavailable.'
  else if (!snippet) body = 'Empty note.'

  // Portalled to <body>: the editor's entry animation leaves a transform on an ancestor,
  // which would otherwise make `position: fixed` relative to that ancestor.
  return createPortal(
    <div className="node-preview-card" style={style} role="tooltip">
      <div className="node-preview-title">{title}</div>
      <div className={`node-preview-snippet${snippet ? '' : ' is-muted'}`}>{body}</div>
    </div>,
    document.body,
  )
}
