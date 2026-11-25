# Spec Delta

## ADDED Requirements

### Requirement: Administrator can send a password reset link to a user
The system SHALL let an administrator trigger a password reset link for any listed user, including administrator accounts, without the administrator being able to see, choose or change the password or see the reset token. The function SHALL be restricted to administrators, and the request SHALL require a valid request verification token.

#### Scenario: Send a reset link
- **WHEN** an administrator chooses "Send nulstillingslink" for a listed user
- **THEN** the system issues a reset link for that user through the same simulated e-mail delivery as a self-service reset
- **AND** tells the administrator that a reset link has been sent to the user's e-mail
- **AND** the user's current password keeps working until the user completes the reset

#### Scenario: Reset link for an administrator account
- **WHEN** an administrator triggers a reset link for an administrator account, including their own
- **THEN** the system issues the reset link in the same way as for an ordinary user

#### Scenario: Administrator sees no secrets
- **WHEN** an administrator triggers a reset link
- **THEN** no response contains the reset token or any password

#### Scenario: Reset link is time-limited and single-use
- **WHEN** a reset link was issued by an administrator
- **THEN** it expires and can be used only once under the same rules as a self-service reset link

#### Scenario: User does not exist
- **WHEN** an administrator triggers a reset link for a user that does not exist or has been deleted
- **THEN** the system reports that the user was not found and issues no link

#### Scenario: Anonymous request
- **WHEN** an unauthenticated caller requests a reset link through the administration function
- **THEN** the system refuses access and issues no link

#### Scenario: Ordinary user request
- **WHEN** an authenticated user without the administrator role requests a reset link through the administration function
- **THEN** the system refuses access and issues no link

#### Scenario: Missing request verification token
- **WHEN** an administrator's request to send a reset link lacks a valid request verification token
- **THEN** the system rejects the request and issues no link

#### Scenario: Reset action offered in the user list
- **WHEN** an administrator views the user list
- **THEN** every listed user, including administrator accounts, offers the "Send nulstillingslink" action
