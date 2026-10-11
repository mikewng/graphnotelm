import { describe, it, expect, afterEach } from 'vitest'
import { Editor } from '@tiptap/core'
import StarterKit from '@tiptap/starter-kit'
import { NoteImage } from './NoteImageExtension'
import { imageApi } from '../api'

const editors = []

function createEditor(content) {
  const editor = new Editor({
    element: document.createElement('div'),
    extensions: [StarterKit, NoteImage],
    content,
  })
  editors.push(editor)
  return editor
}

function imgElements(html) {
  const container = document.createElement('div')
  container.innerHTML = html
  return Array.from(container.querySelectorAll('img'))
}

afterEach(() => {
  editors.splice(0).forEach(editor => editor.destroy())
})

describe('NoteImage extension', () => {
  it('rebuilds src from data-image-id using the current API base URL', () => {
    const editor = createEditor('<img data-image-id="abc-123" src="http://old-host:1234/NoteGraph/images/abc-123">')

    const [img] = imgElements(editor.getHTML())

    expect(img.getAttribute('data-image-id')).toBe('abc-123')
    expect(img.getAttribute('src')).toBe(imageApi.url('abc-123'))
  })

  it('keeps the src of images that have no image id', () => {
    const editor = createEditor('<img src="https://example.com/picture.png" alt="external">')

    const [img] = imgElements(editor.getHTML())

    expect(img.getAttribute('src')).toBe('https://example.com/picture.png')
    expect(img.hasAttribute('data-image-id')).toBe(false)
  })

  it('drops base64 data URI images', () => {
    const editor = createEditor('<p>before</p><img src="data:image/png;base64,iVBORw0KGgo="><p>after</p>')

    expect(imgElements(editor.getHTML())).toHaveLength(0)
    expect(editor.getText()).toContain('before')
  })

  it('round-trips an inserted image through saved HTML', () => {
    const editor = createEditor('<p>note</p>')
    editor.commands.insertContent({ type: 'image', attrs: { src: imageApi.url('xyz'), imageId: 'xyz', alt: 'shot.png' } })
    const saved = editor.getHTML()

    const reloaded = createEditor(saved)
    const [img] = imgElements(reloaded.getHTML())

    expect(img.getAttribute('data-image-id')).toBe('xyz')
    expect(img.getAttribute('src')).toBe(imageApi.url('xyz'))
    expect(img.getAttribute('alt')).toBe('shot.png')
  })
})
