# Spec Delta

## ADDED Requirements

### Requirement: Deleted accounts cannot log in
The system SHALL NOT log in a deleted (anonymized) account, and SHALL give the same response as for an unknown email so that the former existence of the account is not revealed. Sessions of an account that is later deleted SHALL no longer be treated as a logged-in user.

#### Scenario: Login to a deleted account
- **WHEN** someone submits the former email and password of a deleted user
- **THEN** the user is not logged in
- **AND** the response is the same as for an unknown email

#### Scenario: Existing session of a deleted user
- **WHEN** a user's account is deleted while the user still holds a session
- **THEN** the system no longer treats that session as logged in on the next request
