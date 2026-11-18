# Proposal

## Why

Today users can only be created through public self-registration (UC1), and there is no way for staff to correct a user's details or remove a user. UC11 "Administrer brugere" gives administrators a protected way to create, edit and delete users, including GDPR-compliant removal of personal data.

## What Changes

- Add an administrator-only API for listing, creating, editing and deleting users (`/api/admin/users`), protected by the existing `Administrator` policy and the existing antiforgery filter for state-changing requests.
- Add a "Brugere" administration page to the React front end, shown only to administrators, with a user list, a create form, an edit form and a delete confirmation.
- Create and edit validate the same details as registration (email, first name, last name, phone number with country code); create also requires a password meeting the password rules. Edit never changes the password or the roles.
- Administrators cannot assign or remove the administrator role through this feature; the role is still provisioned only via `--grant-admin`.
- Administrator accounts are protected: they are listed but cannot be edited or deleted through this feature, including by themselves.
- Deleting a user with no reservations permanently removes the user and their vehicles. Deleting a user who has reservations anonymizes the account in place (personal data removed, account disabled) so booking history and statistics are preserved without a personal reference.
- Anonymized (deleted) accounts cannot log in, are hidden from the user list, and free up their email for reuse.
- **BREAKING**: none.

## Capabilities

### New Capabilities
- `user-administration`: Administrator-only listing, creation, editing and deletion of users, including validation, authorization, protection of administrator accounts, and GDPR-oriented deletion/anonymization.

### Modified Capabilities
- `user-login`: Anonymized (deleted) accounts must never be able to log in.

## Impact

- Backend (`ParkingRealm`): new `Endpoints/UserAdministrationEndpoints.cs` and registration in `Program.cs`; `ApplicationUser` gets a deletion marker and a EF Core migration; login in `AuthEndpoints.cs` rejects deleted accounts.
- Database: new column on `AspNetUsers` (migration); no change to reservation or vehicle foreign keys.
- Frontend (`parking-realm/src`): new users page, new API helpers in `api/booking.js` (or a dedicated admin API module), navigation entry in `App.js`.
- Tests: new backend tests in `ParkingRealm.Tests` and frontend tests for the page and API helpers.
- Out of scope: role management, password reset by an administrator, audit logging, bulk operations, pagination beyond a simple bounded list.

## Assumptions

- The requirement material leaves the deletion handling of users with bookings/history unspecified; anonymization was chosen to satisfy GDPR while keeping booking statistics intact.
- User-facing text is in Danish, matching the existing UI; API messages follow the existing English conventions.
