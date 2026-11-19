export type PersonSearchMode = 'id' | 'nationalNo'

export type PersonSearchValidationResult =
  | { valid: true; query: string }
  | { valid: false; error: string }

export function getPersonInitials(fullName: string): string {
  return fullName
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase()
}

export function validatePersonSearchQuery(
  searchBy: PersonSearchMode,
  rawQuery: string,
): PersonSearchValidationResult {
  const query = rawQuery.trim()
  if (!query) {
    return { valid: false, error: 'Enter a Person ID or national number first.' }
  }
  if (searchBy === 'id' && (!/^\d+$/.test(query) || Number(query) <= 0)) {
    return { valid: false, error: 'Person ID must be a positive number.' }
  }
  return { valid: true, query }
}
