import { useState, useEffect } from 'react'
import SidePanel from './SidePanel'

export default function MetadataSidebar({ node, isOpen, onToggle, onSave, onGenerate }) {
  const [editing, setEditing] = useState(false)
  const [confidenceRate, setConfidenceRate] = useState('')
  const [llmMetadata, setLlmMetadata] = useState('')
  const [saving, setSaving] = useState(false)
  const [generating, setGenerating] = useState(false)
  const [prettyJson, setPrettyJson] = useState(false)

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

  async function handleGenerate() {
    setGenerating(true)
    try {
      await onGenerate()
    } catch (_) {
    } finally {
      setGenerating(false)
    }
  }

  const metadata = node?.metadata

  const headerActions = node && !editing
    ? <button className="btn-ghost" onClick={() => setEditing(true)}>Edit</button>
    : null

  return (
    <SidePanel
      title="Metadata"
      onClose={onToggle}
      isOpen={isOpen}
      defaultWidth={320}
      headerActions={headerActions}
    >
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
                max="10"
                step="0.1"
                value={confidenceRate}
                onChange={e => setConfidenceRate(e.target.value)}
                placeholder="0 – 10"
              />
            ) : (
              <span className="metadata-value">
                {metadata?.userConfidenceRate != null ? metadata.userConfidenceRate : <em className="metadata-nil">—</em>}
              </span>
            )}
          </div>

          <div className="metadata-field">
            <div className="metadata-field-label-row">
              <label>LLM Metadata</label>
              <div style={{ display: 'flex', gap: 5, alignItems: 'center' }}>
                {!editing && metadata?.llmMetadata && (
                  <button
                    className={`btn-pretty-toggle${prettyJson ? ' active' : ''}`}
                    onClick={() => setPrettyJson(v => !v)}
                    title="Toggle pretty JSON"
                  >
                    {'{ }'}
                  </button>
                )}
                <button
                  className="btn-generate"
                  onClick={handleGenerate}
                  disabled={generating}
                  title="Generate with AI"
                >
                  {generating
                    ? <><span className="generate-spinner" /> Generating…</>
                    : '✦ Generate'
                  }
                </button>
              </div>
            </div>
            {editing ? (
              <textarea
                value={llmMetadata}
                onChange={e => setLlmMetadata(e.target.value)}
                placeholder="LLM-generated metadata..."
                rows={8}
              />
            ) : (
              <div className={`metadata-value metadata-value--text${prettyJson ? ' metadata-value--code' : ''}`}>
                {metadata?.llmMetadata
                  ? prettyJson
                    ? (() => {
                        try { return JSON.stringify(JSON.parse(metadata.llmMetadata), null, 2) }
                        catch { return metadata.llmMetadata }
                      })()
                    : metadata.llmMetadata
                  : <em className="metadata-nil">—</em>
                }
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
    </SidePanel>
  )
}
