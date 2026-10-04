import type { FieldValues, Path, UseFormSetError } from 'react-hook-form'
import { isApiError } from '@/api/errors'

/**
 * Pushes server-side validation errors onto the matching form fields.
 * Returns the message that belongs at the top of the form, if any.
 */
export function applyServerErrors<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  fields: readonly Path<T>[],
): string | null {
  if (!isApiError(error)) return null

  let matchedAny = false
  for (const field of fields) {
    const message = error.fieldError(String(field))
    if (message) {
      setError(field, { type: 'server', message })
      matchedAny = true
    }
  }

  // Anything the form could not place still has to reach the user.
  return matchedAny && error.isValidation ? null : error.message
}

/** Egyptian mobile numbers, the way people actually type them. */
export const EG_MOBILE = /^(\+?20|0)?1[0125]\d{8}$/

/** At least 8 characters with a letter and a digit — the backend's own rule. */
export const STRONG_PASSWORD = /^(?=.*[A-Za-z])(?=.*\d).{8,}$/

/** Normalises a local Egyptian number to the 01XXXXXXXXX form the API expects. */
export function normalizeEgyptianMobile(value: string): string {
  const digits = value.replace(/\D/g, '')
  if (digits.startsWith('20')) return `0${digits.slice(2)}`
  if (digits.startsWith('1') && digits.length === 10) return `0${digits}`
  return digits
}
