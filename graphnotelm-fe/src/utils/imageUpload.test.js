import { describe, it, expect, vi, beforeEach } from 'vitest'

vi.mock('../api', () => ({
  imageApi: {
    upload: vi.fn(),
    url: id => `http://api.test/NoteGraph/images/${id}`,
  },
}))

import { imageApi } from '../api'
import {
  getImageFiles,
  validateImageFile,
  uploadAndInsertImages,
  MAX_IMAGE_BYTES,
} from './imageUpload'

function makeFile(name, type, size = 16) {
  const file = new File(['x'], name, { type })
  Object.defineProperty(file, 'size', { value: size })
  return file
}

function makeEditor() {
  const chain = {
    focus: vi.fn(() => chain),
    insertContent: vi.fn(() => chain),
    run: vi.fn(() => true),
  }
  return { isDestroyed: false, chain: vi.fn(() => chain), chainApi: chain }
}

function deferred() {
  let resolve
  const promise = new Promise(r => { resolve = r })
  return { promise, resolve }
}

describe('getImageFiles', () => {
  it('keeps only image files', () => {
    const png = makeFile('a.png', 'image/png')
    const svg = makeFile('b.svg', 'image/svg+xml')
    const text = makeFile('c.txt', 'text/plain')

    expect(getImageFiles([png, text, svg])).toEqual([png, svg])
  })

  it('handles a missing file list', () => {
    expect(getImageFiles(undefined)).toEqual([])
    expect(getImageFiles(null)).toEqual([])
  })
})

describe('validateImageFile', () => {
  it.each(['image/png', 'image/jpeg', 'image/gif', 'image/webp'])('accepts %s', type => {
    expect(validateImageFile(makeFile('img', type))).toBeNull()
  })

  it.each(['image/svg+xml', 'image/bmp', 'image/tiff', ''])('rejects %s', type => {
    expect(validateImageFile(makeFile('img', type))).toMatch(/unsupported image type/)
  })

  it('accepts a file exactly at the size limit', () => {
    expect(validateImageFile(makeFile('big.png', 'image/png', MAX_IMAGE_BYTES))).toBeNull()
  })

  it('rejects a file over the size limit', () => {
    expect(validateImageFile(makeFile('big.png', 'image/png', MAX_IMAGE_BYTES + 1))).toMatch(/10 MB limit/)
  })
})

describe('uploadAndInsertImages', () => {
  beforeEach(() => {
    imageApi.upload.mockReset()
  })

  it('uploads each file and inserts the images in their original order', async () => {
    const first = deferred()
    const second = deferred()
    imageApi.upload
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise)
    const editor = makeEditor()
    const context = { graphId: 'g1', nodeId: 'n1', onError: vi.fn() }

    const done = uploadAndInsertImages(editor, [makeFile('one.png', 'image/png'), makeFile('two.png', 'image/png')], () => context)
    // The second upload finishing first must not reorder the inserted images.
    second.resolve({ imageId: 'id-2' })
    first.resolve({ imageId: 'id-1' })

    expect(await done).toBe(2)
    expect(imageApi.upload).toHaveBeenCalledWith('g1', 'n1', expect.any(File))
    expect(editor.chainApi.insertContent).toHaveBeenCalledWith([
      { type: 'image', attrs: { src: 'http://api.test/NoteGraph/images/id-1', imageId: 'id-1', alt: 'one.png' } },
      { type: 'image', attrs: { src: 'http://api.test/NoteGraph/images/id-2', imageId: 'id-2', alt: 'two.png' } },
    ])
    expect(context.onError).not.toHaveBeenCalled()
  })

  it('reports failed uploads and still inserts the successful ones', async () => {
    imageApi.upload
      .mockResolvedValueOnce({ imageId: 'ok' })
      .mockRejectedValueOnce(new Error('Unsupported image type. Use PNG, JPEG, GIF, or WebP.'))
    const editor = makeEditor()
    const onError = vi.fn()

    const inserted = await uploadAndInsertImages(
      editor,
      [makeFile('good.png', 'image/png'), makeFile('bad.png', 'image/png')],
      () => ({ graphId: 'g1', nodeId: 'n1', onError }),
    )

    expect(inserted).toBe(1)
    expect(onError).toHaveBeenCalledWith('Unsupported image type. Use PNG, JPEG, GIF, or WebP.')
    expect(editor.chainApi.insertContent).toHaveBeenCalledWith([
      expect.objectContaining({ attrs: expect.objectContaining({ imageId: 'ok' }) }),
    ])
  })

  it('does not upload files that fail the client-side check', async () => {
    const editor = makeEditor()
    const onError = vi.fn()

    const inserted = await uploadAndInsertImages(
      editor,
      [makeFile('vector.svg', 'image/svg+xml')],
      () => ({ graphId: 'g1', nodeId: 'n1', onError }),
    )

    expect(inserted).toBe(0)
    expect(imageApi.upload).not.toHaveBeenCalled()
    expect(onError).toHaveBeenCalledWith(expect.stringMatching(/vector\.svg: unsupported image type/))
    expect(editor.chain).not.toHaveBeenCalled()
  })

  it('skips insertion when the editor switched to another node during the upload', async () => {
    const upload = deferred()
    imageApi.upload.mockReturnValueOnce(upload.promise)
    const editor = makeEditor()
    const context = { graphId: 'g1', nodeId: 'n1', onError: vi.fn() }

    const done = uploadAndInsertImages(editor, [makeFile('a.png', 'image/png')], () => context)
    context.nodeId = 'n2'
    upload.resolve({ imageId: 'late' })

    expect(await done).toBe(0)
    expect(editor.chain).not.toHaveBeenCalled()
  })

  it('skips insertion when the editor was destroyed during the upload', async () => {
    imageApi.upload.mockResolvedValueOnce({ imageId: 'late' })
    const editor = makeEditor()
    editor.isDestroyed = true

    const inserted = await uploadAndInsertImages(
      editor,
      [makeFile('a.png', 'image/png')],
      () => ({ graphId: 'g1', nodeId: 'n1' }),
    )

    expect(inserted).toBe(0)
    expect(editor.chain).not.toHaveBeenCalled()
  })
})
