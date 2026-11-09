# Design

## Context

Current state: `ParkingRealm/` is an empty .NET 10 minimal API (`Program.cs` only maps `GET /`), with no EF Core or Identity packages yet. `parking-realm/` is a fresh Create React App project (React 19, react-scripts 5). The API runs on `http://localhost:5198` / `https://localhost:7287`; the React dev server defaults to `http://localhost:3000`. See proposal.md for scope and specs/user-registration/spec.md for behavior. Registration collects email, password, first name, last name and phone number (all required, phone with country code).

## Goals / Non-Goals

**Goals:**
- Establish the data layer (EF Core + Identity on SQL Server) that later use cases reuse.
- Provide a single registration endpoint with server-side validation as the source of truth.
- Provide a React registration page that surfaces validation, duplicate-email and server errors.

**Non-Goals:**
- Login, token or cookie issuance, email confirmation, roles, password reset.
- The `Car` entity and any car endpoints or tables (later use case).
- Production deployment, hosting, or secrets management beyond using user-secrets/config for the connection string.
- A shared UI component library or routing framework beyond what registration needs.

## Decisions

1. **Custom registration endpoint using `UserManager<ApplicationUser>`, not `MapIdentityApi`.**
   `MapIdentityApi` exposes `/register` but also login, refresh, 2FA and more, which are out of scope, and its error shape is fixed. A single `POST /api/auth/register` (minimal API) lets us control the contract and add profile fields later. `UserManager.CreateAsync` still provides password hashing, password policy and email uniqueness. Alternative: `MapIdentityApi`. Rejected for being broader than UC1 and for giving less control over responses.

2. **`ApplicationUser : IdentityUser` with `FirstName` and `LastName`, and `ApplicationDbContext : IdentityDbContext<ApplicationUser>`.**
   Email and password stay the Identity login credentials. Phone number uses the `PhoneNumber` property `IdentityUser` already has, so no extra column is needed. `FirstName` and `LastName` are required properties. A later `Car` entity will reference the user by foreign key; no car table is created now. Register with `AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<ApplicationDbContext>()`. `AddIdentityCore` is used instead of `AddIdentity` to avoid cookie/UI scaffolding we do not need yet. Login will add sign-in services later.

3. **Request/response contract.**
   `POST /api/auth/register` with JSON `{ email, password, firstName, lastName, phoneNumber }`.
   - `201 Created` on success (no password or hash in the body).
   - `400` as RFC 7807 `ValidationProblem` with per-field errors for missing fields, invalid email, phone number without country code, password-rule failures, and duplicate email (Identity's `DuplicateEmail` is mapped to an `email` field error).
   - `500` as generic `ProblemDetails` with no exception details on unexpected/database failures.
   Validation is done on the server (all fields required, email format, phone number in E.164 form, then Identity password rules); the React form mirrors it for fast feedback only. "With country code" is defined as E.164: `+` followed by the country code and number, at most 15 digits. Phone numbers are not required to be unique.

4. **Unique email enforced at the database, not just in code.**
   Set `RequireUniqueEmail = true` and make Identity's `EmailIndex` on `NormalizedEmail` explicitly unique in the model (Identity's default index is not unique). The register handler also handles a unique-index `DbUpdateException` race and maps it to the duplicate-email response. Case-insensitivity comes from `NormalizedEmail`. UserName is set equal to email. `FirstName` and `LastName` columns are limited to 100 characters (`nvarchar(100)`) and validated against the same limit server-side.

5. **SQL Server via EF Core migrations.**
   Package `Microsoft.EntityFrameworkCore.SqlServer` (+ `.Design` for tooling). Connection string `DefaultConnection` in `appsettings.Development.json` points at LocalDB; real credentials go through user-secrets or environment variables, never committed. Schema is created by an `InitialIdentity` migration applied with `dotnet ef database update` (no auto-migrate on startup in this change).

6. **Failure handling.**
   Wrap creation so a failed save leaves no partial user (`CreateAsync` is a single transaction/`SaveChanges`). Add `AddProblemDetails()` and `UseExceptionHandler()` so unhandled exceptions return generic problem details and are logged server-side.

7. **Frontend: plain React with `fetch`, no new libraries.**
   A `RegisterPage` component with controlled inputs for the five fields, client-side checks (all required, email format, phone number with country code, minimum password length matching server rules), and a small `api/auth.js` client. It displays field errors from the `ValidationProblem` response, a success message on `201`, and a generic "registration failed, try again" on network or `5xx` errors. The API base URL comes from `REACT_APP_API_URL`. Alternatives: react-hook-form/axios. Rejected as unnecessary for one form.

8. **CORS and local dev.**
   Add a CORS policy allowing the React dev origin (`http://localhost:3000`) in Development only. Alternative: CRA `proxy` in `package.json`. Rejected because the API is served over HTTPS and separate origins match how it will be deployed.

9. **Password rules.**
   Use Identity defaults (min 6 characters, digit, lowercase, uppercase, non-alphanumeric), and tighten the minimum length to 8 in options. Documenting one set of rules in server config keeps the client messages in sync.

10. **Tests.**
    Backend integration tests use `WebApplicationFactory` against a LocalDB SQL Server test database, because SQLite or the EF in-memory provider do not reproduce SQL Server unique-index behavior. Frontend tests use the existing React Testing Library setup with a mocked `fetch`.

## Risks / Trade-offs

- [Phone format rules are not specified beyond "with country code"] → E.164 is used as the definition; adjust the validator if the team wants a stricter or looser format.
- [Cars come later] → The user type and foreign-key approach leave room for a `Car` entity without changing the registration contract.
- [Account enumeration through the duplicate-email message] → Accepted for UC1 because the spec requires telling the user the email is in use. Revisit when email confirmation is added.
- [No rate limiting on registration] → Out of scope for UC1; note it for a later hardening change.
- [Developers need SQL Server locally] → Document LocalDB or a Docker `mssql` container in the README.
- [Hand-written endpoint instead of `MapIdentityApi`] → More code to maintain, but a smaller and clearer API surface.
- [Secrets in config] → Only a LocalDB connection string (no credentials) is committed; everything else comes from user-secrets or environment variables.
