import { Link } from 'react-router-dom'
import { CalendarDays, Clock, MapPin, Monitor, Sparkles } from 'lucide-react'
import type { GroupListItemDto } from '@/api/schema'
import { pricingToBreakdown } from '@/lib/pricing'
import { cn } from '@/lib/utils'
import { avatarTraitsFor, coverPatternFor, imageUrl, subjectKeyFor } from '@/lib/identity'
import { formatScheduleDays, scheduleTimeRange } from '@/lib/schedule'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { CoverArt } from '@/components/illustrations/CoverArt'
import { SubjectChip } from '@/components/illustrations/SubjectIcon'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { Rating } from '@/components/common/Rating'
import { PriceTag, SeatsMeter } from '@/components/common/pricing-bits'
import { HoverLift } from '@/components/common/Reveal'
import { WhatsAppIconButton } from '@/components/whatsapp/WhatsAppButton'
import { useI18n } from '@/i18n'

const TYPE_LABELS = {
  Regular: { ar: 'منتظمة', en: 'Regular' },
  Revision: { ar: 'مراجعة نهائية', en: 'Revision' },
  Intensive: { ar: 'مكثّفة', en: 'Intensive' },
  ExamPreparation: { ar: 'استعداد للامتحان', en: 'Exam prep' },
} as const

const MODE_LABELS = {
  Online: { ar: 'أونلاين', en: 'Online' },
  Offline: { ar: 'حضوري', en: 'In person' },
  Hybrid: { ar: 'أونلاين وحضوري', en: 'Hybrid' },
} as const

/** Seat pressure, expressed the way a parent reads it. */
function seatStatus(group: GroupListItemDto) {
  const capacity = group.capacity ?? 0
  const taken = group.enrolledStudentsCount ?? 0
  const left = Math.max(0, capacity - taken)
  if (group.isFull || (capacity > 0 && left === 0)) return 'full' as const
  if (capacity > 0 && left <= Math.max(3, Math.round(capacity * 0.15))) return 'almostFull' as const
  return 'open' as const
}

const STATUS_COPY: Record<
  'open' | 'almostFull' | 'full',
  { ar: string; en: string; variant: 'success' | 'warning' | 'destructive' }
> = {
  open: { ar: 'الحجز مفتوح', en: 'Open', variant: 'success' },
  almostFull: { ar: 'فاضل أماكن قليلة', en: 'Few seats left', variant: 'warning' },
  full: { ar: 'مكتملة', en: 'Full', variant: 'destructive' },
}

function useGroupView(group: GroupListItemDto) {
  const { pick, locale } = useI18n()
  const id = group.id ?? ''
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  const range = scheduleTimeRange(group.schedules)

  return {
    id,
    lang,
    href: `/groups/${id}`,
    name: group.name?.trim() || '—',
    subjectKey: subjectKeyFor(group.subject),
    subjectName: pick(group.subject?.nameAr, group.subject?.nameEn),
    gradeName: pick(group.grade?.nameAr, group.grade?.nameEn),
    pattern: coverPatternFor(id),
    price: pricingToBreakdown(group.pricing),
    currency: group.pricing?.currency ?? 'EGP',
    days: formatScheduleDays(group.schedules, locale),
    range,
    typeLabel: group.type ? TYPE_LABELS[group.type][lang] : (group.typeName ?? ''),
    modeLabel: group.teachingMode
      ? MODE_LABELS[group.teachingMode][lang]
      : (group.teachingModeName ?? ''),
    status: seatStatus(group),
  }
}

export function GroupCard({ group, className }: { group: GroupListItemDto; className?: string }) {
  const { t, locale, formatTime } = useI18n()
  const view = useGroupView(group)
  const teacher = group.teacher
  const status = STATUS_COPY[view.status]

  return (
    <HoverLift className={className}>
      <Card className="flex h-full flex-col overflow-hidden">
        {/* Cover */}
        <div className="relative h-32 overflow-hidden">
          <CoverArt pattern={view.pattern} subject={view.subjectKey} />

          <div className="absolute inset-x-4 top-4 flex items-start justify-between gap-2">
            <SubjectChip subject={view.subjectKey} />
            {!group.isPublished && (
              <Badge variant="outline" size="sm" className="border-white/25 bg-black/30 text-white">
                {t('common.draft')}
              </Badge>
            )}
          </div>

          <div className="absolute inset-x-4 bottom-3 flex items-end justify-between gap-2">
            {view.typeLabel ? (
              <Badge
                variant="outline"
                size="sm"
                className="border-white/25 bg-black/25 text-white backdrop-blur-sm"
              >
                {view.typeLabel}
              </Badge>
            ) : (
              <span />
            )}
            {view.price.percentOff > 0 && (
              <Badge variant="gold" size="sm" className="tnum shadow-soft">
                <Sparkles className="size-3" />
                {locale === 'ar-EG'
                  ? `خصم ${view.price.percentOff}%`
                  : `${view.price.percentOff}% off`}
              </Badge>
            )}
          </div>
        </div>

        {/* Body */}
        <div className="flex flex-1 flex-col gap-4 p-5">
          <div>
            <div className="flex items-start justify-between gap-3">
              <h3 className="min-w-0">
                <Link
                  to={view.href}
                  className="font-display text-heading-sm font-bold leading-snug text-foreground transition-colors hover:text-primary"
                >
                  {view.name}
                </Link>
              </h3>
              <Badge variant={status.variant} size="sm" className="shrink-0">
                {status[view.lang]}
              </Badge>
            </div>

            {teacher?.id && (
              <Link
                to={`/teachers/${teacher.id}`}
                className="mt-2.5 inline-flex items-center gap-2 text-body-sm text-muted-foreground transition-colors hover:text-foreground"
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
          </div>

          {/* Schedule facts */}
          <ul className="space-y-2 text-body-sm text-muted-foreground">
            {view.days && (
              <li className="flex items-center gap-2.5">
                <CalendarDays className="size-4 shrink-0 text-primary" aria-hidden />
                {view.days}
              </li>
            )}
            {view.range && (
              <li className="flex items-center gap-2.5">
                <Clock className="size-4 shrink-0 text-primary" aria-hidden />
                <span className="tnum">
                  {formatTime(view.range.start)} — {formatTime(view.range.end)}
                </span>
              </li>
            )}
            <li className="flex items-center gap-2.5">
              {group.teachingMode === 'Online' ? (
                <Monitor className="size-4 shrink-0 text-primary" aria-hidden />
              ) : (
                <MapPin className="size-4 shrink-0 text-primary" aria-hidden />
              )}
              <span className="truncate">{group.location || view.modeLabel}</span>
            </li>
          </ul>

          <div className="flex flex-wrap items-center gap-2">
            {view.gradeName && (
              <Badge variant="default" size="sm">
                {view.gradeName}
              </Badge>
            )}
            {view.subjectName && (
              <Badge variant="default" size="sm">
                {view.subjectName}
              </Badge>
            )}
            <Rating
              value={group.rating ?? 0}
              count={group.reviewCount}
              size="sm"
              className="ms-auto"
            />
          </div>

          {(group.capacity ?? 0) > 0 && (
            <SeatsMeter
              taken={group.enrolledStudentsCount ?? 0}
              total={group.capacity ?? 0}
            />
          )}

          {/* Footer */}
          <div className="mt-auto flex items-end justify-between gap-3 border-t border-border pt-4">
            <PriceTag
              breakdown={view.price}
              period={locale === 'ar-EG' ? '/ شهريًا' : '/ month'}
              size="sm"
              currency={view.currency}
            />
            <div className="flex items-center gap-2">
              {group.whatsAppLink && (
                <WhatsAppIconButton
                  href={group.whatsAppLink}
                  className="size-10"
                  label={
                    locale === 'ar-EG'
                      ? `اسأل عن ${view.name} على واتساب`
                      : `Ask about ${view.name} on WhatsApp`
                  }
                />
              )}
              <Button
                asChild
                size="md"
                variant={view.status === 'full' ? 'outline' : 'primary'}
              >
                <Link to={view.href}>{t('common.details')}</Link>
              </Button>
            </div>
          </div>
        </div>
      </Card>
    </HoverLift>
  )
}

/** Dense variant used on teacher profiles and dashboards. */
export function GroupMiniCard({ group, className }: { group: GroupListItemDto; className?: string }) {
  const { t, locale, formatTime } = useI18n()
  const view = useGroupView(group)

  return (
    <Card className={cn('flex flex-col gap-3 p-4 sm:flex-row sm:items-center', className)} interactive>
      <div className="min-w-0 flex-1">
        <Link
          to={view.href}
          className="font-display text-body-sm font-bold text-foreground hover:text-primary"
        >
          {view.name}
        </Link>
        <p className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-caption text-muted-foreground">
          {view.days && <span>{view.days}</span>}
          {view.range && (
            <span className="tnum">
              {formatTime(view.range.start)} — {formatTime(view.range.end)}
            </span>
          )}
          {view.modeLabel && <span>{view.modeLabel}</span>}
        </p>
      </div>
      <div className="flex shrink-0 items-center justify-between gap-3 sm:justify-end">
        <PriceTag
          breakdown={view.price}
          period={locale === 'ar-EG' ? '/ شهر' : '/ mo'}
          size="sm"
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
