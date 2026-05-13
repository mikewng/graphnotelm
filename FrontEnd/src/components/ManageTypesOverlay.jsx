export default function ManageTypesOverlay({
  tagsList,
  relTypesList,
  onAddTag,
  onEditTag,
  onDeleteTag,
  onAddRelType,
  onEditRelType,
  onDeleteRelType,
  onClose,
}) {
  return (
    <div className="manage-sidebar">
      <div className="manage-sidebar-header">
        <span>Manage Types</span>
        <button className="btn-ghost btn-icon-sm" onClick={onClose} title="Close"><span className="app-icon icon-close" /></button>
      </div>

      <div className="manage-sidebar-body">

        <section>
          <p className="section-title">Tags</p>
          {tagsList.map(t => (
            <div key={t.id} className="type-row">
              {t.color && <span className="color-swatch" style={{ background: t.color }} />}
              <span className="name">{t.name}</span>
              <button className="btn-outline btn-icon-sm" onClick={() => onEditTag(t.id)} title="Edit"><span className="app-icon icon-edit" /></button>
              <button className="btn-danger btn-icon-sm" onClick={() => onDeleteTag(t.id)} title="Delete"><span className="app-icon icon-delete" /></button>
            </div>
          ))}
          {tagsList.length === 0 && <p className="hint">No tags yet.</p>}
          <div className="type-add-row">
            <button onClick={onAddTag}>+ Add Tag</button>
          </div>
        </section>

        <section>
          <p className="section-title">Relationship Types</p>
          {relTypesList.map(r => (
            <div key={r.id} className="type-row">
              {r.color && <span className="color-swatch" style={{ background: r.color }} />}
              <span className="name">{r.name}</span>
              {r.inverse && <span className="inverse">↔ {r.inverse}</span>}
              <button className="btn-outline btn-icon-sm" onClick={() => onEditRelType(r.id)} title="Edit"><span className="app-icon icon-edit" /></button>
              <button className="btn-danger btn-icon-sm" onClick={() => onDeleteRelType(r.id)} title="Delete"><span className="app-icon icon-delete" /></button>
            </div>
          ))}
          {relTypesList.length === 0 && <p className="hint">No relationship types yet.</p>}
          <div className="type-add-row">
            <button onClick={onAddRelType}>+ Add Relationship Type</button>
          </div>
        </section>

      </div>
    </div>
  )
}
