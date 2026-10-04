import type { Transition, Variants } from 'framer-motion'

/**
 * One motion vocabulary for the whole product: short, eased, intentional.
 * Nothing bounces, nothing spins, nothing outlasts 0.6s.
 */

export const EASE_PREMIUM = [0.22, 1, 0.36, 1] as const
export const EASE_OUT = [0.16, 1, 0.3, 1] as const

export const transitions = {
  fast: { duration: 0.22, ease: EASE_PREMIUM } satisfies Transition,
  base: { duration: 0.38, ease: EASE_PREMIUM } satisfies Transition,
  slow: { duration: 0.56, ease: EASE_PREMIUM } satisfies Transition,
  spring: { type: 'spring', stiffness: 380, damping: 32, mass: 0.7 } satisfies Transition,
  softSpring: { type: 'spring', stiffness: 220, damping: 28 } satisfies Transition,
}

export const fadeUp: Variants = {
  hidden: { opacity: 0, y: 18 },
  visible: { opacity: 1, y: 0, transition: transitions.base },
}

export const fadeIn: Variants = {
  hidden: { opacity: 0 },
  visible: { opacity: 1, transition: transitions.base },
}

export const fadeDown: Variants = {
  hidden: { opacity: 0, y: -14 },
  visible: { opacity: 1, y: 0, transition: transitions.base },
}

/** RTL-aware: content enters from the right edge in an RTL document. */
export const fadeInStart: Variants = {
  hidden: { opacity: 0, x: 26 },
  visible: { opacity: 1, x: 0, transition: transitions.base },
}

export const fadeInEnd: Variants = {
  hidden: { opacity: 0, x: -26 },
  visible: { opacity: 1, x: 0, transition: transitions.base },
}

export const scaleIn: Variants = {
  hidden: { opacity: 0, scale: 0.96 },
  visible: { opacity: 1, scale: 1, transition: transitions.base },
}

export function staggerContainer(stagger = 0.07, delayChildren = 0.04): Variants {
  return {
    hidden: {},
    visible: {
      transition: { staggerChildren: stagger, delayChildren },
    },
  }
}

export const listItem: Variants = {
  hidden: { opacity: 0, y: 14 },
  visible: { opacity: 1, y: 0, transition: transitions.base },
}

/** Route-level transition — deliberately quick so navigation never feels laggy. */
export const pageTransition: Variants = {
  initial: { opacity: 0, y: 8 },
  animate: { opacity: 1, y: 0, transition: { duration: 0.3, ease: EASE_PREMIUM } },
  exit: { opacity: 0, y: -6, transition: { duration: 0.18, ease: 'easeIn' } },
}

export const modalOverlay: Variants = {
  hidden: { opacity: 0 },
  visible: { opacity: 1, transition: { duration: 0.2 } },
  exit: { opacity: 0, transition: { duration: 0.15 } },
}

export const modalContent: Variants = {
  hidden: { opacity: 0, scale: 0.97, y: 12 },
  visible: { opacity: 1, scale: 1, y: 0, transition: transitions.spring },
  exit: { opacity: 0, scale: 0.98, y: 8, transition: { duration: 0.15 } },
}

export const drawerFromStart: Variants = {
  hidden: { x: '100%' },
  visible: { x: 0, transition: { duration: 0.34, ease: EASE_PREMIUM } },
  exit: { x: '100%', transition: { duration: 0.22, ease: 'easeIn' } },
}

/** Standard viewport config for scroll-reveal — fires once, slightly early. */
export const revealViewport = { once: true, margin: '-80px 0px -60px 0px' } as const

export const cardHover = {
  rest: { y: 0 },
  hover: { y: -5, transition: transitions.fast },
}
