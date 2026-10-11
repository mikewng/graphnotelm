// Shared between the study path panel and the graph view.

export const STUDY_MODES = [
  { id: 'order',       label: 'Order' },
  { id: 'frontier',    label: 'Frontier' },
  { id: 'path',        label: 'Weakest path' },
  { id: 'bottlenecks', label: 'Bottlenecks' },
  { id: 'ready',       label: 'Ready' },
]

export const edgeKey = (sourceId, targetId, relationshipId) => `${sourceId}>${targetId}>${relationshipId}`

/**
 * Turns an analysis result into what the graph view draws:
 *   roles — node id → 'primary' (the answer), 'secondary' (context) or 'warn' (in a cycle)
 *   steps — node id → 1-based position, for results that are an ordered list
 *   edges — edgeKey()s of the relationships that connect highlighted nodes
 * Returns null when there is nothing to highlight.
 */
export function buildStudyHighlight(mode, result, nodes, relationshipId = '') {
  if (!result) return null
  const roles = new Map()
  const steps = new Map()

  if (mode === 'order') {
    result.order.forEach((n, i) => { roles.set(n.id, 'primary'); steps.set(n.id, i + 1) })
    // Cycle members keep their place in the order but are flagged.
    result.cycles.flat().forEach(n => roles.set(n.id, 'warn'))
  } else if (mode === 'frontier') {
    result.known.forEach(n => roles.set(n.id, 'secondary'))
    result.frontier.forEach(n => roles.set(n.id, 'primary'))
  } else if (mode === 'path') {
    result.path.forEach((n, i) => { roles.set(n.id, 'primary'); steps.set(n.id, i + 1) })
  } else if (mode === 'bottlenecks') {
    result.bottlenecks.forEach((n, i) => { roles.set(n.id, 'primary'); steps.set(n.id, i + 1) })
  } else if (mode === 'ready') {
    result.ready.forEach(n => roles.set(n.id, 'primary'))
  }
  if (roles.size === 0) return null

  const edges = new Set()
  for (const node of nodes) {
    if (!roles.has(node.id)) continue
    for (const r of node.relationships || []) {
      const targetId = r.targetNodeId
      if (!roles.has(targetId)) continue
      if (relationshipId && r.relationshipId !== relationshipId) continue
      // A path only uses edges between neighbouring steps; a frontier only grows out of known nodes.
      if (mode === 'path' && Math.abs(steps.get(node.id) - steps.get(targetId)) !== 1) continue
      if (mode === 'frontier' && roles.get(node.id) !== 'secondary' && roles.get(targetId) !== 'secondary') continue
      edges.add(edgeKey(node.id, targetId, r.relationshipId))
    }
  }

  return { roles, steps, edges }
}

// Per-graph panel settings — a per-viewer convenience, so storage failures are ignored.
const SETTINGS_PREFIX = 'graphnotelm:studyPath:'

export function loadStudySettings(graphId) {
  try {
    return JSON.parse(localStorage.getItem(SETTINGS_PREFIX + graphId)) || {}
  } catch {
    return {}
  }
}

export function saveStudySettings(graphId, settings) {
  try {
    localStorage.setItem(SETTINGS_PREFIX + graphId, JSON.stringify(settings))
  } catch { /* storage unavailable */ }
}
