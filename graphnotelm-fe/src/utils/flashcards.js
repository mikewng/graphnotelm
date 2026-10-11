import { formatDue } from './review'

// Filters for the graph-wide flashcards screen.
export const FLASHCARD_FILTERS = [
  { id: 'all',   label: 'All' },
  { id: 'due',   label: 'Due' },
  { id: 'new',   label: 'New' },
  { id: 'empty', label: 'No cards' },
]

const IN_FILTER = {
  // Notes that take part in reviews: they have cards, or have been reviewed from their own text.
  all:   n => n.cards.length > 0 || n.dueAt != null,
  due:   n => n.isDue,
  new:   n => n.isNew,
  empty: n => n.cards.length === 0,
}

export function countFlashcardNotes(notes) {
  return {
    cards: notes.reduce((sum, n) => sum + n.cards.length, 0),
    all: notes.filter(IN_FILTER.all).length,
    due: notes.filter(IN_FILTER.due).length,
    new: notes.filter(IN_FILTER.new).length,
    empty: notes.filter(IN_FILTER.empty).length,
  }
}

// Due first, then new, then by next review; never-reviewed notes without cards last.
function reviewRank(n) {
  if (n.isDue) return 0
  if (n.isNew) return 1
  return n.dueAt ? 2 : 3
}

/**
 * Narrows and orders the list. A search matching the note's title keeps all of its cards;
 * otherwise only the cards whose front or back match are kept, and notes left with none drop out.
 */
export function filterFlashcardNotes(notes, { query = '', filter = 'all', sort = 'title' } = {}) {
  const q = query.trim().toLowerCase()
  const inFilter = IN_FILTER[filter] ?? IN_FILTER.all

  const result = []
  for (const note of notes) {
    if (!inFilter(note)) continue
    if (!q || note.title.toLowerCase().includes(q)) {
      result.push(note)
      continue
    }
    const cards = note.cards.filter(c => c.front.toLowerCase().includes(q) || c.back.toLowerCase().includes(q))
    if (cards.length > 0) result.push({ ...note, cards })
  }

  if (sort === 'due') {
    result.sort((a, b) =>
      reviewRank(a) - reviewRank(b)
      || (a.dueAt && b.dueAt ? new Date(a.dueAt) - new Date(b.dueAt) : 0)
      || a.title.localeCompare(b.title, undefined, { sensitivity: 'base' }))
  }
  return result
}

// The short status shown next to a note.
export function noteReviewStatus(note, now = new Date()) {
  if (note.isDue) return { label: 'due now', tone: 'due' }
  if (note.isNew) return { label: 'new', tone: 'new' }
  if (note.dueAt) return { label: `next ${formatDue(note.dueAt, now)}`, tone: 'later' }
  return { label: 'not reviewed', tone: 'none' }
}

// A note after its cards change locally: a never-reviewed note counts as new once it has cards.
export function withCards(note, cards) {
  return { ...note, cards, isNew: note.dueAt == null && cards.length > 0 }
}
