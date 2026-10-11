import { describe, it, expect, vi, afterEach } from 'vitest'
import * as d3 from 'd3'
import {
  measureLayout, untangleLayout, settleUntangled, settleUntangledInFrames,
  forceComponentCenter, componentRadius, packCircles,
} from './graphLayout'

const at = (id, x, y, _r = 8) => ({ id, x, y, _r })
const link = (source, target) => ({ source, target })

describe('measureLayout', () => {
  it('counts edges that cross, but not edges that only share a node', () => {
    const nodes = [at('a', 0, 0), at('b', 100, 100), at('c', 100, 0), at('d', 0, 100)]
    expect(measureLayout(nodes, [link('a', 'b'), link('c', 'd')]).crossings).toBe(1)
    expect(measureLayout(nodes, [link('a', 'b'), link('a', 'c')]).crossings).toBe(0)
  })

  it('counts A→B and B→A as one edge', () => {
    const nodes = [at('a', 0, 0), at('b', 100, 100), at('c', 100, 0), at('d', 0, 100)]
    expect(measureLayout(nodes, [link('a', 'b'), link('b', 'a'), link('c', 'd')]).crossings).toBe(1)
  })

  it('counts an edge passing through a node it does not connect', () => {
    const nodes = [at('a', 0, 0), at('b', 200, 0), at('c', 100, 3)]
    expect(measureLayout(nodes, [link('a', 'b')]).occlusions).toBe(1)
    expect(measureLayout(nodes, [link('a', 'c')]).occlusions).toBe(0)
  })

  it('accepts links whose ends are already resolved to node objects', () => {
    const nodes = [at('a', 0, 0), at('b', 100, 100), at('c', 100, 0), at('d', 0, 100)]
    expect(measureLayout(nodes, [link(nodes[0], nodes[1]), link(nodes[2], nodes[3])]).crossings).toBe(1)
  })
})

describe('untangleLayout', () => {
  it('unfolds a cycle drawn as a bowtie', () => {
    // a-b-c-d-a with b and c swapped, so a-b and c-d cross.
    const nodes = [at('a', 0, 0), at('b', 200, 200), at('c', 200, 0), at('d', 0, 200)]
    const links = [link('a', 'b'), link('b', 'c'), link('c', 'd'), link('d', 'a')]
    expect(measureLayout(nodes, links).crossings).toBe(1)
    untangleLayout(nodes, links)
    expect(measureLayout(nodes, links).crossings).toBe(0)
  })

  it('leaves a layout with nothing to fix where it is', () => {
    const nodes = [at('a', 0, 0), at('b', 200, 0), at('c', 200, 200), at('d', 0, 200)]
    const links = [link('a', 'b'), link('b', 'c'), link('c', 'd'), link('d', 'a')]
    const before = nodes.map(d => [d.x, d.y])
    untangleLayout(nodes, links)
    expect(nodes.map(d => [d.x, d.y])).toEqual(before)
  })
})

// A star of stars, started from a scrambled ring so the forces have work to do.
function scrambledTree() {
  const ids = ['hub', ...Array.from({ length: 4 }, (_, i) => `m${i}`), ...Array.from({ length: 12 }, (_, i) => `l${i}`)]
  const links = [
    ...[0, 1, 2, 3].map(i => link('hub', `m${i}`)),
    ...Array.from({ length: 12 }, (_, i) => link(`m${i % 4}`, `l${i}`)),
  ]
  const nodes = ids.map((id, i) => at(id, Math.cos(i * 2.4) * 300, Math.sin(i * 2.4) * 300))
  const simulation = d3.forceSimulation(nodes).alphaMin(0.01).alpha(0.3)
    .force('link', d3.forceLink(links).id(d => d.id).distance(240).strength(0.8))
    .force('charge', d3.forceManyBody().strength(-210))
  return { nodes, links, simulation }
}

describe('settleUntangled', () => {
  it('settles a tree with no crossings and leaves the simulation stopped', () => {
    const { nodes, links, simulation } = scrambledTree()
    const result = settleUntangled(simulation, nodes, links)
    expect(result.crossings).toBe(0)
    expect(nodes.every(d => Number.isFinite(d.x) && Number.isFinite(d.y))).toBe(true)
    const alpha = simulation.alpha()
    return new Promise(resolve => setTimeout(() => { expect(simulation.alpha()).toBe(alpha); resolve() }, 50))
  })
})

describe('settleUntangledInFrames', () => {
  // Frames run only when the test says so.
  let frames
  function stubFrames() {
    frames = new Map()
    let next = 1
    vi.stubGlobal('requestAnimationFrame', cb => { frames.set(next, cb); return next++ })
    vi.stubGlobal('cancelAnimationFrame', id => { frames.delete(id) })
  }
  function runFrame() {
    const [id, cb] = frames.entries().next().value
    frames.delete(id)
    cb()
  }
  afterEach(() => vi.unstubAllGlobals())

  it('works a slice per frame and reports the untangled layout when done', () => {
    stubFrames()
    const { nodes, links, simulation } = scrambledTree()
    const onDone = vi.fn()
    settleUntangledInFrames(simulation, nodes, links, { onDone, sliceMs: 0 })
    expect(onDone).not.toHaveBeenCalled()   // nothing runs until the first frame
    let count = 0
    while (frames.size > 0 && count < 10000) { runFrame(); count++ }
    expect(count).toBeGreaterThan(1)        // a zero-length slice yields every step
    expect(onDone).toHaveBeenCalledTimes(1)
    expect(onDone.mock.calls[0][0].crossings).toBe(0)
    expect(measureLayout(nodes, links).crossings).toBe(0)
  })

  it('stops when cancelled and never reports', () => {
    stubFrames()
    const { nodes, links, simulation } = scrambledTree()
    const onDone = vi.fn()
    const cancel = settleUntangledInFrames(simulation, nodes, links, { onDone, sliceMs: 0 })
    runFrame()
    cancel()
    expect(frames.size).toBe(0)
    expect(onDone).not.toHaveBeenCalled()
  })
})

describe('forceComponentCenter', () => {
  it('moves each component as a whole toward its centre', () => {
    const nodes = [at('a', 0, 0), at('b', 100, 0), at('c', 500, 500)]
    nodes.forEach(d => { d.vx = 0; d.vy = 0 })
    const componentOf = d => (d.id === 'c' ? 1 : 0)
    const centers = [{ x: 1000, y: 0 }, { x: 500, y: 500 }]
    const force = forceComponentCenter(componentOf, d => centers[componentOf(d)], 0.5)
    force.initialize(nodes)
    force(1)
    expect(nodes[0].vx).toBeCloseTo(475)   // (1000 - 50) * 0.5
    expect(nodes[1].vx).toBeCloseTo(475)   // same push, so a and b keep their spacing
    expect(nodes[2].vx).toBeCloseTo(0)     // already on its centre
  })
})

describe('packCircles', () => {
  it('puts the biggest circle in the middle and overlaps none', () => {
    const radii = [50, 300, 120, 120, 30, 30, 30]
    const gap = 20
    const centers = packCircles(radii, 400, 300, gap)
    expect(centers[1]).toEqual({ x: 400, y: 300 })
    for (let i = 0; i < radii.length; i++)
      for (let j = i + 1; j < radii.length; j++) {
        const dist = Math.hypot(centers[i].x - centers[j].x, centers[i].y - centers[j].y)
        expect(dist).toBeGreaterThanOrEqual(radii[i] + radii[j] + gap - 1e-6)
      }
  })
})

describe('componentRadius', () => {
  it('reserves more room for bigger clusters', () => {
    const sizes = [1, 2, 5, 20, 80].map(n => componentRadius(n, 240, 8))
    sizes.slice(1).forEach((r, i) => expect(r).toBeGreaterThan(sizes[i]))
  })
})
