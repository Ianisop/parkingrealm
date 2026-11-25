# Design

## Context

See proposal.md for motivation. Current state:

- Auth is ASP.NET Core Identity (`AddIdentityCore<ApplicationUser>` + cookies) with minimal-API endpoints in `AuthEndpoints.cs`; login and registration are public and do not use antiforgery, admin endpoints use the `Administrator` policy plus `BookingAntiforgeryFilter`.
- `AddIdentityCore` is registered **without** `AddDefaultTokenProviders()`, so no token provider exists yet.
- The security stamp is validated on every request (`ValidationInterval = TimeSpan.Zero`), so changing the stamp ends sessions immediately.
- Deleted accounts are anonymized rows with `DeletedAt` set; `FindActiveAsync` in `UserAdministrationEndpoints.cs` and the login check already exclude them.
- The React front end has no router (a `view` state in `App.js`) and no test setup. UI text is Danish.

## Goals / Non-Goals

**Goals:**
- A demonstrable, secure reset flow whose only simulated part is the e-mail transport.
- No database migration and no new library.

**Non-Goals:**
- Real e-mail, SMS or other verification channels; rate limiting or lockout; audit log.
- Making earlier reset links unusable when a new one is issued (see Decision 2).

## Decisions

1. **Use Identity's built-in reset tokens (`GeneratePasswordResetTokenAsync` / `ResetPasswordAsync`).** Add `.AddDefaultTokenProviders()` and set `DataProtectionTokenProviderOptions.TokenLifespan` to 30 minutes (read from `PasswordReset:TokenLifespanMinutes`, default 30, so tests can force expiry). Tokens are data-protection-signed, bound to user id and security stamp, stateless, and cover expiry, tamper detection, wrong-account and single use (a successful reset changes the stamp, invalidating the token and all sessions).
   *Alternative:* own table with hashed token + expiry + used flag. Rejected: needs a migration and hand-rolled crypto/lifetime logic for no extra demonstrated value.
   *Consequence:* tokens depend on the data-protection key ring; keys are per-machine by default, so a reset link issued before an app restart on a different key ring would fail (acceptable for the demo; the user simply requests a new link).

2. **Do not invalidate earlier links on a new request.** Doing so would require rotating the security stamp on every request, letting anyone log a victim out by submitting their e-mail (and an admin-triggered link would end the user's sessions without the password being reset). The acceptance criteria only require expiry and non-reuse, which Decision 1 provides.

3. **Simulated e-mail behind `IPasswordResetSender`.** `SendAsync(ApplicationUser user, string resetLink, CancellationToken)`; the registered implementation (`LoggingPasswordResetSender`) writes the link to `ILogger` at Information. A real SMTP sender can later replace it with no endpoint change. The link is `{PasswordReset:BaseUrl}/?resetToken=<base64url token>&email=<url-encoded e-mail>`, with `BaseUrl` defaulting to `http://localhost:3000` (the React dev origin). Tests replace the sender with a capturing fake to read the token.
   *Alternative:* return the token/link in the API response. Rejected: it would let anyone reset any account, defeating identity verification.

4. **Shared issuing logic.** One helper (`PasswordResetService.IssueAsync(user)`) generates the token, base64url-encodes it (`WebEncoders`), builds the link and calls the sender. Both the public forgot endpoint and the admin endpoint call it, so behaviour is identical.

5. **Endpoints.**
   - `POST /api/auth/forgot-password` `{ email }` (public, like login/register, no antiforgery): validate e-mail format (400 on missing/malformed); look up the user; if active, issue the link; always return `200` with the same body. Deleted/unknown accounts return the same response with no link.
   - `POST /api/auth/reset-password` `{ email, token, newPassword }` (public; the UI additionally checks the confirmation field): 400 on missing fields; find active user by e-mail; decode the token; call `ResetPasswordAsync`. Identity errors with code `Password*` map to a `password` validation error (reusing `UserValidation.MapIdentityErrors`); `InvalidToken` (and unknown/deleted user, undecodable token) all map to one `400` problem "reset link is invalid or expired", so the response does not reveal whether the account exists. A password rejected by validation does not consume the token (the stamp only changes on success).
   - `POST /api/admin/users/{id}/password-reset` in the existing admin group (`Administrator` policy, no-store header, `BookingAntiforgeryFilter`): `FindActiveAsync` → 404 if missing/deleted; otherwise issue the link and return `204`. Administrator accounts are allowed (unlike edit/delete) because only a link is sent.
   Unexpected exceptions (database down, save failure) fall through to the existing `UseExceptionHandler` and give a generic 500 problem with no details; `ResetPasswordAsync` updates the user in a single save, so no partial change occurs.

6. **Password rules** come from the existing Identity options (`RequiredLength = 8` plus Identity defaults), the same as registration. The password is never logged.

7. **Front end.** No router is introduced.
   - `App.js` reads `resetToken` and `email` from `window.location.search` on load; if present, the logged-out area shows `ResetPasswordPage`. After a successful reset the query string is removed (`history.replaceState`) and the user is sent to the login view with a success notice.
   - `LoginPage` gets a "Glemt adgangskode?" button that switches the logged-out view to a new `ForgotPasswordPage` (email field; on submit always shows "Hvis e-mailen findes, er der sendt en e-mail med et link til at nulstille adgangskoden." — a neutral message that does not leak existence).
   - `UsersPage` gets a "Send nulstillingslink" button on every row (including administrators), calling the admin endpoint with the existing antiforgery helper and showing a status message.
   - New helpers `forgotPassword`, `resetPassword` in `api/auth.js` and `sendPasswordReset` in `api/booking.js` (where the other admin/CSRF helpers live), following the existing result-object pattern and Danish messages.

## Risks / Trade-offs

- **[Reset link in the server log is a credential]** → This is the deliberate simulation of e-mail; document it, log only at Information from the replaceable sender, and never return the token via the API. A production sender must replace it.
- **[Public endpoints can be spammed / password-guessing of tokens]** → Tokens are signed and bound to the account; no rate limiting is added (out of scope, noted for production).
- **[Timing difference between existing and unknown e-mail]** → Minor with the logging sender; accepted for the demo, to be revisited with a real mail sender (send asynchronously).
- **[No new-link-supersedes-old behaviour]** → Accepted (Decision 2); links still expire in 30 minutes and die on first successful reset.
- **[Data-protection key ring changes invalidate outstanding links]** → Users request a new link; acceptable.
- **[Frontend has no automated tests]** → Backend behaviour is covered by integration tests; the new views are verified manually.
