# Tasks

## 1. Backend authentication setup

- [x] 1.1 In `ParkingRealm/Program.cs`, add `AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies()`, `.AddSignInManager()` on the Identity builder, and `AddAuthorization()`; verify `dotnet build ParkingRealm` succeeds and the app still starts
- [x] 1.2 Configure the application cookie (HttpOnly, SameSite Lax, 8-hour sliding expiration, `SecurePolicy` SameAsRequest in Development and Always otherwise) and make unauthenticated/forbidden API calls return `401`/`403` instead of redirecting; verify with a request to a protected test route that the response is `401` with no `Location` header
- [x] 1.3 Add `UseAuthentication()` and `UseAuthorization()` after `UseCors` and add `AllowCredentials()` to the Development `ReactDev` CORS policy; verify a preflight from `http://localhost:3000` returns `Access-Control-Allow-Credentials: true` and a non-listed origin gets no CORS headers

## 2. Login and current-user endpoints

- [x] 2.1 Add `POST /api/auth/login` (`{ email, password }`) that returns `400` `ValidationProblem` for missing fields, and otherwise finds the user by email, checks the password, signs in a non-persistent cookie, and returns `200` with `{ id, email, firstName, lastName }`; verify with a `.http` request that a registered user gets `200` and a `Set-Cookie` header
- [x] 2.2 Return the same generic `401` problem response for a wrong password and for an unknown email, with no hint about which was wrong; verify both `.http` requests give identical bodies
- [x] 2.3 Add `GET /api/auth/me` with `RequireAuthorization()` that returns the current user's `{ id, email, firstName, lastName }`; verify it returns `200` with the login cookie and `401` without
- [x] 2.4 Add integration tests to `ParkingRealm.Tests` (cookie-keeping client, LocalDB test database) covering: login then `me` returns the user, email in different letter case logs in, wrong password and unknown email return identical `401` bodies, each missing field returns `400`, `me` without login returns `401` without redirect, the signed-in principal has no role claims, and an unreachable database returns a generic `500` with no cookie and no leaked details; verify `dotnet test` passes
- [x] 2.5 Add login and current-user requests to `ParkingRealm/ParkingRealm.http` (including the wrong-password and unauthenticated cases) and verify they run as written against the running API

## 3. React login

- [x] 3.1 Extend `parking-realm/src/api/auth.js` with `login(email, password)` and `getCurrentUser()` using `fetch` with `credentials: 'include'`, mapping `401` to a Danish "wrong email or password" result, `400` to per-field errors, and network/`5xx` to a general failure; verify with unit tests using a mocked `fetch`
- [x] 3.2 Build `LoginPage` with email and password inputs, a client-side required check, Danish field and failure messages, and a submit state; verify it renders and blocks an empty submit
- [x] 3.3 Update `App.js` to restore the login state from `getCurrentUser()` on load, switch between `RegisterPage` and `LoginPage` when not logged in, and show a greeting with the user's name when logged in; verify with component tests that each state renders
- [x] 3.4 Add React Testing Library tests for: login form renders both fields, empty input is blocked, successful login shows the greeting, wrong credentials show "Email eller adgangskode er forkert.", a general failure shows the failure message, and an existing session shows the greeting on load; verify `npm test` in `parking-realm` passes
- [x] 3.5 Update `README.md` with how login works (cookie, `credentials: 'include'`), the same-site requirement for deployment, and the new endpoints; verify the documented steps work as written

## 4. Integration

- [x] 4.1 Manually run the full flow end to end in the browser with both servers running (register, log in, reload the page and stay logged in, wrong password, unknown email, empty fields, database stopped) against `specs/user-login/spec.md`, and verify the results match
