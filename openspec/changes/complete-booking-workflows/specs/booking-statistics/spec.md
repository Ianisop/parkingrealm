## Purpose

Gives authorized administrators booking counts and time-based trends derived from persisted reservations without disclosing booking data to ordinary customers.

## ADDED Requirements

### Requirement: Administrator can view booking counts and patterns
The system SHALL provide booking totals and a time-bucketed booking trend for a chosen valid UTC reporting interval based on persisted reservations, distinguishing active and cancelled reservations. Empty datasets SHALL be explicitly indicated rather than portrayed as meaningful trends.

#### Scenario: Bookings exist
- **WHEN** an authorized administrator selects a valid reporting interval containing recorded bookings
- **THEN** the dashboard shows total counts, active and cancelled counts, and counts grouped by day

#### Scenario: No bookings exist
- **WHEN** the interval contains no booking records
- **THEN** the dashboard shows zero counts and an explicit no-data message rather than a fabricated trend

#### Scenario: Invalid reporting interval
- **WHEN** the reporting interval is missing or invalid
- **THEN** the system rejects the request with a validation error

#### Scenario: Statistics cannot be retrieved
- **WHEN** booking data cannot be retrieved
- **THEN** the administrator sees a failure message instead of misleading statistics

### Requirement: Booking statistics require trusted administrator privileges
The system SHALL restrict booking statistics at the API boundary to a role granted through a trusted, non-public administration mechanism; public registration and ordinary login SHALL never grant that role.

#### Scenario: Ordinary or anonymous request
- **WHEN** an ordinary authenticated user or anonymous caller requests booking statistics
- **THEN** the system refuses access

#### Scenario: Trusted administrator request
- **WHEN** an authenticated account with an explicitly provisioned administrator role requests statistics
- **THEN** the system provides the permitted aggregate data
