import { Link } from 'react-router-dom'
import { BadgeCheck } from 'lucide-react'
import type { ReviewDto } from '@/api/schema'
import { cn, pickFromSeed } from '@/lib/utils'
import { imageUrl } from '@/lib/identity'
import { useI18n } from '@/i18n'
import { Card } from '@/components/ui/card'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { Rating } from '@/components/common/Rating'
import type { AvatarTraits } from '@/types'

/** Student reviewers don't carry stored traits, so we derive a stable look. */
const SKINS = ['#E8B98D', '#F0C9A4', '#D9A575', '#F5D7B8', '#C98E5F']
const HAIRS = ['#241E1A', '#33231C', '#2E211C', '#4A2E22', '#1F1B18']
const GARMENTS = ['#1E4A55', '#6B2D5B', '#2F4F3E', '#3B3A6B', '#8A4B2A', '#2C6B52']
const STYLES: AvatarTraits['style'][] = ['short', 'hijab', 'wavy', 'bun', 'beard', 'bald']

export function traitsFromSeed(seed: string): AvatarTraits {
  return {
    skin: pickFromSeed(SKINS, seed, 1),
    hair: pickFromSeed(HAIRS, seed, 2),
    garment: pickFromSeed(GARMENTS, seed, 3),
    accent: '#C9962F',
    style: pickFromSeed(STYLES, seed, 4),
    glasses: pickFromSeed([true, false, false], seed, 5),
  }
}

interface ReviewCardProps {
  review: ReviewDto
  className?: string
  /** Shows which teacher the review is about — used on aggregate feeds. */
  showTeacher?: boolean
}

export function ReviewCard({ review, className, showTeacher = false }: ReviewCardProps) {
  const { locale, formatDate } = useI18n()
  const seed = review.studentId ?? review.id ?? ''

  return (
    <Card className={cn('flex h-full flex-col gap-4 p-5', className)}>
      <div className="flex items-start gap-3">
        <Avatar
          traits={traitsFromSeed(seed)}
          seed={seed}
          name={review.studentName ?? ''}
          src={imageUrl(review.studentImageUrl)}
          size="md"
        />
        <div className="min-w-0 flex-1">
          <p className="flex items-center gap-1.5 font-display text-body-sm font-bold text-foreground">
            <span className="truncate">{review.studentName}</span>
            {review.isApproved && (
              <BadgeCheck
                className="size-4 shrink-0 text-primary"
                aria-label={locale === 'ar-EG' ? 'تقييم موثّق' : 'Verified review'}
              />
            )}
          </p>
          <p className="text-caption text-muted-foreground">
            {[review.groupName, review.courseTitle].filter(Boolean).join(' · ')}
            {review.createdAt
              ? `${review.groupName || review.courseTitle ? ' · ' : ''}${formatDate(review.createdAt, 'MMMM yyyy')}`
              : ''}
          </p>
        </div>
        <Rating value={review.rating ?? 0} size="sm" />
      </div>

      {showTeacher && review.teacherId && (
        <Link
          to={`/teachers/${review.teacherId}`}
          className="inline-flex w-fit items-center gap-1.5 rounded-full border border-border bg-muted/50 px-2.5 py-1 text-caption text-muted-foreground transition-colors hover:text-foreground"
        >
          {review.teacherName}
        </Link>
      )}

      {review.comment && (
        <p className="flex-1 text-body-sm leading-[1.9] text-foreground/90">{review.comment}</p>
      )}
    </Card>
  )
}
