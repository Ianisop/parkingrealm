# parking-reservations Specification

## Purpose

Lets authenticated customers choose or receive a parking space, persist a non-overlapping reservation for their vehicle, and cancel their own reservations.

## Requirements

### Requirement: Customer can select a space or request automatic assignment
The system SHALL allow an authenticated customer to select a space from the two-dimensional overview or choose automatic assignment, carrying that choice into booking.

#### Scenario: Select a displayed space
- **WHEN** a customer chooses a space shown on the map for the requested interval
- **THEN** the selected space is submitted for booking with the customer's criteria

#### Scenario: Automatic assignment
- **WHEN** a customer chooses automatic assignment
- **THEN** the system selects an available configured space for the requested interval

### Requirement: Booking is persisted without overlapping active reservations
The system SHALL verify availability at confirmation and atomically prevent two active reservations of the same space from having intersecting half-open intervals [start, end), including concurrent requests. It SHALL not confirm a reservation until it has been committed to storage.

#### Scenario: Successful reservation
- **WHEN** an authenticated customer submits valid criteria for an available space
- **THEN** a reservation is saved for their registered vehicle and space and its details are confirmed to the customer

#### Scenario: Space taken after display
- **WHEN** the selected space becomes reserved for an overlapping interval before confirmation
- **THEN** the conflicting request fails without a reservation and the customer is told to select another space

#### Scenario: Concurrent booking attempts
- **WHEN** two customers try to reserve the same space for intersecting intervals at the same time
- **THEN** at most one conflicting reservation is committed and the other customer receives a conflict response

#### Scenario: Adjacent intervals
- **WHEN** a reservation ends exactly when another begins for the same space
- **THEN** the intervals do not conflict

#### Scenario: Booking cannot be saved
- **WHEN** storage fails during confirmation
- **THEN** the customer is not told the reservation succeeded

### Requirement: Customers can view and cancel their own reservations
The system SHALL show a customer's own reservations and allow them to cancel an active reservation they own; cancelled reservations SHALL remain recorded but SHALL no longer block space availability. No unspecified cancellation deadline SHALL be imposed.

#### Scenario: List reservations
- **WHEN** a logged-in customer requests their reservations
- **THEN** the system returns only that customer's reservations with their current status

#### Scenario: Cancel own reservation
- **WHEN** a customer cancels their own active reservation
- **THEN** the system persists its cancelled status, confirms cancellation, and releases its interval for other bookings

#### Scenario: Cancel someone else's reservation
- **WHEN** a customer attempts to cancel another customer's reservation
- **THEN** the system refuses without changing that reservation

#### Scenario: Repeated cancellation or missing reservation
- **WHEN** a reservation is already cancelled or no longer exists
- **THEN** the customer receives a clear failure response without changing other reservations

#### Scenario: Anonymous reservation request
- **WHEN** an unauthenticated caller tries to create, list, or cancel reservations
- **THEN** the system denies access
