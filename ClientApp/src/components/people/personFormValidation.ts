import type { Country } from '../../types/Country'
import { validatePersonName } from '../../utils/nameValidation'

export interface PersonFormValidationValues {
  nationalNo: string
  firstName: string
  secondName: string
  thirdName: string | null
  lastName: string
  dateOfBirth: string
  gender: number
  nationalityCountryID: number
  phone: string
  email: string | null
  address: string
}

export interface PersonFormRawInputs {
  nationalNo: string
  firstName: string
  secondName: string
  thirdName: string
  lastName: string
  dateOfBirth: string
  gender: string
  nationalityCountryID: string
  phone: string
  email: string
  address: string
}

export type PersonFormValidationResult =
  | { ok: true; values: PersonFormValidationValues }
  | { ok: false; field: string; message: string }

/**
 * Calculates the maximum selectable Date of Birth, which is exactly 18 years
 * before the local current date. This also serves as the default form value.
 * Safely handles leap years (e.g. Feb 29 rolls back to Feb 28 on non-leap years)
 * and avoids UTC timezone conversion shifts.
 */
export function calculateEighteenYearsAgo(currentDate: Date = new Date()): string {
  const targetYear = currentDate.getFullYear() - 18
  const targetMonth = currentDate.getMonth()
  const targetDay = currentDate.getDate()

  const cutoffDate = new Date(targetYear, targetMonth, targetDay)
  if (cutoffDate.getMonth() !== targetMonth) {
    cutoffDate.setDate(0)
  }

  const y = cutoffDate.getFullYear()
  const m = String(cutoffDate.getMonth() + 1).padStart(2, '0')
  const d = String(cutoffDate.getDate()).padStart(2, '0')

  return `${y}-${m}-${d}`
}

export function calculateMinDateOfBirth(currentDate: Date = new Date()): string {
  const minYear = currentDate.getFullYear() - 120
  const m = String(currentDate.getMonth() + 1).padStart(2, '0')
  const d = String(currentDate.getDate()).padStart(2, '0')
  return `${minYear}-${m}-${d}`
}

export function resolveDefaultCountryId(
  countries: Country[],
  initialNationalityCountryId?: number | string,
): string {
  if (initialNationalityCountryId) {
    return String(initialNationalityCountryId)
  }
  if (countries.length > 0) {
    const jordan = countries.find((c) => c.countryName.toLowerCase() === 'jordan')
    return String(jordan ? jordan.countryId : countries[0].countryId)
  }
  return ''
}

export function validatePersonPhotoFile(file: Pick<File, 'type' | 'size'>): string | null {
  if (!file.type.startsWith('image/')) {
    return 'Please select a valid image file (JPEG, PNG, WebP).'
  }

  if (file.size > 5 * 1024 * 1024) {
    return 'Image size must not exceed 5 MB.'
  }

  return null
}

export function validatePersonForm(
  inputs: PersonFormRawInputs,
  maxDateOfBirth: string,
  referenceDate: Date = new Date(),
): PersonFormValidationResult {
  const trimmedNationalNo = inputs.nationalNo.trim()
  const trimmedFirstName = inputs.firstName.trim()
  const trimmedSecondName = inputs.secondName.trim()
  const trimmedThirdName = inputs.thirdName.trim()
  const trimmedLastName = inputs.lastName.trim()
  const trimmedPhone = inputs.phone.trim()
  const trimmedAddress = inputs.address.trim()
  const trimmedEmail = inputs.email.trim()

  // 1. National Number validation: exactly 10 numeric digits
  if (!trimmedNationalNo) {
    return { ok: false, field: 'nationalNo', message: 'National Number is required.' }
  }
  if (trimmedNationalNo.length !== 10 || !/^\d{10}$/.test(trimmedNationalNo)) {
    return {
      ok: false,
      field: 'nationalNo',
      message: 'National Number must contain exactly 10 numeric digits.',
    }
  }

  // 2. Names validation (First, Second, Last Name required; Third Name optional)
  const firstNameValidation = validatePersonName(inputs.firstName, 'First Name', true)
  if (!firstNameValidation.valid) {
    return { ok: false, field: 'firstName', message: firstNameValidation.error! }
  }

  const secondNameValidation = validatePersonName(inputs.secondName, 'Second Name', true)
  if (!secondNameValidation.valid) {
    return { ok: false, field: 'secondName', message: secondNameValidation.error! }
  }

  const thirdNameValidation = validatePersonName(inputs.thirdName, 'Third Name', false)
  if (!thirdNameValidation.valid) {
    return { ok: false, field: 'thirdName', message: thirdNameValidation.error! }
  }

  const lastNameValidation = validatePersonName(inputs.lastName, 'Last Name', true)
  if (!lastNameValidation.valid) {
    return { ok: false, field: 'lastName', message: lastNameValidation.error! }
  }

  // 3. Date of Birth validation: minimum 18 years, not in future
  if (!inputs.dateOfBirth) {
    return { ok: false, field: 'dateOfBirth', message: 'Please select a valid Date of Birth.' }
  }

  const birthDate = new Date(`${inputs.dateOfBirth}T00:00:00`)
  const today = new Date(referenceDate.getTime())
  today.setHours(0, 0, 0, 0)

  if (birthDate > today) {
    return { ok: false, field: 'dateOfBirth', message: 'Date of Birth cannot be in the future.' }
  }

  let age = today.getFullYear() - birthDate.getFullYear()
  const monthDifference = today.getMonth() - birthDate.getMonth()
  if (
    monthDifference < 0 ||
    (monthDifference === 0 && today.getDate() < birthDate.getDate())
  ) {
    age--
  }

  if (!Number.isFinite(birthDate.getTime()) || inputs.dateOfBirth > maxDateOfBirth || age < 18) {
    return {
      ok: false,
      field: 'dateOfBirth',
      message: 'The citizen must be at least 18 years old to be registered.',
    }
  }

  // 4. Gender validation: 0 (Male) or 1 (Female)
  if (inputs.gender !== '0' && inputs.gender !== '1') {
    return { ok: false, field: 'gender', message: 'Please select a valid gender.' }
  }

  // 5. Nationality validation
  const countryIdNum = Number(inputs.nationalityCountryID)
  if (!inputs.nationalityCountryID || !Number.isInteger(countryIdNum) || countryIdNum <= 0) {
    return {
      ok: false,
      field: 'nationalityCountryID',
      message: 'Please select a valid nationality country.',
    }
  }

  // 6. Phone validation: Jordanian mobile (077/078/079 + 7 digits)
  if (!trimmedPhone) {
    return { ok: false, field: 'phone', message: 'Phone number is required.' }
  }
  if (!/^(077|078|079)\d{7}$/.test(trimmedPhone)) {
    return {
      ok: false,
      field: 'phone',
      message: 'Phone number must be a 10-digit Jordanian mobile number starting with 077, 078, or 079.',
    }
  }

  // 7. Email validation (Optional, but if provided must match standard format)
  if (trimmedEmail) {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
    if (!emailRegex.test(trimmedEmail)) {
      return {
        ok: false,
        field: 'email',
        message: 'Please enter a valid email address (e.g. citizen@example.com).',
      }
    }
  }

  // 8. Residential Address validation
  if (!trimmedAddress) {
    return { ok: false, field: 'address', message: 'Residential Address is required.' }
  }

  return {
    ok: true,
    values: {
      nationalNo: trimmedNationalNo,
      firstName: trimmedFirstName,
      secondName: trimmedSecondName,
      thirdName: trimmedThirdName || null,
      lastName: trimmedLastName,
      dateOfBirth: inputs.dateOfBirth,
      gender: Number(inputs.gender || 0),
      nationalityCountryID: countryIdNum,
      phone: trimmedPhone,
      email: trimmedEmail || null,
      address: trimmedAddress,
    },
  }
}
