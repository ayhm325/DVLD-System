import { describe, expect, it } from 'vitest'
import {
  getPersonInitials,
  validatePersonSearchQuery,
} from '../../src/components/local-applications/localApplicationFormUtils'

describe('localApplicationFormUtils', () => {
  describe('getPersonInitials', () => {
    it('extracts up to two uppercase initials from a full name', () => {
      expect(getPersonInitials('Ahmad Obeidat')).toBe('AO')
      expect(getPersonInitials('  sara   ali   mahmoud  ')).toBe('SA')
      expect(getPersonInitials('Omar')).toBe('O')
      expect(getPersonInitials('   ')).toBe('')
    })
  })

  describe('validatePersonSearchQuery', () => {
    it('rejects empty or whitespace queries in any mode', () => {
      expect(validatePersonSearchQuery('id', '   ')).toEqual({
        valid: false,
        error: 'Enter a Person ID or national number first.',
      })
      expect(validatePersonSearchQuery('nationalNo', '')).toEqual({
        valid: false,
        error: 'Enter a Person ID or national number first.',
      })
    })

    it('validates positive numeric IDs in id mode', () => {
      expect(validatePersonSearchQuery('id', '0')).toEqual({
        valid: false,
        error: 'Person ID must be a positive number.',
      })
      expect(validatePersonSearchQuery('id', '-5')).toEqual({
        valid: false,
        error: 'Person ID must be a positive number.',
      })
      expect(validatePersonSearchQuery('id', 'abc')).toEqual({
        valid: false,
        error: 'Person ID must be a positive number.',
      })
      expect(validatePersonSearchQuery('id', ' 1042 ')).toEqual({
        valid: true,
        query: '1042',
      })
    })

    it('accepts trimmed national number queries in nationalNo mode', () => {
      expect(validatePersonSearchQuery('nationalNo', ' 9901023456 ')).toEqual({
        valid: true,
        query: '9901023456',
      })
    })
  })
})
