# USTAZ backend audit — 2 October 2026

An engineering audit of the USTAZ backend (`E:\استاذ\Backedn`, .NET 10, clean architecture),
carried out against the real repository with the build and test suite run throughout.

Everything stated below was observed or measured. Where something could not be verified, that is
said explicitly rather than assumed.

---

## 1. Baseline

Established before any change was made.

| | Result |
|---|---|
| `dotnet build Ustaz.sln -c Release` | **Succeeded**, 0 warnings, 0 errors |
| `dotnet test Ustaz.sln -c Release` | **348 passed**, 0 failed, 0 skipped |
| | 265 in `Ustaz.Application.Tests`, 83 in `Ustaz.API.Tests` |
| Source files | 530 `.cs` files across 5 projects |
| Controllers | 30, exposing 288 OpenAPI paths |
| EF Core model drift | None — `dotnet ef migrations has-pending-model-changes` reports no changes |

The codebase was already in good shape: a clean Domain/Application/Infrastructure/Persistence/API
separation, MediatR with validation and logging behaviours, 444 uses of `AsNoTracking` across the
read paths, consistent `ApiResponse<T>` and RFC 7807 envelopes, and a bilingual notification and
email catalogue. This audit is therefore mostly about a small number of **sharp** problems rather
than broad disrepair.

---

## 2. Root causes discovered

### 2.1 HTTP 500.30 — confirmed and reproduced

**Verified root cause: `ConnectionStrings__DefaultConnection` is not set on the production host.**

Reproduced locally by running the Release build with `ASPNETCORE_ENVIRONMENT=Production`:

```
Unhandled exception. System.InvalidOperationException: Connection string 'DefaultConnection'
is not configured. Set ConnectionStrings__DefaultConnection as an environment variable, in
user-secrets, or in appsettings.Development.json.
   at Ustaz.Persistence.DependencyInjection.AddPersistenceLayer(IServiceCollection services,
      IConfiguration configuration) in src\Ustaz.Persistence\DependencyInjection.cs:line 27
   at Program.<Main>$(String[] args) in src\Ustaz.API\Program.cs:line 43
```

Three facts combine to make this present as an uninformative `500.30`:

1. `src/Ustaz.API/appsettings.Production.json` ships
   `ConnectionStrings:DefaultConnection` as `""`. An empty string **overrides** the value in
   `appsettings.json` — configuration layering does not treat blank as absent. On a production
   host the environment variable is therefore the *only* source.
2. `AddPersistenceLayer` throws for a blank connection string, correctly and deliberately. But
   it throws during service registration, so nothing has been built yet and the exception
   escapes `Program.cs` entirely.
3. `publish/web.config` has `stdoutLogEnabled="false"` — the publish default. The ASP.NET Core
   Module turns any unhandled startup exception into the generic
   *"HTTP Error 500.30 — ASP.NET Core app failed to start"* page, and with the stdout log off
   the exception text is written nowhere an operator will look. Serilog's file sink is never
   reached either, because the host was never built.

The symptom named IIS. The cause was one missing environment variable, and nothing in the
running system could say so.

### 2.2 A live database credential was committed to source control — **critical**

`src/Ustaz.API/appsettings.json` contained a complete, working SQL Server connection string for
the production database, including the password. `publish/appsettings.Production.json` contains
one too, and `publish/` was not in `.gitignore`.

The credential is valid: a read-only connection from this machine succeeded against SQL Server
17.00.4085 with 74 base tables.

This is the most serious finding in the audit. The credential has been removed from source, but
**removal is not remediation — the password must be rotated**, because it has already been
distributed wherever this repository has been.

### 2.3 The authentication rate limit was never applied — **high**

`AuthController` declares `[EnableRateLimiting("ustaz-auth")]` (10 requests/minute) to blunt
brute-force attacks on login, registration and password reset. It had no effect.

`Program.cs` also called `app.MapControllers().RequireRateLimiting("ustaz-global")` (300/minute).
That convention appends its `EnableRateLimitingAttribute` to each endpoint's metadata *after* the
controller's own attribute, and ASP.NET Core's rate-limiting middleware reads the **last**
matching metadata entry. The generous global policy therefore silently replaced the tight auth
policy on every credential endpoint.

Confirmed empirically before the fix: **16 consecutive failed logins all returned 401, none
returned 429.**

### 2.4 Uploads were validated only on data the client controls — **high**

`MediaValidator` and `LocalFileStorage` checked the file extension and the declared
`Content-Type` against allow lists. Both of those are strings chosen by the caller. An HTML
document named `avatar.png` and declared as `image/png` passed every check and was written to the
public media directory.

The file's own bytes were never examined.

### 2.5 Students and parents could never set a profile picture — **medium**

`ApplicationUser.ProfileImageUrl` is read in at least a dozen places — the student dashboard, the
parent dashboard, a teacher's class list, the guardian link list. Only `POST
/teacher/profile/image` could ever write it. For every student and parent on the platform the
field was permanently null, with no endpoint able to change that.

### 2.6 The platform admin dashboard did not count schools — **medium**

USTAZ is sold to schools, and `GetAdminDashboardQuery` reported users, teachers, students,
groups, courses, enrollments, reviews, inquiries and revenue — but no school counts at all, and
no count of platform administrators. The first number a platform operator wants ("how many
schools, how many suspended") was not available from any endpoint.

### 2.7 The last platform administrator could lock everyone out — **medium**

`SetUserActiveStateCommand` would suspend any account, including the calling administrator's own
and including the only remaining platform administrator. Either leaves the platform with nobody
able to grant the role back, recoverable only by editing the database directly. There was also no
endpoint to grant or revoke the platform role at all — it could only ever be created by the
seeder reading `Seed__AdminEmail`.

### 2.8 Error and validation messages were English-only — **medium**

Arabic is a first-class language in this product and the notification and email catalogues were
already fully bilingual with Arabic as the default. The **API error surface was not**: every
validation message, every business-rule message, and every `ProblemDetails` title and detail was
English. In addition the 401 and 403 produced by the authentication middleware had **no response
body at all** — an expired session, the most common failure a signed-in user meets, returned an
empty 401 with nothing for the client to display.

### 2.9 Production schema was two migrations behind

Read-only inspection of the production database found exactly one row in
`__EFMigrationsHistory`:

```
20260928171424_initalCreate
```

The repository contains three migrations. Two were unapplied. All ten schema objects they
introduce were confirmed **absent** from production, so nothing had been partially hand-applied.

### 2.10 Cloudinary was a live dependency

`CloudinaryDotNet` 1.29.3 was referenced and `CloudinaryMediaService` was selected whenever
Cloudinary credentials were present. The product requirement is that files live on USTAZ's own
infrastructure. The credentials were blank everywhere, so the local provider was in fact active —
but the dependency, the configuration surface and the code path all still existed.

---

## 3. Fixes implemented

| # | Fix | Files |
|---|---|---|
| 2.1 | New `StartupConfigurationCheck` validates every required setting **before** any service is registered, reports them **all at once** (not just the first failure) to stderr *and* the Windows event log, and names only configuration **keys** — never values. Non-fatal gaps (`Jwt__Key`, `Cors__AllowedOrigins__0`) become warnings logged once the host is up. | `src/Ustaz.API/Services/StartupConfigurationCheck.cs`, `Program.cs` |
| 2.2 | Credential removed from `appsettings.json`, replaced with a blank placeholder and a comment explaining the per-environment contract. `publish/` added to `.gitignore`. | `src/Ustaz.API/appsettings.json`, `.gitignore` |
| 2.3 | The generous baseline is now `options.GlobalLimiter`, which **composes** with named policies instead of racing them. `RequireRateLimiting` removed from `MapControllers`. `UseRateLimiter` moved after `UseAuthentication` so the partition key can actually see the authenticated user — it previously always fell back to the client IP because no principal existed yet. All three budgets are now configuration-driven via `RateLimitingOptions`. | `ApiServiceExtensions.cs`, `Program.cs`, `RateLimitingOptions.cs` |
| 2.4 | New `FileSignature` reads an upload's leading bytes and verifies them against its extension (JPEG, PNG, GIF, WEBP, BMP, PDF). Enforced in three places: the shared `FileUploadRules` (so it surfaces as a field error), `MediaValidator`, and `LocalFileStorage` itself — the last of which cannot be bypassed because it inspects the bytes it is about to write. `FileUploadRequest` gained a `Header`, captured at the HTTP boundary by `ToUploadRequestAsync`. | `FileSignature.cs`, `FileUploadRules.cs`, `MediaValidator.cs`, `LocalFileStorage.cs`, `FormFileExtensions.cs`, `FileUploadRequest.cs` |
| 2.5 | New role-agnostic profile-picture endpoints under `/api/v1/me/profile-picture` (GET, POST, DELETE) working for students, parents, teachers and administrators alike. The old file is deleted only *after* the new one is committed, so a failed upload never leaves a user with no avatar. Removal is idempotent. | `ProfilePictureFeature.cs`, `AccountController.cs` |
| 2.6 | New `GET /api/v1/platform/overview` reporting schools (total/active/suspended/archived/new), people by role, platform administrators, school staff and subscription health including an at-risk count. | `PlatformOverviewQueries.cs`, `PlatformAdminController.cs` |
| 2.7 | New `GET/POST/DELETE /api/v1/platform/administrators`. Revoking is refused when the subject is the caller, or the last active administrator; the same two guards were added to account **suspension**, which locks someone out just as completely. Revoking also closes the subject's outstanding sessions, because their issued access tokens still carry the role claim until they expire. Both actions are audited under new `platform.admin_granted` / `platform.admin_revoked` actions. | `PlatformAdministrators.cs`, `TeacherModeration.cs`, `AuditActions.cs` |
| 2.8 | A centralised localisation layer: `UstazLanguages`, `ErrorCodes`, `ErrorCatalog` (Arabic for ~115 codes), `ValidationMessages`, and `UseRequestLocalization` with Arabic as the default culture. The `ProblemDetails` envelope now always carries a stable `code` and a `language`, plus a localised `detail`. The authentication middleware's 401/403 now return real problem documents, including a distinct `TOKEN_EXPIRED`. Login gained `INVALID_CREDENTIALS`, distinct from the generic "not signed in". | See §4 |
| 2.10 | `CloudinaryDotNet` package, `CloudinaryMediaService` and `CloudinaryOptions` removed. Media options moved onto `StorageOptions`, which is where the private file storage already read from. Public assets now also get a cache header driven by `Storage:PublicCacheSeconds`. | `DependencyInjection.cs`, `Ustaz.Infrastructure.csproj`, `StorageOptions.cs`, `MediaValidator.cs` |

### 3.1 How the Arabic error surface is put together

The approach matters, because the obvious one does not scale. There are 381 exception throw sites
and 131 `WithMessage` calls in this codebase; translating each in place would have touched almost
every handler and guaranteed drift.

Instead:

- **The code is the contract and is never translated.** The domain already attached a stable code
  to each of its ~98 `BusinessRuleException` throws — that pre-existing discipline is what made a
  central catalogue possible without editing a single throw site. `NotFoundException`,
  `ConflictException`, `ForbiddenAccessException` and `UnauthorizedException` gained an optional
  code for the cases that needed to be distinguished.
- **Arabic lives in `ErrorCatalog`**, keyed by code. Where an English message interpolates a
  runtime value ("the score must be between 0 and 40"), the Arabic is phrased to carry the same
  meaning without the number, and those entries are marked in the file.
- **English stays at the throw site**, because those messages are more specific for exactly the
  reason above.
- **FluentValidation's built-in messages come for free.** It ships Arabic translations and
  resolves them from `CultureInfo.CurrentUICulture`, so `UseRequestLocalization` alone turns every
  rule that was *not* overridden with an explicit message into Arabic.
- **Shared rules resolve per failure.** `CommonValidationRules` and `FileUploadRules` now use the
  `Func` overload of `WithMessage`. This is load-bearing: the string overload is evaluated once at
  validator construction, which would freeze whichever language was ambient on the first request
  to resolve that validator.

Language is chosen from `X-Ustaz-Language`, then `?lang=`, then `Accept-Language`, then Arabic.
The user's stored `PreferredLanguage` is deliberately *not* read here — that would put a database
query in front of every request, including anonymous ones, to decide the wording of an error. The
client already has the preference from `GET /auth/me` and sends it.

---

## 4. Database

### Migrations

| Migration | Contents | Risk |
|---|---|---|
| `20260928171424_initalCreate` | The whole schema | Applied everywhere |
| `20260930054644_AddTeacherVerificationSubmission` | 7 added columns (all nullable or defaulted), 1 new table `ActivityEvents`, 6 indexes, 2 check constraints | **Purely additive.** No drop, no column retype, no table recreation |
| `20261001052232_AddEdits` | **Empty** — both `Up` and `Down` are no-ops | None. Kept rather than deleted, so any environment that already recorded it stays consistent |

**No new migration was created.** `dotnet ef migrations has-pending-model-changes` reports no
model drift, so creating one would have produced another empty migration. The schema work needed
was *applying* what already existed, not authoring more.

### Verification performed

- **Full chain on a disposable database.** Created `UstazMigrationVerify` on the local SQL Server
  2022 instance and ran `dotnet ef database update`. All three migrations applied cleanly from
  scratch.
- **Delta script generated for review** —
  [`docs/sql/20261002-initalCreate-to-AddEdits.sql`](sql/20261002-initalCreate-to-AddEdits.sql).
  64 lines, fully additive, wrapped in a transaction per migration. An idempotent full script is
  also provided at
  [`docs/sql/20261002-idempotent-full.sql`](sql/20261002-idempotent-full.sql).
- **Production compared read-only.** Every one of the ten objects the delta introduces was
  confirmed absent, so production sits exactly at `initalCreate` with no partial hand-application.
- **Blast radius measured.** `Homeworks` and `HomeworkSubmissions` — the two tables the delta
  alters — are **both empty** in production. `TeacherProfiles` has 3 rows. The delta cannot lose
  data because there is none in the affected columns' tables.

### Production data state (read-only, 2 Oct 2026)

74 base tables, 36 non-empty. The largest are `RefreshTokens` (21), `PlanFeatures` (18),
`EmailDeliveries` (13), `Subjects` (6). `Users` has 4 rows, `Tenants` 3, `StudentProfiles` 1.
This is a pre-launch or pilot database, not a populated production system — which materially
lowers the risk of the pending migration.

### Migration application status by environment

| Environment | Status |
|---|---|
| Local disposable (`UstazMigrationVerify`, SQL Server 2022) | **All 3 applied and verified** |
| Test host (`Ustaz.API.Tests`) | N/A — EF Core in-memory provider, migrations not used |
| **Production** | **Still at `initalCreate`. Two migrations NOT applied.** No write of any kind was made to production |

### Required manual DBA action

Applying the two pending migrations to production is a deliberate, authorised operation and was
**not** performed. The procedure is in
[`production-deployment.md`](production-deployment.md#applying-a-migration-to-production).

---

## 5. Performance

### Measured

Mean round-trip latency to the production database, measured from this development machine with
20 iterations of `SELECT 1` after warm-up:

```
229.3 ms per round trip
```

Round-trip **count** per dashboard, counted from the handlers:

| Handler | Sequential DB round trips |
|---|---|
| `GetStudentAnalytics` | 24 |
| `GetTeacherAnalyticsOverview` | 18 |
| `ParentDashboardFeature` | 15 |
| `GetStudentDashboard` | 12 |
| `GetTeacherDashboard` | 12 |
| `GetAdminDashboardQuery` | 12 |
| `GetTeacherAnalytics` | 8 |
| `GetPlatformOverviewQuery` (new) | 7 |

At 229 ms per round trip that is **2.7 seconds of pure network wait** for a 12-query dashboard and
**5.5 seconds** for the 24-query one, before any query does work.

**Important caveat, stated plainly:** 229 ms was measured from a developer machine to the hosting
provider, *not* from the production application server. If the app server is co-located with the
database the figure will be very much lower and this ceases to be the dominant cost. **That
measurement has to be taken from the host before acting on this.** The round-trip *counts* are
exact and provider-independent either way.

### A correction worth recording

An attempt was made to fold the platform overview's seven aggregates into a single SQL statement
by writing the other tables' counts as sub-selects inside one projection. **It does not work.**
EF Core funcletizes a scalar `context.OtherTable.Count()` and executes it as its own query:
measured at **7 commands for a 7-count projection** — no saving at all — and it returns a null
row when the anchor table is empty, which silently zeroed every counter on a platform with no
schools. The change was reverted to the straightforward grouped-aggregate form, which is correct
and costs the same.

Collapsing these genuinely into one round trip requires raw SQL, which the in-memory provider the
test suite runs on cannot execute. That trade is worth making **only if** the 229 ms figure holds
from the production host. See §8.

### What is already good

`AsNoTracking` is used on 444 read paths — a scan found no read handler missing it. List endpoints
project in the database rather than materialising entities. `PaginatedList<T>` and `PagedRequest`
are used consistently. The new platform user search projects to a DTO in SQL, which is also why it
cannot leak a password hash by accident: the column is never selected.

---

## 6. Security

### Fixed, with regression tests

| Issue | Status |
|---|---|
| Auth rate limit silently disabled (§2.3) | Fixed; `RateLimitingTests` proves a 429 arrives and that ordinary browsing is unaffected |
| Rate-limit partition always keyed by IP, never user | Fixed by moving `UseRateLimiter` after `UseAuthentication` |
| Upload content never verified (§2.4) | Fixed; `ProfilePictureTests` spoofs HTML-as-PNG, arbitrary bytes under five image extensions, and an honest PDF where an image is expected |
| Credential in source control (§2.2) | Removed from source — **but see "Outstanding risks"** |
| Last-admin lockout (§2.7) | Fixed; `PlatformAdministrationTests` proves neither revoke nor suspend can remove the final administrator |
| 401/403 with empty bodies | Fixed; both now return problem documents with stable codes |
| `NotFoundException` echoing internal type names and the caller's own identifier into the message | Fixed — the `"Entity with identifier 'x' was not found"` form is no longer shown to a client |

### Audited and found sound — no change needed

- **Tenant isolation.** `TenantContext` treats the `X-Tenant-Id` header as a *hint only* and
  `TenantAuthorizationService` re-resolves membership from the database on every request, checks
  tenant status, and returns 404 rather than 403 for a cross-tenant resource so its existence is
  not confirmed. Verified over HTTP: a school owner naming another school in the header is refused
  on all seven workspace routes tested, while their own workspace keeps working.
- **Platform admin vs school admin separation.** These are genuinely distinct in the model —
  `RoleNames.Admin` is a platform identity role granted only by the seeder, while a school admin
  is `TenantMemberRole.Owner` within one workspace. 18 platform-only routes were walked with a
  school owner's token; every one returned 403.
- **Credential enumeration.** A wrong password against a registered address and against an
  unregistered one produce byte-identical responses — same status, same code, same detail.
  Asserted directly by comparing the two.
- **Refresh token rotation and reuse detection**, lockout, and session revocation on suspension
  were already implemented and tested.
- **Health endpoints** publish only an overall status and each check's name and status — no
  durations, no exception text, no connection details. Verified against the real production
  database: `{"status":"Healthy","checks":[{"name":"api",...},{"name":"database",...}]}`.
- **Security headers**: `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, HSTS
  and HTTPS redirection outside Development.
- **Private files** live under `{root}/tenants/...` and `{root}/shared/...`, which no static file
  provider is pointed at; only `{root}/public` is served, at `/uploads`. Path traversal is blocked
  by normalising and then re-checking containment, and the stored filename is always generated.

---

## 7. Verification

| | Result |
|---|---|
| `dotnet build Ustaz.sln -c Release` | **Succeeded**, 0 warnings, 0 errors |
| `dotnet test Ustaz.sln -c Release` | **414 passed, 0 failed, 0 skipped** |
| | 265 `Ustaz.Application.Tests` · 149 `Ustaz.API.Tests` |
| Net new tests | **+66** (348 → 414) |
| OpenAPI spec | Generated from the running API: 288 paths, saved to [`docs/openapi-v1.json`](openapi-v1.json) |
| Production-mode startup | Verified: starts, `/health` reports `Healthy` including the **real production database**, anonymous reads work, authenticated routes return 401 |
| Startup failure diagnostic | Verified: exits 82 with a named list of missing keys and no values |

### New test files

| File | Tests | Covers |
|---|---|---|
| `RateLimitingTests` | 3 | Auth budget actually applies; throttle response shape; ordinary browsing unaffected |
| `PlatformAdminSeparationTests` | 25 | 18 platform-only routes refused to a school owner; cross-tenant header attempts; the legitimate case still works |
| `ArabicErrorSurfaceTests` | 11 | Arabic default, English on request, `Accept-Language`, credential enumeration, expired token, 403, 404, no internal leakage |
| `ProfilePictureTests` | 15 | Full lifecycle; five spoofing vectors; empty and oversized files; three path-traversal names |
| `PlatformAdministrationTests` | 12 | Overview counts; user search pagination/filter/no-credentials; grant and revoke; lockout guards |

### What was *not* tested against production

- **No write of any kind was made to the production database.** Reads were limited to
  `INFORMATION_SCHEMA`, `sys.indexes`, `sys.partitions`, `__EFMigrationsHistory` and row counts.
- **The production IIS host was not accessed.** Its environment variables, file system, app pool
  identity and stdout log could not be inspected, so the claim "the environment variable is
  missing on the host" is an inference from the reproduction plus the committed configuration —
  strongly supported, but confirm it on the host before concluding the deployment is fixed.
- **Production was not deployed to, and the site has not been verified as fixed.**
- Integration tests run on the EF Core **in-memory** provider. They prove routing, authorization,
  validation, localisation and handler behaviour; they do **not** prove SQL translation. The
  migration verification in §4 is what covers the real provider.

---

## 8. Outstanding risks and remaining work

### Critical

1. **Rotate the production database password.** It was committed to source control and is still
   present in `publish/appsettings.Production.json` on this machine. Removing it from the working
   tree does not un-distribute it. Until it is rotated, treat the production database as
   compromised. — *operational*
2. **Set `ConnectionStrings__DefaultConnection` on the production host**, then redeploy. This is
   the 500.30 fix. Until an authorised person does this and checks `/health`, the site is still
   down. — *operational*

### High

3. **Apply the two pending migrations to production** using the reviewed delta script, after a
   backup. Both tables affected are empty, so the risk is low, but it is still a production
   schema change. — *operational*
4. **Re-measure database round-trip latency from the production application server.** The 229 ms
   measured here may be an artefact of measuring from outside the datacentre. The decision about
   whether to rewrite the 12–24 round-trip dashboards depends entirely on this number. — *code +
   operational*
5. **Set `Jwt__Key` explicitly in production.** Without it a key is generated and persisted under
   the storage root; if that file is lost — and on IIS the configured path may fall back to the
   system temp directory — every issued token is invalidated. — *operational*
6. **Confirm the storage root is persistent on the production host.** Uploads must survive a
   redeploy. See [`production-deployment.md`](production-deployment.md#persistent-upload-storage).
   — *operational*

### Medium

7. **Platform-level settings** (`Manage platform-level settings`) is **not implemented.** There is
   no platform settings entity; adding one needs a new table and a migration. — *code*
8. **Platform-wide announcements** are **not implemented.** `Announcement` is tenant-scoped with a
   non-nullable `TenantId`; a platform-wide announcement needs either a nullable tenant or a
   separate entity, and in both cases a migration plus fan-out rules. — *code*
9. **Reduce dashboard round trips**, conditional on item 4. The viable route is raw SQL for the
   summary aggregates, which means those handlers can no longer be covered by the in-memory
   provider and need integration tests against SQL Server. — *code*
10. **Arabic coverage of business-rule messages is partial by design.** ~115 codes have Arabic. A
    code with no catalogue entry falls back to the authored English message, so nothing breaks,
    but the gap is silent. A test that asserts every `BusinessRuleException` code in the codebase
    has a catalogue entry would make it visible. — *code*
11. **Office documents and archives are not signature-verified.** A `.docx` is a ZIP, and so is a
    `.zip`, so the container proves nothing. These are stored privately and only ever returned
    through an authorised download endpoint, so a mislabelled one is a broken file rather than a
    vector — but it is a stated limit, not an oversight. — *accepted*

### Low

12. The empty migration `20261001052232_AddEdits` is harmless noise. Leave it; deleting it would
    desynchronise any environment that has already recorded it. — *code*
13. `UseHttpsRedirection` logged no redirect during the production-mode probe because no HTTPS
    port was configured in that run. Confirm TLS termination and the forwarded headers
    configuration on the real host. — *operational*
14. `GetAdminDashboardQuery` and `GetPlatformOverviewQuery` now overlap in intent. Neither is
    wrong; a future consolidation would reduce the surface. — *code*

---

## 9. Files changed

Grouped by what they are for. 51 source files touched, of which 13 are new.

### Startup and configuration (§2.1)
- `src/Ustaz.API/Services/StartupConfigurationCheck.cs` — **new.** Validates required config
  before anything is registered; reports all failures at once to stderr and the event log, keys
  only.
- `src/Ustaz.API/Program.cs` — calls the check first; logs non-fatal warnings once the logger
  exists; reorders `UseRateLimiter` after `UseAuthentication`; adds `UseRequestLocalization` as
  the first middleware; drops the overriding `RequireRateLimiting`; adds public-asset cache
  headers.
- `src/Ustaz.API/appsettings.json` — **credential removed**, placeholder plus explanatory comment.
- `.gitignore` — `publish/` added.

### Rate limiting (§2.3)
- `src/Ustaz.Infrastructure/Options/RateLimitingOptions.cs` — **new.** All three budgets,
  configuration-driven, production values as defaults.
- `src/Ustaz.API/Extensions/ApiServiceExtensions.cs` — global limiter instead of an overriding
  policy; localised 429.

### Localisation and errors (§2.8)
- `src/Ustaz.Application/Common/Localization/UstazLanguage.cs` — **new.** Language resolution,
  Arabic default.
- `src/Ustaz.Application/Common/Localization/ErrorCodes.cs` — **new.** The canonical codes for
  failures the domain does not already name.
- `src/Ustaz.Application/Common/Localization/ErrorCatalog.cs` — **new.** Arabic for ~115 codes,
  plus per-status fallbacks in both languages.
- `src/Ustaz.Application/Common/Localization/ValidationMessages.cs` — **new.** Field-level
  wording, resolved per failure.
- `src/Ustaz.API/Extensions/LocalizationExtensions.cs` — **new.** Request-culture resolution.
- `src/Ustaz.API/Middleware/GlobalExceptionHandler.cs` — stable `code` on every response,
  localised `detail`, `language` echoed, internal messages confined to Development.
- `src/Ustaz.API/Extensions/AuthenticationExtensions.cs` — `OnChallenge` and `OnForbidden` write
  real problem documents; expired tokens are reported as such.
- `src/Ustaz.Domain/Exceptions/{Unauthorized,Conflict,ForbiddenAccess,NotFound}Exception.cs` —
  optional stable codes; `NotFoundException` can now say whether its message names an identifier.
- `src/Ustaz.Application/Common/Validation/CommonValidationRules.cs` — every message through the
  `Func` overload.
- `src/Ustaz.Application/Features/Authentication/Commands/{Login,RefreshToken}.cs` —
  `INVALID_CREDENTIALS`, `ACCOUNT_LOCKED`, `REFRESH_TOKEN_INVALID`, `TOKEN_EXPIRED`.

### File and image storage (§2.4, §2.10)
- `src/Ustaz.Application/Common/Validation/FileSignature.cs` — **new.** Magic-byte verification
  for the formats USTAZ serves back to a browser.
- `src/Ustaz.Application/Common/Validation/FileUploadRules.cs` — signature check added; Arabic
  messages.
- `src/Ustaz.Application/Common/Models/FileUploadRequest.cs` — carries the captured `Header`.
- `src/Ustaz.API/Extensions/FormFileExtensions.cs` — `ToUploadRequestAsync` captures the header
  without consuming the stream.
- `src/Ustaz.Infrastructure/Services/Storage/LocalFileStorage.cs` — verifies the bytes it is about
  to write.
- `src/Ustaz.Infrastructure/Services/Media/MediaValidator.cs` — rebound to `StorageOptions`;
  signature check added.
- `src/Ustaz.Infrastructure/Options/StorageOptions.cs` — absorbed the media allow lists; added
  `PublicCacheSeconds`.
- `src/Ustaz.Infrastructure/DependencyInjection.cs`, `Ustaz.Infrastructure.csproj` — **Cloudinary
  removed.**
- `src/Ustaz.Infrastructure/Services/Media/{CloudinaryMediaService,LocalMediaService}.cs`,
  `Options/CloudinaryOptions.cs` — Cloudinary files **deleted**; the local service now verifies
  signatures.
- Controllers `Messages`, `StudentLearning`, `TeacherProfile`, `Tenant` — switched to the
  signature-aware conversion.

### Profile pictures for every role (§2.5)
- `src/Ustaz.Application/Features/Accounts/ProfilePictureFeature.cs` — **new.** Get, upload,
  remove; keeps the teacher profile row in step; deletes the old file only after the new one
  commits.
- `src/Ustaz.API/Controllers/AccountController.cs` — **new.** `/api/v1/me/profile-picture`.

### Global platform administration (§2.6, §2.7)
- `src/Ustaz.Application/Features/Admin/Queries/PlatformOverviewQueries.cs` — **new.** Platform
  overview and platform-wide user search.
- `src/Ustaz.Application/Features/Admin/Commands/PlatformAdministrators.cs` — **new.** List,
  grant, revoke, with the lockout guards and audit logging.
- `src/Ustaz.API/Controllers/PlatformAdminController.cs` — **new.** `/api/v1/platform/*`.
- `src/Ustaz.Application/Features/Admin/Commands/TeacherModeration.cs` — suspension now refuses to
  remove the last administrator or the caller.
- `src/Ustaz.Domain/Constants/AuditActions.cs` — two new audit actions.

### Tests
- **New:** `RateLimitingTests`, `PlatformAdminSeparationTests`, `ArabicErrorSurfaceTests`,
  `ProfilePictureTests`, `PlatformAdministrationTests`, `ExpiredTokenFactory`.
- `tests/Ustaz.API.Tests/UstazApiFactory.cs` — hosts can now set their own rate-limit budget via
  `UseSetting`, which is the one configuration layer available while `Program.cs`'s top-level
  statements run.
- `tests/Ustaz.API.Tests/AuthenticationFlowTests.cs` — two tests updated: they asserted the
  English login message, which is now Arabic by default. Their actual intent — that a wrong
  password is indistinguishable from an unknown account — is now asserted on the stable `code`,
  with the English wording pinned via an explicit language header.

### Documentation
- `docs/backend-audit-report.md`, `docs/frontend-api-integration.md`,
  `docs/frontend-api-changes.md`, `docs/production-deployment.md` — **new.**
- `docs/openapi-v1.json` — **new.** Generated from the running API, 288 paths.
- `docs/sql/20261002-initalCreate-to-AddEdits.sql`, `docs/sql/20261002-idempotent-full.sql` —
  **new.** Generated by `dotnet ef migrations script` for DBA review.
