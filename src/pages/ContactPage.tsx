import { Mail, MapPin, MessageCircle } from 'lucide-react'
import { useContactChannels, useContactFaq } from '@/data/content'
import { useI18n } from '@/i18n'
import { SITE } from '@/lib/constants'
import { usePageTitle } from '@/hooks/use-page-title'
import { PageHeader } from '@/components/common/PageHeader'
import { PanelHeading } from '@/components/common/SectionHeading'
import { Reveal } from '@/components/common/Reveal'
import { Card } from '@/components/ui/card'
import { Separator } from '@/components/ui/primitives'
import { ContactForm } from '@/components/forms/ContactForm'
import { FaqAccordion } from '@/components/common/FaqAccordion'
import { CopyButton } from '@/components/common/controls'
import { WhatsAppButton, WhatsAppIcon } from '@/components/whatsapp/WhatsAppButton'
import { GoldRule } from '@/components/illustrations/CoverArt'

const CHANNEL_ICONS = {
  whatsapp: WhatsAppIcon,
  email: Mail,
  address: MapPin,
} as const

export default function ContactPage() {
  const { locale } = useI18n()
  const isAr = locale === 'ar-EG'
  const contactChannels = useContactChannels()
  const contactFaq = useContactFaq()

  usePageTitle(
    isAr ? 'تواصل معنا' : 'Contact us',
    isAr
      ? 'كلّم فريق أستاذ على واتساب أو بالبريد الإلكتروني.'
      : 'Reach the USTAZ team on WhatsApp or by email.',
  )

  return (
    <>
      <PageHeader
        eyebrow={isAr ? 'تواصل' : 'Contact'}
        title={isAr ? 'كلّمنا — بنرد فعلًا' : 'Talk to us — we do reply'}
        description={
          isAr
            ? 'سواء إنت مدرّس عايز تبدأ، طالب محتاج مساعدة، أو سنتر عايز عرض مخصص — إحنا موجودين.'
            : 'Whether you are a teacher getting started, a student who needs help, or a centre after a quote — we are here.'
        }
        crumbs={[
          { label: isAr ? 'الرئيسية' : 'Home', href: '/' },
          { label: isAr ? 'تواصل معنا' : 'Contact' },
        ]}
      />

      <section className="section-y !py-12">
        <div className="container-page">
          <div className="grid gap-10 lg:grid-cols-[1fr,22rem] lg:gap-14">
            {/* Form */}
            <div className="min-w-0">
              <Reveal>
                <PanelHeading
                  title={isAr ? 'ابعتلنا رسالة' : 'Send us a message'}
                  description={
                    isAr
                      ? 'املا الفورم وهنرد عليك خلال يوم عمل واحد على الأكتر.'
                      : 'Fill in the form and we will reply within one working day at most.'
                  }
                />
              </Reveal>

              <Reveal className="mt-7">
                <Card className="p-6">
                  <ContactForm />
                </Card>
              </Reveal>

              <Reveal className="mt-14">
                <PanelHeading
                  title={isAr ? 'أسئلة بتتسأل كتير قبل التواصل' : 'Asked often before getting in touch'}
                />
                <div className="mt-6">
                  <FaqAccordion items={contactFaq} />
                </div>
              </Reveal>
            </div>

            {/* Channels */}
            <aside className="space-y-5 lg:sticky lg:top-24 lg:h-fit">
              <Reveal>
                <Card className="border-whatsapp/25 bg-whatsapp/6 p-6">
                  <span className="grid size-11 place-items-center rounded-xl bg-whatsapp/15 text-whatsapp">
                    <MessageCircle className="size-5" />
                  </span>
                  <h3 className="mt-4 font-display text-heading-sm text-foreground">
                    {isAr ? 'أسرع طريقة: واتساب' : 'Fastest route: WhatsApp'}
                  </h3>
                  <p className="mt-2 text-body-sm leading-relaxed text-muted-foreground">
                    {isAr
                      ? 'بنرد عادةً خلال ساعة في أيام العمل، من ١٠ الصبح لـ ٨ بالليل.'
                      : 'We usually reply within an hour on working days, 10am to 8pm.'}
                  </p>
                  <WhatsAppButton phone={SITE.phone} intent="support" size="lg" block className="mt-5">
                    {isAr ? 'ابدأ محادثة' : 'Start a chat'}
                  </WhatsAppButton>
                </Card>
              </Reveal>

              <Reveal delay={0.06}>
                <Card className="p-6">
                  <h3 className="font-display text-body font-bold text-foreground">
                    {isAr ? 'قنوات التواصل' : 'Ways to reach us'}
                  </h3>
                  <GoldRule className="my-4" />

                  <ul className="space-y-5">
                    {contactChannels.map((c, i) => {
                      const Icon = CHANNEL_ICONS[c.kind]
                      return (
                        <li key={c.id}>
                          {i > 0 && <Separator className="mb-5" />}
                          <div className="flex items-start gap-3">
                            <span className="mt-0.5 grid size-9 shrink-0 place-items-center rounded-lg border border-border bg-muted/60 text-primary">
                              <Icon className="size-4" />
                            </span>
                            <div className="min-w-0 flex-1">
                              <p className="font-display text-body-sm font-bold text-foreground">
                                {c.title}
                              </p>
                              <p className="mt-0.5 text-caption text-muted-foreground">
                                {c.description}
                              </p>
                              <p
                                className="tnum mt-2 break-words font-medium text-foreground"
                                dir={c.kind === 'address' ? 'auto' : 'ltr'}
                              >
                                {c.value}
                              </p>
                              {c.kind !== 'address' && (
                                <CopyButton value={c.value} className="mt-2" size="sm" />
                              )}
                            </div>
                          </div>
                        </li>
                      )
                    })}
                  </ul>
                </Card>
              </Reveal>

              <Reveal delay={0.1}>
                <Card className="p-6">
                  <h3 className="font-display text-body font-bold text-foreground">
                    {isAr ? 'مواعيد العمل' : 'Working hours'}
                  </h3>
                  <dl className="mt-4 space-y-2.5 text-body-sm">
                    <div className="flex items-center justify-between gap-3">
                      <dt className="text-muted-foreground">
                        {isAr ? 'السبت — الخميس' : 'Saturday — Thursday'}
                      </dt>
                      <dd className="tnum font-semibold text-foreground">
                        {isAr ? '١٠ ص — ٨ م' : '10am — 8pm'}
                      </dd>
                    </div>
                    <div className="flex items-center justify-between gap-3">
                      <dt className="text-muted-foreground">{isAr ? 'الجمعة' : 'Friday'}</dt>
                      <dd className="font-semibold text-muted-foreground">
                        {isAr ? 'إجازة' : 'Closed'}
                      </dd>
                    </div>
                  </dl>
                  <p className="mt-4 text-caption leading-relaxed text-muted-foreground">
                    {isAr
                      ? 'الرسايل اللي بتوصل بره المواعيد دي بيتم الرد عليها أول يوم عمل.'
                      : 'Messages that arrive outside these hours are answered on the next working day.'}
                  </p>
                </Card>
              </Reveal>
            </aside>
          </div>
        </div>
      </section>
    </>
  )
}
