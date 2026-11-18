# Tasks

## 1. Data model and shared validation

- [x] 1.1 Add `ApplicationUser.DeletedAt` (`DateTimeOffset?`), generate an EF Core migration, and verify the migration applies (`dotnet ef migrations` output and an updated migration test that checks the column exists)
- [x] 1.2 Extract the shared email/name/phone validation rules and the Identity error mapping / unique-violation helpers from `RegisterRequest` and `AuthEndpoints` into shared code, and verify the existing `RegisterEndpointTests` and `LoginEndpointTests` still pass unchanged

## 2. Login and session handling for deleted accounts

- [x] 2.1 Make login and `/api/auth/me` treat `DeletedAt != null` as unknown user (same 401 response), set the security-stamp validation interval to zero, and verify with new tests in `LoginEndpointTests` (deleted login rejected, session of a deleted user rejected)

## 3. User administration API

- [x] 3.1 Add `UserAdministrationEndpoints` with `GET /api/admin/users?search=` (administrator policy, `no-store`, no hashes/stamps, deleted users excluded) and register it in `Program.cs`; verify with tests for anonymous (401), ordinary user (403), admin list, search and response shape
- [x] 3.2 Add `POST /api/admin/users` (antiforgery filter, reused validation, no roles assigned, duplicate email handling); verify with tests for success + login with the new credentials, invalid/missing details, weak password, duplicate email, and missing antiforgery token
- [x] 3.3 Add `PUT /api/admin/users/{id}` with `UpdateUserRequest` (email/name/phone only, password and roles untouched, 404 for unknown/deleted, refusal for administrator accounts); verify with tests for success, invalid details, duplicate email, not found, and admin account protection
- [x] 3.4 Add `DELETE /api/admin/users/{id}`: hard delete (user + vehicles) without reservations; anonymize in place (placeholders, null password, lockout, security stamp rotation, role/login cleanup, vehicle registration numbers replaced, `DeletedAt` set) with reservations; FK-violation race falls back to anonymization; administrator accounts refused; verify with tests that no email/name/phone/hash/registration number remains, reservations and booking statistics are unchanged, the email can be reused, unknown/deleted id returns 404, and a database failure leaves no partial change

## 4. Frontend

- [x] 4.1 Add `getUsers`, `createUser`, `updateUser`, `deleteUser` helpers in `parking-realm/src/api/booking.js`, extend `bookingRequest` to send the antiforgery token for `PUT`, and add Danish error messages; verify with tests in `booking.test.js`
- [x] 4.2 Add `UsersPage.js` (search, list, create form, edit form, confirmed delete, client-side validation, API field errors, protected administrator rows) and wire a "Brugere" navigation button visible only to administrators in `App.js`; verify with `UsersPage.test.js` and `App.test.js` (button hidden for ordinary users, create/edit/delete flows, validation and failure messages, delete confirmation)

## 5. Integration check

- [x] 5.1 Run the full backend (`dotnet test`) and frontend (`npm test`) suites and verify all pass, then run `openspec validate admin-user-management` and verify it reports no errors
