import { api } from '@/api/client'
import type {
  AuthenticationResponse,
  ChangePasswordCommand,
  CurrentUserDto,
  ForgotPasswordCommand,
  LoginCommand,
  RegisterParentCommand,
  RegisterStudentCommand,
  RegisterTeacherCommand,
  ResendVerificationCommand,
  ResetPasswordCommand,
  UpdateLanguagePreferenceCommand,
  VerifyEmailCommand,
} from '@/api/schema'

export const authApi = {
  login: (body: LoginCommand) =>
    api.post<AuthenticationResponse>('/auth/login', body, { anonymous: true }),

  registerStudent: (body: RegisterStudentCommand) =>
    api.post<AuthenticationResponse>('/auth/register/student', body, { anonymous: true }),

  registerTeacher: (body: RegisterTeacherCommand) =>
    api.post<AuthenticationResponse>('/auth/register/teacher', body, { anonymous: true }),

  /**
   * `childEmail` links the guardian to an existing student on sign-up. The
   * student still has to approve the link, so the account is created either
   * way — a wrong or unknown address is not a registration failure.
   */
  registerParent: (body: RegisterParentCommand) =>
    api.post<AuthenticationResponse>('/auth/register/parent', body, { anonymous: true }),

  logout: (refreshToken: string) => api.post<void>('/auth/logout', { refreshToken }),

  me: () => api.get<CurrentUserDto>('/auth/me'),

  setLanguage: (body: UpdateLanguagePreferenceCommand) =>
    api.put<void>('/auth/me/language', body),

  forgotPassword: (body: ForgotPasswordCommand) =>
    api.post<void>('/auth/forgot-password', body, { anonymous: true }),

  resetPassword: (body: ResetPasswordCommand) =>
    api.post<void>('/auth/reset-password', body, { anonymous: true }),

  changePassword: (body: ChangePasswordCommand) => api.post<void>('/auth/change-password', body),

  verifyEmail: (body: VerifyEmailCommand) =>
    api.post<void>('/auth/verify-email', body, { anonymous: true }),

  resendVerification: (body: ResendVerificationCommand) =>
    api.post<void>('/auth/resend-verification', body, { anonymous: true }),
}
