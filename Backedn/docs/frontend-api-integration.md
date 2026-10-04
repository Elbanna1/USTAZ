# USTAZ API — frontend integration guide

Verified against the running API on **2 October 2026**. The endpoint inventory in
[§13](#13-complete-endpoint-inventory) was generated from
[`openapi-v1.json`](openapi-v1.json), which was itself exported from a live instance — not
written by hand. **346 operations across 288 routes**, every one carrying its purpose.

If you only want to know *what changed and what you must update*, read
[`frontend-api-changes.md`](frontend-api-changes.md) instead.

---

## Contents

1. [Base URL, versioning and envelopes](#1-base-url-versioning-and-envelopes)
2. [Errors and error codes](#2-errors-and-error-codes)
3. [Language — Arabic is the default](#3-language--arabic-is-the-default)
4. [Authentication and token refresh](#4-authentication-and-token-refresh)
5. [Roles and access boundaries](#5-roles-and-access-boundaries)
6. [Multi-tenancy: the school header](#6-multi-tenancy-the-school-header)
7. [Pagination, filtering, sorting, search](#7-pagination-filtering-sorting-search)
8. [File uploads, images and private downloads](#8-file-uploads-images-and-private-downloads)
9. [Notifications and real time](#9-notifications-and-real-time)
10. [Rate limits](#10-rate-limits)
11. [Health checks](#11-health-checks)
12. [Local development](#12-local-development)
13. [Complete endpoint inventory](#13-complete-endpoint-inventory)
14. [Known gaps and limitations](#14-known-gaps-and-limitations)

---

## 1. Base URL, versioning and envelopes

All routes live under `/api/v{version}` and the only version is `1`:

```
https://api.ustaz.app/api/v1/...
```

The version can also travel in an `x-api-version: 1.0` header, and it is assumed when
unspecified — but **use the URL segment**; it is what the whole surface is documented with.

### Success

Every successful response uses one envelope:

```json
{
  "success": true,
  "message": "Operation completed successfully.",
  "data": { }
}
```

`data` is absent on operations that return nothing. `message` is an English operator-facing
string; **do not show it to users** — it is not localised. Localised text for users comes from
the resources themselves (notification titles, catalogue names) and, for failures, from
`detail`.

Null properties are **omitted**, not sent as `null`. A student's payload simply has no
`teacherId` key. Write your types accordingly.

Enums serialise as **strings** (`"Active"`, not `2`). Property names are **camelCase**.

### Failure

RFC 7807 `application/problem+json`. See [§2](#2-errors-and-error-codes).

---

## 2. Errors and error codes

```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc7807",
  "title": "Business rule violation.",
  "status": 422,
  "detail": "الواجب ده اتقفل ومش بيقبل تسليم تاني.",
  "instance": "/api/v1/student/homework/8f3a.../submit",
  "code": "HOMEWORK_CLOSED",
  "success": false,
  "traceId": "0HN7G2K9QJ4R1:00000003",
  "language": "ar-EG"
}
```

| Field | Use it for |
|---|---|
| `code` | **Branching.** Stable, never translated, always present. |
| `detail` | **Displaying.** Localised to the request's language. |
| `errors` | Field-level messages on a 400: `{ "FieldName": ["message"] }`. Keys are the DTO property names in PascalCase. |
| `title` | Logs. English, stable per status. |
| `traceId` | Support tickets — it correlates to the server log. |
| `language` | Which language `detail` is in. |

**Never match on `detail`.** It is localised and will change.

### Status codes

| Status | When |
|---|---|
| 200 | Read, or an update that returns the new state |
| 201 | Created |
| 204 | Deleted, nothing to return |
| 400 | Validation or model binding failed — `errors` is populated |
| 401 | Not authenticated, token invalid, or token expired |
| 403 | Authenticated but not permitted |
| 404 | Not found **— also used for a resource in another school**, so its existence is not confirmed |
| 409 | Conflict, including unique-constraint violations and concurrency |
| 413 | Body too large |
| 422 | A business rule refused the operation — `code` tells you which |
| 429 | Rate limited |
| 500 | Unexpected. `traceId` is all support needs |

### The codes worth handling

Full table in [`frontend-api-changes.md` §1](frontend-api-changes.md#codes-you-should-handle-explicitly).
The essentials:

`VALIDATION_FAILED` · `INVALID_CREDENTIALS` · `ACCOUNT_LOCKED` · `ACCOUNT_INACTIVE` ·
`UNAUTHORIZED` · `TOKEN_EXPIRED` · `REFRESH_TOKEN_INVALID` · `FORBIDDEN` ·
`RESOURCE_NOT_FOUND` · `CONFLICT` · `DUPLICATE_VALUE` · `CONCURRENCY_CONFLICT` ·
`RATE_LIMITED` · `PAYLOAD_TOO_LARGE` · `INTERNAL_ERROR`

Roughly 115 further codes are domain-specific business rules on 422 (`HOMEWORK_CLOSED`,
`EXAM_NOT_OPEN`, `GROUP_FULL`, `PLAN_LIMIT_REACHED`, `TENANT_SUSPENDED`, …). You do not need to
enumerate them: show `detail`, and special-case only the ones your UX actually reacts to. The
authoritative list is `src/Ustaz.Application/Common/Localization/ErrorCatalog.cs`.

---

## 3. Language — Arabic is the default

Arabic is USTAZ's first language, not a translation. **A request that says nothing about
language is answered in Arabic.**

Resolution order, first match wins:

| Priority | Source | Example |
|---|---|---|
| 1 | `X-Ustaz-Language` header | `X-Ustaz-Language: en` |
| 2 | `lang` query parameter | `?lang=en` |
| 3 | `Accept-Language` header | `Accept-Language: en-US` |
| 4 | **Default** | Arabic (`ar-EG`) |

Only a value starting with `en` selects English. Everything else is Arabic.

```ts
// Set it once from the signed-in user's stored preference.
api.defaults.headers.common["X-Ustaz-Language"] = user.preferredLanguage ?? "ar-EG";
```

The user's own preference is stored server-side and returned by `GET /api/v1/auth/me` as
`preferredLanguage` (`"ar-EG"` or `"en"`). Change it with `PUT /api/v1/auth/me/language`:

```json
{ "language": "en" }
```

That field drives **emails and notification text** server-side. It does **not** change API error
language on its own — the API reads the request headers, so keep sending the header. This is
deliberate: resolving the stored preference per request would mean a database lookup in front of
every call, including anonymous ones, just to word an error.

### What is and is not translated

| Localised | Not localised |
|---|---|
| `detail` on errors | `code` |
| `errors[*]` messages | `errors` **keys** (field names) |
| Notification titles and bodies | Routes, query parameters, JSON property names |
| Email subjects and bodies | Enum values (`"Active"`, `"Pending"`) |
| | `message` on success envelopes |

Everything is UTF-8. Arabic content, including Egyptian-dialect notification copy, round-trips
unchanged — the test suite registers users with Arabic names and asserts them back.

---

## 4. Authentication and token refresh

JWT bearer tokens. Access tokens are short-lived; refresh tokens rotate.

### Register

| Route | Body |
|---|---|
| `POST /api/v1/auth/register/student` | `firstName`, `lastName`, `email`, `password`, `phoneNumber`, `city?` |
| `POST /api/v1/auth/register/teacher` | the above plus `subjectIds[]` and `gradeIds[]` (**both required, non-empty**), `headline?`, `bio?`, `yearsOfExperience?`, `whatsAppNumber?`, `preferredTeachingMode?`, `workspaceName?` |
| `POST /api/v1/auth/register/parent` | see the Authentication section of the inventory |

All three return **201** with the same payload as login.

> Registering a teacher also provisions their **school (workspace)** and makes them its owner.
> The response's `user.tenantId` is that school.

Password rules, enforced server-side: at least 8 characters, at most 128, with an uppercase
letter, a lowercase letter and a digit. Phone numbers must be valid Egyptian mobile numbers
(`01012345678`).

### Sign in

`POST /api/v1/auth/login` → `{ "email", "password" }`

```json
{
  "success": true,
  "data": {
    "accessToken": "eyJhbGciOi...",
    "refreshToken": "9f2c4e...",
    "accessTokenExpiresAt": "2026-10-02T19:14:22Z",
    "user": {
      "userId": "…", "displayName": "سلمى إبراهيم", "email": "…",
      "roles": ["Student"], "studentId": "…",
      "preferredLanguage": "ar-EG", "isVerified": false,
      "profileImageUrl": null
    }
  }
}
```

`user` carries only the ids relevant to the holder's roles: `studentId`, `teacherId`,
`parentId`, `tenantId`, `tenantSlug`, `tenantRole`. **Absent keys mean "not applicable"** —
a student's payload has no `teacherId` at all.

Send the token as `Authorization: Bearer <accessToken>`.

### Refresh

`POST /api/v1/auth/refresh-token` → `{ "refreshToken": "…" }` returns a **new pair**.

Three behaviours to build around:

1. **Refresh tokens rotate.** The old one is consumed. Always store the new one.
2. **Reuse is treated as a breach.** Replaying a consumed refresh token revokes the **entire
   token family** — both the replayed token and the one that replaced it. Recovery is a full
   sign-in. So never fire two refreshes concurrently; serialise them behind a single promise.
3. An expired refresh token returns `401` with `code: "TOKEN_EXPIRED"`; an invalid or already-used
   one returns `401` with `code: "REFRESH_TOKEN_INVALID"`.

```ts
let inFlight: Promise<Tokens> | null = null;

function refresh() {
  // One refresh at a time. Concurrent refreshes burn the family.
  inFlight ??= doRefresh().finally(() => { inFlight = null; });
  return inFlight;
}
```

An expired **access** token returns `401` with `code: "TOKEN_EXPIRED"` and the header
`x-token-expired: true`. That is your cue to refresh once and retry.

### Sign out

`POST /api/v1/auth/logout` → `{ "refreshToken": "…" }` revokes it server-side. Call it; dropping
the token client-side alone leaves it valid.

### The rest of the account surface

`POST /auth/verify-email` · `POST /auth/resend-verification` · `POST /auth/forgot-password` ·
`POST /auth/reset-password` · `POST /auth/change-password` · `GET /auth/me` ·
`PUT /auth/me/language`

`forgot-password` always returns success whether or not the address exists — it is not an
account-existence oracle. The same is true of login: a wrong password against a registered
address and against an unregistered one return byte-identical responses.

---

## 5. Roles and access boundaries

Four platform roles, from `RoleNames`:

| Role | Who | Reaches |
|---|---|---|
| `Student` | A learner | `/student/*`, `/enrollments/*`, `/student/favorites/*`, `/student/certificates` |
| `Teacher` | A teacher, who also owns a school | `/teacher/*`, `/tenant/*` |
| `Parent` | A guardian of one or more students | `/parent/*` |
| `Admin` | **USTAZ platform staff** | `/admin/*`, `/platform/*` |

### The distinction that matters most

| Concept | Representation | Scope |
|---|---|---|
| **Global platform admin** | the `Admin` identity role | all of USTAZ |
| **School admin** | `TenantMemberRole.Owner` / `Admin` — a membership row in one school | that one school |

**A school admin has no platform authority.** They are refused `/api/v1/admin/*` and
`/api/v1/platform/*`, and they cannot suspend or activate a school — not even their own. This is
enforced on the server for every route and is covered by tests that walk 18 platform-only routes
with a school owner's token.

Detect which you have from `GET /api/v1/auth/me`: a platform admin has `"Admin"` in `roles` and
**no** `tenantId`; a school admin has `"Teacher"` and a `tenantId`.

### Within a school: granular permissions

A school owner can invite assistants and grant individual capabilities —
`Students.Manage`, `Attendance.Manage`, `Exams.Manage`, `Grades.Manage`, `Homework.Manage`,
`Announcements.Manage`, `Messages.Manage`, `Payments.Manage`, `Analytics.View`,
`Website.Manage`, `Discounts.Manage`, `Notifications.Manage`, plus the owner-only
`Tenant.Manage`, `Team.Manage` and `Billing.Manage`.

Get the catalogue from `GET /api/v1/tenant/assistants/permissions/catalog`.

**Permissions are always resolved server-side from the database**, never from the token. A
permission claim in a token is not trusted. Use the catalogue to render UI; expect a `403` with
`code: "FORBIDDEN"` to be the real answer.

### Verified teachers

Publishing groups and courses requires a **verified** teacher
(`code: "TEACHER_NOT_VERIFIED"`). The flow is
`GET /api/v1/teacher/profile/verification` → `POST /api/v1/teacher/profile/verification`.
Verification needs a headline, a bio, a WhatsApp number and at least one subject and grade
(`code: "VERIFICATION_PROFILE_INCOMPLETE"`).

---

## 6. Multi-tenancy: the school header

A teacher may belong to more than one school. To act inside a specific one:

```http
X-Tenant-Id: 3f9a1c20-....
```

**It is only a hint.** Membership is re-resolved from the database on every request. Naming a
school you do not belong to gets you `403` or `404` — never that school's data. When the header
is absent, the `tenantId` claim in your token is used.

A suspended school returns `403` with `code: "TENANT_SUSPENDED"`; an archived one
`TENANT_ARCHIVED`.

Cross-school access returns **404, not 403**, so the response does not confirm that the other
school's resource exists.

### Public school pages

School websites are public and resolved by slug, with no authentication:
`GET /api/v1/public/tenants/{slug}` and `/branding`, `/groups`, `/courses`, `/teachers`, `/seo`.

---

## 7. Pagination, filtering, sorting, search

Every list endpoint takes the same parameters:

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `page` | int | 1 | 1-based. Values below 1 are clamped to 1 |
| `pageSize` | int | 12 | **Clamped to a maximum of 100.** Asking for 1000 silently gives 100 |
| `search` | string | — | Free text; what it matches depends on the endpoint |
| `sort` | string | — | e.g. `rating`, `price_asc`, `newest` |

```json
{
  "success": true,
  "data": {
    "items": [ ],
    "page": 1,
    "pageSize": 12,
    "totalCount": 1270,
    "totalPages": 106,
    "hasPreviousPage": false,
    "hasNextPage": true
  }
}
```

Some endpoints add their own filters — `/platform/users` takes `role`, `isActive`, `schoolId`,
`sortBy`, `sortDescending`; analytics endpoints take `period` (`Today`, `Last7Days`,
`Last30Days`, `Last90Days`, `Last12Months`, `AllTime`, `Custom`) with `from`/`to` required when
`period=Custom`. A custom range may not exceed the documented maximum and a reversed range is
refused with a field error rather than silently clamped. The per-endpoint parameters are all in
[`openapi-v1.json`](openapi-v1.json).

---

## 8. File uploads, images and private downloads

USTAZ stores every file on **its own server**. There is no third-party image host — the
Cloudinary integration was removed, and the `Cloudinary__*` configuration no longer exists.

### Public images

Avatars, cover images, school logos and course thumbnails are served from `/uploads/...`:

```
/uploads/users_profile/ffd4d9ba7f8e4a14a3af5c22a791e3d2.png
```

The filename is **generated**; the name you upload is never part of the path. Replacing an image
produces a **new URL**, so these are safe to cache hard — the server sends
`Cache-Control: public, max-age=604800, immutable` along with `X-Content-Type-Options: nosniff`
and `Content-Disposition: inline`.

URLs are returned **application-relative**. Prefix them with your API origin.

### Uploading

`multipart/form-data` with the field named **`file`**. Do not set `Content-Type` yourself; let
the browser add the boundary.

| Endpoint | What |
|---|---|
| `POST /api/v1/me/profile-picture` | **Any role.** The caller's own avatar |
| `GET  /api/v1/me/profile-picture` | Current URL, plus `hasPicture` |
| `DELETE /api/v1/me/profile-picture` | Back to the default avatar. Idempotent |
| `POST /api/v1/teacher/profile/image` | A teacher's avatar (kept in step with `/me`) |
| `POST /api/v1/teacher/profile/cover` | A teacher's public-page cover |
| `POST /api/v1/tenant/logo`, `/tenant/cover` | School branding |
| `POST /api/v1/student/homework/{id}/attachment` | A homework answer — **private** |
| `POST /api/v1/messages/conversations/{id}/attachments` | A message attachment — **private** |

### Limits and validation

| Rule | Images | Documents |
|---|---|---|
| Max size | 5 MB | 20 MB |
| Extensions | `.jpg` `.jpeg` `.png` `.webp` `.gif` | `.pdf` `.doc` `.docx` `.ppt` `.pptx` `.xls` `.xlsx` `.txt` `.csv` `.zip` and the image types |
| `Content-Type` | must start with `image/` | must be on the allow list |
| **Content** | **verified against the extension** | verified for PDF; see below |

**The content check will reject files that look fine by name.** The first bytes of every upload
are read and matched against its extension for JPEG, PNG, GIF, WEBP, BMP and PDF. Renaming a PDF
to `avatar.png` is refused with `422` and `code: "FILE_SIGNATURE_MISMATCH"`. If your uploader
crops or re-encodes client-side, make sure it emits a real image of the claimed type.

Office documents and archives are **not** content-verified — `.docx` and `.zip` share a
container, so the bytes prove nothing. They are allow-listed by extension and content type only.

Other upload failures: `FILE_REQUIRED`, `FILE_EMPTY`, `FILE_TOO_LARGE`,
`FILE_EXTENSION_NOT_ALLOWED`, `FILE_TYPE_NOT_ALLOWED`, `FILE_CONTENT_TYPE_NOT_ALLOWED`.

Uploads are rate limited to **20 per 5 minutes**.

### Private documents

Homework attachments, message attachments and course materials are **not** reachable by a static
URL. They live outside anything the static file server is pointed at and come back only through
an authorised endpoint that re-checks your enrollment, role and school:

```
GET /api/v1/messages/attachments/{attachmentId}
```

An attachment that is not yours returns **404**, not 403 — the response does not confirm it
exists. Never try to construct a path to a private file; always use the endpoint.

---

## 9. Notifications and real time

### Over HTTP

| Route | Purpose |
|---|---|
| `GET /api/v1/notifications` | Paged inbox. Supports `page`, `pageSize` and an importance filter |
| `GET /api/v1/notifications/unread-count` | `{ "count": 3 }` — cheap, poll this |
| `GET /api/v1/notifications/{id}` | One notification |
| `PUT /api/v1/notifications/{id}/read` | Mark one read |
| `PUT /api/v1/notifications/read-all` | Mark all read |
| `DELETE /api/v1/notifications/{id}` | Delete one |
| `DELETE /api/v1/notifications/read` | Clear everything already read |
| `GET /api/v1/notifications/preferences` | Per-category switches |
| `PUT /api/v1/notifications/preferences` | Update them |

Each notification carries **both languages** — `titleAr`/`bodyAr` and `titleEn`/`bodyEn` — plus
`type`, `category`, `priority`, `actionUrl`, `isRead` and `createdAt`. Pick the pair that matches
your UI language rather than re-requesting.

`actionUrl` is a **client route** (`/settings/security`, `/homework/{id}`), not an API path.

### Preferences

Categories include `Security`, `Course`, `Certificate`, `Enrollment`, `Homework`, `Exam`,
`Attendance`, `Announcement`, `Message`, `Payment`. A user can silence a category — **except
that `Critical` priority notifications are delivered regardless.** Security events such as a
password change always arrive.

### Real time (SignalR)

```
/hubs/notifications
```

A browser WebSocket cannot set an `Authorization` header, so the hub accepts the token in the
query string — and **only** for the hub path:

```ts
const connection = new signalR.HubConnectionBuilder()
  .withUrl(`${API_ORIGIN}/hubs/notifications`, { accessTokenFactory: () => accessToken })
  .withAutomaticReconnect()
  .build();
```

Build for it being unavailable. Real time can be switched off server-side
(`Realtime:Enabled=false`), in which case a no-op publisher takes over and **everything still
works over HTTP** — notifications are persisted either way. Treat the live connection as an
optimisation over polling `unread-count`, never as the source of truth.

> A delivered database row is not a delivered push. If the user is offline the notification is
> stored and, for the categories that warrant it, emailed. It will be in the inbox when they
> return.

---

## 10. Rate limits

| Endpoints | Budget |
|---|---|
| Credentials: `login`, `register/*`, `refresh-token`, `forgot-password`, `reset-password`, `verify-email`, `resend-verification`, `change-password`, `logout` | **10 / minute** |
| Uploads | **20 / 5 minutes** |
| Everything else | 300 / minute |

Exceeding one gives `429` with `code: "RATE_LIMITED"` and a problem document.

Partitioned per authenticated user where there is one, by client IP otherwise — so anonymous
users behind a shared NAT share a budget.

**Do not auto-retry a failed login.** Ten a minute is generous for a person and immediately
exhausted by a loop.

---

## 11. Health checks

| Route | Includes | Use |
|---|---|---|
| `GET /health` | API + database | Overall |
| `GET /health/live` | API only | Liveness — does not touch the database |
| `GET /health/ready` | Database | Readiness |

All three are anonymous and return `200` or `503`:

```json
{ "status": "Healthy", "checks": [ { "name": "api", "status": "Healthy" }, { "name": "database", "status": "Healthy" } ] }
```

Deliberately terse — no durations, no exception text, no infrastructure detail.

---

## 12. Local development

### Running the API

```bash
cd src/Ustaz.API
dotnet run
```

Development uses `appsettings.Development.json`, which points at a local SQL Server, applies
migrations on startup and seeds demo data. Swagger UI is at `/swagger`.

### Environment variables

Names only — **never commit a value**. Double underscore separates section from key.

| Variable | Needed | Purpose |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | **Yes** | SQL Server connection string. The app **refuses to start without it** and names it in the diagnostic |
| `Jwt__Key` | Production | Signing key, ≥32 characters. Generated and persisted if absent — set it explicitly |
| `Jwt__Issuer`, `Jwt__Audience` | Production | Token issuer and audience |
| `Cors__AllowedOrigins__0` | **Yes, for a browser client** | Your web origin. **Empty means no cross-origin request is allowed** and the client is blocked |
| `Storage__RootPath` | Production | Persistent directory for uploads |
| `Email__Password` | If sending email | SMTP password. Without it messages are written to disk instead |
| `Seed__AdminEmail`, `Seed__AdminPassword` | First deploy | Provisions the first platform administrator |
| `ASPNETCORE_ENVIRONMENT` | Yes | `Development`, `Production`, `Testing` |

`.env.example` lists the full set. Full deployment detail is in
[`production-deployment.md`](production-deployment.md).

### CORS — the one that catches everyone

`Cors__AllowedOrigins` is an **array** and defaults to empty. An empty allow-list means the API
adds no CORS headers at all and every browser cross-origin request fails. Set
`Cors__AllowedOrigins__0=http://localhost:5173` locally.

`AllowAnyOrigin` is never combined with credentials, so the origin must be listed exactly.
`x-token-expired` and `x-pagination` are exposed to the client.

### Swagger

Enabled automatically in Development, and outside it only when `Swagger__Enabled=true`.
`GET /` redirects to `/swagger`.

---

## 13. Complete endpoint inventory

Generated from [`openapi-v1.json`](openapi-v1.json), exported from a running instance on
2 October 2026. **346 operations, 288 routes, 34 groups.** Request and response schemas for every
one are in the spec; load it into Postman, Insomnia or an OpenAPI client generator rather than
transcribing by hand.

Authorization, in the absence of a note below: routes under `/student` require `Student`,
`/teacher` and `/tenant` require `Teacher` or workspace membership, `/parent` requires `Parent`,
`/admin` and `/platform` require the platform `Admin` role, `/public/*` and the catalogue routes
(`/teachers`, `/groups`, `/courses`, `/subjects`, `/grades`, `/schedule`, `/search`) are
anonymous.

<!-- BEGIN GENERATED INVENTORY -->
### Account — 3 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/me/profile-picture` | The caller's current profile picture URL, or null when they have none. |
| POST | `/api/v1/me/profile-picture` | Uploads a profile picture, replacing any existing one. The previous file is removed only after the new one is commi... |
| DELETE | `/api/v1/me/profile-picture` | Removes the caller's profile picture and returns them to the default avatar. Idempotent. |

### Activity — 2 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/teacher/activity` | What has happened in the workspace, newest first. Filter by activity type, date range, or one student. Requires the... |
| GET | `/api/v1/teacher/activity/students/{studentId}` | One student's timeline as staff see it. The student must belong to this workspace; anyone else's id reads as not fo... |

### Admin — 26 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/admin/analytics` | Alias of the dashboard, kept for the analytics screen. |
| GET | `/api/v1/admin/analytics/revenue` | Monthly SaaS revenue: what tenants paid USTAZ. |
| GET | `/api/v1/admin/analytics/tenants` | Workspace growth and subscription mix. |
| GET | `/api/v1/admin/audit-logs` | The platform-wide audit trail. |
| GET | `/api/v1/admin/courses` | Lists every course, drafts included. |
| GET | `/api/v1/admin/dashboard` | Platform-wide counters, revenue trend and the newest teachers. |
| GET | `/api/v1/admin/discounts` | Lists discount codes across the platform. |
| GET | `/api/v1/admin/enrollments` | Lists group enrollments across the platform. |
| GET | `/api/v1/admin/groups` | Lists every group, drafts included. |
| GET | `/api/v1/admin/inquiries` | Lists inquiries across the platform. |
| GET | `/api/v1/admin/plans` | All subscription plans, inactive ones included. |
| GET | `/api/v1/admin/reports` | Monthly platform revenue, suitable for export. |
| GET | `/api/v1/admin/reviews` | Lists reviews, unapproved ones included, for moderation. |
| POST | `/api/v1/admin/reviews/{id}/moderate` | Approves or hides a review, then recalculates the affected ratings. |
| GET | `/api/v1/admin/students` | Lists student accounts. |
| GET | `/api/v1/admin/teachers` | Lists teacher accounts, including unverified and suspended ones. |
| GET | `/api/v1/admin/teachers/{id}` | Gets any teacher's profile, verified or not. |
| POST | `/api/v1/admin/teachers/{id}/activate` | Reactivates a suspended teacher account. |
| POST | `/api/v1/admin/teachers/{id}/begin-review` | Moves a pending verification request into review. |
| POST | `/api/v1/admin/teachers/{id}/feature` | Features or unfeatures a verified teacher on the home page. |
| POST | `/api/v1/admin/teachers/{id}/suspend` | Suspends a teacher account and unpublishes their groups. |
| POST | `/api/v1/admin/teachers/{id}/verify` | Approves or rejects a teacher's verification request. |
| GET | `/api/v1/admin/tenants` | Every workspace on the platform, with its plan and lifetime revenue. |
| POST | `/api/v1/admin/tenants/{id}/activate` | Reactivates a suspended workspace. |
| POST | `/api/v1/admin/tenants/{id}/suspend` | Suspends a workspace. Its members immediately lose access. |
| POST | `/api/v1/admin/users/{userId}/state` | Suspends or reactivates any account by its user id. |

### Announcements — 7 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/student/announcements` | Announcements addressed to the student. |
| GET | `/api/v1/teacher/announcements` | Announcements in the workspace. |
| POST | `/api/v1/teacher/announcements` | Creates an announcement as a draft. |
| GET | `/api/v1/teacher/announcements/{id}` | One announcement. |
| PUT | `/api/v1/teacher/announcements/{id}` | Updates an announcement. |
| DELETE | `/api/v1/teacher/announcements/{id}` | Deletes an announcement. |
| POST | `/api/v1/teacher/announcements/{id}/publish` | Publishes an announcement and notifies its audience. |

### Assistants — 9 operations

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/v1/assistants/invitations/accept` | Accepts an assistant invitation and applies the granted permissions. |
| GET | `/api/v1/tenant/assistants` | Assistants in the caller's workspace. |
| POST | `/api/v1/tenant/assistants/invite` | Invites an assistant with an initial permission set. Assistants start with no capabilities, so each one must be gra... |
| GET | `/api/v1/tenant/assistants/permissions/catalog` | The permissions an assistant may be granted, grouped for the editor. |
| GET | `/api/v1/tenant/assistants/{id}` | One assistant, with their effective permissions. |
| PUT | `/api/v1/tenant/assistants/{id}` | Updates an assistant's title or role. |
| DELETE | `/api/v1/tenant/assistants/{id}` | Removes an assistant from the workspace. |
| PUT | `/api/v1/tenant/assistants/{id}/permissions` | Replaces an assistant's permissions. Unknown permissions and owner-only capabilities are rejected, and nobody can e... |
| PUT | `/api/v1/tenant/assistants/{id}/status` | Activates or deactivates an assistant. |

### Attendance — 11 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/student/attendance` | The student's own attendance history. |
| GET | `/api/v1/teacher/attendance/group/{groupId}` | Attendance records for one group. |
| PUT | `/api/v1/teacher/attendance/records/{id}` | Corrects a single attendance row. |
| GET | `/api/v1/teacher/attendance/sessions` | Attendance sessions, filterable by group, date range and status. |
| POST | `/api/v1/teacher/attendance/sessions` | Opens a register for one group on one date, optionally pre-filled as present. |
| GET | `/api/v1/teacher/attendance/sessions/{id}` | One session with all its student records. |
| PUT | `/api/v1/teacher/attendance/sessions/{id}` | Updates a session's details or closes it. |
| POST | `/api/v1/teacher/attendance/sessions/{id}/bulk` | Bulk entry point; identical semantics to the records endpoint. |
| POST | `/api/v1/teacher/attendance/sessions/{id}/records` | Records attendance for one or more students. Marking a student absent or late notifies them and their confirmed gua... |
| GET | `/api/v1/teacher/attendance/statistics` | Attendance rates for the workspace, a group, or one student. |
| GET | `/api/v1/teacher/attendance/student/{studentId}` | One student's attendance history in this workspace. |

### Authentication — 13 operations

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/v1/auth/change-password` | Changes the signed-in user's password and revokes every existing session. |
| POST | `/api/v1/auth/forgot-password` | Starts password recovery. Always succeeds, to avoid account enumeration. |
| POST | `/api/v1/auth/login` | Signs in with email and password. |
| POST | `/api/v1/auth/logout` | Revokes the presented refresh token and its whole rotation family. |
| GET | `/api/v1/auth/me` | Returns the profile of the currently authenticated user. |
| PUT | `/api/v1/auth/me/language` | Sets the language the caller reads notifications and emails in. |
| POST | `/api/v1/auth/refresh-token` | Exchanges a refresh token for a new token pair (rotation with reuse detection). |
| POST | `/api/v1/auth/register/parent` | Creates a guardian account, optionally requesting access to one child. |
| POST | `/api/v1/auth/register/student` | Creates a student account and returns an access/refresh token pair. |
| POST | `/api/v1/auth/register/teacher` | Creates a teacher account. The profile starts unverified and cannot publish yet. |
| POST | `/api/v1/auth/resend-verification` | Re-sends the verification email. Always succeeds, to avoid account enumeration. |
| POST | `/api/v1/auth/reset-password` | Completes password recovery and revokes every existing session. |
| POST | `/api/v1/auth/verify-email` | Confirms an email address using the token from the verification email. |

### Billing — 12 operations

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/v1/billing/cancel` | Cancels the subscription, by default at the end of the paid period. |
| POST | `/api/v1/billing/change-plan` | Switches plan. A downgrade below current usage is refused. |
| POST | `/api/v1/billing/checkout` | Starts a payment: issues an invoice and a pending transaction. The subscription is NOT activated here; only a verif... |
| GET | `/api/v1/billing/invoices` | The workspace's SaaS invoices. |
| GET | `/api/v1/billing/invoices/{id}` | One SaaS invoice. |
| GET | `/api/v1/billing/plans` | The public price list. |
| GET | `/api/v1/billing/plans/{id}` | One plan, with its quotas and features. |
| POST | `/api/v1/billing/reactivate` | Reactivates a cancelled subscription. |
| GET | `/api/v1/billing/subscription` | The caller's subscription, with live quota usage. |
| GET | `/api/v1/billing/transactions` | The workspace's SaaS payment attempts. |
| GET | `/api/v1/billing/transactions/{id}` | One SaaS transaction. |
| POST | `/api/v1/billing/webhooks/{provider}` | Payment provider callback. The signature is verified before anything is written, and the (provider, eventId) pair i... |

### Calendar — 1 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/calendar` | Classes, exams, homework deadlines and held sessions between two dates, ordered by start time. Both dates are inclu... |

### Certificates — 6 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/public/certificates/verify/{code}` | Verifies a certificate by its verification code. |
| GET | `/api/v1/student/certificates` | Certificates the student has earned. |
| GET | `/api/v1/student/certificates/{id}` | One certificate, including the code to hand to a verifier. |
| GET | `/api/v1/teacher/certificates` | Every certificate the workspace has issued. |
| GET | `/api/v1/teacher/certificates/{id}` | One certificate issued by this workspace. |
| POST | `/api/v1/teacher/certificates/{id}/revoke` | Withdraws a certificate. Verification then reports it as invalid. |

### Courses — 20 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/courses` | Searches published courses. Filters: subjectId, gradeId, teacherId, minPrice, maxPrice, isFree, featuredOnly, minRa... |
| GET | `/api/v1/courses/{id}` | Gets a full course page. Video URLs are only included for preview lessons unless the caller is enrolled, owns the c... |
| GET | `/api/v1/courses/{id}/reviews` | Approved reviews for this course. |
| GET | `/api/v1/courses/{id}/sections` | The course outline: sections and their lessons. |
| POST | `/api/v1/student/courses/{courseId}/lessons/{lessonId}/complete` | Marks one lesson as completed and recomputes the course percentage. |
| DELETE | `/api/v1/student/courses/{courseId}/lessons/{lessonId}/complete` | Reopens a completed lesson and recomputes the course percentage. |
| GET | `/api/v1/student/courses/{courseId}/progress` | Lesson-by-lesson progress through a course the student is enrolled in. |
| GET | `/api/v1/teacher/courses` | Lists the calling teacher's courses, drafts included. |
| POST | `/api/v1/teacher/courses` | Creates a course as a draft. |
| GET | `/api/v1/teacher/courses/{id}` | Gets one of the calling teacher's courses. |
| PUT | `/api/v1/teacher/courses/{id}` | Updates a course the caller owns. |
| DELETE | `/api/v1/teacher/courses/{id}` | Deletes a course, or unpublishes it when students already enrolled. |
| POST | `/api/v1/teacher/courses/{id}/lessons` | Adds a lesson to one of the course's sections. |
| PUT | `/api/v1/teacher/courses/{id}/lessons/{lessonId}` | Updates a lesson. |
| DELETE | `/api/v1/teacher/courses/{id}/lessons/{lessonId}` | Deletes a lesson. |
| POST | `/api/v1/teacher/courses/{id}/publish` | Publishes the course. Requires a verified teacher, a description and content. |
| POST | `/api/v1/teacher/courses/{id}/sections` | Adds a section (chapter) to the course. |
| PUT | `/api/v1/teacher/courses/{id}/sections/{sectionId}` | Updates a section. |
| DELETE | `/api/v1/teacher/courses/{id}/sections/{sectionId}` | Deletes a section and its lessons. |
| POST | `/api/v1/teacher/courses/{id}/unpublish` | Hides the course from the public catalogue. |

### Dashboard — 17 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/student/analytics` | The student's own academic standing: averages, attendance, completion, learning streak and their strongest and weak... |
| GET | `/api/v1/student/dashboard` | Everything the student home screen renders: groups, courses, classes, saves, notifications. |
| GET | `/api/v1/student/progress` | Per-course learning progress. |
| GET | `/api/v1/student/recent-activity` | Recent enrollments, reviews and other activity. |
| GET | `/api/v1/student/upcoming-classes` | The next classes from the student's active group enrollments. |
| GET | `/api/v1/teacher/analytics` | Full analytics: revenue trend, student growth and per-group performance. |
| GET | `/api/v1/teacher/analytics/at-risk` | Students who need attention, worst first, with the numbers behind every flag. The scoring is deterministic: attenda... |
| GET | `/api/v1/teacher/analytics/at-risk/{studentId}` | One student's standing, with the same explainable breakdown. |
| GET | `/api/v1/teacher/analytics/attendance` | Attendance rates for the workspace, broken down by group. |
| GET | `/api/v1/teacher/analytics/courses` | Course performance across the workspace. |
| GET | `/api/v1/teacher/analytics/grades` | Exam performance across the workspace. |
| GET | `/api/v1/teacher/analytics/groups` | Fill rate, revenue and rating per group. |
| GET | `/api/v1/teacher/analytics/overview` | The workspace KPI strip for a date window, with the preceding window of equal length alongside each count. Use `per... |
| GET | `/api/v1/teacher/analytics/revenue` | Monthly revenue split between groups and courses. |
| GET | `/api/v1/teacher/analytics/students` | Monthly new and cumulative student counts. |
| GET | `/api/v1/teacher/analytics/students/{studentId}` | One student's academic standing. The student must belong to this workspace; anyone else's id reads as not found. |
| GET | `/api/v1/teacher/dashboard` | Headline numbers, upcoming classes, top groups and recent activity. |

### Discounts — 8 operations

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/v1/discounts/validate` | Validates a promo code against a group, a course, or an explicit teacher/amount pair. Never increments the usage co... |
| GET | `/api/v1/teacher/discounts` | Lists the calling teacher's promo codes. |
| POST | `/api/v1/teacher/discounts` | Creates a promo code. Codes are unique per teacher and stored upper-cased. |
| GET | `/api/v1/teacher/discounts/{id}` | Gets one of the calling teacher's promo codes. |
| PUT | `/api/v1/teacher/discounts/{id}` | Updates a promo code the caller owns. |
| DELETE | `/api/v1/teacher/discounts/{id}` | Deletes a promo code, or deactivates it when it has already been redeemed. |
| POST | `/api/v1/teacher/discounts/{id}/activate` | Turns a promo code on. |
| POST | `/api/v1/teacher/discounts/{id}/deactivate` | Turns a promo code off. |

### Enrollments — 12 operations

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/v1/enrollments/courses` | Enrolls the calling student in a recorded course. |
| GET | `/api/v1/enrollments/courses/{id}` | Gets one course enrollment (student owner, owning teacher or admin). |
| POST | `/api/v1/enrollments/courses/{id}/cancel` | Cancels a course enrollment. |
| PUT | `/api/v1/enrollments/courses/{id}/progress` | Updates the calling student's watch progress for a course. |
| POST | `/api/v1/enrollments/groups` | Subscribes the calling student to a group. Capacity, publication state and duplicate enrollment are all validated, ... |
| GET | `/api/v1/enrollments/groups/{id}` | Gets one group enrollment (student owner, owning teacher or admin). |
| POST | `/api/v1/enrollments/groups/{id}/cancel` | Cancels a group enrollment and frees the seat. |
| GET | `/api/v1/student/enrollments/courses` | The student's course enrollments. |
| GET | `/api/v1/student/enrollments/groups` | The student's group subscriptions. |
| GET | `/api/v1/teacher/enrollments/courses` | Every student enrolled in the calling teacher's courses. |
| GET | `/api/v1/teacher/enrollments/groups` | Every student enrolled in the calling teacher's groups. |
| POST | `/api/v1/teacher/groups/{groupId}/students` | Enrolls a student into the group on their behalf. |

### Exams — 26 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/student/exams` | Published exams for the student's groups. |
| GET | `/api/v1/student/exams/attempts/{attemptId}` | One of the student's own sittings. Marks appear only once released. |
| POST | `/api/v1/student/exams/attempts/{attemptId}/submit` | Submits the answers for a sitting and grades everything that grades itself. |
| GET | `/api/v1/student/exams/{id}` | One exam the student can see. |
| GET | `/api/v1/student/exams/{id}/attempts` | The student's own sittings of one exam. |
| POST | `/api/v1/student/exams/{id}/attempts` | Starts an online exam, or resumes the sitting already in progress. |
| GET | `/api/v1/student/grades` | The student's released grades, with per-subject averages. |
| GET | `/api/v1/teacher/exams` | Exams in the workspace, filterable by group, subject, type and date. |
| POST | `/api/v1/teacher/exams` | Creates an exam as a draft. |
| GET | `/api/v1/teacher/exams/attempts/{attemptId}` | One sitting, with every answer and its marks. |
| PUT | `/api/v1/teacher/exams/attempts/{attemptId}/grade` | Marks the essay answers of a submitted sitting and finalises it. |
| GET | `/api/v1/teacher/exams/students/{studentId}/grades` | A teacher's view of one student's grades across the workspace. |
| GET | `/api/v1/teacher/exams/{id}` | One exam, with score statistics. |
| PUT | `/api/v1/teacher/exams/{id}` | Updates an exam. Changing the scale recomputes every recorded percentage. |
| DELETE | `/api/v1/teacher/exams/{id}` | Deletes an exam that has no recorded results. |
| GET | `/api/v1/teacher/exams/{id}/analytics` | Full analytics for one exam: overview, cohort performance, score distribution, per-question difficulty, standings a... |
| GET | `/api/v1/teacher/exams/{id}/attempts` | Every sitting of one exam, newest first. |
| POST | `/api/v1/teacher/exams/{id}/publish` | Publishes the exam, and optionally releases its results to students. |
| GET | `/api/v1/teacher/exams/{id}/questions` | The question paper with its answer key. Workspace members only. |
| POST | `/api/v1/teacher/exams/{id}/questions` | Adds a question to an online exam that nobody has started yet. |
| PUT | `/api/v1/teacher/exams/{id}/questions/{questionId}` | Edits a question. Refused once a student has started the exam. |
| DELETE | `/api/v1/teacher/exams/{id}/questions/{questionId}` | Removes a question. Refused once a student has started the exam. |
| GET | `/api/v1/teacher/exams/{id}/results` | Results for one exam, highest first. |
| POST | `/api/v1/teacher/exams/{id}/results` | Records results. Scores below the workspace's configured threshold notify guardians. |
| POST | `/api/v1/teacher/exams/{id}/results/bulk` | Bulk entry point; identical semantics to the results endpoint. |
| PUT | `/api/v1/teacher/exams/{id}/results/{studentId}` | Corrects one student's result. |

### Favorites — 6 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/student/favorites/courses` | The student's saved courses. |
| POST | `/api/v1/student/favorites/courses/{courseId}` | Saves a course. |
| DELETE | `/api/v1/student/favorites/courses/{courseId}` | Removes a saved course. |
| GET | `/api/v1/student/favorites/teachers` | The student's saved teachers. |
| POST | `/api/v1/student/favorites/teachers/{teacherId}` | Saves a teacher. Saving twice is a no-op. |
| DELETE | `/api/v1/student/favorites/teachers/{teacherId}` | Removes a saved teacher. |

### Grades — 5 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/grades` | Lists every active educational grade. |
| GET | `/api/v1/grades/{idOrSlug}` | Gets one grade by id or slug. |
| GET | `/api/v1/grades/{id}/courses` | Published courses for this grade. |
| GET | `/api/v1/grades/{id}/groups` | Published groups for this grade. |
| GET | `/api/v1/grades/{id}/teachers` | Verified teachers who serve this grade. |

### Groups — 19 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/groups` | Searches published groups. Filters: subjectId, gradeId, teacherId, teachingMode, type, minPrice, maxPrice, hasDisco... |
| GET | `/api/v1/groups/{id}` | Gets a full group page, including teacher, subject, grade, schedule and curriculum. |
| GET | `/api/v1/groups/{id}/curriculum` | The syllabus the group covers. |
| GET | `/api/v1/groups/{id}/materials` | The group's materials. Restricted files are returned locked for non-enrolled callers. |
| GET | `/api/v1/groups/{id}/reviews` | Approved reviews for this group. |
| GET | `/api/v1/groups/{id}/schedule` | The group's weekly sessions. |
| GET | `/api/v1/teacher/groups` | Lists the calling teacher's groups, drafts included. |
| POST | `/api/v1/teacher/groups` | Creates a group as a draft. Publishing is a separate, validated step. |
| GET | `/api/v1/teacher/groups/{id}` | Gets one of the calling teacher's groups. |
| PUT | `/api/v1/teacher/groups/{id}` | Updates a group the caller owns. |
| DELETE | `/api/v1/teacher/groups/{id}` | Deletes a group, or archives it when it already has enrollment history. |
| POST | `/api/v1/teacher/groups/{id}/curriculum` | Adds a syllabus item. |
| PUT | `/api/v1/teacher/groups/{id}/curriculum/{curriculumId}` | Updates a syllabus item. |
| DELETE | `/api/v1/teacher/groups/{id}/curriculum/{curriculumId}` | Deletes a syllabus item. |
| POST | `/api/v1/teacher/groups/{id}/publish` | Publishes the group. Requires a verified teacher and at least one active session. |
| POST | `/api/v1/teacher/groups/{id}/schedule` | Adds a weekly session. Overlapping sessions are rejected. |
| PUT | `/api/v1/teacher/groups/{id}/schedule/{scheduleId}` | Updates one weekly session. |
| DELETE | `/api/v1/teacher/groups/{id}/schedule/{scheduleId}` | Deletes one weekly session. |
| POST | `/api/v1/teacher/groups/{id}/unpublish` | Hides the group from public discovery without deleting anything. |

### Homework — 18 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/student/homework` | Homework assigned to the student, with their submission status. |
| PUT | `/api/v1/student/homework/submissions/{id}` | Replaces a submission that has not been graded yet. |
| GET | `/api/v1/student/homework/{id}` | One homework assignment, including the student's own submission. |
| POST | `/api/v1/student/homework/{id}/attachment` | Attaches a file to the student's submission for one homework assignment. |
| POST | `/api/v1/student/homework/{id}/submit` | Submits homework. Late submissions are accepted only when the teacher allows them. |
| GET | `/api/v1/teacher/homework` | Homework in the workspace. |
| POST | `/api/v1/teacher/homework` | Creates a homework assignment as a draft. |
| GET | `/api/v1/teacher/homework/submissions/{submissionId}` | One submission. |
| PUT | `/api/v1/teacher/homework/submissions/{submissionId}/grade` | Grades a submission and notifies the student. |
| POST | `/api/v1/teacher/homework/submissions/{submissionId}/return` | Sends a submission back for revision with a reason. The student gets one more attempt whatever the assignment's res... |
| GET | `/api/v1/teacher/homework/{id}` | One homework assignment. |
| PUT | `/api/v1/teacher/homework/{id}` | Updates a homework assignment. |
| DELETE | `/api/v1/teacher/homework/{id}` | Deletes homework that has no submissions. |
| POST | `/api/v1/teacher/homework/{id}/close` | Closes the assignment. Nothing further is accepted, whatever the deadline and the late-submission setting say. Stud... |
| POST | `/api/v1/teacher/homework/{id}/publish` | Publishes the assignment and notifies the group's students. |
| POST | `/api/v1/teacher/homework/{id}/reopen` | Reopens a closed assignment so outstanding work can still be handed in. |
| GET | `/api/v1/teacher/homework/{id}/submissions` | Submissions for one assignment. |
| POST | `/api/v1/teacher/homework/{id}/submissions/grade` | Grades a batch of submissions for one assignment in a single transaction. Either every entry is applied or none is. |

### Inquiries — 6 operations

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/v1/inquiries` | Sends an inquiry to a teacher, optionally about a specific group or course. |
| GET | `/api/v1/inquiries/{id}` | Gets one inquiry (its sender, the receiving teacher, or an admin). |
| GET | `/api/v1/student/inquiries` | Inquiries the student has sent to teachers. |
| GET | `/api/v1/teacher/inquiries` | Lists inquiries addressed to the calling teacher, with ready-to-use WhatsApp links. |
| GET | `/api/v1/teacher/inquiries/{id}` | Gets one inquiry addressed to the calling teacher. |
| PUT | `/api/v1/teacher/inquiries/{id}/status` | Moves an inquiry through the New → Contacted → Converted/Closed pipeline. |

### Messages — 8 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/messages/attachments/{attachmentId}` | Streams one attachment. Only a participant of the conversation can read it, and the response is always a download r... |
| GET | `/api/v1/messages/conversations` | The caller's conversations, most recent first. |
| POST | `/api/v1/messages/conversations` | Starts a conversation. Every participant must belong to the workspace. |
| GET | `/api/v1/messages/conversations/{id}` | Messages in one conversation. Non-participants get a 404. |
| POST | `/api/v1/messages/conversations/{id}/attachments` | Posts a file into a conversation the caller participates in. |
| POST | `/api/v1/messages/conversations/{id}/messages` | Posts a message into a conversation. |
| PUT | `/api/v1/messages/messages/{id}/read` | Marks a message, and everything before it, as read. |
| GET | `/api/v1/messages/unread-count` | Unread message totals for the caller, for the client's badge. |

### Notifications — 9 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/notifications` | Lists the caller's notifications. |
| GET | `/api/v1/notifications/preferences` | The caller's per-category delivery preferences. Every category is returned, with its default applied, so the client... |
| PUT | `/api/v1/notifications/preferences` | Updates which channels each category is delivered on. Guardians use this to choose, for example, in-app plus email ... |
| DELETE | `/api/v1/notifications/read` | Clears the notifications the caller has already read. |
| PUT | `/api/v1/notifications/read-all` | Marks every notification as read. |
| GET | `/api/v1/notifications/unread-count` | Returns the unread badge count. |
| GET | `/api/v1/notifications/{id}` | One notification, for a client opening a deep link straight onto it. |
| DELETE | `/api/v1/notifications/{id}` | Deletes one notification. |
| PUT | `/api/v1/notifications/{id}/read` | Marks one notification as read. |

### Parents — 17 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/parent/announcements` | Announcements addressed to guardians of the caller's children. |
| GET | `/api/v1/parent/children` | The guardian's linked children. |
| POST | `/api/v1/parent/children/link` | Links a child by their account email. The link is confirmed automatically only when the supplied phone number match... |
| GET | `/api/v1/parent/children/{studentId}` | One child's overview. |
| DELETE | `/api/v1/parent/children/{studentId}` | Removes the link to a child. |
| GET | `/api/v1/parent/children/{studentId}/analytics` | One child's full academic standing: averages, attendance, completion, learning streak and their strongest and weake... |
| GET | `/api/v1/parent/children/{studentId}/attendance` | One child's attendance history. |
| GET | `/api/v1/parent/children/{studentId}/exams` | One child's exams. |
| GET | `/api/v1/parent/children/{studentId}/grades` | One child's released grades. |
| GET | `/api/v1/parent/children/{studentId}/homework` | One child's homework and its submission status. |
| GET | `/api/v1/parent/children/{studentId}/payments` | One child's tuition payments. |
| GET | `/api/v1/parent/children/{studentId}/progress` | One child's academic progress summary. |
| GET | `/api/v1/parent/dashboard` | Everything the guardian's home screen shows, across all their children. |
| GET | `/api/v1/parent/profile` | The calling guardian's profile. |
| PUT | `/api/v1/parent/profile` | Updates the guardian's profile. |
| GET | `/api/v1/student/guardians` | The guardians following this student, pending requests first. |
| POST | `/api/v1/student/guardians/{linkId}/respond` | Approves or rejects a guardian's request to follow this student. |

### Payments — 5 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/student/payments` | The student's tuition payments. |
| GET | `/api/v1/teacher/payments` | Tuition recorded in this workspace. |
| POST | `/api/v1/teacher/payments` | Records a tuition payment against a student. |
| POST | `/api/v1/teacher/payments/{id}/mark-paid` | Marks a pending tuition payment as settled. |
| POST | `/api/v1/teacher/payments/{id}/refund` | Refunds a settled tuition payment. |

### Platform — 5 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/platform/administrators` | Everyone who holds platform authority. Each row says whether it is the caller and whether it is the last remaining ... |
| POST | `/api/v1/platform/administrators/{userId}` | Grants platform-administrator authority to an existing, active account. Audited. |
| DELETE | `/api/v1/platform/administrators/{userId}` | Removes platform-administrator authority and closes that account's open sessions. Refused in two cases, both of whi... |
| GET | `/api/v1/platform/overview` | Schools, people and subscriptions in one call. Built for a dashboard that would otherwise need a dozen requests to ... |
| GET | `/api/v1/platform/users` | Searches every account on the platform, across all schools, with pagination. The only list in USTAZ that is not ten... |

### Reviews — 4 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/reviews` | Lists approved reviews, filtered by teacher, group or course. |
| POST | `/api/v1/reviews` | Writes a review. The student must have a non-cancelled enrollment with the target, and may review each target only ... |
| PUT | `/api/v1/reviews/{id}` | Edits the caller's own review. Ratings are recalculated automatically. |
| DELETE | `/api/v1/reviews/{id}` | Deletes the caller's own review (admins may delete any). |

### Schedule — 8 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/schedule` | The published weekly timetable across the platform. Filters: teacherId, subjectId, gradeId, dayOfWeek, teachingMode. |
| GET | `/api/v1/schedule/upcoming` | The next sessions, ordered by their next concrete occurrence. |
| GET | `/api/v1/student/schedule` | The student's full weekly timetable. |
| GET | `/api/v1/student/schedule/upcoming` | The student's next classes. |
| GET | `/api/v1/teacher/schedule` | The calling teacher's full weekly timetable, drafts included. |
| POST | `/api/v1/teacher/schedule` | Adds a weekly session to one of the calling teacher's groups. |
| PUT | `/api/v1/teacher/schedule/{id}` | Updates one weekly session. |
| DELETE | `/api/v1/teacher/schedule/{id}` | Deletes one weekly session. |

### Search — 1 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/search` | Searches teachers, groups, courses and subjects at once. |

### Students — 2 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/student/profile` | The student's own profile. |
| PUT | `/api/v1/student/profile` | Updates the student's own profile. Omitted fields are left untouched. |

### Subjects — 5 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/subjects` | Lists every active subject with teacher/group/course counters. |
| GET | `/api/v1/subjects/{idOrSlug}` | Gets one subject by id or slug. |
| GET | `/api/v1/subjects/{id}/courses` | Published courses for this subject. |
| GET | `/api/v1/subjects/{id}/groups` | Published groups for this subject. |
| GET | `/api/v1/subjects/{id}/teachers` | Verified teachers who teach this subject. |

### Teachers — 20 operations

| Method | Route | Purpose |
|---|---|---|
| PUT | `/api/v1/teacher/about` | Updates only the biography/headline part of the profile. |
| PUT | `/api/v1/teacher/achievements` | Replaces the teacher's achievement list. |
| PUT | `/api/v1/teacher/grades` | Replaces the educational grades the teacher serves. |
| GET | `/api/v1/teacher/profile` | Returns the calling teacher's own profile. |
| PUT | `/api/v1/teacher/profile` | Updates the calling teacher's profile. Only supplied fields change. |
| POST | `/api/v1/teacher/profile/cover` | Uploads a new cover image. |
| POST | `/api/v1/teacher/profile/image` | Uploads a new profile picture. |
| GET | `/api/v1/teacher/profile/verification` | Returns the calling teacher's verification state and what is still missing before they can ask to be reviewed. |
| POST | `/api/v1/teacher/profile/verification` | Submits the profile for verification. Rejected with a coded business-rule error when the profile is incomplete or a... |
| PUT | `/api/v1/teacher/social-links` | Replaces the teacher's social links. Each URL is validated against its platform. |
| PUT | `/api/v1/teacher/subjects` | Replaces the subjects the teacher is listed under. |
| GET | `/api/v1/teachers` | Searches the verified teacher directory. Filters: subjectId, gradeId, city, teachingMode, minRating, featuredOnly, ... |
| GET | `/api/v1/teachers/{id}` | Gets a teacher's public profile. |
| GET | `/api/v1/teachers/{id}/achievements` | The teacher's listed achievements. |
| GET | `/api/v1/teachers/{id}/courses` | The teacher's published courses. |
| GET | `/api/v1/teachers/{id}/groups` | The teacher's published groups. |
| GET | `/api/v1/teachers/{id}/profile` | Alias of GET /teachers/{id}, kept because the client links to /profile. |
| GET | `/api/v1/teachers/{id}/reviews` | Approved reviews written about this teacher. |
| GET | `/api/v1/teachers/{id}/reviews/summary` | Star distribution for this teacher. |
| GET | `/api/v1/teachers/{id}/schedule` | The teacher's weekly timetable across their published groups. |

### Team — 8 operations

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/v1/team/invitations/accept` | Accepts an invitation. The signed-in account's email must match the invite. |
| POST | `/api/v1/team/invitations/decline` | Declines an invitation. |
| GET | `/api/v1/tenant/team` | Members of the caller's workspace. |
| POST | `/api/v1/tenant/team/invite` | Invites someone to the workspace. Requires Team.Manage and an available seat. |
| GET | `/api/v1/tenant/team/{id}` | One member, with their effective and explicit permissions. |
| DELETE | `/api/v1/tenant/team/{id}` | Removes a member and revokes their permissions. |
| PUT | `/api/v1/tenant/team/{id}/role` | Changes a member's workspace role. Ownership can never be granted here. |
| PUT | `/api/v1/tenant/team/{id}/status` | Activates or deactivates a member. |

### Tenant — 14 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/tenant` | The workspace the caller is acting inside. |
| PUT | `/api/v1/tenant` | Updates the workspace name, slug and contact details. Requires Tenant.Manage. |
| GET | `/api/v1/tenant/audit-logs` | Sensitive actions taken inside this workspace. |
| GET | `/api/v1/tenant/branding` | Colours, fonts and imagery for the workspace and its public site. |
| PUT | `/api/v1/tenant/branding` | Updates branding. Requires Website.Manage. |
| POST | `/api/v1/tenant/cover` | Uploads the workspace cover image. |
| GET | `/api/v1/tenant/domains` | Custom domains pointed at this workspace's public site. |
| POST | `/api/v1/tenant/domains` | Registers a custom domain. Requires a plan that includes custom domains. |
| PUT | `/api/v1/tenant/domains/{id}` | Updates a domain, e.g. promoting a verified one to primary. |
| DELETE | `/api/v1/tenant/domains/{id}` | Removes a custom domain. |
| POST | `/api/v1/tenant/domains/{id}/verify` | Checks the DNS TXT record and marks the domain verified when it matches. |
| POST | `/api/v1/tenant/logo` | Uploads the workspace logo. |
| GET | `/api/v1/tenant/settings` | Operational settings, including the parent notification thresholds. |
| PUT | `/api/v1/tenant/settings` | Updates operational settings. Requires Tenant.Manage. |

### Website — 13 operations

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/public/tenants/{slug}` | The tenant's public profile, branding and published website. |
| GET | `/api/v1/public/tenants/{slug}/branding` | Branding only, for a fast first paint. |
| GET | `/api/v1/public/tenants/{slug}/courses` | The tenant's published courses. |
| GET | `/api/v1/public/tenants/{slug}/groups` | The tenant's published groups. |
| GET | `/api/v1/public/tenants/{slug}/seo` | SEO metadata and the canonical URL for the tenant's site. |
| GET | `/api/v1/public/tenants/{slug}/teachers` | The teachers who work in this workspace. |
| GET | `/api/v1/tenant/website` | The whole website configuration: hero, about, contact, SEO, sections. |
| PUT | `/api/v1/tenant/website` | Updates the website. Requires Website.Manage and a plan with customisation. |
| GET | `/api/v1/tenant/website/sections` | Ordered list of website sections. |
| POST | `/api/v1/tenant/website/sections` | Adds a section to the website. |
| POST | `/api/v1/tenant/website/sections/reorder` | Re-orders sections; the array position becomes the new sort order. |
| PUT | `/api/v1/tenant/website/sections/{id}` | Updates one section. |
| DELETE | `/api/v1/tenant/website/sections/{id}` | Deletes one section. |

<!-- END GENERATED INVENTORY -->

---

## 14. Known gaps and limitations

### Endpoints that exist but need a migration applied first

`GET /api/v1/teacher/activity` and the homework resubmission fields (`allowResubmission`,
`maxSubmissions`, `closedAt`, `returnReason`, `returnedAt`, `submissionCount`) depend on
migrations that are **not yet applied to production**. The code is deployed; the tables and
columns are not. These will fail in that environment until the migration runs. See
[`backend-audit-report.md` §4](backend-audit-report.md#4-database).

### Not implemented

Both are listed as outstanding work in the audit report, not as available APIs:

- **Platform-level settings.** No endpoint, no entity.
- **Platform-wide announcements.** `Announcement` is school-scoped; a platform-wide one needs a
  schema change.

### Limitations to design around

- **Arabic business-rule coverage is partial.** About 115 error codes have Arabic. A code without
  an entry falls back to its authored English message, so the response is always well formed but
  may occasionally be English for a rare business rule.
- **Office documents and archives are not content-verified** (see [§8](#8-file-uploads-images-and-private-downloads)).
- **Dashboard endpoints are chatty.** The student, teacher, parent and admin dashboards each issue
  8–24 sequential database queries. Prefer the compact summary endpoints (`/student/dashboard`,
  `/teacher/dashboard`, `/platform/overview`) over assembling a dashboard from many calls — but
  expect them to be the slowest endpoints in the API. Being measured and addressed; see
  [`backend-audit-report.md` §5](backend-audit-report.md#5-performance).
- **Real time is optional.** Always have the HTTP path working; see [§9](#9-notifications-and-real-time).
- **`message` on success envelopes is not localised.** Do not display it.

### Nothing is deprecated

No route was removed or deprecated in this change. Every endpoint that worked before still works
with the same method, path and success payload. See
[`frontend-api-changes.md`](frontend-api-changes.md) for what changed around them.
