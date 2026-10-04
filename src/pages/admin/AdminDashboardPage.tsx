import * as React from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import {
  BadgeCheck,
  BookOpen,
  Building2,
  ClipboardCheck,
  GraduationCap,
  ShieldAlert,
  Star,
  Users,
  Wallet,
} from 'lucide-react'
import { adminApi } from '@/api/endpoints/admin'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useI18n } from '@/i18n'
import { avatarTraitsFor, imageUrl } from '@/lib/identity'
import { DashboardHeader, DashboardSection } from '@/components/dashboard/DashboardHeader'
import { StatCard } from '@/components/common/StatCard'
import { StatSkeleton, RowSkeleton } from '@/components/ui/skeleton'
import { QueryError } from '@/components/common/QueryBoundary'
import { EmptyState } from '@/components/common/states'
import { RevenueAreaChart } from '@/components/dashboard/charts'
import { FilterChip } from '@/components/common/controls'
import { Card } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { Rating } from '@/components/common/Rating'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'

const RANGES = [3, 6, 12] as const

export default function AdminDashboardPage() {
  const { t, locale, formatNumber, formatCurrency } = useI18n()
  usePageTitle(t('admin.platformOverview'))

  const [months, setMonths] = React.useState<number>(6)

  const dashboard = useQuery({
    queryKey: qk.admin.dashboard(months),
    queryFn: () => adminApi.dashboard(months),
  })

  const data = dashboard.data
  const revenueSeries = (data?.revenueTrend ?? []).map((p) => ({
    label: p.label ?? '',
    value: p.totalRevenue ?? 0,
    secondary: p.courseRevenue ?? 0,
  }))
  const hasRevenue = revenueSeries.some((p) => p.value > 0)

  return (
    <>
      <DashboardHeader
        title={t('admin.platformOverview')}
        description={
          locale === 'ar-EG'
            ? 'حالة المنصة كلها: المستخدمين، المحتوى، والإيرادات.'
            : 'The whole platform at a glance: users, content, and revenue.'
        }
        actions={
          <div className="flex gap-2">
            {RANGES.map((value) => (
              <FilterChip
                key={value}
                label={
                  locale === 'ar-EG' ? `${formatNumber(value)} شهور` : `${formatNumber(value)} months`
                }
                active={months === value}
                onClick={() => setMonths(value)}
              />
            ))}
          </div>
        }
      />

      {dashboard.isPending ? (
        <StatSkeleton count={4} />
      ) : dashboard.isError ? (
        <QueryError error={dashboard.error} onRetry={() => dashboard.refetch()} />
      ) : (
        <>
          <RevealGroup className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <RevealItem>
              <StatCard
                label={t('admin.users')}
                value={data?.totalUsers ?? 0}
                icon={<Users />}
                description={`${formatNumber(data?.totalStudents ?? 0)} ${t('common.students')}`}
              />
            </RevealItem>
            <RevealItem>
              <StatCard
                label={t('common.teachers')}
                value={data?.totalTeachers ?? 0}
                icon={<GraduationCap />}
                description={`${formatNumber(data?.verifiedTeachers ?? 0)} ${locale === 'ar-EG' ? 'موثّق' : 'verified'}`}
              />
            </RevealItem>
            <RevealItem>
              <StatCard
                label={t('dashboard.revenue')}
                value={data?.platformRevenue ?? 0}
                icon={<Wallet />}
                description={formatCurrency(data?.platformRevenue ?? 0)}
              />
            </RevealItem>
            <RevealItem>
              <StatCard
                label={locale === 'ar-EG' ? 'طلبات توثيق' : 'Pending verifications'}
                value={data?.pendingVerifications ?? 0}
                icon={<ShieldAlert />}
              />
            </RevealItem>
          </RevealGroup>

          <RevealGroup className="mt-4 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <RevealItem>
              <StatCard
                label={t('common.groups')}
                value={data?.totalGroups ?? 0}
                icon={<Users />}
                description={`${formatNumber(data?.publishedGroups ?? 0)} ${t('common.published')}`}
              />
            </RevealItem>
            <RevealItem>
              <StatCard
                label={t('common.courses')}
                value={data?.totalCourses ?? 0}
                icon={<BookOpen />}
                description={`${formatNumber(data?.publishedCourses ?? 0)} ${t('common.published')}`}
              />
            </RevealItem>
            <RevealItem>
              <StatCard
                label={locale === 'ar-EG' ? 'الاشتراكات' : 'Enrollments'}
                value={(data?.totalGroupEnrollments ?? 0) + (data?.totalCourseEnrollments ?? 0)}
                icon={<ClipboardCheck />}
              />
            </RevealItem>
            <RevealItem>
              <StatCard
                label={t('common.reviews')}
                value={data?.totalReviews ?? 0}
                icon={<Star />}
                description={
                  (data?.pendingReviews ?? 0) > 0
                    ? `${formatNumber(data!.pendingReviews!)} ${locale === 'ar-EG' ? 'تحت المراجعة' : 'pending'}`
                    : undefined
                }
              />
            </RevealItem>
          </RevealGroup>
        </>
      )}

      <div className="mt-8 grid gap-8 xl:grid-cols-[1.6fr,1fr]">
        <DashboardSection title={t('dashboard.revenue')}>
          <Card className="p-5">
            {dashboard.isPending ? (
              <div className="h-[260px] animate-pulse rounded-lg bg-muted" />
            ) : !hasRevenue ? (
              <EmptyState
                spot="payments"
                compact
                className="border-0 bg-transparent"
                title={locale === 'ar-EG' ? 'لسه مفيش إيرادات' : 'No revenue yet'}
              />
            ) : (
              <RevenueAreaChart data={revenueSeries} />
            )}
          </Card>
        </DashboardSection>

        <DashboardSection
          title={locale === 'ar-EG' ? 'آخر المدرّسين' : 'Newest teachers'}
          action={
            <Button asChild variant="ghost" size="sm">
              <Link to="/admin/teachers">{t('common.viewAll')}</Link>
            </Button>
          }
        >
          {dashboard.isPending ? (
            <RowSkeleton count={4} />
          ) : (data?.recentTeachers ?? []).length === 0 ? (
            <EmptyState
              spot="teachers"
              compact
              title={locale === 'ar-EG' ? 'مفيش مدرّسين جدد' : 'No new teachers'}
            />
          ) : (
            <div className="space-y-2.5">
              {data!.recentTeachers!.map((teacher) => (
                <Card key={teacher.id} className="flex items-center gap-3 p-4">
                  <Avatar
                    traits={avatarTraitsFor(teacher.id ?? '')}
                    seed={teacher.id ?? ''}
                    name={teacher.displayName ?? ''}
                    src={imageUrl(teacher.profileImageUrl)}
                    size="sm"
                  />
                  <div className="min-w-0 flex-1">
                    <Link
                      to={`/admin/teachers?search=${encodeURIComponent(teacher.displayName ?? '')}`}
                      className="flex items-center gap-1.5 truncate font-display text-body-sm font-bold text-foreground hover:text-primary"
                    >
                      {teacher.displayName}
                      {teacher.isVerified && (
                        <BadgeCheck className="size-3.5 shrink-0 text-primary" />
                      )}
                    </Link>
                    <p className="truncate text-caption text-muted-foreground">
                      {teacher.headline}
                    </p>
                  </div>
                  <Rating value={teacher.rating ?? 0} size="sm" />
                </Card>
              ))}
            </div>
          )}
        </DashboardSection>
      </div>

      <DashboardSection title={t('dashboard.quickActions')} className="mt-10">
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          {[
            { href: '/admin/tenants', icon: Building2, label: t('admin.tenants') },
            { href: '/admin/teachers', icon: GraduationCap, label: t('common.teachers') },
            { href: '/admin/reviews', icon: Star, label: t('admin.moderation') },
            { href: '/admin/audit-logs', icon: ShieldAlert, label: t('admin.auditLogs') },
          ].map((action) => (
            <Link
              key={action.href}
              to={action.href}
              className="flex items-center gap-3 rounded-xl border border-border bg-card p-4 transition-all duration-200 hover:-translate-y-0.5 hover:border-primary/30 hover:shadow-soft"
            >
              <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary">
                <action.icon className="size-5" />
              </span>
              <span className="font-display text-body-sm font-semibold text-foreground">
                {action.label}
              </span>
            </Link>
          ))}
        </div>
      </DashboardSection>
    </>
  )
}
