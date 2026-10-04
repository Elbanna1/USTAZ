import { hashSeed, seededRandom } from '@/lib/utils'
import { resolveMediaUrl } from '@/api/config'
import type { AvatarTraits, CoverPattern, SubjectKey } from '@/types'
import type { SubjectDto } from '@/api/schema'

/**
 * The product draws its own portraits and cover art rather than shipping stock
 * photography. Real API records have no trait fields, so we derive a stable set
 * from the entity's id — the same teacher always gets the same portrait.
 */

const SKIN_TONES = ['#F2D0B4', '#E8BC97', '#D9A47C', '#C08659', '#9C6B44', '#7A4F31']
const HAIR_TONES = ['#1C1512', '#2E1F18', '#4A2F21', '#6B4630', '#8A5A3C', '#151212']
const GARMENT_TONES = ['#1A5149', '#2D5C7A', '#6B3F62', '#8A5A2B', '#3E4A6B', '#5C3A3A', '#2F6B52']
const ACCENT_TONES = ['#D7A94A', '#C9962F', '#E0B65C', '#B8853A']
const STYLES: AvatarTraits['style'][] = ['short', 'hijab', 'bald', 'wavy', 'beard', 'bun']

/** Stable pseudo-portrait traits for any entity that has an id. */
export function avatarTraitsFor(seed: string, hints: { style?: AvatarTraits['style'] } = {}): AvatarTraits {
  const pick = <T,>(list: readonly T[], index: number) =>
    list[Math.floor(seededRandom(seed, index) * list.length)]

  return {
    skin: pick(SKIN_TONES, 1),
    hair: pick(HAIR_TONES, 2),
    garment: pick(GARMENT_TONES, 3),
    accent: pick(ACCENT_TONES, 6),
    style: hints.style ?? pick(STYLES, 4),
    glasses: seededRandom(seed, 5) > 0.72,
  }
}

const PATTERNS: CoverPattern[] = ['arabesque', 'grid', 'waves', 'orbit', 'strata', 'bloom']

export function coverPatternFor(seed: string): CoverPattern {
  return PATTERNS[hashSeed(seed) % PATTERNS.length]
}

/* -------------------------------------------------------------------------- */
/*                              Subject mapping                               */
/* -------------------------------------------------------------------------- */

/** API subject slugs mapped onto the icon set the product already draws. */
const SUBJECT_SLUG_MAP: Record<string, SubjectKey> = {
  mathematics: 'math',
  math: 'math',
  maths: 'math',
  physics: 'physics',
  chemistry: 'chemistry',
  biology: 'biology',
  arabic: 'arabic',
  'arabic-language': 'arabic',
  english: 'english',
  'english-language': 'english',
  french: 'french',
  'french-language': 'french',
  history: 'history',
  geology: 'geology',
  science: 'science',
}

/** Falls back to a deterministic icon so an unknown subject never renders blank. */
export function subjectKeyFor(subject?: SubjectDto | null): SubjectKey {
  const slug = subject?.slug?.toLowerCase()
  if (slug && SUBJECT_SLUG_MAP[slug]) return SUBJECT_SLUG_MAP[slug]
  const english = subject?.nameEn?.toLowerCase().replace(/\s+/g, '-')
  if (english && SUBJECT_SLUG_MAP[english]) return SUBJECT_SLUG_MAP[english]
  return 'science'
}

/* -------------------------------------------------------------------------- */
/*                                   Images                                   */
/* -------------------------------------------------------------------------- */

/** A usable absolute image URL, or undefined so the caller draws its artwork. */
export function imageUrl(url?: string | null): string | undefined {
  const resolved = resolveMediaUrl(url)
  if (!resolved) return undefined
  // The API occasionally stores placeholder paths; treat those as "no image".
  if (/placeholder|default-avatar|no-image/i.test(resolved)) return undefined
  return resolved
}

/* -------------------------------------------------------------------------- */
/*                            Display helpers                                 */
/* -------------------------------------------------------------------------- */

/** Prefers a real name, falls back to the email local part, never renders blank. */
export function displayNameOf(entity: {
  displayName?: string | null
  firstName?: string | null
  lastName?: string | null
  email?: string | null
}): string {
  if (entity.displayName?.trim()) return entity.displayName.trim()
  const full = [entity.firstName, entity.lastName].filter(Boolean).join(' ').trim()
  if (full) return full
  if (entity.email) return entity.email.split('@')[0]
  return '—'
}
