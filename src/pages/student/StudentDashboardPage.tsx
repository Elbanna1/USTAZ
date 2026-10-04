import { Link } from 'react-router-dom'
import { useQueries, useQuery } from '@tanstack/react-query'
import {
  ArrowRight,
  BookOpen,
  CalendarDays,
  ClipboardCheck,
  FileText,
  GraduationCap,
  Sparkles,
  TrendingUp,
  Users,
  Wallet,
} from 'lucide-react'
import { motion } from 'framer-motion'
import { studentApi } from '@/api/endpoints/student'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useAuth } from '@/hooks/use-auth'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { imageUrl, subjectKeyFor } from '@/lib/identity'
import { transitions } from '@/lib/motion'
import { DashboardHeader, DashboardSection } from '@/components/dashboard/DashboardHeader'
import { StatCard } from '@/components/common/StatCard'
import { StatSkeleton, RowSkeleton, CardGridSkeleton } from '@/components/ui/skeleton'
import { QueryError } from '@/components/common/QueryBoundary'
import { EmptyState } from '@/components/common/states'
import { SlotCard } from '@/components/schedule/SlotCard'
import { GroupMiniCard } from '@/components/groups/GroupCard'
import { Card } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Progress } from '@/components/ui/primitives'
import { SubjectTile } from '@/components/illustrations/SubjectIcon'
import { CoverImage } from '@/components/illustrations/CoverArt'
import { coverPatternFor } from '@/lib/identity'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'

export default function StudentDashboardPage() {
  const { t, locale, formatNumber, formatCurrency, formatTimeAgo } = useI18n()
  const { user } = useAuth()
  usePageTitle(t('dashboard.overview'))

  const dashboard = useQuery({
    queryKey: qk.student.dashboard,
    queryFn: studentApi.dashboard,
  })

  const [progress, activity, homework, exams] = useQueries({
    queries: [
      { queryKey: qk.student.progress, queryFn: studentApi.progress },
      { queryKey: qk.student.activity, queryFn: () => studentApi.recentActivity(8) },
      {
        queryKey: qk.student.homework({ PageSize: 5, Status: 'Pending' }),
        queryFn: () => studentApi.homework({ PageSize: 5 }),
      },
      {
        queryKey: qk.student.exams({ PageSize: 4, Upcoming: true }),
        queryFn: () => studentApi.exams({ PageSize: 4, Upcoming: true }),
      },
    ],
  })

  const data = dashboard.data
  const firstName = user?.firstName || user?.displayName || ''

  return (
    <>
      <DashboardHeader
        title={t('dashboard.welcome', { name: firstName })}
        description={t('dashboard.todayIs', {
          date: new Intl.DateTimeFormat(locale === 'ar-EG' ? 'ar-EG' : 'en-GB', {
            weekday: 'long',
            day: 'numeric',
            month: 'long',
          }).format(new Date()),
        })}
        actions={
          <>
            <Button asChild variant="outline" size="sm">
              <Link to="/student/schedule">
                <CalendarDays className="size-4" />
                {t('nav.schedule')}
              </Link>
            </Button>
            <Button asChild size="sm">
              <Link to="/courses">
                <BookOpen className="size-4" />
                {locale === 'ar-EG' ? 'تصفّح الكورسات' : 'Browse courses'}
              </Link>
            </Button>
          </>
        }
      />

      {/* ------------------------------ KPI row ------------------------------ */}
      {dashboard.isPending ? (
        <StatSkeleton count={4} />
      ) : dashboard.isError ? (
        <QueryError error={dashboard.error} onRetry={() => dashboard.refetch()} />
      ) : (
        <RevealGroup className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <RevealItem>
            <StatCard
              label={t('dashboard.myGroups')}
              value={data?.activeGroupsCount ?? 0}
              icon={<Users />}
              description={locale === 'ar-EG' ? 'مجموعات نشطة' : 'Active groups'}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={t('dashboard.myCourses')}
              value={data?.activeCoursesCount ?? 0}
              icon={<BookOpen />}
              description={
                locale === 'ar-EG'
                  ? `${formatNumber(data?.completedCoursesCount ?? 0)} مكتمل`
                  : `${formatNumber(data?.completedCoursesCount ?? 0)} completed`
              }
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={t('dashboard.progress')}
              value={Math.round(data?.averageCourseProgress ?? 0)}
              suffix="%"
              icon={<TrendingUp />}
              description={locale === 'ar-EG' ? 'متوسط التقدّم' : 'Average progress'}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={t('dashboard.payments')}
              value={data?.totalSpent ?? 0}
              icon={<Wallet />}
              description={formatCurrency(data?.totalSpent ?? 0)}
            />
          </RevealItem>
        </RevealGroup>
      )}

      <div className="mt-8 grid gap-8 xl:grid-cols-[1.6fr,1fr]">
        <div className="space-y-8">
          {/* --------------------- Continue learning --------------------- */}
          <DashboardSection
            title={t('dashboard.continueLearning')}
            action={
              <Button asChild variant="ghost" size="sm">
                <Link to="/student/courses">
                  {t('common.viewAll')}
                  <ArrowRight className="size-4 flip-rtl" />
                </Link>
              </Button>
            }
          >
            {progress.isPending ? (
              <CardGridSkeleton count={2} className="sm:grid-cols-2 lg:grid-cols-2" />
            ) : progress.isError ? (
              <QueryError error={progress.error} onRetry={() => progress.refetch()} compact />
            ) : (progress.data ?? []).length === 0 ? (
              <EmptyState
                spot="courses"
                compact
                title={locale === 'ar-EG' ? 'لسه مبدأتش أي كورس' : 'No courses started yet'}
                description={
                  locale === 'ar-EG'
                    ? 'اشترك في كورس وابدأ تتعلّم من أي مكان.'
                    : 'Enroll in a course and start learning anywhere.'
                }
                action={{ label: t('nav.courses'), href: '/courses' }}
              />
            ) : (
              <div className="grid gap-4 sm:grid-cols-2">
                {(progress.data ?? []).slice(0, 4).map((course, i) => (
                  <motion.div
                    key={course.courseId}
                    initial={{ opacity: 0, y: 12 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ ...transitions.base, delay: i * 0.06 }}
                  >
                    <Card className="group h-full overflow-hidden" interactive>
                      <Link to={`/student/courses/${course.courseId}`} className="block">
                        <div className="relative h-24 overflow-hidden">
                          <CoverImage
                            src={imageUrl(course.thumbnailUrl)}
                            pattern={coverPatternFor(course.courseId ?? '')}
                            intensity="bold"
                          />
                          <div className="absolute inset-x-3 bottom-2">
                            <Badge
                              variant="outline"
                              size="sm"
                              className="tnum border-white/25 bg-black/35 text-white backdrop-blur-sm"
                            >
                              {Math.round(course.progressPercentage ?? 0)}%
                            </Badge>
                          </div>
                        </div>
                        <div className="space-y-3 p-4">
                          <p className="line-clamp-2-rtl font-display text-body-sm font-bold leading-snug text-foreground transition-colors group-hover:text-primary">
                            {course.courseTitle}
                          </p>
                          {course.teacher?.displayName && (
                            <p className="truncate text-caption text-muted-foreground">
                              {course.teacher.displayName}
                            </p>
                          )}
                          <Progress value={course.progressPercentage ?? 0} className="h-1.5" />
                          <p className="tnum text-caption text-muted-foreground">
                            {t('courses.progressLabel', {
                              done: formatNumber(
                                Math.round(
                                  ((course.progressPercentage ?? 0) / 100) *
                                    (course.lessonsCount ?? 0),
                                ),
                              ),
                              total: formatNumber(course.lessonsCount ?? 0),
                            })}
                          </p>
                        </div>
                      </Link>
                    </Card>
                  </motion.div>
                ))}
              </div>
            )}
          </DashboardSection>

          {/* ------------------------ Active groups ------------------------ */}
          {(data?.activeGroups?.length ?? 0) > 0 && (
            <DashboardSection title={t('dashboard.myGroups')}>
              <div className="space-y-3">
                {data!.activeGroups!.slice(0, 4).map((group) => (
                  <GroupMiniCard key={group.id} group={group} />
                ))}
              </div>
            </DashboardSection>
          )}

          {/* --------------------------- Homework -------------------------- */}
          <DashboardSection
            title={t('dashboard.homework')}
            action={
              <Button asChild variant="ghost" size="sm">
                <Link to="/student/homework">
                  {t('common.viewAll')}
                  <ArrowRight className="size-4 flip-rtl" />
                </Link>
              </Button>
            }
          >
            {homework.isPending ? (
              <RowSkeleton count={3} />
            ) : (homework.data?.items ?? []).length === 0 ? (
              <EmptyState
                spot="homework"
                compact
                title={locale === 'ar-EG' ? 'مفيش واجبات مستحقة' : 'No homework due'}
                description={
                  locale === 'ar-EG' ? 'كل حاجة متسلّمة. شغل حلو!' : 'All caught up. Nice work!'
                }
              />
            ) : (
              <div className="space-y-2.5">
                {homework.data!.items.map((item) => (
                  <Link
                    key={item.id}
                    to={`/student/homework/${item.id}`}
                    className="flex items-center gap-3.5 rounded-lg border border-border bg-card p-3.5 transition-colors hover:border-foreground/20"
                  >
                    <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary">
                      <ClipboardCheck className="size-5" />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block truncate font-display text-body-sm font-semibold text-foreground">
                        {item.title}
                      </span>
                      <span className="block truncate text-caption text-muted-foreground">
                        {item.groupName}
                        {item.dueAt &&
                          ` · ${t('homework.dueAt')} ${new Intl.DateTimeFormat(
                            locale === 'ar-EG' ? 'ar-EG' : 'en-GB',
                            { day: 'numeric', month: 'short' },
                          ).format(new Date(item.dueAt))}`}
                      </span>
                    </span>
                    <ArrowRight className="size-4 shrink-0 flip-rtl text-muted-foreground" />
                  </Link>
                ))}
              </div>
            )}
          </DashboardSection>
        </div>

        {/* ------------------------------ Sidebar ----------------------------- */}
        <div className="space-y-8">
          <DashboardSection title={t('dashboard.upcomingClasses')}>
            {dashboard.isPending ? (
              <RowSkeleton count={3} />
            ) : (data?.upcomingClasses ?? []).length === 0 ? (
              <EmptyState
                spot="calendar"
                compact
                title={t('dashboard.noUpcomingClasses')}
                action={{ label: t('nav.groups'), href: '/groups' }}
              />
            ) : (
              <div className="space-y-2.5">
                {data!.upcomingClasses!.slice(0, 5).map((slot) => (
                  <SlotCard key={slot.id} slot={slot} variant="compact" />
                ))}
              </div>
            )}
          </DashboardSection>

          <DashboardSection title={t('exams.upcoming')}>
            {exams.isPending ? (
              <RowSkeleton count={2} />
            ) : (exams.data?.items ?? []).length === 0 ? (
              <EmptyState
                spot="exams"
                compact
                title={locale === 'ar-EG' ? 'مفيش امتحانات قريبة' : 'No upcoming exams'}
              />
            ) : (
              <div className="space-y-2.5">
                {exams.data!.items.map((exam) => (
                  <Link
                    key={exam.id}
                    to={`/student/exams/${exam.id}`}
                    className="flex items-center gap-3 rounded-lg border border-border bg-card p-3.5 transition-colors hover:border-foreground/20"
                  >
                    {exam.subjectNameEn || exam.subjectNameAr ? (
                      <SubjectTile
                        subject={subjectKeyFor({
                          nameAr: exam.subjectNameAr,
                          nameEn: exam.subjectNameEn,
                        })}
                        size="sm"
                      />
                    ) : (
                      <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-accent/12 text-accent-strong">
                        <FileText className="size-5" />
                      </span>
                    )}
                    <span className="min-w-0 flex-1">
                      <span className="block truncate font-display text-body-sm font-semibold text-foreground">
                        {exam.title}
                      </span>
                      {exam.examDate && (
                        <span className="tnum block truncate text-caption text-muted-foreground">
                          {new Intl.DateTimeFormat(locale === 'ar-EG' ? 'ar-EG' : 'en-GB', {
                            day: 'numeric',
                            month: 'short',
                            hour: 'numeric',
                            minute: '2-digit',
                          }).format(new Date(exam.examDate))}
                        </span>
                      )}
                    </span>
                  </Link>
                ))}
              </div>
            )}
          </DashboardSection>

          <DashboardSection title={t('dashboard.recentActivity')}>
            {activity.isPending ? (
              <RowSkeleton count={4} />
            ) : (activity.data ?? []).length === 0 ? (
              <Card className="p-5 text-center text-body-sm text-muted-foreground">
                {locale === 'ar-EG' ? 'لسه مفيش نشاط.' : 'No activity yet.'}
              </Card>
            ) : (
              <Card className="p-2">
                <ol className="relative space-y-1">
                  {(activity.data ?? []).map((item, i) => (
                    <motion.li
                      key={`${item.referenceId ?? item.title}-${i}`}
                      initial={{ opacity: 0, x: 8 }}
                      animate={{ opacity: 1, x: 0 }}
                      transition={{ ...transitions.fast, delay: i * 0.04 }}
                      className="flex gap-3 rounded-lg p-2.5 transition-colors hover:bg-muted/50"
                    >
                      <span className="relative mt-1.5 flex flex-col items-center">
                        <span className="size-2 shrink-0 rounded-full bg-primary" />
                        {i < (activity.data?.length ?? 0) - 1 && (
                          <span className="mt-1 w-px flex-1 bg-border" />
                        )}
                      </span>
                      <span className="min-w-0 flex-1 pb-1">
                        <span className="block font-display text-body-sm font-semibold text-foreground">
                          {item.title}
                        </span>
                        {item.description && (
                          <span className="block text-caption text-muted-foreground">
                            {item.description}
                          </span>
                        )}
                        {item.occurredAt && (
                          <span className="mt-0.5 block text-[0.6875rem] text-muted-foreground/70">
                            {formatTimeAgo(item.occurredAt)}
                          </span>
                        )}
                      </span>
                    </motion.li>
                  ))}
                </ol>
              </Card>
            )}
          </DashboardSection>

          {/* ------------------------ Quick actions ------------------------ */}
          <DashboardSection title={t('dashboard.quickActions')}>
            <div className="grid grid-cols-2 gap-3">
              {[
                { href: '/student/grades', icon: GraduationCap, label: t('dashboard.grades') },
                { href: '/student/attendance', icon: CalendarDays, label: t('dashboard.attendance') },
                { href: '/student/certificates', icon: Sparkles, label: t('dashboard.certificates') },
                { href: '/student/payments', icon: Wallet, label: t('dashboard.payments') },
              ].map((action) => (
                <Link
                  key={action.href}
                  to={action.href}
                  className={cn(
                    'flex flex-col items-center gap-2 rounded-xl border border-border bg-card p-4 text-center transition-all duration-200',
                    'hover:-translate-y-0.5 hover:border-primary/30 hover:shadow-soft',
                  )}
                >
                  <span className="grid size-10 place-items-center rounded-lg bg-primary/10 text-primary">
                    <action.icon className="size-5" />
                  </span>
                  <span className="font-display text-caption font-semibold text-foreground">
                    {action.label}
                  </span>
                </Link>
              ))}
            </div>
          </DashboardSection>
        </div>
      </div>
    </>
  )
}
