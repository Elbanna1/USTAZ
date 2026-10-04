import { Link } from 'react-router-dom'
import { Heart } from 'lucide-react'
import { usePageTitle } from '@/hooks/use-page-title'
import { useFavoriteCourses, useFavoriteTeachers } from '@/hooks/use-favorites'
import { useI18n } from '@/i18n'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { CardGridSkeleton } from '@/components/ui/skeleton'
import { TeacherCard } from '@/components/teachers/TeacherCard'
import { CourseCard } from '@/components/courses/CourseCard'
import { Button } from '@/components/ui/button'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'

export default function StudentFavoritesPage() {
  const { t, locale, formatNumber } = useI18n()
  usePageTitle(t('dashboard.favorites'))

  const teachers = useFavoriteTeachers()
  const courses = useFavoriteCourses()

  return (
    <>
      <DashboardHeader
        title={t('dashboard.favorites')}
        description={
          locale === 'ar-EG'
            ? 'المدرّسين والكورسات اللي حفظتهم عشان ترجعلهم بعدين.'
            : 'Teachers and courses you saved to come back to.'
        }
        actions={
          <Button asChild variant="outline" size="sm">
            <Link to="/teachers">{t('nav.teachers')}</Link>
          </Button>
        }
      />

      <Tabs defaultValue="teachers">
        <TabsList>
          <TabsTrigger value="teachers">
            <Heart className="size-4" />
            {t('common.teachers')}
            {teachers.teachers.length > 0 && (
              <span className="tnum ms-1.5 rounded-full bg-muted px-1.5 text-[0.6875rem]">
                {formatNumber(teachers.teachers.length)}
              </span>
            )}
          </TabsTrigger>
          <TabsTrigger value="courses">
            <Heart className="size-4" />
            {t('common.courses')}
            {courses.courses.length > 0 && (
              <span className="tnum ms-1.5 rounded-full bg-muted px-1.5 text-[0.6875rem]">
                {formatNumber(courses.courses.length)}
              </span>
            )}
          </TabsTrigger>
        </TabsList>

        <TabsContent value="teachers" className="mt-6">
          {teachers.isLoading ? (
            <CardGridSkeleton count={3} />
          ) : teachers.query.isError ? (
            <QueryError error={teachers.query.error} onRetry={() => teachers.query.refetch()} />
          ) : teachers.teachers.length === 0 ? (
            <EmptyState
              spot="bookmark"
              title={locale === 'ar-EG' ? 'مفيش مدرّسين محفوظين' : 'No saved teachers'}
              description={
                locale === 'ar-EG'
                  ? 'اضغط على القلب في كارت أي مدرّس عشان تحفظه هنا.'
                  : 'Tap the heart on any teacher card to save them here.'
              }
              action={{ label: t('nav.teachers'), href: '/teachers' }}
            />
          ) : (
            <RevealGroup className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
              {teachers.teachers.map((teacher) => (
                <RevealItem key={teacher.id} className="h-full">
                  <TeacherCard teacher={teacher} />
                </RevealItem>
              ))}
            </RevealGroup>
          )}
        </TabsContent>

        <TabsContent value="courses" className="mt-6">
          {courses.isLoading ? (
            <CardGridSkeleton count={3} />
          ) : courses.query.isError ? (
            <QueryError error={courses.query.error} onRetry={() => courses.query.refetch()} />
          ) : courses.courses.length === 0 ? (
            <EmptyState
              spot="bookmark"
              title={locale === 'ar-EG' ? 'مفيش كورسات محفوظة' : 'No saved courses'}
              description={
                locale === 'ar-EG'
                  ? 'اضغط على القلب في كارت أي كورس عشان تحفظه هنا.'
                  : 'Tap the heart on any course card to save it here.'
              }
              action={{ label: t('nav.courses'), href: '/courses' }}
            />
          ) : (
            <RevealGroup className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
              {courses.courses.map((course) => (
                <RevealItem key={course.id} className="h-full">
                  <CourseCard course={course} />
                </RevealItem>
              ))}
            </RevealGroup>
          )}
        </TabsContent>
      </Tabs>
    </>
  )
}
