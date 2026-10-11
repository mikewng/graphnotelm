import { useEffect, useMemo, useState } from 'react'
import { flashcardApi } from '../api'
import CardForm from './FlashcardForm'
import {
  FLASHCARD_FILTERS, countFlashcardNotes, filterFlashcardNotes, noteReviewStatus, withCards,
} from '../utils/flashcards'
import { NEW_PER_SESSION } from '../utils/review'

const EMPTY_MESSAGES = {
  all: 'No flashcards yet. Add some from a note, or pick a note under "No cards".',
  due: 'Nothing is due right now.',
  new: 'No new cards waiting — every note with cards has been reviewed.',
  empty: 'Every note has at least one card.',
}

// Every flashcard in the graph, grouped by note — shown in place of the editor, like the table view.
export default function FlashcardsView({ graphId, selectedNodeId, onOpenNote, onReviewNote, onReviewDue, onCardsChanged, onError }) {
  const [notes, setNotes] = useState(null) // null while loading
  const [query, setQuery] = useState('')
  const [filter, setFilter] = useState('all')
  const [sort, setSort] = useState('title')
  const [editing, setEditing] = useState(null) // { nodeId, cardId (null when adding), front, back }
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let cancelled = false
    flashcardApi.listGraph(graphId)
      .then(list => { if (!cancelled) setNotes(list) })
      .catch(err => {
        if (cancelled) return
        setNotes([])
        onError?.(err.message)
      })
    return () => { cancelled = true }
  }, [graphId]) // eslint-disable-line react-hooks/exhaustive-deps

  const counts = useMemo(() => countFlashcardNotes(notes || []), [notes])
  const visible = useMemo(
    () => filterFlashcardNotes(notes || [], { query, filter, sort }),
    [notes, query, filter, sort],
  )
  // What a session will hold: everything due, plus new notes up to the per-session limit.
  const reviewable = counts.due + Math.min(counts.new, NEW_PER_SESSION)

  function updateCards(nodeId, change) {
    setNotes(prev => prev.map(n => n.nodeId === nodeId ? withCards(n, change(n.cards)) : n))
  }

  async function run(action) {
    setBusy(true)
    try {
      await action()
      onCardsChanged?.()
    } catch (err) {
      onError?.(err.message)
    } finally {
      setBusy(false)
    }
  }

  const saveEditing = () => run(async () => {
    const { nodeId, cardId, front, back } = editing
    if (cardId) {
      const card = await flashcardApi.edit(graphId, nodeId, cardId, { front, back })
      updateCards(nodeId, cards => cards.map(c => c.id === card.id ? card : c))
    } else {
      const card = await flashcardApi.create(graphId, nodeId, { front, back })
      updateCards(nodeId, cards => [...cards, card])
    }
    setEditing(null)
  })

  const deleteCard = (nodeId, cardId) => run(async () => {
    await flashcardApi.delete(graphId, nodeId, cardId)
    updateCards(nodeId, cards => cards.filter(c => c.id !== cardId))
    if (editing?.cardId === cardId) setEditing(null)
  })

  function renderForm() {
    return (
      <CardForm
        front={editing.front}
        back={editing.back}
        onFront={v => setEditing(e => ({ ...e, front: v }))}
        onBack={v => setEditing(e => ({ ...e, back: v }))}
        onSubmit={saveEditing}
        onCancel={() => setEditing(null)}
        submitLabel={editing.cardId ? 'Save' : 'Add card'}
        busy={busy}
        autoFocus
      />
    )
  }

  function renderNote(note) {
    const status = noteReviewStatus(note)
    const addingHere = editing?.nodeId === note.nodeId && !editing.cardId
    return (
      <section key={note.nodeId} className={`flashcards-note${note.nodeId === selectedNodeId ? ' is-selected' : ''}`}>
        <div className="flashcards-note-header">
          <button className="flashcards-note-title" onClick={() => onOpenNote(note.nodeId)} title="Open note">
            {note.title || '(untitled)'}
          </button>
          <span className={`flashcards-status flashcards-status--${status.tone}`}>{status.label}</span>
          <span className="flashcards-conf" title="Confidence">{note.confidence > 0 ? note.confidence : '–'}</span>
          <div className="flashcards-note-actions">
            <button className="btn-ghost" onClick={() => setEditing({ nodeId: note.nodeId, cardId: null, front: '', back: '' })} disabled={busy}>
              Add card
            </button>
            <button className="btn-ghost" onClick={() => onReviewNote({ id: note.nodeId, title: note.title })}>
              Review
            </button>
          </div>
        </div>

        {note.cards.length > 0 && (
          <ul className="flashcard-list">
            {note.cards.map(card => (
              <li key={card.id} className="flashcard-item">
                {editing?.cardId === card.id ? renderForm() : (
                  <>
                    <div className="flashcard-sides">
                      <div className="flashcard-front">{card.front}</div>
                      <div className="flashcard-back">{card.back}</div>
                    </div>
                    <div className="flashcard-item-actions">
                      <button className="btn-ghost btn-icon-sm" title="Edit card" disabled={busy}
                        onClick={() => setEditing({ nodeId: note.nodeId, cardId: card.id, front: card.front, back: card.back })}>
                        <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" width="12" height="12">
                          <path d="M11 2l3 3-8 8H3v-3L11 2z" />
                        </svg>
                      </button>
                      <button className="btn-ghost btn-icon-sm" title="Delete card" disabled={busy}
                        onClick={() => deleteCard(note.nodeId, card.id)}>
                        <span className="app-icon icon-close" />
                      </button>
                    </div>
                  </>
                )}
              </li>
            ))}
          </ul>
        )}
        {addingHere && <div className="flashcard-item">{renderForm()}</div>}
      </section>
    )
  }

  return (
    <div className="flashcards-view">
      <div className="table-view-toolbar">
        <input
          className="table-view-search"
          type="text"
          placeholder="Search cards…"
          value={query}
          onChange={e => setQuery(e.target.value)}
        />
        <div className="study-segmented flashcards-filters">
          {FLASHCARD_FILTERS.map(f => (
            <button key={f.id} className={filter === f.id ? '' : 'btn-ghost'} onClick={() => setFilter(f.id)}>
              {f.label} <span className="flashcards-filter-count">{counts[f.id]}</span>
            </button>
          ))}
        </div>
        <select className="flashcards-sort" value={sort} onChange={e => setSort(e.target.value)} title="Sort">
          <option value="title">Sort: title</option>
          <option value="due">Sort: next review</option>
        </select>
        <span className="flashcards-total">{counts.cards} card{counts.cards === 1 ? '' : 's'}</span>
        <button className="review-primary flashcards-review-due" onClick={onReviewDue} disabled={reviewable === 0}
          title={reviewable === 0 ? 'Nothing is due' : `${counts.due} due · ${Math.min(counts.new, NEW_PER_SESSION)} new`}>
          Start review{reviewable > 0 ? ` (${reviewable})` : ''}
        </button>
      </div>

      <div className="flashcards-body">
        {notes === null
          ? <p className="review-empty">Loading…</p>
          : visible.length === 0
            ? <p className="review-empty">{query.trim() ? 'No cards match your search.' : EMPTY_MESSAGES[filter]}</p>
            : visible.map(renderNote)}
      </div>
    </div>
  )
}
