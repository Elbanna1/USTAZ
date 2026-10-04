# USTAZ production deployment

Written against the real deployment on **2 October 2026**, while diagnosing the
`HTTP Error 500.30` the site was returning. The root cause was a single missing environment
variable; §1 is the fix.

No secret appears anywhere in this document. Variables are named, never valued.

---

## 1. The 500.30 fix — do this first

**Cause:** `ConnectionStrings__DefaultConnection` is not set on the host.

`appsettings.Production.json` deliberately ships that key as an empty string, and **an empty
string overrides the value in `appsettings.json`** — configuration layering does not treat blank
as absent. So in Production the environment variable is the *only* source. Without it the
application throws during service registration, before the host exists, and the ASP.NET Core
Module turns any unhandled startup exception into the generic `500.30` page.

### Fix

Set the variable at the application level in IIS, not the system level:

1. **IIS Manager** → your site → **Configuration Editor**
2. Section: `system.webServer/aspNetCore` → `environmentVariables` → **…**
3. Add:

| Name | Value |
|---|---|
| `ConnectionStrings__DefaultConnection` | *the real connection string* |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

4. Apply, then **recycle the application pool**.

Equivalent via `appcmd`:

```cmd
%windir%\system32\inetsrv\appcmd.exe set config "YourSite" ^
  -section:system.webServer/aspNetCore ^
  /+"environmentVariables.[name='ConnectionStrings__DefaultConnection',value='<connection string>']" ^
  /commit:apphost
```

Or, if your host offers a control-panel field for environment variables, use that — it is the
same mechanism and keeps the value out of `web.config`.

> **Do not put the connection string into `appsettings.Production.json` and redeploy.** That is
> how the credential ended up in source control in the first place.

### Verify

```bash
curl -i https://api.ustaz.app/health
```

Expected:

```json
{"status":"Healthy","checks":[{"name":"api","status":"Healthy"},{"name":"database","status":"Healthy"}]}
```

`"status":"Unhealthy"` with the `database` check failing means the app started but cannot reach
SQL Server — a wrong connection string rather than a missing one. A `500.30` still means startup
failed; read the diagnostic (§3).

---

## 2. Required environment variables

Names only.

### The application will not start without this

| Variable | Notes |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string. Startup fails with a named diagnostic if absent |

### Set these in production

| Variable | Why |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | Must be `Production` |
| `Jwt__Key` | Signing key, **at least 32 characters**. If absent a key is generated and persisted under the storage root — and if that file is later lost, **every issued token is invalidated**. Set it explicitly |
| `Jwt__Issuer`, `Jwt__Audience` | Token issuer and audience. Startup warns if absent |
| `Cors__AllowedOrigins__0` | Your web client's origin. **An empty allow-list blocks every browser cross-origin request** — the API adds no CORS headers at all. Add `__1`, `__2`, … for more |
| `Storage__RootPath` | Absolute path to a **persistent** directory for uploads. See §5 |

### Set these if the feature is used

| Variable | For |
|---|---|
| `Email__Password` | SMTP password. Without it, messages are written to disk instead of sent |
| `Email__Username`, `Email__FromEmail`, `Email__Host`, `Email__Port` | SMTP; non-secret defaults already ship |
| `Seed__AdminEmail`, `Seed__AdminPassword` | Provisions the **first** platform administrator. See §7 |
| `Redis__ConnectionString` | Optional distributed cache. Absent means in-memory, which is correct for a single instance |

### Tunable, with working defaults

| Variable | Default |
|---|---|
| `RateLimiting__GlobalPermitLimit` | `300` per minute |
| `RateLimiting__AuthPermitLimit` | `10` per minute |
| `RateLimiting__UploadPermitLimit` | `20` per 5 minutes |
| `Storage__MaxFileSizeInMegabytes` | `25` |
| `Storage__PublicCacheSeconds` | `604800` |
| `Swagger__Enabled` | `false` outside Development |
| `Realtime__Enabled` | `true` |
| `Seed__ApplyMigrationsOnStartup` | `false` — **leave it false in production**, see §4 |
| `Seed__SeedEssentials` | `true` (roles, subjects, grades — idempotent) |
| `Seed__SeedDemoData` | `false` — **never true in production** |

### Removed

`Cloudinary__CloudName`, `Cloudinary__ApiKey`, `Cloudinary__ApiSecret` and the rest of that
section **no longer exist**. Files are stored on this server. Delete them from any deployment
script; they are ignored.

---

## 3. Reading a startup failure

A `500.30` means the process failed before it could serve anything, so there is no application
log for the request — there was no request.

### Where the diagnostic goes

Missing configuration is now reported in three places before the exception is thrown:

1. **stderr** — captured by the IIS stdout log when it is enabled (§3.1).
2. **The Windows Application event log**, source `Ustaz.API`. Works even with the stdout log off
   and is usually the fastest place to look. Event Viewer → Windows Logs → Application.
3. The exception message itself.

It reports **every** missing key at once, not just the first, and names only keys — never values:

```
USTAZ cannot start: required configuration is missing.

Environment: Production
Content root: C:\inetpub\wwwroot\ustaz-api

Missing settings:
  - ConnectionStrings__DefaultConnection — the SQL Server connection string. Note that
    appsettings.Production.json ships this key as an empty placeholder, which overrides the
    value in appsettings.json: on a production host the environment variable is the only source.

Set these as environment variables on the host (double underscore separates the section from
the key). No value is printed here by design.
```

### 3.1 Enabling the stdout log temporarily

Only when the event log is unavailable. **It is off by default on purpose** — it grows without
bound and records everything the process writes.

In `web.config`:

```xml
<aspNetCore processPath="dotnet" arguments=".\Ustaz.API.dll"
            stdoutLogEnabled="true" stdoutLogFile=".\logs\stdout"
            hostingModel="inprocess" />
```

Make sure `.\logs` exists and the app pool identity can write to it. Recycle the pool, reproduce
the failure, read the newest `logs\stdout_*.log`.

**Then turn it off again:**

1. Set `stdoutLogEnabled="false"`.
2. Recycle the pool.
3. **Delete the stdout files.** They are verbose and can contain request detail you do not want
   retained.

Leaving it on is a disk-exhaustion risk and a log-hygiene problem.

### 3.2 Application logs

Once the app starts, Serilog writes to `logs/ustaz-<date>.log` beside the application, rolling
daily, keeping 14 files. Correlate a user report by the `traceId` in the error response.

Logs deliberately contain **no** access tokens, passwords, connection strings, request bodies or
headers. Request logging records method, path, status, elapsed time, correlation id and client
IP. Keep that in mind for retention: client IP is personal data under most regimes.

---

## 4. Database migrations

### Current state (2 October 2026)

Production is at:

```
20260928171424_initalCreate
```

**Two migrations are unapplied:**

| Migration | Contents |
|---|---|
| `20260930054644_AddTeacherVerificationSubmission` | 7 added columns (all nullable or defaulted), the `ActivityEvents` table, 6 indexes, 2 check constraints |
| `20261001052232_AddEdits` | Empty — a no-op that only records a history row |

Both were verified to apply cleanly from scratch on a disposable SQL Server database. The delta
is **purely additive**: no drop, no column retype, no table recreation. The two tables it alters
(`Homeworks`, `HomeworkSubmissions`) were confirmed **empty** in production, so no data can be
affected.

Reviewed SQL:
[`sql/20261002-initalCreate-to-AddEdits.sql`](sql/20261002-initalCreate-to-AddEdits.sql) ·
idempotent full script:
[`sql/20261002-idempotent-full.sql`](sql/20261002-idempotent-full.sql)

### Applying a migration to production

**`Seed__ApplyMigrationsOnStartup` must stay `false`.** Migrating on startup means a deployment
silently alters the schema, concurrently across instances, with any failure swallowed into a log
line. Schema changes are deliberate operations.

The procedure, in order. Do not skip step 1.

1. **Take a backup and confirm it is restorable.** Not "a backup exists" — confirm you could
   restore it.
2. **Review the SQL.** Generate it yourself if you want to re-verify:

   ```bash
   dotnet ef migrations script 20260928171424_initalCreate 20261001052232_AddEdits \
     --project src/Ustaz.Persistence --startup-project src/Ustaz.API \
     -o migration.sql --idempotent
   ```

   Read it for `DROP`, `ALTER COLUMN` on a populated table, and anything that recreates a table.
   The current delta has none of these.
3. **Rehearse on a copy**, restored from the backup in step 1.
4. **Announce a maintenance window** if the change is not purely additive. This one is, so a
   window is optional.
5. **Apply the script** with a SQL client against the explicitly identified production database.
   Prefer the script over `dotnet ef database update` — a tool that takes a connection string
   from ambient configuration is one wrong environment variable away from migrating the wrong
   database.
6. **Verify:**

   ```sql
   SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;
   ```

7. **Check `/health/ready`** and exercise one affected endpoint.

### Never

- `Database.EnsureCreated()` — it bypasses migrations entirely and cannot be migrated afterwards.
- `dotnet ef database update` against a connection string you have not read with your own eyes.
- Deleting or editing a migration that has already been applied anywhere.
- `dotnet ef database drop`, on anything.

---

## 5. Persistent upload storage

`Storage__RootPath` is an absolute path, or a path relative to the application's content root. It
must point somewhere that **survives a redeploy**.

```
{Storage__RootPath}/
  public/                                   served at /uploads, cacheable, public
    users_profile/{id}.png
    teachers_profile/{id}.jpg
  tenants/{tenantId}/{area}/{ownerId}/...   private, authorised download only
  shared/{area}/...                         private, authorised download only
  keys/jwt-signing.key                      never served
```

### This is the part that most often goes wrong

**Do not point it inside the published application directory.** A redeploy replaces that
directory, and every avatar, logo and homework attachment goes with it.

| Host | Suggested |
|---|---|
| IIS on Windows | `D:\ustaz-data\storage` — outside `wwwroot`, outside the deploy target |
| Docker | a mounted volume, e.g. `/var/ustaz/storage` |
| Shared hosting | **Verify before trusting it.** Many providers wipe everything outside a specific data directory on deploy. Ask, then test |

**Verify rather than assume.** Upload a profile picture, redeploy, then re-request the same
`/uploads/...` URL. If it 404s, the path is ephemeral — and the earlier it is found, the fewer
files are lost.

If a persistent path genuinely is not available, say so in writing and treat uploaded media as
disposable. Do not discover this after a term's worth of homework attachments.

### Filesystem permissions

The application pool identity (`IIS AppPool\<YourAppPool>`, or the account you configured) needs:

| Path | Permission |
|---|---|
| `{Storage__RootPath}` and below | **Modify** — read, write, create, delete |
| `{app}\logs` | **Modify** — Serilog's file sink |
| The application directory | **Read & execute** only |

Grant Modify on the storage root and nothing more:

```cmd
icacls "D:\ustaz-data\storage" /grant "IIS AppPool\UstazApi:(OI)(CI)M" /T
```

Do not grant write access to the application directory. The app never needs to modify its own
binaries or configuration, and denying it closes a whole class of attack.

### Backup and cleanup

- **Back up the storage root alongside the database.** They are one system: a database row
  referencing a file that is gone is a broken homework submission. Restoring one without the
  other produces inconsistency.
- **Back up `{root}/keys/jwt-signing.key`** if `Jwt__Key` is not set — losing it signs every user
  out. Better: set `Jwt__Key` and this stops mattering.
- **Orphaned files.** A file is deleted only after the row replacing it is committed, which is
  the right order — it means a failed delete leaves a harmless orphan rather than a broken
  reference. There is no sweeper for those. They are small and rare; schedule a periodic audit
  comparing `{root}/public` against the `ProfileImagePublicId` columns if disk becomes a concern.

---

## 6. Hosting configuration

### What the deployment requires

| | |
|---|---|
| Runtime | **.NET 10** ASP.NET Core Runtime (the SDK is not needed) |
| IIS | **ASP.NET Core Hosting Bundle** for .NET 10 — this installs the `AspNetCoreModuleV2` the site's handler references |
| Hosting model | `inprocess` (as published) |
| Target framework | `net10.0` |
| Database | SQL Server. Production currently runs 17.00.4085 |

A missing or mismatched Hosting Bundle is the other classic `500.30`: `web.config` names
`AspNetCoreModuleV2`, and if the module is absent IIS cannot start the app at all. After
installing it, **restart IIS** (`net stop was /y && net start w3svc`), not just the pool.

### web.config

Published as:

```xml
<aspNetCore processPath="dotnet" arguments=".\Ustaz.API.dll"
            stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout"
            hostingModel="inprocess" />
```

Keep `stdoutLogEnabled="false"` except while troubleshooting (§3.1).

### TLS and forwarded headers

The application calls `UseHsts()` and `UseHttpsRedirection()` outside Development, and honours
`X-Forwarded-For` and `X-Forwarded-Proto`. If TLS terminates at a proxy or load balancer, make
sure it sets both — otherwise the app sees HTTP, the redirect loops, and client IPs in the logs
are the proxy's.

### Upload size

The application caps a multipart body at 64 MB and each file by
`Storage__MaxFileSizeInMegabytes` (default 25). **IIS caps the request first**, at 30 MB by
default, and returns its own error page before ASP.NET Core sees the request. To allow larger
uploads, raise it too:

```xml
<security>
  <requestFiltering>
    <requestLimits maxAllowedContentLength="68157440" /> <!-- 65 MB -->
  </requestFiltering>
</security>
```

---

## 7. The first platform administrator

The platform `Admin` role is the highest authority in USTAZ. A school owner does **not** have it.

To create the first one, set `Seed__AdminEmail` and `Seed__AdminPassword` with
`Seed__SeedEssentials=true`, and start the application once. The seeder is idempotent — it does
nothing if that email already exists.

Then:

1. Sign in and confirm `GET /api/v1/auth/me` returns `"Admin"` in `roles`.
2. **Create a second administrator** via `POST /api/v1/platform/administrators/{userId}`, so the
   platform is not one lost password away from having no administrator.
3. **Unset `Seed__AdminPassword`** and recycle the pool. It has done its job and should not
   linger in the host's environment.

The API refuses to leave the platform without an administrator: revoking the role from, or
suspending, the last active administrator is rejected (`LAST_PLATFORM_ADMIN`,
`PLATFORM_ADMIN_SELF_DEMOTION`). That guard is a safety net, not a substitute for having two.

---

## 8. Deployment verification checklist

Run after every deployment.

### Startup
- [ ] `GET /health` returns `200` and `"status":"Healthy"`
- [ ] `GET /health/ready` shows the `database` check `Healthy`
- [ ] `GET /health/live` returns `200`
- [ ] No `Error` or `Fatal` entries in `logs/ustaz-<date>.log` from startup
- [ ] No startup configuration **warnings** in the log (`Jwt__Key`, `Cors__AllowedOrigins__0`)

### Schema
- [ ] `SELECT MigrationId FROM __EFMigrationsHistory` lists every migration in the build
- [ ] One endpoint backed by the newest migration works (`GET /api/v1/teacher/activity`)

### Authentication
- [ ] A known account can sign in
- [ ] `GET /api/v1/auth/me` returns the expected roles
- [ ] An anonymous call to `/api/v1/student/dashboard` returns `401` **with a JSON body**
- [ ] Eleven rapid failed logins produce a `429` — confirms the auth rate limit is live

### Authorization
- [ ] A school owner's token gets `403` on `/api/v1/admin/dashboard` and `/api/v1/platform/overview`
- [ ] A platform admin's token gets `200` on both

### Localisation
- [ ] A deliberately invalid registration returns Arabic field errors by default
- [ ] The same request with `X-Ustaz-Language: en` returns English

### Files
- [ ] `POST /api/v1/me/profile-picture` with a real PNG succeeds
- [ ] The returned `/uploads/...` URL loads
- [ ] **After the next redeploy, that URL still loads** — the persistence test that matters
- [ ] A text file renamed to `.png` is refused with `FILE_SIGNATURE_MISMATCH`

### Security hygiene
- [ ] `GET /health` output contains no connection string, exception text or duration
- [ ] `Swagger__Enabled` is `false`, or Swagger is deliberately published
- [ ] `stdoutLogEnabled="false"` in `web.config`
- [ ] No secret in any `appsettings*.json` in the deployed directory

---

## 9. Rollback

### Application code

Keep the previous publish output. Rolling back is swapping the directory and recycling the pool.

**A rollback does not undo a migration.** Because the pending migration is purely additive, the
previous build runs fine against the newer schema — added columns are nullable or defaulted and
the new table is simply unused. This is why additive migrations are worth the discipline: they
make the code and the schema independently reversible.

### Database

Restore the backup taken before the migration. `dotnet ef database update <previous>` runs the
`Down` methods, which for `AddTeacherVerificationSubmission` **drops the columns and the
`ActivityEvents` table** — losing anything written to them since. Prefer the backup unless you
are certain nothing has been written.

### Credential rotation

If the database password is rotated — **and it must be, because it was committed to source
control** — the order is:

1. Add the new login/password on the SQL Server **alongside** the old one.
2. Update `ConnectionStrings__DefaultConnection` on the host; recycle the pool.
3. Confirm `/health/ready`.
4. **Only then** disable the old credential.

Rotating in the other order takes the site down between steps.

---

## 10. Known operational risks

| Risk | Status |
|---|---|
| **The production database password was committed to source control.** It has been removed from the working tree, but removal is not rotation — it has already been distributed wherever the repository has been | **Rotate it.** Until then, treat the database as compromised |
| `ConnectionStrings__DefaultConnection` missing on the host | The `500.30`. §1 |
| Two migrations unapplied | §4 |
| `Jwt__Key` unset | A generated key is persisted under the storage root; if the path is not durable, tokens are invalidated on restart |
| Storage root persistence unverified on this host | §5. Test it with a redeploy before it matters |
| Dashboard endpoints issue 8–24 sequential queries | Measured at ~230 ms per round trip **from outside the datacentre**. Re-measure from the application server before acting — if the database is co-located this is a non-issue. [`backend-audit-report.md` §5](backend-audit-report.md#5-performance) |

---

## 11. What has not been verified

Stated plainly so nothing here is mistaken for a confirmation.

- **The production IIS host was never accessed.** Its environment variables, filesystem, app pool
  identity and stdout log could not be inspected. "The environment variable is missing" is an
  inference from reproducing the identical failure locally plus reading the committed
  configuration — strongly supported, but confirm it on the host.
- **Nothing was deployed, and the live site has not been verified as fixed.**
- **No write of any kind was made to the production database.** Reads were limited to schema
  metadata, migration history and row counts.
- **No credential was rotated** and no production infrastructure was altered.
