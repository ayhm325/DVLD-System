import type { FormEvent } from 'react'
import {
  FileText,
  Save,
  Loader2,
  Calendar,
  Layers,
  Banknote,
  Hash,
} from 'lucide-react'
import { formatDate, formatCurrency } from '../../utils/formatters'
import {
  Button,
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  Label,
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../ui'
import type { LocalDrivingLicenseApplication } from '../../types/LocalDrivingLicenseApplication'
import type { PersonDetails } from '../../types/Person'
import type { LicenseClass } from '../../types/LicenseClass'
import { getPersonInitials } from './localApplicationFormUtils'

export interface ApplicationDetailsStepProps {
  isEdit: boolean
  application?: LocalDrivingLicenseApplication
  person: PersonDetails | null
  classes: LicenseClass[]
  selectedClassId: string
  selectedClass?: LicenseClass
  applicationFees: number
  saving: boolean
  onSelectedClassIdChange: (value: string) => void
  onChangePerson: () => void
  onBackOrCancel: () => void
  onSubmit: (event: FormEvent) => void | Promise<void>
}

export default function ApplicationDetailsStep({
  isEdit,
  application,
  person,
  classes,
  selectedClassId,
  selectedClass,
  applicationFees,
  saving,
  onSelectedClassIdChange,
  onChangePerson,
  onBackOrCancel,
  onSubmit,
}: ApplicationDetailsStepProps) {
  return (
    <form onSubmit={onSubmit} className="space-y-6">
      <Card className="p-6 sm:p-8 space-y-6">
        <CardHeader className="flex-row items-center gap-3 pb-5 border-b border-[#1e3458] mb-2 space-y-0">
          <div className="size-10 rounded-xl bg-blue-500/15 border border-blue-400/30 text-cyan-400 flex items-center justify-center shrink-0">
            <FileText className="size-5" />
          </div>
          <div>
            <CardTitle>Application Information</CardTitle>
            <CardDescription className="mt-0.5">
              Confirm applicant credentials and select the requested driving license class.
            </CardDescription>
          </div>
        </CardHeader>

        {/* Selected Applicant Summary Bar */}
        {!isEdit && person && (
          <div className="flex items-center justify-between gap-3 p-3.5 rounded-xl bg-[#09152b] border border-[#1b2f52]">
            <div className="flex items-center gap-3 min-w-0">
              <span className="size-8 rounded-lg bg-blue-500/20 text-cyan-400 font-bold text-xs flex items-center justify-center shrink-0">
                {getPersonInitials(person.fullName)}
              </span>
              <div className="min-w-0">
                <span className="text-[10px] uppercase font-bold text-slate-400 block">
                  APPLICANT
                </span>
                <span className="text-xs font-bold text-white truncate block">
                  {person.fullName}{' '}
                  <span className="text-slate-400 font-normal">
                    ({person.nationalNo})
                  </span>
                </span>
              </div>
            </div>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={onChangePerson}
              className="text-xs text-cyan-400 hover:text-cyan-300 hover:bg-cyan-950/30 cursor-pointer"
            >
              Change Person
            </Button>
          </div>
        )}

        {/* Details Grid */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
          {/* Application ID */}
          <div>
            <Label className="block text-xs font-bold uppercase tracking-wider text-slate-300 mb-2">
              Application ID
            </Label>
            <div className="h-11 px-3.5 rounded-xl bg-[#091326] border border-[#1b2f52] text-slate-300 font-mono text-sm flex items-center gap-2">
              <Hash className="size-4 text-slate-500" />
              <span>
                {isEdit
                  ? `#${application?.localDrivingLicenseApplicationId}`
                  : 'Assigned on save'}
              </span>
            </div>
          </div>

          {/* Application Date */}
          <div>
            <Label className="block text-xs font-bold uppercase tracking-wider text-slate-300 mb-2">
              Application Date
            </Label>
            <div className="h-11 px-3.5 rounded-xl bg-[#091326] border border-[#1b2f52] text-slate-300 text-sm flex items-center gap-2">
              <Calendar className="size-4 text-slate-500" />
              <span>
                {formatDate(
                  isEdit && application
                    ? application.applicationDate
                    : new Date(),
                )}
              </span>
            </div>
          </div>

          {/* License Class */}
          <div>
            <Label
              htmlFor="license-class-select"
              className="block text-xs font-bold uppercase tracking-wider text-slate-300 mb-2"
            >
              License Class <span className="text-rose-400">*</span>
            </Label>
            <Select
              value={selectedClassId || undefined}
              onValueChange={onSelectedClassIdChange}
              disabled={saving}
            >
              <SelectTrigger
                id="license-class-select"
                aria-label="License class"
                className="w-full h-11 bg-[#0e1a33] border-[#263e68] text-white rounded-xl hover:border-cyan-400/50 focus:border-cyan-400"
              >
                <SelectValue placeholder="Select license class" />
              </SelectTrigger>
              <SelectContent className="max-h-64">
                {classes.map((licenseClass) => (
                  <SelectItem
                    key={licenseClass.licenseClassId}
                    value={String(licenseClass.licenseClassId)}
                  >
                    {licenseClass.licenseClassName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Application Fees */}
          <div>
            <Label className="block text-xs font-bold uppercase tracking-wider text-slate-300 mb-2">
              Application Fees
            </Label>
            <div className="h-11 px-3.5 rounded-xl bg-[#091326] border border-[#1b2f52] text-emerald-400 font-bold text-sm flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Banknote className="size-4 text-emerald-500" />
                <span>{formatCurrency(applicationFees, '')}</span>
              </div>
              <span className="text-xs text-slate-400 font-normal">
                JOD
              </span>
            </div>
          </div>
        </div>

        {/* Selected Class Summary Card */}
        {selectedClass && (
          <div className="rounded-xl border border-blue-500/30 bg-[#08152e] p-4 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 shadow-[0_0_15px_rgba(0,140,255,0.08)]">
            <div className="flex items-center gap-3">
              <div className="size-9 rounded-lg bg-blue-500/15 border border-blue-400/30 text-cyan-400 flex items-center justify-center shrink-0">
                <Layers className="size-4.5" />
              </div>
              <div>
                <strong className="text-sm font-bold text-white block">
                  {selectedClass.licenseClassName}
                </strong>
                <p className="text-xs text-slate-400 mt-0.5">
                  {selectedClass.licenseClassDescription ||
                    'Standard driving license class'}
                </p>
              </div>
            </div>
            <div className="text-right self-end sm:self-auto">
              <span className="text-[10px] uppercase font-bold text-slate-400 block tracking-wider">
                LICENSE CLASS FEES
              </span>
              <strong className="text-sm font-bold text-emerald-400">
                {formatCurrency(selectedClass.licenseClassFees, 'JOD')}
              </strong>
            </div>
          </div>
        )}

        {/* Step 2 Actions */}
        <div className="flex flex-col sm:flex-row items-center justify-end gap-3 pt-4 border-t border-[#1e3458]">
          <Button
            type="button"
            variant="outline"
            onClick={onBackOrCancel}
            className="w-full sm:w-auto h-11 px-5 rounded-xl border-[#263e68] text-slate-300 hover:bg-[#16274a] cursor-pointer"
          >
            {isEdit ? 'Cancel' : 'Back'}
          </Button>
          <Button
            type="submit"
            disabled={saving || !selectedClassId}
            className="w-full sm:w-auto h-11 px-6 gap-2 bg-gradient-to-r from-blue-600 via-blue-500 to-cyan-500 hover:from-blue-500 hover:to-cyan-400 text-white font-bold rounded-xl shadow-[0_0_20px_rgba(0,140,255,0.4)] disabled:opacity-50 cursor-pointer"
          >
            {saving ? (
              <>
                <Loader2 className="size-4 animate-spin" />
                <span>Saving...</span>
              </>
            ) : (
              <>
                <Save className="size-4" />
                <span>
                  {isEdit ? 'Save Changes' : 'Create Application'}
                </span>
              </>
            )}
          </Button>
        </div>
      </Card>
    </form>
  )
}
