const BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000'

function getToken() {
  return localStorage.getItem('token')
}

async function request(path, options = {}) {
  const token = getToken()
  const res = await fetch(`${BASE_URL}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options.headers,
    },
    ...options,
  })
  if (!res.ok) throw new Error(await res.text())
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

export const noteGraphApi = {
  list: () => request('/NoteGraph/list').then(v => v?.graphList ?? []),
  create: (data) => request('/NoteGraph/create', { method: 'POST', body: JSON.stringify(data) }),
  get: (id) => request(`/NoteGraph/id/${id}`),
  delete: (id) => request(`/NoteGraph/delete/${id}`, { method: 'DELETE' }),
  import: (data) => request('/NoteGraph/create/import', { method: 'POST', body: JSON.stringify(data) }),
  // Export returns raw content — bypass envelope unwrapping and return a blob for download
  export: async (id) => {
    const token = getToken()
    const res = await fetch(`${BASE_URL}/NoteGraph/id/${id}/export`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    })
    if (!res.ok) throw new Error(await res.text())
    return res.blob()
  },
}

export const noteNodeApi = {
  create: (graphId, data) =>
    request(`/NoteGraph/id/${graphId}/node/create`, { method: 'POST', body: JSON.stringify(data) }),
  delete: (graphId, nodeId) =>
    request(`/NoteGraph/id/${graphId}/node/delete/${nodeId}`, { method: 'DELETE' }),

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
