import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { imageApi, analysisApi, flashcardApi, reviewApi } from './api'

function jsonResponse(body, status = 200) {
  return new Response(JSON.stringify(body), { status })
}

describe('imageApi.upload', () => {
  beforeEach(() => {
    localStorage.setItem('token', 'test-token')
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    localStorage.clear()
    window.location.hash = ''
  })

  it('posts the file as multipart form data with the bearer token', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse({ success: true, value: { imageId: 'img-1', url: '/NoteGraph/images/img-1' } }),
    )
    vi.stubGlobal('fetch', fetchMock)
    const file = new File(['png-bytes'], 'shot.png', { type: 'image/png' })

    const result = await imageApi.upload('graph-1', 'node-1', file)

    expect(result).toEqual({ imageId: 'img-1', url: '/NoteGraph/images/img-1' })
    const [url, options] = fetchMock.mock.calls[0]
    expect(url).toMatch(/\/NoteGraph\/id\/graph-1\/node\/node-1\/images$/)
    expect(options.method).toBe('POST')
    expect(options.body).toBeInstanceOf(FormData)
    expect(options.body.get('file').name).toBe('shot.png')
    // No Content-Type: the browser must set it, including the multipart boundary.
    expect(options.headers).toEqual({ Authorization: 'Bearer test-token' })
  })

  it('throws the backend error message for a rejected upload', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      jsonResponse({ success: false, error: 'Unsupported image type. Use PNG, JPEG, GIF, or WebP.' }, 400),
    ))

    await expect(imageApi.upload('g', 'n', new File(['x'], 'a.png')))
      .rejects.toThrow('Unsupported image type. Use PNG, JPEG, GIF, or WebP.')
  })

  it('falls back to the status code when the error body is empty', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 413 })))

    await expect(imageApi.upload('g', 'n', new File(['x'], 'a.png'))).rejects.toThrow('413')
  })

  it('clears the session and redirects to auth on 401', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 401 })))

    await expect(imageApi.upload('g', 'n', new File(['x'], 'a.png'))).rejects.toThrow('Unauthorized')
    expect(localStorage.getItem('token')).toBeNull()
    expect(window.location.hash).toBe('#/auth')
  })
})

describe('imageApi.url', () => {
  it('builds the image URL and encodes the id', () => {
    expect(imageApi.url('abc')).toMatch(/\/NoteGraph\/images\/abc$/)
    expect(imageApi.url('../x')).toMatch(/\/NoteGraph\/images\/\.\.%2Fx$/)
  })
})

describe('analysisApi', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sends filters as query parameters, repeating relationship ids', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ success: true, value: { order: [], cycles: [] } }))
    vi.stubGlobal('fetch', fetchMock)

    const result = await analysisApi.learningOrder('graph-1', {
      targetNodeId: 'node-1',
      direction: 'Incoming',
      relationshipIds: ['rel-a', 'rel-b'],
    })

    expect(result).toEqual({ order: [], cycles: [] })
    const url = new URL(fetchMock.mock.calls[0][0])
    expect(url.pathname).toBe('/NoteGraph/id/graph-1/analysis/learningorder')
    expect(url.searchParams.get('targetNodeId')).toBe('node-1')
    expect(url.searchParams.get('direction')).toBe('Incoming')
    expect(url.searchParams.getAll('relationshipIds')).toEqual(['rel-a', 'rel-b'])
  })

  it('leaves out empty filters', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ success: true, value: { known: [], frontier: [] } }))
    vi.stubGlobal('fetch', fetchMock)

    await analysisApi.frontier('g', { startNodeId: 'n', minConfidence: 3, relationshipIds: [], direction: '' })

    const url = new URL(fetchMock.mock.calls[0][0])
    expect([...url.searchParams.keys()]).toEqual(['startNodeId', 'minConfidence'])
  })

  it('throws the error message from a failed result instead of the raw body', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      jsonResponse({ success: false, error: 'Target node not found in this graph.', value: null }, 400),
    ))

    await expect(analysisApi.weakestPath('g', { startNodeId: 'a', targetNodeId: 'b' }))
      .rejects.toThrow(/^Target node not found in this graph\.$/)
  })
})

describe('flashcardApi and reviewApi', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function stubOk(value) {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ success: true, value }))
    vi.stubGlobal('fetch', fetchMock)
    return fetchMock
  }

  it('lists a note\'s cards', async () => {
    const fetchMock = stubOk({ cards: [{ id: 'c1' }] })

    expect(await flashcardApi.list('g', 'n')).toEqual([{ id: 'c1' }])
    expect(fetchMock.mock.calls[0][0]).toMatch(/\/NoteGraph\/id\/g\/node\/n\/flashcards$/)
  })

  it('edits a card with a PATCH carrying both sides', async () => {
    const fetchMock = stubOk({ id: 'c1' })

    await flashcardApi.edit('g', 'n', 'c1', { front: 'Q', back: 'A' })

    const [url, options] = fetchMock.mock.calls[0]
    expect(url).toMatch(/\/flashcards\/edit\/c1$/)
    expect(options.method).toBe('PATCH')
    expect(JSON.parse(options.body)).toEqual({ front: 'Q', back: 'A' })
  })

  it('asks for the queue with the new-note limit', async () => {
    const fetchMock = stubOk({ items: [], dueCount: 0, newCount: 0 })

    await reviewApi.queue('g', 10)

    const url = new URL(fetchMock.mock.calls[0][0])
    expect(url.pathname).toBe('/NoteGraph/id/g/review/queue')
    expect(url.searchParams.get('newLimit')).toBe('10')
  })

  it('submits a grade, sending a null card for a review without one', async () => {
    const fetchMock = stubOk({ nodeId: 'n', confidence: 8.3 })

    await reviewApi.submit('g', 'n', 3)

    const [url, options] = fetchMock.mock.calls[0]
    expect(url).toMatch(/\/NoteGraph\/id\/g\/node\/n\/review$/)
    expect(JSON.parse(options.body)).toEqual({ grade: 3, cardId: null })
  })
})
