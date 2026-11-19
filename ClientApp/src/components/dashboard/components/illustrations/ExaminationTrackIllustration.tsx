/**
 * Purposeful SVG Illustration: Examination Track & Qualification Checkpoints
 */
export default function ExaminationTrackIllustration() {
  return (
    <svg
      viewBox="0 0 120 68"
      className="h-14 w-24 shrink-0 select-none"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      aria-hidden="true"
    >
      <path
        d="M12 50C28 50 32 22 58 22C84 22 88 46 108 46"
        stroke="#1e3a6d"
        strokeWidth="8"
        strokeLinecap="round"
      />
      <path
        d="M12 50C28 50 32 22 58 22C84 22 88 46 108 46"
        stroke="#38bdf8"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeDasharray="4 4"
      />
      <circle cx="16" cy="50" r="6" fill="#0b1528" stroke="#38bdf8" strokeWidth="2" />
      <circle cx="58" cy="22" r="6" fill="#0b1528" stroke="#818cf8" strokeWidth="2" />
      <circle cx="104" cy="46" r="6" fill="#0b1528" stroke="#10b981" strokeWidth="2" />
      <circle cx="104" cy="46" r="2.5" fill="#10b981" />
    </svg>
  );
}
