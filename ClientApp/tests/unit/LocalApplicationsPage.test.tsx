import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import LocalApplicationsPage from '../../src/components/local-applications/LocalApplicationsPage'
import * as localAppService from '../../src/services/localDrivingLicenseApplicationService'
import * as licenseService from '../../src/services/licenseService'
import * as apiClient from '../../src/services/apiClient'
import type {
  LocalDrivingLicenseApplication,
  LocalDrivingLicenseApplicationSummary,
  LocalDrivingLicenseApplicationsPage,
} from '../../src/types/LocalDrivingLicenseApplication'

function makeApp(
  overrides: Partial<LocalDrivingLicenseApplication> = {},
): LocalDrivingLicenseApplication {
  return {
    localDrivingLicenseApplicationId: 101,
    licenseClassId: 3,
    licenseClassName: 'Class 3 - Ordinary driving license',
    nationalNo: 'N1001',
    fullName: 'Sara Ahmad',
    applicationDate: '2026-11-01T10:00:00',
    passedTest: 3,
    applicationStatus: 'New',
    statusText: 'New',
    applicationFees: 15,
    licenseClassFees: 20,
    hasLicense: false,
    applicantPersonId: 501,
    ...overrides,
  }
}

function makePage(
  items: LocalDrivingLicenseApplication[],
  overrides: Partial<LocalDrivingLicenseApplicationsPage> = {},
): LocalDrivingLicenseApplicationsPage {
  return {
    items,
    pageNumber: 1,
    pageSize: 10,
    totalCount: items.length,
    totalPages: Math.max(1, Math.ceil(items.length / 10)),
    hasPreviousPage: false,
    hasNextPage: false,
    ...overrides,
  }
}

const DEFAULT_SUMMARY: LocalDrivingLicenseApplicationSummary = {
  totalApplications: 12,
  inProgressCount: 5,
  readyForIssuanceCount: 2,
}

describe('LocalApplicationsPage & Workflow Integration', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn().mockImplementation((query: string) => ({
        matches: false,
        media: query,
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn(),
      })),
    })
  })

  it('uses interactive summary cards as workflow filters with active state, pagination reset, and no redundant tabs', async () => {
    const getAppsSpy = vi
      .spyOn(localAppService, 'getLocalDrivingLicenseApplications')
      .mockResolvedValue(
        makePage([makeApp({ localDrivingLicenseApplicationId: 1 })], {
          totalCount: 15,
          totalPages: 2,
          hasNextPage: true,
        }),
      )

    const getSummarySpy = vi
      .spyOn(localAppService, 'getLocalDrivingLicenseApplicationsSummary')
      .mockResolvedValue(DEFAULT_SUMMARY)

    render(<LocalApplicationsPage accessToken="staff-token" />)

    // Initial load: Total Applications card active, workflowStage='All'
    await waitFor(() => {
      expect(getAppsSpy).toHaveBeenCalledWith(
        'staff-token',
        1,
        10,
        '',
        'all',
        'All',
        undefined,
        'All',
      )
    })
    expect(getSummarySpy).toHaveBeenCalledTimes(1)

    // Old workflow tab buttons must no longer exist
    expect(screen.queryAllByRole('tab')).toHaveLength(0)
    expect(screen.queryByText('All Applications')).toBeNull()

    // Summary cards are interactive (role="button") and reflect dataset-wide counts
    const statsGrid = document.querySelector('.stats-grid') as HTMLElement
    const totalCard = within(statsGrid).getByText('Total Applications').closest('.stat-card') as HTMLElement
    const inProgressCard = within(statsGrid).getByText('In Progress').closest('.stat-card') as HTMLElement
    const readyCard = within(statsGrid).getByText('Ready for Issuance').closest('.stat-card') as HTMLElement

    expect(totalCard).toHaveAttribute('role', 'button')
    expect(inProgressCard).toHaveAttribute('role', 'button')
    expect(readyCard).toHaveAttribute('role', 'button')

    expect(totalCard).toHaveAttribute('aria-pressed', 'true')
    expect(within(totalCard).getByText('Active')).toBeInTheDocument()
    expect(within(totalCard).getByText('12')).toBeInTheDocument()
    expect(within(inProgressCard).getByText('5')).toBeInTheDocument()
    expect(within(readyCard).getByText('2')).toBeInTheDocument()

    // Go to page 2 -> summary cards stay 12/5/2 and summary endpoint is not re-queried for pagination
    fireEvent.click(screen.getByRole('button', { name: /next/i }))
    await waitFor(() => {
      expect(getAppsSpy).toHaveBeenLastCalledWith(
        'staff-token',
        2,
        10,
        '',
        'all',
        'All',
        undefined,
        'All',
      )
    })
    expect(getSummarySpy).toHaveBeenCalledTimes(1)
    expect(within(totalCard).getByText('12')).toBeInTheDocument()

    // Clicking In Progress card resets pagination to page 1, sets active state, and disables status dropdown
    fireEvent.click(inProgressCard)
    await waitFor(() => {
      expect(getAppsSpy).toHaveBeenLastCalledWith(
        'staff-token',
        1,
        10,
        '',
        'all',
        'All',
        undefined,
        'InProgress',
      )
    })
    expect(inProgressCard).toHaveAttribute('aria-pressed', 'true')
    expect(within(inProgressCard).getByText('Active')).toBeInTheDocument()
    expect(totalCard).toHaveAttribute('aria-pressed', 'false')
    const statusTrigger = screen.getByRole('combobox', { name: /filter by application status/i })
    expect(statusTrigger).toBeDisabled()

    // Clicking Ready for Issuance card sends workflowStage='ReadyForIssuance'
    fireEvent.click(readyCard)
    await waitFor(() => {
      expect(getAppsSpy).toHaveBeenLastCalledWith(
        'staff-token',
        1,
        10,
        '',
        'all',
        'All',
        undefined,
        'ReadyForIssuance',
      )
    })
    expect(readyCard).toHaveAttribute('aria-pressed', 'true')

    // Search inside ReadyForIssuance preserves workflowStage
    const searchInput = screen.getByPlaceholderText(/search by name/i)
    fireEvent.change(searchInput, { target: { value: 'Sara' } })
    await waitFor(() => {
      expect(getAppsSpy).toHaveBeenLastCalledWith(
        'staff-token',
        1,
        10,
        'Sara',
        'all',
        'All',
        undefined,
        'ReadyForIssuance',
      )
    })

    // Clicking Total Applications card resets workflow filter to 'All' while preserving search term
    fireEvent.click(totalCard)
    await waitFor(() => {
      expect(getAppsSpy).toHaveBeenLastCalledWith(
        'staff-token',
        1,
        10,
        'Sara',
        'all',
        'All',
        undefined,
        'All',
      )
    })
    expect(totalCard).toHaveAttribute('aria-pressed', 'true')
    expect(statusTrigger).not.toBeDisabled()
  })

  it('enables Issue Driving License context menu ONLY for eligible applications (New, 3/3 passed, no license) without Actions column', async () => {
    const eligible = makeApp({
      localDrivingLicenseApplicationId: 10,
      fullName: 'Eligible Applicant',
      applicationStatus: 'New',
      statusText: 'New',
      passedTest: 3,
      hasLicense: false,
    })
    const inProgress = makeApp({
      localDrivingLicenseApplicationId: 11,
      fullName: 'InProgress Applicant',
      applicationStatus: 'New',
      statusText: 'New',
      passedTest: 2,
      hasLicense: false,
    })
    const cancelled = makeApp({
      localDrivingLicenseApplicationId: 12,
      fullName: 'Cancelled Applicant',
      applicationStatus: 'Cancelled',
      statusText: 'Cancelled',
      passedTest: 3,
      hasLicense: false,
    })
    const completed = makeApp({
      localDrivingLicenseApplicationId: 13,
      fullName: 'Completed Applicant',
      applicationStatus: 'Completed',
      statusText: 'Completed',
      passedTest: 3,
      hasLicense: false,
    })
    const alreadyIssued = makeApp({
      localDrivingLicenseApplicationId: 14,
      fullName: 'Issued Applicant',
      applicationStatus: 'Completed',
      statusText: 'Completed',
      passedTest: 3,
      hasLicense: true,
    })

    vi.spyOn(localAppService, 'getLocalDrivingLicenseApplications').mockResolvedValue(
      makePage([eligible, inProgress, cancelled, completed, alreadyIssued]),
    )
    vi.spyOn(localAppService, 'getLocalDrivingLicenseApplicationsSummary').mockResolvedValue(
      DEFAULT_SUMMARY,
    )

    render(<LocalApplicationsPage accessToken="staff-token" />)

    await screen.findByText('Eligible Applicant')

    // Actions column must not exist
    expect(screen.queryByRole('columnheader', { name: /actions/i })).toBeNull()

    // Distinct lifecycle badges
    const eligibleRow = screen.getByText('Eligible Applicant').closest('tr')!
    expect(within(eligibleRow).getByText('Ready for Issuance')).toBeInTheDocument()
    expect(within(screen.getByText('Issued Applicant').closest('tr')!).getByText('License Issued')).toBeInTheDocument()

    // Context menu enables Issue Driving License ONLY for eligible row
    fireEvent.contextMenu(eligibleRow, { button: 2, clientX: 100, clientY: 100 })
    const eligibleMenuItem = await screen.findByRole('menuitem', { name: /issue driving license/i })
    expect(eligibleMenuItem).not.toHaveAttribute('aria-disabled', 'true')

    fireEvent.contextMenu(screen.getByText('InProgress Applicant').closest('tr')!, { button: 2, clientX: 100, clientY: 100 })
    expect(await screen.findByRole('menuitem', { name: /issue driving license/i })).toHaveAttribute('aria-disabled', 'true')

    fireEvent.contextMenu(screen.getByText('Cancelled Applicant').closest('tr')!, { button: 2, clientX: 100, clientY: 100 })
    expect(await screen.findByRole('menuitem', { name: /issue driving license/i })).toHaveAttribute('aria-disabled', 'true')

    fireEvent.contextMenu(screen.getByText('Completed Applicant').closest('tr')!, { button: 2, clientX: 100, clientY: 100 })
    expect(await screen.findByRole('menuitem', { name: /issue driving license/i })).toHaveAttribute('aria-disabled', 'true')

    fireEvent.contextMenu(screen.getByText('Issued Applicant').closest('tr')!, { button: 2, clientX: 100, clientY: 100 })
    expect(await screen.findByRole('menuitem', { name: /issue driving license/i })).toHaveAttribute('aria-disabled', 'true')
  })

  it('completes post-issuance flow: opens existing modal, submits once, refreshes list and summary, and removes from Ready for Issuance', async () => {
    const readyApp = makeApp({
      localDrivingLicenseApplicationId: 205,
      fullName: 'Omar Ready',
      passedTest: 3,
      hasLicense: false,
    })

    const getAppsSpy = vi
      .spyOn(localAppService, 'getLocalDrivingLicenseApplications')
      .mockResolvedValueOnce(makePage([readyApp])) // initial All
      .mockResolvedValueOnce(makePage([readyApp])) // ReadyForIssuance tab
      .mockResolvedValueOnce(makePage([])) // post-issuance refresh in ReadyForIssuance

    const getSummarySpy = vi
      .spyOn(localAppService, 'getLocalDrivingLicenseApplicationsSummary')
      .mockResolvedValueOnce({
        totalApplications: 10,
        inProgressCount: 4,
        readyForIssuanceCount: 1,
      })
      .mockResolvedValueOnce({
        totalApplications: 10,
        inProgressCount: 4,
        readyForIssuanceCount: 0,
      })

    let resolveIssuance!: (v: { licenseId: number }) => void
    const issueSpy = vi.spyOn(licenseService, 'issueFirstLicense').mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveIssuance = resolve
        }),
    )

    render(<LocalApplicationsPage accessToken="staff-token" />)

    await screen.findByText('Omar Ready')
    const statsGrid = document.querySelector('.stats-grid') as HTMLElement
    fireEvent.click(within(statsGrid).getByText('Ready for Issuance').closest('.stat-card')!)
    await screen.findByText('Omar Ready')

    // Open context menu on row -> click Issue Driving License -> opens existing IssueDrivingLicenseModal
    fireEvent.contextMenu(screen.getByText('Omar Ready').closest('tr')!, { button: 2, clientX: 100, clientY: 100 })
    fireEvent.click(await screen.findByRole('menuitem', { name: /issue driving license/i }))

    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/issue first-time driving license/i)).toBeInTheDocument()

    // Submit form inside modal
    const submitBtn = within(dialog).getByRole('button', { name: /issue driving license/i })
    fireEvent.click(submitBtn)

    // Duplicate submission prevented while request is in flight
    expect(within(dialog).getByRole('button', { name: /issuing license/i })).toBeDisabled()
    expect(issueSpy).toHaveBeenCalledTimes(1)
    expect(issueSpy).toHaveBeenCalledWith('staff-token', {
      localApplicationId: 205,
      notes: null,
    })

    resolveIssuance({ licenseId: 9001 })

    // Both list and summary refresh; item disappears from Ready for Issuance and count drops to 0
    await waitFor(() => {
      expect(getAppsSpy).toHaveBeenCalledTimes(3)
      expect(getSummarySpy).toHaveBeenCalledTimes(2)
    })

    await screen.findByText(/no applications match the selected filters/i)
    const readyCard = within(statsGrid).getByText('Ready for Issuance').closest('.stat-card')!
    expect(within(readyCard as HTMLElement).getByText('0')).toBeInTheDocument()
    const totalCard = within(statsGrid).getByText('Total Applications').closest('.stat-card')!
    expect(within(totalCard as HTMLElement).getByText('10')).toBeInTheDocument()
  })

  it('handles issuance failure, modal cancellation, retry success, and list/summary error states without false success', async () => {
    const readyApp = makeApp({
      localDrivingLicenseApplicationId: 301,
      fullName: 'Tariq Retry',
      passedTest: 3,
      hasLicense: false,
    })

    vi.spyOn(localAppService, 'getLocalDrivingLicenseApplications').mockResolvedValue(
      makePage([readyApp]),
    )
    vi.spyOn(localAppService, 'getLocalDrivingLicenseApplicationsSummary').mockRejectedValueOnce(
      new Error('Summary unavailable'),
    )

    const issueSpy = vi
      .spyOn(licenseService, 'issueFirstLicense')
      .mockRejectedValueOnce(new Error('A license has already been issued for this application.'))
      .mockResolvedValueOnce({ licenseId: 9002 })

    render(<LocalApplicationsPage accessToken="staff-token" />)

    await screen.findByText('Tariq Retry')

    // Summary error falls back to '—'
    const statsGrid = document.querySelector('.stats-grid') as HTMLElement
    const totalCard = within(statsGrid).getByText('Total Applications').closest('.stat-card')!
    expect(within(totalCard as HTMLElement).getByText('—')).toBeInTheDocument()

    // Open modal via context menu & cancel without calling API
    fireEvent.contextMenu(screen.getByText('Tariq Retry').closest('tr')!, { button: 2, clientX: 100, clientY: 100 })
    fireEvent.click(await screen.findByRole('menuitem', { name: /issue driving license/i }))
    let dialog = await screen.findByRole('dialog')
    fireEvent.click(within(dialog).getByRole('button', { name: /^cancel$/i }))
    expect(issueSpy).not.toHaveBeenCalled()

    // Re-open modal via context menu & fail first attempt -> shows error, no success message
    fireEvent.contextMenu(screen.getByText('Tariq Retry').closest('tr')!, { button: 2, clientX: 100, clientY: 100 })
    fireEvent.click(await screen.findByRole('menuitem', { name: /issue driving license/i }))
    dialog = await screen.findByRole('dialog')
    fireEvent.click(within(dialog).getByRole('button', { name: /issue driving license/i }))

    await within(dialog).findByText(/a license has already been issued for this application/i)
    expect(within(dialog).queryByText(/successfully issued/i)).toBeNull()

    // Retry succeeds
    fireEvent.click(within(dialog).getByRole('button', { name: /issue driving license/i }))
    await waitFor(() => {
      expect(issueSpy).toHaveBeenCalledTimes(2)
    })
  })

  it('constructs correct API query parameters in localDrivingLicenseApplicationService', async () => {
    const apiSpy = vi
      .spyOn(apiClient, 'apiRequest')
      .mockResolvedValueOnce(makePage([]))
      .mockResolvedValueOnce(DEFAULT_SUMMARY)

    await localAppService.getLocalDrivingLicenseApplications(
      'tok',
      2,
      25,
      'Ahmad',
      'name',
      'New',
      4,
      'ReadyForIssuance',
    )

    expect(apiSpy).toHaveBeenCalledWith(
      '/LocalDrivingLicenseApplications?pageNumber=2&pageSize=25&searchQuery=Ahmad&searchBy=name&statusFilter=New&licenseClassId=4&workflowStage=ReadyForIssuance',
      'tok',
    )

    await localAppService.getLocalDrivingLicenseApplicationsSummary('tok')
    expect(apiSpy).toHaveBeenLastCalledWith(
      '/LocalDrivingLicenseApplications/summary',
      'tok',
    )

    apiSpy.mockResolvedValueOnce(null as unknown as LocalDrivingLicenseApplicationSummary)
    await expect(localAppService.getLocalDrivingLicenseApplicationsSummary('tok')).rejects.toThrow(
      /invalid summary response/i,
    )
  })
})
