import { apiRequest } from './apiClient'
import type { Country } from '../types/Country'

export type { Country }

let cachedCountries: Country[] | null = null
let inflightCountries: Promise<Country[]> | null = null

export function getCountries(accessToken: string): Promise<Country[]> {
  if (cachedCountries) {
    return Promise.resolve(cachedCountries)
  }

  if (inflightCountries) {
    return inflightCountries
  }

  inflightCountries = apiRequest<Country[]>('/Countries', accessToken)
    .then((countries) => {
      cachedCountries = countries
      return countries
    })
    .finally(() => {
      inflightCountries = null
    })

  return inflightCountries
}
