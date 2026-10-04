import { Link } from 'react-router-dom'
import { BadgeCheck, Heart, MapPin, Sparkles, Users } from 'lucide-react'
import type { TeacherListItemDto } from '@/api/schema'
import { cn, formatCompactNumber } from '@/lib/utils'
import { avatarTraitsFor, coverPatternFor, imageUrl, subjectKeyFor } from '@/lib/identity'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Hint } from '@/components/ui/primitives'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { CoverImage } from '@/components/illustrations/CoverArt'
import { SubjectIcon } from '@/components/illustrations/SubjectIcon'
import { Rating } from '@/components/common/Rating'
import { HoverLift } from '@/components/common/Reveal'
import { WhatsAppIconButton } from '@/components/whatsapp/WhatsAppButton'
import { useFavoriteTeachers } from '@/hooks/use-favorites'
import { useI18n } from '@/i18n'

const MODE_KEYS = {
  Online: { ar: 'أونلاين', en: 'Online' },
  Offline: { ar: 'حضوري', en: 'In person' },
  Hybrid: { ar: 'أونلاين وحضوري', en: 'Online & in person' },
} as const

function useModeLabel() {
  const { locale } = useI18n()
  return (mode?: 'Online' | 'Offline' | 'Hybrid') =>
    mode ? MODE_KEYS[mode][locale === 'ar-EG' ? 'ar' : 'en'] : ''
}

/** Everything the card needs, derived once from the DTO. */
function useTeacherView(teacher: TeacherListItemDto) {
  const { pick } = useI18n()
  const id = teacher.id ?? ''
  const name = teacher.displayName?.trim() || '—'
  const primarySubject = teacher.subjects?.[0]

  return {
    id,
    name,
    href: `/teachers/${id}`,
    headline: teacher.headline?.trim() ?? '',
    subjectKey: subjectKeyFor(primarySubject),
    subjectName: pick(primarySubject?.nameAr, primarySubject?.nameEn),
    traits: avatarTraitsFor(id),
    pattern: coverPatternFor(id),
    photo: imageUrl(teacher.profileImageUrl),
    cover: imageUrl(teacher.coverImageUrl),
  }
}

interface TeacherCardProps {
  teacher: TeacherListItemDto
  className?: string
  /** `compact` drops the cover band — used in sidebars and dense lists. */
  variant?: 'default' | 'compact'
}

export function TeacherCard({ teacher, className, variant = 'default' }: TeacherCardProps) {
  const { t, locale, formatNumber } = useI18n()
  const modeLabel = useModeLabel()
  const view = useTeacherView(teacher)
  const favorites = useFavoriteTeachers()
  const saved = favorites.isSaved(view.id)

  if (variant === 'compact') {
    return (
      <Card className={cn('flex items-center gap-3.5 p-4', className)} interactive>
        <Avatar
          traits={view.traits}
          seed={view.id}
          name={view.name}
          src={view.photo}
          size="md"
        />
        <div className="min-w-0 flex-1">
          <Link
            to={view.href}
            className="flex items-center gap-1.5 font-display text-body-sm font-bold text-foreground hover:text-primary"
          >
            <span className="truncate">{view.name}</span>
            {teacher.isVerified && <BadgeCheck className="size-4 shrink-0 text-primary" />}
          </Link>
          <p className="truncate text-caption text-muted-foreground">{view.headline}</p>
        </div>
        <Rating value={teacher.rating ?? 0} size="sm" />
      </Card>
    )
  }

  return (
    <HoverLift className={className}>
      <Card className="group relative flex h-full flex-col overflow-hidden">
        {/* Cover */}
        <div className="relative h-24 overflow-hidden">
          <CoverImage
            src={view.cover}
            pattern={view.pattern}
            subject={view.subjectKey}
            intensity="normal"
          />

          <div className="absolute inset-x-3 top-3 flex items-start justify-between gap-2">
            {teacher.isFeatured ? (
              <Badge variant="gold" size="sm" className="shadow-soft">
                <Sparkles className="size-3" />
                {locale === 'ar-EG' ? 'مميّز' : 'Featured'}
              </Badge>
            ) : (
              <span />
            )}

            {favorites.enabled && (
              <Hint
                label={
                  saved
                    ? locale === 'ar-EG'
                      ? 'شيل من المحفوظين'
                      : 'Remove from favorites'
                    : locale === 'ar-EG'
                      ? 'احفظ المدرّس'
                      : 'Save teacher'
                }
              >
                <button
                  type="button"
                  onClick={() => favorites.toggle(view.id)}
                  disabled={favorites.isToggling}
                  aria-pressed={saved}
                  aria-label={saved ? `Remove ${view.name}` : `Save ${view.name}`}
                  className={cn(
                    'grid size-8 place-items-center rounded-full border border-white/25 bg-black/25 text-white backdrop-blur-sm transition-all duration-200',
                    'hover:bg-black/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white/70',
                    'disabled:opacity-60',
                    saved && 'border-accent/50 bg-accent text-accent-foreground hover:bg-accent/90',
                  )}
                >
                  <Heart className={cn('size-4 transition-transform', saved && 'fill-current scale-110')} />
                </button>
              </Hint>
            )}
          </div>
        </div>

        {/* Body */}
        <div className="flex flex-1 flex-col p-5 pt-0">
          <div className="-mt-9 mb-3 flex items-end justify-between gap-3">
            <Avatar
              traits={view.traits}
              seed={view.id}
              name={view.name}
              src={view.photo}
              size="xl"
              className="ring-4 ring-card"
            />
            <Rating
              value={teacher.rating ?? 0}
              count={teacher.reviewCount}
              size="sm"
              className="pb-1"
            />
          </div>

          <h3 className="flex items-center gap-1.5">
            <Link
              to={view.href}
              className="font-display text-heading-sm font-bold text-foreground transition-colors hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              {view.name}
            </Link>
            {teacher.isVerified && (
              <Hint label={locale === 'ar-EG' ? 'بروفايل موثّق' : 'Verified profile'}>
                <BadgeCheck className="size-[1.125rem] shrink-0 text-primary" />
              </Hint>
            )}
          </h3>

          {view.headline && (
            <p className="mt-2 line-clamp-2-rtl text-body-sm leading-relaxed text-muted-foreground">
              {view.headline}
            </p>
          )}

          {/* Meta */}
          <div className="mt-4 flex flex-wrap gap-1.5">
            {view.subjectName && (
              <Badge variant="primary" size="sm">
                <SubjectIcon subject={view.subjectKey} className="size-3.5" strokeWidth={1.8} />
                {view.subjectName}
              </Badge>
            )}
            {teacher.city && (
              <Badge variant="outline" size="sm">
                <MapPin className="size-3" />
                {teacher.city}
              </Badge>
            )}
            {teacher.preferredTeachingMode && (
              <Badge variant="outline" size="sm">
                {modeLabel(teacher.preferredTeachingMode)}
              </Badge>
            )}
          </div>

          {/* Stats strip */}
          <dl className="mt-4 grid grid-cols-3 gap-2 rounded-lg border border-border bg-muted/40 p-3 text-center">
            <div>
              <dt className="text-[0.6875rem] text-muted-foreground">
                {locale === 'ar-EG' ? 'خبرة' : 'Experience'}
              </dt>
              <dd className="tnum font-display text-body-sm font-bold text-foreground">
                {formatNumber(teacher.yearsOfExperience ?? 0)}
                {locale === 'ar-EG' ? ' سنة' : 'y'}
              </dd>
            </div>
            <div className="border-x border-border">
              <dt className="text-[0.6875rem] text-muted-foreground">{t('common.students')}</dt>
              <dd className="tnum font-display text-body-sm font-bold text-foreground">
                {formatCompactNumber(teacher.studentsCount ?? 0)}
              </dd>
            </div>
            <div>
              <dt className="text-[0.6875rem] text-muted-foreground">{t('common.groups')}</dt>
              <dd className="tnum font-display text-body-sm font-bold text-foreground">
                {formatNumber(teacher.groupsCount ?? 0)}
              </dd>
            </div>
          </dl>

          {/* Actions */}
          <div className="mt-4 flex items-center gap-2 pt-1">
            <Button asChild block className="flex-1">
              <Link to={view.href}>
                {locale === 'ar-EG' ? 'شوف البروفايل' : 'View profile'}
              </Link>
            </Button>
            {teacher.whatsAppLink && (
              <WhatsAppIconButton
                href={teacher.whatsAppLink}
                label={
                  locale === 'ar-EG'
                    ? `كلّم ${view.name} على واتساب`
                    : `Message ${view.name} on WhatsApp`
                }
              />
            )}
          </div>
        </div>
      </Card>
    </HoverLift>
  )
}

/** Row layout used by the teacher directory's list view. */
export function TeacherRow({ teacher }: { teacher: TeacherListItemDto }) {
  const { locale, formatNumber } = useI18n()
  const view = useTeacherView(teacher)
  const favorites = useFavoriteTeachers()
  const saved = favorites.isSaved(view.id)

  return (
    <Card className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center" interactive>
      <Avatar
        traits={view.traits}
        seed={view.id}
        name={view.name}
        src={view.photo}
        size="lg"
        className="shrink-0"
      />

      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <Link
            to={view.href}
            className="font-display text-heading-sm font-bold text-foreground hover:text-primary"
          >
            {view.name}
          </Link>
          {teacher.isVerified && <BadgeCheck className="size-4 text-primary" />}
          {teacher.isFeatured && (
            <Badge variant="gold" size="sm">
              <Sparkles className="size-3" /> {locale === 'ar-EG' ? 'مميّز' : 'Featured'}
            </Badge>
          )}
        </div>
        {view.headline && (
          <p className="mt-0.5 line-clamp-2-rtl text-body-sm text-muted-foreground">
            {view.headline}
          </p>
        )}
        <div className="mt-2.5 flex flex-wrap items-center gap-x-4 gap-y-1.5 text-caption text-muted-foreground">
          {view.subjectName && (
            <span className="inline-flex items-center gap-1.5">
              <SubjectIcon subject={view.subjectKey} className="size-3.5" strokeWidth={1.8} />
              {view.subjectName}
            </span>
          )}
          {teacher.city && (
            <span className="inline-flex items-center gap-1.5">
              <MapPin className="size-3.5" /> {teacher.city}
            </span>
          )}
          <span className="inline-flex items-center gap-1.5">
            <Users className="size-3.5" /> {formatCompactNumber(teacher.studentsCount ?? 0)}
          </span>
          <span className="tnum">
            {formatNumber(teacher.yearsOfExperience ?? 0)}
            {locale === 'ar-EG' ? ' سنة خبرة' : ' years experience'}
          </span>
        </div>
      </div>

      <div className="flex shrink-0 items-center gap-2 sm:flex-col sm:items-end">
        <Rating value={teacher.rating ?? 0} count={teacher.reviewCount} size="sm" />
        <div className="flex items-center gap-2">
          {favorites.enabled && (
            <Button
              variant="outline"
              size="icon"
              aria-pressed={saved}
              disabled={favorites.isToggling}
              aria-label={saved ? 'Remove from favorites' : 'Save teacher'}
              onClick={() => favorites.toggle(view.id)}
            >
              <Heart className={cn('size-4', saved && 'fill-accent text-accent')} />
            </Button>
          )}
          <Button asChild size="sm">
            <Link to={view.href}>{locale === 'ar-EG' ? 'البروفايل' : 'Profile'}</Link>
          </Button>
        </div>
      </div>
    </Card>
  )
}
