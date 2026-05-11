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
        <button className="btn-ghost" onClick={onClose}>×</button>
      </div>

      <div className="manage-sidebar-body">

        <section>
          <p className="section-title">Tags</p>
          {tagsList.map(t => (
            <div key={t.id} className="type-row">
              {t.color && <span className="color-swatch" style={{ background: t.color }} />}
              <span className="name">{t.name}</span>
              <button className="btn-outline" onClick={() => onEditTag(t.id)}>Edit</button>
              <button className="btn-danger" onClick={() => onDeleteTag(t.id)}>Del</button>
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
              <button className="btn-outline" onClick={() => onEditRelType(r.id)}>Edit</button>
              <button className="btn-danger" onClick={() => onDeleteRelType(r.id)}>Del</button>
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
