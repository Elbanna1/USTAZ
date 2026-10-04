import * as React from 'react'
import { Slot } from '@radix-ui/react-slot'
import { cva, type VariantProps } from 'class-variance-authority'
import { Loader2 } from 'lucide-react'
import { cn } from '@/lib/utils'

const buttonVariants = cva(
  'inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-lg font-display font-semibold transition-all duration-200 ease-premium focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background disabled:pointer-events-none disabled:opacity-50 active:scale-[0.98] [&_svg]:pointer-events-none [&_svg]:shrink-0',
  {
    variants: {
      variant: {
        primary:
          'bg-primary text-primary-foreground shadow-soft hover:bg-primary/90 hover:shadow-lift',
        accent:
          'bg-accent text-accent-foreground shadow-soft hover:bg-accent/90 hover:shadow-lift',
        secondary:
          'bg-secondary text-secondary-foreground hover:bg-secondary/70 border border-border',
        outline:
          'border border-border bg-transparent text-foreground hover:bg-muted hover:border-foreground/20',
        ghost: 'text-foreground hover:bg-muted',
        link: 'text-primary underline-offset-4 hover:underline h-auto p-0',
        destructive:
          'bg-destructive text-destructive-foreground shadow-soft hover:bg-destructive/90',
        whatsapp:
          'bg-whatsapp text-white shadow-soft hover:bg-whatsapp-dark hover:shadow-lift',
        /* whatsapp-dark on the pale tint measures 3.9:1; the ink green clears AA. */
        'whatsapp-outline':
          'border border-whatsapp/40 bg-whatsapp/8 text-whatsapp-ink hover:bg-whatsapp/15 dark:text-whatsapp',
      },
      size: {
        sm: 'h-9 px-3.5 text-body-sm [&_svg]:size-4',
        md: 'h-11 px-5 text-body-sm [&_svg]:size-[1.125rem]',
        lg: 'h-13 px-7 text-body [&_svg]:size-5',
        xl: 'h-14 px-8 text-body-lg [&_svg]:size-5',
        icon: 'h-10 w-10 [&_svg]:size-[1.125rem]',
        'icon-sm': 'h-8 w-8 rounded-md [&_svg]:size-4',
      },
      block: { true: 'w-full', false: '' },
    },
    defaultVariants: { variant: 'primary', size: 'md', block: false },
  },
)

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean
  loading?: boolean
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  (
    { className, variant, size, block, asChild = false, loading = false, children, disabled, ...props },
    ref,
  ) => {
    const Comp = asChild ? Slot : 'button'
    return (
      <Comp
        ref={ref}
        className={cn(buttonVariants({ variant, size, block }), className)}
        disabled={disabled || loading}
        aria-busy={loading || undefined}
        {...props}
      >
        {loading ? (
          <>
            <Loader2 className="animate-spin" aria-hidden />
            {children}
          </>
        ) : (
          children
        )}
      </Comp>
    )
  },
)
Button.displayName = 'Button'

export { Button, buttonVariants }
