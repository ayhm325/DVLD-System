# Frontend Before / After Comparison (`ClientApp/`)

**Baseline Commit:** `45606d59c3b21d0ee9577d92f64c05a1c7375a63` (`development`)  
**Measurement Command:** `npm.cmd --prefix ClientApp run build` (`tsc -b && vite build`)

---

| Metric | Before | After | Difference |
|---|---|---|---|
| **Initial JS (`index-*.js`)** | `363.42 kB` (`111.30 kB` gzip) | `308.17 kB` (`97.41 kB` gzip) | **`-55.25 kB` (`-13.89 kB` gzip, `-15.2%` raw)** |
| **Initial CSS (`index-*.css`)** | `240.33 kB` (`37.17 kB` gzip) | `226.54 kB` (`34.83 kB` gzip) | **`-13.79 kB` (`-2.34 kB` gzip, `-5.7%` raw)** |
| **AppShell Lazy Chunk** | Bundled in `index` | `123.07 kB` (`37.19 kB` gzip) + `13.78 kB` CSS | Deferred until authenticated |
| **Largest Non-Index Feature Chunk** | `UsersPage` `50.60 kB` (`12.17 kB` gzip) | `UsersPage` `50.70 kB` (`12.19 kB` gzip) | `+0.10 kB` |
| **Build Duration** | `1.14s` | `1.17s` | `+0.03s` |
| **LCP / INP / CLS** | `NOT MEASURED` | `NOT MEASURED` | No live Lighthouse server in sandbox |
| **API Requests (Verified Flows)** | - Person save: full `GET /People` + paged `GET /People` + `/statistics`<br>- `TestAppointmentsPage` mount: 4 requests (duplicate `'all'`)<br>- `Countries`: uncached per mount<br>- `PersonDetailsPage` lightbox: duplicate photo fetch | - Person save: paged `GET /People` + `/statistics` only<br>- `TestAppointmentsPage` mount: 3 requests (no duplicate `'all'`)<br>- `Countries`: 1 request per session<br>- `PersonDetailsPage` lightbox: 1 cached photo fetch | Duplicate requests eliminated |
| **Animation CPU Behavior (Code-Verified)** | - `SpecularCard`: ~30+ idle 60Hz RAF loops + `getBoundingClientRect()`/frame<br>- `StatCard`: up to 15 idle 60Hz RAF loops + `getBoundingClientRect()`/frame<br>- `Sidebar`: React `setState` on every `pointermove` pixel<br>- `SpecularButton`: WebGL2 wake on every parent re-render | - `SpecularCard` & `StatCard`: 0 idle RAF loops; `ResizeObserver`/`scroll`-cached `DOMRect`; `visibilitychange` paused<br>- `Sidebar`: CSS var updates only (0 React re-renders on `pointermove`)<br>- `SpecularButton`: wakes only on visual prop changes; pauses on hidden tab & reduced motion | Idle animation loops & layout thrashing eliminated |
