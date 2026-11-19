import { useId, type CSSProperties, type ReactNode } from "react";
import { cn } from "@/lib/utils";
import { useStatCardEffects } from "./hooks/useStatCardEffects";
import {
  resolveStatCardVariant,
  type StatCardVariant,
} from "./statCardVariants";

export type { StatCardVariant };

export interface StatCardProps {
  /** Primary metric title */
  title?: string;
  /** Backward-compatible alias for title */
  label?: string;
  /** The numerical or formatted metric value */
  value: ReactNode;
  /** Descriptive caption or context */
  description?: string;
  /** Primary Lucide icon placed in the glowing container */
  icon?: ReactNode;
  /** Custom CSS properties for the icon box */
  iconStyle?: CSSProperties;
  /** Additional container classes */
  className?: string;
  /** Inline container style overrides */
  style?: CSSProperties;
  /** Color theme variant */
  variant?: StatCardVariant;
  /** Optional bottom-right badge element or string */
  badge?: ReactNode;
  /** Optional footer content rendered below the main metric row */
  footer?: ReactNode;
  /** Whether the metric is currently loading / updating */
  loading?: boolean;
  /** Whether to render the decorative neon waveform SVG (defaults to true) */
  showWaveform?: boolean;
  /** Optional click handler */
  onClick?: () => void;
  /** Whether this interactive card is currently selected/active */
  active?: boolean;
  /** When false, renders a calm Dark Enterprise card instead of high-glow neon (defaults to true) */
  neon?: boolean;
}

/**
 * Global Premium Neon Statistics Card.
 * Renders consistent deep-navy tinted backgrounds, illuminated 360-degree neon borders,
 * prominent rounded-square glowing icon containers, high-contrast metrics, and multi-peak neon waveforms.
 */
export default function StatCard({
  title,
  label,
  value,
  description,
  icon,
  iconStyle,
  className = "",
  style,
  variant = "blue",
  badge,
  footer,
  loading = false,
  showWaveform = true,
  onClick,
  active,
  neon = true,
}: StatCardProps) {
  const filterUniqueId = useId().replace(/:/g, "");
  const {
    cardRef,
    orbitalRef,
    isHovered,
    reducedMotion,
    dynamicTransform,
    handleMouseMove,
    handleMouseEnter,
    handleMouseLeave,
  } = useStatCardEffects();

  const { config, enterpriseIconTone } = resolveStatCardVariant(variant);
  const titleText = title ?? label ?? "";
  const resolvedBadge = badge ?? (active ? "Active" : undefined);

  const cardStyle: CSSProperties = neon
    ? {
        background: config.background,
        borderColor: isHovered ? config.borderHoverColor : config.borderColor,
        boxShadow: isHovered ? config.shadowHover : config.shadow,
        transform: dynamicTransform,
        ...style,
      }
    : {
        background: "linear-gradient(180deg, #101b30 0%, #0b1322 100%)",
        borderColor: isHovered
          ? config.borderHoverColor
          : "rgba(148, 163, 184, 0.24)",
        boxShadow: isHovered
          ? "0 14px 36px -10px rgba(2, 6, 23, 0.85), 0 0 22px -4px rgba(59, 130, 246, 0.28)"
          : "0 10px 28px -12px rgba(2, 6, 23, 0.75)",
        transform: dynamicTransform,
        ...style,
      };

  const iconBoxStyle: CSSProperties = neon
    ? {
        background: config.iconBg,
        border: config.iconBorder,
        boxShadow: isHovered ? config.iconShadowHover : config.iconShadow,
        ...iconStyle,
      }
    : {
        background: enterpriseIconTone.bg,
        border: enterpriseIconTone.border,
        color: enterpriseIconTone.text,
        boxShadow: isHovered ? config.iconShadow : "none",
        ...iconStyle,
      };

  return (
    <article
      ref={cardRef}
      className={cn(
        "stat-card group relative flex min-h-34 min-w-0 flex-col justify-between overflow-hidden rounded-2xl border transition-all duration-200 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-300",
        neon ? "border-[1.5px]" : "border p-5",
        onClick ? "cursor-pointer" : "cursor-default",
        active && "ring-2 ring-blue-400/80 shadow-[0_0_24px_rgba(59,130,246,0.3)]",
        className,
      )}
      style={cardStyle}
      role={onClick ? "button" : undefined}
      aria-pressed={onClick && active !== undefined ? active : undefined}
      tabIndex={onClick ? 0 : undefined}
      onMouseMove={handleMouseMove}
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
      onClick={onClick}
      onKeyDown={
        onClick
          ? (event) => {
              if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                onClick();
              }
            }
          : undefined
      }
    >
      {/* 0. Dynamic Orbital Dual-Lobe Rim Shimmer (SpecularButton-style perimeter loop) */}
      {!reducedMotion && (
        <div
          ref={orbitalRef}
          className="pointer-events-none absolute inset-0 rounded-[inherit] z-20"
          style={{
            border: "1.5px solid transparent",
            background: `conic-gradient(from var(--stat-orbital-angle, 315deg) at 50% 50%, transparent 0deg, ${config.waveStroke} 24deg, #ffffff 42deg, ${config.waveHighlight} 60deg, transparent 88deg, transparent 190deg, ${config.waveStroke} 215deg, rgba(255,255,255,0.75) 228deg, ${config.waveHighlight} 240deg, transparent 268deg, transparent 360deg) border-box`,
            WebkitMask:
              "linear-gradient(#fff 0 0) padding-box, linear-gradient(#fff 0 0)",
            WebkitMaskComposite: "destination-out",
            maskComposite: "exclude",
            opacity: 0.42,
          }}
          aria-hidden="true"
        />
      )}

      {/* 1. Laser-Sharp Specular Border Spotlight */}
      {!reducedMotion && (
        <div
          className="pointer-events-none absolute inset-0 rounded-[inherit] transition-opacity duration-200 z-20"
          style={{
            border: "2px solid transparent",
            background: `radial-gradient(240px circle at var(--stat-mouse-x, 100px) var(--stat-mouse-y, 50px), #ffffff 0%, ${config.waveHighlight} 35%, transparent 75%) border-box`,
            WebkitMask:
              "linear-gradient(#fff 0 0) padding-box, linear-gradient(#fff 0 0)",
            WebkitMaskComposite: "destination-out",
            maskComposite: "exclude",
            opacity: isHovered ? 1 : active ? 0.75 : 0,
          }}
          aria-hidden="true"
        />
      )}

      {/* 2. Glass Surface Specular Glare / Light Pool */}
      {!reducedMotion && (
        <div
          className="pointer-events-none absolute inset-0 rounded-[inherit] transition-opacity duration-300 z-10"
          style={{
            opacity: isHovered ? 0.85 : active ? 0.45 : 0,
            background: `radial-gradient(350px circle at var(--stat-mouse-x, 100px) var(--stat-mouse-y, 50px), rgba(255, 255, 255, 0.18) 0%, ${config.waveGlow} 40%, transparent 75%)`,
          }}
          aria-hidden="true"
        />
      )}

      {/* Top Border Accent */}
      <div
        className={cn(
          "absolute inset-x-0 top-0 bg-linear-to-r transition-all duration-300",
          neon
            ? "h-[2.5px] opacity-95 group-hover:opacity-100 shadow-sm"
            : "h-[2px] opacity-75",
          config.accentGradient,
        )}
        aria-hidden="true"
      />

      {/* Prominent Multi-Peak Neon Waveform Activity SVG on the right */}
      {showWaveform && (
        <svg
          className="stat-card-wave pointer-events-none absolute right-2.5 top-1/2 -translate-y-1/2 h-16 w-36 sm:w-44 opacity-80 group-hover:opacity-100 transition-opacity duration-300 select-none overflow-visible"
          xmlns="http://www.w3.org/2000/svg"
          viewBox="0 0 200 60"
          preserveAspectRatio="none"
          aria-hidden="true"
        >
          <defs>
            <filter
              id={`wave-glow-${filterUniqueId}`}
              x="-20%"
              y="-20%"
              width="140%"
              height="140%"
            >
              <feGaussianBlur stdDeviation="3.5" result="coloredBlur" />
              <feMerge>
                <feMergeNode in="coloredBlur" />
                <feMergeNode in="SourceGraphic" />
              </feMerge>
            </filter>
          </defs>
          {/* Ambient blurred glow curve */}
          <path
            d="M 0 38 C 22 38, 32 50, 50 32 C 68 15, 82 44, 100 26 C 118 8, 132 38, 150 18 C 164 4, 178 30, 190 20 C 195 16, 198 22, 200 22"
            fill="none"
            stroke={config.waveStroke}
            strokeWidth="4"
            strokeLinecap="round"
            strokeLinejoin="round"
            opacity="0.55"
            filter={`url(#wave-glow-${filterUniqueId})`}
          />
          {/* Sharp bright neon curve */}
          <path
            d="M 0 38 C 22 38, 32 50, 50 32 C 68 15, 82 44, 100 26 C 118 8, 132 38, 150 18 C 164 4, 178 30, 190 20 C 195 16, 198 22, 200 22"
            fill="none"
            stroke={config.waveHighlight}
            strokeWidth="2.4"
            strokeLinecap="round"
            strokeLinejoin="round"
            opacity="0.95"
          />
        </svg>
      )}

      {/* Main Content Layout: Icon Box on Left, Metrics in Middle */}
      <div className="stat-card-content relative z-10 flex min-w-0 items-start gap-4">
        {icon && (
          <div
            className={cn(
              "stat-icon flex shrink-0 items-center justify-center rounded-2xl transition-transform duration-300 [&>svg]:size-6 [&>svg]:stroke-[2]",
              neon
                ? "text-white group-hover:scale-105 [&>svg]:text-white"
                : "size-12",
            )}
            style={iconBoxStyle}
            aria-hidden="true"
          >
            {icon}
          </div>
        )}

        <div className="stat-card-copy min-w-0 flex-1">
          <div className="flex items-center justify-between gap-2">
            <span className="stat-card-title block text-[12px] font-bold uppercase tracking-wider text-slate-300 group-hover:text-white transition-colors">
              {titleText}
            </span>
            {!neon && resolvedBadge && (
              <div className="shrink-0">
                {typeof resolvedBadge === "string" ? (
                  <span
                    className={cn(
                      "inline-flex items-center text-[11px] font-bold px-2.5 py-0.5 rounded-full border",
                      config.badgeClass,
                    )}
                  >
                    {resolvedBadge}
                  </span>
                ) : (
                  resolvedBadge
                )}
              </div>
            )}
          </div>

          <div className="mt-1.5">
            {loading ? (
              <div className="h-9 w-24 rounded-lg bg-slate-800/80 animate-pulse border border-slate-700/60" />
            ) : (
              <strong
                className={cn(
                  "stat-card-value block font-black text-white tracking-tight leading-tight tabular-nums",
                  !neon && "text-2xl sm:text-3xl",
                )}
              >
                {value}
              </strong>
            )}
          </div>

          {description && (
            <p className="stat-card-description mt-1 text-xs font-medium text-slate-400 group-hover:text-slate-300 transition-colors">
              {description}
            </p>
          )}
        </div>
      </div>

      {/* Optional Footer (Enterprise analytical context / progress bar) */}
      {footer && (
        <div className="relative z-10 mt-3.5 border-t border-slate-800/90 pt-3">
          {footer}
        </div>
      )}

      {/* Bottom Right Badge (when neon mode is active) */}
      {neon && resolvedBadge && (
        <div className="relative z-10 self-end mt-2">
          {typeof resolvedBadge === "string" ? (
            <span
              className={cn(
                "inline-flex items-center text-[11px] font-bold px-2.5 py-0.5 rounded-full border",
                config.badgeClass,
              )}
            >
              {resolvedBadge}
            </span>
          ) : (
            resolvedBadge
          )}
        </div>
      )}
    </article>
  );
}

export { StatCard, StatCard as MetricCard };
