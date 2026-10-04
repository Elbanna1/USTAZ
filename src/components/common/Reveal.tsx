import { useMemo } from 'react'
import { motion, type Variants } from 'framer-motion'
import { fadeUp, revealViewport, staggerContainer } from '@/lib/motion'
import { cn } from '@/lib/utils'

/*
 * Scroll-reveal wrappers. Fire once, slightly before the element is fully in
 * view, and inherit the reduced-motion override from globals.css.
 *
 * The motion props are memoised deliberately. These wrappers sit around grids
 * whose sibling queries resolve at different moments, so the parent re-renders
 * while a reveal is still running. Handing framer-motion a freshly built
 * `variants` or `transition` object on one of those renders makes it
 * re-initialise the variant tree mid-tween, which can leave staggered children
 * short of their final state.
 */

interface RevealProps {
  children: React.ReactNode
  className?: string
  /** Seconds to hold before this element animates in. */
  delay?: number
  variants?: Variants
  as?: 'div' | 'section' | 'article' | 'li' | 'header' | 'aside'
}

export function Reveal({
  children,
  className,
  delay = 0,
  variants = fadeUp,
  as = 'div',
}: RevealProps) {
  const Comp = motion[as]
  const transition = useMemo(() => (delay ? { delay } : undefined), [delay])

  return (
    <Comp
      className={className}
      initial="hidden"
      whileInView="visible"
      viewport={revealViewport}
      variants={variants}
      transition={transition}
    >
      {children}
    </Comp>
  )
}

interface RevealGroupProps {
  children: React.ReactNode
  className?: string
  stagger?: number
  delayChildren?: number
  as?: 'div' | 'ul' | 'section'
}

/** Parent that staggers `RevealItem` children as the group scrolls in. */
export function RevealGroup({
  children,
  className,
  stagger = 0.07,
  delayChildren = 0.04,
  as = 'div',
}: RevealGroupProps) {
  const Comp = motion[as]
  const variants = useMemo(
    () => staggerContainer(stagger, delayChildren),
    [stagger, delayChildren],
  )

  return (
    <Comp
      className={className}
      initial="hidden"
      whileInView="visible"
      viewport={revealViewport}
      variants={variants}
    >
      {children}
    </Comp>
  )
}

export function RevealItem({
  children,
  className,
  variants = fadeUp,
  as = 'div',
}: {
  children: React.ReactNode
  className?: string
  variants?: Variants
  as?: 'div' | 'li' | 'article'
}) {
  const Comp = motion[as]
  return (
    <Comp className={className} variants={variants}>
      {children}
    </Comp>
  )
}

/** Card that lifts on hover — used by every card grid in the product. */
export function HoverLift({
  children,
  className,
  disabled,
}: {
  children: React.ReactNode
  className?: string
  disabled?: boolean
}) {
  return (
    <motion.div
      className={cn('h-full', className)}
      whileHover={disabled ? undefined : { y: -5 }}
      transition={{ duration: 0.22, ease: [0.22, 1, 0.36, 1] }}
    >
      {children}
    </motion.div>
  )
}
