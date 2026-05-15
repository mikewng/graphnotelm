import { useEffect, useRef, useState } from 'react'
import * as d3 from 'd3'

const NODE_R = 18
const LINK_DISTANCE = 200

// nodes: [{id, title, relationships: [{targetNodeId, relationshipId}], tags: [tagId]}]
// tagDefs: {[tagId]: {name, color}}
// relDefs: {[relId]: {name, color}}
export default function GraphView({ nodes, tagDefs, relDefs = {}, selectedNodeId, onClose, onSelectNode }) {
  const svgRef = useRef(null)
  const [tagFilter, setTagFilter] = useState(new Set())
  const [hierarchical, setHierarchical] = useState(false)
  const [searchQuery, setSearchQuery] = useState('')

  function toggleTag(id) {
    setTagFilter(prev => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  const tagsList = Object.entries(tagDefs || {}).map(([id, t]) => ({ ...t, id }))

  useEffect(() => {
    const svgEl = svgRef.current
    if (!svgEl) return

    const svg = d3.select(svgEl)
    svg.selectAll('*').remove()

    const width = svgEl.clientWidth
    const height = svgEl.clientHeight

    const cssVars = getComputedStyle(document.documentElement)
    const accent = cssVars.getPropertyValue('--accent').trim()
    const text2  = cssVars.getPropertyValue('--text-2').trim()

    let visibleNodes = nodes.filter(n =>
      tagFilter.size === 0 || (n.tags || []).some(t => tagFilter.has(t))
    )


    const visibleIds = new Set(visibleNodes.map(n => n.id))

    const nodeData = visibleNodes.map(n => ({ ...n }))
    const linkData = visibleNodes.flatMap(n =>
      (n.relationships || [])
        .filter(r => visibleIds.has(r.targetNodeId))
        .map(r => ({ source: n.id, target: r.targetNodeId, relationshipId: r.relationshipId }))
    )

    // Compute incoming edge count and scale radius accordingly
    const incomingCount = {}
    nodeData.forEach(n => { incomingCount[n.id] = 0 })
    linkData.forEach(l => { incomingCount[l.target] = (incomingCount[l.target] || 0) + 1 })
    const maxIncoming = Math.max(...Object.values(incomingCount), 1)
    const rScale = d3.scaleSqrt().domain([0, maxIncoming]).range([NODE_R, NODE_R * 2]).clamp(true)
    nodeData.forEach(n => { n._r = rScale(incomingCount[n.id] || 0) })

    const levels = {}
    if (hierarchical) {
      nodes.forEach(n => (levels[n.id] = 0))
      for (let iter = 0; iter < nodes.length; iter++) {
        let changed = false
        nodes.forEach(n => {
          const sources = nodes
            .filter(s => (s.relationships || []).some(r => r.targetNodeId === n.id))
            .map(s => s.id)
          if (sources.length > 0) {
            const next = Math.max(...sources.map(sid => levels[sid] ?? 0)) + 1
            if (next > (levels[n.id] ?? 0)) { levels[n.id] = next; changed = true }
          }
        })
        if (!changed) break
      }
    }
    const maxLevel = hierarchical ? Math.max(...Object.values(levels), 0) : 0
    const padding = 90
    const levelSpacing = maxLevel > 0 ? (height - padding * 2) / maxLevel : 0

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
    svg.call(
      d3.zoom().scaleExtent([0.2, 3]).on('zoom', e => g.attr('transform', e.transform))
    )

    const simulation = d3.forceSimulation(nodeData)
      .force('link', d3.forceLink(linkData).id(d => d.id).distance(LINK_DISTANCE).strength(0.3))
      .force('charge', d3.forceManyBody().strength(-700))
      .force('x', d3.forceX(width / 2).strength(0.05))
      .force('y', hierarchical
        ? d3.forceY(d => padding + (levels[d.id] || 0) * levelSpacing).strength(0.6)
        : d3.forceY(height / 2).strength(0.05)
      )
      .force('collision', d3.forceCollide(d => d._r + 12))

    const tooltip = d3.select(svgEl.parentElement)
      .selectAll('.graph-tooltip').data([null])
      .join('div').attr('class', 'graph-tooltip')

    // Paths instead of lines — allows curved arcs
    const link = g.append('g')
      .selectAll('path')
      .data(linkData)
      .join('path')
      .attr('class', 'graph-link')
      .attr('stroke', d => relDefs[d.relationshipId]?.color || text2)
      .style('color', d => relDefs[d.relationshipId]?.color || text2)
      .attr('marker-end', 'url(#arrow)')
      .style('pointer-events', 'stroke')
      .style('stroke-width', '2px')
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
        onSelectNode(d)
      })
      .on('mouseover', (event, d) => {
        d._hoverTimer = setTimeout(() => {
          const outEdges = {}
          linkData.forEach(l => {
            const src = typeof l.source === 'object' ? l.source.id : l.source
            const tgt = typeof l.target === 'object' ? l.target.id : l.target
            if (!outEdges[src]) outEdges[src] = []
            outEdges[src].push(tgt)
          })

          const connected = new Set([d.id])
          const queue = [d.id]
          while (queue.length > 0) {
            const current = queue.shift()
            for (const tgt of (outEdges[current] || [])) {
              if (!connected.has(tgt)) {
                connected.add(tgt)
                queue.push(tgt)
              }
            }
          }

          node.classed('dimmed', n => !connected.has(n.id))
              .classed('focused', n => n.id === d.id)
          link.classed('dimmed', l => {
            const src = typeof l.source === 'object' ? l.source.id : l.source
            const tgt = typeof l.target === 'object' ? l.target.id : l.target
            return !connected.has(src) || !connected.has(tgt)
          })
        }, 100)
      })
      .on('mouseout', (event, d) => {
        clearTimeout(d._hoverTimer)
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
      .style('fill-opacity', d => {
        const rate = d.metadata?.userConfidenceRate
        if (rate == null) return 0
        if (rate <= 5) return rate * 0.03          // 1–5 → 0.03–0.15 (very transparent)
        return 0.15 + (rate - 5) * 0.17            // 6–10 → 0.32–1.0  (clearly opaque)
      })

    node.append('text')
      .text(d => {
        const rate = d.metadata?.userConfidenceRate
        return (rate != null && rate > 0) ? rate : ''
      })
      .attr('x', 0)
      .attr('y', 0)
      .attr('text-anchor', 'middle')
      .attr('dominant-baseline', 'middle')
      .attr('font-size', d => Math.max(9, Math.round(d._r * 0.5)))
      .attr('font-weight', '600')
      .style('fill', '#000')
      .style('stroke', 'none')
      .style('pointer-events', 'none')

    node.append('text')
      .text(d => {
        const t = d.title || ''
        return t.length > 18 ? t.slice(0, 17) + '…' : t
      })
      .attr('x', d => d._r + 6)
      .attr('y', 0)

    simulation.on('tick', () => {
      link.attr('d', d => {
        const sx = d.source.x, sy = d.source.y, tx = d.target.x, ty = d.target.y
        const dx = tx - sx, dy = ty - sy
        const dist = Math.sqrt(dx * dx + dy * dy)
        if (dist === 0) return ''
        const ux = dx / dist, uy = dy / dist
        const x1 = sx + ux * d.source._r, y1 = sy + uy * d.source._r
        const x2 = tx - ux * d.target._r, y2 = ty - uy * d.target._r
        const dr = dist * 1.6
        return `M${x1},${y1} A${dr},${dr} 0 0,1 ${x2},${y2}`
      })
      node.attr('transform', d => `translate(${d.x},${d.y})`)
    })

    return () => simulation.stop()
  }, [nodes, tagDefs, relDefs, tagFilter, selectedNodeId, hierarchical])

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

  return (
    <div className="graph-overlay">
      <div className="graph-view-header">
        <button className="btn-ghost" onClick={onClose}>← Back</button>

        <input
          className="graph-search"
          type="text"
          placeholder="Search nodes…"
          value={searchQuery}
          onChange={e => setSearchQuery(e.target.value)}
          autoComplete="off"
        />

        <button
          className={hierarchical ? '' : 'btn-ghost'}
          onClick={() => setHierarchical(v => !v)}
          title="Toggle hierarchical layout"
          style={{ marginLeft: 'auto' }}
        >
          Hierarchical
        </button>
        <div className="graph-tag-filter">
          <button
            className={tagFilter.size === 0 ? 'active' : ''}
            onClick={() => setTagFilter(new Set())}
          >All</button>
          {tagsList.map(t => (
            <button
              key={t.id}
              className={tagFilter.has(t.id) ? 'active' : ''}
              style={t.color ? { '--tag-color': t.color } : undefined}
              onClick={() => toggleTag(t.id)}
            >{t.name}</button>
          ))}
        </div>
      </div>
      <svg ref={svgRef} className="graph-svg" />
    </div>
  )
}
