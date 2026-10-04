import * as React from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { teacherActivityApi, type ActivityQuery } from '@/api/endpoints/teacher'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useI18n } from '@/i18n'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { ActivityTimeline } from '@/components/activity/ActivityTimeline'
import { QueryError } from '@/components/common/QueryBoundary'
import { EmptyState } from '@/components/common/states'
import { FilterChip } from '@/components/common/controls'
import { Pagination } from '@/components/common/Pagination'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'

/*
 * Filters map to `ActivityTypes` keys on the server. They are grouped by what a
 * teacher actually wants to look at — "what did my students do" versus "what did
 * I publish" — rather than mirroring the raw key namespace.
 */
const FILTERS: { key: string; ar: string; en: string; types?: string[] }[] = [
  { key: 'all', ar: 'الكل', en: 'All' },
  {
    key: 'work',
    ar: 'واجبات وامتحانات',
    en: 'Homework & exams',
    types: [
      'homework.submitted',
      'homework.graded',
      'homework.returned',
      'exam.completed',
      'exam.graded',
    ],
  },
  {
    key: 'learning',
    ar: 'تقدّم الدروس',
    en: 'Lesson progress',
    types: ['lesson.completed', 'course.completed', 'course.joined'],
  },
  {
    key: 'enrolment',
    ar: 'انضمام وحضور',
    en: 'Joining & attendance',
    types: ['group.joined', 'group.left', 'student.joined_workspace', 'attendance.recorded'],
  },
  {
    key: 'publishing',
    ar: 'اللي نشرته',
    en: 'What I published',
    types: [
      'homework.published',
      'exam.published',
      'lesson.published',
      'course.published',
      'group.published',
    ],
  },
]

export default function TeacherActivityPage() {
  const { locale } = useI18n()
  const isAr = locale === 'ar-EG'

  usePageTitle(isAr ? 'النشاط' : 'Activity')

  const [filter, setFilter] = React.useState('all')
  const [page, setPage] = React.useState(1)

  React.useEffect(() => setPage(1), [filter])

  const active = FILTERS.find((f) => f.key === filter) ?? FILTERS[0]

  const query = React.useMemo<ActivityQuery>(
    () => ({ Page: page, PageSize: 30, Types: active.types }),
    [page, active.types],
  )

  const activity = useQuery({
    queryKey: qk.teacher.activity(query),
    queryFn: () => teacherActivityApi.list(query),
    placeholderData: keepPreviousData,
  })

  const items = activity.data?.items ?? []

  return (
    <>
      <DashboardHeader
        title={isAr ? 'النشاط' : 'Activity'}
        description={
          isAr
            ? 'كل اللي حصل في مساحتك — تسليمات، درجات، حضور، واشتراكات.'
            : 'Everything happening across your workspace — submissions, grades, attendance, and sign-ups.'
        }
      />

      <div className="no-scrollbar mb-6 flex gap-2 overflow-x-auto pb-1">
        {FILTERS.map((f) => (
          <FilterChip
            key={f.key}
            label={isAr ? f.ar : f.en}
            active={filter === f.key}
            onClick={() => setFilter(f.key)}
          />
        ))}
      </div>

      {activity.isPending ? (
        <Card className="space-y-5 p-5">
          {[0, 1, 2, 3, 4, 5].map((i) => (
            <div key={i} className="flex gap-3.5">
              <Skeleton className="size-9 shrink-0 rounded-full" />
              <div className="flex-1 space-y-2 pt-1">
                <Skeleton className="h-4 w-3/5" />
                <Skeleton className="h-3 w-24" />
              </div>
            </div>
          ))}
        </Card>
      ) : activity.isError ? (
        <QueryError error={activity.error} onRetry={() => activity.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState
          spot="search"
          title={
            filter === 'all'
              ? isAr
                ? 'لسه مفيش نشاط'
                : 'No activity yet'
              : isAr
                ? 'مفيش نشاط من النوع ده'
                : 'Nothing of this kind yet'
          }
          description={
            filter === 'all'
              ? isAr
                ? 'أول ما طلابك يبدأوا يسلّموا ويحضروا هتلاقي كل حاجة هنا.'
                : 'As soon as your students submit work and attend sessions, it shows up here.'
              : isAr
                ? 'جرّب فلتر تاني أو شوف الكل.'
                : 'Try another filter, or view everything.'
          }
          action={
            filter === 'all'
              ? undefined
              : { label: isAr ? 'شوف الكل' : 'View all', onClick: () => setFilter('all') }
          }
        />
      ) : (
        <>
          <Card className="p-5">
            <ActivityTimeline events={items} />
          </Card>
          {activity.data && (
            <Pagination page={activity.data} onChange={setPage} className="mt-6" />
          )}
        </>
      )}
    </>
  )
}
