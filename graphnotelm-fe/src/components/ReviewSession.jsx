import { useCallback, useEffect, useRef, useState } from 'react'
import { flashcardApi, noteNodeApi, reviewApi } from '../api'
import { GRADES, NEW_PER_SESSION, formatDue, noteToPlainText, pickCard } from '../utils/review'

// The review screen, shown in place of the note editor or table view. It's remounted for each
// session, so its state starts fresh. `single` ({ id, title }) reviews just that note; otherwise
// the graph's due queue is used.
export default function ReviewSession({ graphId, single = null, onClose, onReviewed, onError }) {
  const [queue, setQueue] = useState(null) // null while loading
  const [loadError, setLoadError] = useState('')
  const [index, setIndex] = useState(0)
  const [revealed, setRevealed] = useState(false)
  const [card, setCard] = useState(null)
  const [noteText, setNoteText] = useState(null) // for notes without cards; null while loading
  const [submitting, setSubmitting] = useState(false)
  const [tally, setTally] = useState({})
  const [last, setLast] = useState(null) // { title, dueAt } of the previous answer
  const screenRef = useRef(null)

  useEffect(() => {
    let cancelled = false
    const load = single
      ? flashcardApi.list(graphId, single.id).then(cards => [{ nodeId: single.id, title: single.title, isNew: false, cards }])
      : reviewApi.queue(graphId, NEW_PER_SESSION).then(result => result?.items ?? [])
    load
      .then(items => { if (!cancelled) setQueue(items) })
      .catch(err => {
        if (cancelled) return
        setQueue([])
        setLoadError(err.message)
      })
    return () => { cancelled = true }
  }, [graphId, single?.id]) // eslint-disable-line react-hooks/exhaustive-deps

  const current = queue?.[index] ?? null

  // Each step hides the answer and picks a card — or loads the note itself when it has none.
  useEffect(() => {
    if (!current) return
    setRevealed(false)
    const picked = pickCard(current.cards)
    setCard(picked)
    setNoteText(null)
    if (picked) return
    let cancelled = false
    noteNodeApi.get(graphId, current.nodeId)
      .then(node => { if (!cancelled) setNoteText(noteToPlainText(node?.note)) })
      .catch(() => { if (!cancelled) setNoteText('') })
    return () => { cancelled = true }
  }, [current, index, graphId])

  // Shortcuts only apply while this screen has focus, so typing elsewhere never grades a card.
  // The button that was pressed unmounts at each step, so take focus back.
  useEffect(() => {
    screenRef.current?.focus({ preventScroll: true })
  }, [index, revealed, queue === null])

  const grade = useCallback(async value => {
    if (!current || !revealed || submitting) return
    setSubmitting(true)
    try {
      const result = await reviewApi.submit(graphId, current.nodeId, value, card?.id ?? null)
      onReviewed?.(current.nodeId, result)
      setTally(t => ({ ...t, [value]: (t[value] || 0) + 1 }))
      setLast({ title: current.title, dueAt: result.memory.dueAt })
      // A forgotten note comes back at the end of this session.
      if (value === 1) setQueue(q => [...q, { ...current }])
      setIndex(i => i + 1)
    } catch (err) {
      onError?.(err.message)
    } finally {
      setSubmitting(false)
    }
  }, [current, revealed, submitting, graphId, card, onReviewed, onError])

  function onKeyDown(e) {
    if (['INPUT', 'TEXTAREA', 'SELECT'].includes(e.target.tagName)) return
    if (e.key === 'Escape') { onClose(); return }
    if (!current) return
    if (!revealed && (e.key === ' ' || e.key === 'Enter')) {
      e.preventDefault()
      setRevealed(true)
      return
    }
    const match = revealed && GRADES.find(g => g.key === e.key)
    if (match) {
      e.preventDefault()
      grade(match.value)
    }
  }

  const total = queue?.length ?? 0
  const done = queue !== null && total > 0 && index >= total
  const reviewed = Object.values(tally).reduce((sum, n) => sum + n, 0)

  function renderBody() {
    if (queue === null) return <p className="review-empty">Loading…</p>
    if (loadError) return <p className="review-error">{loadError}</p>
    if (total === 0) {
      return (
        <p className="review-empty">
          Nothing is due. Notes join the queue once they have flashcards, and any note can be
          reviewed from its editor.
        </p>
      )
    }
    if (done) {
      return (
        <div className="review-done">
          <p className="review-done-title">Session complete — {reviewed} review{reviewed === 1 ? '' : 's'}</p>
          <div className="review-tally">
            {GRADES.map(g => (
              <span key={g.value} className={`review-tally-item review-grade--${g.label.toLowerCase()}`}>
                {g.label} · {tally[g.value] || 0}
              </span>
            ))}
          </div>
          <div className="review-actions">
            <button className="review-primary" onClick={onClose}>Back to notes</button>
          </div>
        </div>
      )
    }

    return (
      <>
        <div className="review-card">
          <div className="review-note-title">
            {current.title || '(untitled)'}
            {current.isNew && <span className="review-new-badge">new</span>}
          </div>
          <div className="review-front">{card ? card.front : 'What do you remember about this note?'}</div>
          {revealed && (
            <div className="review-back">
              {card ? card.back : noteText === null ? 'Loading note…' : (noteText || 'This note is empty.')}
            </div>
          )}
        </div>
        {revealed ? (
          <div className="review-grades">
            {GRADES.map(g => (
              <button
                key={g.value}
                className={`review-grade review-grade--${g.label.toLowerCase()}`}
                onClick={() => grade(g.value)}
                disabled={submitting}
              >
                {g.label}<kbd>{g.key}</kbd>
              </button>
            ))}
          </div>
        ) : (
          <div className="review-actions">
            <span className="review-hint"><kbd>Space</kbd> to reveal · <kbd>1</kbd>–<kbd>4</kbd> to grade · <kbd>Esc</kbd> to leave</span>
            <button className="review-primary" onClick={() => setRevealed(true)}>Show answer</button>
          </div>
        )}
      </>
    )
  }

  return (
    <div className="review-screen" ref={screenRef} tabIndex={-1} onKeyDown={onKeyDown} aria-label="Review session">
      <div className="review-toolbar">
        <span className="review-title">{single ? `Review · ${single.title || '(untitled)'}` : 'Review'}</span>
        {queue && total > 0 && !done && <span className="review-progress">{index + 1} / {total}</span>}
        <button className="btn-ghost review-exit" onClick={onClose}>Exit review</button>
      </div>
      <div className="review-stage">
        {last && !done && (
          <p className="review-last">“{last.title || '(untitled)'}” — next review {formatDue(last.dueAt)}</p>
        )}
        {renderBody()}
      </div>
    </div>
  )
}
