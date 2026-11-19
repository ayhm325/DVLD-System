import {
  useCallback,
  useState,
  type KeyboardEvent as ReactKeyboardEvent,
  type MouseEvent as ReactMouseEvent,
} from 'react'
import type { DataTableRowAction } from './DataTableRowContextMenu'

export const ROW_INTERACTIVE_TARGET_SELECTOR =
  'a, button, input, select, textarea, summary, [role="button"], [role="link"], [role="menuitem"], [role="checkbox"], [role="radio"], [role="switch"], [role="option"], [contenteditable]:not([contenteditable="false"]), [tabindex]:not([tabindex="-1"])'

export async function copyToClipboard(text: string): Promise<boolean> {
  if (typeof navigator === 'undefined' || !navigator.clipboard?.writeText) {
    return false
  }
  try {
    await navigator.clipboard.writeText(text)
    return true
  } catch {
    return false
  }
}

export interface RowContextMenuState<T> {
  open: boolean
  openKey: number
  x: number
  y: number
  row: T | null
  actions: DataTableRowAction<T>[]
  restoreElement: HTMLElement | null
}

function getTargetElement(target: EventTarget | null): HTMLElement | null {
  if (target instanceof HTMLElement) {
    return target
  }
  if (target instanceof Node) {
    return target.parentElement
  }
  return null
}

export function isNestedInteractiveTarget(
  target: HTMLElement,
  row: HTMLTableRowElement,
): boolean {
  const interactiveTarget = target.closest(ROW_INTERACTIVE_TARGET_SELECTOR)
  return interactiveTarget !== null && interactiveTarget !== row
}

function hasSelectionInsideRow(row: HTMLTableRowElement): boolean {
  const selection = window.getSelection()
  if (!selection || selection.isCollapsed || selection.toString().trim().length === 0) {
    return false
  }

  const rangeCount = selection.rangeCount ?? 0
  if (rangeCount === 0) {
    return selection.anchorNode ? row.contains(selection.anchorNode) : false
  }

  for (let i = 0; i < rangeCount; i += 1) {
    const range = selection.getRangeAt(i)
    if (
      row.contains(range.startContainer) ||
      row.contains(range.endContainer) ||
      (typeof range.intersectsNode === 'function' && range.intersectsNode(row))
    ) {
      return true
    }
  }

  return false
}

function clampViewportPoint(x: number, y: number): { x: number; y: number } {
  const maxWidth = typeof window !== 'undefined' ? window.innerWidth : 0
  const maxHeight = typeof window !== 'undefined' ? window.innerHeight : 0

  return {
    x: maxWidth > 0 ? Math.min(Math.max(0, x), maxWidth) : Math.max(0, x),
    y: maxHeight > 0 ? Math.min(Math.max(0, y), maxHeight) : Math.max(0, y),
  }
}

export function useRowContextMenu<T>(
  rowContextMenu?: (item: T) => DataTableRowAction<T>[],
) {
  const [contextMenuState, setContextMenuState] = useState<RowContextMenuState<T>>({
    open: false,
    openKey: 0,
    x: 0,
    y: 0,
    row: null,
    actions: [],
    restoreElement: null,
  })

  const openContextMenuAt = useCallback(
    (
      item: T,
      rawX: number,
      rawY: number,
      restoreElement: HTMLElement | null,
    ): boolean => {
      if (!rowContextMenu) return false

      const actions = rowContextMenu(item)
      if (actions.length === 0) return false

      const { x, y } = clampViewportPoint(rawX, rawY)
      setContextMenuState((prev) => ({
        open: true,
        openKey: prev.openKey + 1,
        x,
        y,
        row: item,
        actions,
        restoreElement,
      }))
      return true
    },
    [rowContextMenu],
  )

  const handleRowContextMenu = useCallback(
    (
      e: ReactMouseEvent<HTMLTableRowElement>,
      item: T,
      disabled = false,
    ) => {
      if (!rowContextMenu || disabled) return

      // Ignore synthetic keyboard-fallback contextmenu events at (0,0) when a keyboard handler already opened the menu
      if (e.button !== 2 && e.clientX === 0 && e.clientY === 0) {
        e.preventDefault()
        return
      }

      const rowElement = e.currentTarget
      const targetElement = getTargetElement(e.target)
      if (targetElement && isNestedInteractiveTarget(targetElement, rowElement)) {
        return
      }

      if (hasSelectionInsideRow(rowElement)) {
        return
      }

      if (openContextMenuAt(item, e.clientX, e.clientY, null)) {
        e.preventDefault()
        e.stopPropagation()
      }
    },
    [openContextMenuAt, rowContextMenu],
  )

  const handleRowContextMenuKeyDown = useCallback(
    (
      e: ReactKeyboardEvent<HTMLTableRowElement>,
      item: T,
      disabled = false,
    ): boolean => {
      if (!rowContextMenu || disabled) return false
      if (
        e.key !== 'ContextMenu' &&
        !(e.shiftKey && (e.key === 'F10' || e.code === 'F10'))
      ) {
        return false
      }

      const rowElement = e.currentTarget
      const targetElement = getTargetElement(e.target)
      if (targetElement && isNestedInteractiveTarget(targetElement, rowElement)) {
        return false
      }

      const rect = rowElement.getBoundingClientRect()
      const anchorX = Math.round(rect.left + 16)
      const anchorY = Math.round(rect.bottom + 4)

      if (openContextMenuAt(item, anchorX, anchorY, rowElement)) {
        e.preventDefault()
        e.stopPropagation()
        return true
      }

      return false
    },
    [openContextMenuAt, rowContextMenu],
  )

  const handleOpenChange = useCallback((open: boolean) => {
    setContextMenuState((prev) => (prev.open === open ? prev : { ...prev, open }))
  }, [])

  const closeContextMenu = useCallback(() => {
    setContextMenuState((prev) => (prev.open ? { ...prev, open: false } : prev))
  }, [])

  return {
    contextMenuState,
    handleRowContextMenu,
    handleRowContextMenuKeyDown,
    handleOpenChange,
    closeContextMenu,
  }
}
