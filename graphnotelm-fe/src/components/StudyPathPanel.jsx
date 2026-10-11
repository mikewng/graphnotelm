import { useEffect, useMemo, useRef, useState } from 'react'
import SidePanel from './SidePanel'
import SearchSelect from './SearchSelect'
import { analysisApi } from '../api'
import { STUDY_MODES, buildStudyHighlight, loadStudySettings, saveStudySettings } from '../utils/studyPath'
import { displayConfidence } from '../utils/review'

const DRAFT_ID = '__draft__'

const MODE_HINTS = {
  order:       'Everything to learn before the goal, each note after its prerequisites.',
  frontier:    'Notes you already know from the start note, and the ones just past them to learn next.',
  path:        'The route from start to target through the notes you know least.',
  bottlenecks: 'Weak notes that the most other notes build on. Firming these up unblocks the most.',
  ready:       'Notes below the threshold whose prerequisites you already know: what you can start on now.',
}

// Modes that read relationships as prerequisite links, one way.
const PREREQUISITE_MODES = new Set(['order', 'bottlenecks', 'ready'])

const FOLLOW_OPTIONS = [
  { id: 'Both',     label: 'Both ways' },
  { id: 'Outgoing', label: 'Forward' },
  { id: 'Incoming', label: 'Backward' },
]

function NodeField({ label, value, onChange, options, selectedNodeId }) {
  const canUseSelected = selectedNodeId && selectedNodeId !== value && options.some(o => o.id === selectedNodeId)
  return (
    <div className="study-field">
      <div className="study-field-label-row">
        <label>{label}</label>
        {canUseSelected && (
          <button className="study-link-btn" onClick={() => onChange(selectedNodeId)}>Use selected</button>
        )}
      </div>
      <SearchSelect value={value} onChange={onChange} options={options} placeholder="Search notes…" />
    </div>
  )
}

function Segmented({ options, value, onChange }) {
  return (
    <div className="study-segmented">
      {options.map(o => (
        <button key={o.id} className={value === o.id ? '' : 'btn-ghost'} onClick={() => onChange(o.id)}>
          {o.label}
        </button>
      ))}
    </div>
  )
}

export default function StudyPathPanel({
  isOpen, onClose, graphId,
  nodesList, relTypesList, selectedNodeId,
  onNavigate, onHighlightChange,
  showGraph, onShowGraph,
}) {
  const saved = useMemo(() => loadStudySettings(graphId), [graphId])
  const [mode, setMode] = useState(saved.mode || 'order')
  const [startId, setStartId] = useState(saved.startId || '')
  const [targetId, setTargetId] = useState(saved.targetId || '')
  const [relationshipId, setRelationshipId] = useState(saved.relationshipId || '')
  const [orderDirection, setOrderDirection] = useState(saved.orderDirection || 'Outgoing')
  const [followDirection, setFollowDirection] = useState(saved.followDirection || 'Both')
  const [minConfidence, setMinConfidence] = useState(saved.minConfidence ?? 3)
  // The input keeps its raw text so partial entries like "2." aren't rewritten while typing.
  const [minConfidenceText, setMinConfidenceText] = useState(String(minConfidence))

  const [result, setResult] = useState(null)   // { mode, data } — tagged so a stale mode never renders
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const requestSeq = useRef(0)

  useEffect(() => {
    saveStudySettings(graphId, { mode, startId, targetId, relationshipId, orderDirection, followDirection, minConfidence })
  }, [graphId, mode, startId, targetId, relationshipId, orderDirection, followDirection, minConfidence])

  const nodeOptions = useMemo(() => nodesList
    .filter(n => n.id !== DRAFT_ID)
    .map(n => ({ id: n.id, label: n.title || '(untitled)' })), [nodesList])
  const titleById = useMemo(() => new Map(nodeOptions.map(o => [o.id, o.label])), [nodeOptions])

  // Saved picks can outlive their notes or relationship types — treat those as unset.
  const start = titleById.has(startId) ? startId : ''
  const target = titleById.has(targetId) ? targetId : ''
  const relationship = relTypesList.some(r => r.id === relationshipId) ? relationshipId : ''
  const relName = relTypesList.find(r => r.id === relationship)?.name

  // Bottlenecks and ready-to-learn look at the whole graph, so they need neither note.
  const needsStart = mode === 'frontier' || mode === 'path'
  const needsTarget = mode === 'order' || mode === 'path'

  // Fill the field the current mode starts from with the selected note when the panel opens.
  useEffect(() => {
    if (!isOpen || !titleById.has(selectedNodeId)) return
    if (mode === 'order' && !target) setTargetId(selectedNodeId)
    if (needsStart && !start) setStartId(selectedNodeId)
  }, [isOpen, mode]) // eslint-disable-line react-hooks/exhaustive-deps

  // Re-run when edges or confidence change (a self-rating edit or a review); title edits are
  // applied from titleById instead.
  const refreshKey = useMemo(() => isOpen ? JSON.stringify(nodesList.map(n => [
    n.id,
    displayConfidence(n),
    (n.relationships || []).map(r => `${r.targetNodeId}:${r.relationshipId}`).join(','),
  ])) : '', [isOpen, nodesList])

  const ready = (!needsStart || !!start) && (!needsTarget || !!target)

  useEffect(() => {
    if (!isOpen || !ready) {
      requestSeq.current++
      setResult(null)
      setError('')
      setLoading(false)
      return
    }
    const seq = ++requestSeq.current
    const relationshipIds = relationship ? [relationship] : []
    // Debounced so typing a confidence threshold doesn't fire a request per keystroke.
    const timer = setTimeout(() => {
      setLoading(true)
      const calls = {
        order:       () => analysisApi.learningOrder(graphId, { targetNodeId: target, direction: orderDirection, relationshipIds }),
        frontier:    () => analysisApi.frontier(graphId, { startNodeId: start, minConfidence, direction: followDirection, relationshipIds }),
        path:        () => analysisApi.weakestPath(graphId, { startNodeId: start, targetNodeId: target, direction: followDirection, relationshipIds }),
        bottlenecks: () => analysisApi.bottlenecks(graphId, { direction: orderDirection, relationshipIds }),
        ready:       () => analysisApi.readyToLearn(graphId, { minConfidence, direction: orderDirection, relationshipIds }),
      }
      calls[mode]()
        .then(data => { if (seq === requestSeq.current) { setResult({ mode, data }); setError('') } })
        .catch(err => { if (seq === requestSeq.current) { setResult(null); setError(err.message) } })
        .finally(() => { if (seq === requestSeq.current) setLoading(false) })
    }, 200)
    return () => clearTimeout(timer)
  }, [isOpen, ready, mode, graphId, start, target, relationship, orderDirection, followDirection, minConfidence, refreshKey])

  const highlight = useMemo(
    () => result ? buildStudyHighlight(result.mode, result.data, nodesList, relationship) : null,
    [result, nodesList, relationship],
  )
  useEffect(() => {
    onHighlightChange?.(isOpen ? highlight : null)
  }, [isOpen, highlight]) // eslint-disable-line react-hooks/exhaustive-deps

  function changeMode(next) {
    if (next === mode) return
    setResult(null)
    setMode(next)
  }

  const titleOf = n => titleById.get(n.id) ?? n.title ?? '(untitled)'
  const through = relName ? ` through "${relName}"` : ''

  function renderItem(n, step, extraClass = '', detail = null) {
    const rate = n.confidence
    return (
      <li key={n.id}>
        <button
          className={`study-item${n.id === selectedNodeId ? ' active' : ''}${extraClass ? ` ${extraClass}` : ''}`}
          onClick={() => onNavigate(n.id)}
        >
          {step != null && <span className="study-step">{step}</span>}
          <span className="study-item-title">{titleOf(n)}</span>
          {detail}
          <span className="study-conf" title="Confidence">{rate > 0 ? rate : '–'}</span>
        </button>
      </li>
    )
  }

  function renderResult() {
    const { mode: resultMode, data } = result
    if (resultMode === 'order') {
      const inCycle = new Set(data.cycles.flat().map(n => n.id))
      const itemClass = n => n.id === target ? 'study-item--goal' : inCycle.has(n.id) ? 'study-item--warn' : ''
      return (
        <>
          {data.order.length === 1 && data.cycles.length === 0 && (
            <p className="study-empty">Nothing to learn first{through} — this note stands on its own.</p>
          )}
          {data.order.length > 0 && (
            <ol className="study-list">
              {data.order.map((n, i) => renderItem(n, i + 1, itemClass(n)))}
            </ol>
          )}
          {data.cycles.length > 0 && (
            <>
              <p className="study-warning">
                Notes in red are prerequisites of each other, so each loop is kept together above. Remove an edge in a loop to order it properly.
              </p>
              <ul className="study-loops">
                {data.cycles.map(cycle => (
                  <li key={cycle[0].id}>
                    {cycle.length === 1 ? `${titleOf(cycle[0])} is its own prerequisite` : cycle.map(titleOf).join(', ')}
                  </li>
                ))}
              </ul>
            </>
          )}
        </>
      )
    }

    if (resultMode === 'bottlenecks') {
      if (data.bottlenecks.length === 0) {
        return <p className="study-empty">No weak note has other notes depending on it{through}.</p>
      }
      return (
        <ol className="study-list">
          {data.bottlenecks.map((n, i) => renderItem(n, i + 1, '',
            <span className="study-detail" title="Notes that depend on this one">blocks {n.dependents}</span>))}
        </ol>
      )
    }

    if (resultMode === 'ready') {
      return (
        <>
          <div className="study-section-label">Ready to learn · {data.ready.length}</div>
          {data.ready.length === 0
            ? <p className="study-empty">Nothing waiting: every linked note{through} is at or above {minConfidence}.</p>
            : <ul className="study-list">{data.ready.map(n => renderItem(n))}</ul>}
        </>
      )
    }

    if (resultMode === 'frontier') {
      return (
        <>
          <div className="study-section-label">Learn next · {data.frontier.length}</div>
          {data.frontier.length === 0
            ? <p className="study-empty">Every connected note is at or above {minConfidence}.</p>
            : <ul className="study-list">{data.frontier.map(n => renderItem(n))}</ul>}
          <div className="study-section-label">Already known · {data.known.length}</div>
          {data.known.length === 0
            ? <p className="study-empty">The start note is below {minConfidence}, so start there.</p>
            : <ul className="study-list">{data.known.map(n => renderItem(n, null, 'study-item--muted'))}</ul>}
        </>
      )
    }

    if (data.path.length === 0) {
      return <p className="study-empty">No route between these notes with the current relationship and direction.</p>
    }
    return <ol className="study-list">{data.path.map((n, i) => renderItem(n, i + 1))}</ol>
  }

  const prompt = mode === 'order' ? 'Pick a goal note.' : mode === 'frontier' ? 'Pick a start note.' : 'Pick a start and a target note.'
  const firstLabel = orderDirection === 'Outgoing' ? 'A' : 'B'
  const orderHint = relName
    ? `"A ${relName} B" → learn ${firstLabel} first`
    : `Every edge A → B → learn ${firstLabel} first`

  const headerActions = !showGraph && highlight
    ? <button className="btn-ghost" onClick={onShowGraph}>Show on graph</button>
    : null

  return (
    <SidePanel
      title="Study Path"
      onClose={onClose}
      isOpen={isOpen}
      defaultWidth={340}
      headerActions={headerActions}
      className="study-panel"
    >
      <div className="study-tabs" role="tablist">
        {STUDY_MODES.map(m => (
          <button
            key={m.id}
            role="tab"
            aria-selected={mode === m.id}
            className={`study-tab${mode === m.id ? ' active' : ''}`}
            onClick={() => changeMode(m.id)}
          >
            {m.label}
          </button>
        ))}
      </div>
      <p className="study-mode-hint">{MODE_HINTS[mode]}</p>

      <div className="study-fields">
        {needsStart && (
          <NodeField label="Start" value={start} onChange={setStartId} options={nodeOptions} selectedNodeId={selectedNodeId} />
        )}
        {needsTarget && (
          <NodeField label={mode === 'order' ? 'Goal' : 'Target'} value={target} onChange={setTargetId} options={nodeOptions} selectedNodeId={selectedNodeId} />
        )}
        <div className="study-field">
          <label>Relationship</label>
          <select value={relationship} onChange={e => setRelationshipId(e.target.value)}>
            <option value="">All relationships</option>
            {relTypesList.map(r => <option key={r.id} value={r.id}>{r.name}</option>)}
          </select>
        </div>
        {PREREQUISITE_MODES.has(mode) ? (
          <div className="study-field">
            <label>Learn first</label>
            <Segmented
              options={[{ id: 'Outgoing', label: 'Source' }, { id: 'Incoming', label: 'Target' }]}
              value={orderDirection}
              onChange={setOrderDirection}
            />
            <span className="study-field-hint">{orderHint}</span>
          </div>
        ) : (
          <div className="study-field">
            <label>Follow edges</label>
            <Segmented options={FOLLOW_OPTIONS} value={followDirection} onChange={setFollowDirection} />
          </div>
        )}
        {(mode === 'frontier' || mode === 'ready') && (
          <div className="study-field">
            <label>Known at confidence ≥</label>
            <input
              type="number"
              min={0}
              max={10}
              step={0.5}
              value={minConfidenceText}
              onChange={e => {
                const text = e.target.value
                setMinConfidenceText(text)
                if (text !== '' && Number.isFinite(Number(text))) setMinConfidence(Number(text))
              }}
            />
          </div>
        )}
      </div>

      <div className="study-results">
        {error
          ? <p className="study-error">{error}</p>
          : !ready
            ? <p className="study-empty">{prompt}</p>
            : result
              ? renderResult()
              : loading && <p className="study-empty">Working it out…</p>}
      </div>
    </SidePanel>
  )
}
