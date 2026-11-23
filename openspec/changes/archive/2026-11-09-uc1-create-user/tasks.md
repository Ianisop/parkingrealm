# Tasks

## 1. Backend data layer

- [x] 1.1 Add NuGet packages `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer` and `Microsoft.EntityFrameworkCore.Design` to `ParkingRealm/ParkingRealm.csproj`, then verify `dotnet build ParkingRealm` succeeds
- [x] 1.2 Create `ApplicationUser : IdentityUser` with required `FirstName` and `LastName` (phone uses the inherited `PhoneNumber`) and `ApplicationDbContext : IdentityDbContext<ApplicationUser>`, and verify the project builds
- [x] 1.3 Add `DefaultConnection` (LocalDB) to `appsettings.Development.json`, register the DbContext with SQL Server and `AddIdentityCore<ApplicationUser>` (unique email, password min length 8) in `Program.cs`, then verify the app starts
- [x] 1.4 Create the `InitialIdentity` migration, run `dotnet ef database update`, and verify the `AspNetUsers` table exists with a unique index on `NormalizedEmail` and non-null `FirstName`, `LastName` columns; verify no car table is created

## 2. Registration endpoint

- [x] 2.1 Add `POST /api/auth/register` (`{ email, password, firstName, lastName, phoneNumber }`) that requires all fields, validates email format and E.164 phone number (country code), calls `UserManager.CreateAsync`, and returns `201` without password data; verify with a `.http` request that a valid user is created with all fields stored
- [x] 2.2 Map Identity errors (password rules, duplicate email) and request validation failures to a `400` `ValidationProblem` with per-field errors, and verify with `.http` requests for each case
- [x] 2.3 Add `AddProblemDetails()`/`UseExceptionHandler()` so unexpected and database failures return a generic `500` with no exception details, and handle a unique-index `DbUpdateException` race as duplicate email
- [x] 2.4 Add a CORS policy for `http://localhost:3000` in Development only and verify a browser preflight from the React dev server succeeds
- [x] 2.5 Add a backend test project using `WebApplicationFactory` against a LocalDB test database covering: successful registration (all fields stored, password stored hashed), each missing field, malformed email, phone number without country code, weak password, duplicate email in different letter case, and database failure returning `500` with no leak; verify `dotnet test` passes

## 3. React registration page

- [x] 3.1 Add `parking-realm/src/api/auth.js` with a `register({ email, password, firstName, lastName, phoneNumber })` call using `fetch` and `REACT_APP_API_URL`, returning parsed field errors for `400` and a generic error for network/`5xx` failures; verify with unit tests using a mocked `fetch`
- [x] 3.2 Build `RegisterPage` with inputs for email, password, first name, last name and phone number, client-side required/email/phone-country-code/password-length checks, field-level error display, success message, and a generic failure message; verify it renders from `App.js`
- [x] 3.3 Add React Testing Library tests for: form renders all five fields, empty and invalid input (including phone without country code) blocked client-side, server field errors shown (including duplicate email), success message shown, and failure message shown on network/`5xx`; verify `npm test` in `parking-realm` passes

## 4. Documentation and integration

- [x] 4.1 Update `README.md` with SQL Server/LocalDB prerequisites, how to run migrations, how to start the API and React app, and the `REACT_APP_API_URL` setting; verify the documented commands work as written on a clean checkout
- [x] 4.2 Manually run the full flow end to end (valid registration, each rejection case, duplicate email, and database stopped) against the acceptance criteria in `specs/user-registration/spec.md`, and verify the results match
