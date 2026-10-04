import * as React from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { studentApi } from '@/api/endpoints/student'
import { useAuth } from '@/hooks/use-auth'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { useApiErrorToast } from '@/hooks/use-api-toast'

const FAVORITE_TEACHERS_KEY = ['student', 'favorites', 'teachers', 'ids'] as const
const FAVORITE_COURSES_KEY = ['student', 'favorites', 'courses', 'ids'] as const

/**
 * Favorites live on the server. The hook keeps a local id set for instant
 * heart toggles and reconciles against the API response.
 */
export function useFavoriteTeachers() {
  const { hasRole, isAuthenticated } = useAuth()
  const queryClient = useQueryClient()
  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const { locale } = useI18n()
  const enabled = isAuthenticated && hasRole('student')

  const query = useQuery({
    queryKey: FAVORITE_TEACHERS_KEY,
    queryFn: () => studentApi.favoriteTeachers({ PageSize: 100 }),
    enabled,
    staleTime: 120_000,
  })

  const savedIds = React.useMemo(
    () => new Set((query.data?.items ?? []).map((item) => item.id).filter(Boolean) as string[]),
    [query.data],
  )

  const mutation = useMutation({
    mutationFn: ({ id, next }: { id: string; next: boolean }) =>
      next ? studentApi.addFavoriteTeacher(id) : studentApi.removeFavoriteTeacher(id),
    onSuccess: (_result, { next }) => {
      queryClient.invalidateQueries({ queryKey: FAVORITE_TEACHERS_KEY })
      queryClient.invalidateQueries({ queryKey: ['student', 'favorites'] })
      toast({
        title: next
          ? locale === 'ar-EG'
            ? 'اتضاف للمفضّلة'
            : 'Added to favorites'
          : locale === 'ar-EG'
            ? 'اتشال من المفضّلة'
            : 'Removed from favorites',
        variant: 'success',
      })
    },
    onError: (error) => {
      showApiError(error)
    },
  })

  const toggle = React.useCallback(
    (id: string) => {
      if (!enabled) return false
      const next = !savedIds.has(id)
      mutation.mutate({ id, next })
      return next
    },
    [enabled, savedIds, mutation],
  )

  return {
    enabled,
    savedIds,
    teachers: query.data?.items ?? [],
    isLoading: query.isPending && enabled,
    isSaved: (id: string) => savedIds.has(id),
    toggle,
    isToggling: mutation.isPending,
    query,
  }
}

export function useFavoriteCourses() {
  const { hasRole, isAuthenticated } = useAuth()
  const queryClient = useQueryClient()
  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const { locale } = useI18n()
  const enabled = isAuthenticated && hasRole('student')

  const query = useQuery({
    queryKey: FAVORITE_COURSES_KEY,
    queryFn: () => studentApi.favoriteCourses({ PageSize: 100 }),
    enabled,
    staleTime: 120_000,
  })

  const savedIds = React.useMemo(
    () => new Set((query.data?.items ?? []).map((item) => item.id).filter(Boolean) as string[]),
    [query.data],
  )

  const mutation = useMutation({
    mutationFn: ({ id, next }: { id: string; next: boolean }) =>
      next ? studentApi.addFavoriteCourse(id) : studentApi.removeFavoriteCourse(id),
    onSuccess: (_result, { next }) => {
      queryClient.invalidateQueries({ queryKey: FAVORITE_COURSES_KEY })
      toast({
        title: next
          ? locale === 'ar-EG'
            ? 'اتضاف للمفضّلة'
            : 'Added to favorites'
          : locale === 'ar-EG'
            ? 'اتشال من المفضّلة'
            : 'Removed from favorites',
        variant: 'success',
      })
    },
    onError: (error) => {
      showApiError(error)
    },
  })

  return {
    enabled,
    savedIds,
    courses: query.data?.items ?? [],
    isLoading: query.isPending && enabled,
    isSaved: (id: string) => savedIds.has(id),
    toggle: (id: string) => {
      if (!enabled) return false
      const next = !savedIds.has(id)
      mutation.mutate({ id, next })
      return next
    },
    isToggling: mutation.isPending,
    query,
  }
}
