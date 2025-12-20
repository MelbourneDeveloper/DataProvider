# Medical Dashboard Implementation Plan

## Overview

Build a modern medical dashboard using **H5 (C# to JavaScript transpiler)** with **React via JS interop**. The dashboard will connect to both Clinical.Api and Scheduling.Api microservices.

### Design Inspiration
Based on Behance healthcare dashboards (Wellmetrix, Mediso) and healthcare color research:
- **Primary**: Teal/Cyan (`#00BCD4`, `#26C6DA`)
- **Secondary**: Deep blue (`#10217D`, `#2E4450`)
- **Accent**: Coral/warm (`#FF6B6B`) for alerts/actions
- **Neutrals**: Slate grays + clean whites
- **Style**: Modern glassmorphic cards, clean typography, ample whitespace

## Project Structure

```
Samples/
├── Dashboard/
│   ├── Dashboard.Web/              # H5 SPA project
│   │   ├── Dashboard.Web.csproj    # H5 project targeting netstandard2.1
│   │   ├── h5.json                 # H5 compiler config
│   │   ├── wwwroot/
│   │   │   ├── index.html          # Entry HTML with React CDN
│   │   │   └── styles/
│   │   │       └── main.css        # Glassmorphic styles
│   │   ├── React/                  # React JS interop bindings
│   │   │   ├── React.cs            # Core React bindings
│   │   │   ├── ReactDOM.cs         # ReactDOM bindings
│   │   │   ├── Hooks.cs            # useState, useEffect, etc.
│   │   │   └── Elements.cs         # DOM element factories
│   │   ├── Components/             # React components in C#
│   │   │   ├── App.cs              # Root application component
│   │   │   ├── Layout/
│   │   │   │   ├── Sidebar.cs      # Navigation sidebar
│   │   │   │   ├── Header.cs       # Top header with search
│   │   │   │   └── MainContent.cs  # Content area wrapper
│   │   │   ├── Patients/
│   │   │   │   ├── PatientList.cs  # Patient search/list view
│   │   │   │   └── PatientDetail.cs # Full patient record
│   │   │   ├── Appointments/
│   │   │   │   └── AppointmentCalendar.cs
│   │   │   └── Practitioners/
│   │   │       └── PractitionerDirectory.cs
│   │   ├── Api/                    # API client layer
│   │   │   ├── HttpClient.cs       # Fetch API wrapper
│   │   │   ├── ClinicalApi.cs      # Clinical.Api client
│   │   │   └── SchedulingApi.cs    # Scheduling.Api client
│   │   ├── State/                  # Application state
│   │   │   └── AppState.cs         # Global state management
│   │   └── Program.cs              # Entry point
│   └── Dashboard.Web.Tests/        # Test project
```

## Implementation Steps

### Phase 1: Project Setup & React Bindings

1. **Create H5 project structure**
   - Create `Dashboard.Web.csproj` with H5 SDK
   - Configure `h5.json` for output settings
   - Create `wwwroot/index.html` with React 18 CDN links

2. **Build React JS interop layer** (inspired by Bridge.React + dart_node patterns)
   - `React.cs` - Core React API bindings using `[External]` attributes
   - `ReactDOM.cs` - createRoot, render bindings
   - `Hooks.cs` - useState, useEffect, useMemo, useCallback
   - `Elements.cs` - div, span, button, input, etc. factory methods

### Phase 2: Core Infrastructure

3. **HTTP client layer**
   - Wrap browser Fetch API for API calls
   - Create typed clients for Clinical.Api and Scheduling.Api
   - Handle CORS configuration (APIs need CORS headers)

4. **State management**
   - Simple observable state pattern
   - Props/State immutability using records

### Phase 3: Layout Components

5. **Create layout shell**
   - Sidebar with navigation (Patients, Appointments, Practitioners)
   - Header with search and user info
   - Main content area with routing

6. **Apply glassmorphic styling**
   - CSS with backdrop-filter, gradients
   - Teal/cyan primary palette
   - Card components with glass effect

### Phase 4: Feature Views

7. **Patient List/Search view**
   - Search input with debounce
   - Table/card list of patients
   - Pagination
   - Click to detail view

8. **Patient Detail view**
   - Demographics card
   - Encounters list
   - Conditions list
   - Medications list
   - Edit capabilities

9. **Appointments Calendar view**
   - Calendar grid or list view
   - Filter by practitioner/patient
   - Create/edit appointment modal
   - Status indicators

10. **Practitioner Directory view**
    - Grid of practitioner cards
    - Filter by specialty
    - Search functionality

### Phase 5: API Integration & CORS

11. **Configure CORS on APIs**
    - Add CORS middleware to Clinical.Api
    - Add CORS middleware to Scheduling.Api
    - Allow dashboard origin

12. **Connect all views to APIs**
    - Wire up data fetching
    - Handle loading/error states
    - Implement create/update operations

## Critical Files to Create/Modify

### New Files
| File | Purpose |
|------|---------|
| `Samples/Dashboard/Dashboard.Web/Dashboard.Web.csproj` | H5 project |
| `Samples/Dashboard/Dashboard.Web/h5.json` | H5 config |
| `Samples/Dashboard/Dashboard.Web/wwwroot/index.html` | HTML entry |
| `Samples/Dashboard/Dashboard.Web/wwwroot/styles/main.css` | Styles |
| `Samples/Dashboard/Dashboard.Web/React/*.cs` | React bindings |
| `Samples/Dashboard/Dashboard.Web/Components/**/*.cs` | UI components |
| `Samples/Dashboard/Dashboard.Web/Api/*.cs` | API clients |
| `Samples/Dashboard/Dashboard.Web/Program.cs` | Entry point |

### Modified Files
| File | Change |
|------|--------|
| `Samples/Clinical/Clinical.Api/Program.cs` | Add CORS |
| `Samples/Scheduling/Scheduling.Api/Program.cs` | Add CORS |
| `DataProvider.sln` | Add Dashboard project |

## Technical Approach: React Bindings

The React bindings will use H5's `[External]` and JS interop capabilities:

```csharp
// Example pattern for React bindings
[External]
[Name("React")]
public static class React
{
    [Template("React.createElement({type}, {props}, ...{children})")]
    public static extern ReactElement CreateElement(
        Union<string, Func<object, ReactElement>> type,
        object props,
        params ReactElement[] children);
}

// Functional component pattern
public static ReactElement PatientCard(PatientCardProps props) =>
    Div(new { className = "patient-card" },
        H3(null, props.Patient.GivenName + " " + props.Patient.FamilyName),
        P(null, "DOB: " + props.Patient.BirthDate)
    );
```

## Color Palette (NO PURPLE)

| Token | Hex | Usage |
|-------|-----|-------|
| `--primary-500` | `#00BCD4` | Primary teal |
| `--primary-400` | `#26C6DA` | Primary light |
| `--primary-600` | `#00ACC1` | Primary dark |
| `--secondary-500` | `#2E4450` | Deep slate blue |
| `--accent-500` | `#FF6B6B` | Actions/alerts |
| `--success` | `#4CAF50` | Success states |
| `--warning` | `#FF9800` | Warnings |
| `--error` | `#F44336` | Errors |
| `--neutral-100` | `#F5F7FA` | Background |
| `--neutral-800` | `#1E293B` | Text |

## Dependencies

- **h5** - C# to JS transpiler
- **h5.Core** - DOM/ES5 type definitions
- **React 18** - Via CDN (not NuGet)
- **React DOM 18** - Via CDN

## Build & Run

```bash
# Build dashboard
cd Samples/Dashboard/Dashboard.Web
dotnet build

# Serve with dotnet-serve or similar
dotnet serve -p 3000 -d wwwroot

# APIs run on separate ports
cd Samples/Clinical/Clinical.Api && dotnet run  # Port 5000
cd Samples/Scheduling/Scheduling.Api && dotnet run  # Port 5001
```

## Success Criteria

- [ ] H5 project compiles C# to JavaScript
- [ ] React components render via JS interop
- [ ] Dashboard displays patient list from Clinical.Api
- [ ] Dashboard displays appointments from Scheduling.Api
- [ ] Dashboard displays practitioners from Scheduling.Api
- [ ] Patient detail view shows encounters, conditions, medications
- [ ] Create/edit operations work
- [ ] Glassmorphic UI matches healthcare design standards
- [ ] No purple in color scheme

## KNOWN LIMITATION: H5 Transpiler Uses C# 7.2

**CRITICAL FUTURE WORK**: The H5 transpiler internally uses Roslyn with C# 7.2, regardless of the `LangVersion` setting in the csproj. This means:

- No records (use classes instead)
- No file-scoped namespaces
- No global usings
- No range operators (`..`)
- No recursive patterns
- No nullable reference type syntax
- No target-typed new expressions

**TODO**: Fork and rewrite the H5 transpiler to use modern Roslyn (C# 12+). This would involve:
1. Forking https://github.com/aspect-build/aspect-dotnet/tree/master/src/aspect.h5 (or similar H5 fork)
2. Updating the embedded Roslyn compiler to latest
3. Adding transpilation support for modern C# features
4. Publishing as a new NuGet package

For now, all Dashboard.Web code must be written in C# 7.2 compatible syntax.
