# Tasks

## 1. Token provider and simulated e-mail

- [x] 1.1 In `ParkingRealm/Program.cs` add `.AddDefaultTokenProviders()` to Identity and configure `DataProtectionTokenProviderOptions.TokenLifespan` from `PasswordReset:TokenLifespanMinutes` (default 30); add `PasswordReset:BaseUrl` (default `http://localhost:3000`) to `appsettings.json`. Verify the app builds (`dotnet build ParkingRealm/ParkingRealm.slnx`) and existing login/register tests still pass.
- [x] 1.2 Add `IPasswordResetSender` + `LoggingPasswordResetSender` (logs the link at Information, never a password) and `PasswordResetService.IssueAsync(user)` (generate token, base64url-encode, build `{BaseUrl}/?resetToken=..&email=..`, call sender); register in DI. Add a capturing fake sender to `ParkingRealm.Tests/ApiFactory.cs`. Verify the build succeeds and a test using the fake can read the issued link.

## 2. Self-service endpoints (backend)

- [x] 2.1 Add `Endpoints/PasswordResetEndpoints.cs` with `POST /api/auth/forgot-password` (400 for missing/malformed e-mail; identical 200 for existing, unknown and deleted accounts; link issued only for active accounts) and register it in `Program.cs`. Verify with new tests in `ParkingRealm.Tests/PasswordResetEndpointTests.cs`: existing e-mail issues a link, unknown and deleted e-mails return an identical response with no link, invalid e-mail returns 400, response never contains the token, administrator can request.
- [x] 2.2 Add `POST /api/auth/reset-password` (400 for missing fields; password errors mapped via `UserValidation.MapIdentityErrors`; invalid/expired/used/tampered/wrong-account token, unknown and deleted user all give the same "invalid or expired" 400). Verify with tests: successful reset allows login with the new password and rejects the old one; mismatched account/tampered token rejected; password violating the rules is rejected and the same link then still works with a valid password; reuse of a used link rejected; an expired link rejected (factory sets `PasswordReset:TokenLifespanMinutes` so tokens expire immediately); an active session is rejected after the reset; the stored hash is not the plain-text password.
- [x] 2.3 Verify the failure path: with the unreachable-database factory, forgot-password and reset-password return a generic 500 problem without technical details (extend `PasswordResetEndpointTests.cs`).

## 3. Administrator trigger (backend)

- [x] 3.1 Add `POST /api/admin/users/{id}/password-reset` to the admin group in `UserAdministrationEndpoints.cs` (404 for missing/deleted user, allowed for administrator accounts, 204 on success, no token in the response, antiforgery enforced). Verify with new tests in `UserAdministrationTests.cs`: link issued for ordinary and administrator accounts, 404 for unknown/deleted user, 401 anonymous, 403 ordinary user, rejected without antiforgery token, the user's old password still works until the reset is completed, and the issued link completes a reset.

## 4. Front end

- [x] 4.1 Add `forgotPassword` and `resetPassword` helpers to `parking-realm/src/api/auth.js` and `sendPasswordReset` to `parking-realm/src/api/booking.js` (using the existing antiforgery handling), with Danish messages for neutral success, invalid/expired link and password-rule errors. Verify with `npm run build` in `parking-realm`.
- [x] 4.2 Create `ForgotPasswordPage.js` (e-mail field, client-side check for missing e-mail, neutral confirmation message) and add a "Glemt adgangskode?" button on `LoginPage.js`/`App.js` that opens it. Verify manually: the link opens the form and submitting shows the confirmation for both a registered and an unregistered e-mail.
- [x] 4.3 Create `ResetPasswordPage.js` (new password + confirmation, mismatch and password-rule errors, invalid/expired link message) and make `App.js` show it when `resetToken` and `email` are in the URL; after success clear the query string and show the login view with a success notice. Verify manually end to end: request a reset, copy the link from the server log, set a new password, log in with it, and confirm the same link now shows the invalid/expired message.
- [x] 4.4 Add a "Send nulstillingslink" button for every row (including administrators) in `UsersPage.js` with a status/error message. Verify manually as an administrator that the button works for an ordinary user and an administrator account, shows a confirmation, and that the link appears in the server log.

## 5. Final checks

- [x] 5.1 Run the full backend test suite (`dotnet test ParkingRealm/ParkingRealm.slnx` or the tests project) and `openspec validate uc21-reset-password --strict`; verify everything is green and no password or token is returned by any API response.
