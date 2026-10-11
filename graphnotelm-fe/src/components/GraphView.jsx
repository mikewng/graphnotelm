import { useEffect, useMemo, useRef, useState, forwardRef, useImperativeHandle } from 'react'
import * as d3 from 'd3'
import GraphViewContextMenu from './GraphViewToolBar'
import { edgeKey } from '../utils/studyPath'
import { displayConfidence } from '../utils/review'

const NODE_R = 8
const NODE_R_MAX = 26
const NODE_R_GROWTH = 3      // px added per √(edges beyond NODE_R_FREE_EDGES)
const NODE_R_FREE_EDGES = 2  // nodes with this many edges or fewer keep the base size
const LINK_DISTANCE = 240
const RADIAL_RING_SPACING = 180
const COMPACT_R = 5
const COMPACT_R_MAX = 14
const COMPACT_R_GROWTH = 1.8
const COMPACT_LINK_DISTANCE = 90

// Distance-based seed layout (stress majorization): places nodes so on-screen
// distance ≈ shortest-path distance × linkDistance. Started from this, the
// force simulation only has to polish spacing instead of unfolding a random
// tangle, which is what produced heavy edge crossings. Returns positions
// indexed like nodeData (components are laid out independently around 0,0),
// or null when the graph is too large for the O(n²) passes.
function computeStressSeed(nodeData, linkData, linkDistance) {
  const n = nodeData.length
  if (n < 3 || n > 400) return null
  const idToIdx = new Map(nodeData.map((d, i) => [d.id, i]))
  const adj = Array.from({ length: n }, () => [])
  linkData.forEach(l => {
    const s = idToIdx.get(l.source?.id ?? l.source)
    const t = idToIdx.get(l.target?.id ?? l.target)
    if (s == null || t == null || s === t) return
    adj[s].push(t)
    adj[t].push(s)
  })

  // All-pairs shortest paths via BFS from every node (-1 = unreachable)
  const dist = []
  for (let s = 0; s < n; s++) {
    const d = new Int32Array(n).fill(-1)
    d[s] = 0
    const q = [s]
    for (let qi = 0; qi < q.length; qi++) {
      const cur = q[qi]
      for (const nb of adj[cur]) {
        if (d[nb] === -1) { d[nb] = d[cur] + 1; q.push(nb) }
      }
    }
    dist.push(d)
  }

  // Deterministic non-degenerate start: phyllotaxis spiral
  const golden = 2.399963229728653
  const pos = nodeData.map((_, i) => ({
    x: Math.cos(i * golden) * linkDistance * 0.5 * Math.sqrt(i + 1),
    y: Math.sin(i * golden) * linkDistance * 0.5 * Math.sqrt(i + 1),
  }))

  const ITERATIONS = 150
  for (let it = 0; it < ITERATIONS; it++) {
    for (let i = 0; i < n; i++) {
      const di = dist[i]
      let wx = 0, wy = 0, wsum = 0
      for (let j = 0; j < n; j++) {
        if (j === i || di[j] <= 0) continue
        const target = di[j] * linkDistance
        const w = 1 / (target * target)
        const dx = pos[i].x - pos[j].x
        const dy = pos[i].y - pos[j].y
        const len = Math.hypot(dx, dy) || 1e-6
        wx += w * (pos[j].x + (dx / len) * target)
        wy += w * (pos[j].y + (dy / len) * target)
        wsum += w
      }
      if (wsum > 0) { pos[i].x = wx / wsum; pos[i].y = wy / wsum }
    }
  }
  return pos
}

// Three stops joined by a winding route — used by the Study Path toggles.
export function StudyPathIcon() {
  return (
    <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" style={{ width: 13, height: 13, flexShrink: 0 }}>
      <circle cx="3.5" cy="3.5" r="1.75" /><circle cx="12.5" cy="8" r="1.75" /><circle cx="3.5" cy="12.5" r="1.75" />
      <path d="M5.25 3.5H9a2.25 2.25 0 010 4.5H5.5a2.25 2.25 0 000 4.5" />
    </svg>
  )
}

// nodes: [{id, title, relationships: [{targetNodeId, relationshipId}], tags: [tagId]}]
// tagDefs: {[tagId]: {name, color}}
// relDefs: {[relId]: {name, color}}
const GraphView = forwardRef(function GraphView({
  nodes, tagDefs, relDefs = {}, selectedNodeId,
  onClose, onSelectNode, onSelectNodeSplit, showChat, onToggleChat,
  showStudyPath, onToggleStudyPath, studyHighlight = null,
  tagsList: tagsListProp, relTypesList,
  onEditAddRelationship, onEditAddTag, onEditDeleteNode, onEditDeleteEdge, onEditQuickEdit, onCreateNote,
  onEditChangeRelType,
  renderSplitPanel,
  pinnedNodeIds = new Set(),
  onHighlightNode,
}, ref) {
  const svgRef = useRef(null)
  const [tagFilter, setTagFilter] = useState(new Set())
  const [layoutMode, setLayoutMode] = useState(null) // null | 'hierarchical' | 'pinnedTop' | 'radial' | 'tagClusters'
  const [egoView, setEgoView] = useState(false)
  const [egoHops, setEgoHops] = useState(1)
  const [searchQuery, setSearchQuery] = useState('')
  const [tagDropdownOpen, setTagDropdownOpen] = useState(false)
  const [legendOpen, setLegendOpen] = useState(false)
  const [compactMode, setCompactMode] = useState(false)
  const tagDropdownRef = useRef(null)

  // ── Context menu + pending relationship ───────────────────────────────────
  const [contextMenu, setContextMenu] = useState(null)   // { x, y, nodeId }
  const [pendingRel, setPendingRel] = useState(null)      // { sourceNodeId, relTypeId }
  const pendingRelRef = useRef(null)
  const contextMenuRef = useRef(null)
  const nodePositionsRef = useRef({})                     // persists node positions across reloads
  const zoomTransformRef = useRef(null)                    // persists pan/zoom across re-renders

  useImperativeHandle(ref, () => ({
    seedPosition: (nodeId, x, y) => { nodePositionsRef.current[nodeId] = { x, y } },
  }))

  // ── Split view ────────────────────────────────────────────────────────────
  const [splitMode, setSplitMode] = useState(false)
  const splitModeRef = useRef(false)
  useEffect(() => { splitModeRef.current = splitMode }, [splitMode])
  const [splitWidth, setSplitWidth] = useState(55)  // percent of overlay for the graph
  const splitWidthRef = useRef(55)
  useEffect(() => { splitWidthRef.current = splitWidth }, [splitWidth])
  const splitDragging = useRef(false)
  const splitStartX = useRef(0)
  const splitStartW = useRef(0)
  const splitOverlayRef = useRef(null)

  function onSplitDividerMouseDown(e) {
    e.preventDefault()
    splitDragging.current = true
    splitStartX.current = e.clientX
    splitStartW.current = splitWidth
    document.body.style.cursor = 'col-resize'
    document.body.style.userSelect = 'none'
  }

  useEffect(() => {
    function onMouseMove(e) {
      if (!splitDragging.current || !splitOverlayRef.current) return
      const overlayW = splitOverlayRef.current.getBoundingClientRect().width
      const delta = ((e.clientX - splitStartX.current) / overlayW) * 100
      setSplitWidth(Math.min(80, Math.max(20, splitStartW.current + delta)))
    }
    function onMouseUp() {
      if (!splitDragging.current) return
      splitDragging.current = false
      document.body.style.cursor = ''
      document.body.style.userSelect = ''
    }
    window.addEventListener('mousemove', onMouseMove)
    window.addEventListener('mouseup', onMouseUp)
    return () => {
      window.removeEventListener('mousemove', onMouseMove)
      window.removeEventListener('mouseup', onMouseUp)
    }
  }, [])
  useEffect(() => { pendingRelRef.current = pendingRel }, [pendingRel])

  // Capture-phase close — fires before D3, but skips clicks inside the menu
  useEffect(() => {
    if (!contextMenu) return
    function onDown(e) {
      if (contextMenuRef.current && contextMenuRef.current.contains(e.target)) return
      setContextMenu(null)
    }
    document.addEventListener('mousedown', onDown, { capture: true })
    return () => document.removeEventListener('mousedown', onDown, { capture: true })
  }, [contextMenu])

  useEffect(() => {
    function onMouseDown(e) {
      if (tagDropdownRef.current && !tagDropdownRef.current.contains(e.target))
        setTagDropdownOpen(false)
    }
    document.addEventListener('mousedown', onMouseDown)
    return () => document.removeEventListener('mousedown', onMouseDown)
  }, [])

  function toggleTag(id) {
    setTagFilter(prev => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  const tagsList = Object.entries(tagDefs || {}).map(([id, t]) => ({ ...t, id }))

  // Structural signature of the graph: node ids, edges, tags, pins, and the
  // tag/relationship definitions (colors feed node/link styling). Content-only
  // changes (title edits, autosaves, metadata) leave this string identical, so
  // the expensive rebuild effect below is skipped and the lightweight effect
  // updates labels in place instead of tearing down the simulation.
  const structureKey = useMemo(() => JSON.stringify([
    nodes.map(n => [
      n.id,
      (n.tags || []).join(','),
      (n.relationships || []).map(r => `${r.targetNodeId}:${r.relationshipId}`).join(','),
      pinnedNodeIds.has(n.id) ? 1 : 0,
    ]),
    tagDefs,
    relDefs,
  ]), [nodes, tagDefs, relDefs, pinnedNodeIds])

  // Latest nodes for the rebuild effect — reading through a ref keeps the
  // effect's dependency on `nodes` out of its trigger list (structureKey gates it).
  const nodesRef = useRef(nodes)
  nodesRef.current = nodes

  useEffect(() => {
    const nodes = nodesRef.current
    const svgEl = svgRef.current
    if (!svgEl) return

    const svg = d3.select(svgEl)

    // Save current node positions and zoom/pan transform before clearing
    svg.selectAll('.graph-node').each(function(d) {
      if (d?.id && d.x != null) nodePositionsRef.current[d.id] = { x: d.x, y: d.y }
    })
    zoomTransformRef.current = d3.zoomTransform(svgEl)

    svg.selectAll('*').remove()

    const width = svgEl.clientWidth
    const height = svgEl.clientHeight

    const cssVars = getComputedStyle(document.documentElement)
    const accent = cssVars.getPropertyValue('--accent').trim()
    const text2 = cssVars.getPropertyValue('--text-2').trim()

    const visibleNodes = nodes.filter(n =>
      tagFilter.size === 0 || (n.tags || []).some(t => tagFilter.has(t))
    )
    const visibleIds = new Set(visibleNodes.map(n => n.id))

    const nodeData = visibleNodes.map(n => {
      const saved = nodePositionsRef.current[n.id]
      return { ...n, x: saved?.x, y: saved?.y }
    })
    const linkData = visibleNodes.flatMap(n =>
      (n.relationships || [])
        .filter(r => visibleIds.has(r.targetNodeId))
        .map(r => ({ source: n.id, target: r.targetNodeId, relationshipId: r.relationshipId }))
    )

    // Fan out edges that share the same node pair (mutual A↔B or multiple
    // relationships) so they don't draw on top of each other. A pair with a
    // single edge keeps index 0 of 1 → drawn perfectly straight.
    const pairGroups = {}
    linkData.forEach(l => {
      const key = [l.source, l.target].sort().join(' ')
      ;(pairGroups[key] ||= []).push(l)
    })
    Object.values(pairGroups).forEach(group => {
      group.forEach((l, i) => { l._pairIndex = i; l._pairCount = group.length })
    })

    // Size each node by how many drawn edges touch it (incoming + outgoing).
    // The first few edges are free so ordinary chain links stay base size;
    // √ growth keeps hubs prominent without small differences jumping in size,
    // and the cap stops a heavily linked note from swallowing its neighbours.
    const degree = {}
    linkData.forEach(l => {
      degree[l.source] = (degree[l.source] || 0) + 1
      degree[l.target] = (degree[l.target] || 0) + 1
    })
    const [baseR, maxR, growthR] = compactMode
      ? [COMPACT_R, COMPACT_R_MAX, COMPACT_R_GROWTH]
      : [NODE_R, NODE_R_MAX, NODE_R_GROWTH]
    nodeData.forEach(n => {
      const extra = Math.max(0, (degree[n.id] || 0) - NODE_R_FREE_EDGES)
      n._r = Math.min(maxR, baseR + growthR * Math.sqrt(extra))
    })

    // Outgoing-edge map built once for the hover-highlight BFS (was previously
    // rebuilt from linkData on every mouseover).
    const hoverOutEdges = {}
    linkData.forEach(l => {
      const src = l.source?.id ?? l.source
      const tgt = l.target?.id ?? l.target
      ;(hoverOutEdges[src] ||= []).push(tgt)
    })

    const padding = 90
    const levels = {}
    if (layoutMode === 'hierarchical') {
      nodes.forEach(n => (levels[n.id] = 0))
      // Reverse adjacency built once — avoids rescanning every node's
      // relationships for every node on every iteration.
      const incoming = {}
      nodes.forEach(s => (s.relationships || []).forEach(r => {
        ;(incoming[r.targetNodeId] ||= []).push(s.id)
      }))
      for (let iter = 0; iter < nodes.length; iter++) {
        let changed = false
        nodes.forEach(n => {
          const sources = incoming[n.id] || []
          if (sources.length > 0) {
            const next = Math.max(...sources.map(sid => levels[sid] ?? 0)) + 1
            if (next > (levels[n.id] ?? 0)) { levels[n.id] = next; changed = true }
          }
        })
        if (!changed) break
      }
    } else if (layoutMode === 'pinnedTop') {
      nodeData.forEach(n => { levels[n.id] = pinnedNodeIds.has(n.id) ? 0 : 1 })
    }
    const maxLevel = (layoutMode === 'hierarchical' || layoutMode === 'pinnedTop')
      ? Math.max(...Object.values(levels), 0) : 0
    const levelSpacing = maxLevel > 0 ? (height - padding * 2) / maxLevel : 0

    // Radial: BFS distances from selected node (bidirectional)
    const radialDist = {}
    if (layoutMode === 'radial' && selectedNodeId) {
      const outAdj = {}, inAdj = {}
      nodeData.forEach(n => { outAdj[n.id] = []; inAdj[n.id] = [] })
      linkData.forEach(l => {
        const s = l.source?.id ?? l.source, t = l.target?.id ?? l.target
        outAdj[s]?.push(t); inAdj[t]?.push(s)
      })
      radialDist[selectedNodeId] = 0
      const bfsQ = [selectedNodeId]
      while (bfsQ.length) {
        const cur = bfsQ.shift()
        ;[...(outAdj[cur] || []), ...(inAdj[cur] || [])].forEach(nb => {
          if (radialDist[nb] == null) { radialDist[nb] = radialDist[cur] + 1; bfsQ.push(nb) }
        })
      }
      const maxDist = Math.max(...Object.values(radialDist), 0)
      nodeData.forEach(n => { if (radialDist[n.id] == null) radialDist[n.id] = maxDist + 1 })
    }

    // Tag clusters: cluster center positions evenly spaced around canvas center
    const clusterCenters = {}
    if (layoutMode === 'tagClusters') {
      const primaryTags = [...new Set(nodeData.map(n => (n.tags || [])[0]).filter(Boolean))]
      const hasUntagged = nodeData.some(n => !(n.tags || []).length)
      const allClusters = hasUntagged ? [...primaryTags, '__untagged__'] : primaryTags
      const cr = Math.min(width, height) * 0.3
      allClusters.forEach((tagId, i) => {
        const angle = (i / allClusters.length) * 2 * Math.PI - Math.PI / 2
        clusterCenters[tagId] = { x: width / 2 + cr * Math.cos(angle), y: height / 2 + cr * Math.sin(angle) }
      })
    }

    // Default view: keep disconnected subgraphs as distinct clusters that stay
    // packed close together instead of overlapping in one blob. Find connected
    // components (edges treated as undirected), estimate each one's size, then
    // place their centers with a sunflower (phyllotaxis) spiral so they fill the
    // disc from the center outward — biggest cluster in the middle.
    const componentCenters = {}
    if (layoutMode == null) {
      const adj = {}
      nodeData.forEach(n => { adj[n.id] = [] })
      linkData.forEach(l => {
        const s = l.source?.id ?? l.source, t = l.target?.id ?? l.target
        adj[s]?.push(t); adj[t]?.push(s)
      })
      const componentOf = {}
      let comp = 0
      nodeData.forEach(n => {
        if (componentOf[n.id] != null) return
        componentOf[n.id] = comp
        const q = [n.id]
        while (q.length) {
          const cur = q.shift()
          ;(adj[cur] || []).forEach(nb => {
            if (componentOf[nb] == null) { componentOf[nb] = comp; q.push(nb) }
          })
        }
        comp++
      })
      const nComp = comp
      const counts = {}
      nodeData.forEach(n => { const c = componentOf[n.id]; counts[c] = (counts[c] || 0) + 1 })
      // Rough on-screen radius of each component (packing estimate)
      const base = compactMode ? 32 : 55
      const scale = compactMode ? 20 : 40
      const compRadius = {}
      for (let c = 0; c < nComp; c++) compRadius[c] = base + Math.sqrt(counts[c] || 1) * scale
      const centerByComp = {}
      // Largest clusters first so they anchor the center; smaller ones fill in around them.
      const order = [...Array(nComp).keys()].sort((a, b) => compRadius[b] - compRadius[a])
      const golden = Math.PI * (3 - Math.sqrt(5)) // ~137.5° — even sunflower spread
      const packK = compactMode ? 1.4 : 1.15     // spacing so clusters nestle without heavy overlap
      let cumArea = 0
      order.forEach((c, i) => {
        // Distance = radius of a disc that would hold every cluster placed so far
        const dist = Math.sqrt(cumArea / Math.PI) * packK
        const a = i * golden
        centerByComp[c] = { x: width / 2 + dist * Math.cos(a), y: height / 2 + dist * Math.sin(a) }
        cumArea += Math.PI * compRadius[c] * compRadius[c]
      })
      nodeData.forEach(n => { componentCenters[n.id] = centerByComp[componentOf[n.id]] })

      // Fresh open (no remembered positions yet): seed from graph distances so
      // the layout starts untangled with as few edge crossings as possible.
      if (!nodeData.some(n => n.x != null)) {
        const seed = computeStressSeed(
          nodeData, linkData, compactMode ? COMPACT_LINK_DISTANCE : LINK_DISTANCE
        )
        if (seed) {
          // Translate each component's seeded cloud onto its packed center
          const sums = {}
          nodeData.forEach((n, i) => {
            const c = componentOf[n.id]
            const s = (sums[c] ||= { x: 0, y: 0, n: 0 })
            s.x += seed[i].x; s.y += seed[i].y; s.n++
          })
          nodeData.forEach((n, i) => {
            const c = componentOf[n.id]
            n.x = centerByComp[c].x + seed[i].x - sums[c].x / sums[c].n
            n.y = centerByComp[c].y + seed[i].y - sums[c].y / sums[c].n
          })
        }
      }
    }
    const getComponentX = d => componentCenters[d.id]?.x ?? width / 2
    const getComponentY = d => componentCenters[d.id]?.y ?? height / 2

    const defs = svg.append('defs')
    defs.append('marker')
      .attr('id', 'arrow')
      .attr('viewBox', '0 -4 8 8')
      .attr('refX', 7)
      .attr('refY', 0)
      .attr('markerWidth', 6)
      .attr('markerHeight', 6)
      .attr('orient', 'auto')
      .append('path')
      .attr('d', 'M0,-3L7,0L0,3')
      .attr('fill', 'currentColor')

    const g = svg.append('g')
    const zoomBehavior = d3.zoom().scaleExtent([0.2, 3]).on('zoom', e => g.attr('transform', e.transform))
    svg.call(zoomBehavior)

    if (zoomTransformRef.current) {
      svg.call(zoomBehavior.transform, zoomTransformRef.current)
    }

    const hasPositions = nodeData.some(n => n.x != null)

    const getClusterX = d => clusterCenters[(d.tags || [])[0] || '__untagged__']?.x ?? width / 2
    const getClusterY = d => clusterCenters[(d.tags || [])[0] || '__untagged__']?.y ?? height / 2

    const simulation = d3.forceSimulation(nodeData)
      .alphaMin(0.01)
      .alpha(hasPositions ? 0.3 : 1)
      .force('link', d3.forceLink(linkData).id(d => d.id).distance(compactMode ? COMPACT_LINK_DISTANCE : LINK_DISTANCE).strength(0.3))
      .force('charge', d3.forceManyBody().strength(compactMode ? -130 : -700))
      .force('x', layoutMode === 'tagClusters'
        ? d3.forceX(getClusterX).strength(0.5)
        : layoutMode == null
          ? d3.forceX(getComponentX).strength(0.2)
          : d3.forceX(width / 2).strength(0.05)
      )
      .force('y', (layoutMode === 'hierarchical' || layoutMode === 'pinnedTop')
        ? d3.forceY(d => padding + (levels[d.id] || 0) * levelSpacing).strength(0.6)
        : layoutMode === 'tagClusters'
          ? d3.forceY(getClusterY).strength(0.5)
          : layoutMode == null
            ? d3.forceY(getComponentY).strength(0.2)
            : d3.forceY(height / 2).strength(0.05)
      )
      .force('radial', layoutMode === 'radial' && selectedNodeId
        ? d3.forceRadial(d => (radialDist[d.id] || 0) * RADIAL_RING_SPACING, width / 2, height / 2).strength(0.8)
        : null
      )
      .force('collision', d3.forceCollide(d => d._r + (compactMode ? 4 : 12)))

    if (layoutMode === 'radial' && selectedNodeId) {
      const center = nodeData.find(n => n.id === selectedNodeId)
      if (center) { center.fx = width / 2; center.fy = height / 2 }
    }

    const tooltip = d3.select(svgEl.parentElement)
      .selectAll('.graph-tooltip').data([null])
      .join('div').attr('class', 'graph-tooltip')

    const link = g.append('g')
      .selectAll('path')
      .data(linkData)
      .join('path')
      .attr('class', 'graph-link')
      .attr('stroke', d => relDefs[d.relationshipId]?.color || text2)
      .style('color', d => relDefs[d.relationshipId]?.color || text2)
      .attr('marker-end', 'url(#arrow)')
      .style('pointer-events', 'none')
      .style('stroke-width', '2px')

    const linkHit = g.append('g')
      .selectAll('path')
      .data(linkData)
      .join('path')
      .attr('fill', 'none')
      .style('stroke', 'transparent')
      .style('stroke-width', '14px')
      .style('pointer-events', 'stroke')
      .style('cursor', 'pointer')
      .on('mouseover', (event, d) => {
        const label = relDefs[d.relationshipId]?.name || ''
        if (label) {
          tooltip.text(label)
            .style('left', (event.offsetX + 12) + 'px')
            .style('top', (event.offsetY - 8) + 'px')
            .style('opacity', '1')
        }
      })
      .on('mousemove', event => {
        tooltip.style('left', (event.offsetX + 12) + 'px').style('top', (event.offsetY - 8) + 'px')
      })
      .on('mouseout', () => {
        tooltip.style('opacity', '0')
      })
      .on('contextmenu', (event, d) => {
        event.preventDefault()
        event.stopPropagation()
        const src = typeof d.source === 'object' ? d.source.id : d.source
        const tgt = typeof d.target === 'object' ? d.target.id : d.target
        setContextMenu({ x: event.clientX, y: event.clientY, nodeId: null, edgeData: { sourceId: src, targetId: tgt, relationshipId: d.relationshipId } })
      })

    const node = g.append('g')
      .selectAll('g')
      .data(nodeData)
      .join('g')
      .attr('class', d => 'graph-node' + (d.id === selectedNodeId ? ' selected' : ''))
      .call(
        d3.drag()
          .on('start', (event, d) => { if (!event.active) simulation.alphaTarget(0.3).restart(); d.fx = d.x; d.fy = d.y })
          .on('drag', (event, d) => { d.fx = event.x; d.fy = event.y })
          .on('end', (event, d) => { if (!event.active) simulation.alphaTarget(0); d.fx = null; d.fy = null })
      )
      .on('click', (event, d) => {
        if (event.defaultPrevented) return
        const pr = pendingRelRef.current
        if (pr) {
          if (d.id !== pr.sourceNodeId) {
            onEditAddRelationship?.(pr.sourceNodeId, d.id, pr.relTypeId)
            setPendingRel(null)
          }
        } else if (splitDragging.current) {
          // ignore clicks during resize
        } else if (splitModeRef.current) {
          onSelectNodeSplit?.(d)
        } else {
          onHighlightNode?.(d)
        }
      })
      .on('dblclick', (event, d) => {
        if (event.defaultPrevented) return
        event.stopPropagation()
        onSelectNode(d)
      })
      .on('contextmenu', (event, d) => {
        event.preventDefault()
        event.stopPropagation()
        setContextMenu({ x: event.clientX, y: event.clientY, nodeId: d.id })
      })
      .on('mouseover', (event, d) => {
        if (compactMode) {
          tooltip.text(d.title || '(untitled)')
            .style('left', (event.offsetX + 12) + 'px')
            .style('top', (event.offsetY - 8) + 'px')
            .style('opacity', '1')
        }
        d._hoverTimer = setTimeout(() => {
          const connected = new Set([d.id])
          const queue = [d.id]
          while (queue.length > 0) {
            const current = queue.shift()
            for (const tgt of (hoverOutEdges[current] || [])) {
              if (!connected.has(tgt)) { connected.add(tgt); queue.push(tgt) }
            }
          }
          node.classed('dimmed', n => !connected.has(n.id) && n.id !== selectedNodeId)
            .classed('focused', n => n.id === d.id)
          link.classed('dimmed', l => {
            const src = typeof l.source === 'object' ? l.source.id : l.source
            const tgt = typeof l.target === 'object' ? l.target.id : l.target
            return !connected.has(src) || !connected.has(tgt)
          })
        }, 100)
      })
      .on('mousemove', event => {
        if (compactMode) {
          tooltip.style('left', (event.offsetX + 12) + 'px').style('top', (event.offsetY - 8) + 'px')
        }
      })
      .on('mouseout', (event, d) => {
        clearTimeout(d._hoverTimer)
        if (compactMode) tooltip.style('opacity', '0')
        node.classed('dimmed', false).classed('focused', false)
        link.classed('dimmed', false)
      })

    node.append('circle')
      .attr('r', d => d._r)
      .style('stroke', d => {
        const primaryTagId = (d.tags || [])[0]
        return primaryTagId && tagDefs[primaryTagId]?.color ? tagDefs[primaryTagId].color : accent
      })
      .style('color', d => {
        const primaryTagId = (d.tags || [])[0]
        return primaryTagId && tagDefs[primaryTagId]?.color ? tagDefs[primaryTagId].color : accent
      })
      .style('fill', d => {
        const primaryTagId = (d.tags || [])[0]
        return primaryTagId && tagDefs[primaryTagId]?.color ? tagDefs[primaryTagId].color : accent
      })
      .style('fill-opacity', 1)

    if (!compactMode) {
      node.append('text')
        .attr('class', 'graph-node-rate')
        .text(d => {
          const rate = displayConfidence(d)
          return (rate != null && rate > 0) ? rate : ''
        })
        .attr('x', 0).attr('y', 0)
        .attr('text-anchor', 'middle').attr('dominant-baseline', 'middle')
        .style('font-size', d => `${Math.round(d._r * 0.85)}px`)  // inline so it beats the 11px .graph-node text rule
        .attr('font-weight', '600')
        .style('fill', '#000').style('stroke', 'none').style('pointer-events', 'none')
    }

    if (!compactMode) {
      node.append('text')
        .attr('class', 'graph-node-label')
        .text(d => { const t = d.title || ''; return t.length > 18 ? t.slice(0, 17) + '…' : t })
        .attr('x', d => d._r + 6).attr('y', 0)
    }

    if (!compactMode) {
      node.filter(d => pinnedNodeIds.has(d.id))
        .append('g')
        .attr('class', 'graph-node-pin')
        .attr('transform', d => `translate(${Math.round(d._r * 0.8)},${-Math.round(d._r * 0.8)})`)
        .call(g => {
          g.append('circle').attr('r', 6).attr('class', 'graph-node-pin-bg')
          g.append('path')
            .attr('transform', 'translate(-3.6,-3.6) scale(0.45)')
            .attr('d', 'M9.828.722a.5.5 0 0 1 .354.146l4.95 4.95a.5.5 0 0 1 0 .707c-.48.48-1.072.588-1.503.588-.177 0-.335-.018-.46-.039l-3.134 3.134a5.927 5.927 0 0 1 .16 1.013c.046.702-.032 1.687-.72 2.375a.5.5 0 0 1-.707 0l-2.829-2.828-3.182 3.182c-.195.195-1.219.902-1.414.707-.195-.195.512-1.22.707-1.414l3.182-3.182-2.828-2.829a.5.5 0 0 1 0-.707c.688-.688 1.673-.767 2.375-.72a5.922 5.922 0 0 1 1.013.16l3.134-3.133a2.772 2.772 0 0 1-.04-.461c0-.43.108-1.022.589-1.503a.5.5 0 0 1 .353-.146z')
        })
    }

    simulation.on('tick', () => {
      const EDGE_FAN = compactMode ? 10 : 22   // px separation between edges sharing a pair
      const pathD = d => {
        const sx = d.source.x, sy = d.source.y, tx = d.target.x, ty = d.target.y
        const dx = tx - sx, dy = ty - sy
        const dist = Math.sqrt(dx * dx + dy * dy)
        if (dist === 0) return ''
        const ux = dx / dist, uy = dy / dist
        const x1 = sx + ux * d.source._r, y1 = sy + uy * d.source._r
        const x2 = tx - ux * d.target._r, y2 = ty - uy * d.target._r
        // Single edge for this pair → straight line.
        if ((d._pairCount || 1) <= 1) return `M${x1},${y1} L${x2},${y2}`
        // Multiple edges → bow each one aside by a stable, direction-independent
        // offset so mutual/parallel edges separate instead of overlapping.
        const sign = (d.source.id < d.target.id) ? 1 : -1
        const off = (d._pairIndex - (d._pairCount - 1) / 2) * EDGE_FAN * sign
        const mx = (x1 + x2) / 2 - uy * off * 2
        const my = (y1 + y2) / 2 + ux * off * 2
        return `M${x1},${y1} Q${mx},${my} ${x2},${y2}`
      }
      link.attr('d', pathD)
      linkHit.attr('d', pathD)
      node.attr('transform', d => `translate(${d.x},${d.y})`)
    })

    svg.on('contextmenu', (event) => {
      event.preventDefault()
      const t = zoomTransformRef.current || d3.zoomIdentity
      const rect = svgEl.getBoundingClientRect()
      const graphX = (event.clientX - rect.left - t.x) / t.k
      const graphY = (event.clientY - rect.top  - t.y) / t.k
      setContextMenu({ x: event.clientX, y: event.clientY, nodeId: null, graphX, graphY })
    })

    return () => simulation.stop()
  }, [structureKey, tagFilter, layoutMode,
    layoutMode === 'radial' ? selectedNodeId : null, splitMode, compactMode])

  // Lightweight content sync — refreshes titles/confidence text and the bound
  // datum in place when node content changes without a structural change,
  // so autosaves and quick edits never restart the simulation.
  useEffect(() => {
    if (!svgRef.current) return
    const byId = new Map(nodes.map(n => [n.id, n]))
    d3.select(svgRef.current).selectAll('.graph-node').each(function (d) {
      const fresh = d?.id ? byId.get(d.id) : null
      if (!fresh) return
      d.title = fresh.title
      d.metadata = fresh.metadata
      d.confidence = fresh.confidence
      const sel = d3.select(this)
      sel.select('text.graph-node-label')
        .text(() => { const t = fresh.title || ''; return t.length > 18 ? t.slice(0, 17) + '…' : t })
      sel.select('text.graph-node-rate')
        .text(() => {
          const rate = displayConfidence(fresh)
          return (rate != null && rate > 0) ? rate : ''
        })
    })
  }, [nodes])

  // Lightweight selection — just toggles CSS class, no simulation restart
  useEffect(() => {
    if (!svgRef.current) return
    d3.select(svgRef.current).selectAll('.graph-node')
      .classed('selected', d => d?.id === selectedNodeId)
  }, [selectedNodeId])

  // Lightweight ego view — dims non-neighborhood nodes, no simulation restart
  useEffect(() => {
    if (!svgRef.current) return
    const allNodes = d3.select(svgRef.current).selectAll('.graph-node')
    const allLinks = d3.select(svgRef.current).selectAll('.graph-link')
    if (!egoView || !selectedNodeId) {
      allNodes.classed('ego-dimmed', false)
      allLinks.classed('ego-dimmed', false)
      return
    }
    const outAdj = {}, inAdj = {}
    allLinks.each(d => {
      const s = typeof d.source === 'object' ? d.source.id : d.source
      const t = typeof d.target === 'object' ? d.target.id : d.target
      if (!outAdj[s]) outAdj[s] = []
      if (!inAdj[t]) inAdj[t] = []
      outAdj[s].push(t)
      inAdj[t].push(s)
    })
    const reachable = new Set([selectedNodeId])
    let frontier = [selectedNodeId]
    for (let hop = 0; hop < egoHops; hop++) {
      const next = []
      frontier.forEach(id => {
        ;[...(outAdj[id] || []), ...(inAdj[id] || [])].forEach(nb => {
          if (!reachable.has(nb)) { reachable.add(nb); next.push(nb) }
        })
      })
      frontier = next
    }
    allNodes.classed('ego-dimmed', d => !reachable.has(d?.id))
    allLinks.classed('ego-dimmed', d => {
      const s = typeof d.source === 'object' ? d.source.id : d.source
      const t = typeof d.target === 'object' ? d.target.id : d.target
      return !reachable.has(s) || !reachable.has(t)
    })
  }, [egoView, egoHops, selectedNodeId])

  // Lightweight search — just toggles CSS classes, no simulation restart
  useEffect(() => {
    if (!svgRef.current) return
    const allNodes = d3.select(svgRef.current).selectAll('.graph-node')
    const allLinks = d3.select(svgRef.current).selectAll('.graph-link')
    const q = searchQuery.trim().toLowerCase()
    if (!q) {
      allNodes.classed('search-match', false).classed('search-dimmed', false)
      allLinks.classed('search-dimmed', false)
      return
    }
    const matches = new Set()
    allNodes.each(d => { if ((d.title || '').toLowerCase().includes(q)) matches.add(d.id) })
    allNodes
      .classed('search-match', d => matches.has(d.id))
      .classed('search-dimmed', d => !matches.has(d.id))
    allLinks.classed('search-dimmed', l => {
      const src = typeof l.source === 'object' ? l.source.id : l.source
      const tgt = typeof l.target === 'object' ? l.target.id : l.target
      return !matches.has(src) || !matches.has(tgt)
    })
  }, [searchQuery])

  // Study path — highlights the panel's result and numbers its steps, no simulation restart.
  // Shares the rebuild effect's triggers so it re-applies after the svg is redrawn.
  useEffect(() => {
    if (!svgRef.current) return
    const allNodes = d3.select(svgRef.current).selectAll('.graph-node')
    const allLinks = d3.select(svgRef.current).selectAll('.graph-link')
    allNodes.selectAll('.graph-node-step').remove()
    if (!studyHighlight) {
      allNodes.classed('study-primary study-secondary study-warn study-dimmed', false)
      allLinks.classed('study-link study-dimmed', false)
      return
    }
    const { roles, steps, edges } = studyHighlight
    allNodes
      .classed('study-primary', d => roles.get(d.id) === 'primary')
      .classed('study-secondary', d => roles.get(d.id) === 'secondary')
      .classed('study-warn', d => roles.get(d.id) === 'warn')
      .classed('study-dimmed', d => !roles.has(d.id))
    allLinks.each(function (l) {
      const src = typeof l.source === 'object' ? l.source.id : l.source
      const tgt = typeof l.target === 'object' ? l.target.id : l.target
      const onPath = edges.has(edgeKey(src, tgt, l.relationshipId))
      d3.select(this).classed('study-link', onPath).classed('study-dimmed', !onPath)
    })
    if (!compactMode) {
      allNodes.filter(d => steps.has(d.id))
        .append('g')
        .attr('class', 'graph-node-step')
        .attr('transform', d => `translate(${-Math.round(d._r * 0.8)},${-Math.round(d._r * 0.8)})`)
        .call(g => {
          g.append('circle').attr('r', 6)
          g.append('text')
            .text(d => steps.get(d.id))
            .attr('text-anchor', 'middle')
        })
    }
  }, [studyHighlight, structureKey, tagFilter, layoutMode, splitMode, compactMode])

  return (
    <div className={`graph-overlay${pendingRel ? ' graph-overlay--pending' : ''}`} ref={splitOverlayRef}>
      {pendingRel && (
        <div className="gv-pending-hint">
          Click target node to add relationship — <button onClick={() => setPendingRel(null)}>Cancel</button>
        </div>
      )}
      <div className="graph-view-header">
        <button className="btn-ghost" onClick={onClose}>← Back</button>
        <input
          className="graph-search"
          type="text"
          placeholder="Search notes…"
          value={searchQuery}
          onChange={e => setSearchQuery(e.target.value)}
          autoComplete="off"
        />
        <div className="graph-tag-dropdown" ref={tagDropdownRef}>
          <button
            className={`graph-tag-dropdown-btn${tagDropdownOpen ? ' open' : ''}`}
            onClick={() => setTagDropdownOpen(v => !v)}
          >
            {tagFilter.size === 0
              ? 'All tags'
              : tagFilter.size === 1
                ? tagsList.find(t => tagFilter.has(t.id))?.name ?? '1 tag'
                : `${tagFilter.size} tags`
            }
            <svg viewBox="0 0 10 10" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round">
              <path d={tagDropdownOpen ? 'M2 7L5 4l3 3' : 'M2 3l3 3 3-3'} />
            </svg>
          </button>
          {tagDropdownOpen && (
            <div className="graph-tag-dropdown-menu">
              <button
                className={`graph-tag-dropdown-item${tagFilter.size === 0 ? ' selected' : ''}`}
                onClick={() => setTagFilter(new Set())}
              >
                <span className="graph-tag-dropdown-check">{tagFilter.size === 0 ? '✓' : ''}</span>
                All tags
              </button>
              {tagsList.map(t => (
                <button
                  key={t.id}
                  className={`graph-tag-dropdown-item${tagFilter.has(t.id) ? ' selected' : ''}`}
                  onClick={() => toggleTag(t.id)}
                >
                  <span className="graph-tag-dropdown-check">{tagFilter.has(t.id) ? '✓' : ''}</span>
                  <span className="graph-tag-dropdown-dot" style={t.color ? { background: t.color } : undefined} />
                  {t.name}
                </button>
              ))}
            </div>
          )}
        </div>
        <button
          className={`${compactMode ? '' : 'btn-ghost'}`}
          onClick={() => setCompactMode(v => !v)}
          title="Toggle compact view"
        >
          Compact
        </button>
        <button
          className={`${splitMode ? '' : 'btn-ghost'}`}
          onClick={() => setSplitMode(v => !v)}
          title="Toggle split view"
          style={{ marginLeft: 'auto' }}
        >
          Split View
        </button>
        <select
          className="graph-view-style-select"
          value={layoutMode ?? ''}
          onChange={e => setLayoutMode(e.target.value || null)}
          title="View style"
        >
          <option value="">View Style...</option>
          <option value="hierarchical">Hierarchical</option>
          <option value="pinnedTop">Pinned Top</option>
          <option value="radial">Radial</option>
          <option value="tagClusters">Clusters</option>
        </select>
        <div className="graph-ego-controls">
          <button
            className={egoView ? '' : 'btn-ghost'}
            onClick={() => setEgoView(v => !v)}
            title="Show only neighborhood of selected node"
          >Neighbor View</button>
          {egoView && (
            <div className="graph-ego-hops">
              <button
                className={egoHops === 1 ? '' : 'btn-ghost'}
                onClick={() => setEgoHops(1)}
              >1</button>
              <button
                className={egoHops === 2 ? '' : 'btn-ghost'}
                onClick={() => setEgoHops(2)}
              >2</button>
            </div>
          )}
        </div>
        <button
          className={`btn-icon-header${showStudyPath ? ' btn-icon-header--active' : ''}`}
          onClick={onToggleStudyPath}
          title="Study path"
        >
          <StudyPathIcon />
          Study Path
        </button>
        <button
          className={`btn-icon-header${showChat ? ' btn-icon-header--active' : ''}`}
          onClick={onToggleChat}
          title="Assistant"
        >
          <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" style={{ width: 13, height: 13, flexShrink: 0 }}>
            <path d="M2.5 4L13.5 4A1 1 0 0114.5 5L14.5 11A1 1 0 0113.5 12L6 12L3.5 14L3.5 12A1 1 0 012.5 11Z" />
            <line x1="5.5" y1="7" x2="5.5" y2="7.01" /><line x1="8" y1="7" x2="8" y2="7.01" /><line x1="10.5" y1="7" x2="10.5" y2="7.01" />
          </svg>
          Assistant
        </button>
      </div>
      <div className={`graph-body${splitMode ? ' graph-body--split' : ''}`}>
        <div className="graph-canvas-area" style={splitMode ? { width: `${splitWidth}%` } : undefined}>
          <svg ref={svgRef} className="graph-svg" />
          <div className="graph-legend">
            <button
              className="graph-legend-toggle"
              onClick={() => setLegendOpen(v => !v)}
              title="Toggle legend"
            >
              {legendOpen ? '✕' : '?'}
            </button>
            {legendOpen && (
              <div className="graph-legend-panel">
                {tagsList.length > 0 && (
                  <div className="graph-legend-section">
                    <div className="graph-legend-label">Tags</div>
                    {tagsList.map(t => (
                      <div key={t.id} className="graph-legend-row">
                        <span className="graph-legend-dot" style={t.color ? { background: t.color } : undefined} />
                        <span>{t.name}</span>
                      </div>
                    ))}
                  </div>
                )}
                <div className="graph-legend-section">
                  <div className="graph-legend-label">Edges</div>
                  <div className="graph-legend-row">
                    <svg width="36" height="20" style={{ flexShrink: 0 }}>
                      <defs>
                        <marker id="leg-arrow" viewBox="0 -3 7 6" refX="6" refY="0" markerWidth="5" markerHeight="5" orient="auto">
                          <path d="M0,-2.5L6,0L0,2.5" fill="currentColor" />
                        </marker>
                      </defs>
                      <line x1="4" y1="10" x2="30" y2="10" stroke="currentColor" strokeWidth="1.5" markerEnd="url(#leg-arrow)" />
                    </svg>
                    <span>directed relationship</span>
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>
        {splitMode && (
          <>
            <div className="graph-split-divider" onMouseDown={onSplitDividerMouseDown} />
            <div className="graph-split-panel">
              {renderSplitPanel ? renderSplitPanel() : <p className="graph-split-empty">Select a node</p>}
            </div>
          </>
        )}
      </div>
      {contextMenu && (
        <GraphViewContextMenu
          ref={contextMenuRef}
          x={contextMenu.x}
          y={contextMenu.y}
          nodeId={contextMenu.nodeId}
          nodeData={nodes.find(n => n.id === contextMenu.nodeId)}
          edgeData={contextMenu.edgeData}
          relTypesList={relTypesList ?? []}
          tagsList={tagsListProp ?? []}
          onAddRelationship={(sourceNodeId, relTypeId) => setPendingRel({ sourceNodeId, relTypeId })}
          onAddTag={(nodeId, tagId) => onEditAddTag?.(nodeId, tagId)}
          onDeleteNode={(nodeId) => onEditDeleteNode?.(nodeId)}
          onDeleteEdge={(src, tgt, relId) => onEditDeleteEdge?.(src, tgt, relId)}
          onChangeRelType={(src, tgt, oldRelId, newRelId) => onEditChangeRelType?.(src, tgt, oldRelId, newRelId)}
          onQuickEdit={(nodeId, data) => onEditQuickEdit?.(nodeId, data)}
          onCreateNote={(title) => onCreateNote?.(title, contextMenu.graphX, contextMenu.graphY)}
          onClose={() => setContextMenu(null)}
        />
      )}
    </div>
  )
})

export default GraphView
