import type { ReactNode } from "react";
import { SpecularCard } from "@/components/ui/card";
import type { SpecularCardVariant } from "@/components/ui/SpecularCard";
import { cn } from "@/lib/utils";

export interface UnifiedKpiCardProps {
  id: string;
  label: string;
  value: string;
  description: string;
  icon: ReactNode;
  variant: SpecularCardVariant;
  iconToneClass: string;
  topAccentClass: string;
  badge: ReactNode;
  loading?: boolean;
}

/**
 * Unified Dark Enterprise KPI Card built on SpecularCard.
 * Matches the exact surface, border, and orbital border shimmer of the analytical cards below.
 */
export default function UnifiedKpiCard({
  label,
  value,
  description,
  icon,
  variant,
  iconToneClass,
  topAccentClass,
  badge,
  loading = false,
}: UnifiedKpiCardProps) {
  return (
    <SpecularCard
      variant={variant}
      radius={16}
      className="flex flex-col justify-between border border-slate-800/90 bg-[#0e182a]/95 p-5 sm:p-6 shadow-lg hover:-translate-y-0.5"
    >
      {/* Subtle top semantic accent line */}
      <div
        className={cn(
          "pointer-events-none absolute inset-x-0 top-0 h-[2px] opacity-80",
          topAccentClass,
        )}
        aria-hidden="true"
      />

      {/* Top Row: Icon Box + Title + Status Badge */}
      <div className="relative z-10 flex items-center justify-between gap-3">
        <div className="flex items-center gap-3 min-w-0">
          <div
            className={cn(
              "flex size-11 shrink-0 items-center justify-center rounded-xl border transition-transform duration-300 group-hover:scale-105",
              iconToneClass,
            )}
            aria-hidden="true"
          >
            {icon}
          </div>
          <h3 className="text-xs sm:text-sm font-bold uppercase tracking-wider text-slate-100 leading-snug">
            {label}
          </h3>
        </div>
        <div className="shrink-0">{badge}</div>
      </div>

      {/* Main Counter + Clear Definition */}
      <div className="relative z-10 mt-5">
        {loading ? (
          <div className="h-10 w-28 rounded-lg bg-slate-800/80 animate-pulse border border-slate-700/60" />
        ) : (
          <strong className="block text-3xl sm:text-4xl font-extrabold text-white tracking-tight tabular-nums leading-none">
            {value}
          </strong>
        )}
        <p className="mt-2.5 text-xs sm:text-[13px] text-slate-300/90 leading-relaxed">
          {description}
        </p>
      </div>
    </SpecularCard>
  );
}
