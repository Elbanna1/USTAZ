import * as React from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { Award, BadgeCheck, Copy, ExternalLink } from 'lucide-react'
import { studentApi } from '@/api/endpoints/student'
import type { CertificateDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { cn, copyToClipboard } from '@/lib/utils'
import { transitions } from '@/lib/motion'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { Pagination } from '@/components/common/Pagination'
import { StatusBadge } from '@/components/common/StatusBadge'
import { CardGridSkeleton } from '@/components/ui/skeleton'
import { Card } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { GoldRule } from '@/components/illustrations/CoverArt'

const PAGE_SIZE = 12

export default function StudentCertificatesPage() {
  const { t, locale } = useI18n()
  usePageTitle(t('certificates.title'))

  const [page, setPage] = React.useState(1)
  const query = React.useMemo(() => ({ Page: page, PageSize: PAGE_SIZE }), [page])

  const certificates = useQuery({
    queryKey: qk.student.certificates(query),
    queryFn: () => studentApi.certificates(query),
    placeholderData: keepPreviousData,
  })

  const items = certificates.data?.items ?? []

  return (
    <>
      <DashboardHeader
        title={t('certificates.title')}
        description={
          locale === 'ar-EG'
            ? 'شهاداتك على الكورسات اللي خلّصتها — كل شهادة ليها كود تحقّق.'
            : 'Certificates for the courses you completed — each with its own verification code.'
        }
      />

      <div className={cn('transition-opacity', certificates.isFetching && 'opacity-60')}>
        {certificates.isPending ? (
          <CardGridSkeleton count={3} />
        ) : certificates.isError ? (
          <QueryError error={certificates.error} onRetry={() => certificates.refetch()} />
        ) : items.length === 0 ? (
          <EmptyState
            spot="certificate"
            title={locale === 'ar-EG' ? 'لسه مفيش شهادات' : 'No certificates yet'}
            description={
              locale === 'ar-EG'
                ? 'خلّص كورس بالكامل عشان تستلم شهادتك.'
                : 'Complete a course in full to earn your certificate.'
            }
            action={{ label: t('nav.courses'), href: '/courses' }}
          />
        ) : (
          <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
            {items.map((certificate, i) => (
              <CertificateCard key={certificate.id} certificate={certificate} index={i} />
            ))}
          </div>
        )}
      </div>

      {certificates.data && items.length > 0 && (
        <Pagination page={certificates.data} onChange={setPage} className="mt-8" />
      )}
    </>
  )
}

function CertificateCard({
  certificate,
  index,
}: {
  certificate: CertificateDto
  index: number
}) {
  const { t, locale, formatDate, formatNumber } = useI18n()
  const { toast } = useToast()
  const revoked = certificate.status === 'Revoked' || certificate.isValid === false

  return (
    <motion.div
      initial={{ opacity: 0, y: 14 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ ...transitions.base, delay: Math.min(index * 0.06, 0.3) }}
    >
      <Card
        className={cn(
          'relative h-full overflow-hidden',
          revoked ? 'opacity-70' : 'border-accent/25',
        )}
      >
        {/* Certificate chrome — a gold ribbon and rule, never a photo */}
        <div
          className={cn(
            'relative h-24 overflow-hidden',
            revoked
              ? 'bg-muted'
              : 'bg-gradient-to-bl from-primary via-primary-soft to-primary',
          )}
        >
          <div className="absolute inset-0 bg-dot-grid opacity-[0.08]" aria-hidden />
          <span
            className={cn(
              'absolute end-4 top-4 grid size-12 place-items-center rounded-full',
              revoked ? 'bg-muted-foreground/15 text-muted-foreground' : 'bg-accent/20 text-accent-strong',
            )}
          >
            <Award className="size-6" />
          </span>
          <div className="absolute inset-x-5 bottom-4">
            <GoldRule className="max-w-[6rem] opacity-70" />
          </div>
        </div>

        <div className="space-y-4 p-5">
          <div>
            <h3 className="line-clamp-2-rtl font-display text-body font-bold leading-snug text-foreground">
              {certificate.courseTitle}
            </h3>
            {certificate.teacherName && (
              <p className="mt-1 truncate text-caption text-muted-foreground">
                {certificate.teacherName}
                {certificate.workspaceName ? ` · ${certificate.workspaceName}` : ''}
              </p>
            )}
          </div>

          <dl className="grid grid-cols-2 gap-3 rounded-lg border border-border bg-muted/30 p-3">
            <div>
              <dt className="text-[0.6875rem] text-muted-foreground">
                {t('certificates.issuedOn')}
              </dt>
              <dd className="tnum mt-0.5 font-display text-caption font-bold text-foreground">
                {certificate.issuedAt ? formatDate(certificate.issuedAt, 'd MMM yyyy') : '—'}
              </dd>
            </div>
            <div>
              <dt className="text-[0.6875rem] text-muted-foreground">{t('courses.lessons')}</dt>
              <dd className="tnum mt-0.5 font-display text-caption font-bold text-foreground">
                {formatNumber(certificate.lessonsCompleted ?? 0)}
              </dd>
            </div>
          </dl>

          <div className="flex items-center gap-2">
            <StatusBadge status={certificate.status} fallbackLabel={certificate.statusName} />
            {!revoked && (
              <span className="inline-flex items-center gap-1 text-caption text-success-strong">
                <BadgeCheck className="size-3.5" />
                {t('certificates.valid')}
              </span>
            )}
          </div>

          {certificate.verificationCode && (
            <div className="flex items-center gap-2 rounded-lg border border-dashed border-border bg-surface px-3 py-2">
              <span className="min-w-0 flex-1 truncate font-latin text-caption font-semibold text-foreground">
                {certificate.verificationCode}
              </span>
              <button
                type="button"
                onClick={async () => {
                  const ok = await copyToClipboard(certificate.verificationCode!)
                  toast({
                    title: ok ? t('common.copied') : t('states.errorTitle'),
                    variant: ok ? 'success' : 'error',
                  })
                }}
                aria-label={t('common.copy')}
                className="shrink-0 rounded p-1 text-muted-foreground transition-colors hover:text-foreground"
              >
                <Copy className="size-4" />
              </button>
            </div>
          )}

          {certificate.verificationCode && (
            <Button asChild variant="outline" size="sm" block>
              <a
                href={`/verify/${certificate.verificationCode}`}
                target="_blank"
                rel="noopener noreferrer"
              >
                <ExternalLink className="size-4" />
                {locale === 'ar-EG' ? 'صفحة التحقّق' : 'Verification page'}
              </a>
            </Button>
          )}
        </div>
      </Card>
    </motion.div>
  )
}
