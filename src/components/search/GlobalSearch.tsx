import * as React from 'react'
import { useNavigate } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import {
  BookOpen,
  GraduationCap,
  Loader2,
  Search,
  Shapes,
  TriangleAlert,
  Users,
  type LucideIcon,
} from 'lucide-react'
import { searchApi } from '@/api/endpoints/catalog'
import type {
  CourseListItemDto,
  GlobalSearchResultDto,
  GroupListItemDto,
  SubjectDto,
  TeacherListItemDto,
} from '@/api/schema'
import { qk } from '@/lib/query-client'
import { cn } from '@/lib/utils'
import { transitions } from '@/lib/motion'
import { avatarTraitsFor, imageUrl, subjectKeyFor } from '@/lib/identity'
import { useDebounced } from '@/hooks/use-ui'
import { useI18n, type TranslationKey } from '@/i18n'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { SubjectTile } from '@/components/illustrations/SubjectIcon'
import { Spot } from '@/components/illustrations/Spot'
import { Dialog, DialogContent, DialogTitle } from '@/components/ui/dialog'

/**
 * The shortest term the API will accept. Asking with less is a 422, so the box
 * waits rather than sending a request it knows will be refused.
 */
const MIN_TERM = 2

/** Results per entity type. Five keeps every group visible without scrolling. */
const TAKE = 5

/* -------------------------------------------------------------------------- */
/*                                  Flattening                                */
/* -------------------------------------------------------------------------- */

/**
 * One keyboard-navigable row. The palette flattens the four result groups into
 * a single list so ↑/↓ crosses group boundaries the way the eye does, while
 * `group` still lets the renderer put the headings back.
 */
interface Row {
  key: string
  group: 'teachers' | 'groups' | 'courses' | 'subjects'
  href: string
  render: () => React.ReactNode
}

function flatten(
  result: GlobalSearchResultDto | undefined,
  locale: 'ar-EG' | 'en',
): Row[] {
  if (!result) return []

  const rows: Row[] = []

  for (const teacher of result.teachers ?? []) {
    if (!teacher.id) continue
    rows.push({
      key: `teacher-${teacher.id}`,
      group: 'teachers',
      href: `/teachers/${teacher.id}`,
      render: () => <TeacherRow teacher={teacher} locale={locale} />,
    })
  }

  for (const group of result.groups ?? []) {
    if (!group.id) continue
    rows.push({
      key: `group-${group.id}`,
      group: 'groups',
      href: `/groups/${group.id}`,
      render: () => <GroupRow group={group} />,
    })
  }

  for (const course of result.courses ?? []) {
    if (!course.id) continue
    rows.push({
      key: `course-${course.id}`,
      group: 'courses',
      href: `/courses/${course.id}`,
      render: () => <CourseRow course={course} />,
    })
  }

  for (const subject of result.subjects ?? []) {
    if (!subject.id) continue
    rows.push({
      key: `subject-${subject.id}`,
      group: 'subjects',
      // A subject is a lens on the directory, not a page of its own.
      href: `/teachers?subject=${subject.slug ?? subject.id}`,
      render: () => <SubjectRow subject={subject} locale={locale} />,
    })
  }

  return rows
}

/* -------------------------------------------------------------------------- */
/*                                   Palette                                  */
/* -------------------------------------------------------------------------- */

export interface GlobalSearchProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

/**
 * Cross-entity search over teachers, groups, courses and subjects — the one box
 * that reaches the whole catalogue. Mounted by the shells, opened by their
 * trigger or by ⌘K / Ctrl-K.
 */
export function GlobalSearch({ open, onOpenChange }: GlobalSearchProps) {
  const { t, locale, formatNumber, isRtl } = useI18n()
  const navigate = useNavigate()

  const [term, setTerm] = React.useState('')
  const [active, setActive] = React.useState(0)
  const listRef = React.useRef<HTMLDivElement>(null)
  const inputRef = React.useRef<HTMLInputElement>(null)

  const debounced = useDebounced(term.trim(), 300)
  const ready = debounced.length >= MIN_TERM

  const results = useQuery({
    queryKey: qk.search(debounced),
    queryFn: () => searchApi.global(debounced, { Take: TAKE }),
    enabled: open && ready,
    placeholderData: keepPreviousData,
    staleTime: 60_000,
  })

  const rows = React.useMemo(
    () => flatten(ready ? results.data : undefined, locale),
    [ready, results.data, locale],
  )

  /* A new result set always starts the selection over at the top. */
  React.useEffect(() => setActive(0), [debounced])

  /* Each open is a fresh search — a stale term from last time is just noise. */
  React.useEffect(() => {
    if (open) return
    setTerm('')
    setActive(0)
  }, [open])

  /* Keep the highlighted row inside the scroll viewport. */
  React.useEffect(() => {
    const row = listRef.current?.querySelector<HTMLElement>('[data-active="true"]')
    row?.scrollIntoView({ block: 'nearest' })
  }, [active, rows.length])

  const go = React.useCallback(
    (href: string) => {
      onOpenChange(false)
      navigate(href)
    },
    [navigate, onOpenChange],
  )

  const onKeyDown = (event: React.KeyboardEvent) => {
    if (!rows.length) return

    if (event.key === 'ArrowDown') {
      event.preventDefault()
      setActive((i) => (i + 1) % rows.length)
    } else if (event.key === 'ArrowUp') {
      event.preventDefault()
      setActive((i) => (i - 1 + rows.length) % rows.length)
    } else if (event.key === 'Home') {
      event.preventDefault()
      setActive(0)
    } else if (event.key === 'End') {
      event.preventDefault()
      setActive(rows.length - 1)
    } else if (event.key === 'Enter') {
      event.preventDefault()
      const row = rows[active]
      if (row) go(row.href)
    }
  }

  const total = results.data?.totalResults ?? 0
  /* The query is in flight for a term that has no results on screen yet. */
  const firstLoad = ready && results.isFetching && rows.length === 0

  const listboxId = 'global-search-results'
  const activeRow = rows[active]

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        size="lg"
        hideClose
        /*
         * Anchored near the top like a command palette and flush to its edges.
         * The content is a grid, so the result list gets its own `minmax(0,1fr)`
         * row — that is what gives the overflow something finite to scroll in.
         */
        className={cn(
          'top-[7vh] -translate-y-0 gap-0 overflow-hidden p-0',
          'max-h-[min(34rem,86dvh)] grid-rows-[auto_minmax(0,1fr)_auto]',
        )}
        onOpenAutoFocus={(event) => {
          // Focus the field, not the dialog, so typing starts immediately.
          event.preventDefault()
          inputRef.current?.focus()
        }}
      >
        <DialogTitle className="sr-only">{t('search.open')}</DialogTitle>

        {/* -------------------------------- Field ------------------------------- */}

        <div className="flex items-center gap-3 border-b border-border px-4 sm:px-5">
          <Search className="size-[1.125rem] shrink-0 text-muted-foreground" aria-hidden />
          <input
            ref={inputRef}
            type="search"
            value={term}
            onChange={(event) => setTerm(event.target.value)}
            onKeyDown={onKeyDown}
            placeholder={t('search.placeholder')}
            dir="auto"
            autoComplete="off"
            spellCheck={false}
            enterKeyHint="search"
            aria-label={t('search.open')}
            role="combobox"
            aria-expanded={rows.length > 0}
            aria-controls={listboxId}
            aria-activedescendant={activeRow ? `gs-${activeRow.key}` : undefined}
            className={cn(
              'h-14 flex-1 bg-transparent font-display text-body text-foreground outline-none',
              'placeholder:font-sans placeholder:text-body-sm placeholder:text-muted-foreground',
              // Chrome's own clear button would sit on top of the spinner.
              '[&::-webkit-search-cancel-button]:hidden',
            )}
          />

          {/* Refetching with results already up: a quiet spinner, no layout jump. */}
          <AnimatePresence>
            {ready && results.isFetching && rows.length > 0 && (
              <motion.span
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
                transition={transitions.fast}
                aria-hidden
              >
                <Loader2 className="size-4 animate-spin text-muted-foreground" />
              </motion.span>
            )}
          </AnimatePresence>

          <kbd className="hidden shrink-0 rounded border border-border bg-muted px-1.5 py-0.5 font-sans text-[0.625rem] font-semibold text-muted-foreground sm:block">
            Esc
          </kbd>
        </div>

        {/* -------------------------------- Body -------------------------------- */}

        <div ref={listRef} className="overflow-y-auto overscroll-contain">
          {!ready ? (
            <Hint title={t('search.hint')} description={t('search.hintKeys')} />
          ) : firstLoad ? (
            <RowSkeletons />
          ) : results.isError ? (
            <Message
              spot="error"
              title={t('search.failed')}
              description={t('search.failedHint')}
              action={{ label: t('common.retry'), onClick: () => results.refetch() }}
            />
          ) : rows.length === 0 ? (
            <Message
              spot="search"
              title={t('search.empty', { query: debounced })}
              description={t('search.emptyHint')}
            />
          ) : (
            <div id={listboxId} role="listbox" aria-label={t('search.title')} className="py-2">
              {GROUPS.map(({ key, labelKey, icon: Icon, all }) => {
                const groupRows = rows.filter((row) => row.group === key)
                if (!groupRows.length) return null

                return (
                  /* `group` keeps the options' owning listbox intact across
                     the heading that separates them. */
                  <div key={key} role="group" aria-label={t(labelKey)} className="pb-1.5 last:pb-0">
                    <div className="flex items-center gap-1.5 px-4 pb-1 pt-2 sm:px-5">
                      <Icon className="size-3.5 text-muted-foreground" aria-hidden />
                      <h3 aria-hidden className="text-label uppercase tracking-wide text-muted-foreground/80">
                        {t(labelKey)}
                      </h3>
                    </div>

                    {groupRows.map((row) => {
                      const index = rows.indexOf(row)
                      const isActive = index === active
                      return (
                        <div
                          key={row.key}
                          id={`gs-${row.key}`}
                          role="option"
                          aria-selected={isActive}
                          data-active={isActive}
                          tabIndex={-1}
                          onClick={() => go(row.href)}
                          onPointerMove={() => setActive(index)}
                          className="cursor-pointer px-2 sm:px-3"
                        >
                          <div
                            className={cn(
                              'flex items-center gap-3 rounded-xl px-2 py-2.5 transition-colors sm:px-2.5',
                              isActive ? 'bg-muted' : 'bg-transparent',
                            )}
                          >
                            {row.render()}
                          </div>
                        </div>
                      )
                    })}

                    {all && (
                      <div className="px-4 pt-1 sm:px-5">
                        <button
                          type="button"
                          onClick={() => go(`${all.href}?q=${encodeURIComponent(debounced)}`)}
                          className="rounded text-caption font-semibold text-primary underline-offset-4 transition-opacity hover:underline hover:opacity-80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                        >
                          {t(all.labelKey)}
                        </button>
                      </div>
                    )}
                  </div>
                )
              })}
            </div>
          )}
        </div>

        {/* -------------------------------- Footer ------------------------------- */}

        {ready && rows.length > 0 && (
          <div className="flex items-center justify-between gap-3 border-t border-border px-4 py-2.5 sm:px-5">
            <p className="tnum text-caption text-muted-foreground">
              {t('search.resultCount', { count: formatNumber(total) })}
            </p>
            <p className="hidden items-center gap-1.5 text-caption text-muted-foreground sm:flex">
              <kbd className="rounded border border-border bg-muted px-1.5 py-0.5 font-sans text-[0.625rem] font-semibold">
                {isRtl ? '↓ ↑' : '↑ ↓'}
              </kbd>
              <kbd className="rounded border border-border bg-muted px-1.5 py-0.5 font-sans text-[0.625rem] font-semibold">
                ↵
              </kbd>
              {t('search.hintKeys')}
            </p>
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}

/* -------------------------------------------------------------------------- */
/*                                   Groups                                   */
/* -------------------------------------------------------------------------- */

const GROUPS: {
  key: Row['group']
  labelKey: TranslationKey
  icon: LucideIcon
  /** The directory this group's results live in, seeded with the term. */
  all?: { href: string; labelKey: TranslationKey }
}[] = [
  {
    key: 'teachers',
    labelKey: 'search.groupTeachers',
    icon: GraduationCap,
    all: { href: '/teachers', labelKey: 'search.allTeachers' },
  },
  {
    key: 'groups',
    labelKey: 'search.groupGroups',
    icon: Users,
    all: { href: '/groups', labelKey: 'search.allGroups' },
  },
  {
    key: 'courses',
    labelKey: 'search.groupCourses',
    icon: BookOpen,
    all: { href: '/courses', labelKey: 'search.allCourses' },
  },
  {
    key: 'subjects',
    labelKey: 'search.groupSubjects',
    icon: Shapes,
    /* A subject row already lands on the filtered directory. */
  },
]

/* -------------------------------------------------------------------------- */
/*                                    Rows                                    */
/* -------------------------------------------------------------------------- */

function Title({ children }: { children: React.ReactNode }) {
  return (
    <span
      dir="auto"
      className="block truncate font-display text-body-sm font-bold text-foreground"
    >
      {children}
    </span>
  )
}

function Meta({ children }: { children: React.ReactNode }) {
  return (
    <span dir="auto" className="block truncate text-caption text-muted-foreground">
      {children}
    </span>
  )
}

function TeacherRow({
  teacher,
  locale,
}: {
  teacher: TeacherListItemDto
  locale: 'ar-EG' | 'en'
}) {
  const name = teacher.displayName ?? ''
  const subject = teacher.subjects?.[0]
  const subjectName = subject
    ? locale === 'ar-EG'
      ? subject.nameAr
      : (subject.nameEn ?? subject.nameAr)
    : undefined

  return (
    <>
      <Avatar
        size="xs"
        name={name}
        seed={teacher.id ?? name}
        traits={avatarTraitsFor(teacher.id ?? name)}
        src={imageUrl(teacher.profileImageUrl)}
      />
      <span className="min-w-0 flex-1">
        <Title>{name}</Title>
        <Meta>{teacher.headline || subjectName || teacher.city}</Meta>
      </span>
    </>
  )
}

function GroupRow({ group }: { group: GroupListItemDto }) {
  return (
    <>
      <SubjectTile subject={subjectKeyFor(group.subject)} size="sm" />
      <span className="min-w-0 flex-1">
        <Title>{group.name}</Title>
        <Meta>{group.teacher?.displayName || group.typeName}</Meta>
      </span>
    </>
  )
}

function CourseRow({ course }: { course: CourseListItemDto }) {
  return (
    <>
      <SubjectTile subject={subjectKeyFor(course.subject)} size="sm" />
      <span className="min-w-0 flex-1">
        <Title>{course.title}</Title>
        <Meta>{course.teacher?.displayName}</Meta>
      </span>
    </>
  )
}

function SubjectRow({
  subject,
  locale,
}: {
  subject: SubjectDto
  locale: 'ar-EG' | 'en'
}) {
  const isAr = locale === 'ar-EG'
  const name = isAr ? subject.nameAr : (subject.nameEn ?? subject.nameAr)
  /*
   * The search projection fills only the subject's names and styling — its
   * counts come back as zero — so the second line carries the other language's
   * name rather than a teacher count that would always read nought.
   */
  const alternate = isAr ? subject.nameEn : subject.nameAr

  return (
    <>
      <SubjectTile subject={subjectKeyFor(subject)} size="sm" />
      <span className="min-w-0 flex-1">
        <Title>{name}</Title>
        <Meta>{alternate !== name ? alternate : undefined}</Meta>
      </span>
    </>
  )
}

/* -------------------------------------------------------------------------- */
/*                                   States                                   */
/* -------------------------------------------------------------------------- */

function Hint({ title, description }: { title: string; description: string }) {
  return (
    <div className="flex flex-col items-center gap-2 px-6 py-12 text-center">
      <Spot name="search" className="h-24" />
      <p className="font-display text-body-sm font-bold text-foreground">{title}</p>
      <p className="text-caption text-muted-foreground">{description}</p>
    </div>
  )
}

function Message({
  spot,
  title,
  description,
  action,
}: {
  spot: 'search' | 'error'
  title: string
  description: string
  action?: { label: string; onClick: () => void }
}) {
  return (
    <div className="flex flex-col items-center gap-2 px-6 py-12 text-center">
      {spot === 'error' ? (
        <TriangleAlert className="size-8 text-destructive-strong" aria-hidden />
      ) : (
        <Spot name={spot} className="h-24" />
      )}
      <p dir="auto" className="font-display text-body-sm font-bold text-foreground">
        {title}
      </p>
      <p className="text-caption text-muted-foreground">{description}</p>
      {action && (
        <button
          type="button"
          onClick={action.onClick}
          className="mt-1 rounded text-caption font-semibold text-primary underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {action.label}
        </button>
      )}
    </div>
  )
}

function RowSkeletons() {
  return (
    <div className="space-y-1 p-3">
      {[0, 1, 2, 3].map((row) => (
        <div key={row} className="flex items-center gap-3 rounded-xl px-2.5 py-2.5">
          <span className="size-8 shrink-0 animate-pulse rounded-lg bg-muted" />
          <span className="flex-1 space-y-2">
            <span className="block h-3 w-2/5 animate-pulse rounded bg-muted" />
            <span className="block h-3 w-1/4 animate-pulse rounded bg-muted" />
          </span>
        </div>
      ))}
    </div>
  )
}
