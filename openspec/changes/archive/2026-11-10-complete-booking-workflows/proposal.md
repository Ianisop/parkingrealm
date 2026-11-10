## Why

The repository currently implements account registration and login but has no parking inventory, vehicle registration, reservations, booking UI, or booking statistics. The open booking-related use cases (#5–#9 and #13) therefore cannot be completed; this change defines the end-to-end behavior and its prerequisites before implementation.

## What Changes

- Show current parking-space availability on a two-dimensional map and allow selection of a suitable space.
- Allow authenticated customers to provide a registered vehicle and time interval, choose automatic assignment or a specific available space, and create a persisted reservation without overlapping bookings.
- Show a customer's reservations and allow cancellation of their own active reservations, releasing availability for the affected interval.
- Show administrators booking counts and patterns without exposing the statistics to ordinary users.
- Introduce the minimum missing parking-space inventory and user-owned vehicle registration needed for these flows; use existing login sessions and keep the broader vehicle-management and administrator-account workflows outside this change.

## Capabilities

### New Capabilities

- `parking-availability`: Current status and two-dimensional parking-space overview (UC05).
- `booking-criteria`: Time, vehicle, and assignment-mode validation and filtering (UC08).
- `parking-reservations`: Manual selection, automatic assignment, concurrency-safe creation, listing, and cancellation (UC06, UC07, UC09).
- `booking-statistics`: Administrator-only booking totals and time-based patterns (UC13).
- `booking-prerequisites`: Minimal parking inventory and user-owned vehicle registration required to use booking.

### Modified Capabilities

- `user-login`: Preserve ordinary-user permissions and prohibit self-service elevation, while allowing a session to carry an administrator role explicitly provisioned by a trusted operator before login.

## Impact

Affects the .NET 10 minimal API (`ParkingRealm/Endpoints`, `Program.cs`), EF Core SQL Server model and migrations (`ParkingRealm/Data`), React UI and API client (`parking-realm/src`), and backend/frontend tests. No new external service is assumed; live sensor/occupancy integration, full vehicle management (UC03/UC04), and administrator provisioning (UC10/UC11) are separate concerns. An administrator-only statistics endpoint requires an explicitly trusted role assignment mechanism, not self-service privilege escalation.
