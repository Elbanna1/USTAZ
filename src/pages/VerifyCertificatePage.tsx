import * as React from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { Award, BadgeCheck, Search, ShieldX } from 'lucide-react'
import { certificatesApi } from '@/api/endpoints/platform'
import { isApiError } from '@/api/errors'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { transitions } from '@/lib/motion'
import { PageHeader } from '@/components/common/PageHeader'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Field } from '@/components/ui/label'
import { GoldRule } from '@/components/illustrations/CoverArt'
import { QueryError } from '@/components/common/QueryBoundary'

export default function VerifyCertificatePage() {
  const { code: codeParam } = useParams()
  const navigate = useNavigate()
  const { t, locale, formatDate } = useI18n()

  usePageTitle(t('certificates.verifyTitle'))

  const [code, setCode] = React.useState(codeParam ?? '')

  React.useEffect(() => {
    setCode(codeParam ?? '')
  }, [codeParam])

  const verification = useQuery({
    queryKey: qk.certificates.verify(codeParam ?? ''),
    queryFn: () => certificatesApi.verify(codeParam!),
    enabled: Boolean(codeParam),
    retry: false,
  })

  const notFound = verification.isError && isApiError(verification.error) && verification.error.kind === 'notFound'
  const data = verification.data
  const valid = Boolean(data?.isValid)

  return (
    <>
      <PageHeader
        eyebrow={t('certificates.title')}
        title={t('certificates.verifyTitle')}
        description={t('certificates.verifySubtitle')}
        crumbs={[
          { label: locale === 'ar-EG' ? 'الرئيسية' : 'Home', href: '/' },
          { label: t('certificates.verify') },
        ]}
      />

      <section className="section-y !py-12">
        <div className="container-page">
          <div className="mx-auto max-w-2xl">
            <Card className="p-6">
              <form
                onSubmit={(e) => {
                  e.preventDefault()
                  const trimmed = code.trim()
                  if (trimmed) navigate(`/verify/${encodeURIComponent(trimmed)}`)
                }}
                className="flex flex-col gap-3 sm:flex-row sm:items-end"
              >
                <Field label={t('certificates.code')} htmlFor="verify-code" className="flex-1">
                  <Input
                    id="verify-code"
                    value={code}
                    onChange={(e) => setCode(e.target.value)}
                    dir="ltr"
                    className="text-start font-latin"
                    placeholder="USTZ-XXXX-XXXX"
                  />
                </Field>
                <Button type="submit" size="lg" loading={verification.isFetching}>
                  <Search className="size-4" />
                  {t('certificates.verify')}
                </Button>
              </form>
            </Card>

            {codeParam && (
              <div className="mt-8">
                {verification.isPending ? (
                  <Card className="h-64 animate-pulse" />
                ) : notFound || (verification.isError && !data) ? (
                  <motion.div
                    initial={{ opacity: 0, scale: 0.98 }}
                    animate={{ opacity: 1, scale: 1 }}
                    transition={transitions.spring}
                  >
                    <Card className="border-destructive/30 bg-destructive/5 p-8 text-center">
                      <span className="mx-auto grid size-16 place-items-center rounded-2xl bg-destructive/15 text-destructive-strong">
                        <ShieldX className="size-8" />
                      </span>
                      <h2 className="mt-5 font-display text-heading-md font-bold text-foreground">
                        {t('certificates.invalid')}
                      </h2>
                      <p className="mt-2 break-all font-latin text-body-sm text-muted-foreground">
                        {codeParam}
                      </p>
                    </Card>
                  </motion.div>
                ) : verification.isError ? (
                  <QueryError error={verification.error} onRetry={() => verification.refetch()} />
                ) : (
                  <motion.div
                    initial={{ opacity: 0, y: 14 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={transitions.base}
                  >
                    <Card
                      className={cn(
                        'overflow-hidden',
                        valid ? 'border-success/30' : 'border-destructive/30',
                      )}
                    >
                      <div
                        className={cn(
                          'relative p-8 text-center',
                          valid
                            ? 'bg-gradient-to-bl from-primary via-primary-soft to-primary text-primary-foreground'
                            : 'bg-muted',
                        )}
                      >
                        <div className="absolute inset-0 bg-dot-grid opacity-[0.08]" aria-hidden />
                        <span
                          className={cn(
                            'relative mx-auto grid size-16 place-items-center rounded-2xl',
                            valid
                              ? 'bg-accent/20 text-accent-strong'
                              : 'bg-destructive/15 text-destructive-strong',
                          )}
                        >
                          {valid ? <Award className="size-8" /> : <ShieldX className="size-8" />}
                        </span>

                        <h2
                          className={cn(
                            'relative mt-5 font-display text-heading-md font-extrabold',
                            valid ? 'text-primary-foreground' : 'text-foreground',
                          )}
                        >
                          {valid ? t('certificates.valid') : t('certificates.revoked')}
                        </h2>

                        <GoldRule className="relative mx-auto mt-5 max-w-[10rem]" />
                      </div>

                      <dl className="divide-y divide-border">
                        <Row
                          label={t('common.student')}
                          value={data?.studentName ?? '—'}
                        />
                        <Row label={t('common.course')} value={data?.courseTitle ?? '—'} />
                        <Row label={t('common.teacher')} value={data?.teacherName ?? '—'} />
                        {data?.workspaceName && (
                          <Row
                            label={locale === 'ar-EG' ? 'المنصة' : 'Workspace'}
                            value={data.workspaceName}
                          />
                        )}
                        <Row
                          label={t('certificates.issuedOn')}
                          value={data?.issuedAt ? formatDate(data.issuedAt, 'd MMMM yyyy') : '—'}
                        />
                        {data?.completedAt && (
                          <Row
                            label={locale === 'ar-EG' ? 'اكتمل في' : 'Completed on'}
                            value={formatDate(data.completedAt, 'd MMMM yyyy')}
                          />
                        )}
                        <Row
                          label={locale === 'ar-EG' ? 'رقم الشهادة' : 'Certificate number'}
                          value={data?.certificateNumber ?? '—'}
                          latin
                        />
                        <div className="flex items-center justify-between gap-4 p-5">
                          <dt className="text-body-sm text-muted-foreground">
                            {t('common.status')}
                          </dt>
                          <dd>
                            <Badge variant={valid ? 'success' : 'destructive'} size="md">
                              {valid && <BadgeCheck className="size-3.5" />}
                              {data?.status ?? (valid ? t('common.active') : t('certificates.revoked'))}
                            </Badge>
                          </dd>
                        </div>
                      </dl>
                    </Card>
                  </motion.div>
                )}
              </div>
            )}
          </div>
        </div>
      </section>
    </>
  )
}

function Row({ label, value, latin }: { label: string; value: string; latin?: boolean }) {
  return (
    <div className="flex items-center justify-between gap-4 p-5">
      <dt className="shrink-0 text-body-sm text-muted-foreground">{label}</dt>
      <dd
        className={cn(
          'min-w-0 truncate text-end font-display text-body-sm font-bold text-foreground',
          latin && 'font-latin',
        )}
      >
        {value}
      </dd>
    </div>
  )
}
