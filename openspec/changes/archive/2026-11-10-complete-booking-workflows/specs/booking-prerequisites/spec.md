## Purpose

Provides a managed inventory of parking spaces and user-owned vehicles so that reservations can reference real spaces and the authenticated customer's vehicle.

## ADDED Requirements

### Requirement: Parking inventory has stable map positions
The system SHALL store each parkable space with a stable identifier and unique row/column coordinates for the two-dimensional overview; inventory SHALL be populated through trusted deployment configuration rather than customer requests.

#### Scenario: Configured inventory
- **WHEN** a parking house inventory is provisioned
- **THEN** each space has a unique position and identifier available to the overview and booking flows

#### Scenario: Empty inventory
- **WHEN** no spaces have been provisioned
- **THEN** the overview reports no bookable spaces and booking cannot succeed

### Requirement: Customer can register a vehicle for booking
The system SHALL let an authenticated customer register a vehicle with a nonblank registration number, persist its ownership, and retrieve only their own vehicles; it SHALL reject duplicate registrations for the same owner.

#### Scenario: Register and list own vehicle
- **WHEN** a logged-in customer registers a valid registration number and retrieves their vehicles
- **THEN** the vehicle is stored under their account and appears in their list

#### Scenario: Invalid or duplicate registration
- **WHEN** a customer registers a blank or already registered number on their account
- **THEN** the system rejects the request without creating a duplicate vehicle

#### Scenario: Another customer's vehicles
- **WHEN** a customer requests the list of their vehicles
- **THEN** vehicles owned by other customers are not returned

#### Scenario: Unauthenticated vehicle request
- **WHEN** an anonymous caller attempts to register or list vehicles
- **THEN** the system denies access
