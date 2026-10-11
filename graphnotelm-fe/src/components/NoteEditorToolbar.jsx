import { useRef } from 'react'
import { useEditorState } from '@tiptap/react'
import { ACCEPTED_IMAGE_TYPES } from '../utils/imageUpload'

function ToolbarButton({ active, disabled, onClick, title, children }) {
  return (
    <button
      type="button"
      className={`toolbar-btn${active ? ' toolbar-btn--active' : ''}`}
      onMouseDown={e => e.preventDefault()}
      onClick={onClick}
      disabled={disabled}
      title={title}
    >
      {children}
    </button>
  )
}

export default function NoteEditorToolbar({ editor, onInsertNodeRef, onInsertImages, canInsertImages }) {
  const imageInputRef = useRef(null)
  const state = useEditorState({
    editor,
    selector: ctx => {
      if (!ctx.editor) return null
      return {
        bold: ctx.editor.isActive('bold'),
        italic: ctx.editor.isActive('italic'),
        underline: ctx.editor.isActive('underline'),
        strike: ctx.editor.isActive('strike'),
        code: ctx.editor.isActive('code'),
        h1: ctx.editor.isActive('heading', { level: 1 }),
        h2: ctx.editor.isActive('heading', { level: 2 }),
        h3: ctx.editor.isActive('heading', { level: 3 }),
        bulletList: ctx.editor.isActive('bulletList'),
        orderedList: ctx.editor.isActive('orderedList'),
        blockquote: ctx.editor.isActive('blockquote'),
        codeBlock: ctx.editor.isActive('codeBlock'),
        canUndo: ctx.editor.can().undo(),
        canRedo: ctx.editor.can().redo(),
        nodeRef: ctx.editor.isActive('nodeRef'),
        hasSelection: !ctx.editor.state.selection.empty,
      }
    },
  })

  if (!editor || !state) return null

  return (
    <div className="note-toolbar">
      <ToolbarButton title="Bold (Ctrl+B)" active={state.bold} onClick={() => editor.chain().focus().toggleBold().run()}>
        <strong>B</strong>
      </ToolbarButton>
      <ToolbarButton title="Italic (Ctrl+I)" active={state.italic} onClick={() => editor.chain().focus().toggleItalic().run()}>
        <em>I</em>
      </ToolbarButton>
      <ToolbarButton title="Underline (Ctrl+U)" active={state.underline} onClick={() => editor.chain().focus().toggleUnderline().run()}>
        <span style={{ textDecoration: 'underline' }}>U</span>
      </ToolbarButton>
      <ToolbarButton title="Strikethrough" active={state.strike} onClick={() => editor.chain().focus().toggleStrike().run()}>
        <span style={{ textDecoration: 'line-through' }}>S</span>
      </ToolbarButton>
      <ToolbarButton title="Inline code" active={state.code} onClick={() => editor.chain().focus().toggleCode().run()}>
        {'</>'}
      </ToolbarButton>

      <span className="toolbar-divider" />

      <ToolbarButton title="Heading 1" active={state.h1} onClick={() => editor.chain().focus().toggleHeading({ level: 1 }).run()}>
        H1
      </ToolbarButton>
      <ToolbarButton title="Heading 2" active={state.h2} onClick={() => editor.chain().focus().toggleHeading({ level: 2 }).run()}>
        H2
      </ToolbarButton>
      <ToolbarButton title="Heading 3" active={state.h3} onClick={() => editor.chain().focus().toggleHeading({ level: 3 }).run()}>
        H3
      </ToolbarButton>

      <span className="toolbar-divider" />

      <ToolbarButton title="Bullet list" active={state.bulletList} onClick={() => editor.chain().focus().toggleBulletList().run()}>
        •—
      </ToolbarButton>
      <ToolbarButton title="Numbered list" active={state.orderedList} onClick={() => editor.chain().focus().toggleOrderedList().run()}>
        1.
      </ToolbarButton>
      <ToolbarButton title="Quote" active={state.blockquote} onClick={() => editor.chain().focus().toggleBlockquote().run()}>
        "
      </ToolbarButton>
      <ToolbarButton title="Code block" active={state.codeBlock} onClick={() => editor.chain().focus().toggleCodeBlock().run()}>
        { '{ }' }
      </ToolbarButton>

      <span className="toolbar-divider" />

      <ToolbarButton
        title={state.nodeRef ? 'Remove note link' : 'Link to note (select text first)'}
        active={state.nodeRef}
        disabled={!state.hasSelection && !state.nodeRef}
        onClick={e => onInsertNodeRef?.(e.currentTarget.getBoundingClientRect())}
      >
        <svg viewBox="0 0 14 14" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" width="13" height="13">
          <path d="M5.5 8.5a3.5 3.5 0 0 0 4.95 0l1.5-1.5a3.5 3.5 0 0 0-4.95-4.95l-.85.85"/>
          <path d="M8.5 5.5a3.5 3.5 0 0 0-4.95 0L2.05 7a3.5 3.5 0 0 0 4.95 4.95l.85-.85"/>
        </svg>
      </ToolbarButton>
      <ToolbarButton
        title={canInsertImages ? 'Insert image (or paste / drop one)' : 'Create the note before adding images'}
        disabled={!canInsertImages}
        onClick={() => imageInputRef.current?.click()}
      >
        <svg viewBox="0 0 14 14" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" width="13" height="13">
          <rect x="1.5" y="2.5" width="11" height="9" rx="1.5"/>
          <circle cx="5" cy="5.75" r="1"/>
          <path d="M12.5 9l-3-3-6 5.5"/>
        </svg>
      </ToolbarButton>
      <input
        ref={imageInputRef}
        type="file"
        accept={ACCEPTED_IMAGE_TYPES.join(',')}
        multiple
        hidden
        onChange={e => {
          // Copy first: clearing the value empties the live FileList. Clearing lets the
          // same file be picked again.
          const files = Array.from(e.target.files || [])
          e.target.value = ''
          if (files.length > 0) onInsertImages?.(files)
        }}
      />

      <span className="toolbar-divider" />

      <ToolbarButton title="Undo (Ctrl+Z)" disabled={!state.canUndo} onClick={() => editor.chain().focus().undo().run()}>
        ↶
      </ToolbarButton>
      <ToolbarButton title="Redo (Ctrl+Y)" disabled={!state.canRedo} onClick={() => editor.chain().focus().redo().run()}>
        ↷
      </ToolbarButton>
    </div>
  )
}
