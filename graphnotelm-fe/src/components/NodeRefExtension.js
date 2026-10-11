import { Mark, mergeAttributes } from '@tiptap/core'

export const NodeRef = Mark.create({
  name: 'nodeRef',
  priority: 1001, // higher than default marks so it parses before them

  addAttributes() {
    return {
      nodeId: {
        default: null,
        parseHTML: el => el.getAttribute('data-node-id'),
        renderHTML: ({ nodeId }) => nodeId ? { 'data-node-id': nodeId } : {},
      },
      nodeTitle: {
        default: '',
        parseHTML: el => el.getAttribute('data-node-title') || '',
        renderHTML: ({ nodeTitle }) => nodeTitle ? { 'data-node-title': nodeTitle } : {},
      },
    }
  },

  parseHTML() {
    return [{ tag: 'a[data-node-id]' }]
  },

  renderHTML({ HTMLAttributes }) {
    return ['a', mergeAttributes({ class: 'node-ref', role: 'link' }, HTMLAttributes), 0]
  },

  addCommands() {
    return {
      setNodeRef: attrs => ({ commands }) => commands.setMark(this.name, attrs),
      unsetNodeRef: () => ({ commands }) => commands.unsetMark(this.name),
    }
  },
})
