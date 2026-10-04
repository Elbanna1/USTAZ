import type { PriceBreakdown } from '@/types'
import type { PricingDto } from '@/api/schema'

/* -------------------------------------------------------------------------- */
/*                            API pricing adapter                             */
/* -------------------------------------------------------------------------- */

/**
 * The API already resolved the discount server-side, so the frontend only has to
 * reshape `PricingDto` into the breakdown every price component already reads.
 */
export function pricingToBreakdown(pricing?: PricingDto | null): PriceBreakdown {
  const final = pricing?.price ?? 0
  const original = pricing?.originalPrice ?? final
  const discountAmount = Math.max(0, Math.round(original - final))

  // The API only fills `discountPercentage` for coupon-style discounts; a group
  // priced below its original price reports 0 there, so derive it from the
  // amounts whenever the server has not given us a real percentage.
  const serverPercent = pricing?.discountPercentage ?? 0
  const derived = original > 0 ? Math.round((discountAmount / original) * 100) : 0

  return {
    original,
    discountAmount,
    final,
    percentOff: serverPercent > 0 ? serverPercent : derived,
  }
}

export function isFreePricing(pricing?: PricingDto | null): boolean {
  return Boolean(pricing?.isFree) || (pricing?.price ?? 0) <= 0
}
