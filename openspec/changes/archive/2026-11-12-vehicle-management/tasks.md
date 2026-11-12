# Tasks

## 1. Local Development Setup

- [x] 1.1 Set the development connection to the `ParkingRealm` LocalDB database and remove the machine-specific base default; verify configuration overrides and apply existing migrations to LocalDB without altering the SQL Express database.
- [x] 1.2 Update the database setup and deployment instructions in the README, including the lack of automatic data transfer and how to provide non-development connections; verify the documented migration and startup commands against LocalDB.

## 2. Registry Mock and Registration

- [x] 2.1 Create a separate mock registry HTTP project with three documented deterministic plates, case-insensitive lookup, and a not-found response; verify all three records and an unknown number via HTTP.
- [x] 2.2 Add configurable server-side registry lookup with timeout and development mock settings; test matching, unknown, missing, malformed, mismatched, unavailable, error, and timeout responses without relying on the public network.
- [x] 2.3 Integrate registry validation before vehicle persistence while retaining owner isolation, antiforgery, existing validation, case-insensitive duplicate protection, and the response shape; test success, failures without persistence, database errors, and duplicate races.
- [x] 2.4 Update existing booking and vehicle test fixtures to inject a controlled registry response; run the affected API tests and verify booking tests do not depend on a running mock service.
- [x] 2.5 Document the mock startup, port, three sample plates, configuration, and production limitations; run the documented HTTP lookup and registration flow.

## 3. Safe Vehicle Deletion

- [x] 3.1 Add owner-scoped authenticated DELETE with antiforgery and clear missing/nonowner and reservation-conflict responses; test successful deletion, anonymous and invalid-token calls, cross-user access, missing vehicles, active/cancelled/past reservations, and storage failure.
- [x] 3.2 Handle the relevant restrictive foreign-key races in deletion and reservation creation without masking unrelated database errors; run a LocalDB concurrency test with independent requests and verify history and referential integrity remain intact.
- [x] 3.3 Update the API documentation for DELETE, antiforgery, conflict behavior, and preservation of reservation history; verify the documented status codes match integration tests.

## 4. Vehicle Overview and Booking

- [x] 4.1 Add authenticated customer navigation and an overview with loading, empty, registration, deletion, and failure states; test ownership-safe listing, registration and deletion feedback, and navigation for customers and administrators.
- [x] 4.2 Extend the shared API client to send antiforgery tokens for DELETE, accept empty successful responses, and report vehicle conflicts separately from parking conflicts; test each response and token path.
- [x] 4.3 Keep registration from booking using the registry-backed API and refresh the booking vehicle selection after returning from the overview; test successful registration, failed lookup, and removal of deleted vehicles from the selector.

## 5. End-to-End Verification

- [x] 5.1 Run the backend and React test suites; resolve regressions related to this change and record any environment blockers.
- [x] 5.2 With LocalDB and the mock service running, verify login, registration of a known plate, rejection of an unknown plate, booking, blocked deletion for a referenced vehicle, successful deletion of an unreferenced vehicle, and failure feedback when the mock is unavailable.