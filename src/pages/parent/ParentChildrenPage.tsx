import * as React from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { Clock, Trash2, UserPlus } from 'lucide-react'
import { parentApi } from '@/api/endpoints/parent'
import type { ChildDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { isApiError } from '@/api/errors'
import { avatarTraitsFor, imageUrl } from '@/lib/identity'
import { transitions } from '@/lib/motion'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { ConfirmDialog } from '@/components/modals/ConfirmDialog'
import { FormDialog } from '@/components/common/FormDialog'
import { RowSkeleton } from '@/components/ui/skeleton'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Field } from '@/components/ui/label'
import { Switch } from '@/components/ui/primitives'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { useApiErrorToast } from '@/hooks/use-api-toast'

export default function ParentChildrenPage() {
  const { t, locale, formatNumber, formatDate } = useI18n()
  usePageTitle(t('parent.children'))

  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()
  const [linkOpen, setLinkOpen] = React.useState(false)
  const [unlinking, setUnlinking] = React.useState<ChildDto | null>(null)

  const children = useQuery({
    queryKey: qk.parent.children,
    queryFn: parentApi.children,
  })

  const unlink = useMutation({
    mutationFn: (studentId: string) => parentApi.unlinkChild(studentId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['parent'] })
      setUnlinking(null)
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const items = children.data ?? []

  return (
    <>
      <DashboardHeader
        title={t('parent.children')}
        description={t('parent.linkChildHint')}
        actions={
          <Button size="sm" onClick={() => setLinkOpen(true)}>
            <UserPlus className="size-4" />
            {t('parent.linkChild')}
          </Button>
        }
      />

      {children.isPending ? (
        <RowSkeleton count={3} />
      ) : children.isError ? (
        <QueryError error={children.error} onRetry={() => children.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState
          spot="teachers"
          title={t('parent.noChildren')}
          description={t('parent.noChildrenHint')}
          action={{ label: t('parent.linkChild'), onClick: () => setLinkOpen(true) }}
        />
      ) : (
        <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
          {items.map((child, i) => (
            <motion.div
              key={child.studentId}
              initial={{ opacity: 0, y: 12 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ ...transitions.base, delay: Math.min(i * 0.06, 0.3) }}
            >
              <Card className="flex h-full flex-col p-5">
                <div className="flex items-start gap-3.5">
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
                    {child.email && (
                      <p className="truncate font-latin text-caption text-muted-foreground">
                        {child.email}
                      </p>
                    )}
                    <div className="mt-1.5 flex flex-wrap gap-1.5">
                      {child.isConfirmed ? (
                        <Badge variant="success" size="sm">
                          {locale === 'ar-EG' ? 'مؤكد' : 'Confirmed'}
                        </Badge>
                      ) : (
                        <Badge variant="warning" size="sm">
                          <Clock className="size-3" />
                          {locale === 'ar-EG' ? 'مستني موافقة' : 'Pending approval'}
                        </Badge>
                      )}
                      {child.isPrimaryContact && (
                        <Badge variant="primary" size="sm">
                          {locale === 'ar-EG' ? 'جهة اتصال أساسية' : 'Primary contact'}
                        </Badge>
                      )}
                    </div>
                  </div>
                </div>

                <dl className="mt-4 grid grid-cols-2 gap-3 rounded-lg border border-border bg-muted/30 p-3 text-center">
                  <div>
                    <dt className="text-[0.6875rem] text-muted-foreground">{t('common.groups')}</dt>
                    <dd className="tnum font-display text-body-sm font-bold text-foreground">
                      {formatNumber(child.activeGroupsCount ?? 0)}
                    </dd>
                  </div>
                  <div className="border-s border-border">
                    <dt className="text-[0.6875rem] text-muted-foreground">{t('common.courses')}</dt>
                    <dd className="tnum font-display text-body-sm font-bold text-foreground">
                      {formatNumber(child.activeCoursesCount ?? 0)}
                    </dd>
                  </div>
                </dl>

                {(child.schoolName || child.gradeName) && (
                  <p className="mt-3 text-caption text-muted-foreground">
                    {[child.gradeName, child.schoolName].filter(Boolean).join(' · ')}
                  </p>
                )}

                {child.linkedAt && (
                  <p className="tnum mt-1 text-[0.6875rem] text-muted-foreground/70">
                    {locale === 'ar-EG' ? 'اتربط في' : 'Linked'}{' '}
                    {formatDate(child.linkedAt, 'd MMM yyyy')}
                  </p>
                )}

                <div className="mt-auto flex items-center gap-2 border-t border-border pt-4">
                  <Button asChild size="sm" block className="flex-1" disabled={!child.isConfirmed}>
                    <Link to={`/parent/children/${child.studentId}`}>{t('parent.overview')}</Link>
                  </Button>
                  <Button
                    variant="outline"
                    size="icon"
                    aria-label={t('common.remove')}
                    className="text-muted-foreground hover:text-destructive-strong"
                    onClick={() => setUnlinking(child)}
                  >
                    <Trash2 className="size-4" />
                  </Button>
                </div>
              </Card>
            </motion.div>
          ))}
        </div>
      )}

      <LinkChildDialog open={linkOpen} onOpenChange={setLinkOpen} />

      <ConfirmDialog
        open={Boolean(unlinking)}
        onOpenChange={(open) => !open && setUnlinking(null)}
        title={locale === 'ar-EG' ? 'إلغاء الربط؟' : 'Unlink this child?'}
        description={
          locale === 'ar-EG'
            ? `مش هتقدر تتابع ${unlinking?.displayName ?? ''} بعد كده.`
            : `You will stop following ${unlinking?.displayName ?? ''}.`
        }
        confirmLabel={t('common.remove')}
        loading={unlink.isPending}
        onConfirm={() => unlinking?.studentId && unlink.mutate(unlinking.studentId)}
      />
    </>
  )
}

function LinkChildDialog({
  open,
  onOpenChange,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const { t, locale } = useI18n()
  const { toast } = useToast()
  const queryClient = useQueryClient()

  const [studentEmail, setStudentEmail] = React.useState('')
  const [verificationPhoneNumber, setVerificationPhoneNumber] = React.useState('')
  const [relationship, setRelationship] = React.useState('')
  const [isPrimaryContact, setIsPrimaryContact] = React.useState(true)
  const [formError, setFormError] = React.useState<string | null>(null)

  React.useEffect(() => {
    if (!open) return
    setFormError(null)
    setStudentEmail('')
    setVerificationPhoneNumber('')
    setRelationship('')
    setIsPrimaryContact(true)
  }, [open])

  const link = useMutation({
    mutationFn: () =>
      parentApi.linkChild({
        studentEmail: studentEmail.trim(),
        verificationPhoneNumber: verificationPhoneNumber.trim() || null,
        relationship: relationship.trim() || null,
        isPrimaryContact,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['parent'] })
      onOpenChange(false)
      toast({ title: t('parent.linked'), variant: 'success' })
    },
    onError: (error) => setFormError(isApiError(error) ? error.message : t('states.errorTitle')),
  })

  return (
    <FormDialog
      open={open}
      onOpenChange={onOpenChange}
      title={t('parent.linkChild')}
      description={t('parent.linkChildHint')}
      error={formError}
      submitting={link.isPending}
      submitLabel={t('parent.linkChild')}
      size="md"
      onSubmit={(e) => {
        e.preventDefault()
        if (studentEmail.trim()) link.mutate()
      }}
    >
      <Field label={locale === 'ar-EG' ? 'بريد الطالب' : "Student's email"} htmlFor="child-email" required>
        <Input
          id="child-email"
          type="email"
          dir="ltr"
          className="text-start"
          value={studentEmail}
          onChange={(e) => setStudentEmail(e.target.value)}
          placeholder="student@example.com"
        />
      </Field>

      <Field
        label={locale === 'ar-EG' ? 'رقم التأكيد' : 'Verification phone'}
        htmlFor="child-phone"
        hint={
          locale === 'ar-EG'
            ? 'رقم ولي الأمر المسجّل على حساب الطالب.'
            : "The parent phone recorded on the student's account."
        }
      >
        <Input
          id="child-phone"
          type="tel"
          dir="ltr"
          className="text-start"
          value={verificationPhoneNumber}
          onChange={(e) => setVerificationPhoneNumber(e.target.value)}
        />
      </Field>

      <Field label={t('parent.relationship')} htmlFor="child-relationship">
        <Input
          id="child-relationship"
          value={relationship}
          onChange={(e) => setRelationship(e.target.value)}
          placeholder={locale === 'ar-EG' ? 'الأب / الأم' : 'Father / Mother'}
        />
      </Field>

      <label className="flex items-center justify-between gap-4 rounded-xl border border-border bg-muted/20 p-4">
        <span className="font-display text-body-sm font-semibold text-foreground">
          {locale === 'ar-EG' ? 'جهة الاتصال الأساسية' : 'Primary contact'}
        </span>
        <Switch checked={isPrimaryContact} onCheckedChange={setIsPrimaryContact} />
      </label>
    </FormDialog>
  )
}
