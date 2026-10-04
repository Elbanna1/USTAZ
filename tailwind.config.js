import animate from 'tailwindcss-animate'

/** @type {import('tailwindcss').Config} */
export default {
  darkMode: ['class'],
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    container: {
      center: true,
      padding: { DEFAULT: '1.25rem', sm: '1.5rem', lg: '2rem', xl: '2.5rem' },
      screens: { '2xl': '1320px' },
    },
    extend: {
      colors: {
        border: 'hsl(var(--border) / <alpha-value>)',
        input: 'hsl(var(--input) / <alpha-value>)',
        ring: 'hsl(var(--ring) / <alpha-value>)',
        background: 'hsl(var(--background) / <alpha-value>)',
        foreground: 'hsl(var(--foreground) / <alpha-value>)',
        surface: {
          DEFAULT: 'hsl(var(--surface) / <alpha-value>)',
          foreground: 'hsl(var(--surface-foreground) / <alpha-value>)',
        },
        primary: {
          DEFAULT: 'hsl(var(--primary) / <alpha-value>)',
          foreground: 'hsl(var(--primary-foreground) / <alpha-value>)',
          soft: 'hsl(var(--primary-soft) / <alpha-value>)',
        },
        secondary: {
          DEFAULT: 'hsl(var(--secondary) / <alpha-value>)',
          foreground: 'hsl(var(--secondary-foreground) / <alpha-value>)',
        },
        accent: {
          DEFAULT: 'hsl(var(--accent) / <alpha-value>)',
          foreground: 'hsl(var(--accent-foreground) / <alpha-value>)',
          soft: 'hsl(var(--accent-soft) / <alpha-value>)',
          /* Text-grade gold — see globals.css. */
          strong: 'hsl(var(--accent-strong) / <alpha-value>)',
        },
        muted: {
          DEFAULT: 'hsl(var(--muted) / <alpha-value>)',
          foreground: 'hsl(var(--muted-foreground) / <alpha-value>)',
        },
        card: {
          DEFAULT: 'hsl(var(--card) / <alpha-value>)',
          foreground: 'hsl(var(--card-foreground) / <alpha-value>)',
        },
        popover: {
          DEFAULT: 'hsl(var(--popover) / <alpha-value>)',
          foreground: 'hsl(var(--popover-foreground) / <alpha-value>)',
        },
        destructive: {
          DEFAULT: 'hsl(var(--destructive) / <alpha-value>)',
          foreground: 'hsl(var(--destructive-foreground) / <alpha-value>)',
          strong: 'hsl(var(--destructive-strong) / <alpha-value>)',
        },
        success: {
          DEFAULT: 'hsl(var(--success) / <alpha-value>)',
          foreground: 'hsl(var(--success-foreground) / <alpha-value>)',
          strong: 'hsl(var(--success-strong) / <alpha-value>)',
        },
        warning: {
          DEFAULT: 'hsl(var(--warning) / <alpha-value>)',
          foreground: 'hsl(var(--warning-foreground) / <alpha-value>)',
          strong: 'hsl(var(--warning-strong) / <alpha-value>)',
        },
        whatsapp: {
          DEFAULT: '#25D366',
          dark: '#128C7E',
          ink: '#075E54',
        },
      },
      fontFamily: {
        display: ['Cairo', 'IBM Plex Sans Arabic', 'Segoe UI', 'system-ui', 'sans-serif'],
        sans: ['IBM Plex Sans Arabic', 'Cairo', 'Segoe UI', 'system-ui', 'sans-serif'],
        latin: ['Plus Jakarta Sans', 'IBM Plex Sans Arabic', 'system-ui', 'sans-serif'],
      },
      fontSize: {
        /* Capped at 4rem: the hero copy column is ~620px at 1536, and 5rem left
           the Arabic headline at ~8 characters a line. */
        'display-2xl': ['clamp(2.5rem, 4.4vw, 4rem)', { lineHeight: '1.12', letterSpacing: '-0.02em', fontWeight: '800' }],
        'display-xl': ['clamp(2.25rem, 4.6vw, 3.75rem)', { lineHeight: '1.12', letterSpacing: '-0.02em', fontWeight: '800' }],
        'display-lg': ['clamp(1.875rem, 3.4vw, 2.75rem)', { lineHeight: '1.18', letterSpacing: '-0.015em', fontWeight: '700' }],
        'heading-lg': ['clamp(1.5rem, 2.4vw, 2rem)', { lineHeight: '1.25', letterSpacing: '-0.01em', fontWeight: '700' }],
        'heading-md': ['1.375rem', { lineHeight: '1.35', fontWeight: '700' }],
        'heading-sm': ['1.125rem', { lineHeight: '1.45', fontWeight: '600' }],
        'body-lg': ['1.0625rem', { lineHeight: '1.85' }],
        body: ['0.9688rem', { lineHeight: '1.8' }],
        'body-sm': ['0.875rem', { lineHeight: '1.75' }],
        caption: ['0.8125rem', { lineHeight: '1.6' }],
        label: ['0.75rem', { lineHeight: '1.4', letterSpacing: '0.04em', fontWeight: '600' }],
        price: ['1.625rem', { lineHeight: '1.1', letterSpacing: '-0.02em', fontWeight: '800' }],
      },
      borderRadius: {
        xs: 'calc(var(--radius) - 8px)',
        sm: 'calc(var(--radius) - 6px)',
        md: 'calc(var(--radius) - 4px)',
        lg: 'var(--radius)',
        xl: 'calc(var(--radius) + 6px)',
        '2xl': 'calc(var(--radius) + 12px)',
        '3xl': 'calc(var(--radius) + 22px)',
      },
      spacing: {
        13: '3.25rem',
        18: '4.5rem',
        22: '5.5rem',
        section: 'clamp(4rem, 8vw, 7.5rem)',
      },
      boxShadow: {
        xs: '0 1px 2px 0 hsl(var(--shadow-color) / 0.05)',
        soft: '0 2px 4px -1px hsl(var(--shadow-color) / 0.06), 0 6px 16px -4px hsl(var(--shadow-color) / 0.08)',
        card: '0 1px 2px 0 hsl(var(--shadow-color) / 0.04), 0 8px 24px -8px hsl(var(--shadow-color) / 0.10)',
        lift: '0 8px 20px -6px hsl(var(--shadow-color) / 0.14), 0 24px 48px -16px hsl(var(--shadow-color) / 0.16)',
        glow: '0 0 0 1px hsl(var(--accent) / 0.22), 0 12px 32px -10px hsl(var(--accent) / 0.34)',
        insetTop: 'inset 0 1px 0 0 hsl(0 0% 100% / 0.06)',
      },
      keyframes: {
        'accordion-down': { from: { height: '0' }, to: { height: 'var(--radix-accordion-content-height)' } },
        'accordion-up': { from: { height: 'var(--radix-accordion-content-height)' }, to: { height: '0' } },
        'float-slow': { '0%,100%': { transform: 'translateY(0px)' }, '50%': { transform: 'translateY(-10px)' } },
        'float-slower': { '0%,100%': { transform: 'translateY(0px)' }, '50%': { transform: 'translateY(-16px)' } },
        shimmer: { '0%': { transform: 'translateX(100%)' }, '100%': { transform: 'translateX(-200%)' } },
        'pulse-ring': {
          '0%': { transform: 'scale(0.9)', opacity: '0.7' },
          '70%': { transform: 'scale(1.35)', opacity: '0' },
          '100%': { transform: 'scale(1.35)', opacity: '0' },
        },
      },
      animation: {
        'accordion-down': 'accordion-down 0.22s ease-out',
        'accordion-up': 'accordion-up 0.22s ease-out',
        'float-slow': 'float-slow 6s ease-in-out infinite',
        'float-slower': 'float-slower 9s ease-in-out infinite',
        shimmer: 'shimmer 2.2s infinite',
        'pulse-ring': 'pulse-ring 2.4s cubic-bezier(0.24,0,0.38,1) infinite',
      },
      transitionTimingFunction: {
        premium: 'cubic-bezier(0.22, 1, 0.36, 1)',
      },
    },
  },
  plugins: [animate],
}
