import * as React from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Camera, Loader2, Trash2 } from 'lucide-react'
import { accountApi } from '@/api/endpoints/account'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { ConfirmDialog } from '@/components/modals/ConfirmDialog'
import { Button } from '@/components/ui/button'
import { useApiErrorToast } from '@/hooks/use-api-toast'
import { useAuth } from '@/hooks/use-auth'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { avatarTraitsFor, imageUrl } from '@/lib/identity'
import { qk } from '@/lib/query-client'
import { cn } from '@/lib/utils'

/** What the API accepts. Checked here so an oversized file never leaves the page. */
const MAX_BYTES = 5 * 1024 * 1024
const ACCEPTED = ['image/jpeg', 'image/png', 'image/webp', 'image/gif']

interface ProfilePictureFieldProps {
  /** Rendered beside the avatar — usually the account's name and role. */
  children?: React.ReactNode
  className?: string
  /**
   * Query keys to refresh after a change, for screens that render the avatar
   * from their own profile record rather than from the session.
   */
  invalidate?: readonly (readonly unknown[])[]
}

/**
 * The signed-in user's own avatar, for every role.
 *
 * `/me/profile-picture` carries no id — the resource is always the caller's
 * own — so this one component serves students, parents, teachers and admins
 * alike. Teachers have a second route that also sets their public cover image;
 * that one lives on the teacher settings screen and writes the same row, so
 * the two stay in step.
 */
export function ProfilePictureField({
  children,
  className,
  invalidate = [],
}: ProfilePictureFieldProps) {
  const { t, locale } = useI18n()
  const { user, refreshUser } = useAuth()
  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()

  const inputRef = React.useRef<HTMLInputElement>(null)
  const [localError, setLocalError] = React.useState<string | null>(null)
  const [confirmRemove, setConfirmRemove] = React.useState(false)
  /** Shown the instant a file is chosen, so the avatar never looks stuck. */
  const [preview, setPreview] = React.useState<string | null>(null)

  React.useEffect(() => {
    return () => {
      if (preview) URL.revokeObjectURL(preview)
    }
  }, [preview])

  const picture = useQuery({
    queryKey: qk.account.profilePicture,
    queryFn: () => accountApi.profilePicture(),
    staleTime: 5 * 60_000,
  })

  const settle = React.useCallback(
    async (next: { url?: string | null; hasPicture?: boolean }) => {
      queryClient.setQueryData(qk.account.profilePicture, next)
      invalidate.forEach((key) => queryClient.invalidateQueries({ queryKey: key }))
      // The avatar in the navbar reads the session user, not this query.
      await refreshUser()
      toast({ title: t('common.saved'), variant: 'success' })
    },
    [queryClient, invalidate, refreshUser, toast, t],
  )

  const upload = useMutation({
    mutationFn: (file: File) => accountApi.uploadProfilePicture(file),
    onSuccess: async (result) => {
      await settle(result ?? {})
    },
    onError: (error) => showApiError(error),
    onSettled: () => {
      setPreview((current) => {
        if (current) URL.revokeObjectURL(current)
        return null
      })
    },
  })

  const remove = useMutation({
    mutationFn: () => accountApi.removeProfilePicture(),
    onSuccess: async (result) => {
      setConfirmRemove(false)
      await settle(result ?? { url: null, hasPicture: false })
    },
    onError: (error) => {
      setConfirmRemove(false)
      showApiError(error)
    },
  })

  function onPick(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    // Clear the input so choosing the same file twice still fires a change.
    event.target.value = ''
    if (!file) return

    setLocalError(null)

    if (!ACCEPTED.includes(file.type)) {
      setLocalError(
        locale === 'ar-EG'
          ? 'الصورة لازم تكون JPG أو PNG أو WebP.'
          : 'The picture must be a JPG, PNG, or WebP.',
      )
      return
    }
    if (file.size > MAX_BYTES) {
      setLocalError(
        locale === 'ar-EG'
          ? 'حجم الصورة أكبر من ٥ ميجا. اختار صورة أصغر.'
          : 'That picture is over 5 MB. Please choose a smaller one.',
      )
      return
    }

    setPreview(URL.createObjectURL(file))
    upload.mutate(file)
  }

  const busy = upload.isPending || remove.isPending
  /**
   * `hasPicture: false` is authoritative: the removal response carries no `url`
   * at all, and the session user's copy is only refreshed a moment later — so
   * trusting it here would flash the deleted avatar back for one render.
   */
  const cleared = picture.data?.hasPicture === false
  const stored = cleared ? undefined : imageUrl(picture.data?.url ?? user?.profileImageUrl)
  const src = preview ?? stored
  const hasPicture = picture.data?.hasPicture ?? Boolean(stored)
  const seed = user?.id ?? ''

  return (
    <div className={cn('flex flex-wrap items-center gap-5', className)}>
      <div className="relative">
        <Avatar
          traits={avatarTraitsFor(seed)}
          seed={seed}
          name={user?.displayName ?? ''}
          src={src}
          size="2xl"
          className={cn('transition-opacity', busy && 'opacity-60')}
        />
        <button
          type="button"
          onClick={() => inputRef.current?.click()}
          disabled={busy}
          aria-label={t('common.upload')}
          className="absolute -bottom-1 -end-1 grid size-9 place-items-center rounded-full border-2 border-card bg-primary text-primary-foreground shadow-soft transition-transform hover:scale-105 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background disabled:pointer-events-none disabled:opacity-60"
        >
          {busy ? (
            <Loader2 className="size-4 animate-spin" aria-hidden />
          ) : (
            <Camera className="size-4" aria-hidden />
          )}
        </button>
        <input
          ref={inputRef}
          type="file"
          accept={ACCEPTED.join(',')}
          className="hidden"
          onChange={onPick}
        />
      </div>

      <div className="min-w-0 flex-1">
        {children}

        <div className="mt-3 flex flex-wrap items-center gap-2">
          <Button
            type="button"
            size="sm"
            variant="outline"
            onClick={() => inputRef.current?.click()}
            loading={upload.isPending}
            disabled={busy}
          >
            <Camera aria-hidden />
            {hasPicture
              ? locale === 'ar-EG'
                ? 'تغيير الصورة'
                : 'Change picture'
              : locale === 'ar-EG'
                ? 'رفع صورة'
                : 'Upload a picture'}
          </Button>

          {hasPicture && (
            <Button
              type="button"
              size="sm"
              variant="ghost"
              onClick={() => setConfirmRemove(true)}
              disabled={busy}
              className="text-destructive-strong hover:bg-destructive/8"
            >
              <Trash2 aria-hidden />
              {t('common.remove')}
            </Button>
          )}
        </div>

        <p
          className={cn(
            'mt-2 text-caption',
            localError ? 'font-medium text-destructive-strong' : 'text-muted-foreground',
          )}
          role={localError ? 'alert' : undefined}
        >
          {localError ??
            (locale === 'ar-EG'
              ? 'JPG أو PNG أو WebP، وأقصى حجم ٥ ميجا.'
              : 'JPG, PNG, or WebP — up to 5 MB.')}
        </p>
      </div>

      <ConfirmDialog
        open={confirmRemove}
        onOpenChange={setConfirmRemove}
        title={locale === 'ar-EG' ? 'إزالة صورة البروفايل؟' : 'Remove your profile picture?'}
        description={
          locale === 'ar-EG'
            ? 'هترجع للصورة الافتراضية. تقدر ترفع صورة تانية في أي وقت.'
            : "You'll go back to the default avatar. You can upload another at any time."
        }
        confirmLabel={t('common.remove')}
        loading={remove.isPending}
        onConfirm={() => remove.mutate()}
      />
    </div>
  )
}
