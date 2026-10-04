import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { messagesApi, notificationsApi } from '@/api/endpoints/platform'
import { qk } from '@/lib/query-client'
import { useApiErrorToast } from '@/hooks/use-api-toast'
import { useAuth } from '@/hooks/use-auth'

/** Unread badge counts. Polled gently so a long-open tab stays current. */
export function useUnreadCounts() {
  const { isAuthenticated } = useAuth()

  const notifications = useQuery({
    queryKey: qk.notifications.unreadCount,
    queryFn: notificationsApi.unreadCount,
    enabled: isAuthenticated,
    refetchInterval: 90_000,
    staleTime: 45_000,
  })

  const messages = useQuery({
    queryKey: qk.messages.unreadCount,
    queryFn: messagesApi.unreadCount,
    enabled: isAuthenticated,
    refetchInterval: 90_000,
    staleTime: 45_000,
  })

  return {
    notifications: notifications.data?.count ?? 0,
    messages: messages.data?.totalUnread ?? 0,
    conversationsWithUnread: messages.data?.conversationsWithUnread ?? 0,
  }
}

/** Mutations shared by the notification centre and the notifications page. */
export function useNotificationActions() {
  const queryClient = useQueryClient()
  /**
   * Every caller renders these as a bare icon button, so a silent failure looks
   * exactly like a no-op. The toast lives here rather than in each screen.
   */
  const onError = useApiErrorToast()

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['notifications'] })
  }

  return {
    markRead: useMutation({
      mutationFn: (id: string) => notificationsApi.markRead(id),
      onSuccess: invalidate,
      onError,
    }),
    markAllRead: useMutation({
      mutationFn: () => notificationsApi.markAllRead(),
      onSuccess: invalidate,
      onError,
    }),
    remove: useMutation({
      mutationFn: (id: string) => notificationsApi.remove(id),
      onSuccess: invalidate,
      onError,
    }),
    clearRead: useMutation({
      mutationFn: () => notificationsApi.clearRead(),
      onSuccess: invalidate,
      onError,
    }),
  }
}
