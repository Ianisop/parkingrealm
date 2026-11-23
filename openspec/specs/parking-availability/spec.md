# parking-availability Specification

## Purpose

Shows the reservation-based status of configured parking spaces in a two-dimensional view so customers can discover candidate spaces before booking.

## Requirements

### Requirement: Current space overview is available
The system SHALL display the configured spaces in a two-dimensional layout with each space's identifier and reservation-based free/reserved status for the requested interval, defaulting to the current instant when no interval is provided. It SHALL not label a physically occupied space as known free without physical occupancy data.

#### Scenario: View parking map
- **WHEN** a customer opens the parking overview for a valid interval
- **THEN** the system fetches current status and displays each space at its configured position with its status

#### Scenario: Reserved space
- **WHEN** an active reservation overlaps the requested interval for a space
- **THEN** that space is marked unavailable for booking

#### Scenario: Invalid interval
- **WHEN** the overview is requested with an invalid time interval
- **THEN** the request is rejected with a validation error rather than showing misleading availability

### Requirement: Overview failures do not imply availability
The system SHALL indicate when status cannot be fetched and SHALL check availability again during booking rather than treating a previously rendered status as authoritative.

#### Scenario: Status service unavailable
- **WHEN** the overview cannot retrieve current reservation status
- **THEN** the customer sees an error rather than unverified spaces marked free

#### Scenario: Availability changes after display
- **WHEN** another customer books a displayed free space before confirmation
- **THEN** the original overview does not guarantee that the space can still be booked
