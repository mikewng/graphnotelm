import { describe, it, expect } from 'vitest'
import { pickCard, noteToPlainText, formatDue, displayConfidence } from './review'

describe('pickCard', () => {
  const cards = [{ id: 'a' }, { id: 'b' }, { id: 'c' }]

  it('returns null when there are no cards', () => {
    expect(pickCard([])).toBeNull()
    expect(pickCard(undefined)).toBeNull()
  })

  it('maps the random draw onto the cards, including the edges', () => {
    expect(pickCard(cards, () => 0).id).toBe('a')
    expect(pickCard(cards, () => 0.5).id).toBe('b')
    expect(pickCard(cards, () => 0.9999).id).toBe('c')
    expect(pickCard(cards, () => 1).id).toBe('c')
  })
})

describe('noteToPlainText', () => {
  it('keeps paragraph and line breaks and drops markup', () => {
    const html = '<h2>Pods</h2><p>The <strong>smallest</strong> unit.</p><ul><li>one</li><li>two</li></ul><p>a<br>b</p>'

    expect(noteToPlainText(html)).toBe('Pods\nThe smallest unit.\none\ntwo\na\nb')
  })

  it('never runs or keeps markup from the note', () => {
    const text = noteToPlainText('<p>Hi<img src="x" onerror="window.__ran = true"><script>window.__ran = true</script></p>')

    expect(text).toBe('Hi')
    expect(window.__ran).toBeUndefined()
  })

  it('returns an empty string for an empty note', () => {
    expect(noteToPlainText('')).toBe('')
    expect(noteToPlainText(null)).toBe('')
  })
})

describe('formatDue', () => {
  const now = new Date('2026-03-01T09:00:00Z')
  const after = ms => new Date(now.getTime() + ms).toISOString()
  const MIN = 60_000, HOUR = 60 * MIN, DAY = 24 * HOUR

  it('describes how far off the next review is', () => {
    expect(formatDue(after(-5 * MIN), now)).toBe('now')
    expect(formatDue(after(10 * MIN), now)).toBe('in 10 minutes')
    expect(formatDue(after(1 * MIN), now)).toBe('in 1 minute')
    expect(formatDue(after(5 * HOUR), now)).toBe('in 5 hours')
    expect(formatDue(after(1 * DAY), now)).toBe('in 1 day')
    expect(formatDue(after(4 * DAY), now)).toBe('in 4 days')
    expect(formatDue(after(120 * DAY), now)).toBe('in 4 months')
  })
})

describe('displayConfidence', () => {
  it('prefers measured confidence, then the self-rating', () => {
    expect(displayConfidence({ confidence: 6.4, metadata: { userConfidenceRate: 9 } })).toBe(6.4)
    expect(displayConfidence({ metadata: { userConfidenceRate: 9 } })).toBe(9)
    expect(displayConfidence({ confidence: 0, metadata: { userConfidenceRate: 9 } })).toBe(0)
    expect(displayConfidence(null)).toBeNull()
  })
})
