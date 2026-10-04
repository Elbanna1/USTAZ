import * as React from 'react'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'

const badgeVariants = cva(
  'inline-flex items-center gap-1.5 rounded-full border font-display font-semibold transition-colors [&_svg]:size-3.5 [&_svg]:shrink-0',
  {
    variants: {
      variant: {
        default: 'border-border bg-muted text-muted-foreground',
        primary: 'border-primary/20 bg-primary/10 text-primary dark:bg-primary/15',
        accent: 'border-accent/30 bg-accent/12 text-accent-strong',
        solid: 'border-transparent bg-primary text-primary-foreground',
        gold: 'border-transparent bg-gradient-to-l from-accent to-accent-soft text-accent-foreground shadow-xs',
        success: 'border-success/25 bg-success/12 text-success-strong',
        warning: 'border-warning/30 bg-warning/12 text-warning-strong',
        destructive: 'border-destructive/25 bg-destructive/12 text-destructive-strong',
        outline: 'border-border bg-transparent text-foreground',
        whatsapp: 'border-whatsapp/30 bg-whatsapp/12 text-whatsapp-ink dark:text-whatsapp',
      },
      size: {
        sm: 'px-2 py-0.5 text-[0.6875rem]',
        md: 'px-2.5 py-1 text-caption',
        lg: 'px-3 py-1.5 text-body-sm',
      },
    },
    defaultVariants: { variant: 'default', size: 'md' },
  },
)

export interface BadgeProps
  extends React.HTMLAttributes<HTMLSpanElement>,
    VariantProps<typeof badgeVariants> {}

function Badge({ className, variant, size, ...props }: BadgeProps) {
  return <span className={cn(badgeVariants({ variant, size }), className)} {...props} />
}

export { Badge, badgeVariants }
