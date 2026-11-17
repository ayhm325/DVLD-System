'use client'

import {
  forwardRef,
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
  /** Base idle glow intensity when pointer is away (0 to 1, default: 0.35) */
  idleIntensity?: number
  /** Spotlight radial radius in pixels (default: 340) */
  spotlightSize?: number
  /** Proximity distance in pixels for orbital border steering (default: 260) */
  proximity?: number
  /** Whether to continuously orbit the border shimmer when idle (default: true) */
  orbital?: boolean
  /** Whether to render holographic iridescent reflection sheen (great for IDs & licenses) */
  holographic?: boolean
  /** Whether to enable pointer-following specular highlight (default: true) */
  followPointer?: boolean
}

interface VariantColorConfig {
  borderSpot: string
  borderSecondary: string
  surfaceSpot: string
  baseBorder: string
  hoverBorder: string
  activeBorder: string
  activeShadow: string
  hoverShadow: string
}

const VARIANT_COLORS: Record<SpecularCardVariant, VariantColorConfig> = {
  default: {
    borderSpot: 'rgba(147, 197, 253, 0.95)',
    borderSecondary: 'rgba(56, 189, 248, 0.65)',
    surfaceSpot: 'rgba(59, 130, 246, 0.12)',
    baseBorder: 'rgba(38, 58, 92, 0.78)',
    hoverBorder: 'rgba(96, 165, 250, 0.65)',
    activeBorder: 'rgba(96, 165, 250, 0.95)',
    activeShadow: '0 0 26px -2px rgba(59, 130, 246, 0.38)',
    hoverShadow: '0 14px 36px -10px rgba(2, 6, 23, 0.8), 0 0 24px -4px rgba(59, 130, 246, 0.25)',
  },
  blue: {
    borderSpot: 'rgba(147, 197, 253, 0.95)',
    borderSecondary: 'rgba(59, 130, 246, 0.7)',
    surfaceSpot: 'rgba(59, 130, 246, 0.13)',
    baseBorder: 'rgba(38, 58, 92, 0.78)',
    hoverBorder: 'rgba(96, 165, 250, 0.7)',
    activeBorder: 'rgba(96, 165, 250, 0.95)',
    activeShadow: '0 0 26px -2px rgba(59, 130, 246, 0.38)',
    hoverShadow: '0 14px 36px -10px rgba(2, 6, 23, 0.8), 0 0 24px -4px rgba(59, 130, 246, 0.28)',
  },
  cyan: {
    borderSpot: 'rgba(103, 232, 249, 0.95)',
    borderSecondary: 'rgba(34, 211, 238, 0.7)',
    surfaceSpot: 'rgba(34, 211, 238, 0.13)',
    baseBorder: 'rgba(27, 55, 92, 0.8)',
    hoverBorder: 'rgba(34, 211, 238, 0.7)',
    activeBorder: 'rgba(34, 211, 238, 0.95)',
    activeShadow: '0 0 26px -2px rgba(34, 211, 238, 0.38)',
    hoverShadow: '0 14px 36px -10px rgba(2, 6, 23, 0.8), 0 0 24px -4px rgba(34, 211, 238, 0.28)',
  },
  emerald: {
    borderSpot: 'rgba(110, 231, 183, 0.95)',
    borderSecondary: 'rgba(16, 185, 129, 0.7)',
    surfaceSpot: 'rgba(16, 185, 129, 0.13)',
    baseBorder: 'rgba(16, 78, 62, 0.75)',
    hoverBorder: 'rgba(52, 211, 153, 0.7)',
    activeBorder: 'rgba(52, 211, 153, 0.95)',
    activeShadow: '0 0 26px -2px rgba(16, 185, 129, 0.38)',
    hoverShadow: '0 14px 36px -10px rgba(2, 6, 23, 0.8), 0 0 24px -4px rgba(16, 185, 129, 0.28)',
  },
  amber: {
    borderSpot: 'rgba(253, 224, 71, 0.95)',
    borderSecondary: 'rgba(245, 158, 11, 0.7)',
    surfaceSpot: 'rgba(245, 158, 11, 0.13)',
    baseBorder: 'rgba(120, 63, 18, 0.72)',
    hoverBorder: 'rgba(251, 191, 36, 0.7)',
    activeBorder: 'rgba(251, 191, 36, 0.95)',
    activeShadow: '0 0 26px -2px rgba(245, 158, 11, 0.38)',
    hoverShadow: '0 14px 36px -10px rgba(2, 6, 23, 0.8), 0 0 24px -4px rgba(245, 158, 11, 0.28)',
  },
  rose: {
    borderSpot: 'rgba(253, 164, 175, 0.95)',
    borderSecondary: 'rgba(244, 63, 94, 0.7)',
    surfaceSpot: 'rgba(244, 63, 94, 0.13)',
    baseBorder: 'rgba(136, 19, 55, 0.72)',
    hoverBorder: 'rgba(251, 113, 133, 0.7)',
    activeBorder: 'rgba(248, 113, 113, 0.95)',
    activeShadow: '0 0 26px -2px rgba(239, 68, 68, 0.38)',
    hoverShadow: '0 14px 36px -10px rgba(2, 6, 23, 0.8), 0 0 24px -4px rgba(244, 63, 94, 0.28)',
  },
  slate: {
    borderSpot: 'rgba(226, 232, 240, 0.9)',
    borderSecondary: 'rgba(148, 163, 184, 0.65)',
    surfaceSpot: 'rgba(148, 163, 184, 0.1)',
    baseBorder: 'rgba(51, 65, 85, 0.78)',
    hoverBorder: 'rgba(148, 163, 184, 0.65)',
    activeBorder: 'rgba(203, 213, 225, 0.9)',
    activeShadow: '0 0 24px -2px rgba(148, 163, 184, 0.28)',
    hoverShadow: '0 14px 36px -10px rgba(2, 6, 23, 0.8), 0 0 20px -4px rgba(148, 163, 184, 0.2)',
  },
}

const clamp = (v: number, min: number, max: number) =>
  Math.min(max, Math.max(min, v))

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
      idleIntensity = 0.38,
      spotlightSize = 340,
      proximity = 260,
      orbital = true,
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
    const orbitalRef = useRef<HTMLDivElement | null>(null)
    const spotBorderRef = useRef<HTMLDivElement | null>(null)
    const glareRef = useRef<HTMLDivElement | null>(null)
    const [isHovered, setIsHovered] = useState(false)
    const [reducedMotion, setReducedMotion] = useState(() => {
      if (
        typeof window === 'undefined' ||
        typeof window.matchMedia !== 'function'
      )
        return false
      return window.matchMedia('(prefers-reduced-motion: reduce)').matches
    })

    useEffect(() => {
      if (
        typeof window === 'undefined' ||
        typeof window.matchMedia !== 'function'
      )
        return
      const mq = window.matchMedia('(prefers-reduced-motion: reduce)')
      const listener = (e: MediaQueryListEvent) => setReducedMotion(e.matches)
      mq.addEventListener('change', listener)
      return () => mq.removeEventListener('change', listener)
    }, [])

    // Dynamic Orbital Rim Shimmer + Proximity Cursor-Steering Engine (modeled on SpecularButton)
    useEffect(() => {
      const card = cardRef.current
      if (!card || reducedMotion) return

      let raf = 0
      let last = 0
      let visible = true
      let angle = 315 // degrees
      let level = idleIntensity
      const pointer = { x: -9999, y: -9999, known: false, inside: false }

      const updateDom = (
        localX: number,
        localY: number,
        currentAngle: number,
        currentLevel: number,
        nearFactor: number,
      ) => {
        if (orbitalRef.current) {
          orbitalRef.current.style.setProperty(
            '--sc-angle',
            `${currentAngle.toFixed(1)}deg`,
          )
          orbitalRef.current.style.opacity = String(
            clamp(currentLevel * intensity, 0, 1).toFixed(3),
          )
        }
        if (spotBorderRef.current) {
          spotBorderRef.current.style.setProperty(
            '--sc-x',
            `${localX.toFixed(1)}px`,
          )
          spotBorderRef.current.style.setProperty(
            '--sc-y',
            `${localY.toFixed(1)}px`,
          )
          spotBorderRef.current.style.opacity = String(
            clamp(nearFactor * intensity, 0, 1).toFixed(3),
          )
        }
        if (glareRef.current) {
          glareRef.current.style.setProperty(
            '--sc-x',
            `${localX.toFixed(1)}px`,
          )
          glareRef.current.style.setProperty(
            '--sc-y',
            `${localY.toFixed(1)}px`,
          )
          glareRef.current.style.opacity = String(
            clamp(
              (pointer.inside ? 0.95 : nearFactor * 0.55) * intensity,
              0,
              1,
            ).toFixed(3),
          )
        }
      }

      const tick = (now: number) => {
        raf = 0
        if (!visible) return
        const dt = last ? Math.min(0.05, (now - last) / 1000) : 1 / 60
        last = now

        const rect = card.getBoundingClientRect()
        const halfW = rect.width / 2
        const halfH = rect.height / 2
        const centerX = rect.left + halfW
        const centerY = rect.top + halfH

        let near = 0
        let localX = rect.width * 0.2
        let localY = rect.height * 0.2
        let targetAngle = angle

        if (followPointer && pointer.known) {
          localX = pointer.x - rect.left
          localY = pointer.y - rect.top
          const dx = pointer.x - centerX
          const dy = pointer.y - centerY
          const distOutX = Math.max(Math.abs(dx) - halfW, 0)
          const distOutY = Math.max(Math.abs(dy) - halfH, 0)
          const distance = Math.hypot(distOutX, distOutY)
          const t = clamp(1 - distance / Math.max(1, proximity), 0, 1)
          near = t * t * (3 - 2 * t)
          pointer.inside = distance === 0

          if (near > 0) {
            const rad = Math.atan2(dy, dx)
            targetAngle = ((rad * 180) / Math.PI + 90 + 360) % 360
          }
        }

        if (near > 0.02) {
          // Smooth shortest-arc angular interpolation toward the cursor
          const diff = ((targetAngle - angle + 540) % 360) - 180
          angle = (angle + diff * (1 - Math.exp(-dt * 9)) + 360) % 360
        } else if (orbital) {
          // Gentle continuous orbital rotation along the card perimeter
          angle = (angle + dt * 28) % 360
        }

        const targetLevel =
          idleIntensity + (1 - idleIntensity) * (active ? Math.max(near, 0.55) : near)
        level += (targetLevel - level) * (1 - Math.exp(-dt * 7))

        updateDom(localX, localY, angle, level, near)

        if (orbital || near > 0.005 || Math.abs(targetLevel - level) > 0.002) {
          raf = requestAnimationFrame(tick)
        } else {
          last = 0
        }
      }

      const wake = () => {
        if (!raf && visible && typeof requestAnimationFrame === 'function') {
          raf = requestAnimationFrame(tick)
        }
      }

      const handleWinPointerMove = (e: PointerEvent) => {
        pointer.x = e.clientX
        pointer.y = e.clientY
        pointer.known = true
        wake()
      }

      const handleDocLeave = () => {
        pointer.known = false
        pointer.inside = false
        wake()
      }

      let intersection: IntersectionObserver | null = null
      if (typeof IntersectionObserver !== 'undefined') {
        intersection = new IntersectionObserver((entries) => {
          visible = entries.some((entry) => entry.isIntersecting)
          if (visible) {
            last = 0
            wake()
          }
        })
        intersection.observe(card)
      }

      window.addEventListener('pointermove', handleWinPointerMove, {
        passive: true,
      })
      document.documentElement.addEventListener('pointerleave', handleDocLeave)
      wake()

      return () => {
        if (typeof cancelAnimationFrame === 'function') {
          cancelAnimationFrame(raf)
        }
        intersection?.disconnect()
        window.removeEventListener('pointermove', handleWinPointerMove)
        document.documentElement.removeEventListener(
          'pointerleave',
          handleDocLeave,
        )
      }
    }, [
      reducedMotion,
      followPointer,
      orbital,
      proximity,
      idleIntensity,
      intensity,
      active,
    ])

    const colors = VARIANT_COLORS[variant] ?? VARIANT_COLORS.default

    return (
      <div
        ref={(node) => {
          cardRef.current = node
          if (typeof ref === 'function') ref(node)
          else if (ref) ref.current = node
        }}
        onPointerMove={onPointerMove}
        onPointerEnter={(e) => {
          onPointerEnter?.(e)
          setIsHovered(true)
        }}
        onPointerLeave={(e) => {
          onPointerLeave?.(e)
          setIsHovered(false)
        }}
        className={cn(
          'group relative isolate overflow-hidden transition-[box-shadow,border-color,transform] duration-300',
          className,
        )}
        style={
          {
            borderRadius: `${radius}px`,
            borderColor: active
              ? colors.activeBorder
              : isHovered
                ? colors.hoverBorder
                : colors.baseBorder,
            boxShadow: active
              ? colors.activeShadow
              : isHovered
                ? colors.hoverShadow
                : undefined,
            ...style,
          } as CSSProperties
        }
        {...props}
      >
        {/* 1. Dynamic Orbital Dual-Lobe Rim Shimmer (SpecularButton-style perimeter loop) */}
        {!reducedMotion && (
          <div
            ref={orbitalRef}
            className="pointer-events-none absolute inset-0 rounded-[inherit] z-20"
            style={{
              border: '1.5px solid transparent',
              background: `conic-gradient(from var(--sc-angle, 315deg) at 50% 50%, transparent 0deg, ${colors.borderSecondary} 24deg, #ffffff 42deg, ${colors.borderSpot} 60deg, transparent 88deg, transparent 190deg, ${colors.borderSecondary} 215deg, rgba(255,255,255,0.7) 228deg, ${colors.borderSpot} 240deg, transparent 268deg, transparent 360deg) border-box`,
              WebkitMask:
                'linear-gradient(#fff 0 0) padding-box, linear-gradient(#fff 0 0)',
              WebkitMaskComposite: 'destination-out',
              maskComposite: 'exclude',
              opacity: idleIntensity,
            }}
            aria-hidden="true"
          />
        )}

        {/* 2. Laser-Sharp Proximity & Hover Cursor-Follow Border Spotlight */}
        {!reducedMotion && (
          <div
            ref={spotBorderRef}
            className="pointer-events-none absolute inset-0 rounded-[inherit] z-20"
            style={{
              border: '2px solid transparent',
              background: `radial-gradient(${spotlightSize}px circle at var(--sc-x, -999px) var(--sc-y, -999px), #ffffff 0%, ${colors.borderSpot} 35%, transparent 75%) border-box`,
              WebkitMask:
                'linear-gradient(#fff 0 0) padding-box, linear-gradient(#fff 0 0)',
              WebkitMaskComposite: 'destination-out',
              maskComposite: 'exclude',
              opacity: 0,
            }}
            aria-hidden="true"
          />
        )}

        {/* 3. Glass Surface Specular Glare / Hover Light Pool */}
        {!reducedMotion && (
          <div
            ref={glareRef}
            className="pointer-events-none absolute inset-0 rounded-[inherit] z-10"
            style={{
              opacity: 0,
              background: `radial-gradient(${Math.round(spotlightSize * 1.3)}px circle at var(--sc-x, -999px) var(--sc-y, -999px), rgba(255, 255, 255, 0.15) 0%, ${colors.surfaceSpot} 42%, transparent 76%)`,
            }}
            aria-hidden="true"
          />
        )}

        {/* 4. Optional Holographic Security Sheen for Smartcard/Credential badges */}
        {holographic && (
          <div
            className="pointer-events-none absolute -inset-full opacity-[0.06] group-hover:opacity-[0.14] transition-opacity duration-700 mix-blend-color-dodge bg-gradient-to-tr from-cyan-400 via-fuchsia-400 via-amber-300 to-emerald-400 -rotate-45 z-[1]"
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
