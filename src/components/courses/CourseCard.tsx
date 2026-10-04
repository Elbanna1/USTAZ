import { Link } from 'react-router-dom'
import { Clock, Heart, PlayCircle, Sparkles, Users } from 'lucide-react'
import type { CourseListItemDto } from '@/api/schema'
import { pricingToBreakdown } from '@/lib/pricing'
import { cn, formatCompactNumber } from '@/lib/utils'
import { avatarTraitsFor, coverPatternFor, imageUrl, subjectKeyFor } from '@/lib/identity'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { CoverImage } from '@/components/illustrations/CoverArt'
import { SubjectChip } from '@/components/illustrations/SubjectIcon'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { Rating } from '@/components/common/Rating'
import { PriceTag } from '@/components/common/pricing-bits'
import { HoverLift } from '@/components/common/Reveal'
import { useFavoriteCourses } from '@/hooks/use-favorites'
import { useI18n } from '@/i18n'

function useCourseView(course: CourseListItemDto) {
  const { pick, locale } = useI18n()
  const id = course.id ?? ''
  const hours = Math.round(((course.durationInMinutes ?? 0) / 60) * 10) / 10

  return {
    id,
    href: `/courses/${id}`,
    title: course.title?.trim() || '—',
    description: course.description?.trim() ?? '',
    subjectKey: subjectKeyFor(course.subject),
    subjectName: pick(course.subject?.nameAr, course.subject?.nameEn),
    gradeName: pick(course.grade?.nameAr, course.grade?.nameEn),
    thumbnail: imageUrl(course.thumbnailUrl),
    pattern: coverPatternFor(id),
    price: pricingToBreakdown(course.pricing),
    currency: course.pricing?.currency ?? 'EGP',
    durationLabel: hours
      ? locale === 'ar-EG'
        ? `${hours} ساعة`
        : `${hours}h`
      : null,
  }
}

export function CourseCard({
  course,
  className,
}: {
  course: CourseListItemDto
  className?: string
}) {
  const { t, locale, formatNumber } = useI18n()
  const view = useCourseView(course)
  const favorites = useFavoriteCourses()
  const saved = favorites.isSaved(view.id)
  const teacher = course.teacher

  return (
    <HoverLift className={className}>
      <Card className="group flex h-full flex-col overflow-hidden">
        {/* Thumbnail */}
        <div className="relative aspect-[16/9] overflow-hidden">
          <CoverImage
            src={view.thumbnail}
            pattern={view.pattern}
            subject={view.subjectKey}
            intensity="bold"
          />

          <div className="absolute inset-x-4 top-4 flex items-start justify-between gap-2">
            <SubjectChip subject={view.subjectKey} />
            <div className="flex items-center gap-2">
              {course.isFeatured && (
                <Badge variant="gold" size="sm" className="shadow-soft">
                  <Sparkles className="size-3" />
                  {locale === 'ar-EG' ? 'الأكثر طلبًا' : 'Popular'}
                </Badge>
              )}
              {favorites.enabled && (
                <button
                  type="button"
                  onClick={() => favorites.toggle(view.id)}
                  disabled={favorites.isToggling}
                  aria-pressed={saved}
                  aria-label={saved ? 'Remove from favorites' : 'Save course'}
                  className={cn(
                    'grid size-8 place-items-center rounded-full border border-white/25 bg-black/25 text-white backdrop-blur-sm transition-all duration-200',
                    'hover:bg-black/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white/70',
                    saved && 'border-accent/50 bg-accent text-accent-foreground',
                  )}
                >
                  <Heart className={cn('size-4', saved && 'fill-current')} />
                </button>
              )}
            </div>
          </div>

          {/* Play affordance */}
          <Link
            to={view.href}
            aria-label={view.title}
            className="absolute inset-0 grid place-items-center focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white/80"
          >
            <span className="grid size-14 place-items-center rounded-full border border-white/30 bg-black/30 text-white opacity-0 backdrop-blur-sm transition-all duration-300 ease-premium group-hover:scale-105 group-hover:opacity-100">
              <PlayCircle className="size-7" />
            </span>
          </Link>

          <div className="pointer-events-none absolute inset-x-4 bottom-3 flex items-center justify-between gap-2">
            {view.gradeName ? (
              <Badge
                variant="outline"
                size="sm"
                className="border-white/25 bg-black/30 text-white backdrop-blur-sm"
              >
                {view.gradeName}
              </Badge>
            ) : (
              <span />
            )}
            {view.durationLabel && (
              <Badge
                variant="outline"
                size="sm"
                className="tnum border-white/25 bg-black/30 text-white backdrop-blur-sm"
              >
                <Clock className="size-3" />
                {view.durationLabel}
              </Badge>
            )}
          </div>
        </div>

        {/* Body */}
        <div className="flex flex-1 flex-col gap-3.5 p-5">
          <div>
            <h3>
              <Link
                to={view.href}
                className="font-display text-heading-sm font-bold leading-snug text-foreground transition-colors hover:text-primary"
              >
                {view.title}
              </Link>
            </h3>
            {view.description && (
              <p className="mt-1.5 line-clamp-2-rtl text-body-sm text-muted-foreground">
                {view.description}
              </p>
            )}
          </div>

          {teacher?.id && (
            <Link
              to={`/teachers/${teacher.id}`}
              className="inline-flex items-center gap-2 text-body-sm text-muted-foreground transition-colors hover:text-foreground"
            >
              <Avatar
                traits={avatarTraitsFor(teacher.id)}
                seed={teacher.id}
                name={teacher.displayName ?? ''}
                src={imageUrl(teacher.profileImageUrl)}
                size="xs"
              />
              {teacher.displayName}
            </Link>
          )}

          <div className="flex flex-wrap items-center gap-x-4 gap-y-2 text-caption text-muted-foreground">
            <Rating value={course.rating ?? 0} count={course.reviewCount} size="sm" />
            <span className="inline-flex items-center gap-1.5">
              <Users className="size-3.5" />
              <span className="tnum">{formatCompactNumber(course.studentCount ?? 0)}</span>
            </span>
            <span className="tnum">
              {formatNumber(course.lessonsCount ?? 0)}{' '}
              {locale === 'ar-EG' ? 'درس' : 'lessons'}
            </span>
          </div>

          <div className="flex flex-wrap gap-1.5">
            {view.subjectName && (
              <Badge variant="default" size="sm">
                {view.subjectName}
              </Badge>
            )}
            {!course.isPublished && (
              <Badge variant="outline" size="sm">
                {t('common.draft')}
              </Badge>
            )}
          </div>

          <div className="mt-auto flex items-end justify-between gap-3 border-t border-border pt-4">
            <PriceTag breakdown={view.price} size="sm" currency={view.currency} />
            <Button asChild size="md">
              <Link to={view.href}>{t('common.details')}</Link>
            </Button>
          </div>
        </div>
      </Card>
    </HoverLift>
  )
}

/** Horizontal course row used on teacher profiles and dense lists. */
export function CourseRow({
  course,
  className,
}: {
  course: CourseListItemDto
  className?: string
}) {
  const { t, locale, formatNumber } = useI18n()
  const view = useCourseView(course)

  return (
    <Card className={cn('flex gap-4 overflow-hidden p-4', className)} interactive>
      <div className="relative hidden h-24 w-36 shrink-0 overflow-hidden rounded-lg sm:block">
        <CoverImage
          src={view.thumbnail}
          pattern={view.pattern}
          subject={view.subjectKey}
          intensity="bold"
        />
      </div>
      <div className="min-w-0 flex-1">
        <Link
          to={view.href}
          className="font-display text-body font-bold text-foreground hover:text-primary"
        >
          {view.title}
        </Link>
        {view.description && (
          <p className="mt-1 line-clamp-2-rtl text-caption text-muted-foreground">
            {view.description}
          </p>
        )}
        <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-caption text-muted-foreground">
          <Rating value={course.rating ?? 0} size="sm" />
          <span className="tnum">
            {formatNumber(course.lessonsCount ?? 0)} {locale === 'ar-EG' ? 'درس' : 'lessons'}
          </span>
          {view.durationLabel && <span className="tnum">{view.durationLabel}</span>}
        </div>
      </div>
      <div className="flex shrink-0 flex-col items-end justify-between gap-2">
        <PriceTag
          breakdown={view.price}
          size="sm"
          align="end"
          showBadge={false}
          currency={view.currency}
        />
        <Button asChild size="sm" variant="outline">
          <Link to={view.href}>{t('common.details')}</Link>
        </Button>
      </div>
    </Card>
  )
}
