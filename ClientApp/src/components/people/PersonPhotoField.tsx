import { useRef, type ChangeEvent } from 'react'
import { Upload, Trash2 } from 'lucide-react'
import PersonPhoto from '../common/PersonPhoto'
import { Button } from '../ui'

export interface PersonPhotoFieldProps {
  personId?: number | null
  hasStoredPhoto: boolean
  storedPhotoKey: string | null
  localPhotoPreview: string | null
  isPhotoRemoved: boolean
  fullName: string
  accessToken?: string
  saving: boolean
  onPhotoChange: (event: ChangeEvent<HTMLInputElement>) => void
  onRemovePhoto: () => void
}

export default function PersonPhotoField({
  personId,
  hasStoredPhoto,
  storedPhotoKey,
  localPhotoPreview,
  isPhotoRemoved,
  fullName,
  accessToken,
  saving,
  onPhotoChange,
  onRemovePhoto,
}: PersonPhotoFieldProps) {
  const fileInputRef = useRef<HTMLInputElement>(null)
  const hasPhotoToShow = Boolean(localPhotoPreview || (hasStoredPhoto && !isPhotoRemoved))

  function handleRemoveClick() {
    if (fileInputRef.current) {
      fileInputRef.current.value = ''
    }
    onRemovePhoto()
  }

  return (
    <div className="flex flex-col sm:flex-row items-start sm:items-center gap-5 p-4 rounded-xl bg-[#091326] border border-[#1b2f52] mb-6">
      <div className="shrink-0">
        <PersonPhoto
          personId={personId}
          hasPhoto={hasStoredPhoto && !isPhotoRemoved}
          photoKey={storedPhotoKey}
          localPreviewUrl={localPhotoPreview}
          fullName={fullName}
          size="xl"
          accessToken={accessToken}
        />
      </div>

      <div className="flex-1 space-y-2">
        <div className="flex items-center gap-2">
          <span className="text-xs font-bold uppercase tracking-wider text-slate-300">Personal Photo</span>
          <span className="text-[11px] text-slate-500 font-normal">(Optional)</span>
        </div>
        <p className="text-xs text-slate-400 leading-relaxed">
          Upload an official passport-style portrait. Format: JPEG, PNG, or WebP. Maximum size: 5 MB.
        </p>

        <div className="flex flex-wrap items-center gap-2 pt-1">
          <input
            ref={fileInputRef}
            id="person-photo-input"
            type="file"
            accept="image/jpeg,image/png,image/webp"
            onChange={onPhotoChange}
            disabled={saving}
            className="hidden"
          />
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={saving}
            onClick={() => {
              fileInputRef.current?.click()
            }}
            className="gap-2 text-xs font-semibold text-white bg-[#0e1a33] hover:bg-[#16274a] border border-[#263e68] hover:border-cyan-400/50 rounded-lg cursor-pointer"
          >
            <Upload className="size-3.5 text-cyan-400" />
            <span>{hasPhotoToShow ? 'Change Photo' : 'Upload Photo'}</span>
          </Button>

          {hasPhotoToShow && (
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={handleRemoveClick}
              disabled={saving}
              className="gap-1.5 text-xs text-rose-300 hover:text-white hover:bg-rose-950/40 cursor-pointer"
            >
              <Trash2 className="size-3.5 text-rose-400" />
              <span>Remove</span>
            </Button>
          )}
        </div>
      </div>
    </div>
  )
}
