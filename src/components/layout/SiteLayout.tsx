import { Outlet, useLocation } from 'react-router-dom'
import { AnimatePresence, motion } from 'framer-motion'
import { SiteNavbar } from '@/components/layout/SiteNavbar'
import { SiteFooter } from '@/components/layout/SiteFooter'
import { FloatingWhatsApp } from '@/components/whatsapp/WhatsAppButton'
import { useScrollToTop } from '@/hooks/use-ui'
import { pageTransition } from '@/lib/motion'
import { SITE } from '@/lib/constants'

/** Routes that render their own contextual WhatsApp launcher. */
const SELF_MANAGED_WHATSAPP = ['/teachers/', '/groups/', '/courses/']

export function SiteLayout() {
  const location = useLocation()
  useScrollToTop()

  const ownsWhatsApp = SELF_MANAGED_WHATSAPP.some(
    (prefix) => location.pathname.startsWith(prefix) && location.pathname.length > prefix.length,
  )

  return (
    <div className="flex min-h-dvh flex-col bg-background">
      <SiteNavbar />

      <main id="main" className="flex-1">
        <AnimatePresence mode="wait" initial={false}>
          <motion.div
            key={location.pathname}
            variants={pageTransition}
            initial="initial"
            animate="animate"
            exit="exit"
          >
            <Outlet />
          </motion.div>
        </AnimatePresence>
      </main>

      <SiteFooter />

      {!ownsWhatsApp && (
        <FloatingWhatsApp
          phone={SITE.phone}
          contactName="فريق أستاذ"
          contactRole="بنرد عادةً خلال ساعة"
          options={[
            { label: 'أنا مدرّس وعايز أبدأ', intent: 'send-inquiry', note: 'أنا مدرّس وحابب أعرف إزاي أبدأ على المنصة.' },
            { label: 'أنا طالب ومحتاج مساعدة', intent: 'support', note: 'أنا طالب ومحتاج مساعدة في اختيار مدرّس.' },
            { label: 'عندي سنتر تعليمي', intent: 'send-inquiry', note: 'عندي سنتر تعليمي وحابب أعرف تفاصيل باقة السنتر.' },
          ]}
        />
      )}
    </div>
  )
}
