import { useEffect, useState } from 'react'
import { flashcardApi } from '../api'
import CardForm from './FlashcardForm'

export default function NodeFlashcards({ graphId, nodeId, onError, onCardsChanged, onReview }) {
  const [cards, setCards] = useState(null) // null while loading
  const [front, setFront] = useState('')
  const [back, setBack] = useState('')
  const [editing, setEditing] = useState(null) // { id, front, back } | null
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let cancelled = false
    flashcardApi.list(graphId, nodeId)
      .then(list => { if (!cancelled) setCards(list) })
      .catch(err => {
        if (cancelled) return
        setCards([])
        onError?.(err.message)
      })
    return () => { cancelled = true }
  }, [graphId, nodeId]) // eslint-disable-line react-hooks/exhaustive-deps

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

  const handleAdd = () => run(async () => {
    const card = await flashcardApi.create(graphId, nodeId, { front, back })
    setCards(prev => [...(prev || []), card])
    setFront('')
    setBack('')
  })

  const handleSaveEdit = () => run(async () => {
    const card = await flashcardApi.edit(graphId, nodeId, editing.id, { front: editing.front, back: editing.back })
    setCards(prev => prev.map(c => c.id === card.id ? card : c))
    setEditing(null)
  })

  const handleDelete = cardId => run(async () => {
    await flashcardApi.delete(graphId, nodeId, cardId)
    setCards(prev => prev.filter(c => c.id !== cardId))
    if (editing?.id === cardId) setEditing(null)
  })

  return (
    <div className="field">
      <div className="flashcards-header">
        <div className="field-label">Flashcards{cards ? ` · ${cards.length}` : ''}</div>
        <button className="btn-ghost flashcards-review-btn" onClick={onReview}
          title="Review this note now — through a card, or from its own text if it has none">
          Review this note
        </button>
      </div>

      {cards === null ? (
        <span className="field-empty">Loading…</span>
      ) : (
        <>
          {cards.length === 0 && (
            <p className="hint">
              Add cards to quiz yourself on this note. Notes with cards join your review queue,
              and each review updates the note's confidence.
            </p>
          )}
          {cards.length > 0 && (
            <ul className="flashcard-list">
              {cards.map(card => (
                <li key={card.id} className="flashcard-item">
                  {editing?.id === card.id ? (
                    <CardForm
                      front={editing.front}
                      back={editing.back}
                      onFront={v => setEditing(e => ({ ...e, front: v }))}
                      onBack={v => setEditing(e => ({ ...e, back: v }))}
                      onSubmit={handleSaveEdit}
                      onCancel={() => setEditing(null)}
                      submitLabel="Save"
                      busy={busy}
                    />
                  ) : (
                    <>
                      <div className="flashcard-sides">
                        <div className="flashcard-front">{card.front}</div>
                        <div className="flashcard-back">{card.back}</div>
                      </div>
                      <div className="flashcard-item-actions">
                        <button className="btn-ghost btn-icon-sm" title="Edit card"
                          onClick={() => setEditing({ id: card.id, front: card.front, back: card.back })} disabled={busy}>
                          <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" width="12" height="12">
                            <path d="M11 2l3 3-8 8H3v-3L11 2z" />
                          </svg>
                        </button>
                        <button className="btn-ghost btn-icon-sm" title="Delete card" onClick={() => handleDelete(card.id)} disabled={busy}>
                          <span className="app-icon icon-close" />
                        </button>
                      </div>
                    </>
                  )}
                </li>
              ))}
            </ul>
          )}
          <CardForm
            front={front}
            back={back}
            onFront={setFront}
            onBack={setBack}
            onSubmit={handleAdd}
            submitLabel="Add card"
            busy={busy}
          />
        </>
      )}
    </div>
  )
}
