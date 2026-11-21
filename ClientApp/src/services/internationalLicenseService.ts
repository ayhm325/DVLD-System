import type {
  InternationalLicense,
  InternationalLicenseDetails,
} from '../types/InternationalLicense'
import type { LocalLicenseInfo } from '../types/License'

import { apiRequest } from './apiClient'

export interface InternationalLicensePageResponse {
  items: InternationalLicense[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

export const getInternationalLicenses = (
  token: string,
): Promise<InternationalLicense[]> =>
  apiRequest<InternationalLicense[]>('/InternationalLicenses', token)

export const getInternationalLicensesPage = (
  token: string,
  pageNumber = 1,
  pageSize = 10,
  searchQuery?: string,
  searchBy?: string,
  statusFilter?: string,
): Promise<InternationalLicensePageResponse> => {
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
  if (statusFilter && statusFilter.toLowerCase() !== 'all') {
    params.set('statusFilter', statusFilter)
  }

  return apiRequest<InternationalLicensePageResponse>(
    `/InternationalLicenses?${params.toString()}`,
    token,
  )
}

export const getInternationalLicensesByDriverId = (
  token: string,
  driverId: number,
): Promise<InternationalLicense[]> =>
  apiRequest<InternationalLicense[]>(`/InternationalLicenses/driver/${driverId}`, token)

export const getInternationalLicenseById = (
  token: string,
  id: number,
): Promise<InternationalLicenseDetails> =>
  apiRequest<InternationalLicenseDetails>(`/InternationalLicenses/${id}`, token)

export const getLocalLicenseInfo = (
  token: string,
  localLicenseId: number,
): Promise<LocalLicenseInfo> =>
  apiRequest<LocalLicenseInfo>(
    `/InternationalLicenses/license/${localLicenseId}/info`,
    token,
  )

export const issueInternationalLicense = (
  token: string,
  localLicenseId: number,
): Promise<InternationalLicenseDetails> =>
  apiRequest<InternationalLicenseDetails>('/InternationalLicenses', token, {
    method: 'POST',
    body: JSON.stringify({ localLicenseId }),
  })
