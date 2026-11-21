import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'

function buildSessionPayload(role: 'Staff' | 'Admin') {
  return {
    accessToken: `e2e-jwt-${role.toLowerCase()}`,
    expiresAtUtc: new Date(Date.now() + 15 * 60_000).toISOString(),
    userId: 1,
    userName: role.toLowerCase(),
    personId: 10,
    fullName: `${role} User`,
    role,
  }
}

async function mockCommonAuthenticatedApi(
  page: Page,
  role: 'Staff' | 'Admin',
  options?: { logoutStatus?: number },
) {
  await page.route('**/api/**', async (route) => {
    const url = route.request().url()

    if (url.includes('/Auth/refresh')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(buildSessionPayload(role)),
      })
    }

    if (url.includes('/Auth/logout')) {
      return route.fulfill({
        status: options?.logoutStatus ?? 204,
      })
    }

    if (url.includes('/Dashboard/statistics')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          people: 5,
          drivers: 2,
          users: 2,
          licenses: 1,
          internationalLicenses: 0,
          detainedLicenses: 0,
          localDrivingLicenseApplications: 1,
          testAppointments: 1,
        }),
      })
    }

    if (url.includes('/LocalDrivingLicenseApplications/summary')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          totalApplications: 1,
          inProgressCount: 1,
          readyForIssuanceCount: 0,
        }),
      })
    }

    if (url.includes('/Countries')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([{ countryId: 1, countryName: 'Jordan' }]),
      })
    }

    if (url.includes('/People/10')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          personId: 10,
          nationalNo: 'NAT-100',
          firstName: 'Ayhm',
          secondName: 'Test',
          thirdName: null,
          lastName: 'User',
          fullName: 'Ayhm Test User',
          dateOfBirth: '1995-01-01T00:00:00',
          gender: 0,
          address: 'Amman',
          phone: '0790000000',
          email: 'ayhm@example.com',
          nationalityCountryID: 1,
          countryName: 'Jordan',
          imagePath: null,
          rowVersion: 'AAAAAAAA',
        }),
      })
    }

    if (url.includes('/People')) {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              personId: 10,
              nationalNo: 'NAT-100',
              firstName: 'Ayhm',
              secondName: 'Test',
              thirdName: null,
              lastName: 'User',
              fullName: 'Ayhm Test User',
              dateOfBirth: '1995-01-01T00:00:00',
              gender: 0,
              address: 'Amman',
              phone: '0790000000',
              email: 'ayhm@example.com',
              nationalityCountryID: 1,
              countryName: 'Jordan',
              imagePath: null,
              rowVersion: 'AAAAAAAA',
            },
          ],
          pageNumber: 1,
          pageSize: 10,
          totalCount: 1,
          totalPages: 1,
          hasPreviousPage: false,
          hasNextPage: false,
        }),
      })
    }

    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items: [], totalCount: 0, pageNumber: 1, pageSize: 10, totalPages: 0 }),
    })
  })
}

test('Staff user is denied Users page while Admin can access Users', async ({ page }) => {
  await mockCommonAuthenticatedApi(page, 'Staff')
  await page.goto('/users')

  await expect(page.getByRole('link', { name: 'Users' })).toHaveCount(0)
  await expect(page.getByRole('heading', { name: /Restricted|Access Denied|Unauthorized/i })).toBeVisible()
})

test('Person Details -> Edit Person preserves personId and loads edit form', async ({ page }) => {
  await mockCommonAuthenticatedApi(page, 'Staff')
  await page.goto('/people')

  await expect(page.getByText('Ayhm Test User')).toBeVisible()
  await page.getByRole('button', { name: /Actions|More/i }).first().click()
  await page.getByRole('menuitem', { name: /Show Details|Details/i }).click()

  await expect(page.getByRole('button', { name: /Edit Person/i })).toBeVisible()
  await page.getByRole('button', { name: /Edit Person/i }).click()

  await expect(page.getByText('No Person Specified for Editing')).toHaveCount(0)
  await expect(page.getByRole('heading', { name: 'Edit Person' })).toBeVisible()
  await expect(page.getByLabel(/National Number/i)).toHaveValue('NAT-100')
})

test('Logout displays server revocation warning when /Auth/logout returns 500', async ({ page }) => {
  await mockCommonAuthenticatedApi(page, 'Staff', { logoutStatus: 500 })
  await page.goto('/')

  await expect(page.getByRole('button', { name: /Sign Out/i }).first()).toBeVisible()
  await page.getByRole('button', { name: /Sign Out/i }).first().click()

  const dialog = page.getByRole('dialog', { name: /Sign Out of DVLD/i })
  await expect(dialog).toBeVisible()
  await dialog.getByRole('button', { name: /^Sign Out$/i }).click()

  await expect(page.getByRole('heading', { name: 'Welcome back', exact: true })).toBeVisible()
  await expect(page.getByText(/could not confirm session revocation/i)).toBeVisible()
})
