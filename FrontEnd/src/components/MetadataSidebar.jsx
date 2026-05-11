import { useState, useEffect } from 'react'

export default function MetadataSidebar({ node, isOpen, onToggle, onSave }) {
  const [editing, setEditing] = useState(false)
  const [confidenceRate, setConfidenceRate] = useState('')
  const [llmMetadata, setLlmMetadata] = useState('')
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    setEditing(false)
    setConfidenceRate(node?.metadata?.userConfidenceRate ?? '')
    setLlmMetadata(node?.metadata?.llmMetadata ?? '')
  }, [node?.id])

  function handleCancel() {
    setEditing(false)
    setConfidenceRate(node?.metadata?.userConfidenceRate ?? '')
    setLlmMetadata(node?.metadata?.llmMetadata ?? '')
  }

  async function handleSave() {
    setSaving(true)
    try {
      await onSave({
        userConfidenceRate: confidenceRate !== '' ? Number(confidenceRate) : null,
        llmMetadata: llmMetadata || null,
      })
      setEditing(false)
    } catch (_) {
    } finally {
      setSaving(false)
    }
  }

  if (!isOpen) return null

  const metadata = node?.metadata

  return (
    <div className="metadata-sidebar">
      <div className="metadata-panel-header">
        <span>Metadata</span>
        <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
          {node && !editing && (
            <button className="btn-ghost" onClick={() => setEditing(true)}>Edit</button>
          )}
          <button className="btn-ghost" onClick={onToggle} title="Close">×</button>
        </div>
      </div>

      <div className="metadata-panel">
        {!node ? (
          <p className="metadata-empty">Select a node to view metadata.</p>
        ) : (
          <div className="metadata-fields">
            <div className="metadata-field">
              <label>Confidence Rate</label>
              {editing ? (
                <input
                  type="number"
                  min="0"
                  max="1"
                  step="0.01"
                  value={confidenceRate}
                  onChange={e => setConfidenceRate(e.target.value)}
                  placeholder="0.0 – 1.0"
                />
              ) : (
                <span className="metadata-value">
                  {metadata?.userConfidenceRate != null ? metadata.userConfidenceRate : <em className="metadata-nil">—</em>}
                </span>
              )}
            </div>

            <div className="metadata-field">
              <label>LLM Metadata</label>
              {editing ? (
                <textarea
                  value={llmMetadata}
                  onChange={e => setLlmMetadata(e.target.value)}
                  placeholder="LLM-generated metadata..."
                  rows={8}
                />
              ) : (
                <div className="metadata-value metadata-value--text">
                  {metadata?.llmMetadata || <em className="metadata-nil">—</em>}
                </div>
              )}
            </div>

            {editing && (
              <div className="metadata-actions">
                <button onClick={handleCancel}>Cancel</button>
                <button onClick={handleSave} disabled={saving}>
                  {saving ? 'Saving…' : 'Save'}
                </button>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  )
}
