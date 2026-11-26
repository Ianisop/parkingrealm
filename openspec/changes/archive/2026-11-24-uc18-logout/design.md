# Design

## Context

Authentication is cookie-based ASP.NET Core Identity (`IdentityConstants.ApplicationScheme`), configured in [Program.cs](../../../ParkingRealm/Program.cs): HttpOnly cookie, 8 hours sliding expiry, and 401/403 instead of redirects (it is an API). Login is `POST /api/auth/login` in [AuthEndpoints.cs](../../../ParkingRealm/Endpoints/AuthEndpoints.cs), which calls `SignInManager.SignInAsync`. The React app (`parking-realm/src/App.js`) holds the user in state, restores it from `GET /api/auth/me` on load, and shows `LoginPage` when there is no user. State-changing booking/vehicle/admin POST/PUT/DELETE endpoints use `BookingAntiforgeryFilter`, with the token fetched from `/api/antiforgery` (which itself requires login). No logout exists. See proposal.md for motivation.

## Goals / Non-Goals

**Goals:**
- One logout path shared by ordinary users and administrators (no role-specific code).
- Idempotent logout that never fails because the session already expired.
- UI returns to the login page on success and reports failure without pretending the user is logged out.

**Non-Goals:**
- Revoking the same user's sessions on other devices/browsers, or logout on inactivity.
- Changing cookie lifetime, roles, or the login flow.

## Decisions

**1. `POST /api/auth/logout`, anonymous-callable, returns 204.**
It calls `SignInManager.SignOutAsync()`, which deletes the Identity cookie, and returns `204 No Content`. It does NOT use `RequireAuthorization()`: with an expired session that would return 401 and the user could not get "logged out" cleanly (the spec requires success). Alternative considered: require authorization and have the UI treat 401 as success - rejected as it spreads the exception into every client.

**2. No antiforgery token on logout.**
`/api/antiforgery` requires login, so it cannot be fetched with an expired session, which would break decision 1. Risk is low: the auth cookie is `SameSite=Lax`, so cross-site POSTs do not carry it, and a forced logout only costs the user a re-login. Alternative considered: `BookingAntiforgeryFilter` - rejected for the reason above.

**3. POST only.**
A GET logout could be triggered by an image tag or link prefetch; POST with `SameSite=Lax` avoids that.

**4. Cookie removal, not server-side session revocation.**
Identity cookie sessions are stateless, so logout clears the cookie of this browser only. A copy of the old cookie would remain valid until expiry or until the security stamp changes. Rotating the security stamp (`UpdateSecurityStampAsync`) would end all of the user's sessions on every device, which exceeds "the active session" in UC18 and was left out (see Non-Goals). Can be revisited if stricter revocation is wanted.

**5. Frontend: `logout()` in `api/auth.js`, button in `App.js` header.**
`logout()` calls `fetch(API_URL + '/api/auth/logout', { method: 'POST', credentials: 'include' })` and returns `{ ok }`; network errors and non-2xx responses return `{ ok: false }`. On `ok`, `App` calls `setUser(null)` and `setView('login')`, which renders `LoginPage` (this is the "redirect"; the app has no router). On failure it shows a Danish message (e.g. "Log ud mislykkedes. Prøv igen senere.") with `role="alert"` and keeps the user. The button is placed in the existing header next to the role badge, so it is visible for both roles. Other pages keep their own state only while mounted, so unmounting on logout leaves no previous user's data visible.

## Risks / Trade-offs

- [Stolen cookie copy stays valid until expiry] → Accepted; HttpOnly + Secure cookie limits theft; stamp rotation is a possible follow-up.
- [Forced logout via cross-site request] → SameSite=Lax blocks cookie-bearing cross-site POSTs; impact is only a re-login.
