/**
 * Ustaz — domain model.
 * Every mock-data module and every component reads from these types, so swapping
 * `src/data/*` for a real API later is a data-layer change, not a UI rewrite.
 */

/* -------------------------------------------------------------------------- */
/*                                 Primitives                                  */
/* -------------------------------------------------------------------------- */

export type ID = string

/** Teaching delivery mode. */
export type TeachingMode = 'online' | 'offline' | 'hybrid'

/** Egyptian educational stages we cover. */
export type StageKey = 'primary' | 'preparatory' | 'secondary'

/** Weekday keys — Saturday-first, matching the Egyptian school week. */
export type DayKey = 'sat' | 'sun' | 'mon' | 'tue' | 'wed' | 'thu' | 'fri'

export type SubjectKey =
  | 'physics'
  | 'math'
  | 'chemistry'
  | 'biology'
  | 'arabic'
  | 'english'
  | 'french'
  | 'history'
  | 'geology'
  | 'science'

export interface Subject {
  key: SubjectKey
  name: string
  nameEn: string
  /** Hue that tints this subject's cover art, tiles and icons. See data/taxonomy. */
  hue: number
}

export interface Grade {
  key: string
  name: string
  shortName: string
  stage: StageKey
}

export interface Stage {
  key: StageKey
  name: string
  grades: string[]
}

export interface City {
  key: string
  name: string
  governorate: string
}

/* -------------------------------------------------------------------------- */
/*                                  Teachers                                   */
/* -------------------------------------------------------------------------- */

export interface SocialLinks {
  whatsapp?: string
  facebook?: string
  youtube?: string
  telegram?: string
  instagram?: string
  website?: string
}

export interface Achievement {
  id: ID
  title: string
  description: string
  year: string
  /** Icon key resolved by `components/common/AchievementIcon`. */
  icon: 'trophy' | 'star' | 'users' | 'award' | 'sparkles' | 'target'
}

export interface TeachingPhilosophyPoint {
  id: ID
  title: string
  description: string
}

export interface WeeklySlot {
  id: ID
  day: DayKey
  start: string
  end: string
  title: string
  groupSlug?: string
  mode: TeachingMode
  location?: string
}

export interface Teacher {
  id: ID
  slug: string
  name: string
  nameEn: string
  /** e.g. "مدرّس فيزياء" */
  title: string
  subjects: SubjectKey[]
  primarySubject: SubjectKey
  stages: StageKey[]
  grades: string[]
  city: string
  mode: TeachingMode
  yearsExperience: number
  studentsCount: number
  rating: number
  reviewsCount: number
  groupsCount: number
  coursesCount: number
  /** Short line shown on cards. */
  headline: string
  /** Long-form profile copy — rendered as paragraphs. */
  bio: string[]
  philosophy: TeachingPhilosophyPoint[]
  achievements: Achievement[]
  weeklySchedule: WeeklySlot[]
  social: SocialLinks
  phone: string
  email: string
  /** Deterministic seed for the generated portrait + cover artwork. */
  avatarSeed: string
  /** Portrait presentation traits — drives the generated illustration. */
  avatar: AvatarTraits
  coverPattern: CoverPattern
  featured: boolean
  verified: boolean
  joinedAt: string
  responseTimeMins: number
  startingPrice: number
  languages: string[]
  faq: FaqItem[]
}

export interface AvatarTraits {
  skin: string
  hair: string
  garment: string
  accent: string
  /** Headwear/hair silhouette. */
  style: 'short' | 'hijab' | 'bald' | 'wavy' | 'beard' | 'bun'
  glasses: boolean
}

export type CoverPattern = 'arabesque' | 'grid' | 'waves' | 'orbit' | 'strata' | 'bloom'

/* -------------------------------------------------------------------------- */
/*                                   Groups                                    */
/* -------------------------------------------------------------------------- */

export type GroupType = 'intensive' | 'regular' | 'revision' | 'private' | 'workshop'
export type GroupStatus = 'open' | 'almost-full' | 'full' | 'starting-soon'

export interface CurriculumUnit {
  id: ID
  order: number
  title: string
  description: string
  lessons: string[]
  durationWeeks: number
}

export interface Group {
  id: ID
  slug: string
  name: string
  teacherId: ID
  subject: SubjectKey
  grade: string
  stage: StageKey
  type: GroupType
  mode: TeachingMode
  city?: string
  location?: string
  days: DayKey[]
  startTime: string
  endTime: string
  sessionMinutes: number
  monthlyPrice: number
  originalPrice?: number
  discountId?: ID
  seatsTotal: number
  seatsTaken: number
  startDate: string
  description: string
  highlights: string[]
  curriculum: CurriculumUnit[]
  materials: string[]
  benefits: string[]
  whatsappGroupUrl: string
  rating: number
  reviewsCount: number
  featured: boolean
  status: GroupStatus
  coverPattern: CoverPattern
  faq: FaqItem[]
}

/* -------------------------------------------------------------------------- */
/*                                   Courses                                   */
/* -------------------------------------------------------------------------- */

export type CourseLevel = 'beginner' | 'intermediate' | 'advanced'
export type CourseCategory =
  | 'exam-prep'
  | 'full-term'
  | 'problem-solving'
  | 'foundations'
  | 'languages'
  | 'skills'

export interface CourseLesson {
  id: ID
  order: number
  title: string
  durationMins: number
  isPreview: boolean
}

export interface CourseChapter {
  id: ID
  order: number
  title: string
  lessons: CourseLesson[]
}

export interface Course {
  id: ID
  slug: string
  title: string
  subtitle: string
  teacherId: ID
  subject: SubjectKey
  grade: string
  category: CourseCategory
  level: CourseLevel
  price: number
  originalPrice?: number
  discountId?: ID
  totalHours: number
  lessonsCount: number
  studentsCount: number
  rating: number
  reviewsCount: number
  description: string
  outcomes: string[]
  requirements: string[]
  chapters: CourseChapter[]
  featured: boolean
  updatedAt: string
  language: string
  hasCertificate: boolean
  coverPattern: CoverPattern
}

/* -------------------------------------------------------------------------- */
/*                                  Reviews                                    */
/* -------------------------------------------------------------------------- */

export interface Review {
  id: ID
  teacherId: ID
  groupId?: ID
  courseId?: ID
  studentName: string
  studentGrade: string
  avatarSeed: string
  rating: number
  body: string
  date: string
  helpful: number
  verified: boolean
  /** Teacher-side moderation state, used by /teacher/reviews. */
  status: 'published' | 'pending' | 'hidden'
  reply?: string
}

/* -------------------------------------------------------------------------- */
/*                                  Schedule                                   */
/* -------------------------------------------------------------------------- */

export interface ScheduleSession {
  id: ID
  groupId: ID
  teacherId: ID
  subject: SubjectKey
  title: string
  /** ISO date — the concrete calendar day of this session. */
  date: string
  startTime: string
  endTime: string
  durationMins: number
  mode: TeachingMode
  location?: string
  joinUrl?: string
  grade: string
  status: 'upcoming' | 'live' | 'done' | 'cancelled'
}

/* -------------------------------------------------------------------------- */
/*                          Discounts & subscriptions                          */
/* -------------------------------------------------------------------------- */

export type DiscountKind = 'percentage' | 'fixed'
export type DiscountCampaign =
  | 'early-bird'
  | 'first-month'
  | 'multi-subject'
  | 'limited-time'
  | 'referral'

export interface Discount {
  id: ID
  code: string
  label: string
  description: string
  campaign: DiscountCampaign
  kind: DiscountKind
  /** Percentage (0-100) when kind is 'percentage', EGP when 'fixed'. */
  value: number
  /** Cap in EGP for percentage discounts. */
  maxDiscount?: number
  minSpend?: number
  expiresAt: string
  active: boolean
  usedCount: number
  usageLimit: number
  appliesTo: 'all' | 'groups' | 'courses'
  teacherId?: ID
}

export type SubscriptionStatus = 'active' | 'pending' | 'expired' | 'cancelled'

export interface Subscription {
  id: ID
  studentId: ID
  studentName: string
  teacherId: ID
  groupId?: ID
  courseId?: ID
  kind: 'group' | 'course'
  status: SubscriptionStatus
  startedAt: string
  renewsAt?: string
  pricePaid: number
  originalPrice: number
  discountCode?: string
  /** 0-100 — completion of the term/curriculum. */
  progress: number
  attendedSessions: number
  totalSessions: number
}

export interface PriceBreakdown {
  original: number
  discountAmount: number
  final: number
  percentOff: number
  discount?: Discount
}

/* -------------------------------------------------------------------------- */
/*                              Students & people                              */
/* -------------------------------------------------------------------------- */

export interface Student {
  id: ID
  name: string
  grade: string
  stage: StageKey
  city: string
  phone: string
  email: string
  avatarSeed: string
  joinedAt: string
  avatar: AvatarTraits
}

/** A student as seen from the teacher dashboard roster. */
export interface EnrolledStudent {
  id: ID
  name: string
  grade: string
  groupId: ID
  groupName: string
  phone: string
  avatarSeed: string
  joinedAt: string
  attendanceRate: number
  paymentStatus: 'paid' | 'due' | 'late'
  lastActive: string
  avatar: AvatarTraits
}

export interface Inquiry {
  id: ID
  studentName: string
  phone: string
  grade: string
  subject: SubjectKey
  message: string
  receivedAt: string
  status: 'new' | 'replied' | 'converted'
}

/* -------------------------------------------------------------------------- */
/*                                Notifications                                */
/* -------------------------------------------------------------------------- */

export type NotificationKind = 'class' | 'payment' | 'review' | 'system' | 'offer' | 'message'

export interface AppNotification {
  id: ID
  kind: NotificationKind
  title: string
  body: string
  date: string
  read: boolean
  href?: string
}

/* -------------------------------------------------------------------------- */
/*                                  Analytics                                  */
/* -------------------------------------------------------------------------- */

export interface SeriesPoint {
  label: string
  value: number
  secondary?: number
}

export interface TeacherAnalytics {
  totalStudents: number
  studentsDelta: number
  activeGroups: number
  groupsDelta: number
  monthlyRevenue: number
  revenueDelta: number
  averageRating: number
  ratingDelta: number
  upcomingClasses: number
  newInquiries: number
  studentGrowth: SeriesPoint[]
  revenue: SeriesPoint[]
  groupPerformance: SeriesPoint[]
  subscriptionTrend: SeriesPoint[]
  attendanceSplit: SeriesPoint[]
}

export interface StudentAnalytics {
  activeSubscriptions: number
  hoursThisMonth: number
  attendanceRate: number
  savedTeachers: number
  weeklyStudy: SeriesPoint[]
  subjectProgress: SeriesPoint[]
}

/* -------------------------------------------------------------------------- */
/*                               Content blocks                                */
/* -------------------------------------------------------------------------- */

export interface FaqItem {
  id: ID
  question: string
  answer: string
  category?: string
}

export interface PricingPlan {
  id: ID
  name: string
  tagline: string
  monthlyPrice: number
  yearlyPrice: number
  audience: 'teacher' | 'center'
  features: string[]
  limits: string[]
  popular: boolean
  cta: string
}

export interface PlatformStat {
  id: ID
  label: string
  value: number
  suffix?: string
  prefix?: string
  description: string
  icon: 'users' | 'teacher' | 'book' | 'star'
}

/* -------------------------------------------------------------------------- */
/*                               Filter / UI state                             */
/* -------------------------------------------------------------------------- */

export type TeacherSort = 'featured' | 'rating' | 'experience' | 'newest' | 'students'

export interface TeacherFilters {
  query: string
  subjects: SubjectKey[]
  grades: string[]
  cities: string[]
  modes: TeachingMode[]
  minRating: number
  sort: TeacherSort
}

export type GroupSort = 'featured' | 'price-asc' | 'price-desc' | 'rating' | 'seats'

export interface GroupFilters {
  query: string
  subjects: SubjectKey[]
  grades: string[]
  modes: TeachingMode[]
  types: GroupType[]
  maxPrice: number
  sort: GroupSort
}

export type CourseSort = 'featured' | 'price-asc' | 'price-desc' | 'rating' | 'students'

export interface CourseFilters {
  query: string
  subjects: SubjectKey[]
  categories: CourseCategory[]
  levels: CourseLevel[]
  maxPrice: number
  sort: CourseSort
}
