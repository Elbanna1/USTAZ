import * as React from 'react'
import { Link } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { BookOpen, Users } from 'lucide-react'
import { studentApi } from '@/api/endpoints/student'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { coverPatternFor, imageUrl } from '@/lib/identity'
import { transitions } from '@/lib/motion'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { Pagination } from '@/components/common/Pagination'
import { StatusBadge } from '@/components/common/StatusBadge'
import { CardGridSkeleton } from '@/components/ui/skeleton'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Progress } from '@/components/ui/primitives'
import { CoverImage } from '@/components/illustrations/CoverArt'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'

const PAGE_SIZE = 12

export default function StudentCoursesPage() {
  const { t, locale, formatNumber, formatCurrency } = useI18n()
  usePageTitle(t('dashboard.myCourses'))

  const [coursePage, setCoursePage] = React.useState(1)
  const [groupPage, setGroupPage] = React.useState(1)

  const courseQuery = React.useMemo(() => ({ Page: coursePage, PageSize: PAGE_SIZE }), [coursePage])
  const groupQuery = React.useMemo(() => ({ Page: groupPage, PageSize: PAGE_SIZE }), [groupPage])

  const courses = useQuery({
    queryKey: qk.student.courseEnrollments(courseQuery),
    queryFn: () => studentApi.courseEnrollments(courseQuery),
    placeholderData: keepPreviousData,
  })

  const groups = useQuery({
    queryKey: qk.student.groupEnrollments(groupQuery),
    queryFn: () => studentApi.groupEnrollments(groupQuery),
    placeholderData: keepPreviousData,
  })

  const courseItems = courses.data?.items ?? []
  const groupItems = groups.data?.items ?? []

  return (
    <>
      <DashboardHeader
        title={t('dashboard.myCourses')}
        description={
          locale === 'ar-EG'
            ? 'كل الكورسات والمجموعات اللي مشترك فيها.'
            : 'Every course and group you are enrolled in.'
        }
        actions={
          <Button asChild size="sm">
            <Link to="/courses">
              <BookOpen className="size-4" />
              {locale === 'ar-EG' ? 'تصفّح الكورسات' : 'Browse courses'}
            </Link>
          </Button>
        }
      />

      <Tabs defaultValue="courses">
        <TabsList>
          <TabsTrigger value="courses">
            <BookOpen className="size-4" />
            {t('common.courses')}
            {courses.data && (
              <span className="tnum ms-1.5 rounded-full bg-muted px-1.5 text-[0.6875rem]">
                {formatNumber(courses.data.totalCount)}
              </span>
            )}
          </TabsTrigger>
          <TabsTrigger value="groups">
            <Users className="size-4" />
            {t('common.groups')}
            {groups.data && (
              <span className="tnum ms-1.5 rounded-full bg-muted px-1.5 text-[0.6875rem]">
                {formatNumber(groups.data.totalCount)}
              </span>
            )}
          </TabsTrigger>
        </TabsList>

        {/* ------------------------------ Courses ----------------------------- */}
        <TabsContent value="courses" className="mt-6">
          <div className={cn('transition-opacity', courses.isFetching && 'opacity-60')}>
            {courses.isPending ? (
              <CardGridSkeleton count={3} />
            ) : courses.isError ? (
              <QueryError error={courses.error} onRetry={() => courses.refetch()} />
            ) : courseItems.length === 0 ? (
              <EmptyState
                spot="courses"
                title={locale === 'ar-EG' ? 'لسه مشتركتش في كورس' : 'No course enrollments yet'}
                description={
                  locale === 'ar-EG'
                    ? 'اشترك في كورس وابدأ تذاكر من أي مكان.'
                    : 'Enroll in a course and start studying anywhere.'
                }
                action={{ label: t('nav.courses'), href: '/courses' }}
              />
            ) : (
              <RevealGroup className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
                {courseItems.map((enrollment, i) => (
                  <RevealItem key={enrollment.id} className="h-full">
                    <motion.div
                      initial={{ opacity: 0, y: 12 }}
                      animate={{ opacity: 1, y: 0 }}
                      transition={{ ...transitions.base, delay: Math.min(i * 0.05, 0.3) }}
                      className="h-full"
                    >
                      <Card className="group flex h-full flex-col overflow-hidden" interactive>
                        <Link
                          to={`/student/courses/${enrollment.courseId}`}
                          className="flex h-full flex-col"
                        >
                          <div className="relative aspect-[16/9] overflow-hidden">
                            <CoverImage
                              src={imageUrl(enrollment.courseThumbnailUrl)}
                              pattern={coverPatternFor(enrollment.courseId ?? '')}
                              intensity="bold"
                            />
                            <div className="absolute inset-x-4 top-4 flex justify-between gap-2">
                              <StatusBadge
                                status={enrollment.status}
                                fallbackLabel={enrollment.statusName}
                              />
                              <Badge
                                variant="outline"
                                size="sm"
                                className="tnum border-white/25 bg-black/35 text-white backdrop-blur-sm"
                              >
                                {Math.round(enrollment.progressPercentage ?? 0)}%
                              </Badge>
                            </div>
                          </div>

                          <div className="flex flex-1 flex-col gap-3 p-5">
                            <h3 className="line-clamp-2-rtl font-display text-body font-bold leading-snug text-foreground transition-colors group-hover:text-primary">
                              {enrollment.courseTitle}
                            </h3>
                            {enrollment.teacher?.displayName && (
                              <p className="truncate text-caption text-muted-foreground">
                                {enrollment.teacher.displayName}
                              </p>
                            )}

                            <div className="mt-auto space-y-2 border-t border-border pt-4">
                              <Progress
                                value={enrollment.progressPercentage ?? 0}
                                className="h-1.5"
                              />
                              <div className="flex items-center justify-between text-caption text-muted-foreground">
                                <span className="tnum">
                                  {formatNumber(enrollment.lessonsCount ?? 0)}{' '}
                                  {locale === 'ar-EG' ? 'درس' : 'lessons'}
                                </span>
                                <span className="tnum font-semibold text-foreground">
                                  {formatCurrency(enrollment.finalPrice ?? 0)}
                                </span>
                              </div>
                            </div>
                          </div>
                        </Link>
                      </Card>
                    </motion.div>
                  </RevealItem>
                ))}
              </RevealGroup>
            )}
          </div>

          {courses.data && courseItems.length > 0 && (
            <Pagination page={courses.data} onChange={setCoursePage} className="mt-8" />
          )}
        </TabsContent>

        {/* ------------------------------- Groups ----------------------------- */}
        <TabsContent value="groups" className="mt-6">
          <div className={cn('transition-opacity', groups.isFetching && 'opacity-60')}>
            {groups.isPending ? (
              <CardGridSkeleton count={3} />
            ) : groups.isError ? (
              <QueryError error={groups.error} onRetry={() => groups.refetch()} />
            ) : groupItems.length === 0 ? (
              <EmptyState
                spot="groups"
                title={locale === 'ar-EG' ? 'لسه مشتركتش في مجموعة' : 'No group enrollments yet'}
                description={
                  locale === 'ar-EG'
                    ? 'المجموعات فيها حصص بمواعيد ثابتة مع المدرّس.'
                    : 'Groups run live sessions on a fixed weekly schedule.'
                }
                action={{ label: t('nav.groups'), href: '/groups' }}
              />
            ) : (
              <RevealGroup className="space-y-4">
                {groupItems.map((enrollment) => (
                  <RevealItem key={enrollment.id}>
                    <Card className="p-5">
                      <div className="flex flex-wrap items-start justify-between gap-3">
                        <div className="min-w-0">
                          <Link
                            to={`/groups/${enrollment.groupId}`}
                            className="font-display text-heading-sm font-bold text-foreground hover:text-primary"
                          >
                            {enrollment.groupName}
                          </Link>
                          {enrollment.teacher?.displayName && (
                            <p className="mt-0.5 text-body-sm text-muted-foreground">
                              {enrollment.teacher.displayName}
                            </p>
                          )}
                        </div>
                        <div className="flex items-center gap-2">
                          <StatusBadge
                            status={enrollment.status}
                            fallbackLabel={enrollment.statusName}
                          />
                          <span className="tnum font-display text-body-sm font-bold text-foreground">
                            {formatCurrency(enrollment.finalPrice ?? 0)}
                          </span>
                        </div>
                      </div>

                      {(enrollment.schedule?.length ?? 0) > 0 && (
                        <ul className="mt-4 grid gap-2 sm:grid-cols-2">
                          {enrollment.schedule!.map((slot) => (
                            <li
                              key={slot.id}
                              className="tnum rounded-lg border border-border bg-muted/30 px-3 py-2 text-caption text-muted-foreground"
                            >
                              {slot.dayName} · {slot.startTime?.slice(0, 5)} —{' '}
                              {slot.endTime?.slice(0, 5)}
                            </li>
                          ))}
                        </ul>
                      )}
                    </Card>
                  </RevealItem>
                ))}
              </RevealGroup>
            )}
          </div>

          {groups.data && groupItems.length > 0 && (
            <Pagination page={groups.data} onChange={setGroupPage} className="mt-8" />
          )}
        </TabsContent>
      </Tabs>
    </>
  )
}
