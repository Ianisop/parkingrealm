# Proposal

## Why

A user who has forgotten their password is locked out: there is no way to regain access (UC21 "Nulstil brugeradgangskode"). The project is an exam project, so real e-mail delivery is out of scope; the goal is to demonstrate the reset flow and its security properties (identity verification, expiring single-use requests, safe storage) while *simulating* the e-mail.

## What Changes

- Add a public "Glemt adgangskode?" flow: the user enters their e-mail on the login page and is always told that "an e-mail with a reset link has been sent", whether or not the e-mail belongs to an account (no account enumeration).
- Add a simulated mail step: when the e-mail belongs to an active account, the system creates a time-limited, single-use reset token and hands a reset link to a replaceable "reset mail sender". The only implementation in this change writes the link to the server log instead of sending e-mail. No SMTP/e-mail service is added.
- Add a reset page, opened from the reset link, where the user enters and confirms a new password. The token is the identity verification: only the holder of a valid token for that account can set a password.
- The new password must satisfy the same password rules as registration, is stored only as a salted hash, and invalidates the token and the account's existing sessions.
- Reset requests expire after 30 minutes and cannot be reused after they expire or after they have been used.
- Add an administrator action in the user list ("Send nulstillingslink") that triggers the same simulated reset mail for any listed account, including administrator accounts. The administrator never sees or sets the user's password or token.
- Administrator accounts can use the self-service flow for their own account.
- **BREAKING**: none.

## Capabilities

### New Capabilities
- `password-reset`: Requesting a password reset by e-mail (simulated), time-limited single-use reset tokens, setting a new validated password, and the failure cases (invalid/expired/used token, invalid password, system failure).

### Modified Capabilities
- `user-administration`: An administrator can trigger a reset link for any listed user (including administrators) without being able to read or set the password.

## Impact

- Backend (`ParkingRealm`): new `Endpoints/PasswordResetEndpoints.cs` (public forgot/reset endpoints) registered in `Program.cs`; new endpoint in `Endpoints/UserAdministrationEndpoints.cs`; new `IPasswordResetSender` abstraction with a logging implementation; enabling Identity's default token providers with a 30-minute token lifespan in `Program.cs`. No database schema change (ASP.NET Core Identity password-reset tokens are stateless and bound to the user's security stamp).
- Frontend (`parking-realm/src`): "Glemt adgangskode?" link and forgot-password view on the login screen, reset-password view opened from the link (`?resetToken=...&email=...`), "Send nulstillingslink" button in `UsersPage.js`, new helpers in `api/auth.js` / `api/booking.js`.
- Tests: new backend tests in `ParkingRealm.Tests` (forgot, reset, expiry, reuse, admin trigger). The frontend has no test setup, so its views are checked manually.
- Out of scope: real e-mail delivery, SMS/other verification channels, rate limiting/lockout, audit logging, an administrator choosing or seeing a password, changing the password while logged in.

## Assumptions

- "Verification of identity" is proof of control over the e-mail address, which is simulated by delivering the link via the server log. This is explicitly a demonstration, not production-grade delivery.
- The 30-minute validity period was chosen by the project owner.
- Because the link is only logged, the reset token is never returned in any API response.
- User-facing text is in Danish, matching the existing UI; API messages follow the existing English conventions.
