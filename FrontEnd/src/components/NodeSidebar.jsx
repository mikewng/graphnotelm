export default function NodeSidebar({
  nodesList,
  tagsList,
  tagDefs,
  search,
  setSearch,
  tagFilter,
  setTagFilter,
  selectedNodeId,
  onSelectNode,
  onCreateNode,
}) {
  const filteredNodes = nodesList
    .filter(n => !search || (n.title || '').toLowerCase().includes(search.toLowerCase()))
    .filter(n => !tagFilter || (n.tags || []).includes(tagFilter))

  return (
    <div className="sidebar">
      <div className="sidebar-header">
        <span>Nodes</span>
        <button onClick={onCreateNode}>+ New</button>
      </div>
      <div className="sidebar-search">
        <input
          type="text"
          placeholder="Search nodes..."
          value={search}
          onChange={e => setSearch(e.target.value)}
        />
      </div>
      <div className="sidebar-tag-filter">
        <button
          className={tagFilter === null ? 'active' : ''}
          onClick={() => setTagFilter(null)}
        >all</button>
        {tagsList.map(t => (
          <button
            key={t.id}
            className={tagFilter === t.id ? 'active' : ''}
            style={t.color ? { color: t.color, borderColor: t.color, background: t.color + '20' } : undefined}
            onClick={() => setTagFilter(tagFilter === t.id ? null : t.id)}
          >{t.name}</button>
        ))}
      </div>
      <div className="node-list">
        {filteredNodes.map(n => {
          const primaryTagId = (n.tags || [])[0]
          const dotColor = primaryTagId ? tagDefs[primaryTagId]?.color : null
          const connectionCount =
            (n.relationships?.length || 0) +
            nodesList.filter(o => o.id !== n.id && (o.relationships || []).some(r => r.targetNodeId === n.id)).length
          return (
            <div
              key={n.id}
              className={`note-item${selectedNodeId === n.id ? ' active' : ''}`}
              onClick={() => onSelectNode(n.id)}
            >
              <span className="note-dot" style={dotColor ? { background: dotColor } : undefined} />
              <span className="note-name">{n.title || '(untitled)'}</span>
              {connectionCount > 0 && <span className="note-meta">{connectionCount}</span>}
            </div>
          )
        })}
      </div>
    </div>
  )
}
