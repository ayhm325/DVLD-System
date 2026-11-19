import type {
  KeyboardEvent as ReactKeyboardEvent,
  MouseEvent as ReactMouseEvent,
} from 'react'
import type { ApplicationType } from '../../types/ApplicationType'
import { formatCurrency } from '../../utils/formatters'
import { Input, Button } from '../ui'
import { cn } from '@/lib/utils'
import { Loader2 } from 'lucide-react'

interface ApplicationTypeRowProps {
  item: ApplicationType
  isEditing: boolean
  canEdit?: boolean
  editForm: {
    applicationTypeTitle: string
    applicationTypeFees: string
  }
  saving: boolean
  onStartEdit: (item: ApplicationType) => void
  onFormChange: (form: { applicationTypeTitle: string; applicationTypeFees: string }) => void
  onSave: () => void
  onCancel: () => void
  hasContextMenu?: boolean
  isContextMenuOpen?: boolean
  onContextMenu?: (
    e: ReactMouseEvent<HTMLTableRowElement>,
    item: ApplicationType,
    disabled?: boolean,
  ) => void
  onContextMenuKeyDown?: (
    e: ReactKeyboardEvent<HTMLTableRowElement>,
    item: ApplicationType,
    disabled?: boolean,
  ) => void
}

export default function ApplicationTypeRow({
  item,
  isEditing,
  canEdit = true,
  editForm,
  saving,
  onStartEdit,
  onFormChange,
  onSave,
  onCancel,
  hasContextMenu = false,
  isContextMenuOpen = false,
  onContextMenu,
  onContextMenuKeyDown,
}: ApplicationTypeRowProps) {
  return (
    <tr
      data-context-menu-open={isContextMenuOpen ? 'true' : undefined}
      className={cn(
        'align-top',
        isContextMenuOpen && 'bg-[#1d3150]/80 ring-1 ring-inset ring-blue-400/50',
        hasContextMenu &&
          !isEditing &&
          'focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-blue-300',
      )}
      tabIndex={hasContextMenu && !isEditing ? 0 : undefined}
      aria-keyshortcuts={
        hasContextMenu && !isEditing ? 'Shift+F10 ContextMenu' : undefined
      }
      onContextMenu={
        onContextMenu ? (e) => onContextMenu(e, item, isEditing) : undefined
      }
      onKeyDown={
        onContextMenuKeyDown
          ? (e) => onContextMenuKeyDown(e, item, isEditing)
          : undefined
      }
    >
      <td className="col-compact py-3">
        <span style={{ fontFamily: 'ui-monospace, monospace', color: 'var(--dvld-muted)' }}>
          #{item.applicationTypeId}
        </span>
      </td>

      <td className="font-semibold text-[#f1f5f9] py-3">
        {isEditing ? (
          <Input
            className="h-9 text-xs rounded-lg"
            value={editForm.applicationTypeTitle}
            maxLength={100}
            aria-label="Application type name"
            disabled={saving}
            onChange={(event) =>
              onFormChange({
                ...editForm,
                applicationTypeTitle: event.target.value,
              })
            }
          />
        ) : (
          item.applicationTypeTitle
        )}
      </td>

      <td className="font-mono text-[#cbd5e1] py-3">
        {isEditing ? (
          <Input
            className="h-9 text-xs font-mono rounded-lg w-28"
            type="number"
            min="0"
            step="0.01"
            value={editForm.applicationTypeFees}
            aria-label="Application type fees"
            disabled={saving}
            onChange={(event) =>
              onFormChange({
                ...editForm,
                applicationTypeFees: event.target.value,
              })
            }
          />
        ) : (
          formatCurrency(item.applicationTypeFees, '')
        )}
      </td>

      {canEdit && (
        <td className="py-3">
          {isEditing ? (
            <div className="flex items-center gap-2">
              <Button
                type="button"
                size="sm"
                disabled={saving}
                onClick={onSave}
              >
                {saving && <Loader2 className="size-3.5 animate-spin" />}
                <span>{saving ? 'Saving...' : 'Save'}</span>
              </Button>

              <Button
                type="button"
                variant="secondary"
                size="sm"
                disabled={saving}
                onClick={onCancel}
              >
                Cancel
              </Button>
            </div>
          ) : (
            <Button
              type="button"
              variant="secondary"
              size="sm"
              onClick={() => onStartEdit(item)}
            >
              Edit
            </Button>
          )}
        </td>
      )}
    </tr>
  )
}
