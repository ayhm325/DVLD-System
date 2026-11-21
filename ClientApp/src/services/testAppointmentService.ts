import { apiRequest } from './apiClient'
import type {
  AppointmentAvailabilityResponse,
  ExaminationAttemptStats,
  SaveTestResultPayload,
  ScheduleTestPayload,
  ScheduleTestPreparation,
  TestAppointment,
  TestResponse,
} from '../types/TestAppointment'

export interface TestAppointmentPageResponse {
  items: TestAppointment[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

export function getAllAppointments(accessToken: string, signal?: AbortSignal): Promise<TestAppointment[]> {
  return apiRequest<TestAppointment[]>('/TestAppointments', accessToken, { signal })
}

export function getAppointmentsPage(
  accessToken: string,
  pageNumber = 1,
  pageSize = 10,
  searchQuery?: string,
  testTypeId?: string | number,
  statusFilter?: string,
  tab?: string,
  signal?: AbortSignal,
): Promise<TestAppointmentPageResponse> {
  const params = new URLSearchParams({
    pageNumber: String(pageNumber),
    pageSize: String(pageSize),
  })

  if (searchQuery && searchQuery.trim()) {
    params.set('searchQuery', searchQuery.trim())
  }
  if (testTypeId !== undefined && testTypeId !== 'all' && Number(testTypeId) > 0) {
    params.set('testTypeId', String(testTypeId))
  }
  if (statusFilter && statusFilter !== 'all') {
    params.set('statusFilter', statusFilter)
  }
  if (tab && tab !== 'all') {
    params.set('tab', tab)
  }

  return apiRequest<TestAppointmentPageResponse>(
    `/TestAppointments?${params.toString()}`,
    accessToken,
    { signal },
  )
}

export function getAppointmentById(
  accessToken: string,
  appointmentId: number,
): Promise<TestAppointment> {
  return apiRequest<TestAppointment>(`/TestAppointments/${appointmentId}`, accessToken)
}

export function getAppointmentsByLocalAppId(
  accessToken: string,
  localAppId: number,
): Promise<TestAppointment[]> {
  return apiRequest<TestAppointment[]>(
    `/TestAppointments/local-application/${localAppId}`,
    accessToken,
  )
}

export function getSchedulePreparation(
  accessToken: string,
  localAppId: number,
  testTypeId: number,
): Promise<ScheduleTestPreparation> {
  return apiRequest<ScheduleTestPreparation>(
    `/TestAppointments/schedule-preparation/${localAppId}/${testTypeId}`,
    accessToken,
  )
}

export function scheduleTestAppointment(
  accessToken: string,
  payload: ScheduleTestPayload,
): Promise<void> {
  return apiRequest<void>('/TestAppointments/schedule', accessToken, {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

export function canScheduleTestWorkflow(
  accessToken: string,
  localAppId: number,
  testType: number,
): Promise<{ allowed: boolean; error: string | null }> {
  return apiRequest<{ allowed: boolean; error: string | null }>(
    `/TestWorkflow/can-schedule?localAppId=${localAppId}&testType=${testType}`,
    accessToken,
  )
}

export function getAppointmentAvailability(
  accessToken: string,
  localAppId: number,
  testTypeId: number,
  startDate: string,
  endDate: string,
): Promise<AppointmentAvailabilityResponse> {
  const query = new URLSearchParams({
    localAppId: String(localAppId),
    testTypeId: String(testTypeId),
    startDate,
    endDate,
  })
  return apiRequest<AppointmentAvailabilityResponse>(
    `/TestAppointments/availability?${query.toString()}`,
    accessToken,
  )
}

export function getTestByAppointmentId(
  accessToken: string,
  appointmentId: number,
): Promise<TestResponse[]> {
  return apiRequest<TestResponse[]>(
    `/Tests/appointment/${appointmentId}`,
    accessToken,
  )
}

export function cancelTestAppointment(
  accessToken: string,
  appointmentId: number,
): Promise<void> {
  return apiRequest<void>(
    `/TestAppointments/${appointmentId}/cancel`,
    accessToken,
    {
      method: 'POST',
    },
  )
}

export function saveTestResult(
  accessToken: string,
  payload: SaveTestResultPayload,
): Promise<number> {
  return apiRequest<number>('/Tests', accessToken, {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

export function getAttemptStats(
  accessToken: string,
  localAppId: number,
  testTypeId: number,
): Promise<ExaminationAttemptStats> {
  return apiRequest<ExaminationAttemptStats>(
    `/TestAppointments/attempts/${localAppId}/${testTypeId}`,
    accessToken,
  )
}


