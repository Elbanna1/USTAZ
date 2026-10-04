# USTAZ — أستاذ

Backend for USTAZ, a teacher-discovery and learning platform for the Egyptian market.
Teachers publish groups and recorded courses, students discover teachers and enroll,
and admins verify teachers and moderate content.

Built with **.NET 10 / ASP.NET Core Web API**, **Onion + Clean Architecture**,
**CQRS via MediatR**, **EF Core / SQL Server**, **ASP.NET Core Identity + JWT with
refresh-token rotation**, and ready to be consumed directly by a React client.

---

## Contents

1. [Architecture](#architecture)
2. [Technology](#technology)
3. [Folder structure](#folder-structure)
4. [Getting started](#getting-started)
5. [Environment variables](#environment-variables)
6. [Database and migrations](#database-and-migrations)
7. [Seeding](#seeding)
8. [Running with Docker](#running-with-docker)
9. [Swagger](#swagger)
10. [Authentication flow](#authentication-flow)
11. [API modules](#api-modules)
12. [Response contract](#response-contract)
13. [Business rules](#business-rules)
14. [Testing](#testing)
15. [Further documentation](#further-documentation)

---

## Further documentation

| Document | What it is for |
|---|---|
| [`docs/frontend-api-integration.md`](docs/frontend-api-integration.md) | The complete frontend integration guide: auth and token refresh, roles and access boundaries, the school header, pagination, uploads, notifications, rate limits, and an inventory of all 346 operations |
| [`docs/frontend-api-changes.md`](docs/frontend-api-changes.md) | Exactly what the frontend must update, and why. Start here if you already have a client |
| [`docs/backend-audit-report.md`](docs/backend-audit-report.md) | The October 2026 engineering audit: the verified `HTTP 500.30` root cause, security findings, measured performance figures, and the prioritised list of remaining work |
| [`docs/production-deployment.md`](docs/production-deployment.md) | Deployment runbook: required environment variables by name, hosting configuration, the safe migration procedure, persistent upload storage, log inspection, and a post-deploy checklist |
| [`docs/openapi-v1.json`](docs/openapi-v1.json) | The OpenAPI 3 specification, exported from a running instance. Load it into Postman or a client generator rather than hand-writing types |
| [`docs/sql/`](docs/sql) | Migration SQL generated for DBA review |

---

## Architecture

Dependencies point inwards only. The Domain knows nothing about EF Core, HTTP or MediatR.

```text
                      ┌──────────────────────┐
                      │      Ustaz.API       │  controllers, middleware, DI root
                      │  (thin, no logic)    │
                      └───────────┬──────────┘
                                  │
              ┌───────────────────┼───────────────────┐
              │                   │                   │
              ▼                   ▼                   ▼
  ┌───────────────────┐ ┌──────────────────┐ ┌──────────────────┐
  │ Ustaz.Application │ │ Ustaz.Persistence│ │Ustaz.Infrastructure│
  │ CQRS handlers,    │ │ DbContext, EF    │ │ JWT, Cloudinary,  │
  │ validators, DTOs, │ │ configurations,  │ │ SMTP, Redis cache │
  │ interfaces        │ │ migrations, seed │ │                   │
  └─────────┬─────────┘ └────────┬─────────┘ └─────────┬────────┘
            │                    │                     │
            └────────────────────┼─────────────────────┘
                                 ▼
                      ┌──────────────────────┐
                      │    Ustaz.Domain      │  entities, enums, value objects,
                      │   (depends on none)  │  domain events, invariants
                      └──────────────────────┘
```

| Layer | Responsibility | Depends on |
|---|---|---|
| **Domain** | Entities, enums, value objects, domain events, invariants (capacity, publishing, discount redemption) | nothing |
| **Application** | CQRS commands/queries, FluentValidation validators, DTOs, pipeline behaviours, abstractions (`IApplicationDbContext`, `IMediaService`, `ICacheService`, …) | Domain |
| **Persistence** | `UstazDbContext`, `IEntityTypeConfiguration<T>` classes, interceptors, migrations, seeding | Domain, Application |
| **Infrastructure** | JWT issuing, Cloudinary, SMTP/MailKit, Redis-backed cache, URL/WhatsApp guards | Domain, Application |
| **API** | Controllers, exception handler, Swagger, CORS, rate limiting, composition root | Application, Persistence, Infrastructure |

**Cross-cutting MediatR pipeline** (outermost first):
`UnhandledExceptionBehaviour → ValidationBehaviour → CachingBehaviour → PerformanceBehaviour`,
plus a `LoggingBehaviour` pre-processor.

Two `SaveChanges` interceptors run in Persistence: one stamps `CreatedAt`/`UpdatedAt` in UTC,
the other publishes domain events **after** the transaction commits.

---

## Technology

| Concern | Choice |
|---|---|
| Runtime | .NET 10, ASP.NET Core Web API (controllers) |
| Data | EF Core 10, SQL Server |
| Identity | ASP.NET Core Identity (`ApplicationUser : IdentityUser<Guid>`) |
| Auth | JWT bearer + rotating refresh tokens (SHA-256 hashed at rest) |
| CQRS | MediatR 12 |
| Validation | FluentValidation 12 |
| Mapping | Mapster |
| Logging | Serilog (console + rolling file) |
| Docs | Swagger / Swashbuckle with JWT auth |
| Cache | Redis when configured, in-memory otherwise |
| Media | Cloudinary when configured, local disk otherwise |
| Email | MailKit/SMTP when configured, file-drop otherwise |
| Versioning | Asp.Versioning, `/api/v1/...` |
| Tests | xUnit, Shouldly, Moq, EF InMemory, `WebApplicationFactory` |

---

## Folder structure

```text
Ustaz/
├── Ustaz.slnx
├── Directory.Build.props          # shared TFM / nullable / implicit usings
├── Dockerfile
├── docker-compose.yml
├── .env.example
│
├── src/
│   ├── Ustaz.Domain/
│   │   ├── Common/                # BaseEntity, DomainEvent, auditing contracts
│   │   ├── Constants/             # RoleNames, PolicyNames, CacheKeys, DomainRules
│   │   ├── Entities/              # aggregates + Entities/Identity
│   │   ├── Enums/
│   │   ├── Events/
│   │   ├── Exceptions/
│   │   └── ValueObjects/          # EgyptianPhoneNumber, Slug, TimeSlot, DateRange
│   │
│   ├── Ustaz.Application/
│   │   ├── Common/                # Behaviours, Interfaces, Models, Security, Validation
│   │   └── Features/              # one folder per module (vertical slices)
│   │       ├── Authentication/  Teachers/  Subjects/  Grades/
│   │       ├── Groups/  Courses/  Enrollments/  Discounts/
│   │       ├── Reviews/  Favorites/  Inquiries/  Notifications/
│   │       └── Schedule/  Dashboard/  Search/  Admin/  Shared/
│   │
│   ├── Ustaz.Persistence/
│   │   ├── Contexts/              # UstazDbContext + design-time factory
│   │   ├── Configurations/        # IEntityTypeConfiguration<T>, grouped by module
│   │   ├── Interceptors/
│   │   ├── Migrations/
│   │   └── Seeding/
│   │
│   ├── Ustaz.Infrastructure/
│   │   ├── Options/               # strongly typed settings
│   │   └── Services/              # Authentication, Caching, Email, Media, Messaging
│   │
│   └── Ustaz.API/
│       ├── Common/                # ApiResponse, ApiControllerBase
│       ├── Controllers/
│       ├── Extensions/            # auth, Swagger, CORS, versioning, rate limiting
│       ├── Middleware/            # GlobalExceptionHandler
│       └── Services/              # CurrentUserService (reads the JWT)
│
└── tests/
    ├── Ustaz.Application.Tests/   # domain invariants + handler tests
    └── Ustaz.API.Tests/           # HTTP contract + auth flow tests
```

---

## Getting started

**Prerequisites:** .NET 10 SDK, and SQL Server (local instance, LocalDB, or the compose stack).

```bash
git clone <repo> && cd Ustaz

dotnet restore
dotnet build
```

Point the API at your database and set a JWT signing key. For local work the simplest
route is user-secrets, which keeps everything out of source control:

```bash
cd src/Ustaz.API
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=UstazDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
dotnet user-secrets set "Seed:AdminEmail" "admin@ustaz.local"
dotnet user-secrets set "Seed:AdminPassword" "Admin@12345"
```

Then run:

```bash
dotnet run --project src/Ustaz.API
```

In `Development`, `Seed:ApplyMigrationsOnStartup` is `true`, so the schema is created and
reference data seeded on first boot. Swagger opens at the root URL.

---

## Environment variables

Configuration binds from `appsettings.json`, `appsettings.{Environment}.json`, user-secrets
and environment variables (`__` is the section separator). **No secret has a default.**

| Variable | Required | Purpose |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | yes | SQL Server connection string |
| `Jwt__Key` | yes | Signing key, **32+ characters**; the app refuses to start without it |
| `Jwt__Issuer`, `Jwt__Audience` | yes | Token issuer/audience |
| `Jwt__AccessTokenMinutes` | no (60) | Access-token lifetime |
| `Jwt__RefreshTokenDays` | no (14) | Refresh-token lifetime |
| `Cors__AllowedOrigins__0..n` | yes for browsers | Exact client origins; `AllowAnyOrigin` is never used |
| `Redis__ConnectionString` | no | Enables distributed cache; in-memory cache is used when empty |
| `Cloudinary__CloudName` / `ApiKey` / `ApiSecret` | no | Enables Cloudinary; local disk storage is used when empty |
| `Email__Host` / `Port` / `UserName` / `Password` | no | Enables SMTP; otherwise emails are written to `App_Data/sent-emails` |
| `Email__ClientBaseUrl` | no | Base URL used to build verification/reset links |
| `Seed__ApplyMigrationsOnStartup` | no (false) | Applies migrations at boot |
| `Seed__SeedEssentials` | no (true) | Seeds roles, subjects, grades, admin |
| `Seed__SeedDemoData` | no (false) | Seeds fictional demo teachers/groups/courses |
| `Seed__AdminEmail` / `Seed__AdminPassword` | no | The first admin is created **only** when both are set |
| `Swagger__Enabled` | no (false) | Exposes Swagger outside Development |

---

### File storage

Private files — homework answers, message attachments, certificates — are written through
`IFileStorage`, never to a path baked into the code:

```text
{Storage:RootPath}/tenants/{tenantId}/{area}/{ownerId}/{id}{ext}
{Storage:RootPath}/public/...        # local media when Cloudinary is not configured
```

| Setting | Default | Notes |
|---|---|---|
| `Storage__RootPath` | `storage` | Absolute, or relative to the content root. Mount a volume here in production. |
| `Storage__MaxFileSizeInMegabytes` | `25` | Enforced before a byte is written, and again after. |

The stored filename is always generated, the original name is display text only, every key is
re-resolved and checked to still sit inside the root, and reads are authorized per request —
so a physical path is never exposed and a tampered key cannot escape the workspace folder.

### Transactional email

Gmail SMTP, configured entirely from configuration — the app password is never in source,
never in `appsettings*.json`, and never logged:

| Setting | Value | Notes |
|---|---|---|
| `Email__Provider` | `Gmail` | Informational. |
| `Email__Host` | `smtp.gmail.com` | |
| `Email__Port` | `587` | STARTTLS. |
| `Email__UserName` | `support.webcraft.eg@gmail.com` | Gmail requires the sender to match. |
| `Email__Password` | **secret** | User Secrets locally, `Email__Password` in production. |
| `Email__FromEmail` / `Email__FromName` | `support.webcraft.eg@gmail.com` / `Ustaz` | |
| `Email__SupportEmail` | `support.webcraft.eg@gmail.com` | Reply-to, and the footer of every email. |
| `Email__ClientBaseUrl` | client origin | Every CTA link is built against it. |
| `Email__TimeoutInSeconds` | `30` | A hung SMTP conversation never holds a request open. |
| `Email__MaxDeliveryAttempts` | `3` | The retry pass stops here. |
| `Email__Enabled` | `true` | Off records every message as skipped and sends nothing. |

Set the app password once, locally:

```bash
cd src/Ustaz.API
dotnet user-secrets set "Email:Password" "<gmail-app-password>"
```

Generate the app password at <https://myaccount.google.com/apppasswords> (2-Step
Verification must be on). In production pass it as `Email__Password`; in compose it comes
from `EMAIL_PASSWORD` in `.env`.

**Without the secret the platform still runs.** A host with a username but no password is
treated as unconfigured, and messages are written to `{Storage:RootPath}/sent-emails` as
HTML files — so a verification or reset link is still clickable while developing.

Every email is a template in `EmailCatalog`: Egyptian Arabic and English subject and body,
a CTA, and the placeholders it needs. Nothing writes email HTML in a handler. Rendering is
one responsive layout that switches to `dir="rtl" lang="ar"` or `dir="ltr" lang="en"`, with
a plain-text part for clients that refuse HTML.

Delivery is recorded in `EmailDeliveries` — event, recipient, subject, language, status,
attempts, error category and correlation id — so support can answer "did it go out". A
security email stores no payload, which is exactly why it is never retried: a stale reset
link is worse than none.

### Real-time and background work

| Setting | Default | Notes |
|---|---|---|
| `Realtime__Enabled` | `true` | SignalR hub at `/hubs/notifications`. Off = notifications are fetched, not pushed. |
| `Exams__ExpirySweepEnabled` | `true` | Closes abandoned exam sittings. |
| `Exams__ExpirySweepSeconds` | `300` | Sweep interval, clamped to 30–3600. |
| `EarlyWarning__Enabled` | `true` | Weekly guardian alerts for students falling behind. |
| `EarlyWarning__IntervalInHours` | `24` | Pass interval; the alerts themselves dedupe per week. |

The same scheduled pass retries emails that failed earlier, capped by
`Email__MaxDeliveryAttempts`.

SignalR is part of ASP.NET Core, so real-time delivery adds no external dependency. Turn it
off and the platform behaves identically, minus the live push.

---

## Database and migrations

```bash
# Create a migration
dotnet ef migrations add <Name> \
  --project src/Ustaz.Persistence \
  --startup-project src/Ustaz.Persistence \
  --output-dir Migrations

# Apply it
export USTAZ_CONNECTIONSTRING="Server=localhost;Database=UstazDb;Trusted_Connection=True;TrustServerCertificate=True"
dotnet ef database update \
  --project src/Ustaz.Persistence \
  --startup-project src/Ustaz.Persistence
```

`UstazDbContextFactory` supplies the design-time connection string from
`USTAZ_CONNECTIONSTRING` or `ConnectionStrings__DefaultConnection`.

The initial migration creates **31 tables** and **78 indexes**, including unique constraints on
slugs, `(TeacherId, Code)` for discounts, `(StudentId, GroupId)` filtered on non-cancelled
enrollments, one-review-per-target indexes, and composite covering indexes for the public
discovery queries.

---

## Seeding

Every run states a **mode**, and `SeedSafety` checks that mode against the hosting environment
before a single row is written. The mode is the whole safety model.

| Mode | Writes | Allowed where |
|---|---|---|
| `ReferenceData` | Roles, 18 subjects, 5 grades, the SaaS plan catalogue | everywhere, production included |
| `ProductionBootstrap` | Reference data plus the first platform administrator | everywhere, but needs an explicit confirmation flag |
| `DemoData` | The full fictional dataset below | Development and Testing; never Production, with no override |
| `TestFixtures` | The small fixed dataset the integration tests assert on | Development and Testing only |

| Data | When |
|---|---|
| Roles `Admin`, `Teacher`, `Student`, `Parent` | `Seed:SeedEssentials` |
| 18 subjects across the Egyptian curriculum | `Seed:SeedEssentials` |
| 5 grades (Primary → Third Secondary) | `Seed:SeedEssentials` |
| Subscription plans and their features | `Seed:SeedEssentials` |
| Administrator account | only when `Seed:AdminEmail` **and** `Seed:AdminPassword` are both set |
| 10 workspaces, 10 teachers, 12 students, 4 guardians, and the groups, courses, lessons, enrolments, attendance, exams, homework, certificates, reviews, messages, notifications, invoices and tuition payments that hang off them | `Seed:SeedDemoData` |

`SeedAsync` returns a `SeedReport` — inserted, updated and skipped per entity — so a caller can
assert on what a run did rather than reading the log.

### Idempotency

Every seeded row's primary key is derived from a name (`SeedId`, an RFC 4122 version 5 UUID),
so the same row has the same id on every machine and in every database. Insertion is
"add the ids that are missing", which makes a second run a no-op rather than a duplicate, and
means a run interrupted half-way completes on the next attempt instead of starting over.

A re-run against an already-seeded database reports **0 inserted, 0 updated**. Nothing is
deleted and nothing an operator has edited is overwritten.

The one exception is `BackfillLegacyMediaAsync`, which updates rows it did not insert. It
exists because id-keyed insertion cannot reach a row that is already present: a lesson seeded
before the media catalogue existed would keep its dead `/storage/demo/…` URL forever. It
rewrites only values still carrying that prefix, so a genuine upload under `/uploads/` and any
hand-set URL are left alone, and it reports zero on every run after the first.

### Demo media

The demo rows point at real, working images, video and documents rather than placeholder
paths — see `DemoMedia`:

- **Wikimedia Commons** for every photograph, course thumbnail, lesson video and handout.
  Everything there is freely licensed or public domain, and each URL carries its licence
  beside it in the source. Files are chosen from curated categories, so the chemistry course
  shows a chemistry laboratory and the maths lessons are an actual recorded lesson series.
  Videos use Commons' scaled 480p rendition: the originals run to a gigabyte apiece.
- **DiceBear** for every human avatar. The demo people are invented, so a generated avatar is
  the honest choice — it is visibly not a photograph and depicts nobody.

Media URLs are absolute, so the client renders them directly and this instance needs nothing
in its own upload storage.

### The demo personas

The demo teachers, students and guardians are **invented personas for development only**, not
real people, and the quotes attributed to them are written copy rather than anybody's words.
Their password comes from `Seed:DemoPassword` (default `Demo@1234` in Development).

Each teacher owns exactly one workspace — `UX_TeacherProfiles_TenantId` is unique. A teacher
who works at somebody else's centre is a `TenantMember` there and still owns the workspace
their own account came with, which is why ten teachers need ten workspaces.

---

## Running with Docker

Docker is optional; nothing in the app requires it.

```bash
cp .env.example .env      # then fill in MSSQL_SA_PASSWORD and JWT_KEY
docker compose up --build
```

This starts SQL Server 2022, Redis 7 and the API on `http://localhost:8080`.
The API waits for both dependencies to report healthy, applies migrations, and seeds
reference data.

---

## Swagger

* Development: `http://localhost:5195/swagger` (the root path redirects there)
* Other environments: only when `Swagger__Enabled=true`

Swagger documents JWT bearer auth (click **Authorize**, paste the access token — no
`Bearer ` prefix needed), request/response schemas, validation responses and per-endpoint
authorization. Endpoints are grouped by tag: Authentication, Teachers, Students, Subjects,
Grades, Groups, Courses, Schedule, Enrollments, Discounts, Reviews, Favorites, Inquiries,
Notifications, Search, Dashboard, Admin.

---

## Authentication flow

```text
 register / login ──▶ { accessToken, refreshToken, user }
                         │
                         ├─ accessToken  → Authorization: Bearer <token>, ~60 min
                         └─ refreshToken → opaque, 64 random bytes

 POST /api/v1/auth/refresh-token
      │
      ├─ token unknown / expired ────────────▶ 401
      ├─ token already rotated (reuse!) ─────▶ revoke the entire family, 401
      └─ token valid ────────────────────────▶ new pair, old token marked rotated
```

* Only the **SHA-256 hash** of a refresh token is stored; a database leak cannot be replayed.
* Tokens are grouped into a **family** per login, so detecting reuse revokes every sibling.
* Resetting a password revokes all outstanding sessions.
* Suspending an account revokes its sessions and unpublishes its groups.
* Login returns the same message for unknown emails and wrong passwords, so the endpoint
  cannot be used to enumerate accounts. The same applies to forgot-password and
  resend-verification, which always report success.
* Auth endpoints are rate-limited to 10 requests/minute per IP; everything else to 300.

**JWT claims:** `sub`/`uid`, `email`, `displayName`, `isVerified`, one `role` claim per role,
plus `teacherId`, `studentId` or `parentId` when the corresponding profile exists.

---

## API modules

All routes are versioned: `/api/v1/...`

<details open>
<summary><b>Authentication</b></summary>

```http
POST   /api/v1/auth/register/student
POST   /api/v1/auth/register/teacher
POST   /api/v1/auth/register/parent    (childEmail optional: requests access, never grants it)
POST   /api/v1/auth/login
POST   /api/v1/auth/refresh-token
POST   /api/v1/auth/logout
POST   /api/v1/auth/verify-email
POST   /api/v1/auth/resend-verification
POST   /api/v1/auth/forgot-password
POST   /api/v1/auth/reset-password
POST   /api/v1/auth/change-password   (authenticated; revokes every session)
PUT    /api/v1/auth/me/language       (ar-EG | en; drives emails and notifications)
GET    /api/v1/auth/me
```
</details>

<details>
<summary><b>Teachers</b></summary>

```http
GET    /api/v1/teachers            ?subjectId&gradeId&city&teachingMode&minRating&search&sort&page&pageSize
GET    /api/v1/teachers/{id}
GET    /api/v1/teachers/{id}/profile
GET    /api/v1/teachers/{id}/groups
GET    /api/v1/teachers/{id}/courses
GET    /api/v1/teachers/{id}/schedule
GET    /api/v1/teachers/{id}/reviews
GET    /api/v1/teachers/{id}/reviews/summary
GET    /api/v1/teachers/{id}/achievements

GET    /api/v1/teacher/profile
PUT    /api/v1/teacher/profile
PUT    /api/v1/teacher/about
PUT    /api/v1/teacher/subjects
PUT    /api/v1/teacher/grades
PUT    /api/v1/teacher/social-links
PUT    /api/v1/teacher/achievements
POST   /api/v1/teacher/profile/image        (multipart)
POST   /api/v1/teacher/profile/cover        (multipart)
```
</details>

<details>
<summary><b>Subjects &amp; grades</b></summary>

```http
GET    /api/v1/subjects
GET    /api/v1/subjects/{idOrSlug}
GET    /api/v1/subjects/{id}/teachers | /groups | /courses

GET    /api/v1/grades
GET    /api/v1/grades/{idOrSlug}
GET    /api/v1/grades/{id}/teachers | /groups | /courses
```
</details>

<details>
<summary><b>Groups</b></summary>

```http
GET    /api/v1/groups              ?subjectId&gradeId&teacherId&teachingMode&type
                                   &minPrice&maxPrice&hasDiscount&hasAvailableSeats&search&sort
GET    /api/v1/groups/{id}
GET    /api/v1/groups/{id}/schedule | /curriculum | /materials | /reviews

GET    /api/v1/teacher/groups
POST   /api/v1/teacher/groups
GET    /api/v1/teacher/groups/{id}
PUT    /api/v1/teacher/groups/{id}
DELETE /api/v1/teacher/groups/{id}
POST   /api/v1/teacher/groups/{id}/publish
POST   /api/v1/teacher/groups/{id}/unpublish

POST   /api/v1/teacher/groups/{id}/schedule
PUT    /api/v1/teacher/groups/{id}/schedule/{scheduleId}
DELETE /api/v1/teacher/groups/{id}/schedule/{scheduleId}

POST   /api/v1/teacher/groups/{id}/curriculum
PUT    /api/v1/teacher/groups/{id}/curriculum/{curriculumId}
DELETE /api/v1/teacher/groups/{id}/curriculum/{curriculumId}
```
</details>

<details>
<summary><b>Courses</b></summary>

```http
GET    /api/v1/courses             ?subjectId&gradeId&teacherId&minPrice&maxPrice&isFree&search&sort
GET    /api/v1/courses/{id}
GET    /api/v1/courses/{id}/sections | /reviews

GET    /api/v1/teacher/courses
POST   /api/v1/teacher/courses
GET    /api/v1/teacher/courses/{id}
PUT    /api/v1/teacher/courses/{id}
DELETE /api/v1/teacher/courses/{id}
POST   /api/v1/teacher/courses/{id}/publish
POST   /api/v1/teacher/courses/{id}/unpublish

POST   /api/v1/teacher/courses/{id}/sections
PUT    /api/v1/teacher/courses/{id}/sections/{sectionId}
DELETE /api/v1/teacher/courses/{id}/sections/{sectionId}
POST   /api/v1/teacher/courses/{id}/lessons
PUT    /api/v1/teacher/courses/{id}/lessons/{lessonId}
DELETE /api/v1/teacher/courses/{id}/lessons/{lessonId}
```
</details>

<details>
<summary><b>Enrollments</b></summary>

```http
POST   /api/v1/enrollments/groups
GET    /api/v1/enrollments/groups/{id}
POST   /api/v1/enrollments/groups/{id}/cancel

POST   /api/v1/enrollments/courses
GET    /api/v1/enrollments/courses/{id}
POST   /api/v1/enrollments/courses/{id}/cancel
PUT    /api/v1/enrollments/courses/{id}/progress

GET    /api/v1/student/enrollments/groups | /courses
GET    /api/v1/teacher/enrollments/groups | /courses    ?groupId&status&search

POST   /api/v1/teacher/groups/{groupId}/students        (staff enrol a student; Students.Manage)

# Lesson level progress. The percentage is derived server side from completed lessons.
GET    /api/v1/student/courses/{courseId}/progress
POST   /api/v1/student/courses/{courseId}/lessons/{lessonId}/complete
DELETE /api/v1/student/courses/{courseId}/lessons/{lessonId}/complete
```
</details>

<details>
<summary><b>Students</b></summary>

```http
GET    /api/v1/student/profile
PUT    /api/v1/student/profile                          (partial: omitted fields are untouched)
GET    /api/v1/student/dashboard | /progress | /upcoming-classes | /recent-activity
GET    /api/v1/student/attendance | /exams | /grades | /homework | /announcements | /payments
POST   /api/v1/student/homework/{id}/submit
POST   /api/v1/student/homework/{id}/attachment         (multipart; 20 MB cap, type allow list)
PUT    /api/v1/student/homework/submissions/{id}
GET    /api/v1/student/guardians                        (pending requests first; each row has linkId)
POST   /api/v1/student/guardians/{linkId}/respond       ?approve=true|false
```
</details>

<details>
<summary><b>Parents (guardians)</b></summary>

```http
GET    /api/v1/parent/profile
PUT    /api/v1/parent/profile                           (partial: omitted fields are untouched)
GET    /api/v1/parent/dashboard
GET    /api/v1/parent/children
POST   /api/v1/parent/children/link                     (studentEmail + verificationPhoneNumber)
DELETE /api/v1/parent/children/{studentId}
GET    /api/v1/parent/children/{studentId}
GET    /api/v1/parent/children/{studentId}/progress | /analytics | /grades
GET    /api/v1/parent/children/{studentId}/attendance | /exams | /homework | /payments
GET    /api/v1/parent/announcements
```

A guardian account is created by `POST /api/v1/auth/register/parent`, which may name one child.
Naming a child only *requests* access. The link is auto-confirmed when the supplied phone number
matches the parent number already on the student's profile; otherwise it stays pending until the
student approves it from `/student/guardians`. Either way the student is notified, and the
notification carries the `linkId` the response endpoint is keyed by — so an unwanted link can
never go unnoticed. A sign-up naming an unknown child email still succeeds: failing it would turn
the field into an account oracle for student emails.

Every `/parent/children/{studentId}` route goes through the same guard, so a guardian can only
read a child whose link is confirmed.
</details>

<details>
<summary><b>Exams (offline marks and online sittings)</b></summary>

```http
# Authoring
POST   /api/v1/teacher/exams                            (isOnline, maxAttempts, showResultsImmediately)
GET    /api/v1/teacher/exams | /{id}
PUT    /api/v1/teacher/exams/{id}
DELETE /api/v1/teacher/exams/{id}
POST   /api/v1/teacher/exams/{id}/publish

# Question paper. Locked as soon as the first student starts the exam.
GET    /api/v1/teacher/exams/{id}/questions             (includes the answer key)
POST   /api/v1/teacher/exams/{id}/questions
PUT    /api/v1/teacher/exams/{id}/questions/{questionId}
DELETE /api/v1/teacher/exams/{id}/questions/{questionId}

# Sittings
GET    /api/v1/teacher/exams/{id}/attempts              ?status&awaitingGrading
GET    /api/v1/teacher/exams/attempts/{attemptId}
PUT    /api/v1/teacher/exams/attempts/{attemptId}/grade (essay marks)

# Recorded marks for offline exams (unchanged)
GET    /api/v1/teacher/exams/{id}/results
POST   /api/v1/teacher/exams/{id}/results | /results/bulk
PUT    /api/v1/teacher/exams/{id}/results/{studentId}

# Student side
POST   /api/v1/student/exams/{id}/attempts              (starts, or resumes the open sitting)
GET    /api/v1/student/exams/{id}/attempts
GET    /api/v1/student/exams/attempts/{attemptId}
POST   /api/v1/student/exams/attempts/{attemptId}/submit
```
</details>

<details>
<summary><b>Schedule, discounts, reviews, favorites, inquiries, notifications</b></summary>

```http
GET    /api/v1/schedule            ?teacherId&subjectId&gradeId&dayOfWeek&teachingMode
GET    /api/v1/schedule/upcoming
GET    /api/v1/teacher/schedule
POST   /api/v1/teacher/schedule
PUT    /api/v1/teacher/schedule/{id}
DELETE /api/v1/teacher/schedule/{id}?groupId=
GET    /api/v1/student/schedule | /schedule/upcoming

POST   /api/v1/discounts/validate
GET    /api/v1/teacher/discounts
POST   /api/v1/teacher/discounts
GET    /api/v1/teacher/discounts/{id}
PUT    /api/v1/teacher/discounts/{id}
DELETE /api/v1/teacher/discounts/{id}
POST   /api/v1/teacher/discounts/{id}/activate | /deactivate

GET    /api/v1/reviews             ?teacherId&groupId&courseId&minRating&sort
POST   /api/v1/reviews
PUT    /api/v1/reviews/{id}
DELETE /api/v1/reviews/{id}

GET    /api/v1/student/favorites/teachers | /courses
POST   /api/v1/student/favorites/teachers/{teacherId} | courses/{courseId}
DELETE /api/v1/student/favorites/teachers/{teacherId} | courses/{courseId}

POST   /api/v1/inquiries                       (anonymous allowed)
GET    /api/v1/inquiries/{id}
GET    /api/v1/student/inquiries
GET    /api/v1/teacher/inquiries
GET    /api/v1/teacher/inquiries/{id}
PUT    /api/v1/teacher/inquiries/{id}/status

GET    /api/v1/messages/unread-count
POST   /api/v1/messages/conversations/{id}/attachments   (multipart; private storage)
GET    /api/v1/messages/attachments/{attachmentId}       (participants only; always a download)

GET    /api/v1/notifications        ?unreadOnly&type&category&minimumPriority&page&pageSize
GET    /api/v1/notifications/{id}
GET    /api/v1/notifications/unread-count
PUT    /api/v1/notifications/{id}/read
PUT    /api/v1/notifications/read-all
DELETE /api/v1/notifications/{id}
DELETE /api/v1/notifications/read                        (clears read ones; keeps unread)
GET    /api/v1/notifications/preferences
PUT    /api/v1/notifications/preferences

WS     /hubs/notifications                               (SignalR; JWT via access_token)
```
</details>

<details>
<summary><b>Certificates</b></summary>

```http
GET    /api/v1/student/certificates
GET    /api/v1/student/certificates/{id}                 (includes the verification code)

GET    /api/v1/teacher/certificates                      ?courseId&status&search
GET    /api/v1/teacher/certificates/{id}
POST   /api/v1/teacher/certificates/{id}/revoke

GET    /api/v1/public/certificates/verify/{code}         (anonymous; names and dates only)
```
</details>

<details>
<summary><b>Dashboards, search, admin</b></summary>

```http
GET    /api/v1/student/dashboard | /progress | /upcoming-classes | /recent-activity
GET    /api/v1/teacher/dashboard
GET    /api/v1/teacher/analytics            ?months=12
GET    /api/v1/teacher/analytics/revenue | /students | /groups
GET    /api/v1/teacher/analytics/attendance | /grades | /courses
GET    /api/v1/teacher/analytics/at-risk                 ?windowInDays&minimumLevel&take
GET    /api/v1/teacher/analytics/at-risk/{studentId}

GET    /api/v1/search?q=                    ?subjectId&gradeId&take

GET    /api/v1/admin/dashboard | /analytics | /reports
GET    /api/v1/admin/teachers | /teachers/{id}
POST   /api/v1/admin/teachers/{id}/verify | /suspend | /activate | /feature
GET    /api/v1/admin/students | /groups | /courses | /enrollments | /reviews | /inquiries | /discounts
POST   /api/v1/admin/reviews/{id}/moderate
POST   /api/v1/admin/users/{userId}/state

GET    /health                              (anonymous: API, database, Redis)
```
</details>

---

## Response contract

**Success** — every endpoint that returns a body:

```json
{
  "success": true,
  "message": "Operation completed successfully.",
  "data": { }
}
```

**Paged list** — `data` is always this shape:

```json
{
  "items": [],
  "pageNumber": 1,
  "pageSize": 12,
  "totalCount": 100,
  "totalPages": 9,
  "hasPrevious": false,
  "hasNext": true
}
```

`pageSize` is clamped to 100; `page` defaults to 1.

**Failure** — RFC 7807 `application/problem+json`:

```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc7807",
  "title": "Validation failed.",
  "status": 400,
  "detail": "One or more validation failures occurred.",
  "instance": "/api/v1/auth/register/student",
  "errors": { "Email": ["Email must be a valid email address."] },
  "traceId": "0HN...",
  "success": false
}
```

Business-rule failures add a machine-readable `code` (for example `GROUP_FULL`,
`TEACHER_NOT_VERIFIED`, `SUBJECT_NOT_TAUGHT`) so the client can branch without parsing prose.

| Status | Used for |
|---|---|
| 200 / 201 / 204 | success, created, deleted |
| 400 | validation failure (with `errors`) |
| 401 | missing/expired/invalid token |
| 403 | authenticated but not allowed (wrong role or not the owner) |
| 404 | unknown resource, or one the caller may not see |
| 409 | conflict (duplicate email, duplicate enrollment, duplicate review) |
| 422 | business-rule violation (with `code`) |
| 429 | rate limit exceeded |
| 500 | unexpected — logged in full, never returned in detail |

Stack traces and internal exception messages are never sent to clients.

---

## Business rules

Enforced in the domain and handlers, and covered by tests:

* A teacher may only create a group or course for a **subject and grade on their own profile**.
* A subject or grade cannot be removed from a profile while groups or courses still use it.
* Only **verified** teachers can publish.
* A group needs **at least one active weekly session** and a positive capacity to be published.
* A course needs a description and **at least one published lesson with a video**.
* Two sessions of the same group may not **overlap** on the same weekday.
* Enrollment is refused when the group is **full**, unpublished or inactive.
* A student cannot hold **two active enrollments** in the same group or course.
* Capacity cannot be lowered below the number of students already enrolled.
* Discounts honour active flag, start/end window, usage limit, minimum basket and maximum cap —
  and are capped at the basket amount, so a final price is never negative.
* A student may only review a teacher, group or course they actually **enrolled in**, once per target.
* Teacher, group and course ratings are **recalculated** after every review create/update/delete/moderation.
* Teachers only ever touch their own groups, courses, schedules, discounts and inquiries;
  students only their own enrollments, favorites, notifications and reviews. Admins bypass
  ownership but never existence checks.
* Deleting a group or course with history **archives/unpublishes** it instead of destroying
  student records.
* WhatsApp links are built from validated Egyptian numbers; group invite links must be genuine
  `chat.whatsapp.com` URLs. Social links are checked against their claimed platform, and all
  external URLs are screened for private/loopback hosts.
* Enrolling a student — whether they join themselves or a member of staff registers them —
  makes them a **member of the workspace** and charges the plan's student quota. Announcements,
  messaging and tuition records all key off that membership.
* **Course progress is derived on the server** from the lessons the student completed, out of the
  published lessons only. Reopening a lesson takes the course back out of "completed".
* An **online exam** locks its question paper as soon as the first student starts it, and its
  maximum score always equals the points on the paper.
* A sitting is bounded by the exam's duration, captured when the student starts; a late submit is
  still marked but recorded as `Expired`. Closed sittings count against the attempt limit.
* Multiple choice, true/false and short answer are **graded by the server**; an essay holds the
  attempt open until a teacher marks it, and no answer may be awarded more than it is worth.
* A student never receives the answer key, the explanation or their own score until the exam
  releases results (`resultsPublished`, or `showResultsImmediately` for a self-marking quiz).
* Uploads are checked against a shared allow list: 5 MB for images, 20 MB for documents, with
  both the extension and the declared content type on the list.
* **Every notification comes from the catalog**, not from a string in a handler. One place holds
  the Egyptian-Arabic copy, the English copy, the category, the priority and the deep link.
* A **critical** notification (password changed, account suspended, failed payment, expired
  subscription) is delivered even to a user who switched that category off. Everything else
  respects per-category preferences.
* A notification carrying a **deduplication key** is written at most once per recipient, enforced
  by a unique index — a retried handler, a re-published announcement or a second background
  sweep cannot produce a duplicate.
* Notifications are **persisted first and pushed second**. A dead SignalR connection costs
  nothing: the bell still shows the notification on the next fetch.
* An abandoned exam sitting is closed by a background sweep, and by the lazy path on next
  touch. The attempt carries a concurrency token, so a student submitting at the same instant
  wins and the sweep stands down.
* Finishing every published lesson of a course issues a **certificate** with a random
  verification code. Verification is public, reads snapshotted names and dates only, and a
  revoked certificate stops verifying.
* Private files (homework, message attachments, certificates) never get a public URL. They are
  addressed by an opaque storage key under `tenants/{tenantId}/...`, and every read re-checks
  membership *and* that the key belongs to that workspace.
* **At-risk** scoring is deterministic and explainable: attendance, homework, exam average,
  course progress and recency, each with a stated threshold and the measured value attached.
* **Email is a separate channel from the bell**, not a copy of it. The notification row is
  written first; a mail server that is down costs an email and never a notification, and
  never fails the business operation that raised it.
* An event is emailed when its catalog key exists in *both* catalogs. Security, payment,
  subscription, certificate, grade, exam, homework and enrollment default to in-app **and**
  email; attendance, announcements and system notices stay in-app until the user opts in.
* A **security email is always delivered**, whatever the user switched off — and a new-message
  email is only sent to someone who is *not* currently connected.
* Emails deduplicate on `{recipient, key}` with a unique index, so a replayed handler or a
  second nightly pass cannot send the same reminder twice.
* **No token is ever stored.** Verification and reset emails keep no payload, so nothing in
  the database can replay a one-time link; logs carry a hash of the address, never the
  address, the body, the token or the SMTP password.
* Emails are rendered in the **recipient's own language** (`ar-EG` by default, `en` opt-in),
  from the single `PreferredLanguage` field that also drives notifications.

---

## Testing

```bash
dotnet test
```

| Project | Covers |
|---|---|
| `Ustaz.Application.Tests` | Discount maths and redemption windows, group capacity and publishing, course publishing, Egyptian phone parsing, slug generation, time-slot overlap, review ratings, plus handler tests on EF InMemory: enrollment (self-service and staff-registered), workspace membership and plan quota, ownership and subject/grade rules, lesson-level course progress, the online exam lifecycle (authoring, sitting, auto-marking, manual marking, answer-key secrecy), student profile, homework file uploads, and guardian self-registration with the student side of the link it requests |
| `Ustaz.API.Tests` | Full HTTP pipeline via `WebApplicationFactory`: response envelope, pagination contract, ProblemDetails shape, security headers, API versioning, registration → login → refresh → rotation → reuse detection, password change, student profile, health probes, correlation ids, the notification inbox, public certificate verification, guardian sign-up end to end (Parent role, `parentId` claim, the ParentOnly policy and the child-link request/approve/reject flow), and role-based authorization on the exam, progress, analytics and private-file routes |

| `Ustaz.API.Tests` (continued) | Rate limiting on the credential endpoints; the platform-admin / school-admin boundary across 18 platform-only routes plus cross-tenant header attempts; the Arabic error surface (default language, English on request, stable error codes, expired sessions, credential-enumeration resistance); profile pictures for every role including file-signature spoofing and path traversal; platform administration (school counts, platform-wide user search, grant and revoke, and the guards that stop the last administrator being locked out) |

Current status: **428 tests, all passing.**

See [`docs/backend-audit-report.md`](docs/backend-audit-report.md) for the October 2026 audit:
the verified `HTTP 500.30` root cause, the database state by environment, and the prioritised
list of what remains.

---

## Performance notes

* Every read query uses `AsNoTracking()` and projects straight into DTOs with `Select`,
  so entities are never materialised and N+1 loading cannot occur.
* Listings are paged at the database with a single `Count` + `Skip`/`Take`.
* Denormalised counters (`Rating`, `ReviewCount`, `EnrolledStudentsCount`, `StudentsCount`,
  `LessonsCount`, `DurationInMinutes`) keep listing queries free of aggregation.
* Composite indexes back the default discovery queries.
* Cacheable public queries opt in via `ICacheableQuery`; a cache outage falls through to the
  handler rather than failing the request.
* Every handler takes and honours a `CancellationToken`.
