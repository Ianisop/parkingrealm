# Design

## Context

UC1 is implemented: `ParkingRealm/Program.cs` registers `ApplicationDbContext` and `AddIdentityCore<ApplicationUser>` (unique email, password length 8), and `Endpoints/AuthEndpoints.cs` maps `POST /api/auth/register`. There is no authentication scheme, no `SignInManager`, and no `UseAuthentication`/`UseAuthorization` yet. The CORS policy `ReactDev` allows `http://localhost:3000` without credentials. The React app (`parking-realm/`) shows only `RegisterPage`; there is no router. Error responses use RFC 7807 problem details, and the UI is in Danish while API messages are English and translated in `src/api/auth.js`. See proposal.md for scope and specs/user-login/spec.md for behavior. The user chose cookie login and no roles for now.

## Goals / Non-Goals

**Goals:**
- Login with email and password using the existing Identity user store, keeping the user logged in with a secure cookie.
- One protected endpoint that proves login works and lets the UI show the current user.
- API behavior that does not reveal whether an email is registered.

**Non-Goals:**
- Logout, remember-me, lockout, rate limiting, password reset, email confirmation.
- Roles, policies beyond "authenticated user", JWT/bearer tokens.
- A router or global state library in React.

## Decisions

1. **Identity cookie authentication, not JWT.**
   Register `AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies()` and add `.AddSignInManager()` to the existing `AddIdentityCore<ApplicationUser>()`. The cookie is HttpOnly, so React never touches the credential, which avoids storing tokens in `localStorage`. Alternative: JWT (more code, token storage and refresh decisions). Rejected by the user's choice and for being broader than UC2. Alternative: `MapIdentityApi`. Rejected for the same reason as in UC1: it exposes far more than UC2 needs and fixes the response shapes.

2. **Login endpoint `POST /api/auth/login` with `{ email, password }`.**
   Validate that both fields are present (`400` `ValidationProblem` with per-field errors, same shape as register). Look up the user with `UserManager.FindByEmailAsync` (case-insensitive through `NormalizedEmail`), then `SignInManager.CheckPasswordSignInAsync`, and on success call `SignInManager.SignInAsync(user, isPersistent: false)`. Unknown email and wrong password return the identical `401` problem response with one generic message, so the API does not reveal which emails are registered. Success returns `200` with `{ id, email, firstName, lastName }` and `Set-Cookie`; no hash or password data. `lockoutOnFailure` is `false` (see Risks).

3. **Current-user endpoint `GET /api/auth/me`, protected with `RequireAuthorization()`.**
   Returns the same `{ id, email, firstName, lastName }` for the cookie's user. Not logged in returns `401`. The React app calls it on load to restore login state and after login to show the user. It is also the first "user function" the specs refer to. Authorization is the default "authenticated user" policy; no roles or custom policies, so every logged-in user has ordinary user rights and the login state carries no role claims.

4. **API-friendly cookie behavior.**
   Configure the application cookie so that an unauthenticated or forbidden API call returns `401`/`403` instead of redirecting to a login page: set `OnRedirectToLogin` and `OnRedirectToAccessDenied` to write the status code. Cookie settings: `HttpOnly`, `SameSite=Lax`, `SecurePolicy` `SameAsRequest` in Development (plain `http://localhost`) and `Always` otherwise, non-persistent session cookie with an 8-hour sliding expiration. Add `UseAuthentication()` and `UseAuthorization()` after `UseCors` and before the endpoints.

5. **CORS with credentials.**
   Extend the Development-only `ReactDev` policy with `AllowCredentials()`. It already names an explicit origin, which is required when credentials are allowed. The React `fetch` calls use `credentials: 'include'`. `http://localhost:3000` and `http://localhost:5198` are the same site (the port is ignored), so `SameSite=Lax` cookies are sent.

6. **CSRF.**
   The API only accepts JSON bodies for state-changing calls and the cookie is `SameSite=Lax`, which blocks cross-site form posts. No anti-forgery token for now; revisit if non-JSON or cross-site endpoints appear.

7. **Failure handling.**
   Reuse `UseExceptionHandler()`/problem details from UC1, so a database failure during login returns a generic `500` without details and the user stays logged out.

8. **Frontend: no router, a small view switch.**
   `App.js` holds the current user (from `GET /api/auth/me` on load) and a `view` state. Not logged in: buttons switch between `RegisterPage` and a new `LoginPage`. Logged in: a view with a greeting such as "Velkommen, Anna Hansen". `src/api/auth.js` gets `login(email, password)` and `getCurrentUser()`; all calls use `credentials: 'include'`. `LoginPage` mirrors `RegisterPage`: controlled inputs, client-side required check, Danish messages, a `role="alert"` message for failures. The `401` response maps to "Email eller adgangskode er forkert.", other failures to "Login mislykkedes. Prøv igen senere." Alternative: add React Router. Rejected for now because there are only two views; revisit when more pages arrive.

9. **Tests.**
   Backend integration tests with `WebApplicationFactory` against the LocalDB test database (as in UC1), using a client that keeps cookies: successful login then `GET /api/auth/me` returns the user, wrong password and unknown email return identical `401` bodies, missing fields return `400`, `GET /api/auth/me` without login returns `401` with no redirect, email in different letter case logs in, the login state has no role claims, and an unreachable database returns a generic `500` and no cookie. Frontend tests use React Testing Library with a mocked `fetch`/API module.

## Risks / Trade-offs

- [No lockout or rate limiting, so login can be brute-forced] → Explicitly out of scope for UC2; track as a follow-up before production. Password rules from UC1 reduce, but do not remove, the risk.
- [Cookie login assumes React and API are on the same site] → Fine for local development. A deployment on different sites needs `SameSite=None; Secure` plus CORS with credentials, or hosting both under one site/reverse proxy. Documented in README.
- [Timing differences could still hint whether an email exists] → The response body is identical; equalizing timing is not attempted for UC2.
- [No logout, so a login lasts until the 8-hour expiry or the browser session ends] → Logout is a natural next use case; the cookie is non-persistent.
- [`Secure` cookies are not used on plain-HTTP Development] → Only affects local development; production uses `Always`.
