import { describe, expect, it } from 'vitest'
import {
  calculateEighteenYearsAgo,
  calculateMinDateOfBirth,
  resolveDefaultCountryId,
  validatePersonForm,
  validatePersonPhotoFile,
  type PersonFormRawInputs,
} from '../../src/components/people/personFormValidation'
import type { Country } from '../../src/types/Country'

const VALID_INPUTS: PersonFormRawInputs = {
  nationalNo: '9901023456',
  firstName: 'Ahmad',
  secondName: 'Mohammed',
  thirdName: '',
  lastName: 'Obeidat',
  dateOfBirth: '2000-05-15',
  gender: '0',
  nationalityCountryID: '90',
  phone: '0791234567',
  email: 'ahmad@example.com',
  address: 'Amman, Jordan',
}

describe('personFormValidation', () => {
  describe('date of birth cutoffs', () => {
    it('rolls Feb 29 back to Feb 28 on non-leap target year 18 years prior', () => {
      const leapRef = new Date(2024, 1, 29) // 2024-02-29
      expect(calculateEighteenYearsAgo(leapRef)).toBe('2006-02-28')

      const nonLeapRef = new Date(2026, 1, 28) // 2026-02-28
      expect(calculateEighteenYearsAgo(nonLeapRef)).toBe('2008-02-28')
    })

    it('calculates minimum date of birth 120 years prior', () => {
      const ref = new Date(2026, 10, 19)
      expect(calculateMinDateOfBirth(ref)).toBe('1906-11-19')
    })
  })

  describe('resolveDefaultCountryId', () => {
    const countries: Country[] = [
      { countryId: 1, countryName: 'Egypt' },
      { countryId: 90, countryName: 'Jordan' },
    ]

    it('prefers initial country id, otherwise defaults to Jordan, then first country', () => {
      expect(resolveDefaultCountryId(countries, 1)).toBe('1')
      expect(resolveDefaultCountryId(countries)).toBe('90')
      expect(resolveDefaultCountryId([{ countryId: 5, countryName: 'Lebanon' }])).toBe('5')
      expect(resolveDefaultCountryId([])).toBe('')
    })
  })

  describe('validatePersonPhotoFile', () => {
    it('accepts valid image files up to 5 MB and rejects non-images or oversized files', () => {
      expect(validatePersonPhotoFile({ type: 'image/jpeg', size: 1024 })).toBeNull()
      expect(validatePersonPhotoFile({ type: 'image/png', size: 5 * 1024 * 1024 })).toBeNull()
      expect(validatePersonPhotoFile({ type: 'application/pdf', size: 1024 })).toBe(
        'Please select a valid image file (JPEG, PNG, WebP).',
      )
      expect(validatePersonPhotoFile({ type: 'image/webp', size: 5 * 1024 * 1024 + 1 })).toBe(
        'Image size must not exceed 5 MB.',
      )
    })
  })

  describe('validatePersonForm', () => {
    const refDate = new Date(2026, 4, 20) // 2026-05-20
    const maxDob = calculateEighteenYearsAgo(refDate) // 2008-05-20

    it('accepts valid trimmed citizen details and normalizes optional fields', () => {
      const result = validatePersonForm(
        {
          ...VALID_INPUTS,
          nationalNo: ' 9901023456 ',
          email: '  ',
          thirdName: ' ',
        },
        maxDob,
        refDate,
      )

      expect(result).toEqual({
        ok: true,
        values: {
          nationalNo: '9901023456',
          firstName: 'Ahmad',
          secondName: 'Mohammed',
          thirdName: null,
          lastName: 'Obeidat',
          dateOfBirth: '2000-05-15',
          gender: 0,
          nationalityCountryID: 90,
          phone: '0791234567',
          email: null,
          address: 'Amman, Jordan',
        },
      })
    })

    it('requires exactly 10 numeric digits for nationalNo', () => {
      expect(
        validatePersonForm({ ...VALID_INPUTS, nationalNo: '12345' }, maxDob, refDate),
      ).toEqual({
        ok: false,
        field: 'nationalNo',
        message: 'National Number must contain exactly 10 numeric digits.',
      })
    })

    it('enforces 18-year minimum age boundary and rejects future DOB', () => {
      expect(
        validatePersonForm({ ...VALID_INPUTS, dateOfBirth: '2008-05-20' }, maxDob, refDate).ok,
      ).toBe(true)

      expect(
        validatePersonForm({ ...VALID_INPUTS, dateOfBirth: '2008-05-21' }, maxDob, refDate),
      ).toEqual({
        ok: false,
        field: 'dateOfBirth',
        message: 'The citizen must be at least 18 years old to be registered.',
      })

      expect(
        validatePersonForm({ ...VALID_INPUTS, dateOfBirth: '2026-05-21' }, maxDob, refDate),
      ).toEqual({
        ok: false,
        field: 'dateOfBirth',
        message: 'Date of Birth cannot be in the future.',
      })
    })

    it('enforces Jordanian mobile numbers starting with 077, 078, or 079', () => {
      for (const prefix of ['077', '078', '079']) {
        expect(
          validatePersonForm({ ...VALID_INPUTS, phone: `${prefix}1234567` }, maxDob, refDate).ok,
        ).toBe(true)
      }

      expect(
        validatePersonForm({ ...VALID_INPUTS, phone: '0761234567' }, maxDob, refDate),
      ).toEqual({
        ok: false,
        field: 'phone',
        message:
          'Phone number must be a 10-digit Jordanian mobile number starting with 077, 078, or 079.',
      })
    })

    it('validates email format when provided', () => {
      expect(
        validatePersonForm({ ...VALID_INPUTS, email: 'invalid-email' }, maxDob, refDate),
      ).toEqual({
        ok: false,
        field: 'email',
        message: 'Please enter a valid email address (e.g. citizen@example.com).',
      })
    })
  })
})
