# Design

## Context

Users are ASP.NET Core Identity `ApplicationUser` rows (`FirstName`, `LastName`, `PhoneNumber`, email as username). Registration validation lives in `RegisterRequest.Validate()`. The `Administrator` role is provisioned via `--grant-admin`, and an `Administrator` authorization policy already guards `/api/admin/booking-statistics`. State-changing booking endpoints use `BookingAntiforgeryFilter` with a token from `/api/antiforgery`. `Reservation.OwnerId` and `Vehicle.OwnerId` reference users with `DeleteBehavior.Restrict`, so a user row with reservations cannot be physically deleted. The cookie is validated by Identity's security-stamp validator (default interval 30 minutes). See proposal.md for motivation and scope.

## Goals / Non-Goals

**Goals:**
- Administrator-only CRUD-style API and UI for users, reusing existing validation, policy and antiforgery patterns.
- GDPR-aware deletion that keeps booking history/statistics intact.

**Non-Goals:**
- Role management, admin-initiated password reset, audit log, pagination/bulk operations, changing reservation/vehicle foreign keys.

## Decisions

**Endpoints.** New `UserAdministrationEndpoints` in a group `/api/admin/users` with `.RequireAuthorization("Administrator")`:
- `GET ?search=` – list non-deleted users (capped, e.g. 200, ordered by last/first name), projection excludes hashes/stamps; `Cache-Control: no-store`.
- `POST` – create; `PUT /{id}` – edit; `DELETE /{id}` – delete. The three state-changing routes add `BookingAntiforgeryFilter`.
Alternative considered: a single `/api/users` group with per-endpoint role checks; rejected to keep the admin boundary uniform with booking statistics.

**Validation reuse.** Create reuses `RegisterRequest` and its `Validate()`, and the Identity error mapping and duplicate-email handling of `AuthEndpoints` (extract the shared helpers `MapIdentityErrors`, `IsUniqueViolation`, `DuplicateEmail` to an internal shared class rather than duplicating). Edit uses a new `UpdateUserRequest(Email, FirstName, LastName, PhoneNumber)` that reuses the same field rules (extract the email/name/phone rules to shared static validators used by both records so rules cannot drift). Email changes update both `Email` and `UserName` through `UserManager.SetEmailAsync`/`SetUserNameAsync`, or a single `UpdateAsync` with normalized values; duplicates surface as a validation problem (`email`).

**Admin protection.** For edit and delete, look up the target and check `IsInRoleAsync(Administrator)`; if true return `403`-style problem (`Results.Problem`, 403) and change nothing. This also covers self-edit/self-delete. Created users get no roles.

**Deleted marker.** Add `ApplicationUser.DeletedAt` (`DateTimeOffset?`) with a migration. All lookups for the admin API and for login filter on `DeletedAt == null`. A deleted user is treated as "not found" (404) by edit/delete.

**Deletion strategy.** In a single transaction (one `SaveChanges`, or an explicit transaction where Identity calls are involved):
1. No reservations → remove the user's vehicles and the user (`UserManager.DeleteAsync`), which also removes role/claim/login rows via Identity cascade.
2. Has reservations → anonymize in place: set `Email`/`UserName` (and normalized values) to a unique non-identifying placeholder (`deleted-<guid>@deleted.invalid`), `FirstName`/`LastName` to a neutral value (e.g. `Slettet`/`Bruger`), `PhoneNumber` to null, `PasswordHash` to null, `EmailConfirmed=false`, lock out permanently (`LockoutEnd = DateTimeOffset.MaxValue`, `LockoutEnabled = true`), rotate the security stamp, set `DeletedAt`, replace each vehicle's `RegistrationNumber`/`NormalizedRegistrationNumber` with unique placeholders (the `(OwnerId, NormalizedRegistrationNumber)` unique index still applies), and remove roles/logins/claims/tokens. Reservations and vehicles stay so statistics are unchanged.
The reservation check happens inside the transaction; the restrictive FK (SQL error 547) is caught as a race and falls back to the anonymize path, mirroring the vehicle delete endpoint. A database failure returns an unhandled-error problem via the existing exception handler and the transaction rolls back (no partial state).
Alternatives: blocking deletion with 409 (conflicts with GDPR erasure); cascading delete of reservations (destroys statistics) — both rejected after user decision.

**Login and sessions.** `LoginAsync` treats `DeletedAt != null` like an unknown user (same 401). Because anonymized users have a null password hash and lockout the password check already fails, but the explicit check avoids any path leaking existence. Set `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero` so a rotated stamp invalidates an existing session on the next request; hard-deleted users already fail `GetUserAsync`. `GetCurrentUserAsync` also returns 401 for a deleted user.

**Responses.** Success: `201` with user body for create, `200` for edit, `204` for delete; `404` for unknown/deleted ids; `400` validation problem; `403` for protected administrator accounts. Response user shape: `id, email, firstName, lastName, phoneNumber, isAdministrator`.

**Frontend.** New `UsersPage.js` rendered from `App.js` when `user.isAdministrator && view === 'users'`, with a "Brugere" nav button only for administrators. Contains search box, list (with edit/delete disabled for administrators), create/edit form with client-side validation matching the server and field errors from the API, and a `window.confirm`-style confirmation before delete. API helpers (`getUsers`, `createUser`, `updateUser`, `deleteUser`) added to `api/booking.js` using `bookingRequest`, extending it to handle the `PUT` method with the antiforgery token and adding Danish error messages for `/api/admin/users`.

**Tests.** Backend tests in `ParkingRealm.Tests` following the `ApiFactory` pattern (authorization, validation, duplicate email, edit, admin protection, delete with/without reservations, anonymization leaves no personal data, deleted login rejected). Frontend tests for `UsersPage` and API helpers.

## Risks / Trade-offs

- [Zero security-stamp validation interval adds a DB read per authenticated request] → Acceptable at this scale; endpoints already load the user.
- [Anonymized rows remain in `AspNetUsers`] → Contain no personal data; hidden from all lists; documented as the GDPR approach for retained booking history.
- [Race between reservation check and delete] → Handled by catching FK violation and falling back to anonymization.
- [Refactoring shared validation/error helpers out of `AuthEndpoints`/`RegisterRequest` touches registration] → Existing registration tests must stay green.
- [Email can be changed without verification] → Out of scope; no email confirmation exists in the system today.
