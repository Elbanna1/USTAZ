import * as React from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { ArrowLeft, Download, Paperclip, Search, Send, X } from 'lucide-react'
import { messagesApi } from '@/api/endpoints/platform'
import { API_BASE_URL } from '@/api/config'
import type { ConversationDto, MessageDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useAuth } from '@/hooks/use-auth'
import { useToast } from '@/hooks/use-toast'
import { useDebounced } from '@/hooks/use-ui'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { avatarTraitsFor, imageUrl } from '@/lib/identity'
import { transitions } from '@/lib/motion'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { Button } from '@/components/ui/button'
import { Input, Textarea } from '@/components/ui/input'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { RowSkeleton } from '@/components/ui/skeleton'

const MESSAGE_PAGE_SIZE = 50

export default function MessagesPage() {
  const { t, locale, formatTimeAgo } = useI18n()
  usePageTitle(t('messages.title'))

  const { user } = useAuth()
  const { toast } = useToast()
  const queryClient = useQueryClient()

  const [search, setSearch] = React.useState('')
  const [activeId, setActiveId] = React.useState<string | null>(null)
  const debouncedSearch = useDebounced(search, 300)

  const conversationsQuery = React.useMemo(
    () => ({ PageSize: 50, Search: debouncedSearch.trim() || undefined }),
    [debouncedSearch],
  )

  const conversations = useQuery({
    queryKey: qk.messages.conversations(conversationsQuery),
    queryFn: () => messagesApi.conversations(conversationsQuery),
    placeholderData: keepPreviousData,
    refetchInterval: 60_000,
  })

  const list = conversations.data?.items ?? []

  /* Open the first thread on a wide screen so the pane is never empty. */
  React.useEffect(() => {
    if (!activeId && list.length > 0 && window.matchMedia('(min-width: 1024px)').matches) {
      setActiveId(list[0].id ?? null)
    }
  }, [list, activeId])

  const active = list.find((c) => c.id === activeId) ?? null

  return (
    <>
      <DashboardHeader
        title={t('messages.title')}
        description={
          locale === 'ar-EG'
            ? 'محادثاتك مع المدرّسين والطلاب وأولياء الأمور.'
            : 'Your conversations with teachers, students, and parents.'
        }
      />

      <div className="grid gap-4 lg:grid-cols-[22rem,1fr]">
        {/* ------------------------- Conversation list ------------------------- */}
        <Card
          className={cn(
            'flex max-h-[calc(100dvh-16rem)] flex-col overflow-hidden',
            activeId && 'hidden lg:flex',
          )}
        >
          <div className="border-b border-border p-3">
            <div className="relative">
              <Search className="pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder={t('common.search')}
                className="ps-9"
                aria-label={t('common.search')}
              />
            </div>
          </div>

          <div className="flex-1 overflow-y-auto p-2">
            {conversations.isPending ? (
              <RowSkeleton count={5} />
            ) : conversations.isError ? (
              <QueryError
                error={conversations.error}
                onRetry={() => conversations.refetch()}
                compact
              />
            ) : list.length === 0 ? (
              <EmptyState
                spot="messages"
                compact
                title={t('messages.noConversations')}
                description={t('messages.noConversationsHint')}
              />
            ) : (
              <div className="space-y-1">
                {list.map((conversation) => (
                  <ConversationRow
                    key={conversation.id}
                    conversation={conversation}
                    currentUserId={user?.id}
                    active={conversation.id === activeId}
                    onSelect={() => setActiveId(conversation.id ?? null)}
                    formatTimeAgo={formatTimeAgo}
                  />
                ))}
              </div>
            )}
          </div>
        </Card>

        {/* ----------------------------- Thread ------------------------------- */}
        <Card
          className={cn(
            'flex max-h-[calc(100dvh-16rem)] min-h-[28rem] flex-col overflow-hidden',
            !activeId && 'hidden lg:flex',
          )}
        >
          {active ? (
            <Thread
              conversation={active}
              onBack={() => setActiveId(null)}
              onSent={() => {
                queryClient.invalidateQueries({ queryKey: ['messages'] })
              }}
              onError={() => toast({ title: t('states.errorTitle'), variant: 'error' })}
            />
          ) : (
            <div className="flex flex-1 items-center justify-center p-8">
              <EmptyState
                spot="messages"
                compact
                title={t('messages.selectConversation')}
                className="border-0 bg-transparent"
              />
            </div>
          )}
        </Card>
      </div>
    </>
  )
}

/* -------------------------------------------------------------------------- */
/*                             Conversation row                               */
/* -------------------------------------------------------------------------- */

function otherParticipants(conversation: ConversationDto, currentUserId?: string) {
  return (conversation.participants ?? []).filter((p) => p.userId !== currentUserId)
}

function conversationTitle(conversation: ConversationDto, currentUserId?: string): string {
  if (conversation.subject?.trim()) return conversation.subject
  if (conversation.groupName?.trim()) return conversation.groupName
  const others = otherParticipants(conversation, currentUserId)
  return others.map((p) => p.displayName).filter(Boolean).join('، ') || '—'
}

function ConversationRow({
  conversation,
  currentUserId,
  active,
  onSelect,
  formatTimeAgo,
}: {
  conversation: ConversationDto
  currentUserId?: string
  active: boolean
  onSelect: () => void
  formatTimeAgo: (value: string) => string
}) {
  const title = conversationTitle(conversation, currentUserId)
  const other = otherParticipants(conversation, currentUserId)[0]
  const unread = conversation.unreadCount ?? 0

  return (
    <button
      type="button"
      onClick={onSelect}
      aria-current={active ? 'true' : undefined}
      className={cn(
        'relative flex w-full items-start gap-3 rounded-xl p-3 text-start transition-colors',
        active ? 'bg-primary/10' : 'hover:bg-muted',
      )}
    >
      <Avatar
        traits={avatarTraitsFor(other?.userId ?? conversation.id ?? '')}
        seed={other?.userId ?? conversation.id ?? ''}
        name={title}
        src={imageUrl(other?.profileImageUrl)}
        size="md"
      />
      <span className="min-w-0 flex-1">
        <span className="flex items-baseline gap-2">
          <span
            className={cn(
              'block flex-1 truncate font-display text-body-sm',
              unread ? 'font-bold text-foreground' : 'font-semibold text-foreground/90',
            )}
          >
            {title}
          </span>
          {conversation.lastMessageAt && (
            <span className="shrink-0 text-[0.625rem] text-muted-foreground">
              {formatTimeAgo(conversation.lastMessageAt)}
            </span>
          )}
        </span>
        <span className="mt-1 flex items-center gap-2">
          <span className="line-clamp-2-rtl flex-1 text-caption text-muted-foreground">
            {conversation.lastMessagePreview || '—'}
          </span>
          {unread > 0 && (
            <Badge variant="solid" size="sm" className="tnum shrink-0">
              {unread}
            </Badge>
          )}
        </span>
      </span>
    </button>
  )
}

/* -------------------------------------------------------------------------- */
/*                                   Thread                                   */
/* -------------------------------------------------------------------------- */

function Thread({
  conversation,
  onBack,
  onSent,
  onError,
}: {
  conversation: ConversationDto
  onBack: () => void
  onSent: () => void
  onError: () => void
}) {
  const { t, locale, formatDateTime } = useI18n()
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const conversationId = conversation.id!
  const scrollRef = React.useRef<HTMLDivElement>(null)
  const [draft, setDraft] = React.useState('')
  const [file, setFile] = React.useState<File | null>(null)
  const fileInputRef = React.useRef<HTMLInputElement>(null)

  const threadQuery = React.useMemo(() => ({ PageSize: MESSAGE_PAGE_SIZE }), [])
  const messages = useQuery({
    queryKey: qk.messages.thread(conversationId, threadQuery),
    queryFn: () => messagesApi.messages(conversationId, threadQuery),
    refetchInterval: 30_000,
  })

  /** The API returns newest-first; a chat reads oldest-first. */
  const ordered = React.useMemo(
    () =>
      [...(messages.data?.items ?? [])].sort((a, b) =>
        String(a.sentAt ?? '').localeCompare(String(b.sentAt ?? '')),
      ),
    [messages.data],
  )

  React.useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' })
  }, [ordered.length])

  /* Mark inbound messages read once they are on screen. */
  React.useEffect(() => {
    const unreadInbound = ordered.filter((m) => !m.isMine && m.id)
    if (!unreadInbound.length || !conversation.unreadCount) return
    Promise.all(unreadInbound.slice(-10).map((m) => messagesApi.markRead(m.id!).catch(() => undefined)))
      .then(() => {
        queryClient.invalidateQueries({ queryKey: qk.messages.unreadCount })
        queryClient.invalidateQueries({ queryKey: ['messages', 'conversations'] })
      })
      .catch(() => undefined)
    // Only re-run when the thread changes, not on every poll.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [conversationId, ordered.length])

  const send = useMutation({
    mutationFn: async () => {
      if (file) return messagesApi.sendAttachment(conversationId, file, draft.trim() || undefined)
      return messagesApi.send(conversationId, draft.trim())
    },
    onSuccess: () => {
      setDraft('')
      setFile(null)
      if (fileInputRef.current) fileInputRef.current.value = ''
      queryClient.invalidateQueries({ queryKey: ['messages'] })
      onSent()
    },
    onError,
  })

  const canSend = (draft.trim().length > 0 || file !== null) && !send.isPending
  const title = conversationTitle(conversation, user?.id)

  return (
    <>
      <header className="flex items-center gap-3 border-b border-border p-3">
        <Button variant="ghost" size="icon" className="lg:hidden" onClick={onBack} aria-label={t('common.back')}>
          <ArrowLeft className="size-5 flip-rtl" />
        </Button>
        <div className="min-w-0 flex-1">
          <p className="truncate font-display text-body-sm font-bold text-foreground">{title}</p>
          {conversation.groupName && (
            <p className="truncate text-caption text-muted-foreground">{conversation.groupName}</p>
          )}
        </div>
        {conversation.typeName && (
          <Badge variant="outline" size="sm">
            {conversation.typeName}
          </Badge>
        )}
      </header>

      <div ref={scrollRef} className="flex-1 space-y-3 overflow-y-auto p-4">
        {messages.isPending ? (
          <RowSkeleton count={4} />
        ) : messages.isError ? (
          <QueryError error={messages.error} onRetry={() => messages.refetch()} compact />
        ) : ordered.length === 0 ? (
          <div className="flex h-full items-center justify-center">
            <p className="text-body-sm text-muted-foreground">
              {locale === 'ar-EG' ? 'ابدأ المحادثة برسالة.' : 'Start the conversation.'}
            </p>
          </div>
        ) : (
          <AnimatePresence initial={false}>
            {ordered.map((message) => (
              <MessageBubble key={message.id} message={message} formatDateTime={formatDateTime} />
            ))}
          </AnimatePresence>
        )}
      </div>

      <footer className="border-t border-border p-3">
        {file && (
          <motion.div
            initial={{ opacity: 0, y: 6 }}
            animate={{ opacity: 1, y: 0 }}
            className="mb-2 flex items-center gap-2 rounded-lg border border-border bg-muted/40 px-3 py-2 text-caption"
          >
            <Paperclip className="size-3.5 shrink-0 text-muted-foreground" />
            <span className="min-w-0 flex-1 truncate">{file.name}</span>
            <button
              type="button"
              onClick={() => {
                setFile(null)
                if (fileInputRef.current) fileInputRef.current.value = ''
              }}
              aria-label={t('common.remove')}
              className="rounded p-0.5 text-muted-foreground hover:text-destructive-strong"
            >
              <X className="size-3.5" />
            </button>
          </motion.div>
        )}

        <form
          onSubmit={(e) => {
            e.preventDefault()
            if (canSend) send.mutate()
          }}
          className="flex items-end gap-2"
        >
          <input
            ref={fileInputRef}
            type="file"
            className="hidden"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          />
          <Button
            type="button"
            variant="outline"
            size="icon"
            onClick={() => fileInputRef.current?.click()}
            aria-label={t('homework.attachFile')}
          >
            <Paperclip className="size-4" />
          </Button>

          <Textarea
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault()
                if (canSend) send.mutate()
              }
            }}
            placeholder={t('messages.typeMessage')}
            rows={1}
            className="min-h-[2.75rem] flex-1 resize-none py-3"
            aria-label={t('messages.typeMessage')}
          />

          <Button type="submit" size="icon" disabled={!canSend} loading={send.isPending}>
            <Send className="size-4 flip-rtl" />
            <span className="sr-only">{t('messages.send')}</span>
          </Button>
        </form>
      </footer>
    </>
  )
}

function MessageBubble({
  message,
  formatDateTime,
}: {
  message: MessageDto
  formatDateTime: (value: string) => string
}) {
  const mine = Boolean(message.isMine)

  return (
    <motion.div
      layout
      initial={{ opacity: 0, y: 8, scale: 0.98 }}
      animate={{ opacity: 1, y: 0, scale: 1 }}
      transition={transitions.fast}
      className={cn('flex gap-2.5', mine ? 'flex-row-reverse' : 'flex-row')}
    >
      {!mine && (
        <Avatar
          traits={avatarTraitsFor(message.senderUserId ?? '')}
          seed={message.senderUserId ?? ''}
          name={message.senderName ?? ''}
          src={imageUrl(message.senderImageUrl)}
          size="xs"
          className="mt-1 shrink-0"
        />
      )}

      <div className={cn('max-w-[min(32rem,80%)] space-y-1', mine && 'items-end')}>
        {!mine && message.senderName && (
          <p className="px-1 text-[0.6875rem] font-semibold text-muted-foreground">
            {message.senderName}
          </p>
        )}

        <div
          className={cn(
            'rounded-2xl px-3.5 py-2.5 text-body-sm leading-relaxed',
            mine
              ? 'rounded-ee-sm bg-primary text-primary-foreground'
              : 'rounded-es-sm bg-muted text-foreground',
          )}
        >
          {message.body && <p className="whitespace-pre-wrap break-words">{message.body}</p>}

          {message.attachments?.map((attachment) => (
            <a
              key={attachment.id}
              href={`${API_BASE_URL}/messages/attachments/${attachment.id}`}
              target="_blank"
              rel="noopener noreferrer"
              className={cn(
                'mt-2 flex items-center gap-2 rounded-lg border px-2.5 py-2 text-caption transition-colors',
                mine
                  ? 'border-primary-foreground/25 hover:bg-primary-foreground/10'
                  : 'border-border bg-card hover:bg-muted',
              )}
            >
              <Download className="size-3.5 shrink-0" />
              <span className="min-w-0 flex-1 truncate">{attachment.fileName}</span>
            </a>
          ))}
        </div>

        {message.sentAt && (
          <p
            className={cn(
              'px-1 text-[0.625rem] text-muted-foreground',
              mine ? 'text-end' : 'text-start',
            )}
          >
            {formatDateTime(message.sentAt)}
          </p>
        )}
      </div>
    </motion.div>
  )
}
