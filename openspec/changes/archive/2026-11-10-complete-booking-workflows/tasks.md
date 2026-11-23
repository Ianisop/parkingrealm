## 1. Storage and Prerequisites

- [x] 1.1 Add EF Core `ParkingSpace`, `Vehicle`, and `Reservation` entities with ownership, coordinate uniqueness, UTC interval and booking status mapping; verify model tests enforce relationships and uniqueness.
- [x] 1.2 Generate an additive SQL Server migration for booking tables and indexes; verify `dotnet ef database update` applies to an existing identity database and `dotnet build` succeeds.
- [x] 1.3 Add operator-only parking inventory provisioning instructions/input with unique stable coordinates; verify a configured layout loads and an empty layout produces no bookable spaces.
- [x] 1.4 Implement authenticated vehicle registration and listing with normalization, owner filtering and duplicate validation; verify LocalDB integration tests cover anonymous, invalid, duplicate, and cross-user requests.

## 2. Availability and Criteria

- [x] 2.1 Implement interval parsing/UTC validation shared by availability and booking requests; verify tests cover missing, past, zero-length, invalid-offset, and valid adjacent intervals.
- [x] 2.2 Implement public read-only parking availability endpoint returning coordinates and reservation-derived state for a range or current instant; verify integration tests cover empty inventory, overlap, cancellation, and invalid input.
- [x] 2.3 Implement React booking client and parking map/criteria form for registered vehicles, automatic assignment and manual selection; verify UI tests cover displayed positions, validation, no spaces, refresh, and fetch failure. (Accepted by user with the booking-map test run waived.)

## 3. Reservations and Security

- [x] 3.1 Implement centralized reservation creation service with SQL Server per-space update locks, overlap recheck and commit-before-confirmation for both assignment modes; verify two simultaneous LocalDB requests cannot commit overlapping bookings and adjacent intervals can coexist.
- [x] 3.2 Implement authenticated reservation create/list endpoints with owner-vehicle checks and 400/409/error responses; verify integration tests cover success, nonowner vehicle, conflict, unavailable storage, and anonymous access.
- [x] 3.3 Implement owner-only cancellation that preserves history and releases availability; verify integration tests cover own, foreign, missing, already cancelled, and subsequent rebooking requests.
- [x] 3.4 Add antiforgery token issuance/validation for new cookie-authenticated mutations and client token submission; verify forged or missing-token writes are rejected and legitimate requests succeed.
- [x] 3.5 Add React booking confirmation, own-reservation list, cancellation and refreshed availability states; verify UI tests cover successful and failing operations and no false success messaging.

## 4. Administrator Statistics

- [x] 4.1 Enable Identity role authorization and document/provide trusted operator-only `Administrator` role assignment without exposing a public grant API; verify ordinary registration/login does not elevate rights and provisioned role claims work after login.
- [x] 4.2 Implement administrator-only UTC booking totals and daily status counts endpoint; verify tests cover role restrictions, cancellation counts, empty data, invalid range, and reporting failures.
- [x] 4.3 Add React role-gated booking statistics view with empty/error states; verify ordinary users cannot see the navigation and admin UI tests cover results.

## 5. End-to-End Validation

- [x] 5.1 Document booking endpoints, reservation-based availability limits, inventory provisioning, admin provisioning and rollout/rollback in README; verify documented commands and request examples are usable.
- [x] 5.2 Run `dotnet build`, `dotnet test ParkingRealm.Tests`, `npm test -- --watch=false` and `npm run build` in `parking-realm`; verify all pass or report any environment-limited checks explicitly. (Accepted by user with the frontend test exclusion documented below.)

Validation results:
- Workspace build passed; all 67 backend tests passed.
- Frontend tests passed: 40 tests across 7 suites using `npm test -- --watchAll=false --runInBand --testPathIgnorePatterns=BookingPage.test.js`.
- `BookingPage.test.js` was excluded at the user's request. The user explicitly accepted tasks 2.3 and 5.2 as complete without this verification; the excluded tests have not been verified as passing.
- `npm run build` passed with a tooling deprecation warning for `fs.F_OK` (DEP0176).
- `openspec validate complete-booking-workflows --strict` passed.
