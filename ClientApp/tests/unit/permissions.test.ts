import { describe, expect, it } from 'vitest'
import {
  PAGE_PERMISSIONS,
  MANAGEABLE_REFERENCE_PAGES,
  VALID_ROLES,
  canEditPage,
  canManageReferenceData,
  canViewPage,
  isPagePermitted,
  isValidRole,
  type PageId,
} from '../../src/utils/permissions'
import {
  buildVisibleSections,
  isLinkActive,
  type SidebarLinkDef,
} from '../../src/components/layout/sidebar/sidebarNavigation'

const STAFF_ONLY_PAGES: readonly PageId[] = [
  'Drivers',
  'Licenses',
  'Person License History',
  'International Licenses',
  'Local Applications',
  'Test Appointments',
  'Manage Test Appointments',
  'Written Test Appointments',
  'Appointment Details',
  'Schedule Appointment',
  'Take Examination',
  'Renew & Replace License',
  'Renew License',
  'Replace License',
  'License Detention',
  'Detain License',
  'Detained Licenses',
]

const SHARED_PAGES: readonly PageId[] = [
  'Dashboard',
  'People',
  'Add Person',
  'Edit Person',
  'Person Details',
  'Application Types',
  'License Classes',
  'Test Types',
  'Countries',
  'My Profile',
  'Change Password',
]

describe('Frontend RBAC Permission Matrix (ADR-O01 Model B)', () => {
  it('permits Person License History strictly for Staff and denies Admin', () => {
    expect(PAGE_PERMISSIONS['Person License History']).toEqual(['Staff'])
    expect(isPagePermitted('Person License History', 'Staff')).toBe(true)
    expect(isPagePermitted('Person License History', 'Admin')).toBe(false)
    expect(canViewPage('Person License History', 'Staff')).toBe(true)
    expect(canViewPage('Person License History', 'Admin')).toBe(false)
  })

  it('fails closed for missing, null, unrecognized roles, and unknown pages', () => {
    expect(isValidRole('Admin')).toBe(true)
    expect(isValidRole('Staff')).toBe(true)
    expect(VALID_ROLES).toEqual(['Admin', 'Staff'])

    const invalidRoles: Array<string | null | undefined> = [
      undefined,
      null,
      '',
      'admin',
      'staff',
      'Supervisor',
      'Guest',
    ]

    for (const invalidRole of invalidRoles) {
      expect(isValidRole(invalidRole)).toBe(false)
      expect(isPagePermitted('Dashboard', invalidRole)).toBe(false)
      expect(isPagePermitted('People', invalidRole)).toBe(false)
      expect(isPagePermitted('Person License History', invalidRole)).toBe(false)
      expect(isPagePermitted('User Management', invalidRole)).toBe(false)
    }

    expect(isPagePermitted('NonExistentPage', 'Admin')).toBe(false)
    expect(isPagePermitted('NonExistentPage', 'Staff')).toBe(false)
  })

  it('preserves shared People, reference data, account, and Dashboard permissions for both Admin and Staff', () => {
    for (const page of SHARED_PAGES) {
      expect(isPagePermitted(page, 'Admin')).toBe(true)
      expect(isPagePermitted(page, 'Staff')).toBe(true)
    }
  })

  it('keeps User Management Admin-only and operational counter workflows Staff-only', () => {
    expect(isPagePermitted('User Management', 'Admin')).toBe(true)
    expect(isPagePermitted('User Management', 'Staff')).toBe(false)

    for (const page of STAFF_ONLY_PAGES) {
      expect(isPagePermitted(page, 'Staff')).toBe(true)
      expect(isPagePermitted(page, 'Admin')).toBe(false)
    }

    // Ensure test arrays cover every PageId without gaps
    const coveredPages = new Set<string>([
      'User Management',
      ...STAFF_ONLY_PAGES,
      ...SHARED_PAGES,
    ])
    expect(coveredPages.size).toBe(Object.keys(PAGE_PERMISSIONS).length)
  })

  it('restricts reference-data modification exclusively to Admin and keeps Countries read-only', () => {
    expect(MANAGEABLE_REFERENCE_PAGES).toEqual([
      'Application Types',
      'License Classes',
      'Test Types',
    ])

    for (const page of MANAGEABLE_REFERENCE_PAGES) {
      expect(canManageReferenceData(page, 'Admin')).toBe(true)
      expect(canEditPage(page, 'Admin')).toBe(true)
      expect(canManageReferenceData(page, 'Staff')).toBe(false)
      expect(canEditPage(page, 'Staff')).toBe(false)
    }

    expect(canManageReferenceData('Countries', 'Admin')).toBe(false)
    expect(canManageReferenceData('Countries', 'Staff')).toBe(false)
    expect(canEditPage('Countries', 'Admin')).toBe(false)
    expect(canManageReferenceData('Application Types', null)).toBe(false)
    expect(canManageReferenceData('People', 'Admin')).toBe(false)
  })
})

describe('Sidebar Visibility Filtering (sidebarNavigation.ts)', () => {
  function collectSidebarPages(role: string | null | undefined): string[] {
    return buildVisibleSections(role).flatMap((section) =>
      section.entries.flatMap((entry) =>
        entry.kind === 'link'
          ? [entry.page]
          : entry.items.map((item) => item.page),
      ),
    )
  }

  it('excludes Drivers and operational counter sections for Admin while keeping User Management, People, and Settings', () => {
    const adminSections = buildVisibleSections('Admin')
    const adminSectionIds = adminSections.map((s) => s.id)
    const adminPages = collectSidebarPages('Admin')

    expect(adminSectionIds).toEqual([
      'main',
      'directory',
      'settings',
      'account',
    ])
    expect(adminPages).toContain('Dashboard')
    expect(adminPages).toContain('User Management')
    expect(adminPages).toContain('People')
    expect(adminPages).toContain('Application Types')
    expect(adminPages).toContain('License Classes')
    expect(adminPages).toContain('Test Types')
    expect(adminPages).toContain('Countries')
    expect(adminPages).toContain('My Profile')
    expect(adminPages).toContain('Change Password')

    expect(adminPages).not.toContain('Drivers')
    expect(adminPages).not.toContain('Person License History')
    expect(adminPages).not.toContain('Licenses')
    expect(adminPages).not.toContain('Local Applications')
    expect(adminPages).not.toContain('International Licenses')
    expect(adminPages).not.toContain('Test Appointments')
    expect(adminPages).not.toContain('Renew & Replace License')
    expect(adminPages).not.toContain('License Detention')
  })

  it('retains Drivers (activeFor Person License History) and operational counter workflows for Staff while hiding User Management', () => {
    const staffSections = buildVisibleSections('Staff')
    const staffSectionIds = staffSections.map((s) => s.id)
    const staffPages = collectSidebarPages('Staff')

    expect(staffSectionIds).toEqual([
      'main',
      'directory',
      'applications',
      'license-services',
      'settings',
      'account',
    ])
    expect(staffPages).toContain('Drivers')
    expect(staffPages).toContain('Licenses')
    expect(staffPages).toContain('Local Applications')
    expect(staffPages).toContain('International Licenses')
    expect(staffPages).toContain('People')
    expect(staffPages).toContain('Countries')
    expect(staffPages).not.toContain('User Management')

    const directorySection = staffSections.find((s) => s.id === 'directory')
    const driversLink = directorySection?.entries.find(
      (e): e is SidebarLinkDef => e.kind === 'link' && e.page === 'Drivers',
    )
    expect(driversLink).toBeDefined()
    expect(isLinkActive(driversLink!, 'Person License History')).toBe(true)
  })

  it('returns no sections when role is null, undefined, or unrecognized', () => {
    expect(buildVisibleSections(null)).toEqual([])
    expect(buildVisibleSections(undefined)).toEqual([])
    expect(buildVisibleSections('UnknownRole')).toEqual([])
  })
})
