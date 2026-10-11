import { describe, it, expect } from 'vitest'
import { filterFlashcardNotes, countFlashcardNotes, noteReviewStatus, withCards } from './flashcards'

const card = (front, back = 'answer') => ({ id: front, front, back })

const notes = [
  { nodeId: 'a', title: 'Alpha', dueAt: '2026-03-05T00:00:00Z', isDue: false, isNew: false, cards: [card('Pods'), card('Nodes', 'machines')] },
  { nodeId: 'b', title: 'beta', dueAt: null, isDue: false, isNew: true, cards: [card('Services')] },
  { nodeId: 'c', title: 'Gamma', dueAt: '2026-02-20T00:00:00Z', isDue: true, isNew: false, cards: [] },
  { nodeId: 'd', title: 'Delta', dueAt: null, isDue: false, isNew: false, cards: [] },
]

const ids = list => list.map(n => n.nodeId)

describe('filterFlashcardNotes', () => {
  it('shows notes that take part in reviews under All', () => {
    expect(ids(filterFlashcardNotes(notes))).toEqual(['a', 'b', 'c'])
  })

  it('filters by review status and by missing cards', () => {
    expect(ids(filterFlashcardNotes(notes, { filter: 'due' }))).toEqual(['c'])
    expect(ids(filterFlashcardNotes(notes, { filter: 'new' }))).toEqual(['b'])
    expect(ids(filterFlashcardNotes(notes, { filter: 'empty' }))).toEqual(['c', 'd'])
  })

  it('keeps every card when the search matches the note title', () => {
    const [alpha] = filterFlashcardNotes(notes, { query: 'ALPH' })

    expect(alpha.cards).toHaveLength(2)
  })

  it('keeps only matching cards when the search matches card text', () => {
    const result = filterFlashcardNotes(notes, { query: 'machines' })

    expect(ids(result)).toEqual(['a'])
    expect(result[0].cards.map(c => c.front)).toEqual(['Nodes'])
  })

  it('sorts by next review: due, then new, then soonest, then unreviewed', () => {
    const result = filterFlashcardNotes(notes, { filter: 'empty', sort: 'due' })
    expect(ids(result)).toEqual(['c', 'd'])

    expect(ids(filterFlashcardNotes(notes, { sort: 'due' }))).toEqual(['c', 'b', 'a'])
  })
})

describe('countFlashcardNotes', () => {
  it('counts cards and notes per filter', () => {
    expect(countFlashcardNotes(notes)).toEqual({ cards: 3, all: 3, due: 1, new: 1, empty: 2 })
  })
})

describe('noteReviewStatus', () => {
  const now = new Date('2026-03-01T00:00:00Z')

  it('describes where the note is in its review cycle', () => {
    expect(noteReviewStatus(notes[2], now)).toEqual({ label: 'due now', tone: 'due' })
    expect(noteReviewStatus(notes[1], now)).toEqual({ label: 'new', tone: 'new' })
    expect(noteReviewStatus(notes[0], now)).toEqual({ label: 'next in 4 days', tone: 'later' })
    expect(noteReviewStatus(notes[3], now)).toEqual({ label: 'not reviewed', tone: 'none' })
  })
})

describe('withCards', () => {
  it('marks a never-reviewed note new only while it has cards', () => {
    expect(withCards(notes[3], [card('Q')]).isNew).toBe(true)
    expect(withCards(notes[1], []).isNew).toBe(false)
    expect(withCards(notes[0], []).isNew).toBe(false)
  })
})
