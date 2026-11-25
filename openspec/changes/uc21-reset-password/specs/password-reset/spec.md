# Spec Delta

## Purpose

Lets a user who has forgotten their password regain access by requesting a reset link, verifying identity through that time-limited single-use link, and choosing a new password (UC21 "Nulstil brugeradgangskode"). E-mail delivery is simulated.

## ADDED Requirements

### Requirement: User can request a password reset
The system SHALL provide a "Glemt adgangskode?" function on the login page where a user who is not logged in enters an e-mail and submits a reset request. Both ordinary users and administrators SHALL be able to request a reset for their own account.

#### Scenario: Open the forgot-password function
- **WHEN** a user who is not logged in chooses "Glemt adgangskode?" on the login page
- **THEN** the system shows a form with an e-mail field and a submit action

#### Scenario: Request for an existing account
- **WHEN** a user submits the e-mail of an existing, non-deleted account
- **THEN** the system creates a reset request for that account
- **AND** tells the user that an e-mail with a reset link has been sent

#### Scenario: Administrator requests a reset
- **WHEN** an administrator submits their own e-mail
- **THEN** the system creates a reset request exactly as for an ordinary user

#### Scenario: Missing or malformed e-mail
- **WHEN** a user submits an empty or malformed e-mail, including directly to the API
- **THEN** no reset request is created
- **AND** the user is told the e-mail is missing or invalid

### Requirement: Reset request does not reveal which e-mails are registered
The system SHALL give the same response to a reset request for an unknown e-mail or a deleted (anonymized) account as for an existing account, and SHALL NOT create a reset request in those cases.

#### Scenario: Unknown e-mail
- **WHEN** a user submits an e-mail that is not registered
- **THEN** the user is told that an e-mail with a reset link has been sent
- **AND** the response is indistinguishable from the response for a registered e-mail
- **AND** no reset link is issued

#### Scenario: Deleted account
- **WHEN** someone submits the former e-mail of a deleted user
- **THEN** the response is the same as for an unknown e-mail
- **AND** no reset link is issued

### Requirement: Reset link is delivered through a simulated e-mail
The system SHALL deliver the reset link for a valid request through a replaceable delivery step. The delivery in this version SHALL NOT send real e-mail and SHALL make the link available to the operator in the server log. The reset token SHALL NOT be returned in any API response or shown in the user interface.

#### Scenario: Link is issued for a valid request
- **WHEN** a reset request is created for an existing account
- **THEN** a reset link containing a reset token for that account is made available to the operator through the simulated delivery

#### Scenario: Token is not exposed to the requester
- **WHEN** a user or administrator requests a reset
- **THEN** no response from the system contains the reset token

### Requirement: Reset link verifies identity and lets the user set a new password
The system SHALL let the holder of a valid reset link enter and confirm a new password for the account the link was issued for, and SHALL NOT change any password unless the link's token is valid for that account. The user interface SHALL open the reset form from the reset link.

#### Scenario: Successful reset
- **WHEN** a user opens a valid reset link, enters a new password that passes validation, and confirms it with the same value
- **THEN** the system stores the new password
- **AND** tells the user that the password has been reset
- **AND** the user can log in with the new password and no longer with the old one

#### Scenario: Passwords do not match
- **WHEN** a user submits a new password and a confirmation that differ
- **THEN** the password is not changed
- **AND** the user is told the passwords do not match

#### Scenario: Token belongs to another account
- **WHEN** a reset is submitted with a token that was not issued for the submitted account
- **THEN** the password is not changed
- **AND** the user is told the reset link is invalid or expired

#### Scenario: Open reset link in the interface
- **WHEN** a user opens the application with a reset link
- **THEN** the system shows the form for entering and confirming a new password instead of the login page

### Requirement: New password must pass validation
The system SHALL apply the same password rules to a reset as to registration and SHALL leave the existing password and the reset request unchanged when the new password is rejected.

#### Scenario: Password violates the rules
- **WHEN** a user submits a new password that violates the password rules
- **THEN** the password is not changed
- **AND** the user is told the password does not meet the requirements
- **AND** the same reset link can still be used to submit a valid password within its validity period

#### Scenario: Missing fields sent directly to the API
- **WHEN** a reset request with a missing e-mail, token or password reaches the API without passing through the user interface
- **THEN** the API rejects it and nothing is changed

### Requirement: New password is stored securely and ends old sessions
The system SHALL store the new password only as a salted hash, SHALL NOT store or log it in plain text, and SHALL end any existing login sessions of the account when its password is reset.

#### Scenario: Password stored as hash
- **WHEN** a password reset succeeds
- **THEN** the database holds a salted hash of the new password and no plain-text password

#### Scenario: Existing session is ended
- **WHEN** an account's password is reset while a session for that account is still active
- **THEN** the system no longer treats that session as logged in on the next request

### Requirement: Reset requests expire and cannot be reused
The system SHALL accept a reset link for 30 minutes after it was issued, SHALL refuse it afterwards, and SHALL accept each link for only one successful reset.

#### Scenario: Expired request
- **WHEN** a user submits a new password using a reset link older than 30 minutes
- **THEN** the password is not changed
- **AND** the user is told the reset link is invalid or expired and can request a new one

#### Scenario: Reuse after successful reset
- **WHEN** a reset link has already been used successfully and is used again
- **THEN** the password is not changed
- **AND** the user is told the reset link is invalid or expired

#### Scenario: Tampered token
- **WHEN** a reset is submitted with a modified or made-up token
- **THEN** the password is not changed
- **AND** the user is told the reset link is invalid or expired

### Requirement: Failures are reported and leave the account unchanged
The system SHALL tell the user when the reset could not be completed because the link could not be verified or because of a technical error, SHALL NOT expose technical details, and SHALL NOT leave the account with a partially changed password.

#### Scenario: Password cannot be saved
- **WHEN** the new password cannot be stored because of a system or database error
- **THEN** the user is told the reset could not be completed and can try again
- **AND** the previous password still works
- **AND** no database or stack-trace details are shown

#### Scenario: Request cannot be processed
- **WHEN** a reset request cannot be processed because of a system error
- **THEN** the user is told the request could not be completed and can try again
- **AND** no technical details are shown
