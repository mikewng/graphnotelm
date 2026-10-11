// Shared by the review session, the flashcard editor and the confidence displays.

// New (never-reviewed) notes introduced per session; matches the backend default.
export const NEW_PER_SESSION = 10

// FSRS grades — the value is what the backend expects, the key is the keyboard shortcut.
export const GRADES = [
  { value: 1, label: 'Again', key: '1' },
  { value: 2, label: 'Hard',  key: '2' },
  { value: 3, label: 'Good',  key: '3' },
  { value: 4, label: 'Easy',  key: '4' },
]

// A note is reviewed through one of its cards, picked at random so every card gets asked over time.
export function pickCard(cards, random = Math.random) {
  if (!cards?.length) return null
  return cards[Math.min(cards.length - 1, Math.floor(random() * cards.length))]
}

const BLOCK_TAGS = 'p, div, li, h1, h2, h3, h4, h5, h6, blockquote, pre, br'

/**
 * Plain text of a note's HTML, keeping paragraph breaks. Parsed into an inert document,
 * so nothing in the note runs or loads — notes written by the AI can contain raw HTML.
 */
export function noteToPlainText(html) {
  if (!html) return ''
  const doc = new DOMParser().parseFromString(html, 'text/html')
  doc.body.querySelectorAll('script, style').forEach(el => el.remove())
  doc.body.querySelectorAll(BLOCK_TAGS).forEach(el => el.after(doc.createTextNode('\n')))
  return doc.body.textContent.replace(/[ \t]+\n/g, '\n').replace(/\n{3,}/g, '\n\n').trim()
}

// "in 10 minutes", "in 4 days" — when a note is next due.
export function formatDue(dueAt, now = new Date()) {
  const minutes = Math.round((new Date(dueAt) - now) / 60000)
  if (minutes <= 0) return 'now'
  const plural = (n, unit) => `in ${n} ${unit}${n === 1 ? '' : 's'}`
  if (minutes < 60) return plural(minutes, 'minute')
  const hours = Math.round(minutes / 60)
  if (hours < 24) return plural(hours, 'hour')
  const days = Math.round(hours / 24)
  if (days < 60) return plural(days, 'day')
  return plural(Math.round(days / 30), 'month')
}

// What the graph, editor and study path show: measured once reviewed, the self-rating before that.
export function displayConfidence(node) {
  return node?.confidence ?? node?.metadata?.userConfidenceRate ?? null
}
