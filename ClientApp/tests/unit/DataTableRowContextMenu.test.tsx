import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import DataTable, { type DataTableColumn } from '../../src/components/common/DataTable'
import { Card } from '../../src/components/ui/card'
import DetainedLicensesPage from '../../src/components/detained/DetainedLicensesPage'
import ExpiredLicensesTable from '../../src/components/licenses/ExpiredLicensesTable'
import CountriesPage from '../../src/components/countries/CountriesPage'
import ApplicationTypesPage from '../../src/components/application-types/ApplicationTypesPage'
import LicenseClassesPage from '../../src/components/license-classes/LicenseClassesPage'
import TestTypesPage from '../../src/components/test-types/TestTypesPage'
import * as detainedService from '../../src/services/detainedLicenseService'
import * as countryService from '../../src/services/countryService'
import * as applicationTypeService from '../../src/services/applicationTypeService'
import * as licenseClassService from '../../src/services/licenseClassService'
import * as testTypeService from '../../src/services/testTypeService'
import type { DetainedLicense } from '../../src/types/DetainedLicense'
import type { License } from '../../src/types/License'

vi.mock('../../src/context/useAuth', () => ({
  useAuth: () => ({
    user: { userId: 1, userName: 'admin', fullName: 'System Admin', role: 'Admin' },
    accessToken: 'test-token',
    isAuthenticated: true,
    login: vi.fn(),
    logout: vi.fn(),
  }),
}))

type SampleRow = {
  id: number
  name: string
}

const SAMPLE_COLUMNS: DataTableColumn<SampleRow>[] = [
  { key: 'id', header: 'ID', accessor: (row) => row.id },
  { key: 'name', header: 'Name', accessor: (row) => row.name },
]

const SAMPLE_ROWS: SampleRow[] = [
  { id: 1, name: 'Row Alpha' },
  { id: 2, name: 'Row Beta' },
]

describe('DataTable & Row Context Menu Infrastructure', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: {
        writeText: vi.fn().mockResolvedValue(undefined),
      },
    })
  })

  afterEach(() => {
    window.getSelection()?.removeAllRanges()
  })

  it('portals trigger anchor directly to document.body outside backdrop-filtered Card with exact viewport coordinates', async () => {
    const onSelect = vi.fn()

    const { container } = render(
      <Card data-testid="card-wrapper">
        <DataTable<SampleRow>
          columns={SAMPLE_COLUMNS}
          data={SAMPLE_ROWS}
          getRowKey={(r) => r.id}
          rowContextMenu={() => [
            { key: 'inspect', label: 'Inspect Item', onClick: onSelect },
          ]}
        />
      </Card>,
    )

    const row1 = screen.getByText('Row Alpha').closest('tr')!
    fireEvent.contextMenu(row1, { button: 2, clientX: 245, clientY: 318 })

    const menuItem = await screen.findByRole('menuitem', { name: 'Inspect Item' })
    expect(menuItem).toBeInTheDocument()

    const anchor = document.body.querySelector(
      '[data-context-menu-anchor="true"]',
    ) as HTMLElement | null
    expect(anchor).not.toBeNull()
    expect(anchor!.parentElement).toBe(document.body)
    expect(container.contains(anchor)).toBe(false)
    expect(anchor!.style.position).toBe('fixed')
    expect(anchor!.style.left).toBe('245px')
    expect(anchor!.style.top).toBe('318px')

    fireEvent.click(menuItem)
    expect(onSelect).toHaveBeenCalledTimes(1)
    expect(onSelect).toHaveBeenCalledWith(SAMPLE_ROWS[0])
  })

  it('switches cleanly from Row 1 to Row 2 while already open, updating coordinates and row payload', async () => {
    const onInspect = vi.fn()

    render(
      <DataTable<SampleRow>
        columns={SAMPLE_COLUMNS}
        data={SAMPLE_ROWS}
        getRowKey={(r) => r.id}
        rowContextMenu={(row) => [
          {
            key: 'inspect',
            label: `Inspect ${row.name}`,
            onClick: onInspect,
          },
        ]}
      />,
    )

    const row1 = screen.getByText('Row Alpha').closest('tr')!
    const row2 = screen.getByText('Row Beta').closest('tr')!

    fireEvent.contextMenu(row1, { button: 2, clientX: 120, clientY: 140 })
    expect(
      await screen.findByRole('menuitem', { name: 'Inspect Row Alpha' }),
    ).toBeInTheDocument()
    expect(row1).toHaveAttribute('data-context-menu-open', 'true')

    fireEvent.contextMenu(row2, { button: 2, clientX: 360, clientY: 420 })
    const row2Item = await screen.findByRole('menuitem', {
      name: 'Inspect Row Beta',
    })
    expect(row2Item).toBeInTheDocument()
    expect(row2).toHaveAttribute('data-context-menu-open', 'true')
    expect(row1).not.toHaveAttribute('data-context-menu-open', 'true')

    const anchor = document.body.querySelector(
      '[data-context-menu-anchor="true"]',
    ) as HTMLElement | null
    expect(anchor!.style.left).toBe('360px')
    expect(anchor!.style.top).toBe('420px')

    fireEvent.click(row2Item)
    expect(onInspect).toHaveBeenCalledTimes(1)
    expect(onInspect).toHaveBeenCalledWith(SAMPLE_ROWS[1])
  })

  it('scopes text-selection check to the clicked row without blocking when text elsewhere on the page is selected', async () => {
    render(
      <div>
        <p data-testid="outside-text">External heading outside table</p>
        <DataTable<SampleRow>
          columns={SAMPLE_COLUMNS}
          data={SAMPLE_ROWS}
          getRowKey={(r) => r.id}
          rowContextMenu={() => [
            { key: 'action', label: 'Row Action', onClick: vi.fn() },
          ]}
        />
      </div>,
    )

    const outsideNode = screen.getByTestId('outside-text').firstChild!
    const outsideRange = document.createRange()
    outsideRange.setStart(outsideNode, 0)
    outsideRange.setEnd(outsideNode, 8)
    const selection = window.getSelection()!
    selection.removeAllRanges()
    selection.addRange(outsideRange)

    const row1 = screen.getByText('Row Alpha').closest('tr')!
    fireEvent.contextMenu(row1, { button: 2, clientX: 150, clientY: 180 })
    expect(
      await screen.findByRole('menuitem', { name: 'Row Action' }),
    ).toBeInTheDocument()

    // Dismiss menu first, then select text INSIDE Row 2
    fireEvent.keyDown(document.activeElement || document.body, { key: 'Escape' })
    await waitFor(() => {
      expect(screen.queryByRole('menuitem', { name: 'Row Action' })).toBeNull()
    })

    const insideNode = screen.getByText('Row Beta').firstChild!
    const insideRange = document.createRange()
    insideRange.setStart(insideNode, 0)
    insideRange.setEnd(insideNode, 4)
    selection.removeAllRanges()
    selection.addRange(insideRange)

    const row2 = screen.getByText('Row Beta').closest('tr')!
    const notPrevented = fireEvent.contextMenu(row2, {
      button: 2,
      clientX: 200,
      clientY: 220,
    })
    expect(notPrevented).toBe(true)
    expect(screen.queryByRole('menuitem', { name: 'Row Action' })).toBeNull()
  })

  it('auto-dismisses open menu on window scroll and resize, and restores focus on Shift+F10 -> Escape', async () => {
    render(
      <DataTable<SampleRow>
        columns={SAMPLE_COLUMNS}
        data={SAMPLE_ROWS}
        getRowKey={(r) => r.id}
        rowContextMenu={() => [
          { key: 'action', label: 'Row Action', onClick: vi.fn() },
        ]}
      />,
    )

    const row1 = screen.getByText('Row Alpha').closest('tr')!
    fireEvent.contextMenu(row1, { button: 2, clientX: 100, clientY: 100 })
    expect(
      await screen.findByRole('menuitem', { name: 'Row Action' }),
    ).toBeInTheDocument()

    fireEvent.scroll(window)
    await waitFor(() => {
      expect(screen.queryByRole('menuitem', { name: 'Row Action' })).toBeNull()
    })

    fireEvent.contextMenu(row1, { button: 2, clientX: 100, clientY: 100 })
    expect(
      await screen.findByRole('menuitem', { name: 'Row Action' }),
    ).toBeInTheDocument()
    fireEvent(window, new Event('resize'))
    await waitFor(() => {
      expect(screen.queryByRole('menuitem', { name: 'Row Action' })).toBeNull()
    })

    // Keyboard Shift+F10 -> Escape restores focus to the row
    row1.focus()
    fireEvent.keyDown(row1, { key: 'F10', shiftKey: true })
    const item = await screen.findByRole('menuitem', { name: 'Row Action' })
    fireEvent.keyDown(item, { key: 'Escape' })
    await waitFor(() => {
      expect(screen.queryByRole('menuitem', { name: 'Row Action' })).toBeNull()
    })
    await waitFor(() => {
      expect(document.activeElement).toBe(row1)
    })
  })
})

describe('Newly Wired Row Context Menus & Model B RBAC', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: {
        writeText: vi.fn().mockResolvedValue(undefined),
      },
    })
  })

  it('provides Release / Detain / Copy context actions on DetainedLicensesPage', async () => {
    const mockDetentions: DetainedLicense[] = [
      {
        detainId: 101,
        licenseId: 12,
        detainDate: '2026-10-01T00:00:00Z',
        fineFees: 50,
        createdByUserId: 1,
        isReleased: false,
        releaseDate: null,
        releasedByUserId: null,
        releaseApplicationId: null,
        fullName: 'Ahmad Al-Mansoor',
        nationalNo: 'N1001',
      },
      {
        detainId: 102,
        licenseId: 15,
        detainDate: '2026-09-10T00:00:00Z',
        fineFees: 30,
        createdByUserId: 1,
        isReleased: true,
        releaseDate: '2026-09-15T00:00:00Z',
        releasedByUserId: 1,
        releaseApplicationId: 88,
        fullName: 'Sara Haddad',
        nationalNo: 'N2002',
      },
    ]

    vi.spyOn(detainedService, 'getDetainedLicensesPage').mockResolvedValue({
      items: mockDetentions,
      pageNumber: 1,
      pageSize: 10,
      totalCount: mockDetentions.length,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    })
    const onNavigateToDetain = vi.fn()

    render(
      <DetainedLicensesPage
        accessToken="tok"
        onNavigateToDetain={onNavigateToDetain}
      />,
    )

    const activeRowText = await screen.findByText('Ahmad Al-Mansoor')
    const releasedRowText = screen.getByText('Sara Haddad')

    fireEvent.contextMenu(releasedRowText.closest('tr')!, {
      button: 2,
      clientX: 110,
      clientY: 110,
    })
    const disabledReleaseItem = await screen.findByRole('menuitem', {
      name: 'Already Released',
    })
    expect(disabledReleaseItem).toHaveAttribute('data-disabled')

    fireEvent.contextMenu(activeRowText.closest('tr')!, {
      button: 2,
      clientX: 150,
      clientY: 180,
    })
    const detainNewItem = await screen.findByRole('menuitem', {
      name: 'Detain New License',
    })
    fireEvent.click(detainNewItem)
    expect(onNavigateToDetain).toHaveBeenCalledTimes(1)
  })

  it('provides Select for Renewal context action on ExpiredLicensesTable', async () => {
    const onSelectLicense = vi.fn()
    const expiredLicenses: License[] = [
      {
        licenseId: 501,
        applicationId: 10,
        driverId: 2,
        driverName: 'Omar Ziad',
        licenseClassId: 3,
        licenseClassName: 'Class 3 - Ordinary',
        issueDate: '2020-01-01T00:00:00Z',
        expirationDate: '2025-01-01T00:00:00Z',
        notes: null,
        paidFees: 20,
        isActive: true,
        isExpired: true,
        isEligibleForRenewal: true,
        ineligibilityReason: null,
        issueReason: 1,
        issueReasonText: 'FirstTime',
        createdByUserId: 1,
        createdByUserName: 'admin',
      },
    ]

    render(
      <ExpiredLicensesTable
        expiredLicenses={expiredLicenses}
        selectedLicenseId={null}
        loading={false}
        loadingDetails={false}
        onSelectLicense={onSelectLicense}
        onRefresh={vi.fn()}
      />,
    )

    const row = screen.getByText('#501').closest('tr')!
    fireEvent.contextMenu(row, { button: 2, clientX: 180, clientY: 200 })

    const selectItem = await screen.findByRole('menuitem', {
      name: 'Select for Renewal',
    })
    fireEvent.click(selectItem)
    expect(onSelectLicense).toHaveBeenCalledWith(501)
  })

  it('provides Copy Country Name context action on CountriesPage', async () => {
    vi.spyOn(countryService, 'getCountries').mockResolvedValue([
      { countryId: 90, countryName: 'Jordan' },
    ])

    render(<CountriesPage accessToken="tok" />)

    const jordanRow = (await screen.findByText('Jordan')).closest('tr')!
    fireEvent.contextMenu(jordanRow, { button: 2, clientX: 140, clientY: 160 })

    const copyItem = await screen.findByRole('menuitem', {
      name: 'Copy Country Name',
    })
    fireEvent.click(copyItem)
    await waitFor(() => {
      expect(navigator.clipboard.writeText).toHaveBeenCalledWith('Jordan')
    })
  })

  it('enforces Model B RBAC on ApplicationTypesPage, LicenseClassesPage, and TestTypesPage row context menus', async () => {
    vi.spyOn(applicationTypeService, 'getApplicationTypes').mockResolvedValue([
      {
        applicationTypeId: 1,
        applicationTypeTitle: 'New Local Driving License',
        applicationTypeFees: 15,
      },
    ])
    vi.spyOn(licenseClassService, 'getLicenseClasses').mockResolvedValue([
      {
        licenseClassId: 3,
        licenseClassName: 'Class 3 - Ordinary',
        licenseClassDescription: 'Standard vehicles',
        minAllowedAge: 18,
        defaultValidityLength: 10,
        licenseClassFees: 20,
      },
    ])
    vi.spyOn(testTypeService, 'getTestTypes').mockResolvedValue([
      {
        testTypeId: 1,
        testTypeTitle: 'Vision Test',
        testTypeDescription: 'Eyesight examination',
        testTypeFees: 10,
      },
    ])

    // 1. Staff on ApplicationTypesPage: Copy allowed, Edit omitted
    const { unmount: unmountStaff } = render(
      <ApplicationTypesPage accessToken="tok" userRole="Staff" />,
    )
    const staffAppRow = (
      await screen.findByText('New Local Driving License')
    ).closest('tr')!
    fireEvent.contextMenu(staffAppRow, { button: 2, clientX: 120, clientY: 120 })
    expect(
      await screen.findByRole('menuitem', { name: 'Copy Type Name' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('menuitem', { name: 'Edit Application Type' }),
    ).toBeNull()
    unmountStaff()

    // 2. Admin on ApplicationTypesPage: Edit opens inline mode, and editing row ignores context menu
    const { unmount: unmountAdminApp } = render(
      <ApplicationTypesPage accessToken="tok" userRole="Admin" />,
    )
    const adminAppRow = (
      await screen.findByText('New Local Driving License')
    ).closest('tr')!
    fireEvent.contextMenu(adminAppRow, { button: 2, clientX: 120, clientY: 120 })
    const editAppItem = await screen.findByRole('menuitem', {
      name: 'Edit Application Type',
    })
    fireEvent.click(editAppItem)
    expect(
      screen.getByDisplayValue('New Local Driving License'),
    ).toBeInTheDocument()
    fireEvent.contextMenu(adminAppRow, { button: 2, clientX: 130, clientY: 130 })
    expect(
      screen.queryByRole('menuitem', { name: 'Edit Application Type' }),
    ).toBeNull()
    unmountAdminApp()

    // 3. Staff vs Admin on LicenseClassesPage & TestTypesPage
    const { unmount: unmountLcStaff } = render(
      <LicenseClassesPage accessToken="tok" userRole="Staff" />,
    )
    fireEvent.contextMenu(
      (await screen.findByText('Class 3 - Ordinary')).closest('tr')!,
      { button: 2, clientX: 120, clientY: 120 },
    )
    expect(
      await screen.findByRole('menuitem', { name: 'Copy Class Name' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('menuitem', { name: 'Edit License Class' }),
    ).toBeNull()
    unmountLcStaff()

    const { unmount: unmountTtAdmin } = render(
      <TestTypesPage accessToken="tok" userRole="Admin" />,
    )
    fireEvent.contextMenu(
      (await screen.findByText('Vision Test')).closest('tr')!,
      { button: 2, clientX: 120, clientY: 120 },
    )
    expect(
      await screen.findByRole('menuitem', { name: 'Edit Test Type' }),
    ).toBeInTheDocument()
    unmountTtAdmin()
  })
})
