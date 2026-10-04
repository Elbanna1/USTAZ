import * as React from 'react'
import { useSearchParams } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { groupsApi, type GroupQuery } from '@/api/endpoints/catalog'
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
import { GroupCard } from '@/components/groups/GroupCard'
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
const MAX_PRICE = 3000

const SORT_OPTIONS = [
  { value: 'featured', ar: 'المقترحة', en: 'Recommended' },
  { value: 'rating', ar: 'الأعلى تقييمًا', en: 'Highest rated' },
  { value: 'price-asc', ar: 'الأرخص أولًا', en: 'Price: low to high' },
  { value: 'price-desc', ar: 'الأغلى أولًا', en: 'Price: high to low' },
  { value: 'newest', ar: 'الأحدث', en: 'Newest' },
] as const

const TYPES = [
  { value: 'Regular', ar: 'منتظمة', en: 'Regular' },
  { value: 'Revision', ar: 'مراجعة نهائية', en: 'Revision' },
  { value: 'Intensive', ar: 'مكثّفة', en: 'Intensive' },
  { value: 'ExamPreparation', ar: 'استعداد للامتحان', en: 'Exam prep' },
] as const

const MODES = [
  { value: 'Online', ar: 'أونلاين', en: 'Online' },
  { value: 'Offline', ar: 'حضوري', en: 'In person' },
  { value: 'Hybrid', ar: 'أونلاين وحضوري', en: 'Hybrid' },
] as const

interface Filters {
  query: string
  subjectId?: string
  gradeId?: string
  type?: (typeof TYPES)[number]['value']
  mode?: (typeof MODES)[number]['value']
  city?: string
  maxPrice: number
  hasSeats: boolean
  hasDiscount: boolean
  sort: string
}

const EMPTY_FILTERS: Filters = {
  query: '',
  maxPrice: MAX_PRICE,
  hasSeats: false,
  hasDiscount: false,
  sort: 'featured',
}

export default function GroupsPage() {
  const { t, locale, formatNumber, formatCurrency } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'

  usePageTitle(
    t('nav.groups'),
    locale === 'ar-EG'
      ? 'مجموعات شهرية بمواعيد ثابتة — أونلاين وحضوري في كل المحافظات.'
      : 'Monthly groups on a fixed schedule — online and in person across Egypt.',
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

  const apiQuery: GroupQuery = React.useMemo(
    () => ({
      Page: page,
      PageSize: PAGE_SIZE,
      Search: debouncedQuery.trim() || undefined,
      SubjectId: filters.subjectId,
      GradeId: filters.gradeId,
      Type: filters.type,
      TeachingMode: filters.mode,
      City: filters.city || undefined,
      MaxPrice: filters.maxPrice < MAX_PRICE ? filters.maxPrice : undefined,
      HasAvailableSeats: filters.hasSeats || undefined,
      HasDiscount: filters.hasDiscount || undefined,
      Sort: filters.sort,
    }),
    [page, debouncedQuery, filters],
  )

  const results = useQuery({
    queryKey: qk.groups.list(apiQuery),
    queryFn: () => groupsApi.list(apiQuery),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => {
    setPage(1)
  }, [
    debouncedQuery,
    filters.subjectId,
    filters.gradeId,
    filters.type,
    filters.mode,
    filters.city,
    filters.maxPrice,
    filters.hasSeats,
    filters.hasDiscount,
    filters.sort,
  ])

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
    (filters.type ? 1 : 0) +
    (filters.mode ? 1 : 0) +
    (filters.city ? 1 : 0) +
    (filters.maxPrice < MAX_PRICE ? 1 : 0) +
    (filters.hasSeats ? 1 : 0) +
    (filters.hasDiscount ? 1 : 0) +
    (filters.query.trim() ? 1 : 0)

  const data = results.data
  const items = data?.items ?? []
  const totalCount = data?.totalCount ?? 0

  return (
    <>
      <PageHeader
        eyebrow={locale === 'ar-EG' ? 'المجموعات' : 'Study groups'}
        title={
          locale === 'ar-EG' ? 'مجموعات بمواعيد ثابتة تناسب جدولك' : 'Groups with a schedule that fits'
        }
        description={
          locale === 'ar-EG'
            ? 'اختار المادة والصف والميعاد اللي يناسبك، وشوف الأماكن المتاحة قبل ما تحجز.'
            : 'Pick the subject, grade, and time that suit you — and see the seats left before you book.'
        }
        crumbs={[
          { label: locale === 'ar-EG' ? 'الرئيسية' : 'Home', href: '/' },
          { label: t('nav.groups') },
        ]}
        aside={
          <div className="flex items-center gap-6 rounded-xl border border-border bg-card px-5 py-4">
            <div>
              <p className="tnum font-display text-heading-md font-extrabold text-foreground">
                {results.isPending ? '—' : formatNumber(totalCount)}
              </p>
              <p className="text-caption text-muted-foreground">{t('common.groups')}</p>
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
                ? 'دوّر باسم المجموعة، المادة، أو المدرّس…'
                : 'Search by group, subject, or teacher…'
            }
            size="lg"
            id="groups-search"
            className="max-w-2xl"
          />

          <div className="no-scrollbar -mx-1 flex gap-2 overflow-x-auto px-1 pb-1">
            <FilterChip
              label={t('common.all')}
              active={!filters.type}
              onClick={() => patch({ type: undefined })}
            />
            {TYPES.map((type) => (
              <FilterChip
                key={type.value}
                label={type[lang]}
                active={filters.type === type.value}
                onClick={() =>
                  patch({ type: filters.type === type.value ? undefined : type.value })
                }
              />
            ))}
          </div>
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
                    id={`g-subject-${s.id}`}
                    label={subjects.label(s)}
                    count={s.groupsCount}
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
                        id={`g-grade-${g.id}`}
                        label={grades.label(g)}
                        count={g.groupsCount}
                        checked={filters.gradeId === g.id}
                        onChange={() =>
                          patch({ gradeId: filters.gradeId === g.id ? undefined : g.id })
                        }
                      />
                    ))}
                  </div>
                ))}
              </FilterGroup>

              <FilterGroup
                title={locale === 'ar-EG' ? 'طريقة الحضور' : 'Attendance mode'}
                count={filters.mode ? 1 : 0}
              >
                {MODES.map((m) => (
                  <FilterCheckbox
                    key={m.value}
                    id={`g-mode-${m.value}`}
                    label={m[lang]}
                    checked={filters.mode === m.value}
                    onChange={() => patch({ mode: filters.mode === m.value ? undefined : m.value })}
                  />
                ))}
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

              <FilterGroup title={t('common.price')}>
                <div className="space-y-3 pt-1">
                  <div className="flex items-center justify-between text-caption text-muted-foreground">
                    <span>{locale === 'ar-EG' ? 'شهريًا حتى' : 'Monthly up to'}</span>
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
              </FilterGroup>

              <FilterGroup title={locale === 'ar-EG' ? 'خيارات' : 'Options'}>
                <FilterCheckbox
                  id="g-seats"
                  label={locale === 'ar-EG' ? 'فيها أماكن متاحة' : 'Has available seats'}
                  checked={filters.hasSeats}
                  onChange={() => patch({ hasSeats: !filters.hasSeats })}
                />
                <FilterCheckbox
                  id="g-discount"
                  label={locale === 'ar-EG' ? 'عليها خصم' : 'Discounted'}
                  checked={filters.hasDiscount}
                  onChange={() => patch({ hasDiscount: !filters.hasDiscount })}
                />
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
                      {locale === 'ar-EG' ? 'مجموعة' : 'groups'}
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
                  {filters.subjectId && (
                    <ActiveFilterChip
                      label={subjects.label(subjects.byId(filters.subjectId))}
                      onRemove={() => patch({ subjectId: undefined })}
                    />
                  )}
                  {filters.gradeId && (
                    <ActiveFilterChip
                      label={grades.label(grades.byId(filters.gradeId))}
                      onRemove={() => patch({ gradeId: undefined })}
                    />
                  )}
                  {filters.type && (
                    <ActiveFilterChip
                      label={TYPES.find((x) => x.value === filters.type)![lang]}
                      onRemove={() => patch({ type: undefined })}
                    />
                  )}
                  {filters.mode && (
                    <ActiveFilterChip
                      label={MODES.find((x) => x.value === filters.mode)![lang]}
                      onRemove={() => patch({ mode: undefined })}
                    />
                  )}
                  {filters.city && (
                    <ActiveFilterChip
                      label={filters.city}
                      onRemove={() => patch({ city: undefined })}
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
                    spot="groups"
                    title={
                      locale === 'ar-EG' ? 'مفيش مجموعات مطابقة' : 'No groups match these filters'
                    }
                    description={
                      locale === 'ar-EG'
                        ? 'جرّب تشيل فلتر الميعاد أو المحافظة، أو شوف الكورسات المسجّلة.'
                        : 'Try clearing the time or city filter, or browse recorded courses instead.'
                    }
                    action={
                      activeCount
                        ? { label: t('common.clearFilters'), onClick: clearAll }
                        : undefined
                    }
                    secondaryAction={{ label: t('nav.courses'), href: '/courses' }}
                  />
                ) : (
                  <RevealGroup className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
                    {items.map((group) => (
                      <RevealItem key={group.id} className="h-full">
                        <GroupCard group={group} />
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
