import * as React from 'react'
import { useSearchParams } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Star } from 'lucide-react'
import { coursesApi, type CourseQuery } from '@/api/endpoints/catalog'
import { qk } from '@/lib/query-client'
import { cn } from '@/lib/utils'
import { usePageTitle } from '@/hooks/use-page-title'
import { useDebounced } from '@/hooks/use-ui'
import { STAGE_LABELS, useGrades, useSubjects } from '@/hooks/use-taxonomy'
import { useI18n } from '@/i18n'
import { subjectKeyFor } from '@/lib/identity'
import { PageHeader } from '@/components/common/PageHeader'
import { ActiveFilterChip, FilterChip, SearchBar } from '@/components/common/controls'
import { FilterCheckbox, FilterGroup, FilterPanel } from '@/components/common/FilterPanel'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { Pagination } from '@/components/common/Pagination'
import { CardGridSkeleton } from '@/components/ui/skeleton'
import { CourseCard } from '@/components/courses/CourseCard'
import { SubjectIcon } from '@/components/illustrations/SubjectIcon'
import { Slider } from '@/components/ui/primitives'
import { Button } from '@/components/ui/button'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'

const PAGE_SIZE = 12
const MAX_PRICE = 5000

const SORT_OPTIONS = [
  { value: 'featured', ar: 'الأكثر طلبًا', en: 'Most popular' },
  { value: 'rating', ar: 'الأعلى تقييمًا', en: 'Highest rated' },
  { value: 'students', ar: 'الأكتر طلابًا', en: 'Most students' },
  { value: 'price-asc', ar: 'الأرخص أولًا', en: 'Price: low to high' },
  { value: 'price-desc', ar: 'الأغلى أولًا', en: 'Price: high to low' },
] as const

interface Filters {
  query: string
  subjectId?: string
  gradeId?: string
  maxPrice: number
  isFree: boolean
  minRating: number
  sort: string
}

const EMPTY_FILTERS: Filters = {
  query: '',
  maxPrice: MAX_PRICE,
  isFree: false,
  minRating: 0,
  sort: 'featured',
}

export default function CoursesPage() {
  const { t, locale, formatNumber, formatCurrency } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'

  usePageTitle(
    t('nav.courses'),
    locale === 'ar-EG'
      ? 'كورسات مسجّلة في كل المواد — مراجعات نهائية وتأسيس وحل مسائل.'
      : 'Recorded courses across every subject — revision, foundations, and problem solving.',
  )

  const [searchParams, setSearchParams] = useSearchParams()
  const [page, setPage] = React.useState(1)
  const [filters, setFilters] = React.useState<Filters>(() => {
    // Global search hands its term over in `?q=`, so the full directory opens
    // already filtered rather than showing everything for a beat first.
    const seed = searchParams.get('q')?.trim()
    return seed ? { ...EMPTY_FILTERS, query: seed } : EMPTY_FILTERS
  })

  const subjects = useSubjects()
  const grades = useGrades()

  const subjectSlug = searchParams.get('subject')
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

  const apiQuery: CourseQuery = React.useMemo(
    () => ({
      Page: page,
      PageSize: PAGE_SIZE,
      Search: debouncedQuery.trim() || undefined,
      SubjectId: filters.subjectId,
      GradeId: filters.gradeId,
      MaxPrice: filters.maxPrice < MAX_PRICE ? filters.maxPrice : undefined,
      IsFree: filters.isFree || undefined,
      MinRating: filters.minRating || undefined,
      Sort: filters.sort,
    }),
    [page, debouncedQuery, filters],
  )

  const results = useQuery({
    queryKey: qk.courses.list(apiQuery),
    queryFn: () => coursesApi.list(apiQuery),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => {
    setPage(1)
  }, [debouncedQuery, filters.subjectId, filters.gradeId, filters.maxPrice, filters.isFree, filters.minRating, filters.sort])

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
    (filters.maxPrice < MAX_PRICE ? 1 : 0) +
    (filters.isFree ? 1 : 0) +
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
        eyebrow={locale === 'ar-EG' ? 'سوق الكورسات' : 'Course marketplace'}
        title={locale === 'ar-EG' ? 'كورسات تذاكر منها في أي وقت' : 'Courses you can study any time'}
        description={
          locale === 'ar-EG'
            ? 'محتوى مسجّل ومنظّم من مدرّسين المنصة — تشتريه مرة وتفضل معاك.'
            : 'Recorded, structured content from platform teachers — buy once, keep forever.'
        }
        crumbs={[
          { label: locale === 'ar-EG' ? 'الرئيسية' : 'Home', href: '/' },
          { label: t('nav.courses') },
        ]}
        aside={
          <div className="flex items-center gap-6 rounded-xl border border-border bg-card px-5 py-4">
            <div>
              <p className="tnum font-display text-heading-md font-extrabold text-foreground">
                {results.isPending ? '—' : formatNumber(totalCount)}
              </p>
              <p className="text-caption text-muted-foreground">{t('common.courses')}</p>
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
        <div className="space-y-5">
          <SearchBar
            value={filters.query}
            onChange={(v) => patch({ query: v })}
            placeholder={
              locale === 'ar-EG'
                ? 'دوّر باسم الكورس، المادة، أو المدرّس…'
                : 'Search by course, subject, or teacher…'
            }
            size="lg"
            id="courses-search"
            className="max-w-2xl"
          />

          {/* Subject quick-pick */}
          {subjects.subjects.length > 0 && (
            <div className="no-scrollbar -mx-1 flex gap-2 overflow-x-auto px-1 pb-1">
              <FilterChip
                label={t('common.all')}
                active={!filters.subjectId}
                onClick={() => patch({ subjectId: undefined })}
              />
              {subjects.subjects.map((s) => (
                <FilterChip
                  key={s.id}
                  label={subjects.label(s)}
                  icon={<SubjectIcon subject={subjectKeyFor(s)} strokeWidth={1.8} />}
                  active={filters.subjectId === s.id}
                  onClick={() =>
                    patch({ subjectId: filters.subjectId === s.id ? undefined : s.id })
                  }
                />
              ))}
            </div>
          )}
        </div>
      </PageHeader>

      <section className="section-y !py-10">
        <div className="container-page">
          <div className="grid gap-8 lg:grid-cols-[17rem,1fr]">
            <FilterPanel activeCount={activeCount} onClear={clearAll} resultCount={totalCount}>
              <FilterGroup title={t('common.subject')} count={filters.subjectId ? 1 : 0}>
                {subjects.subjects.map((s) => (
                  <FilterCheckbox
                    key={s.id}
                    id={`c-subject-${s.id}`}
                    label={subjects.label(s)}
                    count={s.coursesCount}
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
                ))}
              </FilterGroup>

              <FilterGroup title={t('common.grade')} count={filters.gradeId ? 1 : 0}>
                {Object.entries(grades.byStage).map(([stage, list]) => (
                  <div key={stage} className="space-y-2.5">
                    <p className="text-label uppercase tracking-wide text-muted-foreground">
                      {STAGE_LABELS[stage]?.[lang] ?? stage}
                    </p>
                    {list.map((g) => (
                      <FilterCheckbox
                        key={g.id}
                        id={`c-grade-${g.id}`}
                        label={grades.label(g)}
                        count={g.coursesCount}
                        checked={filters.gradeId === g.id}
                        onChange={() =>
                          patch({ gradeId: filters.gradeId === g.id ? undefined : g.id })
                        }
                      />
                    ))}
                  </div>
                ))}
              </FilterGroup>

              <FilterGroup title={t('common.price')}>
                <div className="space-y-4 pt-1">
                  <FilterCheckbox
                    id="c-free"
                    label={t('common.free')}
                    checked={filters.isFree}
                    onChange={() => patch({ isFree: !filters.isFree })}
                  />
                  <div className={cn('space-y-3', filters.isFree && 'pointer-events-none opacity-50')}>
                    <div className="flex items-center justify-between text-caption text-muted-foreground">
                      <span>{t('common.total')}</span>
                      <span className="tnum font-semibold text-foreground">
                        {filters.maxPrice >= MAX_PRICE
                          ? locale === 'ar-EG'
                            ? 'أي سعر'
                            : 'Any price'
                          : formatCurrency(filters.maxPrice)}
                      </span>
                    </div>
                    <Slider
                      value={[filters.maxPrice]}
                      onValueChange={([v]) => patch({ maxPrice: v })}
                      min={0}
                      max={MAX_PRICE}
                      step={50}
                      aria-label={t('common.price')}
                    />
                  </div>
                </div>
              </FilterGroup>

              <FilterGroup title={t('common.rating')}>
                <div className="space-y-2">
                  {[4.5, 4, 3.5].map((r) => (
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
                      {locale === 'ar-EG' ? 'كورس' : 'courses'}
                    </>
                  )}
                </p>

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
                  {filters.isFree && (
                    <ActiveFilterChip
                      label={t('common.free')}
                      onRemove={() => patch({ isFree: false })}
                    />
                  )}
                  {filters.maxPrice < MAX_PRICE && (
                    <ActiveFilterChip
                      label={`≤ ${formatCurrency(filters.maxPrice)}`}
                      onRemove={() => patch({ maxPrice: MAX_PRICE })}
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

              <div className={cn('mt-6 transition-opacity', results.isFetching && 'opacity-60')}>
                {results.isPending ? (
                  <CardGridSkeleton count={6} className="lg:grid-cols-2 xl:grid-cols-3" />
                ) : results.isError ? (
                  <QueryError error={results.error} onRetry={() => results.refetch()} />
                ) : items.length === 0 ? (
                  <EmptyState
                    spot="courses"
                    title={
                      locale === 'ar-EG' ? 'مفيش كورسات مطابقة' : 'No courses match these filters'
                    }
                    description={
                      locale === 'ar-EG'
                        ? 'وسّع نطاق السعر أو شيل فلتر المادة وجرّب تاني.'
                        : 'Widen the price range or clear the subject filter and try again.'
                    }
                    action={
                      activeCount
                        ? { label: t('common.clearFilters'), onClick: clearAll }
                        : undefined
                    }
                    secondaryAction={{ label: t('nav.teachers'), href: '/teachers' }}
                  />
                ) : (
                  <RevealGroup className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
                    {items.map((course) => (
                      <RevealItem key={course.id} className="h-full">
                        <CourseCard course={course} />
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
