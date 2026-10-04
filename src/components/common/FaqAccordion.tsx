import type { FaqItem } from '@/types'
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion'
import { cn } from '@/lib/utils'

export function FaqAccordion({
  items,
  className,
  defaultOpen,
}: {
  items: FaqItem[]
  className?: string
  /** id of the item that starts expanded. */
  defaultOpen?: string
}) {
  if (items.length === 0) return null

  return (
    <Accordion
      type="single"
      collapsible
      defaultValue={defaultOpen}
      className={cn('space-y-3', className)}
    >
      {items.map((item) => (
        <AccordionItem key={item.id} value={item.id}>
          <AccordionTrigger>{item.question}</AccordionTrigger>
          <AccordionContent>{item.answer}</AccordionContent>
        </AccordionItem>
      ))}
    </Accordion>
  )
}
