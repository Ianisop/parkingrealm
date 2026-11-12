# Spec Delta

## MODIFIED Requirements

### Requirement: Customer can register a vehicle for booking
The system SHALL let an authenticated customer register a vehicle with a valid registration number after a successful external registry lookup, persist its ownership, and retrieve only their own vehicles; it SHALL reject duplicate registrations for the same owner.

#### Scenario: Register and list own vehicle
- **WHEN** a logged-in customer registers a valid registration number found in the registry and retrieves their vehicles
- **THEN** the vehicle is stored under their account and appears in their list and is available for booking

#### Scenario: Invalid or duplicate registration
- **WHEN** a customer registers a blank, invalid, or already registered number on their account
- **THEN** the system rejects the request without creating a vehicle and tells the customer registration failed

#### Scenario: Another customer's vehicles
- **WHEN** a customer requests the list of their vehicles
- **THEN** vehicles owned by other customers are not returned

#### Scenario: Unauthenticated vehicle request
- **WHEN** an anonymous caller attempts to register or list vehicles
- **THEN** the system denies access

#### Scenario: Registry has no complete matching record
- **WHEN** a valid registration number is not found or the registry returns missing, invalid, or mismatched required information
- **THEN** the system does not create a vehicle and tells the customer registration failed

#### Scenario: Registry unavailable
- **WHEN** the registry cannot be reached, times out, or returns an error during lookup
- **THEN** the system does not create a vehicle and tells the customer registration failed

#### Scenario: Registration cannot be saved
- **WHEN** the registry lookup succeeds but the database cannot save the vehicle
- **THEN** the system does not report success or leave a partially registered vehicle and tells the customer registration failed

## ADDED Requirements

### Requirement: Customers can manage their vehicle overview
The system SHALL provide an authenticated overview of the customer's own vehicles with controls to register and delete vehicles and feedback for each operation.

#### Scenario: View and register a vehicle
- **WHEN** a customer opens their vehicle overview and successfully registers a vehicle
- **THEN** the overview displays the new vehicle without showing another customer's vehicles

#### Scenario: Vehicle list cannot be loaded
- **WHEN** the customer's vehicle list cannot be loaded
- **THEN** the overview reports the failure instead of presenting it as an empty list

### Requirement: Customers can delete an unreferenced vehicle
The system SHALL permit an authenticated customer to delete their own vehicle only when no reservation references it, and SHALL preserve all reservation history.

#### Scenario: Delete own unreferenced vehicle
- **WHEN** a customer requests deletion of their own vehicle with no associated reservations
- **THEN** the vehicle is removed and no longer appears in their overview or booking selection

#### Scenario: Vehicle has a reservation
- **WHEN** a customer requests deletion of a vehicle referenced by any active, cancelled, past, or future reservation
- **THEN** deletion is rejected, the customer is told why, and the vehicle and all reservations remain unchanged

#### Scenario: Unknown or another customer's vehicle
- **WHEN** a customer requests deletion of a missing vehicle or another customer's vehicle
- **THEN** the system refuses without changing any vehicle or revealing the other customer's ownership

#### Scenario: Anonymous or unprotected deletion
- **WHEN** a caller requests deletion without authentication or a valid antiforgery token
- **THEN** the system refuses the request without deleting a vehicle

#### Scenario: Deletion cannot be saved
- **WHEN** a database or system error prevents deletion
- **THEN** the system does not report success and informs the customer

#### Scenario: Concurrent booking and deletion
- **WHEN** a booking and deletion for the same unreferenced vehicle are attempted concurrently
- **THEN** the system preserves referential integrity, does not report both incompatible operations as successful, and gives a clear failure for the unsuccessful operation