import { useState, useMemo } from 'react'
import SearchSelect from './SearchSelect'

// Table view over all nodes in a graph — supports multi-select for bulk
// tag assignment, and per-node tag removal via the bulk delete endpoint.
// Built so additional bulk-action columns/toolbar groups can be dropped in later.
export default function NodeTableView({
  nodesList,
  tagsList,
  tagDefs,
  pinnedNodeIds = new Set(),
  onAddTagToNodes,
  onRemoveTagFromNodes,
  onSetPinnedMany,
  onRequestConfirm,
}) {
  const [search, setSearch] = useState('')
  const [selectedNodeIds, setSelectedNodeIds] = useState(new Set())
  const [tagsToDelete, setTagsToDelete] = useState(new Map()) // tagId -> Set(nodeId)
  const [addTagId, setAddTagId] = useState('')
  const [busy, setBusy] = useState(false)

  const rows = useMemo(() => {
    const list = nodesList.filter(n => n.id !== '__draft__')
    if (!search.trim()) return list
    const q = search.trim().toLowerCase()
    return list.filter(n => (n.title || '').toLowerCase().includes(q))
  }, [nodesList, search])

  const tagDeleteCount = useMemo(
    () => [...tagsToDelete.values()].reduce((sum, set) => sum + set.size, 0),
    [tagsToDelete]
  )

  const allSelected = rows.length > 0 && rows.every(n => selectedNodeIds.has(n.id))

  function toggleSelectAll() {
    setSelectedNodeIds(prev => {
      if (allSelected) return new Set()
      const next = new Set(prev)
      rows.forEach(n => next.add(n.id))
      return next
    })
  }

  function toggleSelectNode(nodeId) {
    setSelectedNodeIds(prev => {
      const next = new Set(prev)
      if (next.has(nodeId)) next.delete(nodeId)
      else next.add(nodeId)
      return next
    })
  }

  function toggleTagForDeletion(nodeId, tagId) {
    setTagsToDelete(prev => {
      const next = new Map(prev)
      const set = new Set(next.get(tagId) || [])
      if (set.has(nodeId)) set.delete(nodeId)
      else set.add(nodeId)
      if (set.size === 0) next.delete(tagId)
      else next.set(tagId, set)
      return next
    })
  }

  async function handleAddTag() {
    if (!addTagId || selectedNodeIds.size === 0 || busy) return
    setBusy(true)
    try {
      await onAddTagToNodes(addTagId, [...selectedNodeIds])
      setAddTagId('')
      // Selection is preserved intentionally so more tags can be added.
    } finally {
      setBusy(false)
    }
  }

  function handleDeleteTagsClick() {
    if (tagDeleteCount === 0 || busy) return
    const tagNames = [...tagsToDelete.keys()].map(tagId => tagDefs[tagId]?.name || 'tag')
    onRequestConfirm({
      title: 'Remove tags',
      message: `Remove ${tagDeleteCount} tag assignment${tagDeleteCount === 1 ? '' : 's'} (${tagNames.join(', ')}) from the selected nodes?`,
      confirmLabel: 'Remove',
      danger: true,
      onConfirm: async () => {
        setBusy(true)
        try {
          for (const [tagId, nodeIdSet] of tagsToDelete) {
            await onRemoveTagFromNodes(tagId, [...nodeIdSet])
          }
          setTagsToDelete(new Map())
        } finally {
          setBusy(false)
        }
      },
    })
  }

  async function handlePinSelected(isPinned) {
    if (selectedNodeIds.size === 0 || busy) return
    setBusy(true)
    try {
      await onSetPinnedMany([...selectedNodeIds], isPinned)
    } finally {
      setBusy(false)
    }
  }

  const tagOptions = tagsList.map(t => ({ id: t.id, label: t.name }))

  return (
    <div className="table-view">
      <div className="table-view-toolbar">
        <input
          className="table-view-search"
          type="text"
          placeholder="Filter by title…"
          value={search}
          onChange={e => setSearch(e.target.value)}
        />
        <span className="table-view-selection-count">
          {selectedNodeIds.size} node{selectedNodeIds.size === 1 ? '' : 's'} selected
        </span>

        <div className="table-view-toolbar-group">
          <SearchSelect
            value={addTagId}
            onChange={setAddTagId}
            options={tagOptions}
            placeholder="Select tag…"
          />
          <button onClick={handleAddTag} disabled={!addTagId || selectedNodeIds.size === 0 || busy}>
            + Add Tag to Selected
          </button>
        </div>

        <div className="table-view-toolbar-group">
          <button onClick={() => handlePinSelected(true)} disabled={selectedNodeIds.size === 0 || busy}>
            Pin Selected
          </button>
          <button onClick={() => handlePinSelected(false)} disabled={selectedNodeIds.size === 0 || busy}>
            Unpin Selected
          </button>
        </div>

        <div className="table-view-toolbar-group">
          <button className="btn-danger" onClick={handleDeleteTagsClick} disabled={tagDeleteCount === 0 || busy}>
            Remove {tagDeleteCount > 0 ? `${tagDeleteCount} ` : ''}Selected Tag{tagDeleteCount === 1 ? '' : 's'}
          </button>
        </div>
      </div>

      <div className="table-view-scroll">
        <table className="node-table">
          <thead>
            <tr>
              <th className="node-table-checkbox-col">
                <input
                  type="checkbox"
                  className="node-table-checkbox"
                  checked={allSelected}
                  onChange={toggleSelectAll}
                />
              </th>
              <th>Title</th>
              <th className="node-table-pin-col">Pinned</th>
              <th>Tags</th>
            </tr>
          </thead>
          <tbody>
            {rows.map(n => (
              <tr
                key={n.id}
                className={selectedNodeIds.has(n.id) ? 'selected' : ''}
                onClick={() => toggleSelectNode(n.id)}
              >
                <td className="node-table-checkbox-col">
                  <input
                    type="checkbox"
                    className="node-table-checkbox"
                    checked={selectedNodeIds.has(n.id)}
                    onChange={() => toggleSelectNode(n.id)}
                    onClick={e => e.stopPropagation()}
                  />
                </td>
                <td className="node-table-title">
                  {n.title || '(untitled)'}
                </td>
                <td className="node-table-pin-col">
                  {pinnedNodeIds.has(n.id) && (
                    <span className="pin-indicator" title="Pinned">
                      <svg viewBox="0 0 16 16" fill="currentColor" width="11" height="11">
                        <path d="M9.828.722a.5.5 0 0 1 .354.146l4.95 4.95a.5.5 0 0 1 0 .707c-.48.48-1.072.588-1.503.588-.177 0-.335-.018-.46-.039l-3.134 3.134a5.927 5.927 0 0 1 .16 1.013c.046.702-.032 1.687-.72 2.375a.5.5 0 0 1-.707 0l-2.829-2.828-3.182 3.182c-.195.195-1.219.902-1.414.707-.195-.195.512-1.22.707-1.414l3.182-3.182-2.828-2.829a.5.5 0 0 1 0-.707c.688-.688 1.673-.767 2.375-.72a5.922 5.922 0 0 1 1.013.16l3.134-3.133a2.772 2.772 0 0 1-.04-.461c0-.43.108-1.022.589-1.503a.5.5 0 0 1 .353-.146z"/>
                      </svg>
                    </span>
                  )}
                </td>
                <td>
                  <div className="node-table-tag-list">
                    {(n.tags || []).map(tagId => {
                      const tag = tagDefs[tagId]
                      const marked = tagsToDelete.get(tagId)?.has(n.id)
                      return (
                        <button
                          key={tagId}
                          className={`node-table-tag-pill${marked ? ' marked-delete' : ''}`}
                          style={!marked && tag?.color ? { borderColor: tag.color, color: tag.color } : undefined}
                          onClick={e => { e.stopPropagation(); toggleTagForDeletion(n.id, tagId) }}
                          title="Click to mark/unmark for removal"
                        >
                          {tag?.name || 'tag'}
                        </button>
                      )
                    })}
                    {(n.tags || []).length === 0 && <span className="node-table-tags-empty">—</span>}
                  </div>
                </td>
              </tr>
            ))}
            {rows.length === 0 && (
              <tr>
                <td colSpan={4} className="node-table-empty">No nodes match your filter.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}
