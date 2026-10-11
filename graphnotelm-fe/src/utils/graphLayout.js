// Layout helpers for the graph view's default (force) layout.
//
// nodes: [{id, x, y, _r}] — the simulation's node objects, positions mutated in place
// links: [{source, target}] — ids or node objects, as before or after d3.forceLink resolves them

// Cost weights for untangling: a crossing outweighs an edge brushing a node,
// which outweighs edges drifting from their natural length (the tie-breaker).
const W_CROSS = 10
const W_OCCLUDE = 6
const W_LENGTH = 1
const NODE_CLEARANCE = 6     // px an edge must keep from a node's rim
const RING_ANGLES = 8        // candidate spots around each neighbour
const MAX_RING_NEIGHBOURS = 6
const SWAP_CANDIDATES = 8    // nearest nodes a node may trade places with
const MAX_SWEEPS = 20
const MAX_NODES = 400        // the passes below are O(n²)

const now = () => (typeof performance !== 'undefined' ? performance.now() : Date.now())
const endpointId = end => end?.id ?? end

// Distance-based seed layout (stress majorization): places nodes so on-screen
// distance ≈ shortest-path distance × linkDistance. Started from this, the
// force simulation only has to polish spacing instead of unfolding a random
// tangle, which is what produced heavy edge crossings. Returns positions
// indexed like nodeData (components are laid out independently around 0,0),
// or null when the graph is too large for the O(n²) passes.
export function computeStressSeed(nodeData, linkData, linkDistance) {
  const n = nodeData.length
  if (n < 3 || n > MAX_NODES) return null
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
  const xs = Float64Array.from(nodeData, (_, i) => Math.cos(i * golden) * linkDistance * 0.5 * Math.sqrt(i + 1))
  const ys = Float64Array.from(nodeData, (_, i) => Math.sin(i * golden) * linkDistance * 0.5 * Math.sqrt(i + 1))

  // 1 / target distance for each hop count, so the inner loop is a lookup.
  let maxHops = 0
  for (const d of dist) for (let j = 0; j < n; j++) if (d[j] > maxHops) maxHops = d[j]
  const invTarget = Float64Array.from({ length: maxHops + 1 }, (_, h) => (h > 0 ? 1 / (h * linkDistance) : 0))

  // Each pass moves every node to the weighted average of where its distances
  // to the others say it should be. Stop once no node moves more than a tiny
  // fraction of an edge; the force simulation polishes the rest.
  const ITERATIONS = 150
  const tolerance = linkDistance * 0.002
  for (let it = 0; it < ITERATIONS; it++) {
    let maxMove = 0
    for (let i = 0; i < n; i++) {
      const di = dist[i]
      const xi = xs[i], yi = ys[i]
      let wx = 0, wy = 0, wsum = 0
      for (let j = 0; j < n; j++) {
        const hops = di[j]
        if (hops <= 0) continue   // itself, or another component
        // weight 1/target², pulling j's suggestion for i: target along the j→i direction
        const inv = invTarget[hops], w = inv * inv
        const dx = xi - xs[j], dy = yi - ys[j]
        const len = Math.sqrt(dx * dx + dy * dy) || 1e-6
        wx += w * xs[j] + (dx / len) * inv
        wy += w * ys[j] + (dy / len) * inv
        wsum += w
      }
      if (wsum > 0) {
        const nx = wx / wsum, ny = wy / wsum
        maxMove = Math.max(maxMove, Math.abs(nx - xi), Math.abs(ny - yi))
        xs[i] = nx; ys[i] = ny
      }
    }
    if (maxMove < tolerance) break
  }
  return Array.from(xs, (x, i) => ({ x, y: ys[i] }))
}

// Edges as unique undirected index pairs: A→B and B→A, or several relationships
// between the same two notes, draw as one corridor, so they count once.
function buildEdges(nodes, links) {
  const index = new Map(nodes.map((d, i) => [d.id, i]))
  const seen = new Set()
  const edges = []
  for (const l of links) {
    const a = index.get(endpointId(l.source))
    const b = index.get(endpointId(l.target))
    if (a == null || b == null || a === b) continue
    const lo = Math.min(a, b), hi = Math.max(a, b)
    const key = lo * nodes.length + hi
    if (seen.has(key)) continue
    seen.add(key)
    edges.push([lo, hi])
  }
  return edges
}

const orient = (ax, ay, bx, by, cx, cy) => (bx - ax) * (cy - ay) - (by - ay) * (cx - ax)

// Proper crossing of segments ab and cd (touching or collinear doesn't count).
function segmentsCross(ax, ay, bx, by, cx, cy, dx, dy) {
  const d1 = orient(cx, cy, dx, dy, ax, ay)
  const d2 = orient(cx, cy, dx, dy, bx, by)
  if ((d1 > 0 && d2 > 0) || (d1 < 0 && d2 < 0) || d1 === 0 || d2 === 0) return false
  const d3 = orient(ax, ay, bx, by, cx, cy)
  const d4 = orient(ax, ay, bx, by, dx, dy)
  return (d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)
}

function pointSegmentDist2(px, py, ax, ay, bx, by) {
  const dx = bx - ax, dy = by - ay
  const len2 = dx * dx + dy * dy
  const t = len2 > 0 ? Math.max(0, Math.min(1, ((px - ax) * dx + (py - ay) * dy) / len2)) : 0
  const ex = ax + t * dx - px, ey = ay + t * dy - py
  return ex * ex + ey * ey
}

/**
 * How tangled a layout is: edge pairs that cross, and edges that pass through
 * a node they don't connect.
 */
export function measureLayout(nodes, links) {
  const edges = buildEdges(nodes, links)
  let crossings = 0, occlusions = 0
  for (let e = 0; e < edges.length; e++) {
    const [a, b] = edges[e]
    const A = nodes[a], B = nodes[b]
    for (let f = e + 1; f < edges.length; f++) {
      const [c, d] = edges[f]
      if (c === a || c === b || d === a || d === b) continue
      if (segmentsCross(A.x, A.y, B.x, B.y, nodes[c].x, nodes[c].y, nodes[d].x, nodes[d].y)) crossings++
    }
    for (let k = 0; k < nodes.length; k++) {
      if (k === a || k === b) continue
      const clear = (nodes[k]._r ?? 0) + NODE_CLEARANCE
      if (pointSegmentDist2(nodes[k].x, nodes[k].y, A.x, A.y, B.x, B.y) < clear * clear) occlusions++
    }
  }
  return { crossings, occlusions }
}

const layoutScore = ({ crossings, occlusions }) => crossings * W_CROSS + occlusions * W_OCCLUDE

/**
 * Greedy local search that moves nodes to remove edge crossings and edges
 * running through nodes. For each node caught in one, it tries spots around
 * each of its neighbours, its neighbours' centre, and trading places with a
 * nearby node, keeping the best move that lowers the cost. Nodes only move
 * within their own connected component. Mutates node positions.
 */
export function untangleLayout(nodes, links, { deadline = Infinity } = {}) {
  runToEnd(untangleSteps(nodes, links, () => now() >= deadline))
}

// Drives a step generator to completion and returns its result.
function runToEnd(steps) {
  let step
  do step = steps.next(); while (!step.done)
  return step.value
}

// untangleLayout as a generator that yields after each node it tries to move,
// so the work can be spread across frames. Stops once outOfTime() says so.
function* untangleSteps(nodes, links, outOfTime) {
  const n = nodes.length
  if (n < 3 || n > MAX_NODES) return
  const edges = buildEdges(nodes, links)
  const m = edges.length
  if (m < 2) return

  const x = Float64Array.from(nodes, d => d.x)
  const y = Float64Array.from(nodes, d => d.y)
  const ea = Int32Array.from(edges, e => e[0])
  const eb = Int32Array.from(edges, e => e[1])
  const clear2 = Float64Array.from(nodes, d => ((d._r ?? 0) + NODE_CLEARANCE) ** 2)
  const maxClear = Math.sqrt(Math.max(...clear2))
  const incident = Array.from({ length: n }, () => [])
  const neighbours = Array.from({ length: n }, () => [])
  edges.forEach(([a, b], e) => {
    incident[a].push(e); incident[b].push(e)
    neighbours[a].push(b); neighbours[b].push(a)
  })

  const component = new Int32Array(n).fill(-1)
  for (let s = 0, c = 0; s < n; s++) {
    if (component[s] !== -1) continue
    component[s] = c
    const q = [s]
    for (let qi = 0; qi < q.length; qi++)
      for (const nb of neighbours[q[qi]]) if (component[nb] === -1) { component[nb] = c; q.push(nb) }
    c++
  }

  // Target edge length = the layout's current median, so the length term
  // agrees with where the simulation actually settles edges.
  const lengths = edges.map(([a, b]) => Math.sqrt((x[a] - x[b]) ** 2 + (y[a] - y[b]) ** 2)).sort((p, q) => p - q)
  const target = lengths[lengths.length >> 1] || 1
  const minSep = target * 0.4

  // Uniform grid (one cell ≈ one edge length) over edges and nodes, so each
  // cost check only looks at what is near the segment in question instead of
  // the whole graph. Edges are filed under every cell their bounding box
  // covers. Keys wrap at 65536 cells; a collision only adds candidates, and
  // every candidate still gets the exact geometric test.
  const cellOf = v => Math.floor(v / target)
  const cellKey = (cx, cy) => (cx & 0xffff) * 65536 + (cy & 0xffff)
  const edgeGrid = new Map(), nodeGrid = new Map()
  const edgeKeys = new Array(m)
  const nodeKey = new Float64Array(n)
  const file = (grid, key, item) => { const list = grid.get(key); if (list) list.push(item); else grid.set(key, [item]) }
  const unfile = (grid, key, item) => { const list = grid.get(key); list[list.indexOf(item)] = list[list.length - 1]; list.pop() }
  function addEdge(e) {
    const a = ea[e], b = eb[e], keys = []
    const x1 = cellOf(Math.max(x[a], x[b])), y0 = cellOf(Math.min(y[a], y[b])), y1 = cellOf(Math.max(y[a], y[b]))
    for (let cx = cellOf(Math.min(x[a], x[b])); cx <= x1; cx++)
      for (let cy = y0; cy <= y1; cy++) { const k = cellKey(cx, cy); file(edgeGrid, k, e); keys.push(k) }
    edgeKeys[e] = keys
  }
  const removeEdge = e => { for (const k of edgeKeys[e]) unfile(edgeGrid, k, e) }
  const addNode = v => { nodeKey[v] = cellKey(cellOf(x[v]), cellOf(y[v])); file(nodeGrid, nodeKey[v], v) }
  const removeNode = v => unfile(nodeGrid, nodeKey[v], v)
  for (let e = 0; e < m; e++) addEdge(e)
  for (let v = 0; v < n; v++) addNode(v)

  // Collect each edge / node filed in the cells covering a box, once, into a
  // shared buffer; returns how many.
  const edgeBuf = new Int32Array(m), nodeBuf = new Int32Array(n)
  const seenEdge = new Int32Array(m), seenNode = new Int32Array(n)
  let query = 0
  function gather(grid, buf, seen, x0, y0, x1, y1) {
    query++
    let count = 0
    const cx1 = cellOf(x1), cy0 = cellOf(y0), cy1 = cellOf(y1)
    for (let cx = cellOf(x0); cx <= cx1; cx++)
      for (let cy = cy0; cy <= cy1; cy++) {
        const list = grid.get(cellKey(cx, cy))
        if (list) for (const item of list) if (seen[item] !== query) { seen[item] = query; buf[count++] = item }
      }
    return count
  }
  const edgesNear = (x0, y0, x1, y1) => gather(edgeGrid, edgeBuf, seenEdge, x0, y0, x1, y1)
  const nodesNear = (x0, y0, x1, y1) => gather(nodeGrid, nodeBuf, seenNode, x0, y0, x1, y1)

  // Cost terms that involve any node in `moved`. Comparing it before and after
  // a move gives the move's effect on the whole layout's cost. Moved nodes and
  // their edges are tested directly (the grid still has them where they were);
  // everything else comes from the grid. Stops early once the cost reaches
  // `limit`, since the caller only wants to know if a move beats that.
  const mark = new Int32Array(m), movedMark = new Int32Array(n)
  let stamp = 0
  function localCost(moved, withLength = true, limit = Infinity) {
    stamp++
    const touched = []
    for (const v of moved) {
      movedMark[v] = stamp
      for (const e of incident[v]) if (mark[e] !== stamp) { mark[e] = stamp; touched.push(e) }
    }
    let cost = 0
    for (let p = 0; p < touched.length; p++) {
      const a = ea[touched[p]], b = eb[touched[p]]
      for (let q = p + 1; q < touched.length; q++) {
        const c = ea[touched[q]], d = eb[touched[q]]
        if (c === a || c === b || d === a || d === b) continue
        if (segmentsCross(x[a], y[a], x[b], y[b], x[c], y[c], x[d], y[d])) cost += W_CROSS
      }
    }
    for (const e of touched) {
      const a = ea[e], b = eb[e]
      const ax = x[a], ay = y[a], bx = x[b], by = y[b]
      const x0 = Math.min(ax, bx), y0 = Math.min(ay, by), x1 = Math.max(ax, bx), y1 = Math.max(ay, by)
      const ne = edgesNear(x0, y0, x1, y1)
      for (let q = 0; q < ne; q++) {
        const f = edgeBuf[q]
        if (mark[f] === stamp) continue
        const c = ea[f], d = eb[f]
        if (c === a || c === b || d === a || d === b) continue
        if (segmentsCross(ax, ay, bx, by, x[c], y[c], x[d], y[d])) cost += W_CROSS
      }
      const nn = nodesNear(x0 - maxClear, y0 - maxClear, x1 + maxClear, y1 + maxClear)
      for (let q = 0; q < nn; q++) {
        const k = nodeBuf[q]
        if (k === a || k === b || movedMark[k] === stamp) continue
        if (pointSegmentDist2(x[k], y[k], ax, ay, bx, by) < clear2[k]) cost += W_OCCLUDE
      }
      for (const v of moved) {
        if (v === a || v === b) continue
        if (pointSegmentDist2(x[v], y[v], ax, ay, bx, by) < clear2[v]) cost += W_OCCLUDE
      }
      if (withLength) {
        const stretch = (Math.sqrt((bx - ax) ** 2 + (by - ay) ** 2) - target) / target
        cost += W_LENGTH * stretch * stretch
      }
      if (cost >= limit) return cost
    }
    // A moved node sitting on someone else's edge.
    for (const v of moved) {
      const r = Math.sqrt(clear2[v])
      const ne = edgesNear(x[v] - r, y[v] - r, x[v] + r, y[v] + r)
      for (let q = 0; q < ne; q++) {
        const f = edgeBuf[q]
        if (mark[f] === stamp) continue
        if (pointSegmentDist2(x[v], y[v], x[ea[f]], y[ea[f]], x[eb[f]], y[eb[f]]) < clear2[v]) cost += W_OCCLUDE
      }
    }
    return cost
  }

  function roomAt(i, px, py) {
    const nn = nodesNear(px - minSep, py - minSep, px + minSep, py + minSep)
    for (let q = 0; q < nn; q++) {
      const k = nodeBuf[q]
      if (k === i) continue
      const dx = x[k] - px, dy = y[k] - py
      if (dx * dx + dy * dy < minSep * minSep) return false
    }
    return true
  }

  // Moves nodes for real, keeping the grid in step.
  function relocate(moves) {
    const moved = new Set(moves.map(([v]) => v))
    const edgesToRefile = new Set()
    for (const v of moved) for (const e of incident[v]) edgesToRefile.add(e)
    for (const e of edgesToRefile) removeEdge(e)
    for (const v of moved) removeNode(v)
    for (const [v, px, py] of moves) { x[v] = px; y[v] = py }
    for (const v of moved) addNode(v)
    for (const e of edgesToRefile) addEdge(e)
  }

  const dist2From = (px, py) => j => (x[j] - px) ** 2 + (y[j] - py) ** 2

  for (let sweep = 0; sweep < MAX_SWEEPS && !outOfTime(); sweep++) {
    let improved = false
    for (let i = 0; i < n && !outOfTime(); i++) {
      if (neighbours[i].length === 0 || localCost([i], false, Number.MIN_VALUE) === 0) continue

      let bestDelta = -1e-9, bestMove = null

      // Move i somewhere new.
      const before = localCost([i])
      const ox = x[i], oy = y[i]
      const fromI = dist2From(ox, oy)
      const spots = []
      const ringAround = [...neighbours[i]].sort((p, q) => fromI(p) - fromI(q)).slice(0, MAX_RING_NEIGHBOURS)
      for (const j of ringAround)
        for (let k = 0; k < RING_ANGLES; k++) {
          const ang = (k / RING_ANGLES) * 2 * Math.PI
          spots.push([x[j] + Math.cos(ang) * target, y[j] + Math.sin(ang) * target])
        }
      if (neighbours[i].length >= 2) {
        let cx = 0, cy = 0
        for (const j of neighbours[i]) { cx += x[j]; cy += y[j] }
        spots.push([cx / neighbours[i].length, cy / neighbours[i].length])
      }
      for (const [px, py] of spots) {
        if (!roomAt(i, px, py)) continue
        x[i] = px; y[i] = py
        const delta = localCost([i], true, before + bestDelta) - before
        x[i] = ox; y[i] = oy
        if (delta < bestDelta) { bestDelta = delta; bestMove = [[i, px, py]] }
      }

      // Trade places with one of the nearest few nodes in the same component.
      const partners = []
      for (let j = 0; j < n; j++) {
        if (j === i || component[j] !== component[i]) continue
        const d2 = fromI(j)
        if (partners.length === SWAP_CANDIDATES && d2 >= partners[SWAP_CANDIDATES - 1][0]) continue
        let at = partners.length
        while (at > 0 && partners[at - 1][0] > d2) at--
        partners.splice(at, 0, [d2, j])
        if (partners.length > SWAP_CANDIDATES) partners.pop()
      }
      for (const [, j] of partners) {
        const pair = [i, j]
        const swapBefore = localCost(pair)
        ;[x[i], x[j]] = [x[j], x[i]]; [y[i], y[j]] = [y[j], y[i]]
        const delta = localCost(pair, true, swapBefore + bestDelta) - swapBefore
        ;[x[i], x[j]] = [x[j], x[i]]; [y[i], y[j]] = [y[j], y[i]]
        if (delta < bestDelta) { bestDelta = delta; bestMove = [[i, x[j], y[j]], [j, x[i], y[i]]] }
      }

      if (bestMove) {
        relocate(bestMove)
        improved = true
      }
      yield
    }
    if (!improved) break
  }

  nodes.forEach((d, i) => { d.x = x[i]; d.y = y[i] })
}

// Ticks a stopped simulation until it comes to rest, yielding every few ticks.
function* settleSteps(simulation) {
  for (let i = 0; i < 1000 && simulation.alpha() >= simulation.alphaMin(); i++) {
    simulation.tick()
    if (i % 5 === 4) yield
  }
}

// settleUntangled's work as a generator, so it can run all at once or a slice per frame.
function* settleUntangledSteps(simulation, nodes, links, { outOfTime, rounds, reheat }) {
  simulation.stop()
  yield* settleSteps(simulation)
  const snapshot = () => nodes.map(d => [d.x, d.y])
  let best = snapshot()
  let bestScore = layoutScore(measureLayout(nodes, links))
  const consider = () => {
    const score = layoutScore(measureLayout(nodes, links))
    if (score < bestScore) { bestScore = score; best = snapshot() }
  }
  for (let r = 0; r < rounds && bestScore > 0 && !outOfTime(); r++) {
    yield* untangleSteps(nodes, links, outOfTime)
    consider()
    if (bestScore === 0 || outOfTime()) break
    simulation.alpha(reheat)
    yield* settleSteps(simulation)
    consider()
  }
  nodes.forEach((d, i) => { [d.x, d.y] = best[i]; d.vx = 0; d.vy = 0 })
  return measureLayout(nodes, links)
}

/**
 * Runs the force simulation to rest without drawing, then alternates
 * untangling with re-settling and keeps whichever layout along the way is
 * least tangled. Re-settling evens out spacing but can pull a fix back apart,
 * so a freshly untangled layout can win too. The simulation is left stopped
 * there, so nothing drifts afterwards; dragging a node restarts it as usual.
 */
export function settleUntangled(simulation, nodes, links, { budgetMs = 250, rounds = 3, reheat = 0.3 } = {}) {
  const deadline = now() + budgetMs
  return runToEnd(settleUntangledSteps(simulation, nodes, links, { outOfTime: () => now() >= deadline, rounds, reheat }))
}

/**
 * settleUntangled spread across animation frames, about `sliceMs` of work per
 * frame, so the page keeps painting and responding while it runs. The budget
 * counts working time only. Node positions are scratch space until `onDone`
 * is called with the result. Returns a function that abandons the work.
 */
export function settleUntangledInFrames(simulation, nodes, links, {
  onDone, budgetMs = 250, sliceMs = 10, rounds = 3, reheat = 0.3,
} = {}) {
  let spent = 0, sliceStart = 0, frame = null
  const outOfTime = () => spent + (now() - sliceStart) >= budgetMs
  const steps = settleUntangledSteps(simulation, nodes, links, { outOfTime, rounds, reheat })
  simulation.stop()
  function work() {
    sliceStart = now()
    let step
    do step = steps.next(); while (!step.done && now() - sliceStart < sliceMs)
    spent += now() - sliceStart
    frame = step.done ? null : requestAnimationFrame(work)
    if (step.done) onDone?.(step.value)
  }
  frame = requestAnimationFrame(work)
  return () => {
    if (frame == null) return
    cancelAnimationFrame(frame)
    frame = null
    steps.return()
  }
}

/**
 * Room to reserve for a connected component of `count` nodes when packing
 * clusters. Settled sparse clusters reach about 0.8–0.9 × linkDistance × √count
 * from their centre to their farthest node; packing at three quarters of that
 * lets their thin fringes interleave a little, which keeps the whole graph
 * compact without clusters running into each other.
 */
export function componentRadius(count, linkDistance, nodeRadius = 0) {
  if (count <= 1) return nodeRadius + linkDistance * 0.15
  return 0.75 * linkDistance * Math.max(0.5, 0.9 * Math.sqrt(count) - 0.5)
}

/**
 * Centres for circles of the given radii, packed around (cx, cy): biggest
 * first, each pushed out from the middle along whichever of a spread of
 * directions keeps the overall footprint squarest, just far enough not to
 * overlap the ones already placed (keeping `gap` between them).
 */
export function packCircles(radii, cx, cy, gap = 0) {
  const DIRECTIONS = 16
  const order = radii.map((_, i) => i).sort((a, b) => radii[b] - radii[a])
  const placed = []
  const centers = new Array(radii.length)
  let box = null   // [minX, minY, maxX, maxY] of everything placed so far
  order.forEach((c, k) => {
    const r = radii[c]
    let best = null
    for (let dir = 0; dir < DIRECTIONS; dir++) {
      // Stagger each circle's directions so equal-sized ones don't stack along one ray.
      const ang = (dir / DIRECTIONS + k * 0.381966) * 2 * Math.PI
      const ux = Math.cos(ang), uy = Math.sin(ang)
      // Distances along this ray that would overlap each placed circle, nearest first.
      const blocked = []
      for (const p of placed) {
        const reach = r + p.r + gap
        const b = ux * (p.x - cx) + uy * (p.y - cy)
        const disc = b * b - ((p.x - cx) ** 2 + (p.y - cy) ** 2 - reach * reach)
        if (disc > 0) blocked.push([b - Math.sqrt(disc), b + Math.sqrt(disc)])
      }
      blocked.sort((a, b) => a[0] - b[0])
      let t = 0
      for (const [lo, hi] of blocked) { if (lo > t) break; if (hi > t) t = hi }
      const x = cx + ux * t, y = cy + uy * t
      const grown = box
        ? [Math.min(box[0], x - r), Math.min(box[1], y - r), Math.max(box[2], x + r), Math.max(box[3], y + r)]
        : [x - r, y - r, x + r, y + r]
      const size = Math.max(grown[2] - grown[0], grown[3] - grown[1])
      if (!best || size < best.size - 1e-6 || (size <= best.size + 1e-6 && t < best.t)) best = { t, x, y, size, grown }
    }
    placed.push({ x: best.x, y: best.y, r })
    box = best.grown
    centers[c] = { x: best.x, y: best.y }
  })
  return centers
}

/**
 * Pulls each connected component's centre toward its spot (`centerOf(node)`)
 * by moving the whole component, so clusters stay packed without being
 * squeezed. A per-node pull (forceX/forceY) crushes each cluster into an
 * evenly filled disc whose placement ignores the edges, which is what kept
 * re-tangling the default layout.
 */
export function forceComponentCenter(componentOf, centerOf, strength = 0.2) {
  let nodes = []
  function force(alpha) {
    const sums = new Map()
    for (const d of nodes) {
      const c = componentOf(d)
      const s = sums.get(c) || { x: 0, y: 0, n: 0, d }
      s.x += d.x; s.y += d.y; s.n++
      sums.set(c, s)
    }
    for (const s of sums.values()) {
      const t = centerOf(s.d)
      s.dx = (t.x - s.x / s.n) * strength * alpha
      s.dy = (t.y - s.y / s.n) * strength * alpha
    }
    for (const d of nodes) {
      const s = sums.get(componentOf(d))
      d.vx += s.dx; d.vy += s.dy
    }
  }
  force.initialize = _ => { nodes = _ }
  return force
}
