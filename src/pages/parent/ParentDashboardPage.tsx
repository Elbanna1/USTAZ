import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import {
  AlertTriangle,
  CalendarDays,
  ClipboardCheck,
  FileText,
  GraduationCap,
  UserPlus,
  Users,
  Wallet,
} from 'lucide-react'
import { parentApi } from '@/api/endpoints/parent'
import type { ChildOverviewDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useAuth } from '@/hooks/use-auth'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { avatarTraitsFor, imageUrl } from '@/lib/identity'
import { transitions } from '@/lib/motion'
import { DashboardHeader, DashboardSection } from '@/components/dashboard/DashboardHeader'
import { StatCard } from '@/components/common/StatCard'
import { StatSkeleton, RowSkeleton } from '@/components/ui/skeleton'
import { QueryError } from '@/components/common/QueryBoundary'
import { EmptyState } from '@/components/common/states'
import { SlotCard } from '@/components/schedule/SlotCard'
import { StatusBadge } from '@/components/common/StatusBadge'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Progress } from '@/components/ui/primitives'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'

export default function ParentDashboardPage() {
  const { t, locale, formatNumber, formatCurrency, formatDate } = useI18n()
  const { user } = useAuth()
  usePageTitle(t('dashboard.overview'))

  const dashboard = useQuery({
    queryKey: qk.parent.dashboard,
    queryFn: parentApi.dashboard,
  })

  const data = dashboard.data
  const children = data?.children ?? []

  return (
    <>
      <DashboardHeader
        title={t('dashboard.welcome', { name: user?.firstName || user?.displayName || '' })}
        description={
          locale === 'ar-EG'
            ? 'متابعة أبنائك: الحضور، الدرجات، الواجبات، والمدفوعات.'
            : "Follow your children's attendance, grades, homework, and payments."
        }
        actions={
          <Button asChild size="sm">
            <Link to="/parent/children">
              <UserPlus className="size-4" />
              {t('parent.linkChild')}
            </Link>
          </Button>
        }
      />

      {dashboard.isPending ? (
        <StatSkeleton count={4} />
      ) : dashboard.isError ? (
        <QueryError error={dashboard.error} onRetry={() => dashboard.refetch()} />
      ) : (
        <RevealGroup className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <RevealItem>
            <StatCard
              label={t('parent.children')}
              value={data?.childrenCount ?? 0}
              icon={<Users />}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={locale === 'ar-EG' ? 'تنبيهات غياب' : 'Absence alerts'}
              value={data?.absenceAlertsCount ?? 0}
              icon={<AlertTriangle />}
              invertDelta
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={locale === 'ar-EG' ? 'واجبات مستحقة' : 'Pending homework'}
              value={data?.pendingHomeworkCount ?? 0}
              icon={<ClipboardCheck />}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={locale === 'ar-EG' ? 'مدفوعات مستحقة' : 'Outstanding'}
              value={data?.outstandingPayments ?? 0}
              icon={<Wallet />}
              description={formatCurrency(data?.outstandingPayments ?? 0)}
            />
          </RevealItem>
        </RevealGroup>
      )}

      <div className="mt-8 grid gap-8 xl:grid-cols-[1.6fr,1fr]">
        <div className="space-y-8">
          <DashboardSection
            title={t('parent.children')}
            action={
              <Button asChild variant="ghost" size="sm">
                <Link to="/parent/children">{t('common.viewAll')}</Link>
              </Button>
            }
          >
            {dashboard.isPending ? (
              <RowSkeleton count={2} />
            ) : children.length === 0 ? (
              <EmptyState
                spot="teachers"
                title={t('parent.noChildren')}
                description={t('parent.noChildrenHint')}
                action={{ label: t('parent.linkChild'), href: '/parent/children' }}
              />
            ) : (
              <div className="grid gap-4 md:grid-cols-2">
                {children.map((child, i) => (
                  <ChildCard key={child.studentId} child={child} index={i} />
                ))}
              </div>
            )}
          </DashboardSection>

          <DashboardSection title={locale === 'ar-EG' ? 'آخر الدرجات' : 'Recent grades'}>
            {dashboard.isPending ? (
              <RowSkeleton count={3} />
            ) : (data?.recentGrades ?? []).length === 0 ? (
              <EmptyState
                spot="exams"
                compact
                title={locale === 'ar-EG' ? 'مفيش درجات جديدة' : 'No new grades'}
              />
            ) : (
              <div className="space-y-2.5">
                {data!.recentGrades!.map((result) => (
                  <Card key={result.id} className="flex flex-wrap items-center gap-3 p-4">
                    <span
                      className={cn(
                        'grid size-10 shrink-0 place-items-center rounded-xl',
                        result.isPassed
                          ? 'bg-success/12 text-success-strong'
                          : 'bg-destructive/12 text-destructive-strong',
                      )}
                    >
                      <GraduationCap className="size-5" />
                    </span>
                    <div className="min-w-0 flex-1">
                      <p className="truncate font-display text-body-sm font-bold text-foreground">
                        {result.examTitle}
                      </p>
                      <p className="truncate text-caption text-muted-foreground">
                        {result.studentName}
                        {result.groupName ? ` · ${result.groupName}` : ''}
                      </p>
                    </div>
                    <span className="tnum font-display text-body-sm font-bold text-foreground">
                      {formatNumber(result.score ?? 0)}/{formatNumber(result.maximumScore ?? 0)}
                    </span>
                    <Badge variant={result.isPassed ? 'success' : 'destructive'} size="sm">
                      {result.isPassed ? t('exams.passed') : t('exams.failed')}
                    </Badge>
                  </Card>
                ))}
              </div>
            )}
          </DashboardSection>

          <DashboardSection title={locale === 'ar-EG' ? 'آخر حالات الغياب' : 'Recent absences'}>
            {dashboard.isPending ? (
              <RowSkeleton count={2} />
            ) : (data?.recentAbsences ?? []).length === 0 ? (
              <EmptyState
                spot="success"
                compact
                title={locale === 'ar-EG' ? 'مفيش غياب' : 'No absences'}
                description={
                  locale === 'ar-EG' ? 'الحضور منتظم — شغل حلو!' : 'Attendance is on track.'
                }
              />
            ) : (
              <div className="space-y-2.5">
                {data!.recentAbsences!.map((record) => (
                  <Card key={record.id} className="flex flex-wrap items-center gap-3 p-4">
                    <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-destructive/12 text-destructive-strong">
                      <CalendarDays className="size-5" />
                    </span>
                    <div className="min-w-0 flex-1">
                      <p className="truncate font-display text-body-sm font-bold text-foreground">
                        {record.studentName}
                      </p>
                      <p className="tnum truncate text-caption text-muted-foreground">
                        {record.groupName}
                        {record.sessionDate ? ` · ${formatDate(record.sessionDate, 'd MMM')}` : ''}
                      </p>
                    </div>
                    <StatusBadge status={record.status} fallbackLabel={record.statusName} />
                  </Card>
                ))}
              </div>
            )}
          </DashboardSection>
        </div>

        <div className="space-y-8">
          <DashboardSection title={locale === 'ar-EG' ? 'حصص النهارده' : "Today's classes"}>
            {dashboard.isPending ? (
              <RowSkeleton count={3} />
            ) : (data?.todaysSchedule ?? []).length === 0 ? (
              <EmptyState
                spot="calendar"
                compact
                title={locale === 'ar-EG' ? 'مفيش حصص النهارده' : 'No classes today'}
              />
            ) : (
              <div className="space-y-2.5">
                {data!.todaysSchedule!.map((slot) => (
                  <SlotCard key={slot.id} slot={slot} variant="compact" />
                ))}
              </div>
            )}
          </DashboardSection>

          <DashboardSection title={t('dashboard.upcomingClasses')}>
            {dashboard.isPending ? (
              <RowSkeleton count={3} />
            ) : (data?.upcomingClasses ?? []).length === 0 ? (
              <EmptyState spot="calendar" compact title={t('dashboard.noUpcomingClasses')} />
            ) : (
              <div className="space-y-2.5">
                {data!.upcomingClasses!.slice(0, 5).map((slot) => (
                  <SlotCard key={slot.id} slot={slot} variant="compact" />
                ))}
              </div>
            )}
          </DashboardSection>
        </div>
      </div>
    </>
  )
}

function ChildCard({ child, index }: { child: ChildOverviewDto; index: number }) {
  const { t, locale, formatNumber } = useI18n()

  const metrics = [
    { label: t('dashboard.attendance'), value: Math.round(child.attendanceRate ?? 0) },
    { label: t('dashboard.grades'), value: Math.round(child.averageGradePercentage ?? 0) },
  ]

  return (
    <motion.div
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ ...transitions.base, delay: Math.min(index * 0.06, 0.3) }}
    >
      <Card className="h-full p-5" interactive>
        <Link to={`/parent/children/${child.studentId}`} className="block">
          <div className="flex items-center gap-3.5">
            <Avatar
              traits={avatarTraitsFor(child.studentId ?? '')}
              seed={child.studentId ?? ''}
              name={child.displayName ?? ''}
              src={imageUrl(child.profileImageUrl)}
              size="lg"
            />
            <div className="min-w-0 flex-1">
              <p className="truncate font-display text-body font-bold text-foreground">
                {child.displayName}
              </p>
              {child.gradeName && (
                <p className="truncate text-caption text-muted-foreground">{child.gradeName}</p>
              )}
              <div className="mt-1.5 flex flex-wrap gap-1.5">
                <Badge variant="outline" size="sm" className="tnum">
                  {formatNumber(child.activeGroupsCount ?? 0)} {t('common.groups')}
                </Badge>
                <Badge variant="outline" size="sm" className="tnum">
                  {formatNumber(child.activeCoursesCount ?? 0)} {t('common.courses')}
                </Badge>
              </div>
            </div>
          </div>

          <dl className="mt-4 space-y-3">
            {metrics.map((metric) => (
              <div key={metric.label}>
                <div className="flex items-center justify-between text-caption">
                  <dt className="text-muted-foreground">{metric.label}</dt>
                  <dd className="tnum font-bold text-foreground">{metric.value}%</dd>
                </div>
                <Progress
                  value={metric.value}
                  className="mt-1.5 h-1.5"
                  indicatorClassName={
                    metric.value >= 75
                      ? 'bg-success'
                      : metric.value >= 50
                        ? 'bg-warning'
                        : 'bg-destructive'
                  }
                />
              </div>
            ))}
          </dl>

          <div className="mt-4 flex flex-wrap gap-2 border-t border-border pt-4">
            {(child.pendingHomeworkCount ?? 0) > 0 && (
              <Badge variant="warning" size="sm" className="tnum">
                <ClipboardCheck className="size-3.5" />
                {formatNumber(child.pendingHomeworkCount ?? 0)}{' '}
                {locale === 'ar-EG' ? 'واجب' : 'homework'}
              </Badge>
            )}
            {(child.upcomingExamsCount ?? 0) > 0 && (
              <Badge variant="primary" size="sm" className="tnum">
                <FileText className="size-3.5" />
                {formatNumber(child.upcomingExamsCount ?? 0)}{' '}
                {locale === 'ar-EG' ? 'امتحان' : 'exams'}
              </Badge>
            )}
          </div>
        </Link>
      </Card>
    </motion.div>
  )
}
