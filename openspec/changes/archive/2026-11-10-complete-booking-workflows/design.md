## Context

See proposal.md and the five delta specs for scope. The existing .NET 10 minimal API has Identity cookie login and SQL Server/EF Core, but no parking or vehicle entities; React only shows account pages. `ParkingRealm.Tests/ApiFactory.cs` applies migrations to LocalDB; the UI has React Testing Library tests. `IdentityDbContext<ApplicationUser>` already has role tables, but the application does not configure role services or provision an administrator. The existing user-login spec forbids granting elevated rights through ordinary login.

## Goals / Non-Goals

**Goals:** Preserve cookie-based authentication and existing account flows; persist auditable reservations; make overlapping reservations impossible under concurrent API calls; give customers a responsive reservation-based map and administrators a protected aggregate dashboard.

**Non-Goals:** Live physical-occupancy sensor integration, payments, full vehicle-management workflows, public inventory editing, public admin-role assignment, and booking preference types not specified by UC08.

The administrator implementation follows the trusted-role approach already selected in decision 5. The user-login delta clarifies the exception for explicitly provisioned administrators; ordinary login still never assigns a role. Provisioning is an operator-only command that exits without starting the web server.

## Decisions

1. **Data model and provisioning.** Add `ParkingSpace` (stable ID, unique row/column), `Vehicle` (owner ID, normalized registration number unique per owner), and `Reservation` (owner ID, vehicle ID, space ID, UTC start/end, status, created/cancelled timestamps). Add EF relationships, indices for overlapping-interval queries and reporting, and a migration. Populate spaces via trusted operator-controlled seed/configuration, not an unauthenticated write endpoint; show empty inventory until configured. A single in-memory map was rejected because reservations must survive restarts and support concurrent clients.
2. **Availability semantics and time.** Use half-open UTC intervals `[start, end)` and filter active reservations by `existing.Start < requested.End && requested.Start < existing.End`; when no interval is supplied, use the current instant and check `existing.Start <= now && now < existing.End`. Return map coordinates and a reservation-derived available/unavailable status from the API; present it as reservation availability, not sensor-confirmed physical occupancy. Re-fetch on changed criteria and at confirmation. Live streaming/polling is not assumed: refresh on interaction and after booking/cancellation to avoid stale success claims. Validate future booking ranges and ownership server-side; client checks only improve feedback.
3. **Concurrency.** In a SQL Server transaction, lock the selected parking-space row using `UPDLOCK, HOLDLOCK`, re-query overlaps, and insert before commit; every booking path uses this same lock protocol. Automatic assignment inspects candidates in deterministic ID order, locks a candidate and rechecks it, and continues if taken. Transaction errors are not treated as success; return a conflict on competing reservations, and handle DB failures separately. A simple check-then-insert or unique `(space,start)` constraint was rejected because each permits overlaps with different start times. Test using independent clients/DbContexts hitting LocalDB simultaneously.
4. **API and UI boundaries.** Add authenticated `GET/POST /api/vehicles`, public read-only `GET /api/parking-spaces?start=&end=`, authenticated `GET/POST /api/reservations`, and `POST /api/reservations/{id}/cancel`; all vehicle and reservation operations require login. API responses use 400 for invalid criteria, 401/403 for access, 404 without leaking ownership for foreign reservation IDs, and 409 for no availability/conflict. React authenticated view gains vehicle setup, date/time criteria, a coordinate-based selectable map, confirmation and own-reservation list with cancel action; handle unavailable/error/empty states in Danish. Mutating cookie-authenticated requests must be CSRF-protected (antiforgery token passed by the React client), not just CORS-restricted. Alternative of relying solely on SameSite cookies was rejected because same-site cross-origin requests can still be forged.
5. **Administrator-only aggregates.** Enable Identity role services and require an `Administrator` policy at `/api/admin/booking-statistics` and in the UI navigation. Aggregate persisted bookings within the requested UTC reporting interval by start date, including counts by status; report empty data explicitly. Grant the role only through a trusted operator-run provisioning path using Identity APIs; do not add a public role grant endpoint or upgrade normal login. Re-authentication after a role grant refreshes cookie claims. The existing Identity role tables avoid a new authorization package. Test ordinary and anonymous access alongside trusted-role access.

## Risks / Trade-offs

- [Unknown real-space physical occupancy] → Label computed availability as reservation-based; do not claim sensors exist, and check immediately before commit.
- [Inventory layout is not documented] → Require explicit operator-supplied row/column inventory before booking; do not invent the real garage layout.
- [Lock protocol bypassed by future writers] → Centralize reservation creation in one service, document locking invariant, and add real SQL Server concurrent-request tests.
- [LocalDB or deployment SQL Server unavailable] → Keep integration tests separate from frontend checks and report infrastructure failures rather than silently accepting reservations.
- [Privileged role misconfiguration] → Make stats fail closed until an operator provisions a role; never make role assignment part of self-registration.
- [Additional cancellation/payment rules unspecified] → Permit cancellation of active own reservations without an invented deadline; later policy needs its own specification.

## Migration Plan

1. Apply EF migration for vehicles, spaces, reservations and indexes; preserve existing users and login behavior.
2. Configure physical inventory from trusted deployment data and provision any administrator via operator-controlled Identity role assignment, not a public request; verify role authorization and map status before enabling the UI.
3. Deploy API and React client together, including antiforgery token flow; test registration/login, booking contention, cancellation and authorization against SQL Server.
4. For rollback, disable booking routes/UI first; leave the additive schema and existing reservations intact for recovery, rather than dropping booking data automatically.
