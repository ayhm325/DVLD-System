# DVLD — React Frontend (`ClientApp`)

`ClientApp` is the browser-based React + TypeScript + Vite single-page application for the Driving & Vehicle Licensing Department (DVLD) system. It communicates with `DVLD.Api` over HTTP + JSON + JWT (`http://localhost:5260/api`) using `credentials: 'include'` for the `HttpOnly` refresh cookie.

For repository-wide architecture, backend standards, and security policies, follow the authoritative documents under `docs/development/` rather than duplicating them here.

---

## 1. Prerequisites

1. **Node.js & npm:** Node.js 20+ recommended (`22.x` LTS is used in Azure Pipelines).
2. **Running `DVLD.Api` Backend:**
   - The React app sends requests to `http://localhost:5260/api` (overrideable via `VITE_API_BASE_URL`, defaulting to `/api` via Vite proxy / configuration).
   - `DVLD.Api` configures the `"ReactApp"` CORS policy (`API/DVLD.Api/Program.cs`) allowing `http://localhost:5173` with credentials.
   - Start the backend API from the repository root (`D:\DVLD-System`):
     ```powershell
     dotnet run --project ".\API\DVLD.Api\DVLD.Api.csproj" --launch-profile http
     ```

---

## 2. Setup and Available Scripts

Run all frontend commands from the `ClientApp` directory (`ClientApp/package.json`):

```powershell
cd ClientApp

# Install dependencies
npm install

# Start the Vite development server (http://localhost:5173)
npm run dev

# Type-check (tsc -b) and build production assets into dist/
npm run build

# Run ESLint across TypeScript and React source files
npm run lint

# Preview the production build locally
npm run preview

# Run Vitest unit/component tests (ClientApp/tests/unit/)
npm run test

# Run Playwright E2E tests (ClientApp/tests/e2e/)
npm run test:e2e
```

> **Windows PowerShell Note:** If Windows PowerShell execution policy restricts `npm.ps1`, invoke `npm.cmd` (e.g., `npm.cmd --prefix ClientApp run build`).

---

## 3. Token & Browser Storage Model (Verified Summary)

- **Access Token (`src/utils/authTokenStore.ts`):** Held strictly in JavaScript memory (`currentAccessToken`). Never persisted to `localStorage` or `sessionStorage`.
- **Refresh Token (`AuthContext.tsx`, `apiClient.ts`):** Managed via an `HttpOnly`, `SameSite=Lax` cookie scoped to `/api/Auth`, exchanged via `POST /api/Auth/refresh` with `X-CSRF-Protection: 1`.
- **`localStorage` (`src/components/auth/Login.tsx`):** Stores **only** the optional remembered username (`dvld_remembered_username`) when the user checks "Remember me". Legacy token keys are purged on startup by `purgeLegacyTokenStorage()` (`src/utils/sessionStorage.ts`).

For full security specifications and client-side RBAC details, see [`SECURITY-STANDARDS.md`](../docs/development/SECURITY-STANDARDS.md).

---

## 4. Related Documentation

- **Frontend AI Rules:** [`ClientApp/AGENTS.md`](./AGENTS.md)
- **Repository AI Governance:** [`AGENTS.md`](../AGENTS.md)
- **Developer Setup & Workflow:** [`DEVELOPMENT-GUIDE.md`](../docs/development/DEVELOPMENT-GUIDE.md)
- **Engineering & Layering Standards:** [`ENGINEERING-STANDARDS.md`](../docs/development/ENGINEERING-STANDARDS.md)
- **Security & Session Standards:** [`SECURITY-STANDARDS.md`](../docs/development/SECURITY-STANDARDS.md)
- **Testing Standards:** [`TESTING-STANDARDS.md`](../docs/development/TESTING-STANDARDS.md)
- **Technology Stack & Versions:** [`TECHNOLOGY-STACK.md`](../docs/development/TECHNOLOGY-STACK.md)
- **Licensing Domain Rules:** [`LICENSING-DOMAIN-SPECIFICATION.md`](../docs/reference/LICENSING-DOMAIN-SPECIFICATION.md)
- **Task Priorities & Backlog:** [`ENGINEERING-ROADMAP.md`](../ENGINEERING-ROADMAP.md)
