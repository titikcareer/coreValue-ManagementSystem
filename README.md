# BGuide - Value & Agent Declaration Portal

BGuide is an internal web application for PT. GS Battery that digitizes the periodic
**Value & Agent Declaration** process. Personnel record a safety declaration and a
value declaration, attach photo or video evidence, and the system compiles every
submission into a live dashboard that tracks the most active PIC (person in charge)
per month.

The application is a Blazor Web App targeting .NET 10, using the Interactive Server
render mode. All application data is held in memory for the lifetime of the running
process; there is no database or external file store in the current implementation.

---

## Contents

1. [Purpose and scope](#1-purpose-and-scope)
2. [System overview](#2-system-overview)
3. [Access control and roles](#3-access-control-and-roles)
4. [Prerequisites](#4-prerequisites)
5. [Configuration](#5-configuration)
6. [Build and run](#6-build-and-run)
7. [Using the application](#7-using-the-application)
8. [Seed accounts](#8-seed-accounts)
9. [Business rules and limits](#9-business-rules-and-limits)
10. [Project structure](#10-project-structure)
11. [Operational notes and limitations](#11-operational-notes-and-limitations)
12. [Troubleshooting](#12-troubleshooting)
13. [Extending the application](#13-extending-the-application)

---

## 1. Purpose and scope

The portal replaces the manual, paper-based declaration process with a single
controlled workflow. It provides:

- A declaration form for capturing a safety declaration, a value declaration, and
  supporting evidence per submission.
- A per-theme roster of Core Value agents that can be maintained by authorized staff.
- A dashboard summarizing monthly "Best Agent" results, per-PIC submission totals,
  and the full submission log.

The following are intentionally out of scope for the current build:

- Long-term data persistence (records are lost when the process restarts).
- Reporting export (PDF/Excel), audit trail, and e-mail notification.
- Multi-tenant or multi-company support.

## 2. System overview

| Aspect | Detail |
| --- | --- |
| Runtime | .NET 10 (validated with SDK 10.0.301) |
| Application model | Blazor Web App, Interactive Server render mode |
| Transport | SignalR circuit (WebSocket with fallback) |
| Authentication | ASP.NET Core cookie authentication |
| Data storage | In-memory singleton services (no database) |
| Front end | Razor components, Bootstrap 5, Bootstrap Icons |
| Time zone | Displayed in WIB (Western Indonesian Time) |

The domain state lives in three singleton services registered in `Program.cs`. Each
service raises a `Changed` event when its data is modified. Pages subscribe to those
events and re-render, so a submission or an agent edit made by one user is reflected
on every other connected session without a page refresh.

| Service | Responsibility |
| --- | --- |
| `DeclarationStore` | Stores submitted declarations and computes monthly leaderboard data. |
| `AgentStore` | Holds the Core Value agent roster for each theme and the currently active theme. |
| `UserAccountStore` | Holds user accounts, authenticates logins, and applies lockout rules. |
| `PasswordHasher` | Derives and verifies PBKDF2-SHA256 password hashes. |

Because the stores are singletons and the app runs as a single process, the design
assumes a single application instance. See section 11 for the implications.

## 3. Access control and roles

Authorization is role-based. The only role defined today is `DashboardAccess`.

| Route | Component | Required access |
| --- | --- | --- |
| `/` | Home | Authenticated |
| `/login` | LoginPage | Anonymous |
| `/logout` | Logout | Anonymous (signs the user out) |
| `/declaration` | ValorAgent | Authenticated |
| `/reset-password` | ResetPassword | Authenticated |
| `/dashboard` | Dashboard | `DashboardAccess` |
| `/agent` | Agent Core Value | `DashboardAccess` |
| `/not-found` | NotFound | Any |
| `/Error` | Error | Any |

Users without `DashboardAccess` can still sign in and submit declarations; the
Dashboard and Agent Core Value entries are hidden from their navigation and the
routes reject direct access.

Session behaviour:

- The authentication cookie is `HttpOnly`, `SameSite=Strict`, and valid for 8 hours
  with sliding expiration.
- The cookie is marked `Secure` only when the request that created it was HTTPS
  (`CookieSecurePolicy.SameAsRequest`). In production, HTTPS redirection and HSTS
  ensure this is always the case.

## 4. Prerequisites

- **.NET 10 SDK**. Verify with `dotnet --version`. The project targets `net10.0`.
- A modern browser (Chrome, Edge, Firefox, or Safari).
- An editor or IDE is optional. Visual Studio Code, Visual Studio, and JetBrains Rider
  all work with the solution.
- Outbound network access to `api.dicebear.com` if you want user avatars to render.
  The application degrades gracefully without it.

## 5. Configuration

### appsettings.json

Holds default logging levels and the allowed hosts. No secrets or connection strings
are required by the current build.

### Properties/launchSettings.json

Defines the local run profiles and URLs:

| Profile | URLs |
| --- | --- |
| `http` | `http://localhost:5118` |
| `https` | `https://localhost:7096` and `http://localhost:5118` |

`dotnet run` uses the first profile (`http`) unless a profile is specified.

### Program.cs

Key settings that live in code rather than configuration:

- Cookie expiration (8 hours) and sliding expiration.
- SignalR `MaximumReceiveMessageSize` set to 40 MB so large video attachments can
  traverse the circuit.
- Authentication, authorization policy, and middleware order.

## 6. Build and run

All commands below are run from the `BGuide/main` directory unless stated otherwise.

### Restore and build

```bash
dotnet restore
dotnet build
```

To build the whole solution from the `BGuide` directory:

```bash
dotnet build main.sln
```

### Run locally

```bash
dotnet run
```

The console prints the listening address (for example `http://localhost:5118`). Open
that address in a browser. The app launches the browser automatically because
`launchBrowser` is enabled in the profile.

To run the HTTPS profile explicitly:

```bash
dotnet run --launch-profile https
```

### Trust the development certificate (HTTPS profile)

If the browser warns about an untrusted certificate, trust the ASP.NET Core
development certificate once:

```bash
dotnet dev-certs https --trust
```

### Run from Visual Studio Code

Open the `BGuide` folder (which contains `.vscode/launch.json`), then press `F5`.
The launch configuration builds the project first and starts it with
`ASPNETCORE_ENVIRONMENT=Development`.

### Publish a release build

```bash
dotnet publish -c Release
```

## 7. Using the application

### 7.1 Sign in

Open `/login` and enter the NPK (the 6-digit NIM) and password. A successful login
issues the authentication cookie and redirects to the page the user originally
requested, or to Home when no return URL is present.

If the NPK or password is wrong, the form reports a generic error. After five
consecutive failures for the same NPK, the account is locked for 15 minutes.

### 7.2 Home

The Home screen shows a card for each module the signed-in user is permitted to
open. Users with `DashboardAccess` see the Dashboard and Agent Core Value cards;
everyone else sees only the declaration portal.

### 7.3 Submit a declaration (`/declaration`)

1. **PIC** - Start typing a name or NIM. A suggestion list appears; selecting an
   entry fills the PIC identity and the department automatically.
2. **Department** - Populated from the selected PIC and read-only.
3. **Safety Declaration** - Free text, required, maximum 2000 characters.
4. **Attachment** - Add a photo, a video, or both. At least one is required.
5. **Value Declaration** - Free text, required, maximum 2000 characters.

On submit, the declaration is added to the store, the dashboard updates for all
connected users, and the user is returned to Home.

Attachment rules:

| Type | Maximum size | Accepted formats |
| --- | --- | --- |
| Photo | 8 MB | JPEG, PNG, GIF, WebP |
| Video | 40 MB | MP4, WebM, AVI |

File formats are validated by inspecting the file signature (magic bytes), not the
file extension. Previews are rendered locally as base64 data URIs; only the file
names are stored with the declaration.

### 7.4 Dashboard (`/dashboard`)

The dashboard has three sections and updates live as new declarations arrive:

- **Best Agent** - The top submitter for each month, based on the number of
  submissions. When several PICs tie, all of them are listed and the card notes the
  tie.
- **Recap** - The total number of submissions per PIC, sorted from highest to
  lowest, with a proportional bar for quick comparison.
- **Data Value & Agent Declaration** - The full submission log showing the time
  (WIB), NPK, PIC name, department, both declarations, and attachment indicators.

### 7.5 Manage agents (`/agent`)

Available to users with `DashboardAccess`. The page shows the roster for the
currently selected Core Value theme.

- Use the theme selector to switch the active theme. Switching also opens the theme
  banner, which closes on a click anywhere.
- **Add agent** opens the form; **Edit** and **Delete** are available per row. Delete
  asks for confirmation.
- The NIM must be exactly 6 digits and unique within the active theme's roster.

The active theme determines which PICs appear in the declaration form's search.

### 7.6 Change password (`/reset-password`)

Enter the current password, the new password (minimum 8 characters), and a
confirmation. The new password must differ from the current one. The account's NIM is
taken from the signed-in identity, not from a form field.

### 7.7 Sign out (`/logout`)

Clears the authentication cookie and returns to the login page.

## 8. Seed accounts

The application ships with four seeded accounts. The initial password is derived
from the NIM by rotating its digit pairs: take digits 3-4, then digits 5-6, then
digits 1-2. For example, `011915` becomes `191501`.

| NPK | Name | Department | Role | Initial password |
| --- | --- | --- | --- | --- |
| 011915 | Fajri Kurniawan | GA, CSR, KM, SECURITY | DashboardAccess | 191501 |
| 011916 | Suarsono | GA, CSR, KM, SECURITY | DashboardAccess | 191601 |
| 011917 | M. Amirudin | GA, CSR, KM, SECURITY | (none) | 191701 |
| 011918 | Sri Haryanto | Digital Transformation | (none) | 191801 |

These are development seed accounts. Accounts are flagged with `MustChangePassword`
when created, and the flag is cleared once the password is changed. The current UI
does not force a password reset on first login, so operators should change the seed
passwords before any shared or production use.

## 9. Business rules and limits

- **Declaration retention** - At most 2000 declarations are kept in memory. When the
  limit is exceeded, the oldest entries are removed.
- **Login lockout** - 5 consecutive failures lock the account for 15 minutes. A
  successful login clears the failure counter.
- **Password storage** - Passwords are stored as PBKDF2-SHA256 hashes with a random
  16-byte salt, 100,000 iterations, and a 32-byte derived key, in `salt.key` base64
  format.
- **Attachment size** - Photo 8 MB, video 40 MB. The SignalR message size limit is
  set to 40 MB to accommodate the video case.
- **PIC uniqueness** - A NIM must be unique within a theme's roster.
- **Default theme** - Collaboration is active on startup. Only its roster is
  pre-populated.
- **Time display** - Stored timestamps are UTC and rendered in WIB for the dashboard.

## 10. Project structure

```
BGuide/
|-- main.sln
|-- .vscode/                     Editor launch and build tasks
`-- main/
    |-- main.csproj              Project file (net10.0)
    |-- Program.cs               Startup, services, middleware pipeline
    |-- appsettings.json         Logging and host configuration
    |-- Properties/
    |   `-- launchSettings.json  Local run profiles and URLs
    |-- Services/
    |   |-- AgentStore.cs        Per-theme agent rosters and active theme
    |   |-- DeclarationStore.cs  Submitted declarations and leaderboard logic
    |   |-- PasswordHasher.cs    PBKDF2 hash and verify helpers
    |   `-- UserAccountStore.cs  Accounts, authentication, and lockout
    |-- Components/
    |   |-- App.razor            HTML document shell
    |   |-- Routes.razor         Router and default layout
    |   |-- _Imports.razor       Shared using directives
    |   |-- Layout/
    |   |   |-- MainLayout.razor     Authenticated layout with navigation
    |   |   |-- AuthLayout.razor     Bare layout for login and logout
    |   |   |-- NavMenu.razor        Top navigation and user menu
    |   |   |-- ReconnectModal.razor Blazor reconnection dialog
    |   |   `-- ReconnectModal.razor.js
    |   `-- Pages/
    |       |-- Home.razor           Module launcher
    |       |-- LoginPage.razor      Login form and cookie issuance
    |       |-- Logout.razor         Sign-out endpoint
    |       |-- ResetPassword.razor  Password change form
    |       |-- ValorAgent.razor     Declaration submission form
    |       |-- Dashboard.razor      Reporting dashboard
    |       |-- Agent.razor          Agent roster management
    |       |-- Error.razor          Error page
    |       `-- NotFound.razor       Not-found page
    `-- wwwroot/
        |-- app.css              Global styles
        |-- favicon.png
        |-- lib/bootstrap/       Bootstrap assets
        `-- images/              Logos, watermark, and theme banners
```

## 11. Operational notes and limitations

These points are important when deploying or planning changes:

- **No persistence.** All data is in memory. Restarting the process clears
  declarations, agent edits, and password changes.
- **Single instance only.** The in-memory stores and SignalR circuits assume one
  process. Running multiple instances behind a load balancer would require sticky
  sessions and a shared data store.
- **Attachments are not stored server-side.** Only file names are recorded with a
  declaration. The uploaded bytes exist only in the browser during the session.
- **External avatar service.** The navigation bar loads avatars from the DiceBear
  public API. Without internet access the avatar image will not load; no other
  functionality is affected.
- **Theme banners.** Only the Collaboration banner (`coreValue-collaboration.png`) is
  bundled. Add `coreValue-{slug}.png` files to `wwwroot/images` before switching to
  the other themes, or the banner dialog shows a broken image.
- **Forced password reset is not enforced.** The `MustChangePassword` flag is stored
  and cleared on change, but no redirect currently enforces it.
- **Concurrency.** Store reads and writes are protected by a lock, and change
  notifications are marshalled back onto each component's context. This keeps the
  UI consistent, but it is not a substitute for durable, transactional storage.

## 12. Troubleshooting

**Login appears to succeed but the user is not signed in.**
The cookie requires a secure transport only when the original request was HTTPS. If
you change `CookieSecurePolicy` to `Always` while testing over plain HTTP, the
browser silently discards the cookie. Keep the setting as shipped, or test over
HTTPS.

**"NPK atau password salah".**
Verify the NPK and password. Remember the initial password is the rotated digit
pairs of the NIM, not the NIM itself.

**The account seems locked.**
Five failed attempts trigger a 15-minute lockout. Wait for it to expire or restart
the app to clear the in-memory state.

**A photo or video is rejected.**
Check the size (8 MB photo, 40 MB video) and the format. Validation uses the file
signature, so renaming a file to a supported extension will not help.

**The "Rejoining the server" dialog appears.**
This is the Blazor reconnection dialog. It appears when the SignalR connection drops
and closes automatically once the connection is restored. Use Retry or Resume if it
does not recover.

**`dotnet` is not recognized or the SDK is too old.**
Install the .NET 10 SDK and confirm with `dotnet --version`.

## 13. Extending the application

- **Add or change users.** Edit the account list in the `UserAccountStore`
  constructor. Each account is created through `CreateUser`, which hashes the initial
  password.
- **Add a theme banner.** Place a PNG at `wwwroot/images/coreValue-{slug}.png`, where
  `{slug}` matches the value returned by `CoreValueThemeExtensions.ImageSlug`.
- **Add a role.** Define the constant in `Roles`, register a matching policy in
  `Program.cs`, and gate the relevant pages with `[Authorize(Roles = ...)]`.
- **Replace in-memory storage.** Swap the three store services for a persistent
  implementation (for example EF Core) while keeping their public method signatures
  and `Changed` events, so the components continue to work without modification.
