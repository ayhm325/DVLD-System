import {
  Search,
  UserPlus,
  IdCard,
  Phone,
  Mail,
  X,
} from 'lucide-react'
import PersonPhoto from '../common/PersonPhoto'
import LoadingSpinner from '../common/LoadingSpinner'
import PaginationControls from '../common/PaginationControls'
import { Button } from '../ui/button'
import { Input } from '../ui/input'
import { Label } from '../ui/label'
import type { Person } from '../../types/Person'

export interface EligiblePersonPickerProps {
  accessToken: string
  submitting: boolean
  selectedPerson: Person | null
  personQuery: string
  debouncedQuery: string
  candidates: Person[]
  loadingCandidates: boolean
  candidateError: string | null
  totalCount: number
  totalPages: number
  page: number
  pageSize: number
  hasPreviousPage: boolean
  hasNextPage: boolean
  fieldError?: string
  onPersonQueryChange: (value: string) => void
  onSelectPerson: (person: Person) => void
  onClearSelectedPerson: () => void
  onPreviousPage: () => void
  onNextPage: () => void
  onPageSizeChange: (size: number) => void
  onRefreshCandidates: () => void
  onNavigateToAddPerson?: () => void
  onCloseModal: () => void
}

export default function EligiblePersonPicker({
  accessToken,
  submitting,
  selectedPerson,
  personQuery,
  debouncedQuery,
  candidates,
  loadingCandidates,
  candidateError,
  totalCount,
  totalPages,
  page,
  pageSize,
  hasPreviousPage,
  hasNextPage,
  fieldError,
  onPersonQueryChange,
  onSelectPerson,
  onClearSelectedPerson,
  onPreviousPage,
  onNextPage,
  onPageSizeChange,
  onRefreshCandidates,
  onNavigateToAddPerson,
  onCloseModal,
}: EligiblePersonPickerProps) {
  return (
    <div className="space-y-2.5">
      <div className="flex items-center justify-between gap-2">
        <Label className="text-xs font-bold uppercase tracking-wider text-slate-300">
          1. Linked Person Record <span className="text-rose-400">*</span>
        </Label>
        {onNavigateToAddPerson && (
          <button
            type="button"
            onClick={() => {
              onCloseModal()
              onNavigateToAddPerson()
            }}
            className="inline-flex items-center gap-1 text-xs font-semibold text-cyan-400 hover:text-cyan-300 transition-colors cursor-pointer bg-transparent border-0 p-0"
          >
            <UserPlus className="size-3.5" />
            <span>Register New Person</span>
          </button>
        )}
      </div>

      {selectedPerson ? (
        <div className="flex items-center justify-between gap-3 p-3.5 rounded-xl bg-[#0b172b] border border-cyan-500/40 shadow-sm">
          <div className="flex items-center gap-3 min-w-0">
            <PersonPhoto
              personId={selectedPerson.personId}
              hasPhoto={Boolean(selectedPerson.imagePath ?? selectedPerson.hasPhoto)}
              photoKey={selectedPerson.imagePath}
              fullName={selectedPerson.fullName}
              accessToken={accessToken}
              size="md"
            />
            <div className="min-w-0">
              <p className="text-sm font-bold text-white truncate">
                {selectedPerson.fullName}
              </p>
              <div className="flex flex-wrap items-center gap-2 text-xs text-slate-400 mt-0.5">
                <span className="inline-flex items-center gap-1 font-mono text-cyan-300">
                  <IdCard className="size-3" />
                  {selectedPerson.nationalNo}
                </span>
                <span>•</span>
                <span className="font-mono">Person #{selectedPerson.personId}</span>
                {selectedPerson.phone && (
                  <>
                    <span>•</span>
                    <span className="inline-flex items-center gap-1">
                      <Phone className="size-3" />
                      {selectedPerson.phone}
                    </span>
                  </>
                )}
                {selectedPerson.email && (
                  <>
                    <span>•</span>
                    <span className="inline-flex items-center gap-1 truncate max-w-[180px]">
                      <Mail className="size-3" />
                      {selectedPerson.email}
                    </span>
                  </>
                )}
              </div>
            </div>
          </div>

          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={submitting}
            onClick={onClearSelectedPerson}
            className="shrink-0 h-8 px-2.5 text-xs bg-[#14233c] border-[#263750] text-slate-300 hover:text-white cursor-pointer"
          >
            <X className="size-3.5 mr-1" />
            Change
          </Button>
        </div>
      ) : (
        <div className="space-y-2">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 size-4 text-slate-400" />
            <Input
              type="text"
              value={personQuery}
              onChange={(e) => onPersonQueryChange(e.target.value)}
              placeholder="Search unlinked people by name, National No., Person ID, or phone..."
              aria-label="Search eligible person"
              className="pl-9 bg-[#0b1526] border-[#223656] text-white placeholder:text-slate-500"
            />
          </div>

          <div className="rounded-xl border border-[#223656] bg-[#0a1324] overflow-hidden">
            {loadingCandidates ? (
              <div className="py-6 flex justify-center">
                <LoadingSpinner size="sm" message="Loading eligible people..." />
              </div>
            ) : candidateError ? (
              <div className="p-4 text-center">
                <p className="text-xs text-rose-400 mb-2">{candidateError}</p>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={onRefreshCandidates}
                  className="h-7 text-xs bg-[#14233c] border-[#263750] text-slate-300 hover:text-white cursor-pointer"
                >
                  Retry
                </Button>
              </div>
            ) : candidates.length === 0 ? (
              <div className="p-4 text-center space-y-2">
                <p className="text-xs text-slate-300 font-medium">
                  {debouncedQuery
                    ? `No unlinked person matched "${debouncedQuery}".`
                    : 'Every registered person currently has a linked user account.'}
                </p>
                {onNavigateToAddPerson && !debouncedQuery && (
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => {
                      onCloseModal()
                      onNavigateToAddPerson()
                    }}
                    className="h-8 text-xs bg-[#14233c] border-cyan-500/40 text-cyan-300 hover:text-white cursor-pointer"
                  >
                    <UserPlus className="size-3.5 mr-1.5" />
                    Register New Person First
                  </Button>
                )}
              </div>
            ) : (
              <>
                <div className="divide-y divide-[#1a2b47] max-h-44 overflow-y-auto">
                  {candidates.map((person) => (
                    <button
                      key={person.personId}
                      type="button"
                      onClick={() => onSelectPerson(person)}
                      className="w-full flex items-center justify-between gap-3 px-3.5 py-2.5 text-left hover:bg-[#13233f] transition-colors cursor-pointer bg-transparent border-0"
                    >
                      <div className="flex items-center gap-2.5 min-w-0">
                        <PersonPhoto
                          personId={person.personId}
                          hasPhoto={Boolean(person.imagePath ?? person.hasPhoto)}
                          photoKey={person.imagePath}
                          fullName={person.fullName}
                          accessToken={accessToken}
                          size="xs"
                        />
                        <div className="min-w-0">
                          <p className="text-xs font-bold text-white truncate">
                            {person.fullName}
                          </p>
                          <p className="text-[11px] text-slate-400 font-mono truncate">
                            National No: {person.nationalNo} · ID #{person.personId}
                          </p>
                        </div>
                      </div>
                      <span className="shrink-0 text-[11px] font-semibold text-cyan-400">
                        Select
                      </span>
                    </button>
                  ))}
                </div>
                {totalCount > 0 && (
                  <PaginationControls
                    pageNumber={page}
                    pageSize={pageSize}
                    totalCount={totalCount}
                    totalPages={totalPages}
                    hasPreviousPage={hasPreviousPage}
                    hasNextPage={hasNextPage}
                    onPreviousPage={onPreviousPage}
                    onNextPage={onNextPage}
                    onPageSizeChange={onPageSizeChange}
                    loading={loadingCandidates}
                    itemName="candidates"
                  />
                )}
              </>
            )}
          </div>
        </div>
      )}

      {fieldError && (
        <p className="text-xs font-medium text-rose-400">{fieldError}</p>
      )}
    </div>
  )
}
