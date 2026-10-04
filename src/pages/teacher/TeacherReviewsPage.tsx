import * as React from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { MessageSquare, Star } from 'lucide-react'
import { teachersApi } from '@/api/endpoints/catalog'
import { teacherProfileApi } from '@/api/endpoints/teacher'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useAuth } from '@/hooks/use-auth'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { avatarTraitsFor, imageUrl } from '@/lib/identity'
import { transitions } from '@/lib/motion'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { StatCard } from '@/components/common/StatCard'
import { Rating } from '@/components/common/Rating'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { Pagination } from '@/components/common/Pagination'
import { RowSkeleton, StatSkeleton } from '@/components/ui/skeleton'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { FilterChip } from '@/components/common/controls'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'

const PAGE_SIZE = 15
const RATING_FILTERS = [undefined, 5, 4, 3] as const

export default function TeacherReviewsPage() {
  const { t, locale, formatNumber, formatDate } = useI18n()
  usePageTitle(t('common.reviews'))

  const { user } = useAuth()
  const [page, setPage] = React.useState(1)
  const [minRating, setMinRating] = React.useState<number>()

  const profile = useQuery({
    queryKey: qk.teacher.profile,
    queryFn: teacherProfileApi.get,
  })

  const teacherId = user?.teacherId ?? profile.data?.id ?? ''

  const query = React.useMemo(
    () => ({ Page: page, PageSize: PAGE_SIZE, MinRating: minRating }),
    [page, minRating],
  )

  const reviews = useQuery({
    queryKey: qk.teachers.reviews(teacherId, query),
    queryFn: () => teachersApi.reviews(teacherId, query),
    enabled: Boolean(teacherId),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => setPage(1), [minRating])

  const items = reviews.data?.items ?? []
  const summary = profile.data

  /* Distribution over the page we have; the API has no histogram endpoint. */
  const distribution = React.useMemo(() => {
    const counts: Record<number, number> = { 1: 0, 2: 0, 3: 0, 4: 0, 5: 0 }
    items.forEach((r) => {
      const rounded = Math.round(r.rating ?? 0)
      if (rounded >= 1 && rounded <= 5) counts[rounded] += 1
    })
    return counts
  }, [items])

  return (
    <>
      <DashboardHeader
        title={t('common.reviews')}
        description={
          locale === 'ar-EG'
            ? 'تقييمات طلابك على بروفايلك ومجموعاتك وكورساتك.'
            : 'What your students say about your profile, groups, and courses.'
        }
      />

      {profile.isPending ? (
        <StatSkeleton count={3} />
      ) : (
        <RevealGroup className="grid gap-4 sm:grid-cols-3">
          <RevealItem>
            <StatCard
              label={t('dashboard.averageRating')}
              value={summary?.rating ?? 0}
              decimals={1}
              icon={<Star />}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={t('common.reviews')}
              value={summary?.reviewCount ?? 0}
              icon={<MessageSquare />}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={t('common.students')}
              value={summary?.studentsCount ?? 0}
              icon={<Star />}
            />
          </RevealItem>
        </RevealGroup>
      )}

      <div className="mt-8 grid gap-8 xl:grid-cols-[1fr,2fr]">
        <Card className="h-fit p-5">
          <h2 className="font-display text-heading-sm text-foreground">
            {locale === 'ar-EG' ? 'توزيع التقييمات' : 'Rating breakdown'}
          </h2>
          <div className="mt-4 space-y-2.5">
            {[5, 4, 3, 2, 1].map((star) => {
              const count = distribution[star] ?? 0
              const total = items.length || 1
              const pct = Math.round((count / total) * 100)
              return (
                <div key={star} className="flex items-center gap-3">
                  <span className="tnum flex w-8 items-center gap-1 text-caption text-muted-foreground">
                    {star}
                    <Star className="size-3 fill-accent text-accent" />
                  </span>
                  <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-muted">
                    <motion.div
                      initial={{ width: 0 }}
                      animate={{ width: `${pct}%` }}
                      transition={transitions.slow}
                      className="h-full rounded-full bg-accent"
                    />
                  </div>
                  <span className="tnum w-10 text-end text-caption text-muted-foreground">
                    {formatNumber(count)}
                  </span>
                </div>
              )
            })}
          </div>
        </Card>

        <div>
          <div className="mb-5 flex flex-wrap gap-2">
            {RATING_FILTERS.map((value) => (
              <FilterChip
                key={value ?? 'all'}
                label={value ? `${value}+` : t('common.all')}
                active={minRating === value}
                onClick={() => setMinRating(value)}
              />
            ))}
          </div>

          <div className={cn('transition-opacity', reviews.isFetching && 'opacity-60')}>
            {reviews.isPending ? (
              <RowSkeleton count={4} />
            ) : reviews.isError ? (
              <QueryError error={reviews.error} onRetry={() => reviews.refetch()} />
            ) : items.length === 0 ? (
              <EmptyState
                spot="inbox"
                title={locale === 'ar-EG' ? 'مفيش تقييمات لسه' : 'No reviews yet'}
                description={
                  locale === 'ar-EG'
                    ? 'أول ما طلابك يقيّموك هتلاقي رأيهم هنا.'
                    : 'Student reviews will appear here once they rate you.'
                }
              />
            ) : (
              <div className="space-y-4">
                {items.map((review, i) => (
                  <motion.div
                    key={review.id}
                    initial={{ opacity: 0, y: 10 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ ...transitions.base, delay: Math.min(i * 0.04, 0.3) }}
                  >
                    <Card className="p-5">
                      <div className="flex items-start gap-3.5">
                        <Avatar
                          traits={avatarTraitsFor(review.studentId ?? '')}
                          seed={review.studentId ?? ''}
                          name={review.studentName ?? ''}
                          src={imageUrl(review.studentImageUrl)}
                          size="sm"
                        />
                        <div className="min-w-0 flex-1">
                          <div className="flex flex-wrap items-center gap-2">
                            <p className="font-display text-body-sm font-bold text-foreground">
                              {review.studentName}
                            </p>
                            <Rating value={review.rating ?? 0} size="sm" />
                            {!review.isApproved && (
                              <Badge variant="warning" size="sm">
                                {locale === 'ar-EG' ? 'تحت المراجعة' : 'Pending review'}
                              </Badge>
                            )}
                          </div>

                          {(review.groupName || review.courseTitle) && (
                            <p className="mt-0.5 text-caption text-muted-foreground">
                              {review.groupName ?? review.courseTitle}
                            </p>
                          )}

                          {review.comment && (
                            <p className="mt-2.5 text-body-sm leading-relaxed text-muted-foreground">
                              {review.comment}
                            </p>
                          )}

                          {review.createdAt && (
                            <p className="tnum mt-2 text-caption text-muted-foreground/70">
                              {formatDate(review.createdAt, 'd MMM yyyy')}
                            </p>
                          )}
                        </div>
                      </div>
                    </Card>
                  </motion.div>
                ))}
              </div>
            )}
          </div>

          {reviews.data && items.length > 0 && (
            <Pagination page={reviews.data} onChange={setPage} className="mt-8" />
          )}
        </div>
      </div>
    </>
  )
}
