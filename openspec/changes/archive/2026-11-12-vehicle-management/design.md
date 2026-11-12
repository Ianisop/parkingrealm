# Design

## Context

See proposal.md and specs/booking-prerequisites/spec.md. `VehicleEndpoints` currently accepts trimmed nonblank numbers up to 32 characters and enforces per-owner case-insensitive uniqueness. `BookingPage` provides the registration form and vehicle selector; `App` has no customer vehicle navigation. `Reservation` references a vehicle through an owner-scoped foreign key with restricted deletion, including for cancelled reservations. Reservation creation checks vehicle ownership before its transaction, so deletion can race with booking. The React booking client sends antiforgery tokens only for POST and treats every 409 as a parking-space conflict. The development and base settings currently contain a machine-specific SQL Express connection despite the README describing LocalDB.

## Goals / Non-Goals

**Goals:**
- Keep one registry-backed registration path for both vehicle management and booking, and preserve the existing owned-vehicle API response shape.
- Preserve existing vehicles and all reservations; enforce the deletion rule even under concurrent requests.
- Make the registry service and local SQL Server setup reproducible without secrets.

**Non-Goals:**
- Integrating a real registry provider, billing, vehicle editing, or retroactively validating existing vehicles.
- Cascading deletion, cancelling reservations, or adding a soft-delete lifecycle.
- Changing production database credentials or migrating existing data to LocalDB.

## Decisions

1. **Separate mock HTTP service.** Add a small separate .NET minimal API project with `GET /vehicles/{registrationNumber}` and three deterministic records, e.g. `AB12345`, `CD12345`, and `EF12345`. Normalize lookup using trim and invariant uppercase. Return JSON containing the registration number for a match and 404 for an unknown number. Registration number is the required registry field because the current vehicle model stores no other vehicle attributes. The mock is an explicitly development-only substitute, not an authoritative real registry. This exercises an actual service boundary; an in-process hardcoded dictionary alone would not exercise service availability or configuration.
2. **Server-side registry adapter.** Configure an HTTP client base URL and timeout, with the development setting pointing at the mock; never expose credentials in source or call the registry directly from the browser. Preserve existing trim, length, duplicate, and owner validation; after validation and duplicate check, query the registry and require a nonempty returned registration number matching the normalized request before saving. Treat not-found, malformed or mismatched data, errors, timeouts, and connection failures as failed registrations; do not save first. Keep case-insensitive per-owner uniqueness and its database race handling. Retain the existing response shape and original trimmed registration casing for compatibility. A later real provider can replace the adapter without bypassing server enforcement.
3. **Owner-scoped deletion with database protection.** Add authenticated `DELETE /api/vehicles/{id}` with the existing antiforgery filter. Query only the caller's vehicle and return 404 for missing or nonowned IDs. Reject any reservation reference with a distinguishable 409 and leave all records untouched. Retain the restrictive foreign key as the final race guard: if a reservation wins after the check, translate the relevant SQL foreign-key conflict into the same deletion conflict; if deletion wins after the booking precheck, translate the corresponding booking foreign-key failure to the existing vehicle-not-found outcome rather than an unhandled error. Recheck as needed to avoid misclassifying unrelated database failures. An application-only existence check would leave a race; cascading deletes would lose history.
4. **Shared vehicle flow in React.** Add a user-accessible vehicle overview reachable from booking and back, including an empty state, registration, deletion, and operation-specific feedback. Keep booking registration functional via the same API and refresh the booking selector when returning to booking so deleted vehicles are not retained. Extend the shared client to attach antiforgery tokens to DELETE and handle an empty success response; map vehicle deletion 409 separately from the existing parking-space conflict. Preserve administrator navigation and session behavior.
5. **Explicit LocalDB configuration.** Set the development `DefaultConnection` to `(localdb)\MSSQLLocalDB` with a dedicated `ParkingRealm` database, integrated authentication, and a trusted local certificate. Remove the machine-specific default from base settings so other environments supply their own connection via configuration, user secrets, or environment variables. Do not change the test factory's isolated LocalDB connection. Existing EF migrations are applied to the selected local database; no schema migration is expected for this feature. Switching the configured database does not copy data from SQL Express.
6. **Test the boundary, not the public internet.** Use a controlled fake HTTP handler or test-hosted registry in API integration tests, with deterministic success, unknown, malformed, mismatched, timeout, and failure cases; separately exercise the three mock records over HTTP. Update existing booking fixtures to inject a successful registry response rather than require a separately running service. Test deletion ownership, antiforgery, any reservation status, database failure, and concurrent booking/deletion against SQL Server LocalDB. React tests mock the client and cover navigation, registration, delete feedback, and booking refresh.

## Risks / Trade-offs

- [Only three mock plates work locally] -> Document the records, service startup, failure behavior, and distinction from a production registry.
- [Existing registration behavior changes] -> Keep previously stored vehicles intact and update tests and documentation; a registry outage blocks only new registrations.
- [Delete/booking race or unrelated SQL error] -> Keep the foreign key, handle only relevant constraint failures, and exercise independent concurrent requests.
- [LocalDB points to a new empty database] -> Document migrations and explicitly state that existing SQL Express data is not transferred; never drop the previous database.
- [Development mock mistaken for production] -> Do not configure it as a base/production default; require an explicitly configured provider outside development.

## Migration Plan

1. Back up existing data if switching instances matters; configure and start LocalDB, apply the existing migrations, and configure inventory separately as documented. Do not delete or overwrite the SQL Express database.
2. Start the mock registry and API with the development configuration; verify registration, booking, and deletion. Supply an explicit connection and appropriate registry service in other environments.
3. Deploy backend and frontend together because registration now requires lookup and the new UI uses DELETE. On rollback, restore the previous application/configuration while retaining vehicle and reservation data; the mock service can be stopped after no clients depend on it.