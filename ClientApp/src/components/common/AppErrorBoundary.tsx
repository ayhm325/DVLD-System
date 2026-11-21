import { Component, type ErrorInfo, type ReactNode } from "react";
import { AlertTriangle, Home, RotateCcw } from "lucide-react";

export type AppErrorBoundaryProps = {
  children: ReactNode;
  scope?: "app" | "route";
  resetKey?: string;
  onBackToDashboard?: () => void;
};

type AppErrorBoundaryState = {
  hasError: boolean;
  prevResetKey?: string;
};

export default class AppErrorBoundary extends Component<
  AppErrorBoundaryProps,
  AppErrorBoundaryState
> {
  state: AppErrorBoundaryState = {
    hasError: false,
    prevResetKey: this.props.resetKey,
  };

  static getDerivedStateFromError(): Partial<AppErrorBoundaryState> {
    return { hasError: true };
  }

  static getDerivedStateFromProps(
    props: AppErrorBoundaryProps,
    state: AppErrorBoundaryState,
  ): Partial<AppErrorBoundaryState> | null {
    if (props.resetKey !== state.prevResetKey) {
      return {
        hasError: false,
        prevResetKey: props.resetKey,
      };
    }
    return null;
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    const scope = this.props.scope ?? "app";
    console.error(
      `[DVLD UI ErrorBoundary:${scope}] ${error.name}: ${error.message}`,
      { componentStackLength: info.componentStack?.length ?? 0 },
    );
  }

  private handleReset = () => {
    this.setState({ hasError: false });
  };

  private handleBackToDashboard = () => {
    this.setState({ hasError: false });
    this.props.onBackToDashboard?.();
  };

  render() {
    if (!this.state.hasError) {
      return this.props.children;
    }

    const isRouteScope = this.props.scope === "route";

    return (
      <div
        role="alert"
        className="flex min-h-80 w-full items-center justify-center p-6"
      >
        <div className="w-full max-w-md rounded-xl border border-red-200 bg-white p-6 text-center shadow-sm dark:border-red-900/50 dark:bg-slate-900">
          <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-red-50 text-red-600 dark:bg-red-950/50 dark:text-red-400">
            <AlertTriangle className="h-6 w-6" aria-hidden="true" />
          </div>
          <h2 className="text-lg font-semibold text-slate-900 dark:text-slate-100">
            {isRouteScope
              ? "Unable to display this page"
              : "Something went wrong"}
          </h2>
          <p className="mt-2 text-sm text-slate-600 dark:text-slate-400">
            {isRouteScope
              ? "An unexpected error occurred while loading this section. You can try again or return to the dashboard."
              : "An unexpected application error occurred. Please try again or reload the page."}
          </p>
          <div className="mt-6 flex flex-wrap items-center justify-center gap-3">
            <button
              type="button"
              onClick={this.handleReset}
              className="inline-flex items-center gap-2 rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 dark:bg-slate-100 dark:text-slate-900 dark:hover:bg-slate-200"
            >
              <RotateCcw className="h-4 w-4" aria-hidden="true" />
              Try Again
            </button>
            {this.props.onBackToDashboard && (
              <button
                type="button"
                onClick={this.handleBackToDashboard}
                className="inline-flex items-center gap-2 rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
              >
                <Home className="h-4 w-4" aria-hidden="true" />
                Back to Dashboard
              </button>
            )}
          </div>
        </div>
      </div>
    );
  }
}
