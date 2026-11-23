## Purpose

Defines the minimum booking inputs and validation shared by availability queries and reservation creation without inventing unspecified parking preference rules.

## ADDED Requirements

### Requirement: Booking requires valid criteria
The system SHALL require a future start and end time with end later than start, a vehicle registered to the authenticated customer, and either automatic assignment or selection of a particular space. All supplied instants SHALL be interpreted consistently as UTC.

#### Scenario: Submit valid criteria
- **WHEN** a customer supplies a future UTC interval, their vehicle, and a valid assignment mode
- **THEN** the system uses those criteria when looking for spaces and creating the reservation

#### Scenario: Missing or invalid criteria
- **WHEN** the interval is missing, in the past, or non-positive, or the assignment mode is invalid
- **THEN** the system rejects the request with actionable validation errors and creates no reservation

#### Scenario: Another customer's vehicle
- **WHEN** a customer specifies a vehicle registered to a different customer
- **THEN** the system refuses the booking without revealing the other customer's vehicle details

#### Scenario: No matching spaces
- **WHEN** no spaces are available for the supplied interval and assignment mode
- **THEN** the customer is told that no suitable space is available and no reservation is created
