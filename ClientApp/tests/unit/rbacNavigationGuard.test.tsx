import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { useEffect } from 'react'
import { NavigationProvider } from '../../src/context/NavigationContext'
import { useNavigation } from '../../src/context/useNavigation'
import AppRouter from '../../src/components/layout/AppRouter'
import CommandPalette from '../../src/components/layout/CommandPalette'
import * as peopleService from '../../src/services/peopleService'
import * as licenseService from '../../src/services/licenseService'
import * as internationalLicenseService from '../../src/services/internationalLicenseService'
import * as dashboardService from '../../src/services/dashboardService'
import * as localAppService from '../../src/services/localDrivingLicenseApplicationService'
import * as licenseClassService from '../../src/services/licenseClassService'
import * as testTypeService from '../../src/services/testTypeService'
import * as applicationTypeService from '../../src/services/applicationTypeService'

vi.mock('../../src/services/peopleService', () => ({
  getPersonById: vi.fn(),
  getPersonPhotoBlob: vi.fn(),
}))

vi.mock('../../src/services/licenseService', () => ({
  getLicensesByPersonId: vi.fn(),
  getLicensesByDriverId: vi.fn(),
}))

vi.mock('../../src/services/internationalLicenseService', () => ({
  getInternationalLicensesByDriverId: vi.fn(),
}))

vi.mock('../../src/services/dashboardService', () => ({
  getDashboardStatistics: vi.fn(),
}))

vi.mock('../../src/services/localDrivingLicenseApplicationService', () => ({
  getLocalDrivingLicenseApplicationsSummary: vi.fn(),
}))

vi.mock('../../src/services/licenseClassService', () => ({
  getLicenseClasses: vi.fn(),
}))

vi.mock('../../src/services/testTypeService', () => ({
  getTestTypes: vi.fn(),
}))

vi.mock('../../src/services/applicationTypeService', () => ({
  getApplicationTypes: vi.fn(),
}))

vi.mock('../../src/context/usePeopleData', () => ({
  usePeopleData: () => ({
    people: [],
    loading: false,
    error: null,
    refreshPeople: vi.fn(),
  }),
}))

const SAMPLE_PERSON = {
  personId: 42,
  nationalNo: 'N123456',
  fullName: 'Layla Al-Mansoor',
  firstName: 'Layla',
  secondName: 'Ahmad',
  thirdName: null,
  lastName: 'Al-Mansoor',
  dateOfBirth: '1995-05-12',
  gender: 1,
  address: 'Amman',
  phone: '0790000000',
  email: 'layla@example.com',
  nationalityCountryID: 1,
  countryName: 'Jordan',
  imagePath: null,
  rowVersion: 'AQIDBAUGBwg=',
}

function renderRouterAtUrl(url: string, userRole: string | null | undefined) {
  window.history.replaceState(null, '', url)

  return render(
    <NavigationProvider>
      <AppRouter
        accessToken="test-token"
        userRole={userRole}
        showIssueInternational={false}
        setShowIssueInternational={vi.fn()}
        internationalRefreshKey={0}
        onInternationalIssued={vi.fn()}
        statisticsRefreshKey={0}
      />
    </NavigationProvider>,
  )
}

describe('RBAC Route Guards & Navigation Isolation (A-03 / Model B)', () => {
  beforeEach(() => {
    window.history.replaceState(null, '', '/')

    vi.mocked(peopleService.getPersonById).mockResolvedValue(SAMPLE_PERSON)
    vi.mocked(licenseService.getLicensesByPersonId).mockResolvedValue([])
    vi.mocked(licenseService.getLicensesByDriverId).mockResolvedValue([])
    vi.mocked(
      internationalLicenseService.getInternationalLicensesByDriverId,
    ).mockResolvedValue([])
    vi.mocked(dashboardService.getDashboardStatistics).mockResolvedValue({
      totalPeople: 1,
      totalDrivers: 1,
      activeLicenses: 1,
      pendingApplications: 0,
      localDrivingLicenseApplications: 1,
      internationalLicenses: 0,
      detainedLicenses: 0,
      upcomingTests: 0,
    })
    vi.mocked(
      localAppService.getLocalDrivingLicenseApplicationsSummary,
    ).mockResolvedValue({
      totalApplications: 1,
      inProgressCount: 0,
      readyForIssuanceCount: 0,
    })
    vi.mocked(licenseClassService.getLicenseClasses).mockResolvedValue([])
    vi.mocked(testTypeService.getTestTypes).mockResolvedValue([])
    vi.mocked(applicationTypeService.getApplicationTypes).mockResolvedValue([])
  })

  it('denies Admin direct navigation to /licenses/history without mounting LicenseHistoryPage or calling license APIs', async () => {
    renderRouterAtUrl('/licenses/history?personId=42', 'Admin')

    const alert = await screen.findByRole('alert')
    expect(alert).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { name: 'Access Denied' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Person License History')).toBeInTheDocument()
    expect(screen.getByText('Admin')).toBeInTheDocument()

    expect(
      screen.queryByText(/Comprehensive license portfolio/i),
    ).not.toBeInTheDocument()
    expect(peopleService.getPersonById).not.toHaveBeenCalled()
    expect(licenseService.getLicensesByPersonId).not.toHaveBeenCalled()
    expect(licenseService.getLicensesByDriverId).not.toHaveBeenCalled()
    expect(
      internationalLicenseService.getInternationalLicensesByDriverId,
    ).not.toHaveBeenCalled()

    fireEvent.click(
      screen.getByRole('button', { name: /Return to Dashboard/i }),
    )
    await waitFor(() => {
      expect(window.location.pathname).toBe('/')
    })
  })

  it('denies Admin when navigating programmatically via navigateToPersonLicenseHistory and when role switches from Staff to Admin', async () => {
    function AutoNavigateTrigger() {
      const { navigateToPersonLicenseHistory } = useNavigation()
      useEffect(() => {
        navigateToPersonLicenseHistory(42, null, 'People')
      }, [navigateToPersonLicenseHistory])
      return null
    }

    const { unmount } = render(
      <NavigationProvider>
        <AutoNavigateTrigger />
        <AppRouter
          accessToken="test-token"
          userRole="Admin"
          showIssueInternational={false}
          setShowIssueInternational={vi.fn()}
          internationalRefreshKey={0}
          onInternationalIssued={vi.fn()}
          statisticsRefreshKey={0}
        />
      </NavigationProvider>,
    )

    expect(
      await screen.findByRole('heading', { name: 'Access Denied' }),
    ).toBeInTheDocument()
    expect(licenseService.getLicensesByPersonId).not.toHaveBeenCalled()
    expect(peopleService.getPersonById).not.toHaveBeenCalled()

    unmount()

    // Role transition: Staff -> Admin while viewing Person License History immediately triggers AccessDenied
    window.history.replaceState(null, '', '/licenses/history?personId=42')
    const { rerender } = render(
      <NavigationProvider>
        <AppRouter
          accessToken="test-token"
          userRole="Staff"
          showIssueInternational={false}
          setShowIssueInternational={vi.fn()}
          internationalRefreshKey={0}
          onInternationalIssued={vi.fn()}
          statisticsRefreshKey={0}
        />
      </NavigationProvider>,
    )

    await waitFor(() => {
      expect(licenseService.getLicensesByPersonId).toHaveBeenCalledWith(
        'test-token',
        42,
      )
    })

    rerender(
      <NavigationProvider>
        <AppRouter
          accessToken="test-token"
          userRole="Admin"
          showIssueInternational={false}
          setShowIssueInternational={vi.fn()}
          internationalRefreshKey={0}
          onInternationalIssued={vi.fn()}
          statisticsRefreshKey={0}
        />
      </NavigationProvider>,
    )

    expect(
      await screen.findByRole('heading', { name: 'Access Denied' }),
    ).toBeInTheDocument()
  })

  it('allows Staff to open /licenses/history and loads person license history', async () => {
    renderRouterAtUrl('/licenses/history?personId=42', 'Staff')

    await waitFor(() => {
      expect(peopleService.getPersonById).toHaveBeenCalledWith('test-token', 42)
      expect(licenseService.getLicensesByPersonId).toHaveBeenCalledWith(
        'test-token',
        42,
      )
    })

    expect(
      screen.queryByRole('heading', { name: 'Access Denied' }),
    ).not.toBeInTheDocument()
    expect(
      await screen.findByRole('heading', { name: 'Layla Al-Mansoor' }),
    ).toBeInTheDocument()
  })

  it('preserves shared Person Details access for Admin and keeps User Management denied for Staff', async () => {
    renderRouterAtUrl('/people/details?id=42', 'Admin')

    await waitFor(() => {
      expect(peopleService.getPersonById).toHaveBeenCalledWith('test-token', 42)
    })
    expect(
      screen.queryByRole('heading', { name: 'Access Denied' }),
    ).not.toBeInTheDocument()
    expect(await screen.findByTestId('person-full-name')).toHaveTextContent(
      'Layla Al-Mansoor',
    )

    renderRouterAtUrl('/users', 'Staff')
    expect(
      await screen.findByRole('heading', { name: 'Access Denied' }),
    ).toBeInTheDocument()
    expect(screen.getByText('User Management')).toBeInTheDocument()
  })

  it('filters CommandPalette items strictly by role permissions', () => {
    if (!HTMLElement.prototype.scrollIntoView) {
      HTMLElement.prototype.scrollIntoView = vi.fn()
    }

    const { rerender } = render(
      <NavigationProvider>
        <CommandPalette isOpen onClose={vi.fn()} userRole="Admin" />
      </NavigationProvider>,
    )

    expect(screen.getByText('People Registry')).toBeInTheDocument()
    expect(screen.getByText('User Management')).toBeInTheDocument()
    expect(screen.getByText('Application Types & Fees')).toBeInTheDocument()
    expect(screen.queryByText('Driver Records')).not.toBeInTheDocument()
    expect(screen.queryByText('Driver Licenses')).not.toBeInTheDocument()
    expect(
      screen.queryByText('Local Driving Applications'),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByText('International Driving Permits'),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByText(/Person License History/i),
    ).not.toBeInTheDocument()

    rerender(
      <NavigationProvider>
        <CommandPalette isOpen onClose={vi.fn()} userRole="Staff" />
      </NavigationProvider>,
    )

    expect(screen.getByText('People Registry')).toBeInTheDocument()
    expect(screen.getByText('Driver Records')).toBeInTheDocument()
    expect(screen.getByText('Driver Licenses')).toBeInTheDocument()
    expect(screen.getByText('Local Driving Applications')).toBeInTheDocument()
    expect(screen.queryByText('User Management')).not.toBeInTheDocument()
  })
})
