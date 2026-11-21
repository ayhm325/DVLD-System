import type { CSSProperties, ReactNode } from 'react'
import DataTableContainer from './DataTableContainer'
import LoadingSpinner from './LoadingSpinner'
import EmptyState from './EmptyState'
import ErrorAlert from './ErrorAlert'
import DataTableRowContextMenu, {
  type DataTableRowAction,
} from './DataTableRowContextMenu'
import {
  isNestedInteractiveTarget,
  useRowContextMenu,
} from './useRowContextMenu'
import { ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react'
import { cn } from '@/lib/utils'

export type { DataTableRowAction }
export type SortDirection = 'asc' | 'desc'

export interface DataTableColumn<T> {
  /** Unique key identifying the column */
  key: string
  /** Column header title or node */
  header: ReactNode
  /** Simple property accessor function */
  accessor?: (row: T) => ReactNode
  /** Custom render function receiving row data and index */
  render?: (row: T, index: number) => ReactNode
  /** Cell CSS class name or class name resolver */
  className?: string | ((row: T, index: number) => string | undefined)
  /** Header cell CSS class name */
  headerClassName?: string
  /** Content text alignment */
  align?: 'left' | 'center' | 'right'
  /** Explicit column width */
  width?: string | number
  /** Whether the column supports sorting */
  sortable?: boolean
  /** Value extractor for sorting comparisons */
  sortValue?: (row: T) => string | number | Date | null | undefined
}

export interface DataTableProps<T> {
  /** Table row items */
  data: T[]
  /** Column definitions */
  columns: DataTableColumn<T>[]
  /** Unique row key extractor */
  getRowKey: (row: T, index: number) => string | number

  /** Loading indicator flag */
  loading?: boolean
  /** Custom loading element */
  loadingComponent?: ReactNode
  /** Loading text message */
  loadingMessage?: string

  /** Error state node or message */
  error?: ReactNode

  /** Custom empty state node when data is empty */
  emptyState?: ReactNode
  /** Message displayed when table has no data */
  emptyMessage?: string
  /** Optional icon for empty state */
  emptyIcon?: ReactNode

  /** Optional click handler for entire row */
  onRowClick?: (row: T) => void
  /** Row CSS class name or resolver */
  rowClassName?: string | ((row: T, index: number) => string | undefined)

  /** Table element className (defaults to 'people-table') */
  tableClassName?: string
  /** Minimum width for internal scroll container */
  minWidth?: number | string
  /** Wrapper container className */
  containerClassName?: string

  /** Optional sorting state & handler */
  sortColumn?: string
  sortDirection?: SortDirection
  onSort?: (columnKey: string, direction: SortDirection) => void

  /** Optional custom row render override. Note: Custom row renderers own their own row markup, semantics, and interaction behavior. */
  customRowRender?: (row: T, index: number, columns: DataTableColumn<T>[]) => ReactNode

  /** Accessible table label */
  ariaLabel?: string

  /**
   * Optional context menu generator.
   * When provided, right-clicking (or pressing Shift+F10 / ContextMenu key) on a row
   * opens a cursor-anchored context menu displaying the returned actions.
   */
  rowContextMenu?: (row: T) => DataTableRowAction<T>[]
}

/**
 * Standardized generic presentation-only DataTable component.
 * Provides consistent layout, dark theme styling, horizontal overflow containment,
 * configurable columns, custom cells, and loading/empty state handling.
 */
export default function DataTable<T>({
  data,
  columns,
  getRowKey,

  loading = false,
  loadingComponent,
  loadingMessage = 'Loading records...',

  error,

  emptyState,
  emptyMessage = 'No records found.',
  emptyIcon,

  onRowClick,
  rowClassName,

  tableClassName = 'people-table',
  minWidth = 750,
  containerClassName,

  sortColumn,
  sortDirection,
  onSort,

  customRowRender,
  ariaLabel,
  rowContextMenu,
}: DataTableProps<T>) {
  const {
    contextMenuState,
    handleRowContextMenu,
    handleRowContextMenuKeyDown,
    handleOpenChange,
  } = useRowContextMenu<T>(rowContextMenu)

  function handleRowKeyDown(
    event: React.KeyboardEvent<HTMLTableRowElement>,
    row: T,
  ) {
    if (handleRowContextMenuKeyDown(event, row)) {
      return
    }

    const target = event.target as HTMLElement | null
    if (!target || isNestedInteractiveTarget(target, event.currentTarget)) return

    const activatesRow = event.key === 'Enter' || event.key === ' '
    if (!onRowClick || !activatesRow || event.repeat) return

    event.preventDefault()
    onRowClick(row)
  }
  function handleSortClick(column: DataTableColumn<T>) {
    if (!column.sortable || !onSort) return
    const isCurrent = sortColumn === column.key
    const nextDirection: SortDirection =
      isCurrent && sortDirection === 'asc' ? 'desc' : 'asc'
    onSort(column.key, nextDirection)
  }

  function getAlignmentClass(align?: 'left' | 'center' | 'right') {
    if (align === 'center') return 'text-center'
    if (align === 'right') return 'text-right'
    return 'text-left'
  }

  function getWidthStyle(width?: string | number): CSSProperties | undefined {
    if (width === undefined) return undefined
    return { width: typeof width === 'number' ? `${width}px` : width }
  }

  return (
    <DataTableContainer minWidth={minWidth} containerClassName={containerClassName}>
      <table className={tableClassName} aria-label={ariaLabel}>
        <thead>
          <tr>
            {columns.map((col) => {
              const isSorted = sortColumn === col.key
              const alignClass = getAlignmentClass(col.align)
              const widthStyle = getWidthStyle(col.width)

              return (
                <th
                  key={col.key}
                  className={cn(alignClass, col.headerClassName)}
                  style={widthStyle}
                  aria-sort={
                    isSorted
                      ? sortDirection === 'asc'
                        ? 'ascending'
                        : 'descending'
                      : undefined
                  }
                >
                  {col.sortable && onSort ? (
                    <button
                      type="button"
                      className="inline-flex items-center gap-1.5 font-inherit text-inherit uppercase hover:text-white transition-colors cursor-pointer bg-transparent border-0 p-0"
                      onClick={() => handleSortClick(col)}
                    >
                      <span>{col.header}</span>
                      <span aria-hidden="true" className="text-slate-400">
                        {isSorted ? (
                          sortDirection === 'asc' ? (
                            <ArrowUp size={13} />
                          ) : (
                            <ArrowDown size={13} />
                          )
                        ) : (
                          <ArrowUpDown size={13} />
                        )}
                      </span>
                    </button>
                  ) : (
                    col.header
                  )}
                </th>
              )
            })}
          </tr>
        </thead>

        <tbody>
          {loading ? (
            <tr>
              <td colSpan={columns.length} className="text-center p-8">
                {loadingComponent ?? (
                  <LoadingSpinner message={loadingMessage} />
                )}
              </td>
            </tr>
          ) : error ? (
            <tr>
              <td colSpan={columns.length} className="p-6">
                {typeof error === 'string' ? (
                  <ErrorAlert message={error} />
                ) : (
                  error
                )}
              </td>
            </tr>
          ) : data.length === 0 ? (
            <tr>
              <td colSpan={columns.length} className="p-6 text-center">
                {emptyState ?? (
                  <EmptyState message={emptyMessage} icon={emptyIcon} />
                )}
              </td>
            </tr>
          ) : (
            data.map((row, index) => {
              const rowKey = getRowKey(row, index)

              if (customRowRender) {
                return customRowRender(row, index, columns)
              }

              const resolvedRowClass =
                typeof rowClassName === 'function'
                  ? rowClassName(row, index)
                  : rowClassName

              const isClickable = Boolean(onRowClick)
              const isContextMenuOpen =
                contextMenuState.open &&
                contextMenuState.row !== null &&
                (contextMenuState.row === row ||
                  getRowKey(contextMenuState.row, -1) === rowKey)

              return (
                <tr
                  key={rowKey}
                  data-context-menu-open={isContextMenuOpen ? 'true' : undefined}
                  className={cn(
                    resolvedRowClass,
                    isClickable && 'cursor-pointer hover:bg-[#1d3150] transition-colors',
                    isContextMenuOpen && 'bg-[#1d3150]/80 ring-1 ring-inset ring-blue-400/50',
                    (isClickable || rowContextMenu) &&
                      'focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-blue-300',
                  )}
                  tabIndex={isClickable || rowContextMenu ? 0 : undefined}
                  aria-keyshortcuts={rowContextMenu ? 'Shift+F10 ContextMenu' : undefined}
                  onClick={
                    isClickable
                      ? (e) => {
                          const target = e.target as HTMLElement | null
                          if (target && isNestedInteractiveTarget(target, e.currentTarget)) return
                          onRowClick?.(row)
                        }
                      : undefined
                  }
                  onContextMenu={
                    rowContextMenu
                      ? (e) => handleRowContextMenu(e, row)
                      : undefined
                  }
                  onKeyDown={
                    isClickable || rowContextMenu
                      ? (e) => handleRowKeyDown(e, row)
                      : undefined
                  }
                >
                  {columns.map((col) => {
                    const resolvedCellClass =
                      typeof col.className === 'function'
                        ? col.className(row, index)
                        : col.className

                    const alignClass = getAlignmentClass(col.align)
                    const widthStyle = getWidthStyle(col.width)

                    const cellContent = col.render
                      ? col.render(row, index)
                      : col.accessor
                        ? col.accessor(row)
                        : null

                    return (
                      <td
                        key={col.key}
                        className={cn(alignClass, resolvedCellClass)}
                        style={widthStyle}
                      >
                        {cellContent}
                      </td>
                    )
                  })}
                </tr>
              )
            })
          )}
        </tbody>
      </table>

      {rowContextMenu && (
        <DataTableRowContextMenu<T>
          open={contextMenuState.open}
          menuKey={contextMenuState.openKey}
          onOpenChange={handleOpenChange}
          position={{ x: contextMenuState.x, y: contextMenuState.y }}
          row={contextMenuState.row}
          actions={contextMenuState.actions}
          restoreFocusElement={contextMenuState.restoreElement}
        />
      )}
    </DataTableContainer>
  )
}
