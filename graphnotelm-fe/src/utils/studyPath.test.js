import { describe, it, expect, afterEach } from 'vitest'
import { buildStudyHighlight, edgeKey, loadStudySettings, saveStudySettings } from './studyPath'

const PREREQ = 'rel-prereq'
const RELATED = 'rel-related'

function node(id, ...relationships) {
  return { id, title: id, relationships: relationships.map(([targetNodeId, relationshipId = PREREQ]) => ({ targetNodeId, relationshipId })) }
}

const summary = id => ({ id, title: id, userConfidenceRate: 0 })

describe('buildStudyHighlight', () => {
  it('returns null when there is no result or nothing in it', () => {
    expect(buildStudyHighlight('order', null, [])).toBeNull()
    expect(buildStudyHighlight('path', { path: [] }, [])).toBeNull()
  })

  it('numbers a learning order, flags cycles, and keeps edges of the chosen type', () => {
    const nodes = [
      node('a', ['b'], ['c', RELATED]),
      node('b', ['a'], ['c']),
      node('c'),
      node('outside', ['a']),
    ]
    const result = {
      order: [summary('a'), summary('b'), summary('c')],
      cycles: [[summary('a'), summary('b')]],
    }

    const highlight = buildStudyHighlight('order', result, nodes, PREREQ)

    // Cycle members keep their step in the order.
    expect([...highlight.steps]).toEqual([['a', 1], ['b', 2], ['c', 3]])
    expect(highlight.roles.get('a')).toBe('warn')
    expect(highlight.roles.get('b')).toBe('warn')
    expect(highlight.roles.get('c')).toBe('primary')
    expect(highlight.roles.has('outside')).toBe(false)
    expect([...highlight.edges].sort()).toEqual([
      edgeKey('a', 'b', PREREQ),
      edgeKey('b', 'a', PREREQ),
      edgeKey('b', 'c', PREREQ),
    ].sort())
  })

  it('keeps every relationship type when no type is chosen', () => {
    const nodes = [node('a', ['b', RELATED]), node('b')]

    const highlight = buildStudyHighlight('order', { order: [summary('a'), summary('b')], cycles: [] }, nodes)

    expect([...highlight.edges]).toEqual([edgeKey('a', 'b', RELATED)])
  })

  it('numbers bottlenecks by rank', () => {
    const nodes = [node('base', ['mid']), node('mid', ['top']), node('top')]
    const result = { bottlenecks: [{ ...summary('base'), dependents: 2 }, { ...summary('mid'), dependents: 1 }] }

    const highlight = buildStudyHighlight('bottlenecks', result, nodes)

    expect([...highlight.steps]).toEqual([['base', 1], ['mid', 2]])
    expect(highlight.roles.has('top')).toBe(false)
    expect([...highlight.edges]).toEqual([edgeKey('base', 'mid', PREREQ)])
  })

  it('marks notes ready to learn without numbering them', () => {
    const nodes = [node('known', ['next']), node('next')]

    const highlight = buildStudyHighlight('ready', { ready: [summary('next')] }, nodes)

    expect(highlight.roles.get('next')).toBe('primary')
    expect(highlight.roles.has('known')).toBe(false)
    expect(highlight.steps.size).toBe(0)
  })

  it('marks the frontier as the answer and only draws edges that leave known nodes', () => {
    const nodes = [node('known', ['next']), node('next', ['other']), node('other')]
    const result = { known: [summary('known')], frontier: [summary('next'), summary('other')] }

    const highlight = buildStudyHighlight('frontier', result, nodes)

    expect(highlight.roles.get('known')).toBe('secondary')
    expect(highlight.roles.get('next')).toBe('primary')
    expect([...highlight.edges]).toEqual([edgeKey('known', 'next', PREREQ)])
    expect(highlight.steps.size).toBe(0)
  })

  it('draws only the edges between neighbouring steps of a path, in either direction', () => {
    // Start → Middle is stored forward, Target → Middle backward, plus a shortcut the path skipped.
    const nodes = [node('start', ['middle'], ['target']), node('middle'), node('target', ['middle'])]
    const result = { path: [summary('start'), summary('middle'), summary('target')] }

    const highlight = buildStudyHighlight('path', result, nodes)

    expect([...highlight.edges].sort()).toEqual([
      edgeKey('start', 'middle', PREREQ),
      edgeKey('target', 'middle', PREREQ),
    ].sort())
  })
})

describe('study settings', () => {
  afterEach(() => localStorage.clear())

  it('round-trips settings per graph', () => {
    saveStudySettings('g1', { mode: 'frontier', minConfidence: 5 })

    expect(loadStudySettings('g1')).toEqual({ mode: 'frontier', minConfidence: 5 })
    expect(loadStudySettings('g2')).toEqual({})
  })

  it('ignores unreadable stored settings', () => {
    localStorage.setItem('graphnotelm:studyPath:g1', '{not json')

    expect(loadStudySettings('g1')).toEqual({})
  })
})
