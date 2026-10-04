import type { City, Grade, Stage, Subject, SubjectKey } from '@/types'

/**
 * Subject accents.
 *
 * Every subject tint is drawn from two narrow bands anchored on the brand
 * itself — the pine-teal primary (~172) and the gold accent (~38) — so covers,
 * tiles and icons read as one family rather than a rainbow. Sciences sit in the
 * cool band, languages and humanities in the warm one. Nothing lands in the
 * violet/indigo range, which belongs to no part of this identity.
 *
 * Counts are never stored here; they come from the API.
 */
export const subjects: Subject[] = [
  { key: 'physics', name: 'الفيزياء', nameEn: 'Physics', hue: 196 },
  { key: 'math', name: 'الرياضيات', nameEn: 'Mathematics', hue: 205 },
  { key: 'chemistry', name: 'الكيمياء', nameEn: 'Chemistry', hue: 162 },
  { key: 'biology', name: 'الأحياء', nameEn: 'Biology', hue: 146 },
  { key: 'science', name: 'العلوم', nameEn: 'Science', hue: 178 },
  { key: 'arabic', name: 'اللغة العربية', nameEn: 'Arabic', hue: 38 },
  { key: 'english', name: 'اللغة الإنجليزية', nameEn: 'English', hue: 18 },
  { key: 'french', name: 'اللغة الفرنسية', nameEn: 'French', hue: 46 },
  { key: 'history', name: 'التاريخ', nameEn: 'History', hue: 26 },
  { key: 'geology', name: 'الجيولوجيا', nameEn: 'Geology', hue: 30 },
]

export const subjectByKey = (key: SubjectKey): Subject =>
  subjects.find((s) => s.key === key) ?? subjects[0]

export const subjectName = (key: SubjectKey): string => subjectByKey(key).name

export const stages: Stage[] = [
  { key: 'primary', name: 'المرحلة الابتدائية', grades: ['p4', 'p5', 'p6'] },
  { key: 'preparatory', name: 'المرحلة الإعدادية', grades: ['prep1', 'prep2', 'prep3'] },
  { key: 'secondary', name: 'المرحلة الثانوية', grades: ['sec1', 'sec2', 'sec3'] },
]

export const grades: Grade[] = [
  { key: 'p4', name: 'الصف الرابع الابتدائي', shortName: '٤ ابتدائي', stage: 'primary' },
  { key: 'p5', name: 'الصف الخامس الابتدائي', shortName: '٥ ابتدائي', stage: 'primary' },
  { key: 'p6', name: 'الصف السادس الابتدائي', shortName: '٦ ابتدائي', stage: 'primary' },
  { key: 'prep1', name: 'الصف الأول الإعدادي', shortName: '١ إعدادي', stage: 'preparatory' },
  { key: 'prep2', name: 'الصف الثاني الإعدادي', shortName: '٢ إعدادي', stage: 'preparatory' },
  { key: 'prep3', name: 'الصف الثالث الإعدادي', shortName: '٣ إعدادي', stage: 'preparatory' },
  { key: 'sec1', name: 'الصف الأول الثانوي', shortName: '١ ثانوي', stage: 'secondary' },
  { key: 'sec2', name: 'الصف الثاني الثانوي', shortName: '٢ ثانوي', stage: 'secondary' },
  { key: 'sec3', name: 'الصف الثالث الثانوي', shortName: '٣ ثانوي', stage: 'secondary' },
]

export const gradeByKey = (key: string): Grade =>
  grades.find((g) => g.key === key) ?? grades[grades.length - 1]

export const gradeName = (key: string): string => gradeByKey(key).name

export const gradeShortName = (key: string): string => gradeByKey(key).shortName

export const cities: City[] = [
  { key: 'cairo', name: 'القاهرة', governorate: 'القاهرة' },
  { key: 'giza', name: 'الجيزة', governorate: 'الجيزة' },
  { key: 'alexandria', name: 'الإسكندرية', governorate: 'الإسكندرية' },
  { key: 'mansoura', name: 'المنصورة', governorate: 'الدقهلية' },
  { key: 'tanta', name: 'طنطا', governorate: 'الغربية' },
  { key: 'zagazig', name: 'الزقازيق', governorate: 'الشرقية' },
  { key: 'assiut', name: 'أسيوط', governorate: 'أسيوط' },
  { key: 'portsaid', name: 'بورسعيد', governorate: 'بورسعيد' },
  { key: 'minya', name: 'المنيا', governorate: 'المنيا' },
  { key: 'fayoum', name: 'الفيوم', governorate: 'الفيوم' },
]

export const cityByKey = (key: string): City =>
  cities.find((c) => c.key === key) ?? cities[0]

export const cityName = (key: string): string => cityByKey(key).name
