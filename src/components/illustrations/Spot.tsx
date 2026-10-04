import { cn } from '@/lib/utils'

/**
 * Spot illustrations for empty, error and success states.
 * Theme-aware: they paint with the palette's own tokens rather than fixed hex.
 */

export type SpotName =
  | 'search'
  | 'teachers'
  | 'calendar'
  | 'bookmark'
  | 'inbox'
  | 'error'
  | 'success'
  | 'offline'
  | 'courses'
  | 'groups'
  | 'exams'
  | 'homework'
  | 'messages'
  | 'certificate'
  | 'payments'
  | 'lock'

const SHEET = 'fill-[hsl(var(--card))] stroke-[hsl(var(--border))]'
const INK = 'stroke-[hsl(var(--muted-foreground))]'
const SOFT = 'fill-[hsl(var(--muted))]'
const GOLD = 'stroke-[hsl(var(--accent))]'
const GOLD_FILL = 'fill-[hsl(var(--accent))]'
const PRIMARY_FILL = 'fill-[hsl(var(--primary))]'

function Base({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <svg
      viewBox="0 0 200 160"
      className={cn('h-36 w-auto', className)}
      fill="none"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden
      focusable="false"
    >
      <ellipse cx="100" cy="142" rx="62" ry="7" className={SOFT} />
      {children}
    </svg>
  )
}

const SPOTS: Record<SpotName, React.ReactNode> = {
  search: (
    <>
      <rect x="44" y="30" width="86" height="104" rx="8" className={cn(SHEET, 'stroke-2')} />
      <path d="M60 56h44M60 70h54M60 84h32" className={INK} strokeOpacity="0.55" />
      <circle cx="122" cy="94" r="26" className="fill-[hsl(var(--background))] stroke-[hsl(var(--primary))]" strokeWidth="3" />
      <path d="M141 113l14 14" className="stroke-[hsl(var(--primary))]" strokeWidth="4" />
      <path d="M112 94h20M122 84v20" className={GOLD} strokeWidth="2.5" />
    </>
  ),
  teachers: (
    <>
      <rect x="30" y="52" width="60" height="76" rx="8" className={SHEET} />
      <rect x="110" y="52" width="60" height="76" rx="8" className={SHEET} />
      <circle cx="60" cy="78" r="13" className={SOFT} />
      <circle cx="140" cy="78" r="13" className={SOFT} />
      <path d="M44 106h32M46 116h28" className={INK} strokeOpacity="0.5" />
      <path d="M124 106h32M126 116h28" className={INK} strokeOpacity="0.5" />
      <path d="M100 34l4.5 9.2 10.2 1.5-7.4 7.2 1.8 10.1-9.1-4.8-9.1 4.8 1.8-10.1-7.4-7.2 10.2-1.5z" className={GOLD_FILL} />
    </>
  ),
  calendar: (
    <>
      <rect x="42" y="38" width="116" height="96" rx="10" className={SHEET} />
      <path d="M42 62h116" className="stroke-[hsl(var(--border))]" />
      <path d="M70 30v16M130 30v16" className="stroke-[hsl(var(--primary))]" strokeWidth="3" />
      {[0, 1, 2, 3].map((c) =>
        [0, 1, 2].map((r) => (
          <rect
            key={`${c}-${r}`}
            x={60 + c * 24}
            y={76 + r * 18}
            width="14"
            height="9"
            rx="3"
            className={SOFT}
          />
        )),
      )}
      <rect x="108" y="94" width="14" height="9" rx="3" className={GOLD_FILL} />
    </>
  ),
  bookmark: (
    <>
      <rect x="52" y="26" width="96" height="108" rx="9" className={SHEET} />
      <path d="M68 48h48M68 64h60M68 80h38" className={INK} strokeOpacity="0.5" />
      <path
        d="M108 26h30v52l-15-11-15 11z"
        className={cn(GOLD_FILL, 'stroke-[hsl(var(--accent))]')}
        fillOpacity="0.22"
        strokeWidth="2.5"
      />
      <path d="M62 108h44" className={INK} strokeOpacity="0.35" />
    </>
  ),
  inbox: (
    <>
      <path d="M40 74l16-38h88l16 38v44a8 8 0 0 1-8 8H48a8 8 0 0 1-8-8z" className={SHEET} />
      <path d="M40 74h34l6 14h40l6-14h34" className="stroke-[hsl(var(--primary))]" strokeWidth="2.5" />
      <path d="M78 52h44M84 62h32" className={INK} strokeOpacity="0.5" />
      <circle cx="152" cy="46" r="11" className={GOLD_FILL} />
    </>
  ),
  error: (
    <>
      <rect x="38" y="42" width="124" height="86" rx="10" className={SHEET} />
      <path d="M38 62h124" className="stroke-[hsl(var(--border))]" />
      <circle cx="52" cy="52" r="3" className={GOLD_FILL} />
      <circle cx="64" cy="52" r="3" className={SOFT} />
      <circle cx="76" cy="52" r="3" className={SOFT} />
      <path d="M84 84l32 26M116 84l-32 26" className="stroke-[hsl(var(--destructive))]" strokeWidth="4" />
    </>
  ),
  success: (
    <>
      <circle cx="100" cy="76" r="42" className="fill-[hsl(var(--success))]" fillOpacity="0.12" />
      <circle cx="100" cy="76" r="30" className={PRIMARY_FILL} fillOpacity="0.9" />
      <path
        d="M87 76l9 9 18-19"
        className="stroke-[hsl(var(--primary-foreground))]"
        strokeWidth="4"
      />
      <path d="M46 40l3 7 7 3-7 3-3 7-3-7-7-3 7-3z" className={GOLD_FILL} />
      <path d="M156 96l2.4 5.6 5.6 2.4-5.6 2.4-2.4 5.6-2.4-5.6-5.6-2.4 5.6-2.4z" className={GOLD_FILL} />
      <path d="M148 40l2 4.6 4.6 2-4.6 2-2 4.6-2-4.6-4.6-2 4.6-2z" className={GOLD_FILL} fillOpacity="0.6" />
    </>
  ),
  offline: (
    <>
      <rect x="46" y="44" width="108" height="76" rx="9" className={SHEET} />
      <path d="M78 132h44" className={INK} strokeWidth="3" />
      <path d="M100 120v12" className={INK} strokeWidth="3" />
      <path d="M74 84a36 36 0 0 1 52 0" className={INK} strokeOpacity="0.45" strokeWidth="3" />
      <path d="M86 96a20 20 0 0 1 28 0" className={INK} strokeOpacity="0.45" strokeWidth="3" />
      <circle cx="100" cy="106" r="3.5" className={GOLD_FILL} />
      <path d="M60 58l80 52" className="stroke-[hsl(var(--destructive))]" strokeWidth="3.5" />
    </>
  ),
  courses: (
    <>
      <rect x="36" y="44" width="128" height="80" rx="9" className={SHEET} />
      <path d="M36 62h128" className="stroke-[hsl(var(--border))]" />
      <circle cx="100" cy="94" r="20" className="fill-[hsl(var(--background))] stroke-[hsl(var(--primary))]" strokeWidth="3" />
      <path d="M95 86l14 8-14 8z" className={PRIMARY_FILL} />
      <path d="M64 136h72" className={INK} strokeWidth="3" strokeOpacity="0.4" />
      <circle cx="50" cy="53" r="3" className={GOLD_FILL} />
      <circle cx="62" cy="53" r="3" className={SOFT} />
    </>
  ),
  groups: (
    <>
      <rect x="32" y="56" width="136" height="72" rx="10" className={SHEET} />
      <circle cx="66" cy="84" r="12" className={SOFT} />
      <circle cx="100" cy="78" r="14" className={PRIMARY_FILL} fillOpacity="0.25" />
      <circle cx="134" cy="84" r="12" className={SOFT} />
      <path d="M50 108h32M86 104h28M120 108h32" className={INK} strokeOpacity="0.45" strokeWidth="3" />
      <path d="M100 30l4 8.4 9.2 1.4-6.6 6.4 1.6 9.2-8.2-4.4-8.2 4.4 1.6-9.2-6.6-6.4 9.2-1.4z" className={GOLD_FILL} />
    </>
  ),
  exams: (
    <>
      <rect x="50" y="26" width="100" height="112" rx="9" className={SHEET} />
      <path d="M66 52h40M66 70h56M66 88h34" className={INK} strokeOpacity="0.5" />
      <rect x="64" y="102" width="72" height="22" rx="6" className={SOFT} />
      <circle cx="138" cy="44" r="18" className="fill-[hsl(var(--background))] stroke-[hsl(var(--accent))]" strokeWidth="3" />
      <path d="M138 34v11l7 5" className={GOLD} strokeWidth="3" />
    </>
  ),
  homework: (
    <>
      <rect x="44" y="34" width="104" height="100" rx="9" className={SHEET} />
      <path d="M60 58h52M60 76h64M60 94h38" className={INK} strokeOpacity="0.5" />
      <path d="M118 112l30-30 12 12-30 30-16 4z" className={cn(GOLD_FILL, 'stroke-[hsl(var(--accent))]')} fillOpacity="0.3" strokeWidth="2.5" />
      <path d="M142 88l12 12" className={GOLD} strokeWidth="2.5" />
    </>
  ),
  messages: (
    <>
      <path d="M34 50a10 10 0 0 1 10-10h74a10 10 0 0 1 10 10v38a10 10 0 0 1-10 10H70l-20 18V98h-6a10 10 0 0 1-10-10z" className={SHEET} />
      <path d="M56 62h50M56 78h34" className={INK} strokeOpacity="0.5" />
      <path d="M104 86a9 9 0 0 1 9-9h44a9 9 0 0 1 9 9v30a9 9 0 0 1-9 9h-6v14l-16-14h-22a9 9 0 0 1-9-9z" className="fill-[hsl(var(--primary))] stroke-[hsl(var(--primary))]" fillOpacity="0.14" strokeWidth="2.5" />
      <path d="M120 98h32M120 110h20" className={GOLD} strokeWidth="2.5" />
    </>
  ),
  certificate: (
    <>
      <rect x="38" y="32" width="124" height="82" rx="8" className={SHEET} />
      <path d="M58 54h84M58 70h64" className={INK} strokeOpacity="0.5" />
      <circle cx="100" cy="106" r="20" className={cn(GOLD_FILL, 'stroke-[hsl(var(--accent))]')} fillOpacity="0.3" strokeWidth="2.5" />
      <path d="M92 120l-6 24 14-8 14 8-6-24" className={GOLD} strokeWidth="2.5" fill="none" />
      <path d="M93 106l5 5 10-10" className={GOLD} strokeWidth="3" fill="none" />
    </>
  ),
  payments: (
    <>
      <rect x="34" y="52" width="132" height="80" rx="10" className={SHEET} />
      <path d="M34 74h132" className="stroke-[hsl(var(--primary))]" strokeWidth="8" strokeOpacity="0.18" />
      <rect x="52" y="94" width="34" height="10" rx="4" className={SOFT} />
      <rect x="52" y="110" width="58" height="8" rx="4" className={SOFT} />
      <circle cx="136" cy="108" r="16" className={GOLD_FILL} fillOpacity="0.85" />
      <circle cx="150" cy="108" r="16" className={PRIMARY_FILL} fillOpacity="0.45" />
    </>
  ),
  lock: (
    <>
      <rect x="58" y="72" width="84" height="66" rx="12" className={SHEET} />
      <path d="M76 72V56a24 24 0 0 1 48 0v16" className="stroke-[hsl(var(--primary))]" strokeWidth="4" fill="none" />
      <circle cx="100" cy="100" r="9" className={PRIMARY_FILL} />
      <path d="M100 109v14" className="stroke-[hsl(var(--primary))]" strokeWidth="4" />
      <path d="M156 40l3 7 7 3-7 3-3 7-3-7-7-3 7-3z" className={GOLD_FILL} />
    </>
  ),
}

export function Spot({ name, className }: { name: SpotName; className?: string }) {
  return <Base className={className}>{SPOTS[name]}</Base>
}
