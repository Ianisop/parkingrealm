# Tasks

## 1. Backend logout endpoint

- [x] 1.1 In `ParkingRealm/Endpoints/AuthEndpoints.cs`, map `POST /api/auth/logout` (no `RequireAuthorization`, no antiforgery filter) that calls `SignInManager.SignOutAsync()` and returns 204; verify with `dotnet build ParkingRealm/ParkingRealm.csproj`
- [x] 1.2 Add logout tests to `ParkingRealm.Tests` (new `LogoutEndpointTests.cs` modelled on `LoginEndpointTests`): ordinary user logs out → 204, then `/api/auth/me` and a protected endpoint return 401; administrator logs out → admin-only endpoint returns 401 afterwards; logout without a session and a repeated logout both return 204; logging in again after logout works; the response clears the Identity cookie. Verify with `dotnet test ParkingRealm.Tests --filter LogoutEndpointTests`

## 2. Frontend logout

- [x] 2.1 Add `logout()` to `parking-realm/src/api/auth.js` (`POST /api/auth/logout` with `credentials: 'include'`, returns `{ ok }`, treats network errors and non-2xx as failure) plus a Danish failure message constant; verify with `npm run build` in `parking-realm`
- [x] 2.2 In `parking-realm/src/App.js`, add a "Log ud" button to the logged-in header (visible for both ordinary users and administrators); on success call `setUser(null)` and `setView('login')`; on failure show the Danish error with `role="alert"` and keep the user logged in; verify manually in the browser as ordinary user and as administrator (login page shown, reload still shows login page, failure message appears when the API is stopped)

## 3. Integration check

- [x] 3.1 Run the full backend suite (`dotnet test ParkingRealm.Tests`) and `openspec validate uc18-logout --strict` and verify both pass; confirm the existing login tests are unaffected
