import SidePanel from './SidePanel'

export default function ManageTypesOverlay({
  isOpen,
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
    <SidePanel title="Manage Types" onClose={onClose} isOpen={isOpen} defaultWidth={320}>
      <div className="manage-sidebar-body">
        <section>
          <p className="section-title">Tags</p>
          {tagsList.map(t => (
            <div key={t.id} className="type-row">
              {t.color && <span className="color-swatch" style={{ background: t.color }} />}
              <span className="name">{t.name}</span>
              <button className="btn-outline" onClick={() => onEditTag(t.id)}>
                <span className="app-icon icon-edit" style={{ width: 11, height: 11 }} />
              </button>
              <button className="btn-danger" onClick={() => onDeleteTag(t.id)}>
                <span className="app-icon icon-delete" style={{ width: 11, height: 11 }} />
              </button>
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
              <button className="btn-outline" onClick={() => onEditRelType(r.id)}>
                <span className="app-icon icon-edit" style={{ width: 11, height: 11 }} />
              </button>
              <button className="btn-danger" onClick={() => onDeleteRelType(r.id)}>
                <span className="app-icon icon-delete" style={{ width: 11, height: 11 }} />
              </button>
            </div>
          ))}
          {relTypesList.length === 0 && <p className="hint">No relationship types yet.</p>}
          <div className="type-add-row">
            <button onClick={onAddRelType}>+ Add Relationship Type</button>
          </div>
        </section>
      </div>
    </SidePanel>
  )
}
