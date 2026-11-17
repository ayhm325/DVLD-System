'use client'

import {
  forwardRef,
  useCallback,
  useEffect,
  useRef,
  useState,
  type CSSProperties,
  type HTMLAttributes,
  type ReactNode,
} from 'react'
import { cn } from '@/lib/utils'

export type SpecularCardVariant =
  | 'default'
  | 'blue'
  | 'cyan'
  | 'emerald'
  | 'amber'
  | 'rose'
  | 'slate'

export interface SpecularCardProps extends HTMLAttributes<HTMLDivElement> {
  children?: ReactNode
  /** Color theme for specular reflection */
  variant?: SpecularCardVariant
  /** Corner radius in pixels (default: 16) */
  radius?: number
  /** Whether the card is in an active/selected state */
  active?: boolean
  /** Spotlight intensity multiplier (default: 1) */
  intensity?: number
  /** Base idle glow intensity when pointer is away (0 to 1, default: 0.15) */
  idleIntensity?: number
  /** Spotlight radial radius in pixels (default: 320) */
  spotlightSize?: number
  /** Whether to render holographic iridescent reflection sheen (great for IDs & licenses) */
  holographic?: boolean
  /** Whether to enable pointer-following specular highlight (default: true) */
  followPointer?: boolean
}

interface VariantColorConfig {
  borderSpot: string
  surfaceSpot: string
  baseBorder: string
  activeBorder: string
  activeShadow: string
}

const VARIANT_COLORS: Record<SpecularCardVariant, VariantColorConfig> = {
  default: {
    borderSpot: 'rgba(96, 165, 250, 0.85)',
    surfaceSpot: 'rgba(59, 130, 246, 0.07)',
    baseBorder: 'rgba(38, 55, 80, 0.75)',
    activeBorder: 'rgba(96, 165, 250, 0.95)',
    activeShadow: '0 0 24px -2px rgba(59, 130, 246, 0.35)',
  },
  blue: {
    borderSpot: 'rgba(96, 165, 250, 0.85)',
    surfaceSpot: 'rgba(59, 130, 246, 0.07)',
    baseBorder: 'rgba(38, 55, 80, 0.75)',
    activeBorder: 'rgba(96, 165, 250, 0.95)',
    activeShadow: '0 0 24px -2px rgba(59, 130, 246, 0.35)',
  },
  cyan: {
    borderSpot: 'rgba(34, 211, 238, 0.9)',
    surfaceSpot: 'rgba(34, 211, 238, 0.08)',
    baseBorder: 'rgba(27, 49, 84, 0.8)',
    activeBorder: 'rgba(34, 211, 238, 0.95)',
    activeShadow: '0 0 24px -2px rgba(34, 211, 238, 0.35)',
  },
  emerald: {
    borderSpot: 'rgba(52, 211, 153, 0.9)',
    surfaceSpot: 'rgba(16, 185, 129, 0.08)',
    baseBorder: 'rgba(6, 78, 59, 0.7)',
    activeBorder: 'rgba(52, 211, 153, 0.95)',
    activeShadow: '0 0 24px -2px rgba(16, 185, 129, 0.35)',
  },
  amber: {
    borderSpot: 'rgba(251, 191, 36, 0.9)',
    surfaceSpot: 'rgba(245, 158, 11, 0.08)',
    baseBorder: 'rgba(120, 53, 15, 0.7)',
    activeBorder: 'rgba(251, 191, 36, 0.95)',
    activeShadow: '0 0 24px -2px rgba(245, 158, 11, 0.35)',
  },
  rose: {
    borderSpot: 'rgba(248, 113, 113, 0.9)',
    surfaceSpot: 'rgba(239, 68, 68, 0.08)',
    baseBorder: 'rgba(136, 19, 55, 0.7)',
    activeBorder: 'rgba(248, 113, 113, 0.95)',
    activeShadow: '0 0 24px -2px rgba(239, 68, 68, 0.35)',
  },
  slate: {
    borderSpot: 'rgba(148, 163, 184, 0.8)',
    surfaceSpot: 'rgba(148, 163, 184, 0.06)',
    baseBorder: 'rgba(38, 55, 80, 0.75)',
    activeBorder: 'rgba(203, 213, 225, 0.9)',
    activeShadow: '0 0 24px -2px rgba(148, 163, 184, 0.25)',
  },
}

export const SpecularCard = forwardRef<HTMLDivElement, SpecularCardProps>(
  (
    {
      children,
      className,
      style,
      variant = 'default',
      radius = 16,
      active = false,
      intensity = 1,
      idleIntensity = 0.15,
      spotlightSize = 320,
      holographic = false,
      followPointer = true,
      onPointerMove,
      onPointerEnter,
      onPointerLeave,
      ...props
    },
    ref,
  ) => {
    const cardRef = useRef<HTMLDivElement | null>(null)
    const [mousePos, setMousePos] = useState({ x: -1000, y: -1000 })
    const [isHovered, setIsHovered] = useState(false)
    const [reducedMotion, setReducedMotion] = useState(() => {
      if (typeof window === 'undefined') return false
      return window.matchMedia('(prefers-reduced-motion: reduce)').matches
    })

    useEffect(() => {
      if (typeof window === 'undefined') return
      const mq = window.matchMedia('(prefers-reduced-motion: reduce)')
      const listener = (e: MediaQueryListEvent) => setReducedMotion(e.matches)
      mq.addEventListener('change', listener)
      return () => mq.removeEventListener('change', listener)
    }, [])

    const handlePointerMove = useCallback(
      (e: React.PointerEvent<HTMLDivElement>) => {
        onPointerMove?.(e)
        if (reducedMotion || !followPointer) return
        const rect = e.currentTarget.getBoundingClientRect()
        setMousePos({
          x: e.clientX - rect.left,
          y: e.clientY - rect.top,
        })
      },
      [onPointerMove, reducedMotion, followPointer],
    )

    const handlePointerEnter = useCallback(
      (e: React.PointerEvent<HTMLDivElement>) => {
        onPointerEnter?.(e)
        setIsHovered(true)
      },
      [onPointerEnter],
    )

    const handlePointerLeave = useCallback(
      (e: React.PointerEvent<HTMLDivElement>) => {
        onPointerLeave?.(e)
        setIsHovered(false)
      },
      [onPointerLeave],
    )

    const colors = VARIANT_COLORS[variant] ?? VARIANT_COLORS.default
    const currentOpacity = isHovered
      ? intensity
      : active
        ? Math.max(idleIntensity, 0.45)
        : idleIntensity

    return (
      <div
        ref={(node) => {
          cardRef.current = node
          if (typeof ref === 'function') ref(node)
          else if (ref) ref.current = node
        }}
        onPointerMove={handlePointerMove}
        onPointerEnter={handlePointerEnter}
        onPointerLeave={handlePointerLeave}
        className={cn(
          'group relative isolate overflow-hidden transition-[box-shadow,border-color,transform] duration-300',
          className,
        )}
        style={
          {
            borderRadius: `${radius}px`,
            borderColor: active ? colors.activeBorder : colors.baseBorder,
            boxShadow: active ? colors.activeShadow : undefined,
            ...style,
          } as CSSProperties
        }
        {...props}
      >
        {/* 1. Specular Border Spotlight Layer */}
        {!reducedMotion && (
          <div
            className="pointer-events-none absolute -inset-px rounded-[inherit] transition-opacity duration-300 z-[1]"
            style={{
              opacity: currentOpacity,
              background: `radial-gradient(${spotlightSize}px circle at ${mousePos.x}px ${mousePos.y}px, ${colors.borderSpot}, transparent 70%)`,
              WebkitMask:
                'linear-gradient(#fff 0 0) content-box, linear-gradient(#fff 0 0)',
              WebkitMaskComposite: 'xor',
              maskComposite: 'exclude',
              padding: '1.5px',
            }}
            aria-hidden="true"
          />
        )}

        {/* 2. Specular Surface Ambient Glow */}
        {!reducedMotion && (
          <div
            className="pointer-events-none absolute inset-0 rounded-[inherit] transition-opacity duration-500 z-[1]"
            style={{
              opacity: currentOpacity * 0.9,
              background: `radial-gradient(${spotlightSize * 1.5}px circle at ${mousePos.x}px ${mousePos.y}px, ${colors.surfaceSpot}, transparent 70%)`,
            }}
            aria-hidden="true"
          />
        )}

        {/* 3. Optional Holographic Security Sheen for Smartcard/Credential badges */}
        {holographic && (
          <div
            className="pointer-events-none absolute -inset-full opacity-[0.06] group-hover:opacity-[0.14] transition-opacity duration-700 mix-blend-color-dodge bg-gradient-to-tr from-cyan-400 via-fuchsia-400 via-amber-300 to-emerald-400 -rotate-45 z-[1]"
            style={{
              transform: `translate(${mousePos.x * 0.05}px, ${mousePos.y * 0.05}px) rotate(-45deg)`,
            }}
            aria-hidden="true"
          />
        )}

        {children}
      </div>
    )
  },
)

SpecularCard.displayName = 'SpecularCard'

export default SpecularCard
