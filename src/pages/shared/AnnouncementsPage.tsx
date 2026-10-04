import * as React from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { Megaphone, Pin } from 'lucide-react'
import { parentApi } from '@/api/endpoints/parent'
import { studentApi } from '@/api/endpoints/student'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useDebounced } from '@/hooks/use-ui'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { transitions } from '@/lib/motion'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { SearchBar } from '@/components/common/controls'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { Pagination } from '@/components/common/Pagination'
import { RowSkeleton } from '@/components/ui/skeleton'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'

const PAGE_SIZE = 12

/** Shared by parents and students — the API differs only in the route. */
export default function AnnouncementsPage({ audience }: { audience: 'parent' | 'student' }) {
  const { t, locale, formatDateTime } = useI18n()
  usePageTitle(t('dashboard.announcements'))

  const [page, setPage] = React.useState(1)
  const [search, setSearch] = React.useState('')
  const debounced = useDebounced(search, 300)

  const query = React.useMemo(
    () => ({ Page: page, PageSize: PAGE_SIZE, Search: debounced.trim() || undefined }),
    [page, debounced],
  )

  const announcements = useQuery({
    queryKey:
      audience === 'parent' ? qk.parent.announcements(query) : qk.student.announcements(query),
    queryFn: () =>
      audience === 'parent' ? parentApi.announcements(query) : studentApi.announcements(query),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => setPage(1), [debounced])

  const items = announcements.data?.items ?? []

  return (
    <>
      <DashboardHeader
        title={t('dashboard.announcements')}
        description={
          locale === 'ar-EG'
            ? 'كل الإعلانات من مدرّسينك في مكان واحد.'
            : 'Every announcement from your teachers in one place.'
        }
      >
        <SearchBar
          value={search}
          onChange={setSearch}
          placeholder={t('common.search')}
          className="max-w-md"
          id="announcements-search"
        />
      </DashboardHeader>

      <div className={cn('transition-opacity', announcements.isFetching && 'opacity-60')}>
        {announcements.isPending ? (
          <RowSkeleton count={4} />
        ) : announcements.isError ? (
          <QueryError error={announcements.error} onRetry={() => announcements.refetch()} />
        ) : items.length === 0 ? (
          <EmptyState
            spot="inbox"
            title={locale === 'ar-EG' ? 'مفيش إعلانات' : 'No announcements'}
            description={
              locale === 'ar-EG'
                ? 'أول ما مدرّسك ينشر إعلان هتلاقيه هنا.'
                : 'Announcements from your teachers will appear here.'
            }
          />
        ) : (
          <div className="space-y-3">
            {items.map((item, i) => (
              <motion.div
                key={item.id}
                initial={{ opacity: 0, y: 8 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ ...transitions.fast, delay: Math.min(i * 0.04, 0.3) }}
              >
                <Card className={cn('p-5', item.isPinned && 'border-accent/35 bg-accent/5')}>
                  <div className="flex items-start gap-4">
                    <span
                      className={cn(
                        'grid size-10 shrink-0 place-items-center rounded-xl',
                        item.isPinned ? 'bg-accent/15 text-accent-strong' : 'bg-primary/10 text-primary',
                      )}
                    >
                      {item.isPinned ? <Pin className="size-5" /> : <Megaphone className="size-5" />}
                    </span>

                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <p className="font-display text-body font-bold text-foreground">
                          {item.title}
                        </p>
                        {item.groupName && (
                          <Badge variant="outline" size="sm">
                            {item.groupName}
                          </Badge>
                        )}
                        {item.tenantName && (
                          <Badge variant="primary" size="sm">
                            {item.tenantName}
                          </Badge>
                        )}
                      </div>

                      <p className="mt-2 whitespace-pre-wrap text-body-sm leading-relaxed text-muted-foreground">
                        {item.body}
                      </p>

                      {item.publishedAt && (
                        <p className="tnum mt-2 text-caption text-muted-foreground/70">
                          {formatDateTime(item.publishedAt)}
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

      {announcements.data && items.length > 0 && (
        <Pagination page={announcements.data} onChange={setPage} className="mt-8" />
      )}
    </>
  )
}

export function ParentAnnouncementsPage() {
  return <AnnouncementsPage audience="parent" />
}

export function StudentAnnouncementsPage() {
  return <AnnouncementsPage audience="student" />
}
