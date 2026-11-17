import * as React from 'react'
import { Slot } from '@radix-ui/react-slot'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'
import SpecularButton, {
  type SpecularButtonProps,
  type SpecularButtonSize,
} from './SpecularButton'

const buttonVariants = cva(
  'inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-md text-sm font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/20 focus-visible:border-primary disabled:pointer-events-none disabled:opacity-50 select-none [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0 cursor-pointer',
  {
    variants: {
      variant: {
        default:
          'bg-primary text-primary-foreground shadow-xs hover:bg-[#1d4ed8] active:translate-y-px',
        destructive:
          'bg-destructive text-destructive-foreground shadow-xs hover:bg-destructive/90 active:translate-y-px',
        outline:
          'border border-border-strong bg-surface text-foreground shadow-xs hover:bg-surface-soft hover:text-foreground active:translate-y-px',
        secondary:
          'bg-secondary text-secondary-foreground shadow-xs hover:bg-surface-soft/80 active:translate-y-px',
        ghost:
          'text-foreground hover:bg-surface-soft hover:text-foreground',
        link:
          'text-primary underline-offset-4 hover:underline p-0 h-auto font-medium',
        success:
          'bg-dvld-success text-white shadow-xs hover:bg-[#047857] active:translate-y-px',
      },
      size: {
        default: 'h-9 px-4 py-2',
        sm: 'h-8 rounded-sm px-3 text-xs',
        lg: 'h-10 rounded-lg px-6 text-sm',
        icon: 'size-9 p-0',
        'icon-sm': 'size-8 rounded-sm p-0',
      },
    },
    defaultVariants: {
      variant: 'default',
      size: 'default',
    },
  },
)

type ButtonVariant = NonNullable<VariantProps<typeof buttonVariants>['variant']>
type ButtonSize = NonNullable<VariantProps<typeof buttonVariants>['size']>

type SpecularPreset = Pick<
  SpecularButtonProps,
  | 'tint'
  | 'tintOpacity'
  | 'textColor'
  | 'lineColor'
  | 'baseColor'
  | 'intensity'
  | 'idleIntensity'
  | 'glow'
>

const SPECULAR_PRESETS: Record<
  Exclude<ButtonVariant, 'ghost' | 'link'>,
  SpecularPreset
> = {
  default: {
    tint: '#3b82f6',
    tintOpacity: 0.24,
    textColor: '#ffffff',
    lineColor: '#93c5fd',
    baseColor: '#2563eb',
    intensity: 1,
    idleIntensity: 0.45,
    glow: 0.95,
  },
  destructive: {
    tint: '#ef4444',
    tintOpacity: 0.22,
    textColor: '#fee2e2',
    lineColor: '#fca5a5',
    baseColor: '#b91c1c',
    intensity: 1,
    idleIntensity: 0.45,
    glow: 0.95,
  },
  success: {
    tint: '#10b981',
    tintOpacity: 0.22,
    textColor: '#ecfdf5',
    lineColor: '#6ee7b7',
    baseColor: '#047857',
    intensity: 1,
    idleIntensity: 0.45,
    glow: 0.95,
  },
  outline: {
    tint: '#101a2d',
    tintOpacity: 0.85,
    textColor: '#f1f5f9',
    lineColor: '#38bdf8',
    baseColor: '#354a68',
    intensity: 0.95,
    idleIntensity: 0.35,
    glow: 0.85,
  },
  secondary: {
    tint: '#162238',
    tintOpacity: 0.9,
    textColor: '#f1f5f9',
    lineColor: '#60a5fa',
    baseColor: '#334155',
    intensity: 0.9,
    idleIntensity: 0.3,
    glow: 0.8,
  },
}

const SPECULAR_SIZES: Record<ButtonSize, SpecularButtonSize> = {
  default: 'md',
  sm: 'sm',
  lg: 'lg',
  icon: 'icon',
  'icon-sm': 'icon-sm',
}

const SPECULAR_RADII: Record<ButtonSize, number> = {
  default: 10,
  sm: 8,
  lg: 12,
  icon: 10,
  'icon-sm': 8,
}

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean
  /** Override whether this button uses the shared WebGL2 SpecularButton surface. */
  specular?: boolean
  specularProps?: Partial<SpecularButtonProps>
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  (
    {
      className,
      variant = 'default',
      size = 'default',
      asChild = false,
      specular,
      specularProps,
      type = 'button',
      ...props
    },
    ref,
  ) => {
    const resolvedVariant: ButtonVariant = variant ?? 'default'
    const resolvedSize: ButtonSize = size ?? 'default'
    const useSpecular =
      !asChild &&
      (specular ??
        (resolvedVariant !== 'ghost' && resolvedVariant !== 'link'))

    if (useSpecular) {
      const preset =
        resolvedVariant !== 'ghost' && resolvedVariant !== 'link'
          ? SPECULAR_PRESETS[resolvedVariant]
          : SPECULAR_PRESETS.outline

      return (
        <SpecularButton
          ref={ref}
          type={type}
          size={SPECULAR_SIZES[resolvedSize]}
          radius={SPECULAR_RADII[resolvedSize]}
          className={className}
          {...preset}
          {...specularProps}
          {...props}
        />
      )
    }

    const Comp = asChild ? Slot : 'button'
    return (
      <Comp
        className={cn(buttonVariants({ variant, size, className }))}
        ref={ref}
        type={asChild ? undefined : type}
        {...props}
      />
    )
  },
)
Button.displayName = 'Button'

// eslint-disable-next-line react-refresh/only-export-components
export { Button, buttonVariants, SpecularButton }
