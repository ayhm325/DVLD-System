import { apiRequest } from './apiClient'
import type {
  DetainedLicense,
  CreateDetainedLicenseRequest,
  ReleaseDetainedLicenseRequest,
} from '../types/DetainedLicense'

export type { DetainedLicense, CreateDetainedLicenseRequest, ReleaseDetainedLicenseRequest }

export interface DetainedLicensePageResponse {
  items: DetainedLicense[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

export function getDetainedLicenses(accessToken: string, signal?: AbortSignal): Promise<DetainedLicense[]> {
  return apiRequest<DetainedLicense[]>('/DetainedLicenses', accessToken, { signal })
}

export function getDetainedLicensesPage(
  accessToken: string,
  pageNumber = 1,
  pageSize = 10,
  searchQuery?: string,
  searchBy?: string,
  statusFilter?: string,
  signal?: AbortSignal,
): Promise<DetainedLicensePageResponse> {
  const params = new URLSearchParams({
    pageNumber: String(pageNumber),
    pageSize: String(pageSize),
  })

  if (searchQuery && searchQuery.trim()) {
    params.set('searchQuery', searchQuery.trim())
  }
  if (searchBy && searchBy !== 'all') {
    params.set('searchBy', searchBy)
  }
  if (statusFilter && statusFilter !== 'all') {
    params.set('statusFilter', statusFilter)
  }

  return apiRequest<DetainedLicensePageResponse>(
    `/DetainedLicenses?${params.toString()}`,
    accessToken,
    { signal },
  )
}

export function getDetainedLicenseById(
  accessToken: string,
  id: number,
): Promise<DetainedLicense> {
  return apiRequest<DetainedLicense>(`/DetainedLicenses/${id}`, accessToken)
}

export function getActiveDetainByLicenseId(
  accessToken: string,
  licenseId: number,
): Promise<DetainedLicense> {
  return apiRequest<DetainedLicense>(
    `/DetainedLicenses/license/${licenseId}/active`,
    accessToken,
  )
}

export function checkIsLicenseDetained(
  accessToken: string,
  licenseId: number,
): Promise<{ detained: boolean }> {
  return apiRequest<{ detained: boolean }>(
    `/DetainedLicenses/license/${licenseId}/detained`,
    accessToken,
  )
}

export function detainLicense(
  accessToken: string,
  payload: CreateDetainedLicenseRequest,
): Promise<DetainedLicense> {
  return apiRequest<DetainedLicense>('/DetainedLicenses', accessToken, {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

export function releaseDetainedLicense(
  accessToken: string,
  detainId: number,
): Promise<void> {
  return apiRequest<void>('/DetainedLicenses/release', accessToken, {
    method: 'POST',
    body: JSON.stringify({ detainId }),
  })
}
