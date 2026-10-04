import { useQuery } from '@tanstack/react-query'
import { taxonomyApi } from '@/api/endpoints/catalog'
import { qk } from '@/lib/query-client'
import { useI18n } from '@/i18n'
import type { GradeDto, SubjectDto } from '@/api/schema'

/** Reference data changes rarely — cache it for the session. */
const REFERENCE_OPTIONS = { staleTime: 30 * 60_000, gcTime: 60 * 60_000 } as const

export function useSubjects() {
  const { pick } = useI18n()
  const query = useQuery({
    queryKey: qk.taxonomy.subjects,
    queryFn: taxonomyApi.subjects,
    ...REFERENCE_OPTIONS,
  })

  return {
    ...query,
    subjects: query.data ?? [],
    label: (subject?: SubjectDto | null) => pick(subject?.nameAr, subject?.nameEn),
    byId: (id?: string | null) => query.data?.find((s) => s.id === id),
  }
}

export function useGrades() {
  const { pick } = useI18n()
  const query = useQuery({
    queryKey: qk.taxonomy.grades,
    queryFn: taxonomyApi.grades,
    ...REFERENCE_OPTIONS,
  })

  const grades = query.data ?? []

  /** Grades bucketed by stage, in the order the API sorted them. */
  const byStage = grades.reduce<Record<string, GradeDto[]>>((acc, grade) => {
    const key = grade.stage ?? 'Other'
    ;(acc[key] ??= []).push(grade)
    return acc
  }, {})

  return {
    ...query,
    grades,
    byStage,
    label: (grade?: GradeDto | null) => pick(grade?.nameAr, grade?.nameEn),
    byId: (id?: string | null) => grades.find((g) => g.id === id),
  }
}

export const STAGE_LABELS: Record<string, { ar: string; en: string }> = {
  Primary: { ar: 'ابتدائي', en: 'Primary' },
  Preparatory: { ar: 'إعدادي', en: 'Preparatory' },
  Secondary: { ar: 'ثانوي', en: 'Secondary' },
  Other: { ar: 'أخرى', en: 'Other' },
}
