# Proposal

## Why

UC03 and UC04 require customers to manage their own vehicles and validate registration numbers through a registry. The existing booking workflow only supports local registration and listing, with no registry lookup or deletion.

## What Changes

- Require a successful registration-number lookup before saving a vehicle, with clear errors and no partial registration on invalid input, missing registry data, unavailable service, or database failure.
- Provide an authenticated vehicle overview with registration and deletion; preserve booking integration, ownership isolation, duplicate handling, and antiforgery protection.
- Reject deletion when any reservation, including cancelled or past reservations, references the vehicle; retain booking history and report failures to the customer.
- Add a separate local mock registration-number API with three deterministic vehicles and configure the application to call it server-side.
- Change the development SQL Server connection to LocalDB and document local startup, configuration, migrations, and testing without committing credentials.
- **BREAKING**: Vehicle registration now depends on a successful lookup; numbers previously accepted solely because they were nonblank may be rejected.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `booking-prerequisites`: Extend existing customer vehicle registration and listing with external lookup, a vehicle overview, and ownership-safe deletion that preserves referenced reservations.

## Impact

- ASP.NET Core vehicle endpoints, registry client configuration, and SQL Server development configuration; a separate mock registry service.
- React navigation, vehicle overview, booking integration, API client, and error handling.
- Vehicle and reservation integration tests, frontend tests, local development documentation, and existing booking fixtures that register vehicles.