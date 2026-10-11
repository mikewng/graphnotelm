import { describe, it, expect } from 'vitest'
import { noteSnippet } from './NodePreview'

describe('noteSnippet', () => {
  it('flattens editor HTML into spaced plain text', () => {
    expect(noteSnippet('<p>First <strong>para</strong></p><ul><li><p>one</p></li><li><p>two</p></li></ul>'))
      .toBe('First para one two')
  })

  it('truncates long notes with an ellipsis', () => {
    expect(noteSnippet(`<p>${'word '.repeat(20)}</p>`, 12)).toBe('word word wo…')
  })

  it('returns empty text for empty or image-only notes', () => {
    expect(noteSnippet('')).toBe('')
    expect(noteSnippet(undefined)).toBe('')
    expect(noteSnippet('<p><img src="x.png"></p>')).toBe('')
  })
})
