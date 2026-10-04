# What changed for the frontend — 2 October 2026

Everything on this page is implemented, built and covered by passing tests. Nothing here is
planned work; anything outstanding is in
[`backend-audit-report.md` §8](backend-audit-report.md#8-outstanding-risks-and-remaining-work).

The machine-readable contract is [`openapi-v1.json`](openapi-v1.json), generated from the running
API (288 paths).

---

## TL;DR — the three things that need action

1. **Error responses now carry a stable `code`, and `detail` is Arabic by default.** If you were
   showing `detail` to users you already get Arabic for free. If you were *matching on* `detail`
   text, switch to `code`. See [§1](#1-breaking-the-error-envelope).
2. **401 and 403 now have response bodies.** They used to be empty. You can finally tell an
   expired session (`TOKEN_EXPIRED`) from a missing one (`UNAUTHORIZED`) and show the right thing.
   See [§2](#2-new-401-and-403-have-bodies).
3. **Students and parents can now upload a profile picture** at `/api/v1/me/profile-picture`.
   This never existed before — the avatar field was permanently null for them.
   See [§4](#4-new-profile-pictures-for-every-role).

---

## 1. BREAKING: the error envelope

### What it looks like now

Every error — validation, business rule, 401, 403, 404, 409, 422, 429, 500 — returns
`application/problem+json` with this shape:

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

### What is new on it

| Field | Notes |
|---|---|
| `code` | **New, and always present.** A stable, never-translated identifier. **Branch on this.** |
| `language` | **New.** `ar-EG` or `en` — the language `detail` was written in. |
| `detail` | **Now localised.** Arabic unless you ask for English. |
| `title` | Unchanged in spirit but **now stable per status** and still English — it is a category for your logs, not something to show a user. |
| `errors` | Unchanged. Present on 400s: `{ "fieldName": ["message", ...] }`. Field names stay English; messages are localised. |
| `success`, `traceId`, `instance`, `status`, `type` | Unchanged. |

### Why this is breaking

If any code does `if (error.detail === "Invalid email or password.")`, it will now see
`"الإيميل أو الباسورد غلط."` and stop matching. Replace with
`if (error.code === "INVALID_CREDENTIALS")`.

### Choosing the language

Resolution order, first match wins:

1. `X-Ustaz-Language: en` — **recommended.** Set it from the user's stored `preferredLanguage`,
   which you already receive from `GET /api/v1/auth/me`.
2. `?lang=en` — for a link that must open in a specific language.
3. `Accept-Language: en-US` — what the browser sends.
4. **Arabic** — the default when nothing says otherwise.

Only an explicit `en`-prefixed value selects English. Anything else, including an absent header,
is Arabic.

```ts
// Recommended: one place, set once.
api.defaults.headers.common["X-Ustaz-Language"] = user.preferredLanguage ?? "ar-EG";
```

### Codes you should handle explicitly

| Code | Status | Meaning | Suggested action |
|---|---|---|---|
| `VALIDATION_FAILED` | 400 | Field errors in `errors` | Render beside the inputs |
| `INVALID_CREDENTIALS` | 401 | Wrong email **or** wrong password — deliberately indistinguishable | Show the message; never say which |
| `ACCOUNT_LOCKED` | 401 | Too many failed attempts | Show the message, suggest waiting |
| `ACCOUNT_INACTIVE` | 401/422 | Account suspended | Point at support |
| `UNAUTHORIZED` | 401 | No token, or an invalid one | Send to sign-in |
| `TOKEN_EXPIRED` | 401 | Access or refresh token expired | **Try the refresh flow, then sign-in.** Also carries the `x-token-expired: true` header |
| `REFRESH_TOKEN_INVALID` | 401 | Refresh token reused or revoked — the whole token family is burned | Force sign-in. Do not retry |
| `FORBIDDEN` | 403 | Authenticated but not permitted | Show the message; do not retry |
| `RESOURCE_NOT_FOUND` | 404 | Also returned for a resource in **another tenant**, so its existence is not confirmed | "Not found" |
| `CONFLICT` / `DUPLICATE_VALUE` | 409 | Clashes with existing data | Show the message |
| `CONCURRENCY_CONFLICT` | 409 | Someone else edited it | Prompt to reload |
| `RATE_LIMITED` | 429 | Too many requests | Back off; see §3 |
| `PAYLOAD_TOO_LARGE` | 413 | Request body over the limit | Show a size hint |
| `INTERNAL_ERROR` | 500 | Unexpected | Generic retry message; `traceId` is what support needs |

Business-rule codes (422) are domain-specific and there are about 115 of them —
`HOMEWORK_CLOSED`, `HOMEWORK_DEADLINE_PASSED`, `EXAM_NOT_OPEN`, `GROUP_FULL`,
`ENROLLMENT_CANCELLED`, `PLAN_LIMIT_REACHED`, `TENANT_SUSPENDED`, `FILE_TOO_LARGE`,
`FILE_SIGNATURE_MISMATCH`, and so on. **You do not need to enumerate them.** Show `detail` and
treat the code as a key for any special handling you want. The full list is in
`src/Ustaz.Application/Common/Localization/ErrorCatalog.cs`.

> A code with no Arabic entry yet falls back to the authored English message. The response is
> always well-formed; the language may occasionally be English for a rare business rule.

### Arabic validation example

`POST /api/v1/auth/register/student` with an empty name, a malformed email, a weak password and a
bad phone number:

```json
{
  "title": "Validation failed.",
  "status": 400,
  "detail": "فيه حقول ناقصة أو غير صحيحة. راجع البيانات وجرّب تاني.",
  "code": "VALIDATION_FAILED",
  "language": "ar-EG",
  "success": false,
  "errors": {
    "FirstName": ["الحقل ده مطلوب."],
    "LastName": ["الحقل ده مطلوب."],
    "Email": ["اكتب إيميل صحيح."],
    "Password": [
      "الباسورد لازم يكون 8 حروف على الأقل.",
      "الباسورد لازم يحتوي على حرف كبير واحد على الأقل."
    ],
    "PhoneNumber": ["اكتب رقم موبايل مصري صحيح، مثال: 01012345678."]
  }
}
```

The same request with `X-Ustaz-Language: en`:

```json
{
  "code": "VALIDATION_FAILED",
  "language": "en",
  "errors": {
    "FirstName": ["This field is required."],
    "Email": ["Email must be a valid email address."],
    "Password": ["Password must be at least 8 characters long."],
    "PhoneNumber": ["Phone number must be a valid Egyptian mobile number, e.g. 01012345678."]
  }
}
```

---

## 2. NEW: 401 and 403 have bodies

Previously the authentication middleware returned an **empty** 401 and an **empty** 403. There
was nothing to show a user whose session had simply expired.

**Expired token** — note the distinct code and the header:

```http
HTTP/1.1 401 Unauthorized
Content-Type: application/problem+json
x-token-expired: true
```
```json
{
  "title": "Unauthorized.",
  "status": 401,
  "detail": "الجلسة انتهت. سجّل دخول تاني.",
  "code": "TOKEN_EXPIRED",
  "language": "ar-EG",
  "success": false
}
```

**No token at all:** same shape, `code: "UNAUTHORIZED"`, detail `"محتاج تسجّل دخول الأول."`

**Authenticated but not permitted** (e.g. a student calling `/api/v1/admin/dashboard`):

```json
{
  "title": "Forbidden.",
  "status": 403,
  "detail": "مش مسموح لك بالعملية دي.",
  "code": "FORBIDDEN",
  "language": "ar-EG",
  "success": false
}
```

**Recommended interceptor:**

```ts
if (status === 401 && error.code === "TOKEN_EXPIRED") {
  await refreshAccessToken();   // retry once
} else if (status === 401) {
  redirectToSignIn();
}
```

---

## 3. CHANGED: rate limiting now actually applies

The tight budget on credential endpoints was previously **inert** — a configuration bug meant the
generous 300/minute policy silently replaced it. It now works.

| Endpoints | Budget |
|---|---|
| `POST /auth/login`, `/auth/register/*`, `/auth/refresh-token`, `/auth/forgot-password`, `/auth/reset-password`, `/auth/verify-email`, `/auth/resend-verification`, `/auth/change-password`, `/auth/logout` | **10 per minute** |
| File uploads (profile pictures, logos, covers, homework and message attachments) | **20 per 5 minutes** |
| Everything else | 300 per minute |

**What you need to do:** nothing for normal use, but two things to be careful of.

- **Do not retry a failed login automatically.** Ten attempts a minute is generous for a person
  and tight for a loop.
- **A 429 on login is now reachable.** Handle `code: "RATE_LIMITED"` on the sign-in form, not just
  on bulk operations.

Partitioning is per authenticated user where there is one, and per client IP otherwise — so users
behind a shared NAT share the anonymous budget.

---

## 4. NEW: profile pictures for every role

**This capability did not exist for students or parents.** `profileImageUrl` was returned
everywhere and was permanently `null`, because only `POST /teacher/profile/image` could write it.

### `GET /api/v1/me/profile-picture`

Any authenticated user. No parameters.

```json
{
  "success": true,
  "message": "Operation completed successfully.",
  "data": { "url": "/uploads/users_profile/ffd4d9ba7f8e4a14a3af5c22a791e3d2.png", "hasPicture": true }
}
```

When none is set: `{ "url": null, "hasPicture": false }` — use `hasPicture` to pick your
placeholder.

### `POST /api/v1/me/profile-picture`

`multipart/form-data`, field name **`file`**. Replaces any existing picture.

```ts
const form = new FormData();
form.append("file", file);                      // the field MUST be named "file"
const res = await api.post("/api/v1/me/profile-picture", form);
// do NOT set Content-Type yourself — let the browser add the multipart boundary
```

```json
{
  "success": true,
  "message": "Your profile picture has been updated.",
  "data": { "url": "/uploads/users_profile/6aaddc2943c94e26a76ddda73dc5b995.png", "hasPicture": true }
}
```

Constraints, all enforced server-side:

| Rule | Value |
|---|---|
| Max size | **5 MB** |
| Extensions | `.jpg` `.jpeg` `.png` `.webp` `.gif` |
| `Content-Type` | must start with `image/` |
| **Content** | **the actual bytes must match the extension** — see below |
| Rate limit | 20 per 5 minutes |

**The content check is new and will reject files that used to be accepted.** The first bytes of the
file are read and verified against its extension. Renaming `document.pdf` to `avatar.png` is
refused with `422` and `code: "FILE_SIGNATURE_MISMATCH"`. If your uploader does any client-side
re-encoding or cropping, make sure it writes a real image of the type the filename claims.

Every URL returned is a **new** path — replacing a picture never mutates an existing URL, so you
can cache `/uploads/*` aggressively. The server sends
`Cache-Control: public, max-age=604800, immutable`.

### `DELETE /api/v1/me/profile-picture`

Returns the user to the default avatar. **Idempotent** — deleting when there is no picture returns
`200`, not `404`.

```json
{ "success": true, "message": "Your profile picture has been removed.", "data": { "url": null, "hasPicture": false } }
```

### Teachers

`POST /api/v1/teacher/profile/image` and `/cover` are **unchanged and still work** — teachers need
the cover image for their public page. The two are kept in step: uploading through `/me` also
updates the teacher profile row, so either endpoint is safe to use.

---

## 5. NEW: global platform admin APIs

All require the platform **`Admin`** identity role. A school admin (a workspace `Owner`) gets
`403` on every one — this is enforced server-side and covered by 18 route tests.

> **Do not use these to build school-admin screens.** They are platform-operator tooling.

### `GET /api/v1/platform/overview`

Everything a platform dashboard needs in one call.

```json
{
  "success": true,
  "data": {
    "totalSchools": 42,
    "activeSchools": 38,
    "suspendedSchools": 3,
    "archivedSchools": 1,
    "newSchoolsLast30Days": 5,

    "totalUsers": 1270,
    "activeUsers": 1254,
    "suspendedUsers": 16,
    "totalStudents": 1080,
    "totalTeachers": 61,
    "totalParents": 124,
    "platformAdministrators": 3,
    "schoolStaffMemberships": 77,

    "activeSubscriptions": 35,
    "trialSubscriptions": 4,
    "expiredSubscriptions": 2,
    "cancelledSubscriptions": 1,
    "atRiskSubscriptions": 2,

    "generatedAt": "2026-10-02T18:14:22.431Z"
  }
}
```

`atRiskSubscriptions` is past-due plus grace-period: schools that have stopped paying but have not
been cut off. `generatedAt` lets you show the figures' age.

### `GET /api/v1/platform/users`

Platform-wide user search — the only list in USTAZ that is not tenant-scoped.

| Query parameter | Type | Notes |
|---|---|---|
| `page` | int | Default 1 |
| `pageSize` | int | Default 12, max 100 |
| `search` | string | Matches display name, email **or** phone, case-insensitive substring |
| `role` | string | `student`, `teacher` or `parent` |
| `isActive` | bool | Omit for both |
| `schoolId` | guid | Active members of one school |
| `sortBy` | string | `createdAt` (default), `displayName`, `lastLoginAt` |
| `sortDescending` | bool | Default `true` |

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "userId": "9c1f2e30-...",
        "displayName": "سلمى إبراهيم",
        "email": "salma@example.com",
        "phoneNumber": "01012345678",
        "isActive": true,
        "emailConfirmed": true,
        "profileImageUrl": "/uploads/users_profile/ffd4....png",
        "createdAt": "2026-09-14T08:11:02Z",
        "lastLoginAt": "2026-10-01T19:40:55Z",
        "isStudent": true,
        "isTeacher": false,
        "isParent": false,
        "schoolMemberships": 1
      }
    ],
    "page": 1,
    "pageSize": 12,
    "totalCount": 1270,
    "totalPages": 106,
    "hasPreviousPage": false,
    "hasNextPage": true
  }
}
```

No credential material is ever returned — no password hash, no security stamp, no token. A test
asserts this.

### `GET /api/v1/platform/administrators`

```json
{
  "success": true,
  "data": [
    {
      "userId": "11111111-...",
      "displayName": "USTAZ Administrator",
      "email": "ops@ustaz.app",
      "isActive": true,
      "emailConfirmed": true,
      "createdAt": "2026-08-01T10:00:00Z",
      "lastLoginAt": "2026-10-02T09:12:41Z",
      "isLastAdministrator": false,
      "isCurrentUser": true
    }
  ]
}
```

**Use `isLastAdministrator` and `isCurrentUser` to disable controls rather than letting the request
fail.** Revoking either is refused by the server.

### `POST /api/v1/platform/administrators/{userId}`

Grants the platform role. **`reason` is required** — it goes into the audit trail.

```json
{ "reason": "انضم لفريق تشغيل المنصة" }
```

Response: `200` with `{ "success": true, "message": "Platform administrator access granted." }`

| Failure | Status | Code |
|---|---|---|
| `reason` empty | 400 | `VALIDATION_FAILED` |
| User does not exist | 404 | `RESOURCE_NOT_FOUND` |
| User is suspended | 422 | `ACCOUNT_INACTIVE` |
| Caller is not a platform admin | 403 | `FORBIDDEN` |

Granting someone who already holds the role returns `200` — it is idempotent.

**The new administrator must sign in again** (or refresh) before their token carries the role.

### `DELETE /api/v1/platform/administrators/{userId}`

Takes the role away **and revokes that account's refresh tokens**, because their issued access
token still carries the role claim until it expires. `reason` is required and is sent in the
request body of the DELETE.

```json
{ "reason": "انتهى تكليفه" }
```

| Failure | Status | Code |
|---|---|---|
| Revoking yourself | 422 | `PLATFORM_ADMIN_SELF_DEMOTION` |
| Revoking the last active administrator | 422 | `LAST_PLATFORM_ADMIN` |
| Not currently an administrator | 200 | — (idempotent) |

---

## 6. CHANGED: account suspension now has guards

`POST /api/v1/admin/users/{userId}/state` with `{ "isActive": false }` is refused in two new cases:

| Case | Status | Code |
|---|---|---|
| Suspending your own account | 422 | `PLATFORM_ADMIN_SELF_DEMOTION` |
| Suspending the last active platform administrator | 422 | `LAST_PLATFORM_ADMIN` |

Suspending ordinary users is unchanged.

---

## 7. CHANGED: all uploads verify file content

This applies to **every** upload endpoint, not just profile pictures:

- `POST /api/v1/me/profile-picture`
- `POST /api/v1/teacher/profile/image`, `/api/v1/teacher/profile/cover`
- `POST /api/v1/tenant/logo`, `/api/v1/tenant/cover`
- `POST /api/v1/student/homework/{id}/attachment`
- `POST /api/v1/messages/conversations/{id}/attachments`

The file's leading bytes are now verified against its extension for JPEG, PNG, GIF, WEBP, BMP and
PDF. A mismatch is `422` with `code: "FILE_SIGNATURE_MISMATCH"` and the Arabic detail
`"محتوى الملف مش متطابق مع امتداده. ارفع ملف صحيح."`

Office documents and archives (`.docx`, `.xlsx`, `.pptx`, `.zip`) are **not** content-verified —
they all share a ZIP container, so the bytes prove nothing. They remain allow-listed by extension
and content type.

---

## 8. No endpoints were removed or deprecated

Nothing was deleted. Every route that worked before this change still works, with the same method,
path and success payload. The changes are: the error envelope gained fields and localised `detail`
(§1), 401/403 gained bodies where they had none (§2), rate limits now apply as documented (§3),
and uploads are stricter about content (§7).

---

## 9. New roles and permission rules

No new roles. The clarification worth writing down, because the naming invites confusion:

| Concept | How it is represented | Scope |
|---|---|---|
| **Global platform admin** | `RoleNames.Admin` — an identity role, granted by the seeder or `POST /platform/administrators/{id}` | All of USTAZ |
| **School admin** | `TenantMemberRole.Owner` or `TenantMemberRole.Admin` — a workspace membership row | One school |

A school admin has **no** platform authority. They cannot reach `/api/v1/admin/*` or
`/api/v1/platform/*`, and they cannot suspend or activate a school — **not even their own.**

Read the platform role from `GET /api/v1/auth/me`: `roles` contains `"Admin"`. A school admin's
token instead carries `tenantId`, and a platform admin has no `tenantId` at all.

---

## 10. Database changes affecting frontend behaviour

None that change a payload. Two migrations are pending on production
(`AddTeacherVerificationSubmission`, `AddEdits`); the first adds homework resubmission fields
(`allowResubmission`, `maxSubmissions`, `closedAt`, `returnReason`, `returnedAt`,
`submissionCount`) and the `ActivityEvents` table that backs
`GET /api/v1/teacher/activity`.

**Until those are applied in a given environment, the activity timeline and the homework
resubmission fields will not work there.** The API surface is already present in code.

---

## 11. New notification events

None. The notification catalogue is unchanged — it was already complete and bilingual. The two new
**audit** actions (`platform.admin_granted`, `platform.admin_revoked`) appear in
`GET /api/v1/admin/audit-logs` but generate no user-facing notification.

---

## 12. Environment and configuration changes

Nothing the frontend sets. For completeness, backend configuration gained:

| Key | Default | Purpose |
|---|---|---|
| `RateLimiting:GlobalPermitLimit` | 300 | Baseline requests/minute |
| `RateLimiting:AuthPermitLimit` | 10 | Credential endpoints |
| `RateLimiting:UploadPermitLimit` | 20 | Uploads, per 5 minutes |
| `Storage:PublicCacheSeconds` | 604800 | `max-age` on `/uploads/*` |
| `Storage:AllowedImageExtensions` etc. | see `StorageOptions` | Moved off the removed `Cloudinary` section |

The whole `Cloudinary` configuration section is **gone**. Any deployment script setting
`Cloudinary__*` should drop it; those values are now ignored.
