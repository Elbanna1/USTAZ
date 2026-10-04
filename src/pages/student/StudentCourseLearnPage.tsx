import * as React from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { ArrowLeft, CheckCircle2, Circle, Clock, PlayCircle, Video } from 'lucide-react'
import { studentApi } from '@/api/endpoints/student'
import { coursesApi } from '@/api/endpoints/catalog'
import type { LessonProgressItemDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { transitions } from '@/lib/motion'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Progress } from '@/components/ui/primitives'
import { QueryError } from '@/components/common/QueryBoundary'
import { EmptyState } from '@/components/common/states'
import { RowSkeleton } from '@/components/ui/skeleton'
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion'
import { useApiErrorToast } from '@/hooks/use-api-toast'

export default function StudentCourseLearnPage() {
  const { courseId = '' } = useParams()
  const { t, locale, formatNumber } = useI18n()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()

  const progress = useQuery({
    queryKey: qk.student.courseProgress(courseId),
    queryFn: () => studentApi.courseProgress(courseId),
    enabled: Boolean(courseId),
  })

  const course = useQuery({
    queryKey: qk.courses.details(courseId),
    queryFn: () => coursesApi.details(courseId),
    enabled: Boolean(courseId),
  })

  usePageTitle(progress.data?.courseTitle ?? t('dashboard.myCourses'))

  const [activeLessonId, setActiveLessonId] = React.useState<string | null>(null)

  const toggleLesson = useMutation({
    mutationFn: ({ lessonId, completed }: { lessonId: string; completed: boolean }) =>
      completed
        ? studentApi.uncompleteLesson(courseId, lessonId)
        : studentApi.completeLesson(courseId, lessonId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: qk.student.courseProgress(courseId) })
      queryClient.invalidateQueries({ queryKey: qk.student.progress })
      queryClient.invalidateQueries({ queryKey: qk.student.dashboard })
    },
    onError: (error) =>
      showApiError(error),
  })

  if (progress.isPending) return <RowSkeleton count={6} />
  if (progress.isError) return <QueryError error={progress.error} onRetry={() => progress.refetch()} />

  const report = progress.data!
  const lessons = report.lessons ?? []

  /* Group by section, preserving the order the API returned. */
  const sections = React.useMemo(() => {
    const map = new Map<string, { title: string; lessons: LessonProgressItemDto[] }>()
    lessons.forEach((lesson) => {
      const key = lesson.sectionId ?? 'default'
      if (!map.has(key)) {
        map.set(key, {
          title: lesson.sectionTitle || (locale === 'ar-EG' ? 'الدروس' : 'Lessons'),
          lessons: [],
        })
      }
      map.get(key)!.lessons.push(lesson)
    })
    return [...map.entries()].map(([id, value]) => ({ id, ...value }))
  }, [lessons, locale])

  const activeLesson =
    lessons.find((l) => l.lessonId === activeLessonId) ??
    lessons.find((l) => !l.isCompleted) ??
    lessons[0]

  /* The public course payload carries the video URLs the progress report omits. */
  const lessonVideo = React.useMemo(() => {
    const map = new Map<string, string | null | undefined>()
    course.data?.sections?.forEach((section) => {
      section.lessons?.forEach((lesson) => {
        if (lesson.id) map.set(lesson.id, lesson.videoUrl)
      })
    })
    return map
  }, [course.data])

  const videoUrl = activeLesson?.lessonId ? lessonVideo.get(activeLesson.lessonId) : undefined

  return (
    <>
      <Button asChild variant="ghost" size="sm" className="mb-4">
        <Link to="/student/courses">
          <ArrowLeft className="size-4 flip-rtl" />
          {t('dashboard.myCourses')}
        </Link>
      </Button>

      <DashboardHeader
        title={report.courseTitle ?? ''}
        description={t('courses.progressLabel', {
          done: formatNumber(report.completedLessons ?? 0),
          total: formatNumber(report.totalLessons ?? 0),
        })}
        actions={
          <Badge variant={report.status === 'Completed' ? 'success' : 'primary'} size="md">
            {Math.round(report.progressPercentage ?? 0)}%
          </Badge>
        }
      >
        <Progress
          value={report.progressPercentage ?? 0}
          className="h-2"
          indicatorClassName={report.status === 'Completed' ? 'bg-success' : undefined}
        />
      </DashboardHeader>

      {lessons.length === 0 ? (
        <EmptyState spot="courses" title={t('courses.noLessons')} />
      ) : (
        <div className="grid gap-6 lg:grid-cols-[1.6fr,1fr]">
          {/* ----------------------------- Player ---------------------------- */}
          <div className="space-y-5">
            <Card className="overflow-hidden">
              <div className="relative aspect-video bg-primary">
                {videoUrl ? (
                  <iframe
                    key={videoUrl}
                    src={toEmbedUrl(videoUrl)}
                    title={activeLesson?.title ?? ''}
                    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                    allowFullScreen
                    className="absolute inset-0 size-full"
                  />
                ) : (
                  <div className="absolute inset-0 grid place-items-center text-primary-foreground/70">
                    <div className="text-center">
                      <Video className="mx-auto size-10 opacity-60" />
                      <p className="mt-3 text-body-sm">
                        {locale === 'ar-EG'
                          ? 'الدرس ده مفيهوش فيديو مرفوع.'
                          : 'This lesson has no video attached.'}
                      </p>
                    </div>
                  </div>
                )}
              </div>

              <AnimatePresence mode="wait">
                <motion.div
                  key={activeLesson?.lessonId}
                  initial={{ opacity: 0, y: 8 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0 }}
                  transition={transitions.fast}
                  className="flex flex-wrap items-center justify-between gap-3 p-5"
                >
                  <div className="min-w-0">
                    <h2 className="font-display text-heading-sm font-bold text-foreground">
                      {activeLesson?.title}
                    </h2>
                    <p className="tnum mt-1 flex items-center gap-1.5 text-caption text-muted-foreground">
                      <Clock className="size-3.5" />
                      {formatNumber(activeLesson?.durationInMinutes ?? 0)}{' '}
                      {locale === 'ar-EG' ? 'دقيقة' : 'min'}
                    </p>
                  </div>

                  {activeLesson?.lessonId && (
                    <Button
                      variant={activeLesson.isCompleted ? 'outline' : 'primary'}
                      loading={toggleLesson.isPending}
                      onClick={() =>
                        toggleLesson.mutate({
                          lessonId: activeLesson.lessonId!,
                          completed: Boolean(activeLesson.isCompleted),
                        })
                      }
                    >
                      <CheckCircle2 className="size-4" />
                      {activeLesson.isCompleted
                        ? t('courses.markIncomplete')
                        : t('courses.markComplete')}
                    </Button>
                  )}
                </motion.div>
              </AnimatePresence>
            </Card>
          </div>

          {/* ---------------------------- Curriculum -------------------------- */}
          <Card className="h-fit overflow-hidden lg:sticky lg:top-24">
            <div className="border-b border-border p-4">
              <h2 className="font-display text-body font-bold text-foreground">
                {t('courses.curriculum')}
              </h2>
            </div>

            <div className="max-h-[32rem] overflow-y-auto p-2">
              <Accordion type="multiple" defaultValue={sections.map((s) => s.id)}>
                {sections.map((section) => (
                  <AccordionItem key={section.id} value={section.id} className="border-0">
                    <AccordionTrigger className="rounded-lg px-3 py-2.5 text-body-sm hover:bg-muted hover:no-underline">
                      <span className="flex-1 text-start">{section.title}</span>
                      <span className="tnum me-2 text-caption text-muted-foreground">
                        {formatNumber(section.lessons.filter((l) => l.isCompleted).length)}/
                        {formatNumber(section.lessons.length)}
                      </span>
                    </AccordionTrigger>
                    <AccordionContent className="pb-1">
                      <ul className="space-y-0.5">
                        {section.lessons.map((lesson) => {
                          const active = lesson.lessonId === activeLesson?.lessonId
                          return (
                            <li key={lesson.lessonId}>
                              <button
                                type="button"
                                onClick={() => setActiveLessonId(lesson.lessonId ?? null)}
                                className={cn(
                                  'flex w-full items-center gap-2.5 rounded-lg px-3 py-2.5 text-start transition-colors',
                                  active ? 'bg-primary/10' : 'hover:bg-muted',
                                )}
                              >
                                <span
                                  className={cn(
                                    'shrink-0',
                                    lesson.isCompleted ? 'text-success-strong' : 'text-muted-foreground',
                                  )}
                                >
                                  {lesson.isCompleted ? (
                                    <CheckCircle2 className="size-4" />
                                  ) : active ? (
                                    <PlayCircle className="size-4 text-primary" />
                                  ) : (
                                    <Circle className="size-4" />
                                  )}
                                </span>
                                <span
                                  className={cn(
                                    'min-w-0 flex-1 truncate text-body-sm',
                                    active
                                      ? 'font-semibold text-foreground'
                                      : lesson.isCompleted
                                        ? 'text-muted-foreground'
                                        : 'text-foreground/90',
                                  )}
                                >
                                  {lesson.title}
                                </span>
                                <span className="tnum shrink-0 text-[0.6875rem] text-muted-foreground">
                                  {formatNumber(lesson.durationInMinutes ?? 0)}
                                </span>
                              </button>
                            </li>
                          )
                        })}
                      </ul>
                    </AccordionContent>
                  </AccordionItem>
                ))}
              </Accordion>
            </div>
          </Card>
        </div>
      )}
    </>
  )
}

/** Normalises YouTube/Vimeo watch links into their embeddable form. */
function toEmbedUrl(url: string): string {
  const youtube = url.match(/(?:youtube\.com\/watch\?v=|youtu\.be\/)([\w-]{11})/)
  if (youtube) return `https://www.youtube.com/embed/${youtube[1]}`
  const vimeo = url.match(/vimeo\.com\/(\d+)/)
  if (vimeo) return `https://player.vimeo.com/video/${vimeo[1]}`
  return url
}
