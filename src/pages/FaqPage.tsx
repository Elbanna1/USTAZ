import * as React from 'react'
import { Link } from 'react-router-dom'
import { usePlatformFaq } from '@/data/content'
import { useI18n } from '@/i18n'
import { normalizeArabic, unique } from '@/lib/utils'
import { SITE } from '@/lib/constants'
import { usePageTitle } from '@/hooks/use-page-title'
import { useDebounced } from '@/hooks/use-ui'
import { PageHeader } from '@/components/common/PageHeader'
import { FilterChip, SearchBar } from '@/components/common/controls'
import { FaqAccordion } from '@/components/common/FaqAccordion'
import { EmptyState } from '@/components/common/states'
import { Reveal } from '@/components/common/Reveal'
import { Card } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { WhatsAppButton } from '@/components/whatsapp/WhatsAppButton'

export default function FaqPage() {
  const { locale } = useI18n()
  const isAr = locale === 'ar-EG'
  const platformFaq = usePlatformFaq()

  usePageTitle(
    isAr ? 'الأسئلة الشائعة' : 'FAQ',
    isAr
      ? 'كل اللي بيتسأل عن منصة أستاذ — للمدرّسين والطلاب وأولياء الأمور.'
      : 'Everything people ask about USTAZ — for teachers, students, and parents.',
  )

  const [query, setQuery] = React.useState('')
  // Filter by position, not by label: the labels change with the language.
  const [categoryIndex, setCategoryIndex] = React.useState(-1)
  const debounced = useDebounced(query, 200)

  const categories = React.useMemo(
    () => unique(platformFaq.map((f) => f.category ?? '')),
    [platformFaq],
  )
  const activeCategory = categories[categoryIndex]

  const results = React.useMemo(() => {
    const q = normalizeArabic(debounced)
    return platformFaq.filter((f) => {
      if (activeCategory && (f.category ?? '') !== activeCategory) return false
      if (!q) return true
      return normalizeArabic(f.question).includes(q) || normalizeArabic(f.answer).includes(q)
    })
  }, [platformFaq, debounced, activeCategory])

  return (
    <>
      <PageHeader
        eyebrow={isAr ? 'مركز المساعدة' : 'Help centre'}
        title={isAr ? 'الأسئلة الشائعة' : 'Frequently asked questions'}
        description={
          isAr
            ? 'جمّعنا هنا كل اللي بيتسأل عن المنصة — من المدرّسين والطلاب وأولياء الأمور.'
            : 'Everything teachers, students, and parents ask about the platform, in one place.'
        }
        crumbs={[
          { label: isAr ? 'الرئيسية' : 'Home', href: '/' },
          { label: isAr ? 'الأسئلة الشائعة' : 'FAQ' },
        ]}
      >
        <div className="space-y-5">
          <SearchBar
            value={query}
            onChange={setQuery}
            placeholder={isAr ? 'اكتب سؤالك أو كلمة منه…' : 'Type your question, or a word from it…'}
            size="lg"
            id="faq-search"
            className="max-w-2xl"
          />

          <div className="no-scrollbar flex gap-2 overflow-x-auto pb-1">
            <FilterChip
              label={isAr ? 'الكل' : 'All'}
              active={categoryIndex === -1}
              onClick={() => setCategoryIndex(-1)}
              count={platformFaq.length}
            />
            {categories.map((c, i) => (
              <FilterChip
                key={c}
                label={c}
                active={categoryIndex === i}
                onClick={() => setCategoryIndex(i)}
                count={platformFaq.filter((f) => (f.category ?? '') === c).length}
              />
            ))}
          </div>
        </div>
      </PageHeader>

      <section className="section-y !py-12">
        <div className="container-page">
          <div className="grid gap-10 lg:grid-cols-[1fr,20rem] lg:gap-14">
            <div className="min-w-0">
              <p className="text-body-sm text-muted-foreground">
                <span className="tnum font-display font-bold text-foreground">{results.length}</span>{' '}
                {isAr ? 'سؤال' : results.length === 1 ? 'question' : 'questions'}
                {debounced &&
                  (isAr ? (
                    <> مطابق لبحثك عن «{debounced}»</>
                  ) : (
                    <> matching “{debounced}”</>
                  ))}
              </p>

              <div className="mt-6">
                {results.length === 0 ? (
                  <EmptyState
                    spot="search"
                    title={isAr ? 'مفيش سؤال مطابق' : 'No matching question'}
                    description={
                      isAr
                        ? 'جرّب كلمة أبسط، أو ابعتلنا سؤالك مباشرة على واتساب وهنجاوبك.'
                        : 'Try a simpler word, or send us the question on WhatsApp and we will answer it.'
                    }
                    action={{
                      label: isAr ? 'امسح البحث' : 'Clear search',
                      onClick: () => {
                        setQuery('')
                        setCategoryIndex(-1)
                      },
                    }}
                    secondaryAction={{
                      label: isAr ? 'تواصل معنا' : 'Contact us',
                      href: '/contact',
                    }}
                  />
                ) : (
                  <FaqAccordion items={results} defaultOpen={results[0]?.id} />
                )}
              </div>
            </div>

            <aside className="space-y-5 lg:sticky lg:top-24 lg:h-fit">
              <Reveal>
                <Card className="border-whatsapp/25 bg-whatsapp/6 p-6">
                  <h2 className="font-display text-heading-sm text-foreground">
                    {isAr ? 'لسه محتاج مساعدة؟' : 'Still need help?'}
                  </h2>
                  <p className="mt-2 text-body-sm leading-relaxed text-muted-foreground">
                    {isAr
                      ? 'ابعتلنا سؤالك على واتساب وهنرد عليك خلال ساعة في أيام العمل.'
                      : 'Send us your question on WhatsApp — we reply within an hour on working days.'}
                  </p>
                  <WhatsAppButton phone={SITE.phone} intent="support" size="lg" block className="mt-5">
                    {isAr ? 'اسأل على واتساب' : 'Ask on WhatsApp'}
                  </WhatsAppButton>
                  <Button asChild variant="outline" block className="mt-2.5">
                    <Link to="/contact">{isAr ? 'ابعت رسالة' : 'Send a message'}</Link>
                  </Button>
                </Card>
              </Reveal>

              <Reveal delay={0.06}>
                <Card className="p-6">
                  <h3 className="font-display text-body font-bold text-foreground">
                    {isAr ? 'روابط مفيدة' : 'Useful links'}
                  </h3>
                  <ul className="mt-4 space-y-2.5">
                    {[
                      { label: 'ابدأ كمدرّس', href: '/register' },
                      { label: 'باقات وأسعار', href: '/pricing' },
                      { label: 'تصفّح المدرّسين', href: '/teachers' },
                      { label: 'المجموعات المتاحة', href: '/groups' },
                      { label: 'عن أستاذ', href: '/about' },
                    ].map((l) => (
                      <li key={l.href}>
                        <Link
                          to={l.href}
                          className="link-underline text-body-sm text-muted-foreground transition-colors hover:text-foreground"
                        >
                          {l.label}
                        </Link>
                      </li>
                    ))}
                  </ul>
                </Card>
              </Reveal>
            </aside>
          </div>
        </div>
      </section>
    </>
  )
}
