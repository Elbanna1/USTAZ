import * as React from 'react'
import { useSearchParams } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { LayoutGrid, List, Star } from 'lucide-react'
import { teachersApi, type TeacherQuery } from '@/api/endpoints/catalog'
import { qk } from '@/lib/query-client'
import { cn } from '@/lib/utils'
import { usePageTitle } from '@/hooks/use-page-title'
import { useDebounced } from '@/hooks/use-ui'
import { STAGE_LABELS, useGrades, useSubjects } from '@/hooks/use-taxonomy'
import { useI18n } from '@/i18n'
import { PageHeader } from '@/components/common/PageHeader'
import { ActiveFilterChip, SearchBar } from '@/components/common/controls'
import { FilterCheckbox, FilterGroup, FilterPanel } from '@/components/common/FilterPanel'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { Pagination } from '@/components/common/Pagination'
import { CardGridSkeleton } from '@/components/ui/skeleton'
import { TeacherCard, TeacherRow } from '@/components/teachers/TeacherCard'
import { SubjectIcon } from '@/components/illustrations/SubjectIcon'
import { Button } from '@/components/ui/button'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'
import { subjectKeyFor } from '@/lib/identity'

const PAGE_SIZE = 12

const SORT_OPTIONS = [
  { value: 'featured', ar: 'المميّزين أولًا', en: 'Featured first' },
  { value: 'rating', ar: 'الأعلى تقييمًا', en: 'Highest rated' },
  { value: 'experience', ar: 'الأكتر خبرة', en: 'Most experienced' },
  { value: 'students', ar: 'الأكتر طلابًا', en: 'Most students' },
  { value: 'newest', ar: 'الأحدث انضمامًا', en: 'Newest' },
] as const

const MODES = [
  { value: 'Online', ar: 'أونلاين', en: 'Online' },
  { value: 'Offline', ar: 'حضوري', en: 'In person' },
  { value: 'Hybrid', ar: 'أونلاين وحضوري', en: 'Hybrid' },
] as const

const RATING_STEPS = [4.5, 4, 3.5]

interface Filters {
  query: string
  subjectId?: string
  gradeId?: string
  city?: string
  mode?: 'Online' | 'Offline' | 'Hybrid'
  minRating: number
  sort: string
}

const EMPTY_FILTERS: Filters = { query: '', minRating: 0, sort: 'featured' }

export default function TeachersPage() {
  const { t, locale, formatNumber } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'

  usePageTitle(
    locale === 'ar-EG' ? 'المدرّسين' : 'Teachers',
    locale === 'ar-EG'
      ? 'اكتشف مدرّسين مصريين حسب المادة والصف والمحافظة وطريقة الشرح.'
      : 'Discover Egyptian teachers by subject, grade, city, and teaching style.',
  )

  const [searchParams, setSearchParams] = useSearchParams()
  const [view, setView] = React.useState<'grid' | 'list'>('grid')
  const [page, setPage] = React.useState(1)

  const subjects = useSubjects()
  const grades = useGrades()

  // `?subject=` lets nav and footer links deep-link into a filtered directory.
  const subjectSlug = searchParams.get('subject')
  const [filters, setFilters] = React.useState<Filters>(() => {
    // Global search hands its term over in `?q=`, so the full directory opens
    // already filtered rather than showing everything for a beat first.
    const seed = searchParams.get('q')?.trim()
    return seed ? { ...EMPTY_FILTERS, query: seed } : EMPTY_FILTERS
  })

  /* Arriving at `?q=` from a route we are already on changes params, not state. */
  const queryParam = searchParams.get('q')
  React.useEffect(() => {
    const seed = queryParam?.trim()
    if (!seed) return
    setFilters((f) => (f.query === seed ? f : { ...f, query: seed }))
  }, [queryParam])

  React.useEffect(() => {
    if (!subjectSlug || !subjects.subjects.length) return
    const match = subjects.subjects.find((s) => s.slug === subjectSlug || s.id === subjectSlug)
    if (match?.id) setFilters((f) => (f.subjectId === match.id ? f : { ...f, subjectId: match.id }))
  }, [subjectSlug, subjects.subjects])

  const debouncedQuery = useDebounced(filters.query, 300)

  const apiQuery: TeacherQuery = React.useMemo(
    () => ({
      Page: page,
      PageSize: PAGE_SIZE,
      Search: debouncedQuery.trim() || undefined,
      SubjectId: filters.subjectId,
      GradeId: filters.gradeId,
      City: filters.city || undefined,
      TeachingMode: filters.mode,
      MinRating: filters.minRating || undefined,
      Sort: filters.sort,
    }),
    [page, debouncedQuery, filters],
  )

  const results = useQuery({
    queryKey: qk.teachers.list(apiQuery),
    queryFn: () => teachersApi.list(apiQuery),
    placeholderData: keepPreviousData,
  })

  /* Any filter change starts the list over at page one. */
  React.useEffect(() => {
    setPage(1)
  }, [debouncedQuery, filters.subjectId, filters.gradeId, filters.city, filters.mode, filters.minRating, filters.sort])

  function patch(next: Partial<Filters>) {
    setFilters((f) => ({ ...f, ...next }))
  }

  function clearAll() {
    setFilters(EMPTY_FILTERS)
    setSearchParams({}, { replace: true })
  }

  const activeCount =
    (filters.subjectId ? 1 : 0) +
    (filters.gradeId ? 1 : 0) +
    (filters.city ? 1 : 0) +
    (filters.mode ? 1 : 0) +
    (filters.minRating ? 1 : 0) +
    (filters.query.trim() ? 1 : 0)

  const data = results.data
  const items = data?.items ?? []
  const totalCount = data?.totalCount ?? 0
  const activeSubject = subjects.byId(filters.subjectId)
  const activeGrade = grades.byId(filters.gradeId)

  return (
    <>
      <PageHeader
        eyebrow={locale === 'ar-EG' ? 'دليل المدرّسين' : 'Teacher directory'}
        title={
          locale === 'ar-EG' ? 'لاقي المدرّس اللي يفرق معاك' : 'Find the teacher who makes the difference'
        }
        description={
          locale === 'ar-EG'
            ? 'فلتر حسب المادة والصف والمحافظة وطريقة الشرح، وقارن بالتقييمات الحقيقية قبل ما تحجز.'
            : 'Filter by subject, grade, city, and teaching mode — then compare real ratings before you book.'
        }
        crumbs={[
          { label: locale === 'ar-EG' ? 'الرئيسية' : 'Home', href: '/' },
          { label: t('nav.teachers') },
        ]}
        aside={
          <div className="flex items-center gap-6 rounded-xl border border-border bg-card px-5 py-4">
            <div>
              <p className="tnum font-display text-heading-md font-extrabold text-foreground">
                {results.isPending ? '—' : formatNumber(totalCount)}
              </p>
              <p className="text-caption text-muted-foreground">{t('common.teachers')}</p>
            </div>
            <div className="h-10 w-px bg-border" />
            <div>
              <p className="tnum font-display text-heading-md font-extrabold text-foreground">
                {formatNumber(subjects.subjects.length)}
              </p>
              <p className="text-caption text-muted-foreground">{t('common.subject')}</p>
            </div>
          </div>
        }
      >
        <SearchBar
          value={filters.query}
          onChange={(v) => patch({ query: v })}
          placeholder={
            locale === 'ar-EG'
              ? 'دوّر باسم المدرّس، المادة، أو المحافظة…'
              : 'Search by teacher name, subject, or city…'
          }
          size="lg"
          id="teachers-search"
          className="max-w-2xl"
        />
      </PageHeader>

      <section className="section-y !py-10">
        <div className="container-page">
          <div className="grid gap-8 lg:grid-cols-[17rem,1fr]">
            {/* Filters */}
            <FilterPanel activeCount={activeCount} onClear={clearAll} resultCount={totalCount}>
              <FilterGroup title={t('common.subject')} count={filters.subjectId ? 1 : 0}>
                {subjects.isPending ? (
                  <FilterSkeleton rows={5} />
                ) : (
                  subjects.subjects.map((s) => (
                    <FilterCheckbox
                      key={s.id}
                      id={`subject-${s.id}`}
                      label={subjects.label(s)}
                      count={s.teachersCount}
                      checked={filters.subjectId === s.id}
                      onChange={() =>
                        patch({ subjectId: filters.subjectId === s.id ? undefined : s.id })
                      }
                      icon={
                        <SubjectIcon
                          subject={subjectKeyFor(s)}
                          className="size-4 text-muted-foreground"
                        />
                      }
                    />
                  ))
                )}
              </FilterGroup>

              <FilterGroup
                title={t('common.grade')}
                count={filters.gradeId ? 1 : 0}
                defaultOpen={false}
              >
                {grades.isPending ? (
                  <FilterSkeleton rows={4} />
                ) : (
                  Object.entries(grades.byStage).map(([stage, list]) => (
                    <div key={stage} className="space-y-2.5">
                      <p className="text-label uppercase tracking-wide text-muted-foreground">
                        {STAGE_LABELS[stage]?.[lang] ?? stage}
                      </p>
                      {list.map((g) => (
                        <FilterCheckbox
                          key={g.id}
                          id={`grade-${g.id}`}
                          label={grades.label(g)}
                          count={g.teachersCount}
                          checked={filters.gradeId === g.id}
                          onChange={() =>
                            patch({ gradeId: filters.gradeId === g.id ? undefined : g.id })
                          }
                        />
                      ))}
                    </div>
                  ))
                )}
              </FilterGroup>

              <FilterGroup title={t('common.city')} count={filters.city ? 1 : 0} defaultOpen={false}>
                <input
                  type="text"
                  value={filters.city ?? ''}
                  onChange={(e) => patch({ city: e.target.value })}
                  placeholder={locale === 'ar-EG' ? 'اكتب اسم المحافظة' : 'Type a city'}
                  className="h-10 w-full rounded-lg border border-input bg-surface px-3 text-body-sm outline-none transition-colors placeholder:text-muted-foreground focus:border-ring focus-visible:ring-2 focus-visible:ring-ring"
                />
              </FilterGroup>

              <FilterGroup
                title={locale === 'ar-EG' ? 'طريقة الشرح' : 'Teaching mode'}
                count={filters.mode ? 1 : 0}
              >
                {MODES.map((m) => (
                  <FilterCheckbox
                    key={m.value}
                    id={`mode-${m.value}`}
                    label={m[lang]}
                    checked={filters.mode === m.value}
                    onChange={() =>
                      patch({ mode: filters.mode === m.value ? undefined : m.value })
                    }
                  />
                ))}
              </FilterGroup>

              <FilterGroup title={t('common.rating')}>
                <div className="space-y-2">
                  {RATING_STEPS.map((r) => (
                    <button
                      key={r}
                      type="button"
                      onClick={() => patch({ minRating: filters.minRating === r ? 0 : r })}
                      aria-pressed={filters.minRating === r}
                      className={cn(
                        'flex w-full items-center gap-2 rounded-lg border px-3 py-2 text-body-sm transition-colors',
                        filters.minRating === r
                          ? 'border-primary bg-primary/8 font-semibold text-foreground'
                          : 'border-border text-muted-foreground hover:border-foreground/20 hover:text-foreground',
                      )}
                    >
                      <Star className="size-4 fill-accent text-accent" aria-hidden />
                      <span className="tnum">
                        {r}
                        {locale === 'ar-EG' ? ' فأعلى' : '+'}
                      </span>
                    </button>
                  ))}
                </div>
              </FilterGroup>
            </FilterPanel>

            {/* Results */}
            <div className="min-w-0">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <p className="text-body-sm text-muted-foreground">
                  {results.isPending ? (
                    <span className="inline-block h-4 w-32 animate-pulse rounded bg-muted" />
                  ) : (
                    <>
                      <span className="tnum font-display font-bold text-foreground">
                        {formatNumber(totalCount)}
                      </span>{' '}
                      {locale === 'ar-EG' ? 'مدرّس مطابق لبحثك' : 'teachers match your search'}
                    </>
                  )}
                </p>

                <div className="flex items-center gap-2">
                  <div className="hidden items-center rounded-lg border border-border bg-card p-1 sm:flex">
                    {(['grid', 'list'] as const).map((v) => (
                      <button
                        key={v}
                        type="button"
                        onClick={() => setView(v)}
                        aria-pressed={view === v}
                        aria-label={v === 'grid' ? 'Grid view' : 'List view'}
                        className={cn(
                          'grid size-8 place-items-center rounded-md transition-colors',
                          view === v
                            ? 'bg-primary text-primary-foreground'
                            : 'text-muted-foreground hover:text-foreground',
                        )}
                      >
                        {v === 'grid' ? (
                          <LayoutGrid className="size-4" />
                        ) : (
                          <List className="size-4" />
                        )}
                      </button>
                    ))}
                  </div>

                  <Select value={filters.sort} onValueChange={(v) => patch({ sort: v })}>
                    <SelectTrigger className="w-48">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {SORT_OPTIONS.map((o) => (
                        <SelectItem key={o.value} value={o.value}>
                          {o[lang]}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>

              {/* Active chips */}
              {activeCount > 0 && (
                <div className="mt-4 flex flex-wrap items-center gap-2">
                  {filters.query.trim() && (
                    <ActiveFilterChip
                      label={`${t('common.search')}: ${filters.query}`}
                      onRemove={() => patch({ query: '' })}
                    />
                  )}
                  {activeSubject && (
                    <ActiveFilterChip
                      label={subjects.label(activeSubject)}
                      onRemove={() => patch({ subjectId: undefined })}
                    />
                  )}
                  {activeGrade && (
                    <ActiveFilterChip
                      label={grades.label(activeGrade)}
                      onRemove={() => patch({ gradeId: undefined })}
                    />
                  )}
                  {filters.city && (
                    <ActiveFilterChip
                      label={filters.city}
                      onRemove={() => patch({ city: undefined })}
                    />
                  )}
                  {filters.mode && (
                    <ActiveFilterChip
                      label={MODES.find((m) => m.value === filters.mode)![lang]}
                      onRemove={() => patch({ mode: undefined })}
                    />
                  )}
                  {filters.minRating > 0 && (
                    <ActiveFilterChip
                      label={`${filters.minRating}+`}
                      onRemove={() => patch({ minRating: 0 })}
                    />
                  )}
                  <Button variant="ghost" size="sm" onClick={clearAll}>
                    {t('common.clearFilters')}
                  </Button>
                </div>
              )}

              {/* Grid */}
              <div className={cn('mt-6 transition-opacity', results.isFetching && 'opacity-60')}>
                {results.isPending ? (
                  <CardGridSkeleton count={6} className="lg:grid-cols-2 xl:grid-cols-3" />
                ) : results.isError ? (
                  <QueryError error={results.error} onRetry={() => results.refetch()} />
                ) : items.length === 0 ? (
                  <EmptyState
                    spot="teachers"
                    title={
                      locale === 'ar-EG'
                        ? 'مفيش مدرّس مطابق للبحث ده'
                        : 'No teachers match this search'
                    }
                    description={
                      locale === 'ar-EG'
                        ? 'جرّب تشيل شوية فلاتر، أو دوّر باسم المادة بس.'
                        : 'Try removing a filter, or search by subject only.'
                    }
                    action={
                      activeCount
                        ? { label: t('common.clearFilters'), onClick: clearAll }
                        : undefined
                    }
                    secondaryAction={{ label: t('nav.groups'), href: '/groups' }}
                  />
                ) : view === 'grid' ? (
                  <RevealGroup className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
                    {items.map((teacher) => (
                      <RevealItem key={teacher.id} className="h-full">
                        <TeacherCard teacher={teacher} />
                      </RevealItem>
                    ))}
                  </RevealGroup>
                ) : (
                  <RevealGroup className="space-y-4">
                    {items.map((teacher) => (
                      <RevealItem key={teacher.id}>
                        <TeacherRow teacher={teacher} />
                      </RevealItem>
                    ))}
                  </RevealGroup>
                )}
              </div>

              {data && items.length > 0 && (
                <Pagination page={data} onChange={setPage} className="mt-8" />
              )}
            </div>
          </div>
        </div>
      </section>
    </>
  )
}

function FilterSkeleton({ rows }: { rows: number }) {
  return (
    <div className="space-y-2.5">
      {Array.from({ length: rows }, (_, i) => (
        <div key={i} className="h-7 animate-pulse rounded-md bg-muted" />
      ))}
    </div>
  )
}
