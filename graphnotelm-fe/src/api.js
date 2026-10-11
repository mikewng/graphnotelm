const BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000'

// The desktop app shows the UI while its backend is still starting, so requests wait until the
// backend accepts connections. In a browser there's nothing to wait for. A failed wait lets
// requests go ahead and report their own errors.
const backendReady = Promise.resolve(window.electronAPI?.whenBackendReady?.()).catch(() => {})

function getToken() {
  return localStorage.getItem('token')
}

/**
 * Builds the Error for a failed response. Some failures — notably a 404 from
 * routing — have an empty body, and `new Error('')` reaches callers with a blank
 * message that renders as no feedback at all. Always fall back to the status.
 */
async function requestError(res, path) {
  const body = (await res.text()).trim()
  if (body) return new Error(errorMessageFrom(body))
  if (res.status === 404)
    return new Error(`Endpoint not found (${path}). Your backend may be out of date — rebuild it.`)
  return new Error(`Request failed (${res.status}${res.statusText ? ` ${res.statusText}` : ''}).`)
}

// Controllers wrap failures as { success: false, error }, and model-binding failures come
// back as ProblemDetails with a title. Anything else is shown as-is.
function errorMessageFrom(body) {
  try {
    const parsed = JSON.parse(body)
    if (typeof parsed?.error === 'string' && parsed.error) return parsed.error
    if (typeof parsed?.title === 'string' && parsed.title) return parsed.title
  } catch { /* not JSON */ }
  return body
}

// Repeats array values (relationshipIds=a&relationshipIds=b) and skips empty ones.
function toQuery(params) {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value == null || value === '') continue
    if (Array.isArray(value)) value.forEach(v => search.append(key, v))
    else search.append(key, value)
  }
  return search.toString()
}

async function request(path, options = {}) {
  await backendReady
  const token = getToken()
  const res = await fetch(`${BASE_URL}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options.headers,
    },
    ...options,
  })
  if (res.status === 401) {
    localStorage.removeItem('token')
    window.location.hash = '/auth'
    throw new Error('Unauthorized')
  }
  if (!res.ok) throw await requestError(res, path)
  const text = await res.text()
  if (!text) return null
  const body = JSON.parse(text)
  if (body.success === false) throw new Error(body.error || 'Request failed')
  return body.value !== undefined ? body.value : body
}

export const auth = {
  login: (email, password) =>
    request('/UserAuth/login', { method: 'POST', body: JSON.stringify({ email, password }) }),
  register: (username, email, password) =>
    request('/UserAuth/register', { method: 'POST', body: JSON.stringify({ username, email, password }) }),
}

export const accountApi = {
  get:    () => request('/account'),
  // Returns { account, session } — the new session token carries the updated claims.
  update: (data) => request('/account', { method: 'PATCH', body: JSON.stringify(data) }),
  changePassword: (data) =>
    request('/account/password', { method: 'PATCH', body: JSON.stringify(data) }),
  setSessionPreference: (neverExpire) =>
    request('/account/session', { method: 'PATCH', body: JSON.stringify({ neverExpire }) }),
}

export const noteGraphApi = {
  list: () => request('/NoteGraph/list').then(v => v?.graphList ?? []),
  listArchived: () => request('/NoteGraph/archive/list').then(v => v?.graphList ?? []),
  create: (data) => request('/NoteGraph/create', { method: 'POST', body: JSON.stringify(data) }),
  get: (id) => request(`/NoteGraph/id/${id}`),
  delete: (id) => request(`/NoteGraph/delete/${id}`, { method: 'DELETE' }),
  hardDelete: (id) => request(`/NoteGraph/harddelete/${id}`, { method: 'DELETE' }),
  unarchive: (id) => request(`/NoteGraph/unarchive/${id}`, { method: 'PATCH' }),
  editMetadata: (id, data) => request(`/NoteGraph/edit/${id}/metadata`, { method: 'PATCH', body: JSON.stringify(data) }),
  getContext:  (id) => request(`/NoteGraph/id/${id}`),
  editContext: (id, data) => request(`/NoteGraph/edit/${id}/graphcontext`, { method: 'PATCH', body: JSON.stringify(data) }),
  import: (data) => request('/NoteGraph/create/import', { method: 'POST', body: JSON.stringify(data) }),
  createFromExtract: (data) => request('/NoteGraph/create/extract', { method: 'POST', body: JSON.stringify(data) }),
  // Export returns raw content — bypass envelope unwrapping and return a blob for download
  export: async (id) => {
    const token = getToken()
    const res = await fetch(`${BASE_URL}/NoteGraph/id/${id}/export`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    })
    if (res.status === 401) {
      localStorage.removeItem('token')
      window.location.hash = '/auth'
      throw new Error('Unauthorized')
    }
    if (!res.ok) throw await requestError(res, `/NoteGraph/id/${id}/export`)
    return res.blob()
  },
}

export const noteNodeApi = {
  create: (graphId, data) =>
    request(`/NoteGraph/id/${graphId}/node/create`, { method: 'POST', body: JSON.stringify(data) }),
  delete: (graphId, nodeId) =>
    request(`/NoteGraph/id/${graphId}/node/delete/${nodeId}`, { method: 'DELETE' }),

  // Fetch full node content (note + metadata) — skeleton doesn't include these
  get: (graphId, nodeId) =>
    request(`/NoteGraph/id/${graphId}/node/${nodeId}`),

  // Save title + note only
  saveContent: (graphId, nodeId, data) =>
    request(`/NoteGraph/id/${graphId}/node/${nodeId}/content`, { method: 'PATCH', body: JSON.stringify(data) }),

  // Per-tag assignment
  addTag: (graphId, nodeId, tagId) =>
    request(`/NoteGraph/id/${graphId}/node/${nodeId}/tags`, { method: 'POST', body: JSON.stringify({ tagId }) }),
  removeTag: (graphId, nodeId, tagId) =>
    request(`/NoteGraph/id/${graphId}/node/${nodeId}/tags/${tagId}`, { method: 'DELETE' }),

  // Per-relationship assignment
  addRelationship: (graphId, nodeId, targetNodeId, relationshipId) =>
    request(`/NoteGraph/id/${graphId}/node/${nodeId}/relationships`, { method: 'POST', body: JSON.stringify({ targetNodeId, relationshipId }) }),
  removeRelationship: (graphId, nodeId, targetNodeId, relationshipId) =>
    request(`/NoteGraph/id/${graphId}/node/${nodeId}/relationships/${targetNodeId}/${relationshipId}`, { method: 'DELETE' }),

  // Node metadata
  updateMetadata: (graphId, nodeId, data) =>
    request(`/NoteGraph/id/${graphId}/node/${nodeId}/metadata`, { method: 'PATCH', body: JSON.stringify(data) }),

  // LLM-generated metadata analysis
  generateLlmMetadata: (graphId, nodeId) =>
    request(`/NoteGraph/id/${graphId}/node/${nodeId}/metadata/llmanalysis`, { method: 'PATCH' }),

  // Pinning
  getPinned: (graphId) =>
    request(`/NoteGraph/id/${graphId}/nodes/pinned`).then(v => v?.nodes ?? []),
  setPin: (graphId, nodeId, isPinned) =>
    request(`/NoteGraph/id/${graphId}/node/${nodeId}/pin`, { method: 'PATCH', body: JSON.stringify({ isPinned }) }),
  setPinnedMany: (graphId, nodeIds) =>
    request(`/NoteGraph/id/${graphId}/nodes/pin`, { method: 'PATCH', body: JSON.stringify({ nodeIds }) }),
  setUnpinnedMany: (graphId, nodeIds) =>
    request(`/NoteGraph/id/${graphId}/nodes/unpin`, { method: 'PATCH', body: JSON.stringify({ nodeIds }) }),

  // Bulk tag assignment — the backend takes a list of tags; callers apply one at a time
  addTagToNodes: (graphId, tagId, nodeIds) =>
    request(`/NoteGraph/id/${graphId}/nodes/tags`, { method: 'POST', body: JSON.stringify({ tagIds: [tagId], nodeIds }) }),
  removeTagFromNodes: (graphId, tagId, nodeIds) =>
    request(`/NoteGraph/id/${graphId}/nodes/tags`, { method: 'DELETE', body: JSON.stringify({ tagIds: [tagId], nodeIds }) }),

  // Paste raw content and let the LLM create a structured node
  paste: (graphId, pastedContent) =>
    request(`/NoteGraph/id/${graphId}/node/paste`, { method: 'POST', body: JSON.stringify({ pastedContent }) }),

  searchNodes: (graphId, q) =>
    request(`/NoteGraph/id/${graphId}/nodes/search?q=${encodeURIComponent(q)}`).then(v => v?.results ?? []),
}

// Flashcards belong to a note; each side is plain text.
export const flashcardApi = {
  // Every note in the graph with its cards and review status → [{ nodeId, title, confidence, dueAt, reviews, isDue, isNew, cards }]
  listGraph: (graphId) =>
    request(`/NoteGraph/id/${graphId}/flashcards`).then(v => v?.notes ?? []),
  list: (graphId, nodeId) =>
    request(`/NoteGraph/id/${graphId}/node/${nodeId}/flashcards`).then(v => v?.cards ?? []),
  create: (graphId, nodeId, { front, back }) =>
    request(`/NoteGraph/id/${graphId}/node/${nodeId}/flashcards/create`, { method: 'POST', body: JSON.stringify({ front, back }) }),
  edit: (graphId, nodeId, cardId, { front, back }) =>
    request(`/NoteGraph/id/${graphId}/node/${nodeId}/flashcards/edit/${cardId}`, { method: 'PATCH', body: JSON.stringify({ front, back }) }),
  delete: (graphId, nodeId, cardId) =>
    request(`/NoteGraph/id/${graphId}/node/${nodeId}/flashcards/delete/${cardId}`, { method: 'DELETE' }),
}

// Spaced-repetition reviews. Grades are 1 (Again) to 4 (Easy).
export const reviewApi = {
  // → { dueCount, newCount }
  summary: (graphId, newLimit) =>
    request(`/NoteGraph/id/${graphId}/review/summary?${toQuery({ newLimit })}`),
  // → { items: [{ nodeId, title, isNew, dueAt, confidence, cards }], dueCount, newCount }
  queue: (graphId, newLimit) =>
    request(`/NoteGraph/id/${graphId}/review/queue?${toQuery({ newLimit })}`),
  // → { nodeId, confidence, memory }
  submit: (graphId, nodeId, grade, cardId = null) =>
    request(`/NoteGraph/id/${graphId}/node/${nodeId}/review`, { method: 'POST', body: JSON.stringify({ grade, cardId }) }),
}

// Study-path analysis over confidence ratings and relationships. Every call takes an optional
// direction ('Both' | 'Outgoing' | 'Incoming') and relationshipIds (empty = every type).
export const analysisApi = {
  // { startNodeId, targetNodeId } → { path }
  weakestPath: (graphId, params) =>
    request(`/NoteGraph/id/${graphId}/analysis/weakestpath?${toQuery(params)}`),
  // { startNodeId, minConfidence } → { known, frontier }
  frontier: (graphId, params) =>
    request(`/NoteGraph/id/${graphId}/analysis/frontier?${toQuery(params)}`),
  // { targetNodeId } → { order, cycles }; direction must be 'Outgoing' or 'Incoming'
  learningOrder: (graphId, params) =>
    request(`/NoteGraph/id/${graphId}/analysis/learningorder?${toQuery(params)}`),
  // { minConfidence } → { ready }; direction must be 'Outgoing' or 'Incoming'
  readyToLearn: (graphId, params) =>
    request(`/NoteGraph/id/${graphId}/analysis/readytolearn?${toQuery(params)}`),
  // { limit } → { bottlenecks: [{ ...node, dependents }] }; direction must be 'Outgoing' or 'Incoming'
  bottlenecks: (graphId, params) =>
    request(`/NoteGraph/id/${graphId}/analysis/bottlenecks?${toQuery(params)}`),
}

export const imageApi = {
  // Image GETs are anonymous — an <img> can't send the bearer token — and addressed by
  // an unguessable id.
  url: (imageId) => `${BASE_URL}/NoteGraph/images/${encodeURIComponent(imageId)}`,

  // Multipart upload bypasses request(), whose JSON Content-Type would replace the
  // multipart boundary the browser sets. Resolves to { imageId, url }.
  upload: async (graphId, nodeId, file) => {
    const path = `/NoteGraph/id/${graphId}/node/${nodeId}/images`
    const token = getToken()
    const form = new FormData()
    form.append('file', file)

    const res = await fetch(`${BASE_URL}${path}`, {
      method: 'POST',
      headers: token ? { Authorization: `Bearer ${token}` } : {},
      body: form,
    })
    if (res.status === 401) {
      localStorage.removeItem('token')
      window.location.hash = '/auth'
      throw new Error('Unauthorized')
    }

    const text = (await res.text()).trim()
    let body = null
    try { body = text ? JSON.parse(text) : null } catch { /* non-JSON error body */ }
    if (!res.ok || body?.success === false) {
      if (body?.error) throw new Error(body.error)
      throw new Error(text || `Image upload failed (${res.status}${res.statusText ? ` ${res.statusText}` : ''}).`)
    }
    return body?.value ?? body
  },
}

export const llmSettingsApi = {
  getProviders: () => request('/settings/llm/providers'),
  get:          () => request('/settings/llm'),
  update: (data) => request('/settings/llm', { method: 'PATCH', body: JSON.stringify(data) }),
}

export const mcpSettingsApi = {
  get:           () => request('/settings/mcp'),
  toggle:        (isEnabled) => request('/settings/mcp/toggle', { method: 'PATCH', body: JSON.stringify({ isEnabled }) }),
  configureUser: () => request('/settings/mcp/configure-user', { method: 'POST' }),
  regenerateKey: () => request('/settings/mcp/regenerate-key', { method: 'POST' }),
}

// Tag definitions CRUD — tagName + tagColor
export const tagApi = {
  create: (graphId, data) =>
    request(`/NoteGraph/id/${graphId}/tags/create`, { method: 'POST', body: JSON.stringify(data) }),
  edit: (graphId, tagId, data) =>
    request(`/NoteGraph/id/${graphId}/tags/edit/${tagId}`, { method: 'PATCH', body: JSON.stringify(data) }),
  delete: (graphId, tagId) =>
    request(`/NoteGraph/id/${graphId}/tags/delete/${tagId}`, { method: 'DELETE' }),
}

// Folder CRUD + node assignment
export const folderApi = {
  list: (graphId) =>
    request(`/NoteGraph/id/${graphId}/folders`).then(v => v?.folders ?? {}),
  create: (graphId, data) =>
    request(`/NoteGraph/id/${graphId}/folders/create`, { method: 'POST', body: JSON.stringify(data) }),
  edit: (graphId, folderId, data) =>
    request(`/NoteGraph/id/${graphId}/folders/edit/${folderId}`, { method: 'PATCH', body: JSON.stringify(data) }),
  delete: (graphId, folderId) =>
    request(`/NoteGraph/id/${graphId}/folders/delete/${folderId}`, { method: 'DELETE' }),
  moveNode: (graphId, nodeId, folderId) =>
    request(`/NoteGraph/id/${graphId}/node/${nodeId}/folder`, { method: 'PUT', body: JSON.stringify({ folderId }) }),
  moveNodes: (graphId, nodeIds, folderId) =>
    request(`/NoteGraph/id/${graphId}/nodes/folder`, { method: 'PUT', body: JSON.stringify({ nodeIds, folderId }) }),
}

// Relationship type definitions CRUD — type + color + inverse
// relationId passed as query param for delete
export const relationshipApi = {
  create: (graphId, data) =>
    request(`/NoteGraph/id/${graphId}/relationships/create`, { method: 'POST', body: JSON.stringify(data) }),
  edit: (graphId, relationId, data) =>
    request(`/NoteGraph/id/${graphId}/relationships/edit/${relationId}`, { method: 'PATCH', body: JSON.stringify(data) }),
  delete: (graphId, relationId) =>
    request(`/NoteGraph/id/${graphId}/relationships/delete/${relationId}`, { method: 'DELETE' }),
}
