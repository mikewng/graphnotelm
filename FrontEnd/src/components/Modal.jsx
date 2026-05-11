import { useEffect, useRef, useState } from 'react'

// field shapes:
//   { key, label?, placeholder?, defaultValue?, required?,
//     fieldType?: 'text'|'color'|'toggle',
//     visibleWhen?: (values) => boolean }
export default function Modal({ modal, onCancel }) {
  const [values, setValues] = useState({})
  const firstInputRef = useRef(null)

  useEffect(() => {
    if (!modal) return
    if (modal.fields) {
      const init = {}
      modal.fields.forEach(f => {
        if (f.fieldType === 'color') init[f.key] = f.defaultValue || '#888888'
        else if (f.fieldType === 'toggle') init[f.key] = f.defaultValue === true
        else init[f.key] = f.defaultValue || ''
      })
      setValues(init)
    } else {
      setValues({ _single: modal.defaultValue || '' })
    }
    setTimeout(() => firstInputRef.current?.focus(), 50)
  }, [modal])

  if (!modal) return null

  function isVisible(f) {
    return !f.visibleWhen || f.visibleWhen(values)
  }

  function handleConfirm() {
    if (modal.type === 'input') {
      if (modal.fields) {
        const requiredFields = modal.fields.filter(f =>
          f.required !== false && f.fieldType !== 'color' && f.fieldType !== 'toggle' && isVisible(f)
        )
        if (requiredFields.some(f => !values[f.key]?.trim())) return
        modal.onConfirm(Object.fromEntries(
          Object.entries(values).map(([k, v]) => [k, typeof v === 'string' ? v.trim() : v])
        ))
      } else {
        if (!values._single?.trim()) return
        modal.onConfirm(values._single.trim())
      }
    } else {
      modal.onConfirm()
    }
    onCancel()
  }

  function handleKey(e) {
    if (e.key === 'Enter' && e.target.tagName !== 'TEXTAREA') handleConfirm()
    if (e.key === 'Escape') onCancel()
  }

  const confirmLabel = modal.confirmLabel || (modal.type === 'confirm' ? 'Confirm' : 'Create')

  return (
    <div className="modal-backdrop" onClick={e => { if (e.target === e.currentTarget) onCancel() }}>
      <div className="modal-box" onKeyDown={handleKey}>
        <h3 className="modal-title">{modal.title}</h3>
        {modal.message && <p className="modal-message">{modal.message}</p>}

        {modal.type === 'input' && modal.fields && modal.fields.map((f, i) => {
          if (!isVisible(f)) return null
          const isFirstText = modal.fields.findIndex(x => !x.fieldType || x.fieldType === 'text') === i
          return (
            <div key={f.key} className="modal-field">
              {f.label && (
                <label className="modal-field-label">
                  {f.label}
                  {f.required === false && f.fieldType !== 'toggle' && f.fieldType !== 'color' && (
                    <span className="modal-optional"> — optional</span>
                  )}
                </label>
              )}
              {f.fieldType === 'color' && (
                <div className="modal-color-row">
                  <input
                    type="color"
                    className="modal-color-input"
                    value={values[f.key] || '#888888'}
                    autoComplete="off"
                    onChange={e => setValues(prev => ({ ...prev, [f.key]: e.target.value }))}
                  />
                  <span className="modal-color-value">{values[f.key] || '#888888'}</span>
                </div>
              )}
              {f.fieldType === 'toggle' && (
                <button
                  type="button"
                  className={`modal-toggle${values[f.key] ? ' on' : ''}`}
                  onClick={() => setValues(prev => ({ ...prev, [f.key]: !prev[f.key] }))}
                >
                  <span className="modal-toggle-thumb" />
                </button>
              )}
              {(!f.fieldType || f.fieldType === 'text') && (
                <input
                  ref={isFirstText ? firstInputRef : undefined}
                  className="modal-input"
                  placeholder={f.placeholder || ''}
                  value={values[f.key] || ''}
                  autoComplete="off"
                  onChange={e => setValues(prev => ({ ...prev, [f.key]: e.target.value }))}
                />
              )}
            </div>
          )
        })}

        {modal.type === 'input' && !modal.fields && (
          <input
            ref={firstInputRef}
            className="modal-input"
            placeholder={modal.placeholder || ''}
            value={values._single || ''}
            autoComplete="off"
            onChange={e => setValues({ _single: e.target.value })}
          />
        )}

        <div className="modal-actions">
          <button className="btn-ghost" onClick={onCancel}>Cancel</button>
          <button
            className={modal.danger ? 'btn-danger' : ''}
            onClick={handleConfirm}
          >{confirmLabel}</button>
        </div>
      </div>
    </div>
  )
}
