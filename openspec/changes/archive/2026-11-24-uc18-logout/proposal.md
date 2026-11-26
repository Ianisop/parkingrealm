# Proposal

## Why

UC18 "Log ud": a logged-in user (ordinary user or administrator) can log in (UC2) but has no way to end the session. The login cookie lives for 8 hours with sliding expiry, so on a shared computer the next person keeps the previous user's access. Users need an explicit way to end their session and leave the system.

## What Changes

- Add an API endpoint `POST /api/auth/logout` that ends the current session by clearing the login cookie. It works the same for ordinary users and administrators.
- Logout is idempotent: if the session has already expired or the caller is not logged in, the endpoint still succeeds, so the user ends up logged out and sent to the login page.
- Add a "Log ud" button in the logged-in layout of the React app (visible to both ordinary users and administrators). On success the app clears its user state and shows the login page; if the logout request fails, the user is told and stays logged in.
- After logout, protected functions answer 401 until the user logs in again.

## Capabilities

### New Capabilities
- `user-logout`: A logged-in user or administrator can end their session; afterwards protected functions are unavailable until a new login, and the user is shown the login page (UC18).

### Modified Capabilities
<!-- None: existing user-login requirements are unchanged. -->

## Impact

- Backend: [AuthEndpoints.cs](../../../ParkingRealm/Endpoints/AuthEndpoints.cs) (new logout endpoint using `SignInManager.SignOutAsync`).
- Frontend: `parking-realm/src/api/auth.js` (new `logout()`), `parking-realm/src/App.js` (logout button, state reset).
- Tests: new logout tests alongside `LoginEndpointTests` in `ParkingRealm.Tests`.
- No database, dependency, or migration changes. No change to roles or existing endpoints.
- Out of scope: revoking other sessions of the same user on other devices, "log out everywhere", and automatic logout on inactivity.
