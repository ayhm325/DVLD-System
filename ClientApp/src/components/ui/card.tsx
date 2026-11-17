import * as React from 'react'
import { cn } from '@/lib/utils'
import SpecularCard, {
  type SpecularCardProps,
  type SpecularCardVariant,
} from './SpecularCard'

export interface CardProps extends React.HTMLAttributes<HTMLDivElement> {
  /** When true, wraps the card in a specular border reflection container */
  specular?: boolean
  specularVariant?: SpecularCardVariant
  specularProps?: Partial<SpecularCardProps>
}

const Card = React.forwardRef<HTMLDivElement, CardProps>(
  (
    {
      className,
      specular = true,
      specularVariant = 'default',
      specularProps,
      ...props
    },
    ref,
  ) => {
    if (specular) {
      return (
        <SpecularCard
          ref={ref}
          variant={specularVariant}
          radius={16}
          intensity={0.65}
          idleIntensity={0.12}
          spotlightSize={340}
          className={cn(
            'w-full max-w-full min-w-0 rounded-2xl border border-blue-500/30 bg-[#0d182e]/90 shadow-[0_0_25px_rgba(0,140,255,0.08)] backdrop-blur-md text-white',
            className,
          )}
          {...specularProps}
          {...props}
        />
      )
    }

    return (
      <div
        ref={ref}
        className={cn(
          'w-full max-w-full min-w-0 rounded-2xl border border-blue-500/30 bg-[#0d182e]/90 shadow-[0_0_25px_rgba(0,140,255,0.08)] backdrop-blur-md text-white',
          className,
        )}
        {...props}
      />
    )
  },
)
Card.displayName = 'Card'

const CardHeader = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div
      ref={ref}
      className={cn('flex min-w-0 flex-col space-y-1.5 px-4 py-4 sm:px-6 sm:py-5', className)}
      {...props}
    />
  ),
)
CardHeader.displayName = 'CardHeader'

const CardTitle = React.forwardRef<HTMLHeadingElement, React.HTMLAttributes<HTMLHeadingElement>>(
  ({ className, ...props }, ref) => (
    <h3
      ref={ref}
      className={cn('min-w-0 whitespace-normal break-words text-lg font-bold text-white tracking-tight leading-tight', className)}
      {...props}
    />
  ),
)
CardTitle.displayName = 'CardTitle'

const CardDescription = React.forwardRef<HTMLParagraphElement, React.HTMLAttributes<HTMLParagraphElement>>(
  ({ className, ...props }, ref) => (
    <p
      ref={ref}
      className={cn('min-w-0 whitespace-normal break-words text-xs text-slate-400 leading-relaxed', className)}
      {...props}
    />
  ),
)
CardDescription.displayName = 'CardDescription'

const CardContent = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div ref={ref} className={cn('min-w-0 p-4 pt-0 sm:p-6 sm:pt-0', className)} {...props} />
  ),
)
CardContent.displayName = 'CardContent'

const CardFooter = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div
      ref={ref}
      className={cn('flex min-w-0 flex-wrap items-center gap-3 px-4 pb-4 pt-0 sm:px-6 sm:pb-6', className)}
      {...props}
    />
  ),
)
CardFooter.displayName = 'CardFooter'

export {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
  CardFooter,
  SpecularCard,
}
