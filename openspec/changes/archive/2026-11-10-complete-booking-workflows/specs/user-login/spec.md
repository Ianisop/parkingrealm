## MODIFIED Requirements

### Requirement: Logged-in users have ordinary user rights only
The system SHALL give an ordinary registered user only ordinary user rights after login. Login and public registration SHALL NOT assign elevated roles. Login SHALL carry an administrator role only when a trusted operator explicitly provisioned that role on the existing account before authentication.

#### Scenario: No elevated rights after login
- **WHEN** a user without a provisioned administrator role logs in
- **THEN** the login state carries no role or permission beyond that of an ordinary user

#### Scenario: Operator-provisioned administrator login
- **WHEN** an existing account with an explicitly operator-provisioned administrator role logs in
- **THEN** the session carries that role and can access administrator-only booking statistics

#### Scenario: Public requests cannot grant roles
- **WHEN** a caller supplies role fields during public registration or login
- **THEN** those fields do not grant any elevated role
