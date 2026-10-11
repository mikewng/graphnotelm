import Image from '@tiptap/extension-image'
import { imageApi } from '../api'

// Uploaded images store their id in `data-image-id`. On load the src is rebuilt from
// that id and the current API base URL, so saved notes keep working if the backend
// host or port changes. Images without an id keep whatever src they were given.
export const NoteImage = Image.extend({
  addAttributes() {
    return {
      ...this.parent?.(),
      src: {
        default: null,
        parseHTML: el => {
          const imageId = el.getAttribute('data-image-id')
          return imageId ? imageApi.url(imageId) : el.getAttribute('src')
        },
      },
      imageId: {
        default: null,
        parseHTML: el => el.getAttribute('data-image-id'),
        renderHTML: ({ imageId }) => (imageId ? { 'data-image-id': imageId } : {}),
      },
    }
  },
}).configure({ inline: false, allowBase64: false })
