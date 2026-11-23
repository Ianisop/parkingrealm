# Proposal

## Why

UC1 lets a person create a profile, but nobody can use it yet. UC2 "Log ind" lets a registered, not-yet-logged-in user prove who they are with the email and password from registration and get access to the user functions. Every later user feature (cars, parking) needs to know who is calling.

## What Changes

- Add a login endpoint to the minimal API that verifies email and password against the Identity user store and, on success, establishes a logged-in state with a secure cookie (ASP.NET Core Identity cookie authentication).
- Add a protected "current user" endpoint, the first endpoint that requires a logged-in user. It lets the UI show who is logged in and keeps the login state across page reloads.
- Reject missing or wrong credentials without granting access, and without revealing whether the email exists. Tell the user when login fails, including on technical errors.
- Logged-in users get ordinary user rights only. There are no roles yet; every registered user is an ordinary user.
- Add a login page to the React frontend and a simple way to switch between "Opret bruger" and "Log ind", plus a logged-in view that greets the user.
- Allow the React dev origin to send cookies to the API (CORS with credentials, Development only).

Out of scope:
- Logout, "remember me", password reset, email confirmation.
- Account lockout and rate limiting of login attempts.
- Roles and permissions (for example admin).
- Cars and parking features.
- Auto-login after registration.

## Capabilities

### New Capabilities
- `user-login`: A registered user can log in with email and password through the UI. Valid credentials establish a logged-in state with ordinary user rights. Missing or invalid credentials are rejected without access, and the user is told when login fails.

### Modified Capabilities
<!-- None: user-registration requirements do not change. -->

## Impact

- **Backend** (`ParkingRealm/`): Identity cookie authentication and `SignInManager` registered in `Program.cs`, authentication and authorization middleware, a login endpoint and a protected current-user endpoint in the auth endpoints, CORS change for credentials. No database migration: the Identity tables from UC1 are reused.
- **Backend tests** (`ParkingRealm.Tests/`): new integration tests for login and the protected endpoint.
- **Frontend** (`parking-realm/`): new login page, API client calls that send cookies, and a small view switch in `App.js`.
- **APIs**: adds `POST /api/auth/login` and `GET /api/auth/me`. The cookie becomes the contract later protected endpoints rely on.
- **Deployment note**: cookie login works when the React app and the API are on the same site; a different-site deployment needs extra cookie settings (see design.md).
