import { api } from '@/api/client'
import { params, type PageQuery } from '@/api/endpoints/common'
import type {
  AcceptInvitationCommand,
  AddTenantDomainCommand,
  AuditLogDto,
  ChangeMemberRoleCommand,
  ChangeMemberStatusCommand,
  CreateWebsiteSectionCommand,
  InviteTeamMemberCommand,
  MediaUploadResult,
  Paginated,
  PermissionCatalogDto,
  TenantBrandingDto,
  TenantDomainDto,
  TenantDto,
  TenantInvitationDto,
  TenantMemberDto,
  TenantSettingsDto,
  TenantWebsiteDto,
  UpdateMemberPermissionsCommand,
  UpdateTenantBrandingCommand,
  UpdateTenantCommand,
  UpdateTenantDomainCommand,
  UpdateTenantSettingsCommand,
  UpdateTenantWebsiteCommand,
  UpdateWebsiteSectionCommand,
  WebsiteSectionDto,
} from '@/api/schema'

export interface MemberQuery extends PageQuery {
  Role?: 'Owner' | 'Admin' | 'Assistant' | 'Moderator' | 'ContentManager'
  Status?: 'Invited' | 'Active' | 'Inactive' | 'Declined' | 'Removed'
  AssistantsOnly?: boolean
}

function fileForm(file: File): FormData {
  const form = new FormData()
  form.append('file', file)
  return form
}

/** Assistants and team members are the same resource under two routes. */
export const assistantsApi = {
  list: (query: MemberQuery = {}) =>
    api.get<Paginated<TenantMemberDto>>('/tenant/assistants', params(query)),
  details: (id: string) => api.get<TenantMemberDto>(`/tenant/assistants/${id}`),
  invite: (body: InviteTeamMemberCommand) =>
    api.post<TenantInvitationDto>('/tenant/assistants/invite', body),
  changeRole: (id: string, body: Omit<ChangeMemberRoleCommand, 'memberId'>) =>
    api.put<TenantMemberDto>(`/tenant/assistants/${id}`, { ...body, memberId: id }),
  setPermissions: (id: string, permissions: string[]) =>
    api.put<TenantMemberDto>(`/tenant/assistants/${id}/permissions`, {
      memberId: id,
      permissions,
    } satisfies UpdateMemberPermissionsCommand),
  setStatus: (id: string, isActive: boolean) =>
    api.put<TenantMemberDto>(`/tenant/assistants/${id}/status`, {
      memberId: id,
      isActive,
    } satisfies ChangeMemberStatusCommand),
  remove: (id: string) => api.delete<void>(`/tenant/assistants/${id}`),
  permissionCatalog: () => api.get<PermissionCatalogDto>('/tenant/assistants/permissions/catalog'),
  acceptInvitation: (body: AcceptInvitationCommand) =>
    api.post<TenantMemberDto>('/assistants/invitations/accept', body),
}

export const teamApi = {
  list: (query: MemberQuery = {}) =>
    api.get<Paginated<TenantMemberDto>>('/tenant/team', params(query)),
  details: (id: string) => api.get<TenantMemberDto>(`/tenant/team/${id}`),
  invite: (body: InviteTeamMemberCommand) =>
    api.post<TenantInvitationDto>('/tenant/team/invite', body),
  changeRole: (id: string, body: Omit<ChangeMemberRoleCommand, 'memberId'>) =>
    api.put<TenantMemberDto>(`/tenant/team/${id}/role`, { ...body, memberId: id }),
  setStatus: (id: string, isActive: boolean) =>
    api.put<TenantMemberDto>(`/tenant/team/${id}/status`, { memberId: id, isActive }),
  remove: (id: string) => api.delete<void>(`/tenant/team/${id}`),
  acceptInvitation: (body: AcceptInvitationCommand) =>
    api.post<TenantMemberDto>('/team/invitations/accept', body),
  declineInvitation: (token: string) => api.post<void>('/team/invitations/decline', { token }),
}

export const tenantApi = {
  get: () => api.get<TenantDto>('/tenant'),
  update: (body: UpdateTenantCommand) => api.put<TenantDto>('/tenant', body),
  settings: () => api.get<TenantSettingsDto>('/tenant/settings'),
  updateSettings: (body: UpdateTenantSettingsCommand) =>
    api.put<TenantSettingsDto>('/tenant/settings', body),
  branding: () => api.get<TenantBrandingDto>('/tenant/branding'),
  updateBranding: (body: UpdateTenantBrandingCommand) =>
    api.put<TenantBrandingDto>('/tenant/branding', body),
  uploadLogo: (file: File) => api.upload<MediaUploadResult>('/tenant/logo', fileForm(file)),
  uploadCover: (file: File) => api.upload<MediaUploadResult>('/tenant/cover', fileForm(file)),
  auditLogs: (query: PageQuery & { Action?: string; From?: string; To?: string } = {}) =>
    api.get<Paginated<AuditLogDto>>('/tenant/audit-logs', params(query)),

  domains: () => api.get<TenantDomainDto[]>('/tenant/domains'),
  addDomain: (body: AddTenantDomainCommand) => api.post<TenantDomainDto>('/tenant/domains', body),
  updateDomain: (id: string, body: UpdateTenantDomainCommand) =>
    api.put<TenantDomainDto>(`/tenant/domains/${id}`, { ...body, domainId: id }),
  removeDomain: (id: string) => api.delete<void>(`/tenant/domains/${id}`),
  verifyDomain: (id: string) => api.post<TenantDomainDto>(`/tenant/domains/${id}/verify`),

  website: () => api.get<TenantWebsiteDto>('/tenant/website'),
  updateWebsite: (body: UpdateTenantWebsiteCommand) =>
    api.put<TenantWebsiteDto>('/tenant/website', body),
  sections: () => api.get<WebsiteSectionDto[]>('/tenant/website/sections'),
  addSection: (body: CreateWebsiteSectionCommand) =>
    api.post<WebsiteSectionDto>('/tenant/website/sections', body),
  updateSection: (id: string, body: UpdateWebsiteSectionCommand) =>
    api.put<WebsiteSectionDto>(`/tenant/website/sections/${id}`, { ...body, sectionId: id }),
  removeSection: (id: string) => api.delete<void>(`/tenant/website/sections/${id}`),
  reorderSections: (sectionIds: string[]) =>
    api.post<WebsiteSectionDto[]>('/tenant/website/sections/reorder', { sectionIds }),
}
