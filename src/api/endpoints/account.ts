import { api } from '@/api/client'
import type { ProfilePictureDto } from '@/api/schema'

/**
 * The signed-in user's own avatar, whichever role they hold.
 *
 * There is no id in any of these calls — the resource is always the caller's
 * own, so there is nothing a client could point somewhere else. Teachers have
 * `/teacher/profile/image` as well (it also sets the cover image on their
 * public page) and the two stay in step because the server writes both rows.
 */
export const accountApi = {
  profilePicture: () => api.get<ProfilePictureDto>('/me/profile-picture'),

  /** Replaces any existing picture. The browser sets the multipart boundary. */
  uploadProfilePicture: (file: File) => {
    const form = new FormData()
    form.append('file', file)
    return api.upload<ProfilePictureDto>('/me/profile-picture', form)
  },

  /** Back to the default avatar. Idempotent. */
  removeProfilePicture: () => api.delete<ProfilePictureDto>('/me/profile-picture'),
}
