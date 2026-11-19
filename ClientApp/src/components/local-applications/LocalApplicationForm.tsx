import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { ArrowLeft } from 'lucide-react'
import { cn } from '@/lib/utils'
import PageHero from '../common/PageHero'
import ErrorAlert from '../common/ErrorAlert'
import LoadingSpinner from '../common/LoadingSpinner'
import { Button, Card } from '../ui'
import {
  createLocalDrivingLicenseApplication,
  getLocalApplicationCreateInfo,
  updateLocalDrivingLicenseApplication,
} from '../../services/localDrivingLicenseApplicationService'
import { getPersonById, getPersonByNationalNo } from '../../services/peopleService'
import { getLicenseClasses } from '../../services/licenseClassService'
import type { LocalDrivingLicenseApplication } from '../../types/LocalDrivingLicenseApplication'
import type { PersonDetails } from '../../types/Person'
import type { LicenseClass } from '../../types/LicenseClass'
import ApplicantIdentificationStep from './ApplicantIdentificationStep'
import ApplicationDetailsStep from './ApplicationDetailsStep'
import {
  validatePersonSearchQuery,
  type PersonSearchMode,
} from './localApplicationFormUtils'

type Props = {
  accessToken: string
  mode: 'create' | 'edit'
  application?: LocalDrivingLicenseApplication
  onCancel: () => void
  onSaved: () => void
}

type Step = 'person' | 'application'

export default function LocalApplicationForm({
  accessToken,
  mode,
  application,
  onCancel,
  onSaved,
}: Props) {
  const isEdit = mode === 'edit'
  const [step, setStep] = useState<Step>(isEdit ? 'application' : 'person')
  const [searchBy, setSearchBy] = useState<PersonSearchMode>('id')
  const [searchValue, setSearchValue] = useState('')
  const [person, setPerson] = useState<PersonDetails | null>(null)
  const [classes, setClasses] = useState<LicenseClass[]>([])
  const [selectedClassId, setSelectedClassId] = useState(
    String(application?.licenseClassId ?? ''),
  )
  const [applicationFees, setApplicationFees] = useState(0)
  const [loading, setLoading] = useState(true)
  const [searching, setSearching] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  useEffect(() => {
    let cancelled = false
    async function initialize() {
      setLoading(true)
      setError('')
      try {
        const [licenseClasses, createInfo] = await Promise.all([
          getLicenseClasses(accessToken),
          getLocalApplicationCreateInfo(accessToken),
        ])
        if (cancelled) return
        setClasses(licenseClasses)
        setApplicationFees(createInfo.applicationFees)
        if (!selectedClassId && licenseClasses.length) {
          setSelectedClassId(String(licenseClasses[0].licenseClassId))
        }
        if (application) {
          const found = await getPersonById(
            accessToken,
            application.applicantPersonId,
          )
          if (!cancelled) setPerson(found)
        }
      } catch (err) {
        if (!cancelled) {
          setError(
            err instanceof Error
              ? err.message
              : 'Could not initialize the form.',
          )
        }
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    void initialize()
    return () => {
      cancelled = true
    }
    // selectedClassId intentionally initializes from the first API result only once.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [accessToken, application])

  const selectedClass = useMemo(
    () =>
      classes.find(
        (item) => item.licenseClassId === Number(selectedClassId),
      ),
    [classes, selectedClassId],
  )

  async function searchPerson(event?: FormEvent) {
    event?.preventDefault()
    if (searching) return
    const validation = validatePersonSearchQuery(searchBy, searchValue)
    if (!validation.valid) {
      setError(validation.error)
      return
    }
    setSearching(true)
    setError('')
    setNotice('')
    setPerson(null)
    try {
      const found =
        searchBy === 'id'
          ? await getPersonById(accessToken, Number(validation.query))
          : await getPersonByNationalNo(accessToken, validation.query)
      setPerson(found)
      setNotice('Applicant found. Review the details before continuing.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Person search failed.')
    } finally {
      setSearching(false)
    }
  }

  async function saveApplication(event: FormEvent) {
    event.preventDefault()
    if (saving) return
    setError('')
    setNotice('')
    if (!isEdit && !person) {
      setError('Search for and select a person first.')
      setStep('person')
      return
    }
    if (!selectedClassId) {
      setError('Select a license class.')
      return
    }
    setSaving(true)
    try {
      if (isEdit && application) {
        await updateLocalDrivingLicenseApplication(
          accessToken,
          application.localDrivingLicenseApplicationId,
          { licenseClassId: Number(selectedClassId) },
        )
      } else {
        const created = await createLocalDrivingLicenseApplication(
          accessToken,
          {
            applicantPersonId: person!.personId,
            licenseClassId: Number(selectedClassId),
          },
        )
        setNotice(
          `Application #${created.localDrivingLicenseApplicationId} was created successfully.`,
        )
      }
      onSaved()
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'Could not save the application.',
      )
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="page-content space-y-6">
      {/* Modernized Page Hero */}
      <PageHero
        pageId="Local Applications"
        title={isEdit ? 'Edit' : 'Create'}
        highlight="Application"
        description={
          isEdit
            ? 'Update the requested driving license class for this local application record.'
            : 'Complete applicant verification and configure driving license category details to submit a request.'
        }
      />

      <div className="flex items-center">
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={onCancel}
          className="h-9 gap-2 text-xs font-semibold text-white bg-[#0e1b33] hover:bg-[#142647] border border-[#263750] hover:border-blue-500/40 rounded-xl shadow-xs transition-all cursor-pointer"
        >
          <ArrowLeft className="size-4 text-cyan-400" />
          <span>Back to Applications</span>
        </Button>
      </div>

      {/* Two-Step Wizard Stepper (in Create Mode) */}
      {!isEdit && (
        <nav
          aria-label="Application progress steps"
          className="flex flex-wrap sm:flex-nowrap items-center gap-3 sm:gap-4 p-4 sm:p-5 rounded-2xl bg-[#0d182e]/90 border border-blue-500/30 shadow-[0_0_20px_rgba(0,140,255,0.06)]"
        >
          <div
            className={cn(
              'flex items-center gap-3 min-w-0 transition-opacity',
              step === 'person' ? 'opacity-100' : 'opacity-70',
            )}
          >
            <span
              className={cn(
                'size-9 rounded-xl flex items-center justify-center font-bold text-xs shrink-0 transition-colors',
                step === 'person' || person
                  ? 'bg-blue-600 text-white shadow-[0_0_12px_rgba(37,99,235,0.5)]'
                  : 'bg-[#13233f] text-slate-400 border border-[#263e68]',
              )}
            >
              01
            </span>
            <div className="min-w-0">
              <strong className="block text-xs font-bold text-white truncate">
                Applicant Identification
              </strong>
              <small className="block text-[11px] text-slate-400 truncate">
                Search and verify person
              </small>
            </div>
          </div>

          <div className="hidden sm:block h-px flex-1 bg-blue-500/20" />

          <div
            className={cn(
              'flex items-center gap-3 min-w-0 transition-opacity',
              step === 'application' ? 'opacity-100' : 'opacity-70',
            )}
          >
            <span
              className={cn(
                'size-9 rounded-xl flex items-center justify-center font-bold text-xs shrink-0 transition-colors',
                step === 'application'
                  ? 'bg-blue-600 text-white shadow-[0_0_12px_rgba(37,99,235,0.5)]'
                  : 'bg-[#13233f] text-slate-400 border border-[#263e68]',
              )}
            >
              02
            </span>
            <div className="min-w-0">
              <strong className="block text-xs font-bold text-white truncate">
                Application Details
              </strong>
              <small className="block text-[11px] text-slate-400 truncate">
                Select license class & fees
              </small>
            </div>
          </div>
        </nav>
      )}

      {/* Shared Alerts */}
      {error && (
        <ErrorAlert
          title="Application Notice"
          message={error}
          onDismiss={() => setError('')}
        />
      )}

      {notice && (
        <ErrorAlert
          variant="success"
          title="Status Update"
          message={notice}
          onDismiss={() => setNotice('')}
        />
      )}

      {/* Loading Placeholder */}
      {loading ? (
        <Card className="p-12 text-center">
          <LoadingSpinner
            message="Loading application configuration..."
            size="lg"
            layout="col"
          />
        </Card>
      ) : (
        <>
          {/* STEP 1: Applicant Identification */}
          {step === 'person' && !isEdit && (
            <ApplicantIdentificationStep
              searchBy={searchBy}
              searchValue={searchValue}
              searching={searching}
              person={person}
              onSearchByChange={setSearchBy}
              onSearchValueChange={setSearchValue}
              onSearchSubmit={searchPerson}
              onCancel={onCancel}
              onContinue={() => {
                setError('')
                setStep('application')
              }}
            />
          )}

          {/* STEP 2: Application Information */}
          {(step === 'application' || isEdit) && (
            <ApplicationDetailsStep
              isEdit={isEdit}
              application={application}
              person={person}
              classes={classes}
              selectedClassId={selectedClassId}
              selectedClass={selectedClass}
              applicationFees={applicationFees}
              saving={saving}
              onSelectedClassIdChange={setSelectedClassId}
              onChangePerson={() => setStep('person')}
              onBackOrCancel={() => (isEdit ? onCancel() : setStep('person'))}
              onSubmit={saveApplication}
            />
          )}
        </>
      )}
    </section>
  )
}
