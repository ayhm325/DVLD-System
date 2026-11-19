import type {
  KeyboardEvent as ReactKeyboardEvent,
  MouseEvent as ReactMouseEvent,
} from 'react'
import type { LicenseClass } from '../../types/LicenseClass'
import { formatCurrency } from '../../utils/formatters'
import { Input, Textarea, Button } from '../ui'
import { cn } from '@/lib/utils'
import { Loader2 } from 'lucide-react'

export interface LicenseClassEditForm {
  licenseClassName: string
  licenseClassDescription: string
  minAllowedAge: string
  defaultValidityLength: string
  licenseClassFees: string
}

interface LicenseClassRowProps {
  item: LicenseClass
  isEditing: boolean
  canEdit?: boolean
  editForm: LicenseClassEditForm
  saving: boolean
  onStartEdit: (item: LicenseClass) => void
  onFormChange: (form: LicenseClassEditForm) => void
  onSave: () => void
  onCancel: () => void
  hasContextMenu?: boolean
  isContextMenuOpen?: boolean
  onContextMenu?: (
    e: ReactMouseEvent<HTMLTableRowElement>,
    item: LicenseClass,
    disabled?: boolean,
  ) => void
  onContextMenuKeyDown?: (
    e: ReactKeyboardEvent<HTMLTableRowElement>,
    item: LicenseClass,
    disabled?: boolean,
  ) => void
}

export default function LicenseClassRow({
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
}: LicenseClassRowProps) {
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
      <td className="col-compact pt-3.5">
        <span style={{ fontFamily: 'ui-monospace, monospace', color: 'var(--dvld-muted)' }}>
          #{item.licenseClassId}
        </span>
      </td>

      <td className="whitespace-nowrap font-semibold text-[#f1f5f9] pt-3.5">
        {isEditing ? (
          <Input
            className="h-9 text-xs rounded-lg min-w-[140px]"
            value={editForm.licenseClassName}
            maxLength={100}
            aria-label="License class name"
            disabled={saving}
            onChange={(event) =>
              onFormChange({
                ...editForm,
                licenseClassName: event.target.value,
              })
            }
          />
        ) : (
          item.licenseClassName
        )}
      </td>

      <td className="table-cell-description pt-3.5">
        {isEditing ? (
          <Textarea
            className="min-h-[72px] text-xs leading-relaxed p-2.5 rounded-lg resize-y"
            rows={3}
            value={editForm.licenseClassDescription}
            maxLength={500}
            aria-label="License class description"
            disabled={saving}
            onChange={(event) =>
              onFormChange({
                ...editForm,
                licenseClassDescription: event.target.value,
              })
            }
          />
        ) : (
          <p className="m-0 leading-relaxed text-[#c0cddf] text-xs whitespace-normal break-words">
            {item.licenseClassDescription}
          </p>
        )}
      </td>

      <td className="whitespace-nowrap font-mono text-[#cbd5e1] pt-3.5">
        {isEditing ? (
          <Input
            type="number"
            className="h-9 text-xs font-mono rounded-lg w-20"
            value={editForm.minAllowedAge}
            min={1}
            max={100}
            aria-label="Minimum age"
            disabled={saving}
            onChange={(event) =>
              onFormChange({
                ...editForm,
                minAllowedAge: event.target.value,
              })
            }
          />
        ) : (
          item.minAllowedAge
        )}
      </td>

      <td className="whitespace-nowrap font-mono text-[#cbd5e1] pt-3.5">
        {isEditing ? (
          <Input
            type="number"
            className="h-9 text-xs font-mono rounded-lg w-20"
            value={editForm.defaultValidityLength}
            min={1}
            max={100}
            aria-label="Default validity length"
            disabled={saving}
            onChange={(event) =>
              onFormChange({
                ...editForm,
                defaultValidityLength: event.target.value,
              })
            }
          />
        ) : (
          `${item.defaultValidityLength} yrs`
        )}
      </td>

      <td className="whitespace-nowrap font-mono font-semibold text-[#cbd5e1] pt-3.5">
        {isEditing ? (
          <Input
            type="number"
            step="0.01"
            className="h-9 text-xs font-mono rounded-lg w-28"
            value={editForm.licenseClassFees}
            min={0}
            aria-label="License class fees"
            disabled={saving}
            onChange={(event) =>
              onFormChange({
                ...editForm,
                licenseClassFees: event.target.value,
              })
            }
          />
        ) : (
          formatCurrency(item.licenseClassFees, '')
        )}
      </td>

      {canEdit && (
        <td className="whitespace-nowrap pt-3">
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
