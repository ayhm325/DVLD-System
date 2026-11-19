export type StatCardVariant =
  | "blue"
  | "green"
  | "emerald"
  | "purple"
  | "violet"
  | "amber"
  | "gold"
  | "rose"
  | "slate"
  | "default";

export type NormalizedStatCardVariant =
  | "blue"
  | "emerald"
  | "violet"
  | "amber"
  | "rose"
  | "slate";

export interface StatCardVariantDefinition {
  background: string;
  borderColor: string;
  borderHoverColor: string;
  shadow: string;
  shadowHover: string;
  iconBg: string;
  iconBorder: string;
  iconShadow: string;
  iconShadowHover: string;
  waveStroke: string;
  waveHighlight: string;
  waveGlow: string;
  accentGradient: string;
  badgeClass: string;
}

export interface EnterpriseIconTone {
  bg: string;
  border: string;
  text: string;
}

export interface ResolvedStatCardVariant {
  normalized: NormalizedStatCardVariant;
  config: StatCardVariantDefinition;
  enterpriseIconTone: EnterpriseIconTone;
}

/**
 * Normalizes public StatCard color aliases into canonical theme tokens.
 */
export function normalizeStatCardVariant(
  variant: StatCardVariant = "blue",
): NormalizedStatCardVariant {
  switch (variant) {
    case "green":
    case "emerald":
      return "emerald";
    case "purple":
    case "violet":
      return "violet";
    case "amber":
    case "gold":
      return "amber";
    case "rose":
      return "rose";
    case "slate":
      return "slate";
    case "blue":
    case "default":
    default:
      return "blue";
  }
}

export const STAT_CARD_VARIANT_CONFIG: Record<
  NormalizedStatCardVariant,
  StatCardVariantDefinition
> = {
  blue: {
    background:
      "radial-gradient(circle at 85% 45%, rgba(59, 130, 246, 0.35) 0%, transparent 60%), radial-gradient(circle at 15% 25%, rgba(37, 99, 235, 0.4) 0%, transparent 55%), linear-gradient(135deg, #071530 0%, #0d224d 50%, #071328 100%)",
    borderColor: "rgba(59, 130, 246, 0.65)",
    borderHoverColor: "rgba(96, 165, 250, 0.95)",
    shadow:
      "0 0 24px -2px rgba(59, 130, 246, 0.38), 0 8px 30px rgba(0, 0, 0, 0.5), inset 0 1px 1px rgba(255, 255, 255, 0.25), inset 0 0 20px rgba(59, 130, 246, 0.12)",
    shadowHover:
      "0 0 36px 0px rgba(59, 130, 246, 0.6), 0 12px 36px rgba(0, 0, 0, 0.65), inset 0 1px 2px rgba(255, 255, 255, 0.4), inset 0 0 28px rgba(59, 130, 246, 0.2)",
    iconBg: "linear-gradient(135deg, #3b82f6 0%, #1d4ed8 100%)",
    iconBorder: "1.5px solid rgba(147, 197, 253, 0.6)",
    iconShadow:
      "0 0 20px rgba(59, 130, 246, 0.65), inset 0 1px 2px rgba(255, 255, 255, 0.45)",
    iconShadowHover:
      "0 0 26px rgba(59, 130, 246, 0.85), inset 0 1px 2px rgba(255, 255, 255, 0.6)",
    waveStroke: "#0284c7",
    waveHighlight: "#38bdf8",
    waveGlow: "rgba(56, 189, 248, 0.55)",
    accentGradient: "from-blue-500 via-cyan-400 to-indigo-500",
    badgeClass:
      "bg-blue-950/90 text-blue-200 border-blue-500/40 shadow-[0_0_10px_rgba(59,130,246,0.25)]",
  },
  emerald: {
    background:
      "radial-gradient(circle at 85% 45%, rgba(16, 185, 129, 0.35) 0%, transparent 60%), radial-gradient(circle at 15% 25%, rgba(5, 150, 105, 0.4) 0%, transparent 55%), linear-gradient(135deg, #041c16 0%, #08382b 50%, #031812 100%)",
    borderColor: "rgba(16, 185, 129, 0.65)",
    borderHoverColor: "rgba(52, 211, 153, 0.95)",
    shadow:
      "0 0 24px -2px rgba(16, 185, 129, 0.38), 0 8px 30px rgba(0, 0, 0, 0.5), inset 0 1px 1px rgba(255, 255, 255, 0.25), inset 0 0 20px rgba(16, 185, 129, 0.12)",
    shadowHover:
      "0 0 36px 0px rgba(16, 185, 129, 0.6), 0 12px 36px rgba(0, 0, 0, 0.65), inset 0 1px 2px rgba(255, 255, 255, 0.4), inset 0 0 28px rgba(16, 185, 129, 0.2)",
    iconBg: "linear-gradient(135deg, #10b981 0%, #047857 100%)",
    iconBorder: "1.5px solid rgba(110, 231, 183, 0.6)",
    iconShadow:
      "0 0 20px rgba(16, 185, 129, 0.65), inset 0 1px 2px rgba(255, 255, 255, 0.45)",
    iconShadowHover:
      "0 0 26px rgba(16, 185, 129, 0.85), inset 0 1px 2px rgba(255, 255, 255, 0.6)",
    waveStroke: "#059669",
    waveHighlight: "#34d399",
    waveGlow: "rgba(52, 211, 153, 0.55)",
    accentGradient: "from-emerald-500 via-teal-400 to-green-500",
    badgeClass:
      "bg-emerald-950/90 text-emerald-200 border-emerald-500/40 shadow-[0_0_10px_rgba(16,185,129,0.25)]",
  },
  violet: {
    background:
      "radial-gradient(circle at 85% 45%, rgba(168, 85, 247, 0.35) 0%, transparent 60%), radial-gradient(circle at 15% 25%, rgba(124, 58, 237, 0.4) 0%, transparent 55%), linear-gradient(135deg, #170b2e 0%, #2b1458 50%, #120724 100%)",
    borderColor: "rgba(168, 85, 247, 0.65)",
    borderHoverColor: "rgba(192, 132, 252, 0.95)",
    shadow:
      "0 0 24px -2px rgba(168, 85, 247, 0.38), 0 8px 30px rgba(0, 0, 0, 0.5), inset 0 1px 1px rgba(255, 255, 255, 0.25), inset 0 0 20px rgba(168, 85, 247, 0.12)",
    shadowHover:
      "0 0 36px 0px rgba(168, 85, 247, 0.6), 0 12px 36px rgba(0, 0, 0, 0.65), inset 0 1px 2px rgba(255, 255, 255, 0.4), inset 0 0 28px rgba(168, 85, 247, 0.2)",
    iconBg: "linear-gradient(135deg, #a855f7 0%, #7c3aed 100%)",
    iconBorder: "1.5px solid rgba(216, 180, 254, 0.6)",
    iconShadow:
      "0 0 20px rgba(168, 85, 247, 0.65), inset 0 1px 2px rgba(255, 255, 255, 0.45)",
    iconShadowHover:
      "0 0 26px rgba(168, 85, 247, 0.85), inset 0 1px 2px rgba(255, 255, 255, 0.6)",
    waveStroke: "#7c3aed",
    waveHighlight: "#c084fc",
    waveGlow: "rgba(192, 132, 252, 0.55)",
    accentGradient: "from-purple-500 via-violet-400 to-indigo-500",
    badgeClass:
      "bg-purple-950/90 text-purple-200 border-purple-500/40 shadow-[0_0_10px_rgba(168,85,247,0.25)]",
  },
  amber: {
    background:
      "radial-gradient(circle at 85% 45%, rgba(245, 158, 11, 0.35) 0%, transparent 60%), radial-gradient(circle at 15% 25%, rgba(217, 119, 6, 0.4) 0%, transparent 55%), linear-gradient(135deg, #241403 0%, #3f2307 50%, #1c0e02 100%)",
    borderColor: "rgba(245, 158, 11, 0.65)",
    borderHoverColor: "rgba(251, 191, 36, 0.95)",
    shadow:
      "0 0 24px -2px rgba(245, 158, 11, 0.38), 0 8px 30px rgba(0, 0, 0, 0.5), inset 0 1px 1px rgba(255, 255, 255, 0.25), inset 0 0 20px rgba(245, 158, 11, 0.12)",
    shadowHover:
      "0 0 36px 0px rgba(245, 158, 11, 0.6), 0 12px 36px rgba(0, 0, 0, 0.65), inset 0 1px 2px rgba(255, 255, 255, 0.4), inset 0 0 28px rgba(245, 158, 11, 0.2)",
    iconBg: "linear-gradient(135deg, #f59e0b 0%, #d97706 100%)",
    iconBorder: "1.5px solid rgba(253, 230, 138, 0.6)",
    iconShadow:
      "0 0 20px rgba(245, 158, 11, 0.65), inset 0 1px 2px rgba(255, 255, 255, 0.45)",
    iconShadowHover:
      "0 0 26px rgba(245, 158, 11, 0.85), inset 0 1px 2px rgba(255, 255, 255, 0.6)",
    waveStroke: "#d97706",
    waveHighlight: "#fbbf24",
    waveGlow: "rgba(251, 191, 36, 0.55)",
    accentGradient: "from-amber-500 via-orange-400 to-yellow-500",
    badgeClass:
      "bg-amber-950/90 text-amber-200 border-amber-500/40 shadow-[0_0_10px_rgba(245,158,11,0.25)]",
  },
  rose: {
    background:
      "radial-gradient(circle at 85% 45%, rgba(244, 63, 94, 0.35) 0%, transparent 60%), radial-gradient(circle at 15% 25%, rgba(225, 29, 72, 0.4) 0%, transparent 55%), linear-gradient(135deg, #290818 0%, #460d29 50%, #1f0512 100%)",
    borderColor: "rgba(244, 63, 94, 0.65)",
    borderHoverColor: "rgba(251, 113, 133, 0.95)",
    shadow:
      "0 0 24px -2px rgba(244, 63, 94, 0.38), 0 8px 30px rgba(0, 0, 0, 0.5), inset 0 1px 1px rgba(255, 255, 255, 0.25), inset 0 0 20px rgba(244, 63, 94, 0.12)",
    shadowHover:
      "0 0 36px 0px rgba(244, 63, 94, 0.6), 0 12px 36px rgba(0, 0, 0, 0.65), inset 0 1px 2px rgba(255, 255, 255, 0.4), inset 0 0 28px rgba(244, 63, 94, 0.2)",
    iconBg: "linear-gradient(135deg, #f43f5e 0%, #be123c 100%)",
    iconBorder: "1.5px solid rgba(254, 205, 211, 0.6)",
    iconShadow:
      "0 0 20px rgba(244, 63, 94, 0.65), inset 0 1px 2px rgba(255, 255, 255, 0.45)",
    iconShadowHover:
      "0 0 26px rgba(244, 63, 94, 0.85), inset 0 1px 2px rgba(255, 255, 255, 0.6)",
    waveStroke: "#e11d48",
    waveHighlight: "#fb7185",
    waveGlow: "rgba(244, 63, 94, 0.55)",
    accentGradient: "from-rose-500 via-pink-400 to-red-500",
    badgeClass:
      "bg-rose-950/90 text-rose-200 border-rose-500/40 shadow-[0_0_10px_rgba(244,63,94,0.25)]",
  },
  slate: {
    background:
      "radial-gradient(circle at 85% 45%, rgba(148, 163, 184, 0.28) 0%, transparent 60%), radial-gradient(circle at 15% 25%, rgba(100, 116, 139, 0.32) 0%, transparent 55%), linear-gradient(135deg, #0c1424 0%, #18253d 50%, #0a101d 100%)",
    borderColor: "rgba(148, 163, 184, 0.55)",
    borderHoverColor: "rgba(203, 213, 225, 0.85)",
    shadow:
      "0 0 24px -2px rgba(148, 163, 184, 0.28), 0 8px 30px rgba(0, 0, 0, 0.5), inset 0 1px 1px rgba(255, 255, 255, 0.2), inset 0 0 20px rgba(148, 163, 184, 0.1)",
    shadowHover:
      "0 0 36px 0px rgba(148, 163, 184, 0.45), 0 12px 36px rgba(0, 0, 0, 0.65), inset 0 1px 2px rgba(255, 255, 255, 0.3), inset 0 0 28px rgba(148, 163, 184, 0.15)",
    iconBg: "linear-gradient(135deg, #64748b 0%, #334155 100%)",
    iconBorder: "1.5px solid rgba(203, 213, 225, 0.5)",
    iconShadow:
      "0 0 20px rgba(148, 163, 184, 0.5), inset 0 1px 2px rgba(255, 255, 255, 0.35)",
    iconShadowHover:
      "0 0 26px rgba(148, 163, 184, 0.7), inset 0 1px 2px rgba(255, 255, 255, 0.5)",
    waveStroke: "#475569",
    waveHighlight: "#94a3b8",
    waveGlow: "rgba(148, 163, 184, 0.45)",
    accentGradient: "from-slate-400 via-slate-300 to-slate-500",
    badgeClass:
      "bg-slate-800 text-slate-200 border-slate-600 shadow-[0_0_10px_rgba(100,116,139,0.25)]",
  },
};

export const STAT_CARD_ENTERPRISE_ICON_COLORS: Record<
  NormalizedStatCardVariant,
  EnterpriseIconTone
> = {
  blue: {
    bg: "rgba(59, 130, 246, 0.14)",
    border: "1px solid rgba(59, 130, 246, 0.32)",
    text: "#60a5fa",
  },
  emerald: {
    bg: "rgba(16, 185, 129, 0.14)",
    border: "1px solid rgba(16, 185, 129, 0.32)",
    text: "#34d399",
  },
  violet: {
    bg: "rgba(139, 92, 246, 0.14)",
    border: "1px solid rgba(139, 92, 246, 0.32)",
    text: "#a78bfa",
  },
  amber: {
    bg: "rgba(245, 158, 11, 0.14)",
    border: "1px solid rgba(245, 158, 11, 0.32)",
    text: "#fbbf24",
  },
  rose: {
    bg: "rgba(244, 63, 94, 0.14)",
    border: "1px solid rgba(244, 63, 94, 0.32)",
    text: "#fb7185",
  },
  slate: {
    bg: "rgba(148, 163, 184, 0.14)",
    border: "1px solid rgba(148, 163, 184, 0.28)",
    text: "#94a3b8",
  },
};

/**
 * Resolves the normalized palette configuration and enterprise icon tone for a variant.
 */
export function resolveStatCardVariant(
  variant: StatCardVariant = "blue",
): ResolvedStatCardVariant {
  const normalized = normalizeStatCardVariant(variant);
  return {
    normalized,
    config: STAT_CARD_VARIANT_CONFIG[normalized],
    enterpriseIconTone: STAT_CARD_ENTERPRISE_ICON_COLORS[normalized],
  };
}
