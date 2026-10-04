import { Link } from 'react-router-dom'
import { useQueries, useQuery } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import {
  AlertTriangle,
  ArrowRight,
  BookOpen,
  CalendarPlus,
  ClipboardCheck,
  Megaphone,
  MessageSquare,
  Star,
  TrendingUp,
  UserPlus,
  Users,
  Wallet,
} from 'lucide-react'
import { teacherAnalyticsApi, teacherInquiriesApi } from '@/api/endpoints/teacher'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useAuth } from '@/hooks/use-auth'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { avatarTraitsFor } from '@/lib/identity'
import { transitions } from '@/lib/motion'
import { DashboardHeader, DashboardSection } from '@/components/dashboard/DashboardHeader'
import { VerificationPanel } from '@/components/teacher/VerificationPanel'
import { StatCard } from '@/components/common/StatCard'
import { StatSkeleton, RowSkeleton } from '@/components/ui/skeleton'
import { QueryError } from '@/components/common/QueryBoundary'
import { EmptyState } from '@/components/common/states'
import { SlotCard } from '@/components/schedule/SlotCard'
import { RevenueAreaChart } from '@/components/dashboard/charts'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Progress } from '@/components/ui/primitives'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'

const RISK_TONES: Record<string, string> = {
  High: 'border-destructive/30 bg-destructive/6',
  Medium: 'border-warning/30 bg-warning/6',
  Low: 'border-border bg-card',
}

export default function TeacherDashboardPage() {
  const { t, locale, formatNumber, formatCurrency, formatTimeAgo } = useI18n()
  const { user } = useAuth()
  usePageTitle(t('dashboard.overview'))

  const dashboard = useQuery({
    queryKey: qk.teacher.dashboard,
    queryFn: teacherAnalyticsApi.dashboard,
  })

  const [revenue, atRisk, inquiries] = useQueries({
    queries: [
      { queryKey: qk.teacher.revenue(6), queryFn: () => teacherAnalyticsApi.revenue(6) },
      {
        queryKey: qk.teacher.atRisk({ Take: 5 }),
        queryFn: () => teacherAnalyticsApi.atRisk({ Take: 5, MinimumLevel: 'Medium' }),
      },
      {
        queryKey: qk.teacher.inquiries({ Status: 'New', PageSize: 5 }),
        queryFn: () => teacherInquiriesApi.list({ Status: 'New', PageSize: 5 }),
      },
    ],
  })

  const data = dashboard.data
  const revenueSeries = (revenue.data ?? []).map((point) => ({
    label: point.label ?? '',
    value: point.totalRevenue ?? 0,
    secondary: point.courseRevenue ?? 0,
  }))

  return (
    <>
      <DashboardHeader
        title={t('dashboard.welcome', { name: user?.firstName || user?.displayName || '' })}
        description={
          locale === 'ar-EG'
            ? 'نظرة سريعة على مجموعاتك وطلابك وإيراداتك.'
            : 'A quick read on your groups, students, and revenue.'
        }
        actions={
          <>
            <Button asChild variant="outline" size="sm">
              <Link to="/teacher/announcements">
                <Megaphone className="size-4" />
                {t('dashboard.announcements')}
              </Link>
            </Button>
            <Button asChild size="sm">
              <Link to="/teacher/groups?new=1">
                <CalendarPlus className="size-4" />
                {locale === 'ar-EG' ? 'مجموعة جديدة' : 'New group'}
              </Link>
            </Button>
          </>
        }
      />

      {/* Shows what is missing and lets the teacher act, rather than just warning them. */}
      <VerificationPanel className="mb-6" hideWhenVerified />

      {/* ------------------------------ KPI row ------------------------------ */}
      {dashboard.isPending ? (
        <StatSkeleton count={4} />
      ) : dashboard.isError ? (
        <QueryError error={dashboard.error} onRetry={() => dashboard.refetch()} />
      ) : (
        <RevealGroup className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <RevealItem>
            <StatCard
              label={t('dashboard.totalStudents')}
              value={data?.totalStudents ?? 0}
              icon={<Users />}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={t('dashboard.monthlyRevenue')}
              value={data?.monthlyRevenue ?? 0}
              icon={<Wallet />}
              description={`${t('common.total')}: ${formatCurrency(data?.totalRevenue ?? 0)}`}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={t('dashboard.activeGroups')}
              value={data?.activeGroupsCount ?? 0}
              icon={<Users />}
              description={
                (data?.draftGroupsCount ?? 0) > 0
                  ? `${formatNumber(data!.draftGroupsCount!)} ${t('common.draft')}`
                  : undefined
              }
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={t('dashboard.averageRating')}
              value={data?.averageRating ?? 0}
              decimals={1}
              icon={<Star />}
              description={`${formatNumber(data?.reviewCount ?? 0)} ${t('common.reviews')}`}
            />
          </RevealItem>
        </RevealGroup>
      )}

      <div className="mt-8 grid gap-8 xl:grid-cols-[1.6fr,1fr]">
        <div className="space-y-8">
          {/* --------------------------- Revenue --------------------------- */}
          <DashboardSection
            title={t('dashboard.revenue')}
            description={
              locale === 'ar-EG' ? 'آخر ٦ شهور' : 'Last 6 months'
            }
            action={
              <Button asChild variant="ghost" size="sm">
                <Link to="/teacher/analytics">
                  {t('dashboard.analytics')}
                  <ArrowRight className="size-4 flip-rtl" />
                </Link>
              </Button>
            }
          >
            <Card className="p-5">
              {revenue.isPending ? (
                <div className="h-[260px] animate-pulse rounded-lg bg-muted" />
              ) : revenue.isError ? (
                <QueryError error={revenue.error} onRetry={() => revenue.refetch()} compact />
              ) : revenueSeries.every((p) => p.value === 0) ? (
                <EmptyState
                  spot="payments"
                  compact
                  className="border-0 bg-transparent"
                  title={locale === 'ar-EG' ? 'لسه مفيش إيرادات' : 'No revenue yet'}
                  description={
                    locale === 'ar-EG'
                      ? 'أول ما طالب يشترك في مجموعة أو كورس هتشوف الأرقام هنا.'
                      : 'Numbers appear here as soon as a student enrolls.'
                  }
                />
              ) : (
                <RevenueAreaChart data={revenueSeries} />
              )}
            </Card>
          </DashboardSection>

          {/* ----------------------- At-risk students ---------------------- */}
          <DashboardSection
            title={t('dashboard.atRiskStudents')}
            action={
              <Button asChild variant="ghost" size="sm">
                <Link to="/teacher/students">
                  {t('common.viewAll')}
                  <ArrowRight className="size-4 flip-rtl" />
                </Link>
              </Button>
            }
          >
            {atRisk.isPending ? (
              <RowSkeleton count={3} />
            ) : atRisk.isError ? (
              <QueryError error={atRisk.error} onRetry={() => atRisk.refetch()} compact />
            ) : (atRisk.data ?? []).length === 0 ? (
              <EmptyState
                spot="success"
                compact
                title={locale === 'ar-EG' ? 'مفيش طلاب محتاجين متابعة' : 'No students need attention'}
                description={
                  locale === 'ar-EG'
                    ? 'كل طلابك منتظمين في الحضور والواجبات.'
                    : 'Everyone is keeping up with attendance and homework.'
                }
              />
            ) : (
              <div className="space-y-3">
                {(atRisk.data ?? []).map((student, i) => (
                  <motion.div
                    key={student.studentId}
                    initial={{ opacity: 0, x: 10 }}
                    animate={{ opacity: 1, x: 0 }}
                    transition={{ ...transitions.base, delay: i * 0.05 }}
                  >
                    <Card
                      className={cn('p-4', RISK_TONES[student.riskLevel ?? 'Low'])}
                    >
                      <div className="flex items-start gap-3">
                        <Avatar
                          traits={avatarTraitsFor(student.studentId ?? '')}
                          seed={student.studentId ?? ''}
                          name={student.studentName ?? ''}
                          size="sm"
                        />
                        <div className="min-w-0 flex-1">
                          <div className="flex flex-wrap items-center gap-2">
                            <p className="font-display text-body-sm font-bold text-foreground">
                              {student.studentName}
                            </p>
                            <Badge
                              variant={
                                student.riskLevel === 'High'
                                  ? 'destructive'
                                  : student.riskLevel === 'Medium'
                                    ? 'warning'
                                    : 'outline'
                              }
                              size="sm"
                            >
                              {student.riskLevelName ?? student.riskLevel}
                            </Badge>
                          </div>

                          <ul className="mt-2 space-y-1">
                            {(student.reasons ?? []).slice(0, 2).map((reason) => (
                              <li
                                key={reason.code}
                                className="flex items-start gap-1.5 text-caption text-muted-foreground"
                              >
                                <AlertTriangle className="mt-0.5 size-3 shrink-0" />
                                {locale === 'ar-EG' ? reason.messageAr : reason.messageEn}
                              </li>
                            ))}
                          </ul>

                          <div className="mt-3 grid grid-cols-3 gap-3">
                            <MiniMetric
                              label={t('dashboard.attendance')}
                              value={Math.round(student.attendanceRate ?? 0)}
                            />
                            <MiniMetric
                              label={t('dashboard.homework')}
                              value={Math.round(student.homeworkCompletionRate ?? 0)}
                            />
                            <MiniMetric
                              label={t('dashboard.grades')}
                              value={Math.round(student.averageExamPercentage ?? 0)}
                            />
                          </div>
                        </div>
                      </div>
                    </Card>
                  </motion.div>
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
                action={{ label: t('nav.schedule'), href: '/teacher/schedule' }}
              />
            ) : (
              <div className="space-y-2.5">
                {data!.upcomingClasses!.slice(0, 5).map((slot) => (
                  <SlotCard key={slot.id} slot={slot} variant="compact" showTeacher={false} />
                ))}
              </div>
            )}
          </DashboardSection>

          <DashboardSection title={locale === 'ar-EG' ? 'أفضل المجموعات' : 'Top groups'}>
            {dashboard.isPending ? (
              <RowSkeleton count={3} />
            ) : (data?.topGroups ?? []).length === 0 ? (
              <EmptyState
                spot="groups"
                compact
                title={locale === 'ar-EG' ? 'لسه مفيش مجموعات' : 'No groups yet'}
                action={{ label: t('common.create'), href: '/teacher/groups' }}
              />
            ) : (
              <div className="space-y-3">
                {data!.topGroups!.slice(0, 4).map((group) => (
                  <Card key={group.groupId} className="p-4">
                    <div className="flex items-start justify-between gap-3">
                      <Link
                        to={`/teacher/groups/${group.groupId}`}
                        className="min-w-0 flex-1 truncate font-display text-body-sm font-bold text-foreground hover:text-primary"
                      >
                        {group.name}
                      </Link>
                      <span className="tnum shrink-0 text-caption font-semibold text-foreground">
                        {formatCurrency(group.monthlyRevenue ?? 0)}
                      </span>
                    </div>
                    <div className="mt-2.5 flex items-center gap-2.5">
                      <Progress value={group.fillRatePercentage ?? 0} className="h-1.5 flex-1" />
                      <span className="tnum text-[0.6875rem] text-muted-foreground">
                        {formatNumber(group.enrolledStudentsCount ?? 0)}/
                        {formatNumber(group.capacity ?? 0)}
                      </span>
                    </div>
                  </Card>
                ))}
              </div>
            )}
          </DashboardSection>

          <DashboardSection title={t('dashboard.newInquiries')}>
            {inquiries.isPending ? (
              <RowSkeleton count={2} />
            ) : (inquiries.data?.items ?? []).length === 0 ? (
              <EmptyState
                spot="inbox"
                compact
                title={locale === 'ar-EG' ? 'مفيش طلبات جديدة' : 'No new inquiries'}
              />
            ) : (
              <div className="space-y-2.5">
                {inquiries.data!.items.map((inquiry) => (
                  <Card key={inquiry.id} className="p-4">
                    <div className="flex items-start gap-3">
                      <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary">
                        <UserPlus className="size-4" />
                      </span>
                      <div className="min-w-0 flex-1">
                        <p className="truncate font-display text-body-sm font-semibold text-foreground">
                          {inquiry.name}
                        </p>
                        <p className="line-clamp-2-rtl text-caption text-muted-foreground">
                          {inquiry.message}
                        </p>
                        {inquiry.createdAt && (
                          <p className="mt-1 text-[0.6875rem] text-muted-foreground/70">
                            {formatTimeAgo(inquiry.createdAt)}
                          </p>
                        )}
                      </div>
                    </div>
                  </Card>
                ))}
              </div>
            )}
          </DashboardSection>

          <DashboardSection title={t('dashboard.quickActions')}>
            <div className="grid grid-cols-2 gap-3">
              {[
                { href: '/teacher/attendance', icon: ClipboardCheck, label: t('dashboard.attendance') },
                { href: '/teacher/homework', icon: BookOpen, label: t('dashboard.homework') },
                { href: '/teacher/exams', icon: TrendingUp, label: t('dashboard.exams') },
                { href: '/teacher/messages', icon: MessageSquare, label: t('nav.messages') },
              ].map((action) => (
                <Link
                  key={action.href}
                  to={action.href}
                  className="flex flex-col items-center gap-2 rounded-xl border border-border bg-card p-4 text-center transition-all duration-200 hover:-translate-y-0.5 hover:border-primary/30 hover:shadow-soft"
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

function MiniMetric({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-lg bg-background/60 p-2 text-center">
      <p className="text-[0.625rem] text-muted-foreground">{label}</p>
      <p
        className={cn(
          'tnum font-display text-body-sm font-bold',
          value >= 75 ? 'text-success-strong' : value >= 50 ? 'text-warning-strong' : 'text-destructive-strong',
        )}
      >
        {value}%
      </p>
    </div>
  )
}
