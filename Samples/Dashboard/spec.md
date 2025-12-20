# Medical Dashboard Spec

## Overview

Medical dashboard using React (vanilla JS, no JSX) served by ASP.NET Core. Connects to Clinical.Api and Scheduling.Api microservices.

**Future**: Offline-first sync client using IndexedDB for occasionally-connected mode.

## Architecture

```
Dashboard.Web/
├── wwwroot/
│   ├── index.html              # React app (vanilla JS)
│   ├── js/vendor/              # Bundled React 18
│   └── css/                    # Styles
├── App.cs                      # ASP.NET Core host
└── Program.cs                  # Entry point

Dashboard.Integration.Tests/    # Playwright E2E tests
```

## React Loading: Bundled Vendor Files (NOT CDN)

**Decision**: React loaded from `wwwroot/js/vendor/`, not CDN.

**Why**:
1. **Offline/occasionally-connected**: Medical facilities have restricted networks. Future sync client will use IndexedDB for offline operation - can't depend on CDN.
2. **Deterministic E2E testing**: Playwright tests work reliably without network.
3. **HIPAA**: Minimize external network calls.
4. **Version pinning**: Exact version in source control.

**Why NOT npm/bundler**: Sample project. No webpack complexity needed.

**Update React**:
```bash
curl -o wwwroot/js/vendor/react.development.js https://unpkg.com/react@18/umd/react.development.js
curl -o wwwroot/js/vendor/react-dom.development.js https://unpkg.com/react-dom@18/umd/react-dom.development.js
```

## Color Palette

| Token | Hex | Usage |
|-------|-----|-------|
| `--primary-500` | `#00BCD4` | Teal primary |
| `--secondary-500` | `#2E4450` | Deep slate |
| `--accent-500` | `#FF6B6B` | Actions/alerts |
| `--success` | `#4CAF50` | Success |
| `--error` | `#F44336` | Errors |

**NO PURPLE.**

## APIs

- Clinical.Api: `http://localhost:5080` - Patients, Encounters, Conditions
- Scheduling.Api: `http://localhost:5001` - Practitioners, Appointments

## Navigation: Hash-Based Routing with Browser History Integration

**Decision**: Full browser history integration via hash-based routing (`#view` or `#view/edit/id`).

**Implementation**:
1. **URL reflects state**: Navigating updates `window.location.hash` (e.g., `#patients`, `#patients/edit/123`)
2. **Browser back/forward work**: `history.pushState` on navigate, `popstate` listener restores state
3. **Deep linking works**: Opening `#patients/edit/123` directly loads that view
4. **Cancel buttons use `history.back()`**: In-app cancel mirrors browser back button behavior

**Why**:
1. **UX expectation**: Users expect browser back to work in web apps
2. **Deep linking**: Bookmarkable URLs to specific views
3. **Testable**: E2E tests can verify navigation via URL changes

**Route Format**:
- `#dashboard` - Dashboard view
- `#patients` - Patient list
- `#patients/edit/{id}` - Edit specific patient
- `#appointments` - Appointments list
- `#practitioners` - Practitioners list

**Cancel/Back Button Behavior**:
In-app "Cancel" buttons call `window.history.back()` so they behave identically to the browser back button. This ensures consistent navigation regardless of how the user chooses to go back.

## Sync Dashboard

Administrative dashboard for monitoring and managing sync operations across microservices.

**Permission Required**: `sync:admin` - Only users with this permission can access sync dashboard features.

**Features**:
1. **Sync Status Overview**: Real-time status of sync operations per microservice (Clinical.Api, Scheduling.Api)
2. **Sync Records Browser**: View, filter, and search sync records by:
   - Microservice (source system)
   - Sync record ID
   - Status (pending, synced, failed, conflict)
   - Date range
3. **Error Investigation**: Drill into failed sync records to see error details
4. **Manual Retry**: Trigger retry of failed sync operations

**Route**: `#sync` (requires `sync:admin` permission)

**API Endpoints** (to be implemented in each microservice):
- `GET /sync/status` - Current sync state
- `GET /sync/records?service={}&status={}&search={}` - Paginated sync records
- `POST /sync/records/{id}/retry` - Retry failed record

## Future: Offline Sync Client

The dashboard will implement a sync client for occasionally-connected operation:

1. **IndexedDB storage**: Local patient/appointment cache
2. **Change tracking**: Queue mutations when offline
3. **Sync protocol**: Reconcile with server when connected
4. **Conflict resolution**: Last-write-wins or manual merge

This is why bundled vendor files matter - the app must work without any network.
