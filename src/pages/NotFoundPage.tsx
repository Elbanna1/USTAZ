import { Link } from 'react-router-dom'
import { ArrowLeft, Home, Search } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Spot } from '@/components/illustrations/Spot'
import { AmbientGlow, GoldRule, PaperTexture } from '@/components/illustrations/CoverArt'
import { LogoMark } from '@/components/brand/Logo'
import { usePageTitle } from '@/hooks/use-page-title'
import { MAIN_NAV } from '@/lib/constants'

export default function NotFoundPage() {
  usePageTitle('الصفحة مش موجودة')

  return (
    <section className="relative flex min-h-[70dvh] items-center overflow-hidden py-section">
      <AmbientGlow />
      <PaperTexture className="opacity-40 dark:opacity-20" />

      <div className="container-page relative">
        <div className="mx-auto max-w-2xl text-center">
          <LogoMark className="mx-auto size-14" />

          <p className="tnum mt-8 font-display text-[clamp(4rem,14vw,8rem)] font-extrabold leading-none text-gradient-gold">
            404
          </p>

          <h1 className="mt-2 text-display-lg text-foreground">الصفحة دي مش موجودة</h1>

          <p className="mt-4 text-body-lg text-muted-foreground">
            يمكن اللينك اتغيّر أو اتكتب غلط. تقدر ترجع للرئيسية أو تدوّر على اللي محتاجه من هنا.
          </p>

          <Spot name="offline" className="mx-auto mt-8 h-32" />

          <GoldRule className="mx-auto mt-8 max-w-xs" />

          <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
            <Button asChild size="lg">
              <Link to="/">
                <Home className="size-4" />
                الرجوع للرئيسية
              </Link>
            </Button>
            <Button asChild size="lg" variant="outline">
              <Link to="/teachers">
                <Search className="size-4" />
                دوّر على مدرّس
              </Link>
            </Button>
          </div>

          <div className="mt-12">
            <p className="text-caption text-muted-foreground">أو جرّب الصفحات دي</p>
            <ul className="mt-4 flex flex-wrap items-center justify-center gap-2">
              {MAIN_NAV.map((item) => (
                <li key={item.href}>
                  <Link
                    to={item.href}
                    className="group inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-4 py-2 text-body-sm text-muted-foreground transition-colors hover:border-foreground/20 hover:text-foreground"
                  >
                    {item.label}
                    <ArrowLeft className="size-3.5 -translate-x-1 opacity-0 transition-all group-hover:translate-x-0 group-hover:opacity-100" />
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        </div>
      </div>
    </section>
  )
}
