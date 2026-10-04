import { Hero } from '@/components/home/Hero'
import {
  CoursesSection,
  FeaturedTeachers,
  FinalCta,
  GroupsSection,
  HomeFaq,
  HowItWorks,
  OffersStrip,
  PricingPreview,
  StatsBand,
  SubjectsSection,
  StudentReviewsSection,
  UpcomingClasses,
} from '@/components/home/sections'
import { usePageTitle } from '@/hooks/use-page-title'

export default function HomePage() {
  usePageTitle('أستاذ — كل اللي محتاجه عشان تبني اسمك كمدرّس')

  return (
    <>
      <Hero />
      <StatsBand />
      <SubjectsSection />
      <FeaturedTeachers />
      <GroupsSection />
      <UpcomingClasses />
      <CoursesSection />
      <StudentReviewsSection />
      <HowItWorks />
      <OffersStrip />
      <PricingPreview />
      <HomeFaq />
      <FinalCta />
    </>
  )
}
