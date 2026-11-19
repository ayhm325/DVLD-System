import { Profiler } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen } from "@testing-library/react";
import StatCard, { MetricCard } from "../../src/components/common/StatCard";
import {
  normalizeStatCardVariant,
  resolveStatCardVariant,
} from "../../src/components/common/statCardVariants";

interface MockMediaQueryController {
  mediaQuery: MediaQueryList;
  emitChange: (matches: boolean) => void;
  addEventListenerSpy: ReturnType<typeof vi.fn>;
  removeEventListenerSpy: ReturnType<typeof vi.fn>;
}

function createMatchMediaMock(initialMatches: boolean): MockMediaQueryController {
  let matches = initialMatches;
  const listeners = new Set<(e: MediaQueryListEvent) => void>();

  const addEventListenerSpy = vi.fn(
    (type: string, cb: EventListenerOrEventListenerObject) => {
      if (type === "change" && typeof cb === "function") {
        listeners.add(cb as (e: MediaQueryListEvent) => void);
      }
    },
  );

  const removeEventListenerSpy = vi.fn(
    (type: string, cb: EventListenerOrEventListenerObject) => {
      if (type === "change" && typeof cb === "function") {
        listeners.delete(cb as (e: MediaQueryListEvent) => void);
      }
    },
  );

  const mediaQuery = {
    get matches() {
      return matches;
    },
    media: "(prefers-reduced-motion: reduce)",
    onchange: null,
    addEventListener: addEventListenerSpy,
    removeEventListener: removeEventListenerSpy,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    dispatchEvent: vi.fn(),
  } as unknown as MediaQueryList;

  return {
    mediaQuery,
    addEventListenerSpy,
    removeEventListenerSpy,
    emitChange(nextMatches: boolean) {
      matches = nextMatches;
      const event = {
        matches: nextMatches,
        media: "(prefers-reduced-motion: reduce)",
      } as MediaQueryListEvent;
      for (const listener of listeners) {
        listener(event);
      }
    },
  };
}

describe("StatCard", () => {
  const originalMatchMedia = window.matchMedia;
  const originalIntersectionObserver = window.IntersectionObserver;

  beforeEach(() => {
    const controller = createMatchMediaMock(false);
    Object.defineProperty(window, "matchMedia", {
      configurable: true,
      writable: true,
      value: vi.fn(() => controller.mediaQuery),
    });
  });

  afterEach(() => {
    Object.defineProperty(window, "matchMedia", {
      configurable: true,
      writable: true,
      value: originalMatchMedia,
    });
    Object.defineProperty(window, "IntersectionObserver", {
      configurable: true,
      writable: true,
      value: originalIntersectionObserver,
    });
  });

  it("normalizes variant aliases and resolves variant configuration + enterprise tones", () => {
    expect(normalizeStatCardVariant("green")).toBe("emerald");
    expect(normalizeStatCardVariant("emerald")).toBe("emerald");
    expect(normalizeStatCardVariant("purple")).toBe("violet");
    expect(normalizeStatCardVariant("violet")).toBe("violet");
    expect(normalizeStatCardVariant("gold")).toBe("amber");
    expect(normalizeStatCardVariant("amber")).toBe("amber");
    expect(normalizeStatCardVariant("rose")).toBe("rose");
    expect(normalizeStatCardVariant("slate")).toBe("slate");
    expect(normalizeStatCardVariant("default")).toBe("blue");
    expect(normalizeStatCardVariant()).toBe("blue");

    const resolved = resolveStatCardVariant("gold");
    expect(resolved.normalized).toBe("amber");
    expect(resolved.config.waveHighlight).toBe("#fbbf24");
    expect(resolved.enterpriseIconTone.text).toBe("#fbbf24");
  });

  it("preserves public props, CSS class hooks, MetricCard alias, and non-neon enterprise mode", () => {
    const { rerender } = render(
      <MetricCard
        label="Legacy Label"
        value={42}
        description="Total recorded"
        variant="emerald"
        active
        footer={<span>Footer details</span>}
      />,
    );

    const card = document.querySelector(".stat-card") as HTMLElement;
    expect(card).toBeInTheDocument();
    expect(screen.getByText("Legacy Label")).toHaveClass("stat-card-title");
    expect(screen.getByText("42")).toHaveClass("stat-card-value");
    expect(screen.getByText("Total recorded")).toHaveClass(
      "stat-card-description",
    );
    expect(screen.getByText("Active")).toBeInTheDocument();
    expect(screen.getByText("Footer details")).toBeInTheDocument();
    expect(card.querySelector(".stat-card-wave")).toBeInTheDocument();

    rerender(
      <StatCard
        title="Enterprise Metric"
        value={99}
        variant="blue"
        neon={false}
        showWaveform={false}
        loading
        badge="Filter"
      />,
    );

    expect(screen.getByText("Enterprise Metric")).toBeInTheDocument();
    expect(screen.queryByText("99")).not.toBeInTheDocument();
    expect(screen.getByText("Filter")).toBeInTheDocument();
    expect(card.querySelector(".stat-card-wave")).not.toBeInTheDocument();
    expect(card).toHaveClass("border", "p-5");
  });

  it("supports interactive click, Enter, and Space keyboard activation while keeping static cards non-interactive", () => {
    const { rerender } = render(<StatCard title="Static Card" value={10} />);
    const staticCard = document.querySelector(".stat-card") as HTMLElement;
    expect(staticCard).not.toHaveAttribute("role");
    expect(staticCard).not.toHaveAttribute("tabindex");

    const handleClick = vi.fn();
    rerender(
      <StatCard
        title="Interactive Card"
        value={10}
        onClick={handleClick}
        active={false}
      />,
    );

    const buttonCard = screen.getByRole("button");
    expect(buttonCard).toHaveAttribute("aria-pressed", "false");
    expect(buttonCard).toHaveAttribute("tabindex", "0");

    fireEvent.click(buttonCard);
    fireEvent.keyDown(buttonCard, { key: "Enter" });
    fireEvent.keyDown(buttonCard, { key: " " });
    expect(handleClick).toHaveBeenCalledTimes(3);
  });

  it("updates pointer and tilt CSS custom properties on mousemove without causing React re-renders", () => {
    let commitCount = 0;

    render(
      <Profiler
        id="stat-card-profiler"
        onRender={() => {
          commitCount += 1;
        }}
      >
        <StatCard title="Zero Rerender Card" value={120} />
      </Profiler>,
    );
    const card = document.querySelector(".stat-card") as HTMLElement;
    vi.spyOn(card, "getBoundingClientRect").mockReturnValue({
      x: 100,
      y: 50,
      left: 100,
      top: 50,
      right: 300,
      bottom: 150,
      width: 200,
      height: 100,
      toJSON: () => ({}),
    });

    expect(commitCount).toBe(1);

    fireEvent.mouseEnter(card, { clientX: 140, clientY: 80 });
    expect(commitCount).toBe(2);
    expect(card.style.getPropertyValue("--stat-mouse-x")).toBe("40px");
    expect(card.style.getPropertyValue("--stat-mouse-y")).toBe("30px");

    const commitsAfterEnter = commitCount;
    for (let i = 0; i < 10; i++) {
      fireEvent.mouseMove(card, { clientX: 200 + i * 5, clientY: 100 + i * 2 });
    }

    // Continuous pointer movement must never trigger additional React commits
    expect(commitCount).toBe(commitsAfterEnter);
    expect(card.style.getPropertyValue("--stat-mouse-x")).toBe("145px");
    expect(card.style.getPropertyValue("--stat-mouse-y")).toBe("68px");
    expect(card.style.getPropertyValue("--stat-tilt-x")).not.toBe("");
    expect(card.style.getPropertyValue("--stat-tilt-y")).not.toBe("");

    fireEvent.mouseLeave(card);
    expect(commitCount).toBe(3);
    expect(card.style.getPropertyValue("--stat-tilt-x")).toBe("0deg");
    expect(card.style.getPropertyValue("--stat-tilt-y")).toBe("0deg");
  });

  it("handles zero-size bounding rects without producing NaN tilt values", () => {
    render(<StatCard title="Zero Rect" value={5} />);
    const card = document.querySelector(".stat-card") as HTMLElement;
    vi.spyOn(card, "getBoundingClientRect").mockReturnValue({
      x: 0,
      y: 0,
      left: 0,
      top: 0,
      right: 0,
      bottom: 0,
      width: 0,
      height: 0,
      toJSON: () => ({}),
    });

    fireEvent.mouseMove(card, { clientX: 12, clientY: 20 });
    expect(card.style.getPropertyValue("--stat-tilt-x")).toBe("0.00deg");
    expect(card.style.getPropertyValue("--stat-tilt-y")).toBe("0.00deg");
  });

  it("respects prefers-reduced-motion initially and cleans up animation frames and listeners when toggled or unmounted", () => {
    const controller = createMatchMediaMock(true);
    Object.defineProperty(window, "matchMedia", {
      configurable: true,
      writable: true,
      value: vi.fn(() => controller.mediaQuery),
    });

    const rafSpy = vi.spyOn(window, "requestAnimationFrame");
    const addWinListenerSpy = vi.spyOn(window, "addEventListener");

    const { unmount } = render(<StatCard title="Reduced Card" value={7} />);
    const reducedCard = document.querySelector(".stat-card") as HTMLElement;
    expect(reducedCard.style.transform).toBe("");
    expect(rafSpy).not.toHaveBeenCalled();
    expect(
      addWinListenerSpy.mock.calls.some(([type]) => type === "pointermove"),
    ).toBe(false);
    unmount();

    // Now mount with animations enabled, verify cleanup on dynamic reduced-motion toggle and unmount
    const activeController = createMatchMediaMock(false);
    Object.defineProperty(window, "matchMedia", {
      configurable: true,
      writable: true,
      value: vi.fn(() => activeController.mediaQuery),
    });

    const disconnectSpy = vi.fn();
    const observeSpy = vi.fn();
    class MockIntersectionObserver {
      observe = observeSpy;
      disconnect = disconnectSpy;
      unobserve = vi.fn();
      takeRecords = vi.fn(() => []);
      root = null;
      rootMargin = "";
      thresholds = [];
    }
    Object.defineProperty(window, "IntersectionObserver", {
      configurable: true,
      writable: true,
      value: MockIntersectionObserver,
    });

    let nextFrameId = 100;
    const cancelSpy = vi.spyOn(window, "cancelAnimationFrame").mockImplementation(() => {});
    vi.spyOn(window, "requestAnimationFrame").mockImplementation(() => ++nextFrameId);
    const removeWinListenerSpy = vi.spyOn(window, "removeEventListener");

    const view = render(<StatCard title="Animated Card" value={88} />);
    expect(observeSpy).toHaveBeenCalledTimes(1);

    const activeCard = document.querySelector(".stat-card") as HTMLElement;
    expect(activeCard.style.transform).toContain("perspective(800px)");

    act(() => {
      activeController.emitChange(true);
    });
    expect(cancelSpy).toHaveBeenCalledWith(101);
    expect(disconnectSpy).toHaveBeenCalledTimes(1);
    expect(
      removeWinListenerSpy.mock.calls.some(([type]) => type === "pointermove"),
    ).toBe(true);
    expect(activeCard.style.transform).toBe("");

    view.unmount();
    expect(activeController.removeEventListenerSpy).toHaveBeenCalledWith(
      "change",
      expect.any(Function),
    );
  });
});
