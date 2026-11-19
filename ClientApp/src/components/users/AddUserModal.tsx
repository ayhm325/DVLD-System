import { useMemo, useState, type FormEvent } from 'react'
import {
  ShieldCheck,
  UserPlus,
  User as UserIcon,
  Eye,
  EyeOff,
  CheckCircle2,
  Circle,
  Loader2,
} from 'lucide-react'
import ModalShell from '../common/ModalShell'
import ErrorAlert from '../common/ErrorAlert'
import { Button } from '../ui/button'
import { Input } from '../ui/input'
import { Label } from '../ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../ui/select'
import { createUser } from '../../services/userService'
import type { Person } from '../../types/Person'
import type { User } from '../../types/User'
import { validatePassword, validateUsername } from './userValidation'
import { useEligiblePeoplePicker } from './useEligiblePeoplePicker'
import EligiblePersonPicker from './EligiblePersonPicker'

export interface AddUserModalProps {
  isOpen: boolean
  accessToken: string
  existingUsers: User[]
  onClose: () => void
  onCreated: (newUserId: number, userName: string, role: number) => void
  onNavigateToAddPerson?: () => void
}

interface FormErrors {
  personId?: string
  userName?: string
  password?: string
  confirmPassword?: string
}

export default function AddUserModal({
  isOpen,
  accessToken,
  existingUsers,
  onClose,
  onCreated,
  onNavigateToAddPerson,
}: AddUserModalProps) {
  const {
    personQuery,
    setPersonQuery,
    debouncedQuery,
    candidates,
    loadingCandidates,
    candidateError,
    refreshCandidates,
    totalCount,
    totalPages,
    pagination,
    resetPicker,
  } = useEligiblePeoplePicker({ isOpen, accessToken })

  const [selectedPerson, setSelectedPerson] = useState<Person | null>(null)

  const [userName, setUserName] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [role, setRole] = useState<number>(2) // 2 = Admin, 1 = Staff
  const [isActive, setIsActive] = useState(true)

  const [showPassword, setShowPassword] = useState(false)
  const [showConfirmPassword, setShowConfirmPassword] = useState(false)

  const [fieldErrors, setFieldErrors] = useState<FormErrors>({})
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const linkedPersonIds = useMemo(
    () => new Set(existingUsers.map((u) => u.personId)),
    [existingUsers],
  )

  const resetForm = () => {
    resetPicker()
    setSelectedPerson(null)
    setUserName('')
    setPassword('')
    setConfirmPassword('')
    setRole(2)
    setIsActive(true)
    setShowPassword(false)
    setShowConfirmPassword(false)
    setFieldErrors({})
    setSubmitError(null)
  }

  const passwordMinMet = password.length >= 8
  const passwordMaxMet = password.length > 0 && password.length <= 100
  const passwordsMatch =
    password.length > 0 && confirmPassword.length > 0 && password === confirmPassword

  function validateForm(): boolean {
    const nextErrors: FormErrors = {}

    if (!selectedPerson || selectedPerson.personId <= 0) {
      nextErrors.personId = 'Please select an eligible person to link this user account.'
    } else if (linkedPersonIds.has(selectedPerson.personId)) {
      nextErrors.personId = 'This person is already associated with a user account.'
    }

    const usernameErr = validateUsername(userName)
    if (usernameErr) {
      nextErrors.userName = usernameErr
    } else if (
      existingUsers.some(
        (u) => u.userName.toLowerCase() === userName.trim().toLowerCase(),
      )
    ) {
      nextErrors.userName = 'Username is already in use.'
    }

    const passwordErr = validatePassword(password)
    if (passwordErr) {
      nextErrors.password = passwordErr
    }

    if (!confirmPassword) {
      nextErrors.confirmPassword = 'Password confirmation is required.'
    } else if (password !== confirmPassword) {
      nextErrors.confirmPassword = 'Passwords do not match.'
    }

    setFieldErrors(nextErrors)
    return Object.keys(nextErrors).length === 0
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (submitting) return

    setSubmitError(null)
    if (!validateForm() || !selectedPerson) {
      return
    }

    setSubmitting(true)
    const trimmedUserName = userName.trim()

    try {
      const createdId = await createUser(accessToken, {
        personId: selectedPerson.personId,
        userName: trimmedUserName,
        password,
        isActive,
        role,
      })

      resetForm()
      onCreated(createdId, trimmedUserName, role)
    } catch (err) {
      setSubmitError(
        err instanceof Error
          ? err.message
          : 'Failed to create user account.',
      )
    } finally {
      setSubmitting(false)
    }
  }

  const handleClose = () => {
    if (submitting) return
    resetForm()
    onClose()
  }

  return (
    <ModalShell
      isOpen={isOpen}
      onClose={handleClose}
      preventClose={submitting}
      size="xl"
      title="Add New User Account"
      description="Create a new DVLD system account with an explicitly selected role (Admin or Staff) linked to a verified person record."
      contentClassName="pr-1"
      footer={
        <>
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={submitting}
            onClick={handleClose}
            className="bg-[#0e1b33] border-[#263750] text-slate-300 hover:bg-[#142647] hover:text-white cursor-pointer"
          >
            Cancel
          </Button>
          <Button
            type="submit"
            form="add-user-form"
            size="sm"
            disabled={submitting || loadingCandidates}
            className="gap-2 bg-gradient-to-r from-blue-600 to-cyan-600 hover:from-blue-500 hover:to-cyan-500 text-white font-semibold shadow-md shadow-blue-600/20 cursor-pointer"
          >
            {submitting ? (
              <>
                <Loader2 className="size-4 animate-spin" />
                <span>Creating User...</span>
              </>
            ) : (
              <>
                <UserPlus className="size-4" />
                <span>Create User Account</span>
              </>
            )}
          </Button>
        </>
      }
    >
      <form id="add-user-form" onSubmit={handleSubmit} className="space-y-5" noValidate>
        {submitError && <ErrorAlert message={submitError} />}
        {candidateError && (
          <ErrorAlert
            message={candidateError}
            onRetry={() => void refreshCandidates()}
            retryLabel="Retry"
          />
        )}

        {/* Step 1: Linked Person Selection */}
        <EligiblePersonPicker
          accessToken={accessToken}
          submitting={submitting}
          selectedPerson={selectedPerson}
          personQuery={personQuery}
          debouncedQuery={debouncedQuery}
          candidates={candidates}
          loadingCandidates={loadingCandidates}
          candidateError={candidateError}
          totalCount={totalCount}
          totalPages={totalPages}
          page={pagination.page}
          pageSize={pagination.pageSize}
          hasPreviousPage={pagination.hasPreviousPage}
          hasNextPage={pagination.hasNextPage}
          fieldError={fieldErrors.personId}
          onPersonQueryChange={setPersonQuery}
          onSelectPerson={(person) => {
            setSelectedPerson(person)
            setFieldErrors((prev) => ({ ...prev, personId: undefined }))
          }}
          onClearSelectedPerson={() => {
            setSelectedPerson(null)
            setFieldErrors((prev) => ({ ...prev, personId: undefined }))
          }}
          onPreviousPage={pagination.previousPage}
          onNextPage={pagination.nextPage}
          onPageSizeChange={pagination.setPageSize}
          onRefreshCandidates={() => void refreshCandidates()}
          onNavigateToAddPerson={onNavigateToAddPerson}
          onCloseModal={handleClose}
        />

        {/* Step 2: Account Credentials & Role */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 pt-1">
          <div className="space-y-1.5">
            <Label htmlFor="user-username" className="text-xs font-semibold text-slate-200">
              Username <span className="text-rose-400">*</span>
            </Label>
            <Input
              id="user-username"
              type="text"
              autoComplete="off"
              value={userName}
              disabled={submitting}
              onChange={(e) => {
                setUserName(e.target.value)
                if (fieldErrors.userName) {
                  setFieldErrors((prev) => ({ ...prev, userName: undefined }))
                }
              }}
              placeholder="e.g. omar_dvld"
              className="bg-[#0b1526] border-[#223656] text-white placeholder:text-slate-500"
            />
            {fieldErrors.userName ? (
              <p className="text-xs font-medium text-rose-400">{fieldErrors.userName}</p>
            ) : (
              <p className="text-[11px] text-slate-400">
                3–20 chars, letters/numbers/underscores only.
              </p>
            )}
          </div>

          {/* Explicit Role Selection (Admin or Staff) */}
          <div className="space-y-1.5">
            <Label htmlFor="user-role-select" className="text-xs font-semibold text-slate-200">
              Assigned Role <span className="text-rose-400">*</span>
            </Label>
            <Select
              value={String(role)}
              onValueChange={(val) => setRole(Number(val))}
              disabled={submitting}
            >
              <SelectTrigger
                id="user-role-select"
                className="h-10 w-full bg-[#0b1526] border-[#223656] text-xs text-white"
              >
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="2">
                  <span className="flex items-center gap-1.5 font-semibold text-violet-300">
                    <ShieldCheck className="size-3.5 text-violet-400" />
                    Admin
                  </span>
                </SelectItem>
                <SelectItem value="1">
                  <span className="flex items-center gap-1.5 font-semibold text-blue-300">
                    <UserIcon className="size-3.5 text-blue-400" />
                    Staff
                  </span>
                </SelectItem>
              </SelectContent>
            </Select>
            <p className="text-[11px] text-slate-400">
              Select Admin for full system management or Staff for operations.
            </p>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="user-status-select" className="text-xs font-semibold text-slate-200">
              Account Status
            </Label>
            <Select
              value={isActive ? 'active' : 'inactive'}
              onValueChange={(val) => setIsActive(val === 'active')}
              disabled={submitting}
            >
              <SelectTrigger
                id="user-status-select"
                className="h-10 w-full bg-[#0b1526] border-[#223656] text-xs text-white"
              >
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="active">Active (Can Sign In)</SelectItem>
                <SelectItem value="inactive">Inactive (Disabled)</SelectItem>
              </SelectContent>
            </Select>
            <p className="text-[11px] text-slate-400">
              New accounts are active by default.
            </p>
          </div>
        </div>

        {/* Step 3: Password & Confirmation */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div className="space-y-1.5">
            <Label htmlFor="user-password" className="text-xs font-semibold text-slate-200">
              Password <span className="text-rose-400">*</span>
            </Label>
            <div className="relative">
              <Input
                id="user-password"
                type={showPassword ? 'text' : 'password'}
                autoComplete="new-password"
                value={password}
                disabled={submitting}
                onChange={(e) => {
                  setPassword(e.target.value)
                  if (fieldErrors.password) {
                    setFieldErrors((prev) => ({ ...prev, password: undefined }))
                  }
                }}
                placeholder="Minimum 8 characters"
                className="pr-9 bg-[#0b1526] border-[#223656] text-white placeholder:text-slate-500"
              />
              <button
                type="button"
                onClick={() => setShowPassword((prev) => !prev)}
                aria-label={showPassword ? 'Hide password' : 'Show password'}
                className="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-white cursor-pointer bg-transparent border-0 p-0"
              >
                {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
              </button>
            </div>
            {fieldErrors.password && (
              <p className="text-xs font-medium text-rose-400">{fieldErrors.password}</p>
            )}
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="user-confirm-password" className="text-xs font-semibold text-slate-200">
              Confirm Password <span className="text-rose-400">*</span>
            </Label>
            <div className="relative">
              <Input
                id="user-confirm-password"
                type={showConfirmPassword ? 'text' : 'password'}
                autoComplete="new-password"
                value={confirmPassword}
                disabled={submitting}
                onChange={(e) => {
                  setConfirmPassword(e.target.value)
                  if (fieldErrors.confirmPassword) {
                    setFieldErrors((prev) => ({ ...prev, confirmPassword: undefined }))
                  }
                }}
                placeholder="Re-enter password"
                className="pr-9 bg-[#0b1526] border-[#223656] text-white placeholder:text-slate-500"
              />
              <button
                type="button"
                onClick={() => setShowConfirmPassword((prev) => !prev)}
                aria-label={showConfirmPassword ? 'Hide confirm password' : 'Show confirm password'}
                className="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-white cursor-pointer bg-transparent border-0 p-0"
              >
                {showConfirmPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
              </button>
            </div>
            {fieldErrors.confirmPassword && (
              <p className="text-xs font-medium text-rose-400">{fieldErrors.confirmPassword}</p>
            )}
          </div>
        </div>

        {/* Live Password Policy Checklist */}
        <div className="p-3 rounded-xl bg-[#0a1324] border border-[#1e304d] flex flex-wrap items-center gap-x-5 gap-y-1.5 text-xs">
          <span className={passwordMinMet ? 'text-emerald-400 flex items-center gap-1.5 font-medium' : 'text-slate-400 flex items-center gap-1.5'}>
            {passwordMinMet ? <CheckCircle2 className="size-3.5" /> : <Circle className="size-3.5" />}
            At least 8 characters
          </span>
          <span className={passwordMaxMet ? 'text-emerald-400 flex items-center gap-1.5 font-medium' : 'text-slate-400 flex items-center gap-1.5'}>
            {passwordMaxMet ? <CheckCircle2 className="size-3.5" /> : <Circle className="size-3.5" />}
            Max 100 characters
          </span>
          <span className={passwordsMatch ? 'text-emerald-400 flex items-center gap-1.5 font-medium' : 'text-slate-400 flex items-center gap-1.5'}>
            {passwordsMatch ? <CheckCircle2 className="size-3.5" /> : <Circle className="size-3.5" />}
            Passwords match
          </span>
        </div>
      </form>
    </ModalShell>
  )
}
