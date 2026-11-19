import { CalendarDays, ChevronDown, Search } from 'lucide-react'
import type { LocalDrivingLicenseApplication } from '../../../types/LocalDrivingLicenseApplication'
import StatusBadge from '../../common/StatusBadge'
import { Button } from '../../ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '../../ui/card'
import { isApplicationNewStatus } from '../utils/appointmentEligibility'

export interface ApplicationCandidatePickerProps {
  application: LocalDrivingLicenseApplication | null
  isNewStatus: boolean
  showCandidateSelector: boolean
  selectedAppId: number | null
  candidateSearchQuery: string
  onSearchQueryChange: (query: string) => void
  filteredCandidates: LocalDrivingLicenseApplication[]
  onSelectApplication: (appId: number | null) => void
  hasMoreCandidates: boolean
  loadingMoreCandidates: boolean
  onLoadMoreCandidates: () => void | Promise<void>
  directLookupId: string
  onDirectLookupIdChange: (id: string) => void
  directLookupLoading: boolean
  directLookupError: string | null
  onDirectLookupSubmit: (e: React.FormEvent) => void | Promise<void>
}

/**
 * Renders Card 1 of the scheduling flow: Scenario B candidate search, dropdown,
 * pagination, direct Application ID lookup, and the selected applicant's summary.
 */
export default function ApplicationCandidatePicker({
  application,
  isNewStatus,
  showCandidateSelector,
  selectedAppId,
  candidateSearchQuery,
  onSearchQueryChange,
  filteredCandidates,
  onSelectApplication,
  hasMoreCandidates,
  loadingMoreCandidates,
  onLoadMoreCandidates,
  directLookupId,
  onDirectLookupIdChange,
  directLookupLoading,
  directLookupError,
  onDirectLookupSubmit,
}: ApplicationCandidatePickerProps) {
  return (
    <Card className="border-[#1e3458] bg-[#0c1830] rounded-2xl shadow-md overflow-hidden">
      <CardHeader className="border-b border-[#1e3458] px-6 py-5">
        <div className="flex items-center justify-between gap-3">
          <CardTitle className="text-base font-bold text-white flex items-center gap-2.5">
            <CalendarDays className="size-4.5 text-cyan-400" />
            <span>Candidate & Application</span>
          </CardTitle>
          {application ? (
            <StatusBadge
              variant={
                isNewStatus
                  ? 'warning'
                  : application.applicationStatus === 'Completed'
                    ? 'success'
                    : 'neutral'
              }
              label={
                application.statusText || application.applicationStatus || 'New'
              }
            />
          ) : (
            <span className="text-[11px] text-amber-300 font-medium px-2.5 py-0.5 rounded-full bg-amber-500/10 border border-amber-500/20">
              Selection Required
            </span>
          )}
        </div>
        <CardDescription className="text-xs text-slate-400 mt-1">
          Select or verify the local driving license application for this appointment.
        </CardDescription>
      </CardHeader>

      <CardContent className="p-6 space-y-4">
        {/* Scenario B Candidate Selection Tools */}
        {showCandidateSelector && (
          <div className="space-y-3.5 rounded-2xl bg-slate-950/60 p-4.5 border border-slate-800">
            <div className="space-y-1.5">
              <label
                htmlFor="candidate-search"
                className="block text-[11px] font-semibold uppercase tracking-wider text-slate-400"
              >
                Search Candidate (Name, National No, or App ID)
              </label>
              <div className="relative">
                <Search className="size-4 absolute left-3.5 top-3 text-slate-500" />
                <input
                  id="candidate-search"
                  type="text"
                  value={candidateSearchQuery}
                  onChange={(e) => onSearchQueryChange(e.target.value)}
                  placeholder="Type applicant name, national ID..."
                  className="w-full bg-[#111e33] border border-[#223656] text-xs text-white pl-9.5 pr-3 py-2 rounded-xl focus:outline-none focus:border-cyan-500"
                />
              </div>
            </div>

            {/* Candidate Select Dropdown with Status Indicators */}
            <div className="space-y-1.5">
              <label
                htmlFor="select-application"
                className="block text-[11px] font-semibold uppercase tracking-wider text-slate-400 mb-1"
              >
                Loaded Applications ({filteredCandidates.length})
              </label>
              <select
                id="select-application"
                value={selectedAppId ? String(selectedAppId) : ''}
                onChange={(e) => {
                  const val = e.target.value
                  onSelectApplication(val ? Number(val) : null)
                }}
                className="w-full bg-[#111e33] border border-[#223656] text-xs text-white p-2.5 rounded-xl focus:outline-none focus:border-cyan-500 cursor-pointer"
              >
                <option value="">— Select an application —</option>
                {filteredCandidates.map((app) => {
                  const isEligible = isApplicationNewStatus(app)
                  return (
                    <option
                      key={app.localDrivingLicenseApplicationId}
                      value={app.localDrivingLicenseApplicationId}
                      className={
                        isEligible
                          ? 'text-white bg-slate-900'
                          : 'text-slate-400 bg-slate-950'
                      }
                    >
                      #{app.localDrivingLicenseApplicationId} — {app.fullName}{' '}
                      [{app.statusText || app.applicationStatus || 'New'}] (
                      {app.licenseClassName})
                    </option>
                  )
                })}
              </select>
            </div>

            {/* Load More Candidates Pagination Button */}
            {hasMoreCandidates && (
              <div className="pt-1">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => void onLoadMoreCandidates()}
                  disabled={loadingMoreCandidates}
                  className="w-full text-xs h-8 text-cyan-300 border-[#223656] hover:bg-[#14233c] rounded-xl"
                >
                  {loadingMoreCandidates ? (
                    <span>Loading more candidates…</span>
                  ) : (
                    <span className="flex items-center gap-1.5 justify-center">
                      <ChevronDown className="size-3.5" />
                      <span>Load More Applications</span>
                    </span>
                  )}
                </Button>
              </div>
            )}

            {/* Direct App ID Lookup Form */}
            <form
              onSubmit={(e) => void onDirectLookupSubmit(e)}
              className="pt-3 border-t border-slate-800"
            >
              <span className="text-[10px] text-slate-400 block mb-1.5">
                Or enter exact Application ID:
              </span>
              <div className="flex gap-2">
                <input
                  type="number"
                  value={directLookupId}
                  onChange={(e) => onDirectLookupIdChange(e.target.value)}
                  placeholder="App ID #"
                  className="w-32 bg-[#111e33] border border-[#223656] text-xs text-white px-3 py-1.5 rounded-xl focus:outline-none focus:border-cyan-500 font-mono"
                />
                <Button
                  type="submit"
                  size="sm"
                  disabled={directLookupLoading || !directLookupId.trim()}
                  className="h-8 text-xs bg-cyan-700 hover:bg-cyan-600 text-white rounded-xl px-3.5 cursor-pointer"
                >
                  {directLookupLoading ? 'Finding…' : 'Lookup'}
                </Button>
              </div>
              {directLookupError && (
                <span className="text-[11px] text-rose-400 block pt-1.5">
                  {directLookupError}
                </span>
              )}
            </form>
          </div>
        )}

        {/* Selected Application Details */}
        {application ? (
          <div className="p-4.5 rounded-xl bg-slate-950/40 border border-slate-800/80 space-y-2.5 text-xs">
            <div className="flex items-center justify-between py-1.5 border-b border-slate-800/80">
              <span className="text-slate-400">Applicant Full Name:</span>
              <strong className="text-white font-medium">
                {application.fullName}
              </strong>
            </div>
            <div className="flex items-center justify-between py-1.5 border-b border-slate-800/80">
              <span className="text-slate-400">National Number:</span>
              <span className="font-mono text-slate-200">
                {application.nationalNo}
              </span>
            </div>
            <div className="flex items-center justify-between py-1.5 border-b border-slate-800/80">
              <span className="text-slate-400">License Class:</span>
              <span className="text-cyan-300 font-medium">
                {application.licenseClassName ||
                  `Class ${application.licenseClassId}`}
              </span>
            </div>
            <div className="flex items-center justify-between py-1.5">
              <span className="text-slate-400">Examinations Passed:</span>
              <span className="font-bold text-emerald-400 font-mono">
                {application.passedTest} / 3 Stages
              </span>
            </div>
          </div>
        ) : (
          <div className="p-5 rounded-xl border border-dashed border-slate-700 text-center text-xs text-slate-400">
            Please select an application from the dropdown above to load candidate details.
          </div>
        )}
      </CardContent>
    </Card>
  )
}
