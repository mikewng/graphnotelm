// Front and back inputs for adding or editing a flashcard; Ctrl/Cmd+Enter submits.
export default function FlashcardForm({ front, back, onFront, onBack, onSubmit, submitLabel, onCancel, busy, autoFocus = false }) {
  const canSubmit = front.trim() && back.trim() && !busy
  function onKeyDown(e) {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey) && canSubmit) {
      e.preventDefault()
      onSubmit()
    }
    if (e.key === 'Escape' && onCancel) onCancel()
  }
  return (
    <div className="flashcard-form">
      <textarea value={front} onChange={e => onFront(e.target.value)} onKeyDown={onKeyDown}
        placeholder="Front — the question" rows={2} autoFocus={autoFocus} />
      <textarea value={back} onChange={e => onBack(e.target.value)} onKeyDown={onKeyDown}
        placeholder="Back — the answer" rows={2} />
      <div className="flashcard-form-actions">
        {onCancel && <button className="btn-ghost" onClick={onCancel} disabled={busy}>Cancel</button>}
        <button className="btn-outline" onClick={onSubmit} disabled={!canSubmit}>{submitLabel}</button>
      </div>
    </div>
  )
}
