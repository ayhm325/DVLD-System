import type { FormEvent } from 'react'
import {
  ArrowRight,
  UserCheck,
  Search,
  CheckCircle2,
  Loader2,
} from 'lucide-react'
import {
  Button,
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  Input,
  Label,
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../ui'
import type { PersonDetails } from '../../types/Person'
import { getPersonInitials, type PersonSearchMode } from './localApplicationFormUtils'

export interface ApplicantIdentificationStepProps {
  searchBy: PersonSearchMode
  searchValue: string
  searching: boolean
  person: PersonDetails | null
  onSearchByChange: (value: PersonSearchMode) => void
  onSearchValueChange: (value: string) => void
  onSearchSubmit: (event?: FormEvent) => void | Promise<void>
  onCancel: () => void
  onContinue: () => void
}

export default function ApplicantIdentificationStep({
  searchBy,
  searchValue,
  searching,
  person,
  onSearchByChange,
  onSearchValueChange,
  onSearchSubmit,
  onCancel,
  onContinue,
}: ApplicantIdentificationStepProps) {
  return (
    <Card className="p-6 sm:p-8 space-y-6">
      <CardHeader className="flex-row items-center gap-3 pb-5 border-b border-[#1e3458] mb-2 space-y-0">
        <div className="size-10 rounded-xl bg-blue-500/15 border border-blue-400/30 text-cyan-400 flex items-center justify-center shrink-0">
          <UserCheck className="size-5" />
        </div>
        <div>
          <CardTitle>Applicant Identification</CardTitle>
          <CardDescription className="mt-0.5">
            Find an existing registered person using their Person ID or national number.
          </CardDescription>
        </div>
      </CardHeader>

      {/* Search Form Bar */}
      <form onSubmit={onSearchSubmit} className="space-y-4">
        <div className="flex flex-col sm:flex-row items-stretch sm:items-end gap-3">
          <div className="w-full sm:w-48 shrink-0">
            <Label
              htmlFor="search-by-select"
              className="block text-xs font-bold uppercase tracking-wider text-slate-300 mb-2"
            >
              Search By
            </Label>
            <Select
              value={searchBy}
              onValueChange={(val) => onSearchByChange(val as PersonSearchMode)}
            >
              <SelectTrigger
                id="search-by-select"
                className="w-full h-11 bg-[#0e1a33] border-[#263e68] text-white rounded-xl"
              >
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="id">Person ID</SelectItem>
                <SelectItem value="nationalNo">
                  National Number
                </SelectItem>
              </SelectContent>
            </Select>
          </div>

          <div className="flex-1">
            <Label
              htmlFor="search-query-input"
              className="block text-xs font-bold uppercase tracking-wider text-slate-300 mb-2"
            >
              {searchBy === 'id' ? 'Person ID' : 'National Number'}
            </Label>
            <Input
              id="search-query-input"
              value={searchValue}
              onChange={(e) => onSearchValueChange(e.target.value)}
              placeholder={
                searchBy === 'id'
                  ? 'e.g. 1042'
                  : 'Enter 10-digit national number'
              }
              disabled={searching}
            />
          </div>

          <Button
            type="submit"
            disabled={searching}
            className="h-11 px-6 gap-2 bg-gradient-to-r from-blue-600 to-cyan-600 hover:from-blue-500 hover:to-cyan-500 text-white font-bold rounded-xl shadow-[0_0_15px_rgba(0,140,255,0.35)] shrink-0 cursor-pointer"
          >
            {searching ? (
              <>
                <Loader2 className="size-4 animate-spin" />
                <span>Searching...</span>
              </>
            ) : (
              <>
                <Search className="size-4" />
                <span>Find Person</span>
              </>
            )}
          </Button>
        </div>
      </form>

      {/* Verified Person Display or Empty State */}
      {person ? (
        <div className="rounded-xl border border-emerald-500/30 bg-[#091a2e]/90 p-5 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 shadow-[0_0_15px_rgba(16,185,129,0.1)]">
          <div className="flex items-center gap-4 min-w-0">
            <div className="size-12 rounded-xl bg-gradient-to-br from-blue-500 to-cyan-500 text-white font-bold text-sm flex items-center justify-center shrink-0 shadow-sm">
              {getPersonInitials(person.fullName)}
            </div>
            <div className="min-w-0 space-y-1">
              <div className="flex items-center gap-2">
                <span className="text-[10px] font-extrabold uppercase tracking-wider text-emerald-400">
                  VERIFIED CITIZEN
                </span>
              </div>
              <h4 className="text-base font-bold text-white truncate">
                {person.fullName}
              </h4>
              <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-slate-300">
                <span>
                  National No.{' '}
                  <strong className="text-white">
                    {person.nationalNo}
                  </strong>
                </span>
                <span>·</span>
                <span>
                  Person ID{' '}
                  <strong className="text-white font-mono">
                    #{person.personId}
                  </strong>
                </span>
                <span>·</span>
                <span>
                  Country{' '}
                  <strong className="text-white">
                    {person.countryName || '—'}
                  </strong>
                </span>
                <span>·</span>
                <span>
                  Phone{' '}
                  <strong className="text-white">
                    {person.phone || '—'}
                  </strong>
                </span>
              </div>
            </div>
          </div>

          <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-bold bg-emerald-500/15 border border-emerald-500/30 text-emerald-300 shrink-0">
            <CheckCircle2 className="size-3.5" />
            <span>Verified</span>
          </span>
        </div>
      ) : (
        <div className="rounded-xl border border-dashed border-[#263e68] bg-[#091326] p-8 text-center text-slate-400 space-y-1.5">
          <UserCheck className="size-8 text-slate-500 mx-auto mb-2" />
          <p className="font-semibold text-slate-200 text-sm">
            No applicant selected
          </p>
          <p className="text-xs text-slate-400">
            Search for a citizen or resident above to proceed with the application.
          </p>
        </div>
      )}

      {/* Step 1 Actions */}
      <div className="flex flex-col sm:flex-row items-center justify-end gap-3 pt-4 border-t border-[#1e3458]">
        <Button
          type="button"
          variant="outline"
          onClick={onCancel}
          className="w-full sm:w-auto h-11 px-5 rounded-xl border-[#263e68] text-slate-300 hover:bg-[#16274a] cursor-pointer"
        >
          Cancel
        </Button>
        <Button
          type="button"
          disabled={!person}
          onClick={onContinue}
          className="w-full sm:w-auto h-11 px-6 gap-2 bg-gradient-to-r from-blue-600 via-blue-500 to-cyan-500 hover:from-blue-500 hover:to-cyan-400 text-white font-bold rounded-xl shadow-[0_0_20px_rgba(0,140,255,0.4)] disabled:opacity-50 cursor-pointer"
        >
          <span>Continue to Application Details</span>
          <ArrowRight className="size-4" />
        </Button>
      </div>
    </Card>
  )
}
