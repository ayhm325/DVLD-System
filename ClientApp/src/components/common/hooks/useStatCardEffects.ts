import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type MouseEvent as ReactMouseEvent,
  type RefObject,
} from "react";

export interface UseStatCardEffectsResult {
  cardRef: RefObject<HTMLElement | null>;
  orbitalRef: RefObject<HTMLDivElement | null>;
  isHovered: boolean;
  reducedMotion: boolean;
  dynamicTransform: string | undefined;
  handleMouseMove: (e: ReactMouseEvent<HTMLElement>) => void;
  handleMouseEnter: (e: ReactMouseEvent<HTMLElement>) => void;
  handleMouseLeave: (e: ReactMouseEvent<HTMLElement>) => void;
}

/**
 * Manages StatCard visual interaction effects: reduced-motion subscription,
 * IntersectionObserver-gated requestAnimationFrame orbital rim shimmer,
 * proximity pointer steering, and zero-rerender 3D perspective tilt via CSS variables.
 */
export function useStatCardEffects(): UseStatCardEffectsResult {
  const cardRef = useRef<HTMLElement | null>(null);
  const orbitalRef = useRef<HTMLDivElement | null>(null);
  const [isHovered, setIsHovered] = useState(false);
  const [reducedMotion, setReducedMotion] = useState(() => {
    if (
      typeof window === "undefined" ||
      typeof window.matchMedia !== "function"
    ) {
      return false;
    }
    return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  });

  useEffect(() => {
    if (
      typeof window === "undefined" ||
      typeof window.matchMedia !== "function"
    ) {
      return;
    }

    const mq = window.matchMedia("(prefers-reduced-motion: reduce)");
    const listener = (e: MediaQueryListEvent) => setReducedMotion(e.matches);

    if (typeof mq.addEventListener === "function") {
      mq.addEventListener("change", listener);
      return () => mq.removeEventListener("change", listener);
    }

    const legacyMq = mq as MediaQueryList & {
      addListener?: (cb: (e: MediaQueryListEvent) => void) => void;
      removeListener?: (cb: (e: MediaQueryListEvent) => void) => void;
    };
    legacyMq.addListener?.(listener);
    return () => legacyMq.removeListener?.(listener);
  }, []);

  // Dynamic Orbital Dual-Lobe Rim Shimmer + Proximity Cursor Steering
  useEffect(() => {
    const card = cardRef.current;
    if (!card || reducedMotion) return;

    let disposed = false;
    let raf = 0;
    let last = 0;
    let visible = true;
    let docVisible = typeof document === "undefined" ? true : !document.hidden;
    let angle = 315;
    let level = 0.42;
    let cachedRect: DOMRect | null = null;
    let rectDirty = true;
    const pointer = { x: -9999, y: -9999, known: false };

    const readRect = (): DOMRect => {
      if (rectDirty || !cachedRect) {
        cachedRect = card.getBoundingClientRect();
        rectDirty = false;
      }
      return cachedRect;
    };

    const tick = (now: number) => {
      raf = 0;
      if (disposed || !visible || !docVisible) return;
      const dt = last ? Math.min(0.05, (now - last) / 1000) : 1 / 60;
      last = now;

      const rect = readRect();
      const halfW = rect.width / 2;
      const halfH = rect.height / 2;
      const centerX = rect.left + halfW;
      const centerY = rect.top + halfH;

      let near = 0;
      let targetAngle = angle;

      if (pointer.known) {
        const dx = pointer.x - centerX;
        const dy = pointer.y - centerY;
        const distOutX = Math.max(Math.abs(dx) - halfW, 0);
        const distOutY = Math.max(Math.abs(dy) - halfH, 0);
        const distance = Math.hypot(distOutX, distOutY);
        const t = Math.min(1, Math.max(0, 1 - distance / 260));
        near = t * t * (3 - 2 * t);

        if (near > 0) {
          const rad = Math.atan2(dy, dx);
          targetAngle = ((rad * 180) / Math.PI + 90 + 360) % 360;
        }
      }

      if (near > 0.02) {
        const diff = ((targetAngle - angle + 540) % 360) - 180;
        angle = (angle + diff * (1 - Math.exp(-dt * 9)) + 360) % 360;
      }

      const targetLevel = 0.42 + 0.58 * near;
      level += (targetLevel - level) * (1 - Math.exp(-dt * 7));

      if (orbitalRef.current) {
        orbitalRef.current.style.setProperty(
          "--stat-orbital-angle",
          `${angle.toFixed(1)}deg`,
        );
        orbitalRef.current.style.opacity = String(
          Math.min(1, Math.max(0, level)).toFixed(3),
        );
      }

      if (
        !disposed &&
        (near > 0.005 || Math.abs(targetLevel - level) > 0.002) &&
        typeof requestAnimationFrame === "function"
      ) {
        raf = requestAnimationFrame(tick);
      } else {
        last = 0;
      }
    };

    const wake = () => {
      if (
        !disposed &&
        !raf &&
        visible &&
        docVisible &&
        typeof requestAnimationFrame === "function"
      ) {
        raf = requestAnimationFrame(tick);
      }
    };

    const handleWinPointerMove = (e: PointerEvent) => {
      if (disposed) return;
      pointer.x = e.clientX;
      pointer.y = e.clientY;
      pointer.known = true;
      wake();
    };

    const markDirty = () => {
      rectDirty = true;
    };

    const handleVisibilityChange = () => {
      docVisible = !document.hidden;
      if (docVisible && pointer.known) {
        last = 0;
        wake();
      }
    };

    const resizeObserver =
      typeof ResizeObserver !== "undefined"
        ? new ResizeObserver(markDirty)
        : null;
    resizeObserver?.observe(card);

    let intersection: IntersectionObserver | null = null;
    if (typeof IntersectionObserver !== "undefined") {
      intersection = new IntersectionObserver((entries) => {
        if (disposed) return;
        visible = entries.some((entry) => entry.isIntersecting);
        if (visible) {
          rectDirty = true;
          last = 0;
          wake();
        }
      });
      intersection.observe(card);
    }

    window.addEventListener("pointermove", handleWinPointerMove, {
      passive: true,
    });
    window.addEventListener("scroll", markDirty, { passive: true, capture: true });
    document.addEventListener("visibilitychange", handleVisibilityChange);
    wake();

    return () => {
      disposed = true;
      if (raf && typeof cancelAnimationFrame === "function") {
        cancelAnimationFrame(raf);
        raf = 0;
      }
      resizeObserver?.disconnect();
      intersection?.disconnect();
      window.removeEventListener("pointermove", handleWinPointerMove);
      window.removeEventListener("scroll", markDirty, true);
      document.removeEventListener("visibilitychange", handleVisibilityChange);
    };
  }, [reducedMotion]);

  const handleMouseMove = useCallback(
    (e: ReactMouseEvent<HTMLElement>) => {
      if (reducedMotion) return;
      const rect = e.currentTarget.getBoundingClientRect();
      const x = e.clientX - rect.left;
      const y = e.clientY - rect.top;
      const centerX = rect.width / 2;
      const centerY = rect.height / 2;
      // Subtle, elegant 3D perspective tilt (-3.5 to +3.5 degrees)
      const rotateX = centerY > 0 ? ((y - centerY) / centerY) * -3.5 : 0;
      const rotateY = centerX > 0 ? ((x - centerX) / centerX) * 3.5 : 0;

      const style = e.currentTarget.style;
      style.setProperty("--stat-mouse-x", `${x}px`);
      style.setProperty("--stat-mouse-y", `${y}px`);
      style.setProperty("--stat-tilt-x", `${rotateX.toFixed(2)}deg`);
      style.setProperty("--stat-tilt-y", `${rotateY.toFixed(2)}deg`);
    },
    [reducedMotion],
  );

  const handleMouseEnter = useCallback(
    (e: ReactMouseEvent<HTMLElement>) => {
      setIsHovered(true);
      if (reducedMotion) return;
      const rect = e.currentTarget.getBoundingClientRect();
      const style = e.currentTarget.style;
      style.setProperty("--stat-mouse-x", `${e.clientX - rect.left}px`);
      style.setProperty("--stat-mouse-y", `${e.clientY - rect.top}px`);
    },
    [reducedMotion],
  );

  const handleMouseLeave = useCallback((e: ReactMouseEvent<HTMLElement>) => {
    setIsHovered(false);
    const style = e.currentTarget.style;
    style.setProperty("--stat-tilt-x", "0deg");
    style.setProperty("--stat-tilt-y", "0deg");
  }, []);

  const dynamicTransform = reducedMotion
    ? undefined
    : isHovered
      ? "perspective(800px) rotateX(var(--stat-tilt-x, 0deg)) rotateY(var(--stat-tilt-y, 0deg)) translateY(-4px)"
      : "perspective(800px) rotateX(0deg) rotateY(0deg) translateY(0px)";

  return {
    cardRef,
    orbitalRef,
    isHovered,
    reducedMotion,
    dynamicTransform,
    handleMouseMove,
    handleMouseEnter,
    handleMouseLeave,
  };
}
