import { imageApi } from '../api'

export const ACCEPTED_IMAGE_TYPES = ['image/png', 'image/jpeg', 'image/gif', 'image/webp']

// Mirrors ImageFormats.MaxUploadBytes on the backend.
export const MAX_IMAGE_BYTES = 10 * 1024 * 1024

/** Image files from a paste, drop, or file-input FileList; other files are ignored. */
export function getImageFiles(fileList) {
  return Array.from(fileList || []).filter(file => file.type?.startsWith('image/'))
}

/**
 * Client-side precheck for quick feedback. The backend re-validates by file contents.
 * Returns an error message, or null when the file can be uploaded.
 */
export function validateImageFile(file) {
  const label = file.name || 'Image'
  if (!ACCEPTED_IMAGE_TYPES.includes(file.type))
    return `${label}: unsupported image type. Use PNG, JPEG, GIF, or WebP.`
  if (file.size > MAX_IMAGE_BYTES)
    return `${label}: exceeds the ${MAX_IMAGE_BYTES / (1024 * 1024)} MB limit.`
  return null
}

/**
 * Uploads the files in parallel, then inserts the successful ones at the current
 * selection in their original order. `getContext` returns `{ graphId, nodeId, onError }`
 * and is read again once the uploads finish: if the editor has moved on to a different
 * node (or was destroyed) in the meantime, nothing is inserted.
 *
 * Returns the number of images inserted.
 */
export async function uploadAndInsertImages(editor, files, getContext) {
  const { graphId, nodeId, onError } = getContext()

  const results = await Promise.allSettled(files.map(async file => {
    const problem = validateImageFile(file)
    if (problem) throw new Error(problem)
    const { imageId } = await imageApi.upload(graphId, nodeId, file)
    return { type: 'image', attrs: { src: imageApi.url(imageId), imageId, alt: file.name || null } }
  }))

  results
    .filter(r => r.status === 'rejected')
    .forEach(r => onError?.(r.reason?.message || 'Image upload failed.'))

  const images = results.filter(r => r.status === 'fulfilled').map(r => r.value)
  if (images.length === 0 || !editor || editor.isDestroyed || getContext().nodeId !== nodeId)
    return 0

  editor.chain().focus().insertContent(images).run()
  return images.length
}
